using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AispaStudio.Api.Mail;

/// <summary>Ausgehende Mail. Versand immer über die Queue (<see cref="SendMail"/>), nie im HTTP-Aufruf.</summary>
/// <param name="InReplyTo">Message-ID der beantworteten Mail, damit Antworten im selben Verlauf landen (KI-Terminannahme).</param>
/// <param name="ReplyTo">Adresse für Antworten des Empfängers, z. B. das Postfach des Spas, wenn über den gemeinsamen Versand gesendet wird.</param>
/// <param name="FromName">Anzeigename des Absenders, z. B. der Name des Spas; ohne Angabe der aus <see cref="MailOptions"/>.</param>
/// <param name="Attachments">Anhänge, z. B. ein Beleg als PDF (ADR 0092); zusammen höchstens <see cref="MaxAttachmentBytes"/>.</param>
public sealed record OutgoingMail(
    string To,
    string Subject,
    string Text,
    string? Html = null,
    string? InReplyTo = null,
    string? ReplyTo = null,
    string? FromName = null,
    IReadOnlyList<MailAttachment>? Attachments = null)
{
    /// <summary>5 MB je Mail: mehr nehmen viele Postfächer nicht an, und die Queue trägt den Anhang mit.</summary>
    public const int MaxAttachmentBytes = 5 * 1024 * 1024;

    public int AttachmentBytes => Attachments?.Sum(a => a.Content.Length) ?? 0;

    public bool AttachmentsTooLarge => AttachmentBytes > MaxAttachmentBytes;
}

/// <summary>Ein Anhang einer Mail: Dateiname, Inhaltstyp (z. B. <c>application/pdf</c>) und die Bytes.</summary>
public sealed record MailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>Nachricht in der Queue: diese Mail versenden. Schlägt der Versand fehl, wiederholt die Queue ihn.</summary>
public sealed record SendMail(OutgoingMail Mail);

public interface IMailSender
{
    Task SendAsync(OutgoingMail mail, CancellationToken ct);
}

public static class SendMailHandler
{
    public static Task Handle(SendMail message, IMailSender sender, CancellationToken ct) => sender.SendAsync(message.Mail, ct);
}

/// <summary>SMTP-Zugang (Abschnitt <c>Mail</c>). Ohne <c>Smtp:Host</c> werden Mails nur ins Log geschrieben.</summary>
public sealed class MailOptions
{
    public const string Section = "Mail";

    public string FromAddress { get; set; } = "noreply@aispa.local";
    public string FromName { get; set; } = "AISPA Studio";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>„StartTls“ (Standard), „SslOnConnect“ oder „None“ (nur für lokale Test-Postfächer wie Mailpit).</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
}

internal sealed class SmtpMailSender(IOptions<MailOptions> options) : IMailSender
{
    public Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        var o = options.Value;
        return SmtpTransport.SendAsync(MailMessages.Build(mail, new MailboxAddress(mail.FromName ?? o.FromName, o.FromAddress)), o.Smtp, ct);
    }
}

/// <summary>Baut die MIME-Nachricht; gemeinsam für den Versand über SMTP und über das Postfach eines Spas (Graph).</summary>
internal static class MailMessages
{
    public static MimeMessage Build(OutgoingMail mail, MailboxAddress from)
    {
        var message = new MimeMessage();
        message.From.Add(from);
        message.To.Add(MailboxAddress.Parse(mail.To));
        if (mail.ReplyTo is { } replyTo)
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = mail.Subject;
        if (mail.InReplyTo is { } inReplyTo)
        {
            message.InReplyTo = inReplyTo;
            message.References.Add(inReplyTo);
        }

        if (mail.AttachmentsTooLarge)
        {
            throw new InvalidOperationException($"Anhänge dürfen zusammen höchstens 5 MB groß sein, hier {mail.AttachmentBytes / 1024} KB.");
        }

        var body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html };
        foreach (var attachment in mail.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        message.Body = body.ToMessageBody();
        return message;
    }
}

internal static class SmtpTransport
{
    public static async Task SendAsync(MimeMessage message, SmtpOptions smtp, CancellationToken ct)
    {
        using var client = await ConnectAsync(smtp, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>Verbinden und anmelden, ohne zu senden.</summary>
    public static async Task VerifyAsync(SmtpOptions smtp, CancellationToken ct)
    {
        using var client = await ConnectAsync(smtp, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static async Task<SmtpClient> ConnectAsync(SmtpOptions smtp, CancellationToken ct)
    {
        var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtp.Host!, smtp.Port, smtp.Security, ct);
            if (!string.IsNullOrEmpty(smtp.UserName))
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? "", ct);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}

/// <summary>
/// Ohne SMTP-Zugang: in der Entwicklung die Mail ins Log schreiben, damit Anmeldecodes und Links lokal sichtbar sind.
/// Außerhalb der Entwicklung nur warnen, denn Codes und Links dürfen nicht im Log landen.
/// </summary>
internal sealed partial class LoggingMailSender(ILogger<LoggingMailSender> logger, IHostEnvironment env) : IMailSender
{
    public Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        if (env.IsDevelopment())
        {
            Log(logger, mail.To, mail.Subject, mail.Text);
            if (mail.Attachments is { Count: > 0 } attachments)
            {
                var names = string.Join(", ", attachments.Select(a => $"{a.FileName} ({a.Content.Length / 1024} KB)"));
                LogAttachments(logger, names);
            }
        }
        else
        {
            NotSent(logger, mail.Subject);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mail an {To}: {Subject}\n{Text}")]
    private static partial void Log(ILogger logger, string to, string subject, string text);

    [LoggerMessage(Level = LogLevel.Information, Message = "Anhänge: {Attachments}")]
    private static partial void LogAttachments(ILogger logger, string attachments);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail „{Subject}“ nicht versendet: kein SMTP-Zugang eingerichtet (Mail:Smtp:Host).")]
    private static partial void NotSent(ILogger logger, string subject);
}

internal static class MailSetup
{
    public static IServiceCollection AddMail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.Section));
        if (string.IsNullOrWhiteSpace(configuration[$"{MailOptions.Section}:Smtp:Host"]))
        {
            services.AddSingleton<IMailSender, LoggingMailSender>();
        }
        else
        {
            services.AddSingleton<IMailSender, SmtpMailSender>();
        }

        return services;
    }
}

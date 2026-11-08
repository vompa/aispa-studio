using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Mailboxes;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>
/// Mails eines Spas an seine Gäste (Antworten der Terminannahme, Erinnerungen). Mit eigenem Postfach und eingeschaltetem
/// Versand gehen sie aus diesem Postfach; sonst über den gemeinsamen Versand, mit dem Namen des Spas als Absender und dem
/// eigenen Postfach als Antwortadresse, damit die Antwort des Gastes beim Spa landet.
/// </summary>
internal sealed class SpaMailSender(
    AispaDbContext db,
    TenantContext tenants,
    ISpaMailboxFactory factory,
    SpaMailboxSecrets secrets,
    IMailSender shared)
{
    private const int MaxError = 500;

    public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        var tenantId = tenants.TenantId ?? throw new InvalidOperationException("Kein Spa gesetzt.");
        var box = await db.SpaMailboxes.AsNoTracking().OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
        mail = mail with { FromName = mail.FromName ?? await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct) };
        if (box is not { Enabled: true })
        {
            await shared.SendAsync(mail, ct);
            return;
        }

        if (!SendsItself(box))
        {
            await shared.SendAsync(mail with { ReplyTo = mail.ReplyTo ?? box.Address }, ct);
            return;
        }

        string? error = null;
        try
        {
            var sender = factory.CreateSender(SpaMailboxSettings.Of(box, secrets.Unprotect(tenantId, box.Id, box.ProtectedSecret!)));
            await sender.SendAsync(mail, ct);
        }
        catch (SentCopyFailedException ex)
        {
            // Beim Gast angekommen: nicht wiederholen, nur beim Spa vermerken.
            error = ex.Message.Length > MaxError ? ex.Message[..MaxError] : ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Beim Spa sichtbar machen; die Queue bzw. die Erinnerung wiederholt oder hält den Fehler fest.
            error = ex.Message.Length > MaxError ? ex.Message[..MaxError] : ex.Message;
            throw;
        }
        finally
        {
            if (error != box.LastSendError)
            {
                await db.SpaMailboxes.Where(b => b.Id == box.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(b => b.LastSendError, error), CancellationToken.None);
            }
        }
    }

    /// <summary>Ob das Postfach selbst versendet: eingeschaltet, Geheimnis vorhanden und bei IMAP ein SMTP-Server.</summary>
    /// <summary>Kommt eine Mail wirklich hinaus? Der <see cref="LoggingMailSender"/> ohne SMTP-Zugang schreibt nur ins Log.</summary>
    public static bool CanDeliver(IMailSender shared, IEnumerable<SpaMailbox> boxes) =>
        shared is not LoggingMailSender || boxes.Any(b => b.Enabled && SendsItself(b));

    public static bool SendsItself(SpaMailbox box) =>
        box.SendReplies && box.ProtectedSecret is not null && (box.Kind == MailboxKind.Graph || !string.IsNullOrWhiteSpace(box.SmtpHost));
}

/// <summary>Nachricht in der Queue: Mail eines Spas an einen Gast, aus dem Postfach des Spas, wenn es selbst versendet.</summary>
public sealed record SendSpaMail(Guid TenantId, OutgoingMail Mail);

public static class SendSpaMailHandler
{
    // Eigener Scope mit dem Mandanten der Nachricht: ohne HTTP-Anfrage kennt der Scope sonst kein Spa.
    public static async Task Handle(SendSpaMail message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(message.TenantId);
        await scope.ServiceProvider.GetRequiredService<SpaMailSender>().SendAsync(message.Mail, ct);
    }
}

internal static class GuestMails
{
    /// <summary>Mail an einen Gast in die Queue: im Namen des aktuellen Spas (eigenes Postfach oder Antwortadresse), ohne Spa gemeinsam.</summary>
    public static ValueTask PublishGuestMailAsync(this IMessageBus bus, TenantContext tenants, OutgoingMail mail) =>
        tenants.TenantId is { } tenantId ? bus.PublishAsync(new SendSpaMail(tenantId, mail)) : bus.PublishAsync(new SendMail(mail));
}

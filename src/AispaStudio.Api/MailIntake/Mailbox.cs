using System.Collections.Concurrent;
using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake;

/// <summary>Eine Mail oder Kurznachricht, wie sie ankommt (Text, ohne Anhänge).</summary>
/// <param name="From">Mailadresse, bei SMS, WhatsApp und Anruf die Telefonnummer (E.164, z. B. +436601234567).</param>
/// <param name="SenderVerified">Das Postfach hat den Absender bestätigt (SPF/DKIM/DMARC bestanden); bei signierten Webhooks immer.</param>
public sealed record IncomingMail(
    string MessageId,
    string From,
    string? FromName,
    string To,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt,
    bool SenderVerified,
    BookingChannel Channel = BookingChannel.Email);

/// <summary>
/// Postfach, aus dem die Terminannahme liest. Austauschbar: IMAP, Microsoft Graph, Gmail-API oder ein Webhook
/// des Mail-Anbieters. Im Prototyp das <see cref="TestInbox"/>.
/// Gesendet wird über <see cref="AispaStudio.Api.Mail.IMailSender"/> (SMTP), denselben Weg wie die Online-Buchung.
/// </summary>
public interface IMailbox
{
    /// <summary>Neue, noch nicht als verarbeitet markierte Mails.</summary>
    Task<IReadOnlyList<IncomingMail>> FetchNewAsync(CancellationToken ct);

    /// <summary>Die Mail ist gespeichert und in der Queue; sie soll beim nächsten Abruf nicht mehr kommen.</summary>
    Task MarkProcessedAsync(string messageId, CancellationToken ct);
}

/// <summary>
/// Test-Postfach im Speicher: nimmt Mails über <see cref="Deliver"/> an. Für Prototyp, Entwicklung und Tests;
/// ein echtes Postfach ersetzt es per Konfiguration.
/// </summary>
public sealed class TestInbox : IMailbox
{
    private readonly ConcurrentDictionary<string, IncomingMail> _inbox = new();

    public void Deliver(IncomingMail mail) => _inbox[mail.MessageId] = mail;

    public Task<IReadOnlyList<IncomingMail>> FetchNewAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncomingMail>>([.. _inbox.Values.OrderBy(m => m.ReceivedAt)]);

    public Task MarkProcessedAsync(string messageId, CancellationToken ct)
    {
        _inbox.TryRemove(messageId, out _);
        return Task.CompletedTask;
    }
}

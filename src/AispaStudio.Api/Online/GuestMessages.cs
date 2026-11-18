using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Nachrichten zu Karte und Anzahlung an den Gast: per Mail, ohne Mailadresse per SMS an die Handynummer aus seiner Kundenkarte (Auftraggeber,
/// 18.11.2026). Eine leere Mailadresse an Hinterlegung oder Anzahlung heißt „per SMS“. Die SMS trägt den Betreff der Mail (schon in der Sprache
/// des Gastes) und, wo es einen gibt, den Link. Ein Fehler beim SMS-Versand bricht nichts ab: Belastung, Beleg und Erstattung sind dann schon
/// gebucht, das Team sieht den Stand am Termin.
/// </summary>
internal sealed partial class GuestMessages(
    AispaDbContext db,
    IMessageBus bus,
    TenantContext tenants,
    ITextMessageSender texts,
    IOptions<TextChannelOptions> channels,
    ILogger<GuestMessages> logger)
{
    /// <summary>Ist ein SMS-Anbieter eingerichtet?</summary>
    public bool CanText => !string.IsNullOrWhiteSpace(channels.Value.SmsProvider);

    /// <summary>Handynummer des Gastes im Format E.164, sonst <c>null</c>.</summary>
    public static string? Phone(Customer customer) => PhoneNumbers.Normalize(customer.Phone ?? "") is { Length: > 0 } phone ? phone : null;

    /// <summary>Lässt sich der Gast erreichen: Mailadresse, sonst Handynummer und SMS-Anbieter.</summary>
    public bool CanReach(Customer customer) => !string.IsNullOrWhiteSpace(customer.Email) || (CanText && Phone(customer) is not null);

    /// <summary>Die Mail, oder ohne Empfänger deren Betreff mit <paramref name="link"/> als SMS an <paramref name="customerId"/>.</summary>
    public async Task SendAsync(OutgoingMail mail, Guid customerId, string? link, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(mail.To))
        {
            await bus.PublishGuestMailAsync(tenants, mail);
            return;
        }

        await TextAsync(customerId, link is null ? mail.Subject : $"{mail.Subject}: {link}", ct);
    }

    /// <summary>Eine SMS an den Gast; ohne Nummer oder Anbieter bleibt es bei einem Eintrag im Log.</summary>
    public async Task TextAsync(Guid customerId, string text, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null || Phone(customer) is not { } phone || !CanText)
        {
            Unreachable(logger, customerId);
            return;
        }

        try
        {
            // Von der eigenen Nummer des Spas, damit eine Antwort des Gastes wieder beim richtigen Spa landet.
            var own = await IntakeRoutes.OwnAsync(db, BookingChannel.Sms, ct);
            await texts.SendAsync(new OutgoingText(BookingChannel.Sms, phone, text, From: own?.Address, SenderId: own?.SenderId), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            TextFailed(logger, customerId, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gast {CustomerId} hat weder Mailadresse noch Handynummer für eine SMS; Nachricht nicht gesendet.")]
    private static partial void Unreachable(ILogger logger, Guid customerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS an Gast {CustomerId} nicht gesendet: {Error}")]
    private static partial void TextFailed(ILogger logger, Guid customerId, string error);
}

using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>
/// Kartenhinterlegung für Termine, die die Rezeption gebucht hat (Telefon, vor Ort): Der Gast bekommt einen Link per Mail, stimmt
/// dort der Ausfallgebühr des Standorts zu und hinterlegt dann die Karte wie bei einer Online-Buchung. Ohne Zustimmung wird nie
/// belastet.
/// </summary>
internal sealed class CardRequests(
    AispaDbContext db,
    OnlineDbContext online,
    CardGuarantees cards,
    OnlineRules rules,
    OnlinePortal portal,
    AccessLinks links,
    TimeProvider time,
    GuestMessages messages)
{
    public abstract record RequestOutcome
    {
        public sealed record Sent(CardGuaranteeStaffDto Guarantee) : RequestOutcome;

        public sealed record NotFound : RequestOutcome;

        public sealed record Refused(string Message) : RequestOutcome;
    }

    /// <summary>Ob und mit welcher Gebühr sich für den Termin eine Karte anfordern lässt; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<CardRequestOffer?> OfferAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null)
        {
            return null;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var locationRules = await rules.ForAsync(location.Id, ct);
        var percent = await cards.PercentForAsync(locationRules, appointment.Treatment, appointment.CustomerId, ct);
        var fee = CardGuarantees.Fee(appointment.Price, percent);
        var existing = await online.CardGuarantees.AsNoTracking().Where(g => g.AppointmentId == appointmentId).Select(g => (CardGuaranteeStatus?)g.Status)
            .FirstOrDefaultAsync(ct);
        string? reason = (percent, existing) switch
        {
            ( <= 0, _) => "Für diese Behandlung ist keine Ausfallgebühr eingestellt, sie gilt nur für neue Gäste, oder es ist kein Zahlungsanbieter eingerichtet.",
            (_, { } status) when status != CardGuaranteeStatus.AwaitingConsent => "Für diesen Termin gibt es schon eine Kartenhinterlegung.",
            _ when appointment.Status != AppointmentStatus.Booked || appointment.Start <= time.GetUtcNow() => "Nur für bevorstehende, gebuchte Termine.",
            _ when !messages.CanReach(appointment.Customer) => messages.CanText
                ? "Für den Gast ist weder eine E-Mail-Adresse noch eine Handynummer hinterlegt."
                : "Für den Gast ist keine E-Mail-Adresse hinterlegt, und SMS sind nicht eingerichtet.",
            _ when fee <= 0 => "Der Termin hat keinen Preis.",
            _ => null,
        };
        return new CardRequestOffer(reason is null, fee, location.Currency, percent, reason,
            CardGuarantees.CancellationDeadline(appointment.Start, locationRules.CancellationDeadlineHours));
    }

    /// <summary>Legt die Hinterlegung an (wartet auf Zustimmung) und mailt den Link; ein zweiter Aufruf schickt einen neuen Link.</summary>
    public async Task<RequestOutcome> RequestAsync(Guid appointmentId, CancellationToken ct)
    {
        if (await OfferAsync(appointmentId, ct) is not { } offer)
        {
            return new RequestOutcome.NotFound();
        }

        if (!offer.Possible)
        {
            return new RequestOutcome.Refused(offer.Reason!);
        }

        var appointment = (await portal.LoadAppointmentAsync(appointmentId, ct))!;
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        // Ohne Mailadresse geht der Link per SMS; die leere Adresse merkt sich das für die Nachrichten danach.
        var email = appointment.Customer.Email ?? "";
        var guarantee = await online.CardGuarantees.FirstOrDefaultAsync(g => g.AppointmentId == appointmentId, ct)
            ?? (await cards.AddAsync(appointment, location.Currency, await rules.ForAsync(location.Id, ct), email, "de", consented: false, ct))!;
        var token = links.Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointment.Id, appointment.End.AddDays(30));
        await online.SaveChangesAsync(ct);

        var url = portal.Url($"termin/{token}");
        await messages.SendAsync(OnlineMails.CardRequest(email, GuestName.Of(appointment.Customer, location.TimeZone),
            OnlineMails.ForGuest(appointment.Treatment.Name, appointment.PlaceholderGuest, guarantee.Language),
            appointment.Start, location.TimeZone, location.Name, guarantee.FeeAmount, guarantee.Currency, guarantee.FeePercent,
            guarantee.DeadlineHours, url, guarantee.Language), appointment.CustomerId, url, ct);
        return new RequestOutcome.Sent(CardGuarantees.ToStaff(guarantee, appointment));
    }
}

using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Anzahlung für Termine, die die Rezeption gebucht hat (Telefon, vor Ort), wie „Karte per Link anfordern“ (<see cref="CardRequests"/>):
/// Der Gast bekommt einen Link per Mail und bezahlt dort die Anzahlung des Standorts beim Zahlungsanbieter. Bezahlt wird über denselben Weg
/// wie beim Online-Buchen; die Kasse stellt den Anzahlungsbeleg aus und verrechnet ihn beim Kassieren. Ohne Zahlung bleibt der Termin stehen.
/// </summary>
internal sealed class DepositRequests(
    AispaDbContext db,
    OnlineDbContext online,
    Deposits deposits,
    OnlineRules rules,
    OnlinePortal portal,
    AccessLinks links,
    IMessageBus bus,
    TenantContext tenants,
    TimeProvider time)
{
    public abstract record RequestOutcome
    {
        public sealed record Sent(DepositStaffDto Deposit) : RequestOutcome;

        public sealed record NotFound : RequestOutcome;

        public sealed record Refused(string Message) : RequestOutcome;
    }

    /// <summary>Ob und mit welchem Betrag sich für den Termin eine Anzahlung anfordern lässt; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<DepositRequestOffer?> OfferAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null)
        {
            return null;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var percent = await deposits.PercentAtAsync(await rules.ForAsync(location.Id, ct), appointment.Treatment, location.Id, ct);
        var amount = CardGuarantees.Fee(appointment.Price, percent);
        var existing = await online.Deposits.AsNoTracking().Where(d => d.AppointmentId == appointmentId).Select(d => (OnlineDepositStatus?)d.Status)
            .FirstOrDefaultAsync(ct);
        var card = await online.CardGuarantees.AsNoTracking().Where(g => g.AppointmentId == appointmentId).Select(g => (CardGuaranteeStatus?)g.Status)
            .FirstOrDefaultAsync(ct);
        string? reason = (percent, existing) switch
        {
            ( <= 0, _) => "Für diese Behandlung ist keine Anzahlung eingestellt, oder Zahlungskonto oder Kasse des Standorts fehlen.",
            // Eine offene oder abgebrochene Anforderung lässt sich wiederholen; alles andere ist schon bezahlt oder erledigt.
            (_, { } status) when status is not (OnlineDepositStatus.Pending or OnlineDepositStatus.Failed) => "Für diesen Termin gibt es schon eine Anzahlung.",
            _ when card is CardGuaranteeStatus.Saved or CardGuaranteeStatus.Pending or CardGuaranteeStatus.AwaitingConsent => "Für diesen Termin ist schon eine Karte für die Ausfallgebühr hinterlegt oder angefordert.",
            _ when appointment.Status != AppointmentStatus.Booked || appointment.Start <= time.GetUtcNow() => "Nur für bevorstehende, gebuchte Termine.",
            _ when string.IsNullOrWhiteSpace(appointment.Customer.Email) => "Für den Gast ist keine E-Mail-Adresse hinterlegt.",
            _ when amount <= 0 => "Der Termin hat keinen Preis.",
            _ => null,
        };
        return new DepositRequestOffer(reason is null, amount, location.Currency, percent, reason);
    }

    /// <summary>Legt die Anzahlung an, beginnt die Zahlung und mailt den Link; ein zweiter Aufruf beginnt neu und schickt einen neuen Link.</summary>
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
        var email = appointment.Customer.Email!;
        var language = GuestLanguage.Resolve(appointment.Customer);
        var deposit = await online.Deposits.FirstOrDefaultAsync(d => d.AppointmentId == appointmentId, ct)
            ?? (await deposits.AddAsync(appointment, location.Currency, await rules.ForAsync(location.Id, ct), email, language, ct, requested: true))!;
        deposit.Status = OnlineDepositStatus.Pending;
        var token = links.Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointment.Id, appointment.End.AddDays(30));
        await online.SaveChangesAsync(ct);
        var url = portal.Url($"termin/{token}");
        await deposits.StartAsync(deposit, appointment.Treatment.Name, location.Name, url, ct);

        await bus.PublishGuestMailAsync(tenants, OnlineMails.DepositRequest(email, GuestName.Of(appointment.Customer, location.TimeZone), appointment.Treatment.Name,
            appointment.Start, location.TimeZone, location.Name, deposit.Amount, deposit.Currency, deposit.Percent, url, deposit.Language));
        return new RequestOutcome.Sent(Deposits.ToStaff(deposit, appointment, null));
    }
}

using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Angaben einer Buchungsplattform zu einem Termin (Treatwell, ADR 0062).</summary>
/// <param name="PrepaidAmount">Vorab über die Plattform bezahlter Betrag; die Buchung begrenzt ihn auf den Preis der Behandlung.</param>
/// <param name="AcceptPending">Die Buchung ist in der Plattform noch anzunehmen.</param>
internal sealed record PlatformBooking(PlatformPayment Payment, decimal? PrepaidAmount, bool AcceptPending);

// ---------- Plattformbuchungen: Gast zuordnen und „in der Plattform angenommen“ ----------
internal sealed partial class BookingService
{
    /// <summary>
    /// Hängt einen Termin auf einen anderen Kunden um (die Rezeption hat den vorläufig angelegten Treatwell-Gast einem Bestandskunden
    /// zugeordnet). Beachtet „Nicht bei mir“ des neuen Kunden wie die Buchung selbst.
    /// </summary>
    public async Task<BookingOutcome> ReassignCustomerAsync(Guid id, Guid customerId, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        if (customer.DeletedAt is not null)
        {
            return new BookingOutcome.Invalid("customerId", CustomerAnonymizer.NotSelectableMessage);
        }

        if (appointment.CustomerId == customer.Id)
        {
            return new BookingOutcome.Success(ToDto(appointment, appointment.Treatment.Name));
        }

        var rules = await CustomerRules.LoadAsync(db, customerId, ct);
        if (rules.ExcludedEmployees.Contains(appointment.EmployeeId))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Bei dieser Person ist der Gast ausgeschlossen („Nicht bei mir“). Bitte erst umbuchen.",
                [new BookingConflict(ResourceKind.Employee, appointment.EmployeeId, "Person", "hat für diesen Gast „Nicht bei mir“ gesetzt")]));
        }

        appointment.CustomerId = customer.Id;
        appointment.Customer = customer;
        appointment.UpdatedAt = time.GetUtcNow();
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }

    /// <summary>
    /// Die Rezeption hat die Treatwell-Buchung in Treatwell angenommen; der Hinweis „noch annehmen“ verschwindet am Termin und in den
    /// Mails zu diesem Termin.
    /// </summary>
    public async Task<BookingOutcome> ConfirmPlatformAcceptedAsync(Guid id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (appointment.AcceptInPlatformPending)
        {
            appointment.AcceptInPlatformPending = false;
            appointment.UpdatedAt = time.GetUtcNow();
            if (!await TrySaveAsync(ct))
            {
                return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
            }
        }

        await db.InboundMails.Where(m => m.AppointmentId == id && m.AcceptInPlatformPending)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AcceptInPlatformPending, false), ct);
        return await PublishAsync(appointment, appointment.Treatment.Name);
    }
}

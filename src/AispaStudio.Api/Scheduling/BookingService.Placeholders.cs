using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

// ---------- Gruppen mit Gästen ohne Namen: Namen später nachtragen ----------
internal sealed partial class BookingService
{
    /// <summary>Trägt den Gast für einen Platzhalter ein, bei allen Terminen und Eintritten dieses Gastes in der Buchung.</summary>
    public async Task<BookingOutcome> AssignGuestAsync(Guid id, Guid customerId, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        return appointment.PlaceholderGuest is not { } guest || appointment.GroupId is not { } groupId
            ? new BookingOutcome.Invalid(nameof(AssignGuestRequest.CustomerId), "Der Termin hat schon einen Gast.")
            : await AssignAsync(groupId, guest, appointment.CustomerId, customerId, ct);
    }

    /// <summary>
    /// Wie <see cref="AssignGuestAsync"/>, aber vom Eintritt in Sauna oder Ruheraum aus: für einen Gast ohne Namen, der in der Buchung nur einen
    /// Bereich hat (Auftraggeber, 18.11.2026). Hat er auch Termine, bekommen sie den Gast ebenso.
    /// </summary>
    public async Task<BookingOutcome> AssignVisitGuestAsync(Guid locationId, Guid visitId, Guid customerId, CancellationToken ct)
    {
        var visit = await db.AreaVisits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == visitId && v.LocationId == locationId, ct);
        if (visit is null)
        {
            return new BookingOutcome.NotFound("Eintritt");
        }

        return visit.PlaceholderGuest is not { } guest || visit.GroupId is not { } groupId || visit.IsCancelled
            ? new BookingOutcome.Invalid(nameof(AssignGuestRequest.CustomerId), "Der Eintritt hat schon einen Gast.")
            : await AssignAsync(groupId, guest, visit.CustomerId, customerId, ct);
    }

    private async Task<BookingOutcome> AssignAsync(Guid groupId, int guest, Guid bookerId, Guid customerId, CancellationToken ct)
    {
        if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        if (customer.DeletedAt is not null)
        {
            return new BookingOutcome.Invalid(nameof(AssignGuestRequest.CustomerId), CustomerAnonymizer.NotSelectableMessage);
        }

        var parts = await db.Appointments.Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.GroupId == groupId && a.PlaceholderGuest == guest && a.CustomerId == bookerId
                && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.Start)
            .ToListAsync(ct);
        var rules = await CustomerRules.LoadAsync(db, customerId, ct);
        if (parts.FirstOrDefault(a => rules.ExcludedEmployees.Contains(a.EmployeeId)) is { } excluded)
        {
            return new BookingOutcome.Rejected(new BookingRejected("Bei dieser Person ist der Gast ausgeschlossen („Nicht bei mir“). Bitte erst umbuchen.",
                [new BookingConflict(ResourceKind.Employee, excluded.EmployeeId, "Person", "hat für diesen Gast „Nicht bei mir“ gesetzt")]));
        }

        // Sauna und Ruheraum des Gastes gehören mit: sie tragen dieselbe Nummer und laufen bis jetzt auf die buchende Person.
        var visits = await db.AreaVisits.Include(v => v.Customer)
            .Where(v => v.GroupId == groupId && v.PlaceholderGuest == guest && v.CustomerId == bookerId && !v.IsCancelled)
            .ToListAsync(ct);
        var now = time.GetUtcNow();
        foreach (var part in parts)
        {
            part.CustomerId = customer.Id;
            part.Customer = customer;
            part.PlaceholderGuest = null;
            part.UpdatedAt = now;
        }

        foreach (var visit in visits)
        {
            visit.CustomerId = customer.Id;
            visit.Customer = customer;
            visit.PlaceholderGuest = null;
            visit.UpdatedAt = now;
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishGroupAsync(groupId, parts.Select(a => (a, a.Treatment.Name)), visits: visits);
    }
}

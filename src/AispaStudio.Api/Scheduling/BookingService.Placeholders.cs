using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

// ---------- Gruppen mit Gästen ohne Namen: Namen später nachtragen ----------
internal sealed partial class BookingService
{
    /// <summary>Trägt den Gast für einen Platzhalter ein, bei allen Terminen dieses Gastes in der Buchung.</summary>
    public async Task<BookingOutcome> AssignGuestAsync(Guid id, Guid customerId, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (appointment.PlaceholderGuest is not { } guest || appointment.GroupId is not { } groupId)
        {
            return new BookingOutcome.Invalid(nameof(AssignGuestRequest.CustomerId), "Der Termin hat schon einen Gast.");
        }

        if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        var parts = await db.Appointments.Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.GroupId == groupId && a.PlaceholderGuest == guest && a.CustomerId == appointment.CustomerId
                && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.Start)
            .ToListAsync(ct);
        var rules = await CustomerRules.LoadAsync(db, customerId, ct);
        if (parts.FirstOrDefault(a => rules.ExcludedEmployees.Contains(a.EmployeeId)) is { } excluded)
        {
            return new BookingOutcome.Rejected(new BookingRejected("Bei dieser Person ist der Gast ausgeschlossen („Nicht bei mir“). Bitte erst umbuchen.",
                [new BookingConflict(ResourceKind.Employee, excluded.EmployeeId, "Person", "hat für diesen Gast „Nicht bei mir“ gesetzt")]));
        }

        var now = time.GetUtcNow();
        foreach (var part in parts)
        {
            part.CustomerId = customer.Id;
            part.Customer = customer;
            part.PlaceholderGuest = null;
            part.UpdatedAt = now;
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishGroupAsync(groupId, parts.Select(a => (a, a.Treatment.Name)));
    }
}

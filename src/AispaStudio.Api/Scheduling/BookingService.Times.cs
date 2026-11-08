using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

// ---------- Vor- und Nachbereitung eines einzelnen Termins ändern (Schritt 12) ----------
internal sealed partial class BookingService
{
    /// <summary>Größte Vor- oder Nachbereitung eines Termins in Minuten.</summary>
    public const int MaxTimesMinutes = 120;

    /// <summary>
    /// Ändert Vor- und Nachbereitung eines Termins. Der Planer prüft dieselbe Person, denselben Raum und dasselbe Gerät zur selben Startzeit
    /// mit den neuen Minuten; passt es nicht, bleibt alles wie es war. Der Puffer „Braucht mehr Zeit“ bleibt.
    /// </summary>
    public async Task<BookingOutcome> ChangeTimesAsync(Guid id, int preparation, int followUp, CancellationToken ct, uint? expectedVersion = null)
    {
        if (preparation is < 0 or > MaxTimesMinutes)
        {
            return new BookingOutcome.Invalid(nameof(preparation), "Zwischen 0 und 120 Minuten.");
        }

        if (followUp is < 0 or > MaxTimesMinutes)
        {
            return new BookingOutcome.Invalid(nameof(followUp), "Zwischen 0 und 120 Minuten.");
        }

        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, expectedVersion) is { } stale)
        {
            return stale;
        }

        if (appointment.Status is not (AppointmentStatus.Booked or AppointmentStatus.CheckedIn))
        {
            return new BookingOutcome.Invalid(nameof(appointment.Status),
                "Vor- und Nachbereitung lassen sich nur bei gebuchten oder eingecheckten Terminen ändern.");
        }

        var zoneId = await db.Locations.Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).FirstAsync(ct);
        var context = await LoadContextAsync(
            appointment.LocationId, appointment.TreatmentId, LocalTime.LocalDate(appointment.Start, LocalTime.Zone(zoneId)), appointment.Id, ct);
        if (context is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        var rules = await CustomerRules.LoadAsync(db, appointment.CustomerId, ct);
        context = WithTimesOf(appointment, rules.Apply(context, appointment.EmployeeId)) with
        {
            DurationMinutes = (int)(appointment.End - appointment.Start).TotalMinutes,
            PreparationMinutes = preparation,
            FollowUpMinutes = followUp,
        };
        var result = new ResourcePlanner(context).Plan(appointment.Start, appointment.EmployeeId, appointment.RoomId, appointment.EquipmentId);
        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts));
        }

        db.ResourceBookings.RemoveRange(appointment.Bookings);
        Apply(appointment, plan, time.GetUtcNow());
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }
}

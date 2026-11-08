using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Tagesregel am Status-Endpunkt des Teams (L1): Eingecheckt wird nur am Tag des Termins, Erledigt und Nicht erschienen erst ab dem Tag des Termins,
/// jeweils in der Zeitzone des Standorts. Die Prüfung sitzt am Endpunkt wie <see cref="PastBookings"/>; Beispieldaten und die Handy-App („begonnen“) rechnen im Dienst.
/// </summary>
internal static class StatusDays
{
    public const string CheckInMessage = "Einchecken geht nur am Tag des Termins.";

    public const string CompleteMessage = "Erledigt und Nicht erschienen lassen sich erst am Tag des Termins setzen.";

    /// <summary>Die Ablehnung, wenn der Tag des Termins den Wechsel nicht zulässt; sonst <c>null</c> (auch bei unbekanntem Termin, den meldet der Statuswechsel selbst).</summary>
    public static async Task<BookingOutcome.Invalid?> CheckAsync(AispaDbContext db, TimeProvider time, Guid appointmentId, AppointmentStatus status, CancellationToken ct)
    {
        if (status is not (AppointmentStatus.CheckedIn or AppointmentStatus.Completed or AppointmentStatus.NoShow))
        {
            return null;
        }

        var found = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId)
            .Select(a => new { a.Start, ZoneId = db.Locations.Where(l => l.Id == a.LocationId).Select(l => l.TimeZone).FirstOrDefault() })
            .FirstOrDefaultAsync(ct);
        if (found?.ZoneId is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(found.ZoneId);
        var day = LocalTime.LocalDate(found.Start, zone);
        var today = LocalTime.LocalDate(time.GetUtcNow(), zone);
        return status == AppointmentStatus.CheckedIn
            ? day != today ? new BookingOutcome.Invalid("status", CheckInMessage) : null
            : day > today ? new BookingOutcome.Invalid("status", CompleteMessage) : null;
    }
}

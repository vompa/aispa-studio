using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Die Rezeption bucht nur für heute oder später: ein früherer Tag wird abgelehnt, an heute ändert die Uhrzeit nichts, damit ein Gast
/// nachgetragen werden kann, der schon da war. Die Prüfung sitzt an den Endpunkten des Teams; Beispieldaten und Planer rechnen weiter in der Vergangenheit.
/// </summary>
internal static class PastBookings
{
    public const string Message = "Termine lassen sich nicht in der Vergangenheit buchen. Bitte heute oder einen späteren Tag wählen.";

    /// <summary>Die Ablehnung, wenn <paramref name="start"/> vor dem heutigen Tag des Standorts liegt; sonst <c>null</c> (auch bei unbekanntem Standort, den meldet die Buchung selbst).</summary>
    public static async Task<BookingRejected?> CheckAsync(AispaDbContext db, TimeProvider time, Guid locationId, DateTimeOffset start, CancellationToken ct)
    {
        var zoneId = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return null;
        }

        return IsBeforeToday(start, time.GetUtcNow(), LocalTime.Zone(zoneId)) ? new BookingRejected(Message, []) : null;
    }

    /// <summary>Liegt der Kalendertag von <paramref name="start"/> vor dem Kalendertag von <paramref name="now"/> in der Zeitzone?</summary>
    public static bool IsBeforeToday(DateTimeOffset start, DateTimeOffset now, TimeZoneInfo zone) =>
        LocalTime.LocalDate(start, zone) < LocalTime.LocalDate(now, zone);
}

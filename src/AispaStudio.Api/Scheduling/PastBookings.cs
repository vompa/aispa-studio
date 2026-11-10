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
    public const string Message = "Termine lassen sich nicht in der Vergangenheit buchen. Möglich ist heute oder ein späterer Tag.";

    /// <summary>Dieselbe Regel beim Verschieben (L2): Ein Tippfehler im Tag darf den Termin nicht auf gestern legen, wo ihn niemand mehr sieht.</summary>
    public const string MoveMessage = "Termine lassen sich nicht in die Vergangenheit verschieben. Möglich ist heute oder ein späterer Tag.";

    /// <summary>Dieselbe Regel beim Zurücknehmen einer Stornierung zur alten Zeit (Verlauf, Aufgabe E): ein vergangener Termin kommt nur zu einer neuen Zeit zurück.</summary>
    public const string RestoreMessage = "Der Termin liegt in der Vergangenheit. Die Stornierung lässt sich nur zu einer neuen Zeit zurücknehmen.";

    /// <summary>Die Ablehnung, wenn <paramref name="start"/> vor dem heutigen Tag des Standorts liegt; sonst <c>null</c> (auch bei unbekanntem Standort, den meldet die Buchung selbst).</summary>
    public static async Task<BookingRejected?> CheckAsync(AispaDbContext db, TimeProvider time, Guid locationId, DateTimeOffset start, CancellationToken ct, string message = Message)
    {
        var zoneId = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return null;
        }

        return IsBeforeToday(start, time.GetUtcNow(), LocalTime.Zone(zoneId)) ? new BookingRejected(message, []) : null;
    }

    /// <summary>Wie <see cref="CheckAsync"/> für das Verschieben eines Termins an den Standort, an dem er liegt; ein unbekannter Termin ergibt <c>null</c> (den meldet das Verschieben selbst).</summary>
    public static async Task<BookingRejected?> CheckMoveAsync(AispaDbContext db, TimeProvider time, Guid appointmentId, DateTimeOffset start, CancellationToken ct)
    {
        var locationId = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId).Select(a => (Guid?)a.LocationId).FirstOrDefaultAsync(ct);
        return locationId is { } id ? await CheckAsync(db, time, id, start, ct, MoveMessage) : null;
    }

    /// <summary>Liegt der Kalendertag von <paramref name="start"/> vor dem Kalendertag von <paramref name="now"/> in der Zeitzone?</summary>
    public static bool IsBeforeToday(DateTimeOffset start, DateTimeOffset now, TimeZoneInfo zone) =>
        LocalTime.LocalDate(start, zone) < LocalTime.LocalDate(now, zone);
}

using System.Collections.Concurrent;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AispaStudio.Api.Scheduling;

/// <summary>Öffnungszeiten und Schließzeiten eines Standorts für eine Woche (Montag bis Sonntag in Ortszeit, Schließzeiten mit Rand für Vor- und Nachbereitung).</summary>
internal sealed record OpeningWeek(IReadOnlyList<LocationOpeningHours> OpeningHours, IReadOnlyList<ClosedSpan> Closed);

/// <summary>
/// Zwischenspeicher für die schnelle Ablehnung beim Buchen (Slice 78): Öffnungszeiten und Schließzeiten je Standort und Woche, fünf Minuten lang.
/// Jede Änderung an Schließzeiten oder Öffnungszeiten des Standorts lässt alle Wochen des Standorts sofort verfallen (<see cref="Invalidate"/>).
/// Nur die Vorabprüfung liest hier; der Raumplaner lädt beim Buchen weiter frisch aus der Datenbank. Ein veralteter Eintrag (zweite Server-Instanz, Abgleich)
/// kann deshalb höchstens eine Ablehnung bewirken, nie eine Buchung in einer Schließzeit. Der Standort ist Teil des Schlüssels; aufgerufen wird erst, nachdem der
/// Standort im Mandanten des Aufrufs gefunden wurde.
/// </summary>
internal sealed class OpeningCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, long> _generations = new();

    /// <summary>Die Woche mit <paramref name="date"/>; beim ersten Aufruf aus der Datenbank.</summary>
    public async Task<OpeningWeek> WeekAsync(AispaDbContext db, Guid locationId, DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var key = (nameof(OpeningCache), locationId, _generations.GetValueOrDefault(locationId), monday);
        if (cache.TryGetValue(key, out OpeningWeek? week) && week is not null)
        {
            return week;
        }

        var hours = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).SelectMany(l => l.OpeningHours).ToListAsync(ct);
        var (from, _) = BookingService.BookingWindow(monday, zone);
        var (_, until) = BookingService.BookingWindow(monday.AddDays(6), zone);
        var closed = await ClosureService.SpansAsync(db, locationId, from, until, zone, ct);
        week = new OpeningWeek(hours, closed);
        cache.Set(key, week, Lifetime);
        return week;
    }

    /// <summary>Nach jeder Änderung an Schließzeiten oder Öffnungszeiten des Standorts: alle gespeicherten Wochen gelten nicht mehr.</summary>
    public void Invalidate(Guid locationId) => _generations.AddOrUpdate(locationId, 1, (_, generation) => generation + 1);
}

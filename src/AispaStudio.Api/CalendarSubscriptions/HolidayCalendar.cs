using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>Ein Feiertag an einem Standort und woher er stammt: aus den Regeln im Code oder aus einem aktiven Abo.</summary>
internal sealed record HolidayHit(DateOnly Date, string Name, Guid? SubscriptionId, string Source, DateTimeOffset? AsOf)
{
    public bool FromRules => SubscriptionId is null;
}

/// <summary>Ferien eines aktiven Abos am Standort: ein Zeitraum mit Titel, nebeneinanderliegende Tage gleichen Titels sind zusammengefasst.</summary>
internal sealed record PeriodHit(DateOnly From, DateOnly Until, string Title, Guid SubscriptionId, string Source, DateTimeOffset? AsOf);

/// <summary>
/// Die eine Quelle für „ist das ein Feiertag?“ (ADR 0075): die Regel-Feiertage im Code (<see cref="PublicHolidays"/>, Grundlage der Lohnverrechnung) und dazu die
/// Tage aktiver Abos der Art „gesetzliche Feiertage“. Abo-Tage ergänzen die Regeln; was die Leitung bei einem Widerspruch mit „ignorieren“ entschieden hat, zählt nicht.
/// Öffnungszeiten-Antworten, Feiertags-Antworten des Assistenten, Serienvorschau, Feiertagsliste und Kalenderkopf lesen hier. Die Lohnverrechnung liest weiter nur die Regeln.
/// Ob ein Abo im Kalender angezeigt wird, ist Sache jedes Zugangs und spielt hier keine Rolle (nur <see cref="CalendarSubscription.IsActive"/>).
/// </summary>
internal sealed class HolidayCalendar(AispaDbContext db)
{
    private Dictionary<Guid, LocationHolidays>? _places;
    private List<CalendarSubscription>? _subscriptions;
    private HashSet<(Guid, DateOnly)>? _ignored;

    /// <summary>Feiertage eines Standorts von <paramref name="from"/> bis <paramref name="until"/> (beide einschließlich), nach Datum geordnet.</summary>
    public async Task<IReadOnlyList<HolidayHit>> ForLocationAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var places = _places ??= await PublicHolidays.LocationsAsync(db, ct);
        if (!places.TryGetValue(locationId, out var place))
        {
            return [];
        }

        var hits = new Dictionary<DateOnly, HolidayHit>();
        foreach (var year in Enumerable.Range(from.Year, until.Year - from.Year + 1))
        {
            foreach (var (date, name) in PublicHolidays.Named(place.Country, place.Region, year).Where(h => h.Date >= from && h.Date <= until))
            {
                hits.TryAdd(date, new HolidayHit(date, name, null, RulesSource(place), null));
            }
        }

        var subscriptions = await ActiveAsync(ct);
        _ignored ??= (await db.CalendarSubscriptionDecisions.AsNoTracking().Where(d => d.Decision == SubscriptionDecision.Ignored)
            .Select(d => new { d.SubscriptionId, d.Date }).ToListAsync(ct)).Select(d => (d.SubscriptionId, d.Date)).ToHashSet();
        foreach (var sub in subscriptions.Where(s => s.Kind == SubscriptionKind.PublicHoliday && Applies(s, locationId, place)))
        {
            foreach (var entry in sub.Entries.Where(e => e.AllDay && CountsAsHoliday(e, place)))
            {
                foreach (var date in Days(entry, from, until).Where(d => !_ignored.Contains((sub.Id, d))))
                {
                    hits.TryAdd(date, new HolidayHit(date, DisplayTitle(entry.Title), sub.Id, SubscriptionSource(sub), sub.LastFetchAt));
                }
            }
        }

        return [.. hits.Values.OrderBy(h => h.Date)];
    }

    /// <summary>Der Feiertag an einem Tag, falls einer gilt.</summary>
    public async Task<HolidayHit?> OnAsync(Guid locationId, DateOnly date, CancellationToken ct) =>
        await ForLocationAsync(locationId, date, date, ct) is { Count: > 0 } found ? found[0] : null;

    /// <summary>
    /// Alles, was der Kalenderkopf zu einem Tag zeigt: Feiertag (Regeln oder Abo), Schulferien, Hinweise, Sonstiges. Abos, die dieser Zugang ausgeblendet hat, fehlen
    /// (nur Anzeige); der Feiertag aus den Regeln bleibt immer.
    /// </summary>
    public async Task<IReadOnlyList<CalendarDayFact>> FactsAsync(Guid locationId, DateOnly date, string? userId, CancellationToken ct)
    {
        var places = _places ??= await PublicHolidays.LocationsAsync(db, ct);
        if (!places.TryGetValue(locationId, out var place))
        {
            return [];
        }

        var hidden = userId is null
            ? []
            : await db.CalendarSubscriptionHidden.AsNoTracking().Where(h => h.UserId == userId).Select(h => h.SubscriptionId).ToListAsync(ct);
        var facts = new List<CalendarDayFact>();
        foreach (var hit in await ForLocationAsync(locationId, date, date, ct))
        {
            if (hit.SubscriptionId is { } id && hidden.Contains(id))
            {
                continue;
            }

            facts.Add(new CalendarDayFact(hit.SubscriptionId, SubscriptionKind.PublicHoliday, hit.Name, hit.Date, hit.Date, hit.Source, hit.AsOf));
        }

        foreach (var sub in (await ActiveAsync(ct)).Where(s => s.Kind != SubscriptionKind.PublicHoliday && !hidden.Contains(s.Id) && Applies(s, locationId, place)))
        {
            foreach (var (from, until, title) in Merge(sub.Entries).Where(e => e.From <= date && date <= e.Until).OrderBy(e => e.From).ThenBy(e => e.Title, StringComparer.Ordinal))
            {
                facts.Add(new CalendarDayFact(sub.Id, sub.Kind, DisplayTitle(title), from, until, SubscriptionSource(sub), sub.LastFetchAt));
            }
        }

        return [.. facts.OrderBy(f => f.Kind).ThenBy(f => f.Title, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Ferien (Abos der Art „Schulferien“) am Standort, die den Zeitraum von <paramref name="from"/> bis <paramref name="until"/> berühren, nach Beginn geordnet.
    /// Für den Assistenten („Wann sind Ferien?“); wie bei den Feiertagen zählt nur, ob das Abo aktiv ist.
    /// </summary>
    public async Task<IReadOnlyList<PeriodHit>> SchoolHolidaysAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var places = _places ??= await PublicHolidays.LocationsAsync(db, ct);
        if (!places.TryGetValue(locationId, out var place))
        {
            return [];
        }

        return [.. (await ActiveAsync(ct)).Where(s => s.Kind == SubscriptionKind.SchoolHoliday && Applies(s, locationId, place))
            .SelectMany(s => Merge(s.Entries).Where(e => e.Until >= from && e.From <= until)
                .Select(e => new PeriodHit(e.From, e.Until, DisplayTitle(e.Title), s.Id, SubscriptionSource(s), s.LastFetchAt)))
            .OrderBy(h => h.From).ThenBy(h => h.Title, StringComparer.Ordinal)];
    }

    /// <summary>Fasst aufeinanderfolgende Tage und überlappende Zeiträume mit gleichem Titel zu einem Eintrag zusammen (aus „15., 16., 17.“ wird „15. bis 17.“).</summary>
    public static IEnumerable<(DateOnly From, DateOnly Until, string Title)> Merge(IEnumerable<CalendarSubscriptionEntry> entries)
    {
        foreach (var group in entries.GroupBy(e => e.Title, StringComparer.Ordinal))
        {
            var current = default((DateOnly From, DateOnly Until)?);
            foreach (var entry in group.OrderBy(e => e.From))
            {
                if (current is { } open && entry.From <= open.Until.AddDays(1))
                {
                    current = (open.From, entry.Until > open.Until ? entry.Until : open.Until);
                    continue;
                }

                if (current is { } done)
                {
                    yield return (done.From, done.Until, group.Key);
                }

                current = (entry.From, entry.Until);
            }

            if (current is { } last)
            {
                yield return (last.From, last.Until, group.Key);
            }
        }
    }

    /// <summary>Aktive Abos mit ihren Einträgen (ohne Verfolgung), einmal je Anfrage geladen.</summary>
    private async Task<List<CalendarSubscription>> ActiveAsync(CancellationToken ct) =>
        _subscriptions ??= await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().Where(s => s.IsActive).ToListAsync(ct);

    // ---------- gemeinsame Regeln (auch für die Widerspruchsliste) ----------

    /// <summary>Gilt das Abo am Standort? Der Standort passt, und die Region (Land oder Bundesland, Kanton) ist die des Standorts.</summary>
    public static bool Applies(CalendarSubscription sub, Guid locationId, LocationHolidays place) =>
        (sub.LocationId is null || sub.LocationId == locationId) && RegionMatches(sub.Region, place);

    public static bool RegionMatches(string? region, LocationHolidays place) =>
        string.IsNullOrEmpty(region)
        || (region.Length == 2 ? string.Equals(region, place.Country.ToString(), StringComparison.OrdinalIgnoreCase)
            : string.Equals(region, place.Region, StringComparison.OrdinalIgnoreCase));

    /// <summary>Feiertag ja, Gedenktag nein; ein regionaler Feiertag nur, wenn der Standort in einem der genannten Bundesländer oder Kantone liegt.</summary>
    public static bool CountsAsHoliday(CalendarSubscriptionEntry entry, LocationHolidays place) => entry.Scope switch
    {
        EntryScope.Holiday => true,
        EntryScope.Regional => PublicHolidays.Region(place.Region) is { } region
            && (entry.RegionNames ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(name => string.Equals(name, region.Name, StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };

    public static IEnumerable<DateOnly> Days(CalendarSubscriptionEntry entry, DateOnly from, DateOnly until)
    {
        var first = entry.From > from ? entry.From : from;
        var last = entry.Until < until ? entry.Until : until;
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    /// <summary>„Heilige Drei Könige (regionaler Feiertag)“ wird zu „Heilige Drei Könige“; die Region steht ohnehin in der Quelle.</summary>
    public static string DisplayTitle(string title)
    {
        const string Suffix = " (regionaler Feiertag)";
        return title.EndsWith(Suffix, StringComparison.Ordinal) ? title[..^Suffix.Length] : title;
    }

    public static string RulesSource(LocationHolidays place) =>
        PublicHolidays.Region(place.Region) is { } region
            ? $"Feiertagsregeln {CountryName(place.Country)}, {region.Name}"
            : $"Feiertagsregeln {CountryName(place.Country)}";

    public static string SubscriptionSource(CalendarSubscription sub) => $"Abo ‚{sub.Name}‘";

    public static string CountryName(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => "Österreich",
        FiscalCountry.DE => "Deutschland",
        _ => "Schweiz",
    };

    /// <summary>„Stand heute“, „Stand gestern“, „Stand vor 3 Tagen“ oder „Stand 12.10.2026“; ohne Abruf „noch nicht abgerufen“.</summary>
    public static string AsOfText(DateTimeOffset? asOf, DateOnly today, TimeZoneInfo zone)
    {
        if (asOf is not { } at)
        {
            return "noch nicht abgerufen";
        }

        var day = LocalTime.LocalDate(at, zone);
        var age = today.DayNumber - day.DayNumber;
        return age switch
        {
            <= 0 => "Stand heute",
            1 => "Stand gestern",
            <= 14 => $"Stand vor {age} Tagen",
            _ => $"Stand {day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}",
        };
    }
}

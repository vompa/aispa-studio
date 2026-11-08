using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Beispiel-Abos für das Beispiel-Spa (ADR 0075, docs/testdaten.md): je Standort ein Feiertags-Abo mit lokal gespeicherter Kopie (die Tage der Regel-Feiertage des Landes,
/// als käme die Kopie von der Quelle) und ein Abo für Schulferien in Wien. Beim Einspielen geht nichts ins Netz; der Hintergrundjob ruft die Abos beim ersten Lauf nach
/// <c>NextCheckAt</c> ab. Die Beispieldaten enthalten keinen Widerspruch zwischen Abo und Regeln (Auftraggeber, Ä50): die Widerspruchsliste ist in der Vorführung leer.
/// </summary>
internal static class DemoCalendarSubscriptions
{
    /// <summary>Schulferien Wien 2026 bis 2028 (Stand 22.10.2026, openholidaysapi.org, Tage einschließlich). Die Quelle liefert keine Semesterferien.</summary>
    private static readonly (DateOnly From, DateOnly Until, string Title)[] ViennaSchoolHolidays =
    [
        (new(2025, 12, 24), new(2026, 1, 6), "Weihnachtsferien"),
        (new(2026, 3, 28), new(2026, 4, 6), "Osterferien"),
        (new(2026, 5, 23), new(2026, 5, 25), "Pfingstferien"),
        (new(2026, 10, 27), new(2026, 10, 31), "Herbstferien"),
        (new(2026, 11, 2), new(2026, 11, 2), "Allerseelen"),
        (new(2026, 12, 24), new(2027, 1, 6), "Weihnachtsferien"),
        (new(2027, 3, 20), new(2027, 3, 29), "Osterferien"),
        (new(2027, 5, 15), new(2027, 5, 17), "Pfingstferien"),
        (new(2027, 10, 27), new(2027, 10, 31), "Herbstferien"),
        (new(2027, 11, 2), new(2027, 11, 2), "Allerseelen"),
        (new(2027, 12, 24), new(2028, 1, 6), "Weihnachtsferien"),
        (new(2028, 4, 8), new(2028, 4, 17), "Osterferien"),
        (new(2028, 6, 3), new(2028, 6, 5), "Pfingstferien"),
        (new(2028, 10, 27), new(2028, 10, 31), "Herbstferien"),
        (new(2028, 11, 2), new(2028, 11, 2), "Allerseelen"),
        (new(2028, 12, 24), new(2029, 1, 6), "Weihnachtsferien"),
    ];

    public static void Add(AispaDbContext db, DateTimeOffset now, Location vienna, Location salzburg, Location graz)
    {
        var firstYear = now.Year;
        var locations = new[] { (vienna, "AT-9"), (salzburg, "AT-5"), (graz, "AT-6") };
        foreach (var (location, region) in locations)
        {
            var name = location.Name.Split(' ')[0];
            var sub = New($"Feiertage {name}", CalendarFeedSources.GoogleAustria, SubscriptionKind.PublicHoliday, region, location.Id, now);
            foreach (var year in Enumerable.Range(firstYear, 3))
            {
                foreach (var (date, title) in PublicHolidays.Named(FiscalCountry.AT, region, year))
                {
                    sub.Entries.Add(Entry(sub, date, date, title, EntryScope.Holiday, null, CalendarSubscriptionEntry.FromFeed));
                }
            }

            db.CalendarSubscriptions.Add(sub);
        }

        var school = New("Schulferien Wien", CalendarFeedSources.SchoolHolidays("AT", "AT-9", firstYear, firstYear + 2), SubscriptionKind.SchoolHoliday, "AT-9", vienna.Id, now);
        foreach (var (from, until, title) in ViennaSchoolHolidays)
        {
            school.Entries.Add(Entry(school, from, until, title, EntryScope.Holiday, null, CalendarSubscriptionEntry.FromFeed));
        }

        db.CalendarSubscriptions.Add(school);
    }

    private static CalendarSubscription New(string name, string url, SubscriptionKind kind, string region, Guid locationId, DateTimeOffset now) => new()
    {
        Name = name,
        Url = url,
        Kind = kind,
        Region = region,
        LocationId = locationId,
        IsActive = true,
        CreatedAt = now,
        LastFetchAt = now,
        LastResult = SubscriptionFetchResult.Ok,
        NextCheckAt = now.AddDays(1),
    };

    private static CalendarSubscriptionEntry Entry(CalendarSubscription sub, DateOnly from, DateOnly until, string title, EntryScope scope, string? regions, string source) => new()
    {
        SubscriptionId = sub.Id,
        From = from,
        Until = until,
        Title = title,
        Scope = scope,
        RegionNames = regions,
        Source = source,
    };
}

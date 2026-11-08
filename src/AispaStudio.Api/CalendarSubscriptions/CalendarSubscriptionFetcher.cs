using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Holt ein Abo und ersetzt seine gespeicherte Kopie (ADR 0075). Läuft nur im Hintergrund (Wolverine, <see cref="FetchCalendarSubscriptionHandler"/>), nie im HTTP-Aufruf.
/// Bei einem Fehler bleibt die letzte Kopie gültig; der Fehler steht am Abo und als Meldung („Erneut versuchen“). Ein leerer Kalender ersetzt eine vorhandene Kopie nicht.
/// Der Vergleich mit der alten Kopie ergibt die Meldung „2 neue Tage, 1 entfernt“.
/// </summary>
internal sealed class CalendarSubscriptionFetcher(AispaDbContext db, ICalendarFeedClient feed, TimeProvider time)
{
    /// <summary>Nach einem Erfolg wird täglich neu abgerufen, nach einem Fehler schon nach sechs Stunden.</summary>
    private static readonly TimeSpan SuccessInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan FailureInterval = TimeSpan.FromHours(6);

    /// <summary>Ruft ein aktives Abo mit Adresse ab; für andere liefert es null.</summary>
    public async Task<SubscriptionRefreshResult?> FetchAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub is not { IsActive: true } || sub.Url.Length == 0)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var download = await feed.GetAsync(sub.Url, ct);
        if (download.Content is null)
        {
            return await FailAsync(sub, download.Error ?? "Unbekannter Fehler beim Abruf.", now, ct);
        }

        var zone = await ZoneAsync(sub, ct);
        var parsed = FeedReader.Read(download.Content, LocalTime.LocalDate(now, zone), zone);
        if (!parsed.IsCalendar)
        {
            return await FailAsync(sub, "Die Adresse liefert keinen Kalender (ICS).", now, ct);
        }

        var (from, until) = FeedReader.Horizon(LocalTime.LocalDate(now, zone));
        if (parsed.Events.Count == 0 && sub.Entries.Any(e => e.Source == CalendarSubscriptionEntry.FromFeed))
        {
            return await FailAsync(sub, $"Der Kalender enthält keine Einträge für {from.Year} bis {until.Year}; die bisherige Kopie bleibt.", now, ct);
        }

        var result = Replace(sub, parsed, CalendarSubscriptionEntry.FromFeed);
        sub.LastFetchAt = now;
        sub.LastResult = SubscriptionFetchResult.Ok;
        sub.LastError = null;
        sub.NextCheckAt = now + SuccessInterval;
        await ResolveAsync($"failed:{sub.Id}", now, ct);
        if (result.Added + result.Removed > 0)
        {
            await NoteChangeAsync(sub, result, now, ct);
        }

        await db.SaveChangesAsync(ct);
        return result.Summary;
    }

    /// <summary>
    /// Übernimmt eine Datei (ICS oder CSV) als Kopie dieses Abos, ersetzt die Einträge aus früheren Dateien. Dieselben Leser wie beim Abruf, derselbe Horizont.
    /// </summary>
    public async Task<(SubscriptionRefreshResult? Result, string? Error)> ImportAsync(Guid subscriptionId, string content, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub is null)
        {
            return (null, null);
        }

        var zone = await ZoneAsync(sub, ct);
        var now = time.GetUtcNow();
        var parsed = FeedReader.Read(content, LocalTime.LocalDate(now, zone), zone);
        if (!parsed.IsCalendar || parsed.Events.Count == 0)
        {
            var (from, until) = FeedReader.Horizon(LocalTime.LocalDate(now, zone));
            return (null, parsed.IsCalendar
                ? $"Die Datei enthält keine Einträge für {from.Year} bis {until.Year}."
                : "Die Datei ist weder ein Kalender (ICS) noch eine Liste mit Datum und Titel (CSV).");
        }

        var result = Replace(sub, parsed, CalendarSubscriptionEntry.FromFile);
        await db.SaveChangesAsync(ct);
        return (result.Summary, null);
    }

    // ---------- Kopie ersetzen und vergleichen ----------

    private sealed record Replacement(int Added, int Removed, bool First, IReadOnlyList<(DateOnly Date, string Title)> NewDays, SubscriptionRefreshResult Summary);

    private Replacement Replace(CalendarSubscription sub, FeedParseResult parsed, string source)
    {
        var old = sub.Entries.Where(e => e.Source == source).ToList();
        var fresh = parsed.Events.Select(e =>
        {
            var (scope, regions) = EntryClassifier.Classify(e.Description);
            return new CalendarSubscriptionEntry
            {
                SubscriptionId = sub.Id,
                From = e.Start,
                Until = e.EndInclusive,
                Title = e.Summary,
                AllDay = e.AllDay,
                Scope = scope,
                RegionNames = regions,
                Source = source,
            };
        }).ToList();

        var before = CountedDays(sub, old);
        var after = CountedDays(sub, fresh);
        var added = after.Except(before).OrderBy(d => d.Date).ThenBy(d => d.Title, StringComparer.Ordinal).ToList();
        var removed = before.Except(after).ToList();

        foreach (var entry in old)
        {
            sub.Entries.Remove(entry);
            db.CalendarSubscriptionEntries.Remove(entry);
        }

        foreach (var entry in fresh)
        {
            sub.Entries.Add(entry);
            db.CalendarSubscriptionEntries.Add(entry);
        }

        var days = CountedDays(sub, sub.Entries).Select(d => d.Date).Distinct().Count();
        var text = Describe(sub.Name, added.Count, removed.Count, before.Count == 0, days);
        if (parsed.Warnings.Count > 0)
        {
            text += " " + string.Join(" ", parsed.Warnings);
        }

        return new Replacement(added.Count, removed.Count, before.Count == 0, added, new SubscriptionRefreshResult(added.Count, removed.Count, days, text, SubscriptionFetchResult.Ok));
    }

    /// <summary>Tage mit Titel, die der Abo-Art nach zählen (bei Feiertagen ohne Gedenktage); Zeiträume werden in Tage aufgelöst.</summary>
    private static HashSet<(DateOnly Date, string Title)> CountedDays(CalendarSubscription sub, IEnumerable<CalendarSubscriptionEntry> entries)
    {
        var days = new HashSet<(DateOnly, string)>();
        foreach (var entry in entries.Where(e => sub.Kind != SubscriptionKind.PublicHoliday || e.Scope != EntryScope.Observance))
        {
            foreach (var day in HolidayCalendar.Days(entry, entry.From, entry.Until.DayNumber - entry.From.DayNumber > 62 ? entry.From.AddDays(62) : entry.Until))
            {
                days.Add((day, entry.Title));
            }
        }

        return days;
    }

    /// <summary>„Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 entfernt“, beim ersten Mal „… 136 Tage übernommen“, ohne Änderung „unverändert“.</summary>
    private static string Describe(string name, int added, int removed, bool first, int days)
    {
        var tag = $"Abo ‚{name}‘: ";
        if (first)
        {
            return tag + (days == 1 ? "1 Tag übernommen." : $"{days.ToString(CultureInfo.InvariantCulture)} Tage übernommen.");
        }

        if (added + removed == 0)
        {
            return tag + "unverändert.";
        }

        var parts = new List<string>();
        if (added > 0)
        {
            parts.Add(added == 1 ? "1 neuer Tag" : $"{added} neue Tage");
        }

        if (removed > 0)
        {
            parts.Add(removed == 1 ? "1 Tag entfernt" : $"{removed} Tage entfernt");
        }

        return tag + string.Join(", ", parts) + ".";
    }

    // ---------- Meldungen ----------

    private async Task NoteChangeAsync(CalendarSubscription sub, Replacement result, DateTimeOffset now, CancellationToken ct)
    {
        // Der erste Abruf ist keine „Änderung“: sonst steht nach dem Einrichten sofort eine Meldung da.
        if (result.First)
        {
            return;
        }

        var sample = result.NewDays.Take(3).Select(d => $"{d.Date.ToString("d.M.yyyy", CultureInfo.InvariantCulture)} {HolidayCalendar.DisplayTitle(d.Title)}").ToList();
        var text = result.Summary.Message + (sample.Count > 0 ? $" Neu: {string.Join(", ", sample)}{(result.Added > sample.Count ? " …" : "")}" : "");
        await UpsertNoticeAsync(sub, SubscriptionNoticeKind.Changed, $"changed:{sub.Id}", "Kalender-Abo geändert", text, forLeadership: false, now, ct);
    }

    private async Task<SubscriptionRefreshResult> FailAsync(CalendarSubscription sub, string error, DateTimeOffset now, CancellationToken ct)
    {
        sub.LastResult = SubscriptionFetchResult.Failed;
        sub.LastError = error.Length > 500 ? error[..500] : error;
        sub.NextCheckAt = now + FailureInterval;
        var copy = sub.LastFetchAt is { } at ? $"Es gilt die Kopie vom {at.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}." : "Es gibt noch keine Kopie.";
        var text = $"Abo ‚{sub.Name}‘ konnte nicht abgerufen werden: {error.TrimEnd('.')}. {copy}";
        await UpsertNoticeAsync(sub, SubscriptionNoticeKind.Failed, $"failed:{sub.Id}", "Kalender-Abo nicht abgerufen", text, forLeadership: false, now, ct);
        await db.SaveChangesAsync(ct);
        return new SubscriptionRefreshResult(0, 0, 0, text, SubscriptionFetchResult.Failed);
    }

    private async Task UpsertNoticeAsync(CalendarSubscription sub, SubscriptionNoticeKind kind, string key, string title, string text, bool forLeadership,
        DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.CalendarSubscriptionNotices.FirstOrDefaultAsync(n => n.Key == key, ct);
        if (existing is null)
        {
            db.CalendarSubscriptionNotices.Add(new CalendarSubscriptionNotice
            {
                SubscriptionId = sub.Id,
                LocationId = sub.LocationId,
                Kind = kind,
                Key = key,
                Title = title,
                Text = text.Length > 1000 ? text[..1000] : text,
                CreatedAt = now,
                ForLeadership = forLeadership,
            });
            return;
        }

        existing.Title = title;
        existing.Text = text.Length > 1000 ? text[..1000] : text;
        existing.CreatedAt = now;
        existing.ResolvedAt = null;
    }

    private async Task ResolveAsync(string key, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var notice in await db.CalendarSubscriptionNotices.Where(n => n.Key == key && n.ResolvedAt == null).ToListAsync(ct))
        {
            notice.ResolvedAt = now;
        }
    }

    private async Task<TimeZoneInfo> ZoneAsync(CalendarSubscription sub, CancellationToken ct)
    {
        var zone = await db.Locations.AsNoTracking().Where(l => sub.LocationId == null || l.Id == sub.LocationId).OrderBy(l => l.Name)
            .Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        return LocalTime.Zone(zone ?? "Europe/Vienna");
    }
}

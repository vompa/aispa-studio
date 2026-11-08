using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Kalender-Abos verwalten (ADR 0075). Anlegen, Ändern, Löschen, Ein- und Ausschalten, Importieren und Entscheiden bei Widersprüchen darf nur, wer die Standorte
/// verwaltet (<see cref="AccessRight.ManageLocation"/>) und den Standort des Abos sieht; ein Abo für alle Standorte verwaltet nur, wer alle Standorte sieht.
/// „Im Kalender zeigen“ (je Zugang) und „Falsch melden“ darf jeder mit Kalenderrecht. Abrufen läuft nie hier, sondern im Hintergrund.
/// </summary>
internal sealed partial class CalendarSubscriptionService(AispaDbContext db, TimeProvider time)
{
    private const int MaxSubscriptions = 50;
    private const int MaxReportReason = 300;
    private static readonly TimeSpan ChangedNoticeLifetime = TimeSpan.FromDays(7);

    // ---------- Abfrage ----------

    public async Task<List<CalendarSubscriptionDto>> ListAsync(UserAccess access, Guid? locationId, CancellationToken ct)
    {
        var places = await PublicHolidays.LocationsAsync(db, ct);
        var subs = await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().ToListAsync(ct);
        var hidden = (await db.CalendarSubscriptionHidden.AsNoTracking().Where(h => h.UserId == access.UserId).Select(h => h.SubscriptionId).ToListAsync(ct)).ToHashSet();
        var decisions = await DecisionsAsync(ct);
        var today = await TodayAsync(ct);
        return [.. subs
            .Where(s => Visible(access, s) && (locationId is not { } id || (places.TryGetValue(id, out var place) && HolidayCalendar.Applies(s, id, place))))
            .OrderBy(s => s.Kind).ThenBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => ToDto(s, !hidden.Contains(s.Id), ConflictsOf(s, places, decisions.GetValueOrDefault(s.Id) ?? [], today).Count(c => c.Decision is null)))];
    }

    public async Task<CalendarSubscriptionDto?> GetAsync(Guid id, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        return sub is null || !Visible(access, sub) ? null : await ToDtoAsync(sub, access, ct);
    }

    public static bool Visible(UserAccess access, CalendarSubscription sub) => sub.LocationId is not { } id || access.CanSee(id);

    public static bool Manages(UserAccess access, CalendarSubscription sub) =>
        access.Has(AccessRight.ManageLocation) && (sub.LocationId is { } id ? access.CanSee(id) : access.AllLocations);

    public async Task<bool> CanManageAsync(Guid id, UserAccess access, CancellationToken ct) =>
        await db.CalendarSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) is { } sub && Manages(access, sub);

    /// <summary>
    /// „Jetzt abrufen“: prüft Recht und Zustand und merkt das Abo vor (der Job holt es ab, das Ergebnis steht danach am Abo). Ohne Adresse oder ausgeschaltet gibt es nichts zu holen.
    /// </summary>
    public async Task<(bool Found, string? Error)> MarkForFetchAsync(Guid id, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Manages(access, sub))
        {
            return (false, null);
        }

        if (!sub.IsActive || sub.Url.Length == 0)
        {
            return (true, sub.IsActive ? "Das Abo hat keine Adresse (nur Datei-Import)." : "Das Abo ist ausgeschaltet.");
        }

        sub.NextCheckAt = time.GetUtcNow().AddMinutes(15);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    // ---------- Anlegen, Ändern, Löschen ----------

    public async Task<(CalendarSubscriptionDto? Subscription, Errors? Errors)> CreateAsync(CalendarSubscriptionInput input, UserAccess access, CancellationToken ct)
    {
        var errors = new Errors();
        var (url, region) = await ValidateAsync(input, access, errors, ct);
        errors.Check(await db.CalendarSubscriptions.CountAsync(ct) < MaxSubscriptions, nameof(input.Name), $"Höchstens {MaxSubscriptions} Abos.");
        if (errors.Any)
        {
            return (null, errors);
        }

        var sub = new CalendarSubscription
        {
            Name = input.Name.Trim(),
            Url = url,
            Kind = input.Kind,
            Region = region,
            LocationId = input.LocationId,
            IsActive = input.IsActive,
            CreatedAt = time.GetUtcNow(),
        };
        db.CalendarSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(sub, access, ct), null);
    }

    public async Task<(CalendarSubscriptionDto? Subscription, Errors? Errors)> UpdateAsync(Guid id, CalendarSubscriptionInput input, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Manages(access, sub))
        {
            return (null, null);
        }

        var errors = new Errors();
        var (url, region) = await ValidateAsync(input, access, errors, ct);
        errors.Check(sub.Preset is null || input.Kind == sub.Kind, nameof(input.Kind), "Die Art eines vorab eingetragenen Abos bleibt.");
        if (errors.Any)
        {
            return (null, errors);
        }

        var changedSource = sub.Url != url;
        sub.Name = input.Name.Trim();
        sub.Url = url;
        sub.Kind = sub.Preset is null ? input.Kind : sub.Kind;
        sub.Region = region;
        sub.LocationId = input.LocationId;
        sub.IsActive = input.IsActive;
        if (changedSource || input.IsActive)
        {
            // Neue oder geänderte Adresse: sofort neu abrufen (der Job holt Abos ohne Termin). Die alte Kopie bleibt bis zum ersten Erfolg.
            sub.NextCheckAt = null;
            sub.LastError = changedSource ? null : sub.LastError;
        }

        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(sub, access, ct), null);
    }

    /// <summary>Löscht ein Abo samt Kopie und Entscheidungen; vorab eingetragene Abos (Land) bleiben, man schaltet sie nur aus.</summary>
    public async Task<(bool Found, string? Error)> DeleteAsync(Guid id, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Manages(access, sub))
        {
            return (false, null);
        }

        if (sub.Preset is not null)
        {
            return (true, "Ein vorab eingetragenes Abo lässt sich nicht löschen, nur ausschalten.");
        }

        db.CalendarSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    /// <summary>Schaltet das Abo für alle ein oder aus (<c>IsActive</c>); nur die Leitung. Eingeschaltet wird es beim nächsten Lauf des Jobs abgerufen.</summary>
    public async Task<CalendarSubscriptionDto?> SetActiveAsync(Guid id, bool active, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Manages(access, sub))
        {
            return null;
        }

        sub.IsActive = active;
        sub.NextCheckAt = null;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(sub, access, ct);
    }

    /// <summary>„Im Kalender zeigen“ des eigenen Zugangs: gespeichert je Benutzer, ohne besonderes Recht; ändert nichts an Buchung, Öffnungszeiten und Assistent.</summary>
    public async Task<CalendarSubscriptionDto?> SetShowAsync(Guid id, bool show, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Visible(access, sub))
        {
            return null;
        }

        var row = await db.CalendarSubscriptionHidden.FirstOrDefaultAsync(h => h.SubscriptionId == id && h.UserId == access.UserId, ct);
        if (show && row is not null)
        {
            db.CalendarSubscriptionHidden.Remove(row);
        }
        else if (!show && row is null)
        {
            db.CalendarSubscriptionHidden.Add(new CalendarSubscriptionHidden { SubscriptionId = id, UserId = access.UserId });
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(sub, access, ct);
    }

    private async Task<(string Url, string? Region)> ValidateAsync(CalendarSubscriptionInput input, UserAccess access, Errors errors, CancellationToken ct)
    {
        errors.Check(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= CalendarSubscriptionModel.NameLength, nameof(input.Name),
            $"Name angeben, höchstens {CalendarSubscriptionModel.NameLength} Zeichen.");
        errors.Check(Enum.IsDefined(input.Kind), nameof(input.Kind), "Art unbekannt.");
        var urlOk = SubscriptionUrl.TryNormalize(input.Url, out var url, out var urlError);
        errors.Check(urlOk, nameof(input.Url), urlError ?? "");
        var region = string.IsNullOrWhiteSpace(input.Region) ? null : input.Region.Trim().ToUpperInvariant();
        errors.Check(region is null || RegionCode().IsMatch(region), nameof(input.Region), "Region als Ländercode oder ISO-Code, z. B. „AT“ oder „CH-ZH“.");
        if (input.LocationId is { } locationId)
        {
            errors.Check(await db.Locations.AnyAsync(l => l.Id == locationId, ct) && access.CanSee(locationId), nameof(input.LocationId), "Standort nicht gefunden.");
        }
        else
        {
            errors.Check(access.AllLocations, nameof(input.LocationId), "Ein Abo für alle Standorte legt nur an, wer alle Standorte verwaltet.");
        }

        return (url, region);
    }

    // ---------- Widersprüche ----------

    public async Task<List<SubscriptionConflictDto>?> ConflictsAsync(Guid id, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Visible(access, sub))
        {
            return null;
        }

        var decisions = (await DecisionsAsync(ct)).GetValueOrDefault(id) ?? [];
        return ConflictsOf(sub, await PublicHolidays.LocationsAsync(db, ct), decisions, await TodayAsync(ct));
    }

    /// <summary>Hält die Entscheidung der Leitung zu einem Tag fest oder nimmt sie zurück (<paramref name="decision"/> leer).</summary>
    public async Task<bool> DecideAsync(Guid id, DateOnly date, SubscriptionDecision? decision, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Manages(access, sub))
        {
            return false;
        }

        var row = await db.CalendarSubscriptionDecisions.FirstOrDefaultAsync(d => d.SubscriptionId == id && d.Date == date, ct);
        if (decision is null)
        {
            if (row is not null)
            {
                db.CalendarSubscriptionDecisions.Remove(row);
            }
        }
        else if (row is null)
        {
            db.CalendarSubscriptionDecisions.Add(new CalendarSubscriptionDecision
            {
                SubscriptionId = id,
                Date = date,
                Decision = decision.Value,
                UserId = access.UserId,
                DecidedAt = time.GetUtcNow(),
            });
        }
        else
        {
            (row.Decision, row.UserId, row.DecidedAt) = (decision.Value, access.UserId, time.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Dictionary<Guid, Dictionary<DateOnly, SubscriptionDecision>>> DecisionsAsync(CancellationToken ct) =>
        (await db.CalendarSubscriptionDecisions.AsNoTracking().Select(d => new { d.SubscriptionId, d.Date, d.Decision }).ToListAsync(ct))
            .GroupBy(d => d.SubscriptionId).ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Date, d => d.Decision));

    /// <summary>
    /// Vergleicht die Tage des Abos mit den Regel-Feiertagen je Standort, an dem das Abo gilt, ab heute: „nur im Abo“ (z. B. Heiligabend als halber Tag) und „nur in den Regeln“.
    /// Nur Jahre, für die das Abo überhaupt Feiertage kennt, sonst wäre jeder Regeltag ein Widerspruch. Gleiche Tage mehrerer Standorte stehen einmal mit allen Standorten.
    /// </summary>
    internal static List<SubscriptionConflictDto> ConflictsOf(CalendarSubscription sub, IReadOnlyDictionary<Guid, LocationHolidays> places,
        IReadOnlyDictionary<DateOnly, SubscriptionDecision> decisions, DateOnly today)
    {
        if (sub.Kind != SubscriptionKind.PublicHoliday)
        {
            return [];
        }

        var years = sub.Entries.Where(e => e.AllDay && e.Scope != EntryScope.Observance).SelectMany(e => new[] { e.From.Year, e.Until.Year }).Distinct().ToList();
        if (years.Count == 0)
        {
            return [];
        }

        var found = new Dictionary<(DateOnly, SubscriptionConflictKind), (string Name, string? RuleName, SortedSet<string> Locations)>();
        foreach (var (locationId, place) in places.Where(p => HolidayCalendar.Applies(sub, p.Key, p.Value)))
        {
            var rules = years.SelectMany(y => PublicHolidays.Named(place.Country, place.Region, y)).Where(h => h.Date >= today)
                .GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.First().Name);
            var fromSub = sub.Entries.Where(e => e.AllDay && HolidayCalendar.CountsAsHoliday(e, place))
                .SelectMany(e => HolidayCalendar.Days(e, today, new DateOnly(years.Max(), 12, 31)).Select(d => (Date: d, Name: HolidayCalendar.DisplayTitle(e.Title))))
                .GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.First().Name);

            foreach (var (date, name) in fromSub.Where(d => !rules.ContainsKey(d.Key)))
            {
                Add((date, SubscriptionConflictKind.OnlySubscription), name, null, place.Name);
            }

            foreach (var (date, name) in rules.Where(d => !fromSub.ContainsKey(d.Key) && years.Contains(d.Key.Year)))
            {
                Add((date, SubscriptionConflictKind.OnlyRules), name, name, place.Name);
            }
        }

        return [.. found.OrderBy(f => f.Key.Item1).ThenBy(f => f.Key.Item2)
            .Select(f => new SubscriptionConflictDto(f.Key.Item1, f.Value.Name, f.Key.Item2, f.Value.RuleName, [.. f.Value.Locations],
                decisions.TryGetValue(f.Key.Item1, out var decision) ? decision : null))];

        void Add((DateOnly, SubscriptionConflictKind) key, string name, string? ruleName, string location)
        {
            if (!found.TryGetValue(key, out var item))
            {
                found[key] = item = (name, ruleName, []);
            }

            item.Locations.Add(location);
        }
    }

    // ---------- Meldungen ----------

    public async Task<List<SubscriptionNoticeDto>> NoticesAsync(UserAccess access, Guid? locationId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var notices = await db.CalendarSubscriptionNotices.AsNoTracking().Where(n => n.ResolvedAt == null).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        return [.. notices
            .Where(n => (!n.ForLeadership || access.Has(AccessRight.ManageLocation))
                && (n.Kind != SubscriptionNoticeKind.Changed || now - n.CreatedAt <= ChangedNoticeLifetime)
                && (locationId is { } id ? n.LocationId is null || n.LocationId == id : n.LocationId is not { } own || access.CanSee(own)))
            .Select(n => new SubscriptionNoticeDto(n.Id, n.SubscriptionId, n.LocationId, n.Kind, n.Title, n.Text, n.CreatedAt, n.Kind == SubscriptionNoticeKind.Failed))];
    }

    public async Task<bool> ResolveNoticeAsync(Guid noticeId, UserAccess access, CancellationToken ct)
    {
        var notice = await db.CalendarSubscriptionNotices.FirstOrDefaultAsync(n => n.Id == noticeId, ct);
        if (notice is null || (notice.ForLeadership && !access.Has(AccessRight.ManageLocation)) || (notice.LocationId is { } id && !access.CanSee(id)))
        {
            return false;
        }

        notice.ResolvedAt ??= time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>„Falsch melden“: macht aus einem Tag eines Abos und dem Grund eine Meldung an die Leitung; das Abo ändert sich nicht.</summary>
    public async Task<(bool Found, Errors? Errors)> ReportAsync(Guid id, SubscriptionReportInput input, UserAccess access, CancellationToken ct)
    {
        var sub = await db.CalendarSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null || !Visible(access, sub))
        {
            return (false, null);
        }

        var errors = new Errors();
        var reason = (input.Reason ?? "").Trim();
        errors.Check(reason.Length is >= 3 and <= MaxReportReason, nameof(input.Reason), $"Grund angeben, 3 bis {MaxReportReason} Zeichen.");
        errors.Check(input.Date.Year is >= 2000 and <= 2100, nameof(input.Date), "Datum von 2000 bis 2100.");
        errors.Check(input.LocationId is not { } place || access.CanSee(place), nameof(input.LocationId), "Standort nicht gefunden.");
        if (errors.Any)
        {
            return (true, errors);
        }

        var who = await db.Users.AsNoTracking().Where(u => u.Id == access.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "Ein Zugang";
        var location = input.LocationId ?? sub.LocationId;
        var date = input.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        db.CalendarSubscriptionNotices.Add(new CalendarSubscriptionNotice
        {
            SubscriptionId = sub.Id,
            LocationId = location,
            Kind = SubscriptionNoticeKind.Report,
            Key = $"report:{sub.Id}:{date}:{Guid.CreateVersion7()}",
            Title = $"Abo ‚{sub.Name}‘: {date} falsch gemeldet",
            Text = $"{who} meldet den {date}: {reason}",
            CreatedAt = time.GetUtcNow(),
            ForLeadership = true,
        });
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    // ---------- Umwandlung ----------

    private async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var zone = await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        return LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(zone ?? "Europe/Vienna"));
    }

    private async Task<CalendarSubscriptionDto> ToDtoAsync(CalendarSubscription sub, UserAccess access, CancellationToken ct)
    {
        var shown = !await db.CalendarSubscriptionHidden.AnyAsync(h => h.SubscriptionId == sub.Id && h.UserId == access.UserId, ct);
        var decisions = (await DecisionsAsync(ct)).GetValueOrDefault(sub.Id) ?? [];
        var open = ConflictsOf(sub, await PublicHolidays.LocationsAsync(db, ct), decisions, await TodayAsync(ct)).Count(c => c.Decision is null);
        return ToDto(sub, shown, open);
    }

    private static CalendarSubscriptionDto ToDto(CalendarSubscription sub, bool shown, int openConflicts)
    {
        var counted = sub.Entries.Where(e => sub.Kind != SubscriptionKind.PublicHoliday || e.Scope != EntryScope.Observance).ToList();
        var days = counted.SelectMany(e => HolidayCalendar.Days(e, e.From, e.Until.DayNumber - e.From.DayNumber > 62 ? e.From.AddDays(62) : e.Until)).Distinct().Count();
        return new CalendarSubscriptionDto(sub.Id, sub.Name, sub.Url, sub.Kind, sub.Region, sub.LocationId, sub.IsActive, sub.Preset is not null, shown,
            sub.LastFetchAt, sub.LastResult, sub.LastError, sub.NextCheckAt, days,
            counted.Count == 0 ? null : counted.Min(e => e.From), counted.Count == 0 ? null : counted.Max(e => e.Until), openConflicts);
    }

    [GeneratedRegex("^[A-Z]{2}(-[A-Z0-9]{1,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex RegionCode();
}

/// <summary>Die drei Feiertags-Abos nach Land, die jedes Spa von Anfang an hat (ADR 0075): aktiv ist nur das Abo des Landes des Spas.</summary>
internal static class CalendarSubscriptionDefaults
{
    /// <summary>
    /// Legt die fehlenden Abos an (noch ohne Speichern; ohne Netzabruf, die Einträge holt der erste Lauf des Jobs). Mehrfaches Aufrufen ist unschädlich.
    /// Aktiv wird das Abo, dessen Land das Land des Spas ist.
    /// </summary>
    public static async Task AddMissingAsync(AispaDbContext db, string country, DateTimeOffset now, CancellationToken ct)
    {
        var existing = (await db.CalendarSubscriptions.Where(s => s.Preset != null).Select(s => s.Preset!).ToListAsync(ct)).ToHashSet();
        foreach (var (code, name, url) in CalendarFeedSources.Presets.Where(p => !existing.Contains(p.Country)))
        {
            db.CalendarSubscriptions.Add(new CalendarSubscription
            {
                Name = name,
                Url = url,
                Kind = SubscriptionKind.PublicHoliday,
                Region = code,
                Preset = code,
                IsActive = string.Equals(code, country, StringComparison.OrdinalIgnoreCase),
                CreatedAt = now,
            });
        }
    }
}

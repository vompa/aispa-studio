using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Schließzeiten verwalten und abfragen (ADR 0073). Verwalten darf, wer die Standorte verwaltet (<see cref="AccessRight.ManageLocation"/>); lesen darf jeder mit
/// Kalender. Der Planer, der Kalender, der Kundenbereich und der Assistent fragen über die statischen Abfragen, ohne selbst zu rechnen.
/// </summary>
internal sealed class ClosureService(AispaDbContext db, OnlineDbContext online, TimeProvider time, OpeningCache openingCache)
{
    /// <summary>Längste Schließzeit in Tagen; länger ist ein Fehler bei der Eingabe, kein Betriebsurlaub.</summary>
    public const int MaxDays = 366;

    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    // ---------- Abfragen für Planer, Kalender, Kundenbereich und Assistent ----------

    /// <summary>Die Schließzeiten, die den Zeitraum schneiden, nach Beginn geordnet (ohne Verfolgung).</summary>
    public static async Task<List<Closure>> InAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct)
    {
        // Grob nach Kalendertagen vorfiltern (mit einem Tag Rand für Zeitzonen), genau in Ortszeit nachprüfen.
        var firstDay = LocalTime.LocalDate(from, zone).AddDays(-1);
        var lastDay = LocalTime.LocalDate(until, zone).AddDays(1);
        var candidates = await db.Closures.AsNoTracking()
            .Where(c => c.LocationId == locationId && c.FromDate <= lastDay && c.UntilDate >= firstDay)
            .ToListAsync(ct);
        return [.. candidates.Where(c => c.StartsAt(zone) < until && from < c.EndsAt(zone)).OrderBy(c => c.StartsAt(zone))];
    }

    /// <summary>Die geschlossenen Zeiträume für den Planer.</summary>
    public static async Task<List<ClosedSpan>> SpansAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct) =>
        [.. (await InAsync(db, locationId, from, until, zone, ct)).Select(c => new ClosedSpan(c.StartsAt(zone), c.EndsAt(zone), c.Kind))];

    /// <summary>Die Schließzeit, in der <paramref name="from"/> bis <paramref name="until"/> ganz oder teilweise liegt; sonst <c>null</c>.</summary>
    public static async Task<Closure?> FindAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct) =>
        (await InAsync(db, locationId, from, until, zone, ct)).FirstOrDefault();

    /// <summary>Offene Termine (gebucht oder eingecheckt, noch nicht vorbei) in der Schließzeit, nach Beginn geordnet.</summary>
    public static async Task<List<Appointment>> OpenAppointmentsAsync(AispaDbContext db, Closure closure, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
        return await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == closure.LocationId && Open.Contains(a.Status) && a.Start < until && from < a.End && a.End > now)
            .OrderBy(a => a.Start).ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    /// <summary>Wie viele offene Termine je Schließzeit (für Kalender-Band und Meldung).</summary>
    public static async Task<Dictionary<Guid, int>> OpenCountsAsync(AispaDbContext db, IReadOnlyCollection<Closure> closures, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var closure in closures)
        {
            var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
            counts[closure.Id] = closure.PhaseAt(now, zone) == ClosurePhase.Ended
                ? 0
                : await db.Appointments.AsNoTracking()
                    .CountAsync(a => a.LocationId == closure.LocationId && Open.Contains(a.Status) && a.Start < until && from < a.End && a.End > now, ct);
        }

        return counts;
    }

    /// <summary>
    /// Ab so vielen Tagen bis zum Beginn ist die Meldung „Termine in der Schließzeit“ ein Fehler (die Schließzeit läuft oder beginnt in dieser Zeit), davor eine Warnung
    /// (Auftraggeber, Ä49): wer eine Schließzeit mit offenen Terminen oder Wartelisten-Einträgen hat, soll es früh sehen, aber erst kurz vorher dringend.
    /// </summary>
    public const int UrgentDays = 14;

    /// <summary>Läuft die Schließzeit oder beginnt sie in den nächsten <see cref="UrgentDays"/> Tagen?</summary>
    public static bool IsUrgent(Closure closure, TimeZoneInfo zone, DateTimeOffset now) => closure.StartsAt(zone) <= now.AddDays(UrgentDays);

    /// <summary>
    /// Die Schließzeiten, die laufen oder noch kommen, gleich wie fern, und noch offene Termine oder Wartelisten-Einträge haben: Bänder für die Meldung. Eine Grenze nach vorn gibt
    /// es nicht mehr (Ä49); wie dringend sie ist, sagt <see cref="ClosureBand.Urgent"/>.
    /// </summary>
    public static async Task<List<ClosureBand>> DueAsync(AispaDbContext db, OnlineDbContext online, Guid locationId, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var closures = await InAsync(db, locationId, now, now.AddYears(100), zone, ct);
        var counts = await OpenCountsAsync(db, closures, zone, now, ct);
        var waiting = await WaitlistCountsAsync(db, online, closures, zone, now, ct);
        return [.. closures.Where(c => counts[c.Id] + waiting[c.Id] > 0).Select(c => ToBand(c, zone, counts[c.Id], waiting[c.Id], now))];
    }

    /// <summary>
    /// Wartelisten-Einträge, deren Wunsch in die Schließzeit fällt (Ä48): wartend, noch nicht vorbei und noch nicht wegen dieser Schließzeit benachrichtigt. Es zählt die Tageszeit des
    /// Wunsches (Ä54, <see cref="WishFallsIn"/>): ein Wunsch „nachmittags“ an einem Tag, der nur vormittags zu ist, fällt nicht hinein. Meldung, Dialog und Zählung nutzen diese Abfrage.
    /// </summary>
    public static async Task<List<WaitlistEntry>> OpenWaitlistAsync(AispaDbContext db, OnlineDbContext online, Closure closure, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        if (closure.PhaseAt(now, zone) == ClosurePhase.Ended)
        {
            return [];
        }

        var today = LocalTime.LocalDate(now, zone);
        var entries = await online.WaitlistEntries.AsNoTracking()
            .Where(w => w.LocationId == closure.LocationId && w.Status == WaitlistStatus.Waiting && w.Until >= today && w.From <= closure.UntilDate && w.Until >= closure.FromDate)
            .OrderBy(w => w.From).ThenBy(w => w.CreatedAt).ThenBy(w => w.Id)
            .ToListAsync(ct);
        var notified = await db.ClosureNotices.AsNoTracking().Where(n => n.ClosureId == closure.Id && n.WaitlistEntryId != null).Select(n => n.WaitlistEntryId!.Value).ToListAsync(ct);
        return [.. entries.Where(e => !notified.Contains(e.Id) && WishFallsIn(e, closure, zone))];
    }

    /// <summary>
    /// Fällt der Wunsch in die Schließzeit? Je gewünschtem Tag zählt die Zeit, die der Wunsch meint (vormittags, nachmittags, abends, ohne Angabe der ganze Tag, in Ortszeit des
    /// Standorts, <see cref="Waitlist.Window"/>): sie muss sich mit der Schließzeit überschneiden, auch an einem halben Tag.
    /// </summary>
    public static bool WishFallsIn(WaitlistEntry entry, Closure closure, TimeZoneInfo zone)
    {
        var (closedFrom, closedUntil) = (closure.StartsAt(zone), closure.EndsAt(zone));
        var last = entry.Until < closure.UntilDate ? entry.Until : closure.UntilDate;
        for (var day = entry.From > closure.FromDate ? entry.From : closure.FromDate; day <= last; day = day.AddDays(1))
        {
            var (from, until) = Waitlist.Window(entry.DayPart, day, zone);
            if (from < closedUntil && until > closedFrom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Wie viele Wartelisten-Einträge je Schließzeit noch zu benachrichtigen sind (für Kalender-Band und Meldung).</summary>
    public static async Task<Dictionary<Guid, int>> WaitlistCountsAsync(AispaDbContext db, OnlineDbContext online, IReadOnlyCollection<Closure> closures, TimeZoneInfo zone, DateTimeOffset now,
        CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var closure in closures)
        {
            counts[closure.Id] = (await OpenWaitlistAsync(db, online, closure, zone, now, ct)).Count;
        }

        return counts;
    }

    public static ClosureBand ToBand(Closure c, TimeZoneInfo zone, int openAppointments, int waitlistEntries, DateTimeOffset now) =>
        new(c.Id, c.StartsAt(zone), c.EndsAt(zone), c.Kind, ClosureText.Title(c.Kind), ClosureText.GuestText(c.Kind, c.GuestText), c.InternalReason, openAppointments, waitlistEntries,
            IsUrgent(c, zone, now));

    /// <summary>Schließzeiten für den Kundenbereich: die laufenden und kommenden (ein Jahr voraus), nie der interne Grund.</summary>
    public async Task<List<OnlineClosure>> OnlineAsync(Guid locationId, CancellationToken ct)
    {
        var zone = await ZoneAsync(locationId, ct);
        if (zone is null)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return [.. (await InAsync(db, locationId, now, now.AddYears(1), zone, ct))
            .Select(c => new OnlineClosure(c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.Kind, Clean(c.GuestText)))];
    }

    // ---------- Verwaltung ----------

    public async Task<List<ClosureDto>?> ListAsync(Guid locationId, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone)
        {
            return null;
        }

        var closures = await db.Closures.AsNoTracking().Where(c => c.LocationId == locationId).ToListAsync(ct);
        var now = time.GetUtcNow();
        var open = await OpenCountsAsync(db, closures, zone, now, ct);
        var waiting = await WaitlistCountsAsync(db, online, closures, zone, now, ct);
        var cancelled = await CancelledCountsAsync(closures.Select(c => c.Id).ToList(), ct);
        return [.. closures.OrderByDescending(c => c.FromDate).ThenByDescending(c => c.FromTime)
            .Select(c => ToDto(c, zone, now, open[c.Id], cancelled.GetValueOrDefault(c.Id), waiting[c.Id]))];
    }

    public async Task<ClosureDto?> GetAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone || await FindByIdAsync(locationId, closureId, ct) is not { } closure)
        {
            return null;
        }

        return await ToDtoAsync(closure, zone, ct);
    }

    public async Task<(ClosureDto? Closure, Errors? Errors)> CreateAsync(Guid locationId, ClosureInput input, UserAccess access, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone)
        {
            return (null, null);
        }

        var errors = new Errors();
        var closure = new Closure
        {
            LocationId = locationId,
            FromDate = input.FromDate,
            FromTime = input.FromTime,
            UntilDate = input.UntilDate,
            UntilTime = input.UntilTime,
            Kind = input.Kind,
            InternalReason = Clean(input.InternalReason),
            GuestText = Clean(input.GuestText),
            CreatedByUserId = access.UserId,
            CreatedByName = await NameOfAsync(access.UserId, ct),
            CreatedAt = time.GetUtcNow(),
        };
        await ValidateAsync(closure, zone, errors, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        db.Closures.Add(closure);
        await db.SaveChangesAsync(ct);
        openingCache.Invalidate(locationId);
        return (await ToDtoAsync(closure, zone, ct), null);
    }

    /// <summary>Ändert eine Schließzeit. <c>(null, null)</c>: gibt es nicht an diesem Standort.</summary>
    public async Task<(ClosureDto? Closure, Errors? Errors)> UpdateAsync(Guid locationId, Guid closureId, ClosureInput input, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone || await db.Closures.FirstOrDefaultAsync(c => c.Id == closureId && c.LocationId == locationId, ct) is not { } closure)
        {
            return (null, null);
        }

        closure.FromDate = input.FromDate;
        closure.FromTime = input.FromTime;
        closure.UntilDate = input.UntilDate;
        closure.UntilTime = input.UntilTime;
        closure.Kind = input.Kind;
        closure.InternalReason = Clean(input.InternalReason);
        closure.GuestText = Clean(input.GuestText);
        var errors = new Errors();
        await ValidateAsync(closure, zone, errors, ct);
        if (errors.Any)
        {
            db.ChangeTracker.Clear();
            return (null, errors);
        }

        await db.SaveChangesAsync(ct);
        openingCache.Invalidate(locationId);
        return (await ToDtoAsync(closure, zone, ct), null);
    }

    /// <summary>
    /// Löscht eine Schließzeit. <c>NotFound</c>: gibt es nicht. <c>Kept</c>: wegen dieser Schließzeit wurden schon Termine abgesagt, sie bleibt als Verlauf stehen
    /// (anpassen oder ihr Ende vorziehen geht weiterhin).
    /// </summary>
    public async Task<(bool NotFound, bool Kept)> DeleteAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        if (await db.Closures.FirstOrDefaultAsync(c => c.Id == closureId && c.LocationId == locationId, ct) is not { } closure)
        {
            return (true, false);
        }

        if (await db.ClosureNotices.AnyAsync(n => n.ClosureId == closureId, ct))
        {
            return (false, true);
        }

        db.Closures.Remove(closure);
        await db.SaveChangesAsync(ct);
        openingCache.Invalidate(locationId);
        return (false, false);
    }

    // ---------- Hilfen ----------

    internal async Task<Closure?> FindByIdAsync(Guid locationId, Guid closureId, CancellationToken ct) =>
        await db.Closures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == closureId && c.LocationId == locationId, ct);

    internal async Task<TimeZoneInfo?> ZoneAsync(Guid locationId, CancellationToken ct) =>
        await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is { } id ? LocalTime.Zone(id) : null;

    internal async Task<ClosureDto> ToDtoAsync(Closure closure, TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var open = await OpenCountsAsync(db, [closure], zone, now, ct);
        var waiting = await WaitlistCountsAsync(db, online, [closure], zone, now, ct);
        var cancelled = await CancelledCountsAsync([closure.Id], ct);
        return ToDto(closure, zone, now, open[closure.Id], cancelled.GetValueOrDefault(closure.Id), waiting[closure.Id]);
    }

    private static ClosureDto ToDto(Closure c, TimeZoneInfo zone, DateTimeOffset now, int open, int cancelled, int waiting) =>
        new(c.Id, c.LocationId, c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.StartsAt(zone), c.EndsAt(zone), c.Kind, c.InternalReason, c.GuestText,
            c.PhaseAt(now, zone), c.CreatedByName, c.CreatedAt, open, cancelled, waiting);

    /// <summary>Wie viele Termine wegen der Schließzeit abgesagt wurden (die Benachrichtigungen der Warteliste zählen nicht mit).</summary>
    private async Task<Dictionary<Guid, int>> CancelledCountsAsync(IReadOnlyCollection<Guid> closureIds, CancellationToken ct) =>
        await db.ClosureNotices.AsNoTracking().Where(n => closureIds.Contains(n.ClosureId) && n.AppointmentId != null && !n.IsCorrection)
            .GroupBy(n => n.ClosureId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<string> NameOfAsync(string userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) is { Length: > 0 } name ? name : "Unbekannt";

    private async Task ValidateAsync(Closure closure, TimeZoneInfo zone, Errors errors, CancellationToken ct)
    {
        errors.Check(closure.UntilDate >= closure.FromDate, nameof(ClosureInput.UntilDate), "Das Ende darf nicht vor dem Beginn liegen.");
        errors.Check(closure.InternalReason is null || closure.InternalReason.Length <= ClosureModel.InternalReasonLength, nameof(ClosureInput.InternalReason),
            $"Höchstens {ClosureModel.InternalReasonLength} Zeichen.");
        errors.Check(closure.GuestText is null || closure.GuestText.Length <= ClosureModel.GuestTextLength, nameof(ClosureInput.GuestText),
            $"Höchstens {ClosureModel.GuestTextLength} Zeichen.");
        errors.Check(Enum.IsDefined(closure.Kind), nameof(ClosureInput.Kind), "Unbekannte Art.");
        if (errors.Any)
        {
            return;
        }

        var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
        errors.Check(from < until, nameof(ClosureInput.UntilTime), "Das Ende muss nach dem Beginn liegen.");
        errors.Check(closure.UntilDate.DayNumber - closure.FromDate.DayNumber < MaxDays, nameof(ClosureInput.UntilDate), $"Höchstens {MaxDays} Tage.");
        if (errors.Any)
        {
            return;
        }

        var clash = (await InAsync(db, closure.LocationId, from, until, zone, ct)).FirstOrDefault(c => c.Id != closure.Id);
        errors.Check(clash is null, nameof(ClosureInput.FromDate),
            clash is null ? "" : $"Der Zeitraum überschneidet sich mit einer anderen Schließzeit ({ClosureText.Period(clash.FromDate, clash.FromTime, clash.UntilDate, clash.UntilTime, clash.FromDate)}).");
    }
}

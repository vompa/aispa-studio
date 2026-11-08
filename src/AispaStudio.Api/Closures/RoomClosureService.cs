using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Raumsperren verwalten und abfragen (ADR 0076). Verwalten darf, wer die Standorte verwaltet (<see cref="AccessRight.ManageLocation"/>); lesen darf jeder mit Kalender.
/// Der Planer, der Kalender und die Bereiche fragen über die statischen Abfragen, ohne selbst zu rechnen.
/// </summary>
internal sealed class RoomClosureService(AispaDbContext db, TimeProvider time)
{
    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    // ---------- Abfragen für Planer, Kalender und Bereiche ----------

    /// <summary>Die Raumsperren der Räume des Standorts, die den Zeitraum schneiden, nach Beginn geordnet (ohne Verfolgung).</summary>
    public static async Task<List<RoomClosure>> InAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct)
    {
        // Grob nach Kalendertagen vorfiltern (mit einem Tag Rand für Zeitzonen), genau in Ortszeit nachprüfen.
        var firstDay = LocalTime.LocalDate(from, zone).AddDays(-1);
        var lastDay = LocalTime.LocalDate(until, zone).AddDays(1);
        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        var candidates = await db.RoomClosures.AsNoTracking()
            .Where(c => rooms.Contains(c.RoomId) && c.FromDate <= lastDay && c.UntilDate >= firstDay)
            .ToListAsync(ct);
        return [.. candidates.Where(c => c.StartsAt(zone) < until && from < c.EndsAt(zone)).OrderBy(c => c.StartsAt(zone)).ThenBy(c => c.Id)];
    }

    /// <summary>Die gesperrten Zeiträume der Räume für den Planer.</summary>
    public static async Task<List<ClosedRoomSpan>> SpansAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct) =>
        [.. (await InAsync(db, locationId, from, until, zone, ct)).Select(c => new ClosedRoomSpan(c.RoomId, c.StartsAt(zone), c.EndsAt(zone), c.Kind, c.Reason))];

    /// <summary>Die Raumsperre, in der <paramref name="from"/> bis <paramref name="until"/> ganz oder teilweise liegt; sonst <c>null</c>.</summary>
    public static async Task<ClosedRoomSpan?> FindAsync(AispaDbContext db, Guid locationId, Guid roomId, DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone, CancellationToken ct) =>
        (await SpansAsync(db, locationId, from, until, zone, ct)).FirstOrDefault(s => s.RoomId == roomId);

    /// <summary>Offene Termine (gebucht oder eingecheckt, noch nicht vorbei) im gesperrten Raum während der Sperre, nach Beginn geordnet.</summary>
    public static async Task<List<Appointment>> OpenAppointmentsAsync(AispaDbContext db, RoomClosure closure, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
        return await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.RoomId == closure.RoomId && Open.Contains(a.Status) && a.Start < until && from < a.End && a.End > now)
            .OrderBy(a => a.Start).ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    /// <summary>Wie viele offene Termine je Raumsperre (für Spaltenkopf, Band und Meldung).</summary>
    public static async Task<Dictionary<Guid, int>> OpenCountsAsync(AispaDbContext db, IReadOnlyCollection<RoomClosure> closures, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var closure in closures)
        {
            var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
            counts[closure.Id] = closure.PhaseAt(now, zone) == ClosurePhase.Ended
                ? 0
                : await db.Appointments.AsNoTracking().CountAsync(a => a.RoomId == closure.RoomId && Open.Contains(a.Status) && a.Start < until && from < a.End && a.End > now, ct);
        }

        return counts;
    }

    /// <summary>
    /// Die Raumsperren, die laufen oder noch kommen, gleich wie fern, und noch offene Termine im Raum haben: Bänder für die Meldung. Wie dringend sie ist, sagt
    /// <see cref="RoomClosureBand.Urgent"/> (dieselbe Grenze wie bei den Schließzeiten, <see cref="ClosureService.UrgentDays"/>).
    /// </summary>
    public static async Task<List<RoomClosureBand>> DueAsync(AispaDbContext db, Guid locationId, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var closures = await InAsync(db, locationId, now, now.AddYears(100), zone, ct);
        var counts = await OpenCountsAsync(db, closures, zone, now, ct);
        var names = await RoomNamesAsync(db, closures, ct);
        return [.. closures.Where(c => counts[c.Id] > 0).Select(c => ToBand(c, names, zone, counts[c.Id], now))];
    }

    public static RoomClosureBand ToBand(RoomClosure c, IReadOnlyDictionary<Guid, string> roomNames, TimeZoneInfo zone, int openAppointments, DateTimeOffset now) =>
        new(c.Id, c.RoomId, roomNames.GetValueOrDefault(c.RoomId, "Raum"), c.StartsAt(zone), c.EndsAt(zone), c.Kind, RoomClosureText.Title(c.Kind), c.Reason, openAppointments,
            c.StartsAt(zone) <= now.AddDays(ClosureService.UrgentDays));

    public static async Task<Dictionary<Guid, string>> RoomNamesAsync(AispaDbContext db, IEnumerable<RoomClosure> closures, CancellationToken ct)
    {
        var ids = closures.Select(c => c.RoomId).Distinct().ToList();
        return await db.Rooms.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
    }

    // ---------- Verwaltung ----------

    public async Task<List<RoomClosureDto>?> ListAsync(Guid locationId, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone)
        {
            return null;
        }

        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        var closures = await db.RoomClosures.AsNoTracking().Where(c => rooms.Contains(c.RoomId)).ToListAsync(ct);
        var now = time.GetUtcNow();
        var open = await OpenCountsAsync(db, closures, zone, now, ct);
        var names = await RoomNamesAsync(db, closures, ct);
        return [.. closures.OrderByDescending(c => c.FromDate).ThenByDescending(c => c.FromTime).ThenBy(c => c.Id)
            .Select(c => ToDto(c, locationId, names, zone, now, open[c.Id]))];
    }

    public async Task<RoomClosureDto?> GetAsync(Guid locationId, Guid closureId, CancellationToken ct) =>
        await ZoneAsync(locationId, ct) is { } zone && await FindByIdAsync(locationId, closureId, ct) is { } closure ? await ToDtoAsync(closure, locationId, zone, ct) : null;

    public async Task<(RoomClosureDto? Closure, Errors? Errors)> CreateAsync(Guid locationId, RoomClosureInput input, UserAccess access, CancellationToken ct)
    {
        if (await ZoneAsync(locationId, ct) is not { } zone)
        {
            return (null, null);
        }

        var closure = new RoomClosure
        {
            RoomId = input.RoomId,
            FromDate = input.FromDate,
            FromTime = input.FromTime,
            UntilDate = input.UntilDate,
            UntilTime = input.UntilTime,
            Kind = input.Kind,
            Reason = Clean(input.Reason),
            CreatedByUserId = access.UserId,
            CreatedByName = await NameOfAsync(access.UserId, ct),
            CreatedAt = time.GetUtcNow(),
        };
        var errors = new Errors();
        await ValidateAsync(closure, locationId, zone, errors, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        db.RoomClosures.Add(closure);
        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(closure, locationId, zone, ct), null);
    }

    /// <summary>Ändert eine Raumsperre. <c>(null, null)</c>: gibt es nicht an diesem Standort.</summary>
    public async Task<(RoomClosureDto? Closure, Errors? Errors)> UpdateAsync(Guid locationId, Guid closureId, RoomClosureInput input, CancellationToken ct)
    {
        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        if (await ZoneAsync(locationId, ct) is not { } zone
            || await db.RoomClosures.FirstOrDefaultAsync(c => c.Id == closureId && rooms.Contains(c.RoomId), ct) is not { } closure)
        {
            return (null, null);
        }

        // Der Raum einer Sperre ändert sich nicht: eine andere Sperre ist eine neue.
        var errors = new Errors();
        errors.Check(input.RoomId == closure.RoomId, nameof(input.RoomId), "Der Raum einer Sperre lässt sich nicht ändern. Bitte eine neue Sperre anlegen.");
        closure.FromDate = input.FromDate;
        closure.FromTime = input.FromTime;
        closure.UntilDate = input.UntilDate;
        closure.UntilTime = input.UntilTime;
        closure.Kind = input.Kind;
        closure.Reason = Clean(input.Reason);
        if (!errors.Any)
        {
            await ValidateAsync(closure, locationId, zone, errors, ct);
        }

        if (errors.Any)
        {
            db.ChangeTracker.Clear();
            return (null, errors);
        }

        await db.SaveChangesAsync(ct);
        return (await ToDtoAsync(closure, locationId, zone, ct), null);
    }

    /// <summary>Löscht eine Raumsperre; <c>false</c>, wenn es sie an diesem Standort nicht gibt.</summary>
    public async Task<bool> DeleteAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        return await db.RoomClosures.Where(c => c.Id == closureId && rooms.Contains(c.RoomId)).ExecuteDeleteAsync(ct) > 0;
    }

    // ---------- Hilfen ----------

    internal async Task<RoomClosure?> FindByIdAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        return await db.RoomClosures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == closureId && rooms.Contains(c.RoomId), ct);
    }

    internal async Task<TimeZoneInfo?> ZoneAsync(Guid locationId, CancellationToken ct) =>
        await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is { } id ? LocalTime.Zone(id) : null;

    private async Task<RoomClosureDto> ToDtoAsync(RoomClosure closure, Guid locationId, TimeZoneInfo zone, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var open = await OpenCountsAsync(db, [closure], zone, now, ct);
        return ToDto(closure, locationId, await RoomNamesAsync(db, [closure], ct), zone, now, open[closure.Id]);
    }

    private static RoomClosureDto ToDto(RoomClosure c, Guid locationId, IReadOnlyDictionary<Guid, string> roomNames, TimeZoneInfo zone, DateTimeOffset now, int open) =>
        new(c.Id, locationId, c.RoomId, roomNames.GetValueOrDefault(c.RoomId, "Raum"), c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.StartsAt(zone), c.EndsAt(zone), c.Kind, c.Reason,
            c.PhaseAt(now, zone), c.CreatedByName, c.CreatedAt, open);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<string> NameOfAsync(string userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) is { Length: > 0 } name ? name : "Unbekannt";

    private async Task ValidateAsync(RoomClosure closure, Guid locationId, TimeZoneInfo zone, Errors errors, CancellationToken ct)
    {
        errors.Check(await db.Rooms.AnyAsync(r => r.Id == closure.RoomId && r.LocationId == locationId && r.IsActive, ct), nameof(RoomClosureInput.RoomId), "Unbekannter Raum an diesem Standort.");
        errors.Check(closure.UntilDate >= closure.FromDate, nameof(RoomClosureInput.UntilDate), "Das Ende darf nicht vor dem Beginn liegen.");
        errors.Check(closure.Reason is null || closure.Reason.Length <= RoomClosureModel.ReasonLength, nameof(RoomClosureInput.Reason), $"Höchstens {RoomClosureModel.ReasonLength} Zeichen.");
        errors.Check(Enum.IsDefined(closure.Kind), nameof(RoomClosureInput.Kind), "Unbekannte Art.");
        if (errors.Any)
        {
            return;
        }

        var (from, until) = (closure.StartsAt(zone), closure.EndsAt(zone));
        errors.Check(from < until, nameof(RoomClosureInput.UntilTime), "Das Ende muss nach dem Beginn liegen.");
        errors.Check(closure.UntilDate.DayNumber - closure.FromDate.DayNumber < ClosureService.MaxDays, nameof(RoomClosureInput.UntilDate), $"Höchstens {ClosureService.MaxDays} Tage.");
        if (errors.Any)
        {
            return;
        }

        var clash = (await InAsync(db, locationId, from, until, zone, ct)).FirstOrDefault(c => c.Id != closure.Id && c.RoomId == closure.RoomId);
        errors.Check(clash is null, nameof(RoomClosureInput.FromDate),
            clash is null ? "" : $"Der Zeitraum überschneidet sich mit einer anderen Sperre dieses Raums ({ClosureText.Period(clash.FromDate, clash.FromTime, clash.UntilDate, clash.UntilTime, clash.FromDate)}).");
    }
}

/// <summary>Worte zu den Arten der Raumsperre.</summary>
internal static class RoomClosureText
{
    public static string Title(RoomClosureKind kind) => kind switch
    {
        RoomClosureKind.Maintenance => "Wartung",
        RoomClosureKind.Damage => "Schaden",
        RoomClosureKind.Renovation => "Umbau",
        _ => "Sonstiges",
    };

    /// <summary>Warum der Planer den Raum ablehnt: „Der Raum ist gesperrt (Wartung).“ bzw. mit Grund „… Wartung: Filter tauschen“.</summary>
    public static string Reason(RoomClosureKind kind, string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? $"ist gesperrt ({Title(kind)})" : $"ist gesperrt ({Title(kind)}: {reason.Trim()})";
}

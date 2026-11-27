using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Eine aktive Behandlung für die Ersteinrichtung: nötige Qualifikationen (nach Name) und die Standorte, an denen sie einen Behandlungsraum hat.</summary>
internal sealed record SetupTreatment(Guid Id, string Name, decimal Price, IReadOnlyList<QualificationDto> Required, IReadOnlySet<Guid> RoomLocationIds, bool PricedEverywhere = false)
{
    public bool CanBeDoneBy(SetupEmployee employee) => Required.All(q => employee.Qualifications.Contains(q.Id));
}

/// <summary>Eine aktive Person für die Ersteinrichtung: <c>HasHours</c>, wenn sie Wochenarbeitszeiten oder ab heute einen Dienst im Dienstplan hat.</summary>
internal sealed record SetupEmployee(Guid Id, IReadOnlySet<Guid> Qualifications, bool HasHours);

/// <summary>
/// Was die Ersteinrichtung über den Betrieb wissen muss, mit wenigen Abfragen geladen; Schritte und Bereitschaftsprüfung rechnen daraus.
/// Geschlossene Standorte (ADR 0099, Punkt 10b) zählen nirgends: <c>Locations</c> sind nur die offenen, Räume, Behandlungsräume und Arbeitszeiten nur an ihnen.
/// <c>AllLocationsClosed</c>: es gibt Standorte, aber keiner ist heute offen.
/// </summary>
internal sealed record SetupFacts(
    IReadOnlyList<Location> Locations,
    bool AllLocationsClosed,
    bool AnyRoom,
    bool AnyTreatmentRoom,
    IReadOnlyList<SetupTreatment> Treatments,
    IReadOnlyList<SetupEmployee> Employees,
    bool AnyRegister)
{
    public static async Task<SetupFacts> LoadAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var all = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).OrderBy(l => l.Name).ThenBy(l => l.Id).ToListAsync(ct);
        var locations = all.Where(l => l.ClosedFrom is not { } closedFrom || closedFrom > LocalTime.LocalDate(now, LocalTime.Zone(l.TimeZone))).ToList();
        var open = locations.Select(l => l.Id).ToList();
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive && open.Contains(r.LocationId)).Select(r => r.IsArea).Distinct().ToListAsync(ct);
        var treatments = await db.Treatments.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Price,
                LocationPrices = t.LocationPrices.Select(p => new { p.LocationId, p.Price }).ToList(),
                Required = t.RequiredQualifications.OrderBy(q => q.Name).Select(q => new QualificationDto(q.Id, q.Name)).ToList(),
                RoomLocations = t.Rooms.Where(r => r.IsActive && !r.IsArea && open.Contains(r.LocationId)).Select(r => r.LocationId).ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        // Ein Dienst im Dienstplan zählt ab gestern (UTC), damit kein Tag in einer Zeitzone östlich von UTC fehlt.
        var since = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var employees = await db.Employees.AsNoTracking().Where(e => e.IsActive)
            .Select(e => new
            {
                e.Id,
                Qualifications = e.Qualifications.Select(q => q.Id).ToList(),
                HasHours = e.WorkingHours.Any(w => open.Contains(w.LocationId))
                    || db.RosterDays.Any(d => d.EmployeeId == e.Id && d.Date >= since
                        && d.Entries.Any(x => x.Kind == RosterKind.Work && x.LocationId != null && open.Contains(x.LocationId.Value))),
            })
            .ToListAsync(ct);
        var anyRegister = await db.CashRegisters.AnyAsync(r => r.IsActive, ct);

        return new SetupFacts(
            locations,
            all.Count > 0 && locations.Count == 0,
            rooms.Count > 0,
            rooms.Contains(false),
            [.. treatments.Select(t => new SetupTreatment(t.Id, t.Name, t.Price, t.Required, t.RoomLocations.ToHashSet(), t.Price > 0 || (t.RoomLocations.Count > 0 && t.RoomLocations.All(l => t.LocationPrices.Any(p => p.LocationId == l && p.Price > 0)))))],
            [.. employees.Select(e => new SetupEmployee(e.Id, e.Qualifications.ToHashSet(), e.HasHours))],
            anyRegister);
    }
}

/// <summary>
/// Bereitschaftsprüfung der Ersteinrichtung (ADR 0104, Entscheidung 6): lässt sich in den nächsten 14 Tagen mindestens eine Behandlung buchen?
/// Gesucht wird über denselben Weg wie bei der Online-Buchung (<see cref="BookingService.LoadContextAsync"/> und <see cref="ResourcePlanner.FreeSlots"/>).
/// Vorher prüft eine kleine Diagnose aus den geladenen Daten, was sicher fehlt, damit der Grund ein fertiger Satz ist und der leere Betrieb nichts sucht.
/// </summary>
internal static class SetupReadiness
{
    public const int Days = 14;

    /// <summary>Höchstens so viele Paare aus Standort und Behandlung werden durchsucht.</summary>
    public const int MaxPairs = 3;

    /// <summary>Höchstens so oft wird ein Planungskontext geladen (je rund 17 Abfragen); danach gilt der allgemeine Grund.</summary>
    public const int MaxContextLoads = 8;

    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    public const string NoOpeningHours = "Kein Standort hat Öffnungszeiten. Unter „Standorte“ die Zeiten eintragen, zu denen Gäste buchen können.";

    public const string NoTreatment = "Noch keine Behandlung angelegt. Unter „Behandlungen“ eine Behandlung anlegen.";

    public const string NoTreatmentRoom = "Keine Behandlung hat einen Raum an einem Standort mit Öffnungszeiten. Unter „Behandlungen“ einen Raum zuordnen.";

    public const string NoEmployee = "Noch niemand im Team. Unter „Team“ eine Person anlegen.";

    public const string NoDuty = "In den nächsten 14 Tagen hat niemand Dienst, der eine Behandlung durchführen kann. Unter „Team“ Arbeitszeiten oder im „Dienstplan“ Dienste eintragen.";

    public const string NoFreeTime = "In den nächsten 14 Tagen ist keine Zeit frei, in der alles frei ist, was eine Behandlung braucht. Kalender, Räume und „Dienstplan“ prüfen.";

    public const string AllLocationsClosed = "Alle Standorte sind geschlossen. Unter „Standorte“ einen Standort wieder öffnen oder einen neuen anlegen.";

    public static async Task<SetupReadinessDto> CheckAsync(
        SetupFacts facts, AispaDbContext db, BookingService booking, RosterService roster, DateTimeOffset now, CancellationToken ct)
    {
        if (facts.AllLocationsClosed)
        {
            return NotReady(AllLocationsClosed);
        }

        var open = facts.Locations.Where(l => l.OpeningHours.Count > 0).ToList();
        if (open.Count == 0)
        {
            return NotReady(NoOpeningHours);
        }

        if (facts.Treatments.Count == 0)
        {
            return NotReady(NoTreatment);
        }

        var withRoom = open.Select(l => facts.Treatments.Where(t => t.RoomLocationIds.Contains(l.Id)).Select(t => (Location: l, Treatment: t)).ToList()).ToList();
        if (withRoom.All(l => l.Count == 0))
        {
            return NotReady(NoTreatmentRoom);
        }

        if (facts.Employees.Count == 0)
        {
            return NotReady(NoEmployee);
        }

        // Reihum über die Standorte: erst je Standort die erste geeignete Behandlung, dann die zweite, damit nicht ein Standort alle Plätze belegt.
        var suitable = withRoom.Select(l => l.Where(p => facts.Employees.Any(p.Treatment.CanBeDoneBy)).ToList()).ToList();
        var pairs = Enumerable.Range(0, MaxPairs).SelectMany(round => suitable.Where(l => round < l.Count).Select(l => l[round])).Take(MaxPairs).ToList();
        if (pairs.Count == 0)
        {
            var first = withRoom.First(l => l.Count > 0)[0].Treatment;
            return NotReady(NoQualification(first.Name, MissingQualifications(first, facts.Employees)));
        }

        var closed = new Dictionary<Guid, List<ClosedSpan>>();
        var shifts = new Dictionary<(Guid, DateOnly), List<EmployeeShift>>();
        var (onDuty, loads) = (false, 0);
        for (var day = 0; day < Days; day++)
        {
            foreach (var (location, treatment) in pairs)
            {
                var zone = LocalTime.Zone(location.TimeZone);
                var today = LocalTime.LocalDate(now, zone);
                var date = today.AddDays(day);
                if (!closed.TryGetValue(location.Id, out var spans))
                {
                    // Schließzeiten der 14 Tage einmal je Standort (auch „geschlossen ab“); Tage ohne Öffnungsfenster kosten keine Suche.
                    var (from, until) = (BookingService.BookingWindow(today, zone).From, BookingService.BookingWindow(today.AddDays(Days - 1), zone).Until);
                    spans = await ClosureService.SpansAsync(db, location.Id, from, until, zone, ct);
                    closed[location.Id] = spans;
                }

                if (!ResourcePlanner.OpeningWindows(location.OpeningHours, zone, date, spans).Any())
                {
                    continue;
                }

                if (!shifts.TryGetValue((location.Id, date), out var dayShifts))
                {
                    dayShifts = await roster.ShiftsAsync(location.Id, date, zone, ct);
                    shifts[(location.Id, date)] = dayShifts;
                }

                var qualified = facts.Employees.Where(treatment.CanBeDoneBy).Select(e => e.Id).ToHashSet();
                if (!dayShifts.Any(s => qualified.Contains(s.EmployeeId)))
                {
                    continue;
                }

                onDuty = true;
                if (loads == MaxContextLoads)
                {
                    return NotReady(NoFreeTime);
                }

                loads++;
                if (await booking.LoadContextAsync(location.Id, treatment.Id, date, null, ct) is not { } context)
                {
                    continue;
                }

                if (new ResourcePlanner(context).FreeSlots(date, Step, null).FirstOrDefault(s => s.Start >= now) is { } slot)
                {
                    return new SetupReadinessDto(true, new SetupFirstSlotDto(location.Id, location.Name, treatment.Id, treatment.Name, slot.Start));
                }
            }
        }

        return NotReady(onDuty ? NoFreeTime : NoDuty);
    }

    /// <summary>Der Grund, wenn niemand im Team eine Behandlung durchführen kann, mit den fehlenden Qualifikationen in der übergebenen Reihenfolge.</summary>
    internal static string NoQualification(string treatment, IReadOnlyList<string> qualifications)
    {
        var names = qualifications.Select(q => $"„{q}“").ToList();
        var list = names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} und {names[^1]}";
        var noun = names.Count == 1 ? "die Qualifikation" : "die Qualifikationen";
        return $"Niemand im Team hat {noun} {list} für „{treatment}“. Unter „Team“ bei einer Person eintragen.";
    }

    /// <summary>Welche Qualifikationen fehlen, nach Name: die, die niemand im Team hat; haben alle je jemand, aber niemand alle zusammen, alle nötigen.</summary>
    private static List<string> MissingQualifications(SetupTreatment treatment, IReadOnlyList<SetupEmployee> employees)
    {
        var missing = treatment.Required.Where(q => employees.All(e => !e.Qualifications.Contains(q.Id))).ToList();
        return [.. (missing.Count > 0 ? missing : treatment.Required).Select(q => q.Name)];
    }

    private static SetupReadinessDto NotReady(string reason) => new(false, Reason: reason);
}

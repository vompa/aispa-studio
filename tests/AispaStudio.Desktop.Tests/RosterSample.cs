using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Stub des Dienstplans (R05 Slice 99): hält die geplanten Tage wie die API, liefert Wochen mit und ohne <c>employeeId</c>, nimmt <c>PUT</c>, <c>DELETE</c>, Veröffentlichen und Verwerfen
/// an und antwortet mit dem neuen Stand. Wie viele Termine nach dem Speichern außerhalb des Dienstes liegen, sagt der Test (<see cref="Outside"/>); die App rechnet das nie selbst.
/// Dazu die Zeiterfassung: <c>GET …/timesheet</c> (mit und ohne <c>employeeId</c>), Nachtragen, Ändern und Entfernen von Abschnitten und ihr Verlauf (<see cref="AddRecord"/>).
/// </summary>
internal sealed class RosterSample
{
    public static readonly DateOnly Monday = new(2027, 3, 1);

    /// <summary>Dienstag, der 2.3.2027, 10:00 Uhr in Wien: „heute“ der Tests, die Woche beginnt am Montag davor.</summary>
    public static readonly DateTimeOffset Now = new(2027, 3, 2, 9, 0, 0, TimeSpan.Zero);

    public static readonly LocationDto Vienna = new(Guid.NewGuid(), "Wien", "Europe/Vienna", "EUR", []);
    public static readonly LocationDto Graz = new(Guid.NewGuid(), "Graz", "Europe/Vienna", "EUR", []);

    /// <summary>Lena arbeitet dienstags 9–17 Uhr in Wien (Wochenarbeitszeit).</summary>
    public static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [],
        [new WorkingHours(Vienna.Id, DayOfWeek.Tuesday, new(9, 0), new(17, 0))]);

    /// <summary>Marco hat keine Wochenarbeitszeit.</summary>
    public static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [], []);

    private readonly Dictionary<(Guid Employee, DateOnly Date), RosterCell> _plan = [];
    private readonly LocationDto _location;
    private readonly IReadOnlyList<EmployeeDto> _people;
    private readonly StubApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly List<TimeRecordDto> _records = [];
    private readonly Dictionary<Guid, List<TimeRecordChangeDto>> _changes = [];

    public RosterSample(StubApi api, LocationDto location, params EmployeeDto[] people)
        : this(api, location, Monday, people)
    {
    }

    /// <summary>Die Woche, in der „heute“ liegt, beginnt am <paramref name="week"/> (ein Montag); die Wochen davor und danach sind abrufbar.</summary>
    public RosterSample(StubApi api, LocationDto location, DateOnly week, params EmployeeDto[] people)
    {
        _location = location;
        _people = people;
        _api = api;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
        foreach (var monday in new[] { week.AddDays(-7), week, week.AddDays(7) })
        {
            api.OnJson($"GET /api/locations/{location.Id}/roster?from={monday:yyyy-MM-dd}", _ => WeekOf(monday, null));
            foreach (var person in people)
            {
                api.OnJson($"GET /api/locations/{location.Id}/roster?from={monday:yyyy-MM-dd}&employeeId={person.Id}", _ => WeekOf(monday, person.Id));
                api.OnJson($"GET /api/locations/{location.Id}/timesheet?from={monday:yyyy-MM-dd}&employeeId={person.Id}", _ => TimesheetOf(monday, person.Id));
            }

            api.OnJson($"GET /api/locations/{location.Id}/timesheet?from={monday:yyyy-MM-dd}", _ => TimesheetOf(monday, null));
        }

        foreach (var person in people)
        {
            for (var day = week.AddDays(-7); day < week.AddDays(14); day = day.AddDays(1))
            {
                var date = day;
                api.OnJson($"PUT /api/locations/{location.Id}/roster/{person.Id}/{date:yyyy-MM-dd}", body => Put(person, date, body!));
                api.OnJson($"DELETE /api/locations/{location.Id}/roster/{person.Id}/{date:yyyy-MM-dd}?draft=true", _ => Reset(person, date, draft: true));
                api.OnJson($"DELETE /api/locations/{location.Id}/roster/{person.Id}/{date:yyyy-MM-dd}?draft=false", _ => Reset(person, date, draft: false));
            }
        }

        api.OnJson($"POST /api/locations/{location.Id}/time-records", body => PostRecord(body!));
        api.OnJson($"POST /api/locations/{location.Id}/roster/publish", _ => Publish())
            .OnJson($"POST /api/locations/{location.Id}/roster/discard", _ => Discard());
    }

    /// <summary>So viele Termine liegen nach dem nächsten Speichern außerhalb des Dienstes.</summary>
    public int Outside { get; set; }

    /// <summary>Hinweise zur kurzfristigen Änderung, die das nächste Veröffentlichen meldet.</summary>
    public List<string> ShortNotice { get; } = [];

    /// <summary>Hinweise der API zum nächsten gespeicherten Tag.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Hinweis nach dem Arbeitszeitrecht, den die Zeiterfassung an jedem Tag mit Zeiten meldet.</summary>
    public string? TimeWarning { get; set; }

    public IReadOnlyList<TimeRecordDto> Records => _records;

    public int Drafts => _plan.Values.Count(c => c.Draft);

    /// <summary>Ein Zeitabschnitt am <paramref name="date"/> (Ortszeit des Standorts), von <paramref name="start"/> bis <paramref name="end"/> (leer = läuft).</summary>
    public TimeRecordDto AddRecord(EmployeeDto person, DateOnly date, TimeOnly start, TimeOnly? end, TimeRecordSource source = TimeRecordSource.Clock, Guid? location = null)
    {
        var record = new TimeRecordDto(Guid.NewGuid(), person.Id, location ?? _location.Id, Utc(date, start), end is { } e ? Utc(e < start ? date.AddDays(1) : date, e) : null, source, false);
        _records.Add(record);
        _changes[record.Id] = [];
        if (source == TimeRecordSource.Manual)
        {
            _changes[record.Id].Add(new TimeRecordChangeDto(Now.AddDays(-1), "Marco Berger", TimeRecordAction.Created, null, null, record.Start, record.End, "Stempeln vergessen"));
        }

        var path = $"/api/locations/{_location.Id}/time-records/{record.Id}";
        _api.OnJson($"PUT {path}", body => PutRecord(record.Id, body!))
            .OnJson($"DELETE {path}", body => DeleteRecord(record.Id, body!))
            .OnJson($"GET {path}/changes", _ => _changes[record.Id].ToList());
        return record;
    }

    public RosterCell? PlannedCell(EmployeeDto person, DateOnly date) => _plan.GetValueOrDefault((person.Id, date));

    /// <summary>Legt einen Tag fest, wie ihn die API nach dem Speichern liefert (für Tests der Wochenansicht).</summary>
    public void Plan(EmployeeDto person, DateOnly date, bool draft, params RosterEntry[] entries) =>
        _plan[(person.Id, date)] = new RosterCell(date, RosterSource.Plan, entries, null, [], draft);

    /// <summary>Urlaub von <paramref name="from"/> bis <paramref name="until"/> (je Tag ein Eintrag), als veröffentlichter Stand.</summary>
    public void PlanVacation(EmployeeDto person, DateOnly from, DateOnly until)
    {
        for (var day = from; day <= until; day = day.AddDays(1))
        {
            Plan(person, day, false, new RosterEntry(RosterKind.Vacation, null, null, null));
        }
    }

    private RosterCell CellOf(EmployeeDto person, DateOnly date) =>
        _plan.TryGetValue((person.Id, date), out var planned)
            ? planned
            : new RosterCell(date, RosterSource.Pattern,
                [.. person.WorkingHours.Where(h => h.Day == date.DayOfWeek).Select(h => new RosterEntry(RosterKind.Work, h.LocationId, h.Start, h.End))], null, []);

    private static double WorkHours(RosterCell cell) =>
        cell.Entries.Where(e => e.Kind == RosterKind.Work).Sum(e => (e.End!.Value - e.Start!.Value).TotalHours);

    private RosterWeek WeekOf(DateOnly monday, Guid? employeeId)
    {
        var dates = Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
        var rows = _people.Where(p => employeeId is null ? p.WorkingHours.Any(h => h.LocationId == _location.Id) || dates.Any(d => _plan.ContainsKey((p.Id, d))) : p.Id == employeeId)
            .Select(p =>
            {
                var cells = dates.Select(d => CellOf(p, d)).ToList();
                var hours = cells.Sum(c => WorkHours(c));
                return new RosterRow(p.Id, p.DisplayName, p.Color, cells, hours, []);
            })
            .ToList();
        var drafts = _plan.Where(c => c.Value.Draft && dates.Contains(c.Key.Date)).Count();
        return new RosterWeek(_location.Id, monday, dates, rows, drafts, null);
    }

    private DateTimeOffset Utc(DateOnly date, TimeOnly time) => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), _zone), TimeSpan.Zero);

    private DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _zone).DateTime);

    private Timesheet TimesheetOf(DateOnly monday, Guid? employeeId)
    {
        var dates = Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
        var rows = _people.Where(p => employeeId is null
                ? p.WorkingHours.Any(h => h.LocationId == _location.Id) || dates.Any(d => _plan.ContainsKey((p.Id, d))) || _records.Any(r => r.EmployeeId == p.Id && r.LocationId == _location.Id)
                : p.Id == employeeId)
            .Select(p =>
            {
                var days = dates.Select(d =>
                {
                    var planned = CellOf(p, d).Entries.Where(e => e.Kind == RosterKind.Work).Sum(e => (e.End!.Value - e.Start!.Value).TotalHours);
                    var spans = _records.Where(r => r.EmployeeId == p.Id && LocalDate(r.Start) == d).OrderBy(r => r.Start).ToList();
                    var actual = spans.Sum(r => ((r.End ?? Now) - r.Start).TotalHours);
                    var breaks = spans.Zip(spans.Skip(1), (a, b) => (int)(b.Start - (a.End ?? b.Start)).TotalMinutes).Sum();
                    return new TimesheetDay(d, Math.Round(planned, 2), Math.Round(actual, 2), breaks, spans, spans.Count > 0 && TimeWarning is { } w ? [w] : []);
                }).ToList();
                return new TimesheetRow(p.Id, p.DisplayName, p.Color, days, days.Sum(x => x.PlannedHours), days.Sum(x => x.ActualHours), []);
            })
            .ToList();
        return new Timesheet(_location.Id, "AT", monday, dates, rows);
    }

    private TimeRecordDto PostRecord(string body)
    {
        var input = JsonSerializer.Deserialize<TimeRecordInput>(body, ApiClient.Json)!;
        var record = new TimeRecordDto(Guid.NewGuid(), input.EmployeeId, _location.Id, input.Start, input.End, TimeRecordSource.Manual, false);
        _records.Add(record);
        _changes[record.Id] = [new TimeRecordChangeDto(Now, "Katrin Aigner", TimeRecordAction.Created, null, null, input.Start, input.End, input.Reason)];
        return record;
    }

    private TimeRecordDto PutRecord(Guid id, string body)
    {
        var input = JsonSerializer.Deserialize<TimeRecordInput>(body, ApiClient.Json)!;
        var index = _records.FindIndex(r => r.Id == id);
        var old = _records[index];
        _records[index] = old with { Start = input.Start, End = input.End, Source = TimeRecordSource.Manual, Changed = true };
        _changes[id].Add(new TimeRecordChangeDto(Now, "Katrin Aigner", TimeRecordAction.Changed, old.Start, old.End, input.Start, input.End, input.Reason));
        return _records[index];
    }

    private object DeleteRecord(Guid id, string body)
    {
        var input = JsonSerializer.Deserialize<TimeRecordDeletion>(body, ApiClient.Json)!;
        var old = _records.Single(r => r.Id == id);
        _records.Remove(old);
        _changes[id].Add(new TimeRecordChangeDto(Now, "Katrin Aigner", TimeRecordAction.Deleted, old.Start, old.End, null, null, input.Reason));
        return new { };
    }

    private RosterDayResult Put(EmployeeDto person, DateOnly date, string body)
    {
        var input = JsonSerializer.Deserialize<RosterDayInput>(body, ApiClient.Json)!;
        var cell = new RosterCell(date, RosterSource.Plan, input.Entries, input.Note, [.. Warnings], input.Draft);
        _plan[(person.Id, date)] = cell;
        return new RosterDayResult(cell, OutsideAppointments(person));
    }

    private RosterDayResult Reset(EmployeeDto person, DateOnly date, bool draft)
    {
        _plan.Remove((person.Id, date));
        var cell = CellOf(person, date) with { Draft = draft };
        return new RosterDayResult(cell, []);
    }

    private RosterPublishResult Publish()
    {
        var days = _plan.Values.Count(c => c.Draft);
        foreach (var key in _plan.Where(c => c.Value.Draft).Select(c => c.Key).ToList())
        {
            _plan[key] = _plan[key] with { Draft = false };
        }

        return new RosterPublishResult(days, Now, [.. ShortNotice], []);
    }

    private object Discard()
    {
        foreach (var key in _plan.Where(c => c.Value.Draft).Select(c => c.Key).ToList())
        {
            _plan.Remove(key);
        }

        return new { };
    }

    private List<AppointmentDto> OutsideAppointments(EmployeeDto person) =>
    [
        .. Enumerable.Range(0, Outside).Select(i => TestData.Appointment(10 + i, person) with { CustomerName = $"Gast {i + 1}", LocationId = _location.Id }),
    ];

    public static AbsenceRequestDto Request(EmployeeDto person, RosterKind kind, int from, int until, int affected = 0, string? note = null) =>
        new(Guid.NewGuid(), person.Id, person.DisplayName, kind, new DateOnly(2027, 3, from), new DateOnly(2027, 3, until), note, Now.AddDays(-1), affected);
}

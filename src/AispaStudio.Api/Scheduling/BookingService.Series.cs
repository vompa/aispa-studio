using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Wiederkehrende Termine: Vorschau mit Konflikten je Termin, Anlegen, ab hier stornieren
/// und bei „nach dem letzten Besuch“ den nächsten Termin buchen, sobald der letzte erledigt ist.
/// </summary>
internal sealed partial class BookingService
{
    private const int MaxSeriesItems = 52;

    /// <summary>Wie weit eine Ersatzzeit am selben Tag von der gewünschten Uhrzeit abweichen darf.</summary>
    private static readonly TimeSpan SeriesSearchWindow = TimeSpan.FromHours(3);

    private static readonly TimeSpan SeriesStep = TimeSpan.FromMinutes(15);

    /// <summary>Was die Serie ab diesem Termin ergäbe; gebucht wird noch nichts.</summary>
    public async Task<BookingOutcome> PreviewSeriesAsync(Guid appointmentId, SeriesInput rule, CancellationToken ct)
    {
        var appointment = await LoadAsync(appointmentId, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (await SeriesStartProblemAsync(appointment, rule, ct) is { } invalid)
        {
            return invalid;
        }

        var zone = await ZoneAsync(appointment.LocationId, ct);
        var local = TimeZoneInfo.ConvertTime(appointment.Start, zone);
        var dates = SeriesDates(DateOnly.FromDateTime(local.DateTime), rule);
        var fixedEmployee = appointment.Customer.PreferredEmployeeFixed && appointment.Customer.PreferredEmployeeId == appointment.EmployeeId;
        var rules = await CustomerRules.LoadAsync(db, appointment.CustomerId, ct);
        var occurrences = new List<SeriesOccurrence>();
        foreach (var date in dates)
        {
            occurrences.Add(await OccurrenceAsync(appointment.LocationId, appointment.TreatmentId, date, TimeOnly.FromDateTime(local.DateTime),
                appointment.EmployeeId, fixedEmployee, zone, rules, ct));
        }

        return new BookingOutcome.SeriesPreviewed(occurrences);
    }

    /// <summary>Legt die Serie an und bucht die übernommenen Termine; was inzwischen belegt ist, steht in <c>Failed</c>.</summary>
    public async Task<BookingOutcome> CreateSeriesAsync(Guid appointmentId, SeriesCreate input, CancellationToken ct)
    {
        var appointment = await LoadAsync(appointmentId, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (await SeriesStartProblemAsync(appointment, input.Rule, ct) is { } invalid)
        {
            return invalid;
        }

        var max = input.Rule.Mode == SeriesMode.AfterLastVisit ? 1 : MaxSeriesItems;
        if (input.Picks.Count > max)
        {
            return new BookingOutcome.Invalid(nameof(input.Picks), input.Rule.Mode == SeriesMode.AfterLastVisit
                ? "Bei „nach dem letzten Besuch“ wird nur der nächste Termin gebucht."
                : $"Höchstens {MaxSeriesItems} Termine auf einmal.");
        }

        if (input.Picks.Any(p => p.Start <= appointment.Start))
        {
            return new BookingOutcome.Invalid(nameof(input.Picks), "Die Termine der Serie müssen nach diesem Termin liegen.");
        }

        var zone = await ZoneAsync(appointment.LocationId, ct);
        var now = time.GetUtcNow();
        var series = new AppointmentSeries
        {
            LocationId = appointment.LocationId,
            CustomerId = appointment.CustomerId,
            TreatmentId = appointment.TreatmentId,
            IntervalWeeks = input.Rule.IntervalWeeks,
            Mode = input.Rule.Mode,
            Count = input.Rule.Count,
            Until = input.Rule.Until,
            StartTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.Start, zone).DateTime),
            EmployeeId = appointment.EmployeeId,
            CreatedAt = now,
        };
        db.AppointmentSeries.Add(series);
        appointment.SeriesId = series.Id;
        appointment.UpdatedAt = now;
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        var changed = new List<AppointmentDto> { ((BookingOutcome.Success)await PublishAsync(appointment, appointment.Treatment.Name)).Appointment };
        var failed = new List<SeriesFailure>();
        foreach (var pick in input.Picks.OrderBy(p => p.Start))
        {
            var outcome = await BookAsync(new AppointmentRequest(series.LocationId, series.TreatmentId, series.CustomerId, pick.Start,
                pick.EmployeeId, null, null, null), ct, series.Id);
            Collect(outcome, pick.Start, changed, failed);
        }

        return new BookingOutcome.SeriesChanged(new SeriesResult(await SeriesInfoAsync(series.Id, ct), changed, failed));
    }

    /// <summary>Storniert diesen und alle folgenden gebuchten Termine der Serie und beendet sie.</summary>
    public async Task<BookingOutcome> CancelSeriesFromAsync(Guid appointmentId, string? reason, CancellationToken ct)
    {
        var appointment = await LoadAsync(appointmentId, ct);
        if (appointment?.SeriesId is not { } seriesId)
        {
            return appointment is null ? new BookingOutcome.NotFound("Termin") : NotInSeries();
        }

        var series = await db.AppointmentSeries.FirstAsync(s => s.Id == seriesId, ct);
        series.EndedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var ids = await db.Appointments.AsNoTracking()
            .Where(a => a.SeriesId == seriesId && a.Start >= appointment.Start
                && (a.Status == AppointmentStatus.Booked || (a.Id == appointment.Id && a.Status == AppointmentStatus.CheckedIn)))
            .OrderBy(a => a.Start).Select(a => new { a.Id, a.Start }).ToListAsync(ct);
        var changed = new List<AppointmentDto>();
        var failed = new List<SeriesFailure>();
        foreach (var next in ids)
        {
            Collect(await CancelAsync(next.Id, reason, ct), next.Start, changed, failed);
        }

        return new BookingOutcome.SeriesChanged(new SeriesResult(await SeriesInfoAsync(seriesId, ct), changed, failed));
    }

    /// <summary>
    /// „Nach dem letzten Besuch“: ist ein Termin der Serie erledigt und kein weiterer gebucht, den nächsten N Wochen später buchen,
    /// zur gewohnten Uhrzeit bei der gewohnten Person, sonst wie in der Vorschau. Geht nichts, steht der Grund an der Serie.
    /// </summary>
    public async Task ContinueSeriesAsync(Guid appointmentId, CancellationToken ct)
    {
        var done = await db.Appointments.AsNoTracking().Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.Status == AppointmentStatus.Completed, ct);
        if (done?.SeriesId is not { } seriesId
            || await db.AppointmentSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct) is not { Mode: SeriesMode.AfterLastVisit, EndedAt: null } series)
        {
            return;
        }

        var visits = await db.Appointments.AsNoTracking()
            .Where(a => a.SeriesId == seriesId && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.Start, a.Status }).ToListAsync(ct);
        if (visits.Any(v => v.Status == AppointmentStatus.Booked && v.Start > done.Start)
            || (series.Count is { } count && visits.Count - 1 >= count))
        {
            return;
        }

        var zone = await ZoneAsync(series.LocationId, ct);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(done.Start, zone).DateTime).AddDays(7 * series.IntervalWeeks);
        if (series.Until is { } until && date > until)
        {
            return;
        }

        var fixedEmployee = done.Customer.PreferredEmployeeFixed && done.Customer.PreferredEmployeeId == series.EmployeeId;
        var next = await OccurrenceAsync(series.LocationId, series.TreatmentId, date, series.StartTime, series.EmployeeId, fixedEmployee, zone,
            await CustomerRules.LoadAsync(db, done.CustomerId, ct), ct);
        string? problem;
        if (next.State == SeriesOccurrenceState.Conflict)
        {
            problem = $"Nächster Termin am {date:d.M.yyyy} nicht gebucht: {next.Problem}";
        }
        else
        {
            var outcome = await BookAsync(new AppointmentRequest(series.LocationId, series.TreatmentId, series.CustomerId, next.Start,
                next.EmployeeId, null, null, null), ct, series.Id);
            problem = outcome is BookingOutcome.Rejected r ? $"Nächster Termin am {date:d.M.yyyy} nicht gebucht: {r.Reason.Message}" : null;
        }

        series = await db.AppointmentSeries.FirstAsync(s => s.Id == seriesId, ct);
        series.Problem = problem;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Serien für den Kalender, mit allen nicht stornierten Terminen je Serie.</summary>
    public static async Task<List<SeriesInfo>> SeriesInfosAsync(AispaDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var series = await db.AppointmentSeries.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var starts = await db.Appointments.AsNoTracking()
            .Where(a => a.SeriesId != null && ids.Contains(a.SeriesId.Value) && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { SeriesId = a.SeriesId!.Value, a.Start }).ToListAsync(ct);
        return [.. series.Select(s => new SeriesInfo(s.Id, s.IntervalWeeks, s.Mode, s.Count, s.Until,
            [.. starts.Where(a => a.SeriesId == s.Id).Select(a => a.Start).Order()], s.Problem, s.EndedAt is not null))];
    }

    private async Task<SeriesInfo> SeriesInfoAsync(Guid seriesId, CancellationToken ct) => (await SeriesInfosAsync(db, [seriesId], ct)).Single();

    private static void Collect(BookingOutcome outcome, DateTimeOffset start, List<AppointmentDto> changed, List<SeriesFailure> failed)
    {
        switch (outcome)
        {
            case BookingOutcome.Success s:
                changed.Add(s.Appointment);
                break;
            case BookingOutcome.Rejected r:
                failed.Add(new SeriesFailure(start, Describe(r.Reason.Message, r.Reason.Conflicts)));
                break;
            case BookingOutcome.Invalid i:
                failed.Add(new SeriesFailure(start, i.Message));
                break;
            case BookingOutcome.NotFound n:
                failed.Add(new SeriesFailure(start, $"{n.What} nicht gefunden."));
                break;
        }
    }

    private static BookingOutcome.Invalid NotInSeries() => new("seriesId", "Der Termin gehört zu keiner Serie.");

    private async Task<BookingOutcome?> SeriesStartProblemAsync(Appointment appointment, SeriesInput rule, CancellationToken ct)
    {
        if (appointment.SeriesId is not null)
        {
            return new BookingOutcome.Invalid("seriesId", "Der Termin gehört schon zu einer Serie.");
        }

        if (appointment.GroupId is not null)
        {
            return new BookingOutcome.Invalid("groupId", "Gemeinsame Buchungen (Paar, Spa-Tag) lassen sich noch nicht wiederholen.");
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            return new BookingOutcome.Invalid(nameof(appointment.Status), "Stornierte oder nicht wahrgenommene Termine lassen sich nicht wiederholen.");
        }

        if (rule.IntervalWeeks is < 1 or > 8)
        {
            return new BookingOutcome.Invalid(nameof(rule.IntervalWeeks), "Der Abstand muss zwischen 1 und 8 Wochen liegen.");
        }

        if (rule.Count is null && rule.Until is null)
        {
            return new BookingOutcome.Invalid(nameof(rule.Count), "Bitte eine Anzahl oder ein Enddatum angeben.");
        }

        if (rule.Count is < 1 or > MaxSeriesItems)
        {
            return new BookingOutcome.Invalid(nameof(rule.Count), $"Die Anzahl muss zwischen 1 und {MaxSeriesItems} liegen.");
        }

        var zone = await ZoneAsync(appointment.LocationId, ct);
        var date = LocalTime.LocalDate(appointment.Start, zone);
        if (rule.Until is { } until && (until <= date || until > date.AddDays(366)))
        {
            return new BookingOutcome.Invalid(nameof(rule.Until), "Das Enddatum muss nach diesem Termin und höchstens ein Jahr danach liegen.");
        }

        return null;
    }

    private static List<DateOnly> SeriesDates(DateOnly first, SeriesInput rule)
    {
        var dates = new List<DateOnly>();
        var limit = rule.Mode == SeriesMode.AfterLastVisit ? 1 : Math.Min(rule.Count ?? MaxSeriesItems, MaxSeriesItems);
        for (var k = 1; dates.Count < limit; k++)
        {
            var date = first.AddDays(7 * rule.IntervalWeeks * k);
            if (date > (rule.Until ?? first.AddDays(366)) || date > first.AddDays(366))
            {
                break;
            }

            dates.Add(date);
        }

        return dates;
    }

    /// <summary>
    /// Ein Termin der Serie: gleiche Zeit bei derselben Person, sonst dieselbe Person zu einer Zeit in der Nähe, sonst (nicht bei fester
    /// Therapeut:in) jemand anderes zur gleichen oder einer nahen Zeit.
    /// </summary>
    private async Task<SeriesOccurrence> OccurrenceAsync(Guid locationId, Guid treatmentId, DateOnly date, TimeOnly time, Guid employeeId,
        bool fixedEmployee, TimeZoneInfo zone, CustomerRules rules, CancellationToken ct)
    {
        var start = LocalTime.ToUtc(date, time, zone);
        var context = await LoadContextAsync(locationId, treatmentId, date, null, ct);
        if (context is null)
        {
            return new SeriesOccurrence(date, start, null, null, SeriesOccurrenceState.Conflict, "Die Behandlung wird nicht mehr angeboten.");
        }

        var names = await db.Employees.AsNoTracking().Select(e => new { e.Id, e.DisplayName }).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var planner = new ResourcePlanner(rules.Apply(context, employeeId));
        SeriesOccurrence Occurrence(DateTimeOffset at, Guid employee, SeriesOccurrenceState state, string? problem) =>
            new(date, at, employee, names.GetValueOrDefault(employee), state, problem);

        var wish = planner.Plan(start, employeeId);
        if (wish.Plan is { } plan)
        {
            return Occurrence(plan.Times.Start, plan.Employee.Id, SeriesOccurrenceState.Free, null);
        }

        var problem = Describe(wish.Message, wish.Conflicts);
        AvailableSlot? Nearest(Guid? employee) => planner.FreeSlots(date, SeriesStep, employee)
            .Where(s => (s.Start - start).Duration() <= SeriesSearchWindow)
            .MinBy(s => ((s.Start - start).Duration(), s.Start));

        if (Nearest(employeeId) is { } same)
        {
            return Occurrence(same.Start, same.EmployeeId, SeriesOccurrenceState.OtherTime, problem);
        }

        if (!fixedEmployee)
        {
            if (planner.Plan(start).Plan is { } other)
            {
                return Occurrence(other.Times.Start, other.Employee.Id, SeriesOccurrenceState.OtherEmployee, problem);
            }

            if (Nearest(null) is { } any)
            {
                return Occurrence(any.Start, any.EmployeeId, SeriesOccurrenceState.OtherTimeAndEmployee, problem);
            }
        }

        return new SeriesOccurrence(date, start, null, null, SeriesOccurrenceState.Conflict, problem);
    }

    private async Task<TimeZoneInfo> ZoneAsync(Guid locationId, CancellationToken ct) =>
        LocalTime.Zone(await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct));

    /// <summary>„Anna: hat Urlaub“; ohne Einzelheiten die allgemeine Meldung.</summary>
    private static string Describe(string? message, IReadOnlyList<BookingConflict> conflicts) =>
        conflicts.Count > 0 ? string.Join(", ", conflicts.Select(c => $"{c.ResourceName}: {c.Reason}")) : message ?? "Nicht frei.";
}

/// <summary>Queue: ein Termin einer Serie „nach dem letzten Besuch“ ist erledigt; den nächsten buchen.</summary>
public sealed record ContinueSeries(Guid AppointmentId);

/// <summary>Erledigte Termine einer Serie stoßen den nächsten an; entkoppelt von der Statusänderung.</summary>
public static class SeriesAppointmentChangedHandler
{
    public static IEnumerable<object> Handle(AppointmentChangedMessage message)
    {
        if (message.Appointment is { SeriesId: not null, Status: AppointmentStatus.Completed })
        {
            yield return new ContinueSeries(message.Appointment.Id);
        }
    }
}

public static class ContinueSeriesHandler
{
    public static async Task Handle(ContinueSeries message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Appointments.Where(a => a.Id == message.AppointmentId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<BookingService>().ContinueSeriesAsync(message.AppointmentId, ct);
    }
}

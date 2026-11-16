using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Api.TimeTracking;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Kennzahlen zu einer Person für die Übersicht der Personenkarte der Windows-App. Der Endpunkt rechnet nichts Neues, sondern ruft die Rechenwege der Einzel-Endpunkte auf
/// (Dienstplan, Zeiterfassung, Urlaubskonto, Zertifikate) und gibt jeden Teil nur mit dem Recht frei, das auch der Einzel-Endpunkt verlangt.
/// </summary>
internal static class EmployeeOverviewEndpoints
{
    /// <summary>Wie weit voraus der nächste Dienst gesucht wird.</summary>
    private const int ShiftLookaheadDays = 31;

    public static IEndpointRouteBuilder MapEmployeeOverviewEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/employees/{employeeId:guid}/overview", async Task<Results<Ok<EmployeeOverviewDto>, NotFound>> (
                Guid employeeId, AispaDbContext db, RosterService roster, TimeTrackingService times, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var employee = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var today = await StaffRegulationEndpoints.TodayAsync(db, time, ct);

                // Dienstplan, Zeiten und Urlaubskonto: Standortleitung oder Personalunterlagen (wie GetRoster mit employeeId, GetTimesheet und GetLeaveAccount).
                var schedule = access.Has(AccessRight.ManageLocation) || access.Has(AccessRight.ViewStaffRecords);
                (decimal Planned, decimal Actual)? week = schedule ? await WeekAsync(employee, today, access, roster, times, ct) : null;
                var leave = schedule ? await LeaveAsync(db, employeeId, today, ct) : null;
                var nextShift = schedule ? await NextShiftAsync(db, roster, employee, today, time.GetUtcNow(), access, ct) : null;

                // Offene Anträge: wie ListAbsenceRequests nur mit Standortleitung und nur für Personen an den eigenen Standorten.
                int? openRequests = access.Has(AccessRight.ManageLocation) && AbsenceRequestEndpoints.MayDecide(access, employee)
                    ? await db.AbsenceRequests.CountAsync(r => r.EmployeeId == employeeId && r.State == AbsenceRequestState.Open, ct)
                    : null;

                // Zertifikate: nur mit der Personalakte (wie ListStaffCertificates).
                (int Due, int Expired)? certificates = access.Has(AccessRight.ManageHrFile) ? await CertificatesAsync(db, employeeId, today, ct) : null;

                return TypedResults.Ok(new EmployeeOverviewDto(employeeId, nextShift, week?.Planned, week?.Actual, leave, openRequests, certificates?.Due, certificates?.Expired));
            })
            .WithTags("Dienstplan")
            .WithName("GetEmployeeOverview")
            .WithSummary("Übersicht der Personenkarte: nächster Dienst, Stunden der laufenden Woche, Urlaubskonto, offene Abwesenheitsanträge und fällige Zertifikate; Teile ohne das nötige Recht fehlen.")
            .WithDescription("Jeder Teil gilt mit dem Recht des Einzel-Endpunkts (Dienst, Stunden und Urlaub: Standortleitung oder Personalunterlagen; Anträge: Standortleitung; Zertifikate: Personalakte) und ist sonst leer. Wer keines dieser Rechte hat, bekommt 403.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageLocation)
            .AlsoAllow(AccessRight.ManageHrFile, insteadOf: AccessRight.ManageLocation);

        return routes;
    }

    /// <summary>Soll (veröffentlichter Dienstplan) und Ist (Zeiterfassung) der laufenden Woche von Montag bis Sonntag, über alle sichtbaren Standorte; dieselbe Rechnung wie die Zeiten-Maske.</summary>
    private static async Task<(decimal Planned, decimal Actual)> WeekAsync(
        Employee employee, DateOnly today, UserAccess access, RosterService roster, TimeTrackingService times, CancellationToken ct)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var (_, _, rows) = await TimeTrackingEndpoints.RowsAsync([employee], monday, 7, access, roster, times, ct);
        return ((decimal)rows[0].PlannedHours, (decimal)rows[0].ActualHours);
    }

    private static async Task<EmployeeLeaveSummaryDto?> LeaveAsync(AispaDbContext db, Guid employeeId, DateOnly today, CancellationToken ct) =>
        await LeaveAccount.LoadAsync(db, employeeId, today.Year, today, ct) is { } account
            ? new EmployeeLeaveSummaryDto(account.Year, account.Remaining, account.Planned, account.Taken)
            : null;

    /// <summary>
    /// Der nächste Dienst an einem sichtbaren Standort: der heutige, solange er nicht vorbei ist (Ende in der Ortszeit seines Standorts), sonst der erste bis zu
    /// <see cref="ShiftLookaheadDays"/> Tage voraus. Entwürfe zählen nicht; die Wochenarbeitszeit gilt, wo kein Dienstplan für den Tag steht.
    /// </summary>
    private static async Task<EmployeeNextShiftDto?> NextShiftAsync(
        AispaDbContext db, RosterService roster, Employee employee, DateOnly today, DateTimeOffset now, UserAccess access, CancellationToken ct)
    {
        var plans = await roster.PlansAsync([employee], today, today.AddDays(ShiftLookaheadDays), ct);
        var locations = await db.Locations.AsNoTracking().Select(l => new { l.Id, l.Name, l.TimeZone }).ToDictionaryAsync(l => l.Id, ct);
        for (var date = today; date <= today.AddDays(ShiftLookaheadDays); date = date.AddDays(1))
        {
            foreach (var shift in plans[(employee.Id, date)].Shifts.Where(s => s.LocationId is { } id && access.CanSee(id)).OrderBy(s => s.Start))
            {
                if (!locations.TryGetValue(shift.LocationId!.Value, out var location))
                {
                    continue;
                }

                if (LocalTime.ToUtc(date, shift.End!.Value, LocalTime.Zone(location.TimeZone)) > now)
                {
                    return new EmployeeNextShiftDto(date, shift.Start!.Value, shift.End.Value, location.Id, location.Name);
                }
            }
        }

        return null;
    }

    /// <summary>Fällige Zertifikate (ab dem Erinnerungstag, bis sie erneuert sind) und davon die abgelaufenen, nach denselben Regeln wie die Liste der Zertifikate.</summary>
    private static async Task<(int Due, int Expired)> CertificatesAsync(AispaDbContext db, Guid employeeId, DateOnly today, CancellationToken ct)
    {
        var certificates = await db.Set<StaffCertificate>().AsNoTracking().Where(c => c.EmployeeId == employeeId).ToListAsync(ct);
        var due = certificates.Where(c => StaffDutyRules.IsDue(c, today, certificates)).ToList();
        return (due.Count, due.Count(c => c.ValidUntil < today));
    }
}

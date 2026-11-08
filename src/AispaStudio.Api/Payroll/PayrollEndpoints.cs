using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Payroll;

/// <summary>Übergabe an die Lohnverrechnung: Einstellungen, Personalnummern, Monatsvorschau und Export. Gilt fürs ganze Unternehmen, daher ändern nur Inhaber und Manager; lesen und exportieren darf auch das Backoffice.</summary>
internal static partial class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payroll").WithTags("Lohnverrechnung").RequirePermission(AccessRight.ManageOrganization);

        group.MapGet("/settings", async (AispaDbContext db, CancellationToken ct) =>
                ToDto(await db.PayrollSettings.AsNoTracking().SettingsAsync(ct)))
            .WithName("GetPayrollSettings")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageOrganization);

        group.MapPut("/settings", async Task<Results<Ok<PayrollSettingsDto>, ValidationProblem>> (
                PayrollSettingsDto input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Dictionary<string, string[]>();
                if (input.DatevConsultantNumber is { Length: > 0 } consultant && (consultant.Length > 7 || !Digits().IsMatch(consultant)))
                {
                    errors[nameof(input.DatevConsultantNumber)] = ["Beraternummer: bis zu 7 Ziffern."];
                }

                if (input.DatevClientNumber is { Length: > 0 } client && (client.Length > 5 || !Digits().IsMatch(client)))
                {
                    errors[nameof(input.DatevClientNumber)] = ["Mandantennummer: bis zu 5 Ziffern."];
                }

                if (input.NightStart == input.NightEnd)
                {
                    errors[nameof(input.NightEnd)] = ["Nachtzeit: Beginn und Ende müssen sich unterscheiden."];
                }

                foreach (var (field, value) in new[]
                {
                    (nameof(input.HoursWageType), input.HoursWageType), (nameof(input.SundayWageType), input.SundayWageType),
                    (nameof(input.HolidayWageType), input.HolidayWageType), (nameof(input.NightWageType), input.NightWageType),
                    (nameof(input.VacationWageType), input.VacationWageType), (nameof(input.SickWageType), input.SickWageType),
                    (nameof(input.TrainingWageType), input.TrainingWageType),
                })
                {
                    if (value is { Length: > 0 } code && (code.Length > 10 || !WageType().IsMatch(code)))
                    {
                        errors[field] = ["Lohnart: bis zu 10 Ziffern oder Buchstaben."];
                    }
                }

                if (errors.Count > 0)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                var settings = await db.PayrollSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
                if (settings is null)
                {
                    db.PayrollSettings.Add(settings = new PayrollSettings());
                }

                settings.Country = input.Country;
                settings.DatevConsultantNumber = Blank(input.DatevConsultantNumber);
                settings.DatevClientNumber = Blank(input.DatevClientNumber);
                settings.NightStart = input.NightStart;
                settings.NightEnd = input.NightEnd;
                settings.HoursWageType = Blank(input.HoursWageType);
                settings.SundayWageType = Blank(input.SundayWageType);
                settings.HolidayWageType = Blank(input.HolidayWageType);
                settings.NightWageType = Blank(input.NightWageType);
                settings.VacationWageType = Blank(input.VacationWageType);
                settings.SickWageType = Blank(input.SickWageType);
                settings.TrainingWageType = Blank(input.TrainingWageType);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(settings));
            })
            .WithName("SetPayrollSettings");

        group.MapGet("/employees", async (AispaDbContext db, CancellationToken ct) =>
            {
                var numbers = await db.EmployeePayrolls.AsNoTracking().ToDictionaryAsync(p => p.EmployeeId, p => p.PersonnelNumber, ct);
                var employees = await db.Employees.AsNoTracking().OrderBy(e => e.DisplayName).ToListAsync(ct);
                return employees.Select(e => new PayrollEmployeeDto(e.Id, e.DisplayName, e.IsActive, numbers.GetValueOrDefault(e.Id))).ToList();
            })
            .WithName("ListPayrollEmployees")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageOrganization);

        group.MapPut("/employees/{employeeId:guid}", async Task<Results<Ok<PayrollEmployeeDto>, NotFound, ValidationProblem>> (
                Guid employeeId, PersonnelNumberInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var number = Blank(input.PersonnelNumber);
                if (number is { Length: > 20 })
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.PersonnelNumber)] = ["Höchstens 20 Zeichen."] });
                }

                if (number is not null && await db.EmployeePayrolls.AnyAsync(p => p.PersonnelNumber == number && p.EmployeeId != employeeId, ct))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.PersonnelNumber)] = ["Diese Personalnummer hat schon jemand."] });
                }

                var entry = await db.EmployeePayrolls.FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct);
                if (number is null)
                {
                    if (entry is not null)
                    {
                        db.EmployeePayrolls.Remove(entry);
                    }
                }
                else if (entry is null)
                {
                    db.EmployeePayrolls.Add(new EmployeePayroll { EmployeeId = employeeId, PersonnelNumber = number });
                }
                else
                {
                    entry.PersonnelNumber = number;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new PayrollEmployeeDto(employee.Id, employee.DisplayName, employee.IsActive, number));
            })
            .WithName("SetPersonnelNumber");

        group.MapGet("/preview", async Task<Results<Ok<PayrollPreviewDto>, ValidationProblem>> (
                int year, int month, PayrollExport export, CancellationToken ct) =>
                Period(year, month) is { } problem ? problem : TypedResults.Ok(await export.PreviewAsync(year, month, ct)))
            .WithName("PreviewPayroll")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageOrganization)
            .WithSummary("Monatswerte je Person aus dem Dienstplan: Stunden, Sonntags-, Feiertags- und Nachtstunden, Abwesenheiten.");

        group.MapGet("/export", async Task<Results<FileContentHttpResult, ValidationProblem>> (
                PayrollFormat format, int year, int month, PayrollExport export, CancellationToken ct) =>
            {
                if (Period(year, month) is { } problem)
                {
                    return problem;
                }

                var (content, name, type) = await export.FileAsync(format, year, month, ct);
                return TypedResults.File(content, type, name);
            })
            .WithName("ExportPayroll")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageOrganization)
            .WithSummary("Datei für DATEV LODAS, DATEV Lohn und Gehalt oder BMD Lohn; nur Personen mit Personalnummer und Positionen mit Lohnart.");

        return routes;
    }

    private static ValidationProblem? Period(int year, int month) =>
        year is < 2000 or > 2100 || month is < 1 or > 12
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Monat 1 bis 12, Jahr 2000 bis 2100."] })
            : null;

    private static PayrollSettingsDto ToDto(PayrollSettings s) => new(s.Country, s.DatevConsultantNumber, s.DatevClientNumber, s.NightStart, s.NightEnd,
        s.HoursWageType, s.SundayWageType, s.HolidayWageType, s.NightWageType, s.VacationWageType, s.SickWageType, s.TrainingWageType);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex Digits();

    [GeneratedRegex("^[0-9A-Za-z]+$")]
    private static partial Regex WageType();
}

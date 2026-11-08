using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Payroll;

/// <summary>Einstellungen für den Lohnexport, eine Zeile je Unternehmen; fehlt sie, gelten die Vorgaben (angelegt beim ersten Speichern).</summary>
public sealed class PayrollSettings
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public FiscalCountry Country { get; set; } = FiscalCountry.AT;
    public string? DatevConsultantNumber { get; set; }
    public string? DatevClientNumber { get; set; }

    /// <summary>Nachtzeit; Vorgabe 22 bis 6 Uhr (AZG/ARG, viele Kollektivverträge). In Deutschland gilt für § 3b EStG 20 bis 6 Uhr.</summary>
    public TimeOnly NightStart { get; set; } = new(22, 0);

    public TimeOnly NightEnd { get; set; } = new(6, 0);

    // Vorgaben als Platzhalter: die echten Lohnarten legt die Steuerberatung bzw. Lohnverrechnung fest.
    public string? HoursWageType { get; set; } = "100";
    public string? SundayWageType { get; set; } = "110";
    public string? HolidayWageType { get; set; } = "120";
    public string? NightWageType { get; set; } = "130";
    public string? VacationWageType { get; set; } = "200";
    public string? SickWageType { get; set; } = "210";
    public string? TrainingWageType { get; set; } = "220";

    public string? WageType(PayrollItem item) => item switch
    {
        PayrollItem.Hours => HoursWageType,
        PayrollItem.SundayHours => SundayWageType,
        PayrollItem.HolidayHours => HolidayWageType,
        PayrollItem.NightHours => NightWageType,
        PayrollItem.VacationDays => VacationWageType,
        PayrollItem.SickDays => SickWageType,
        _ => TrainingWageType,
    };
}

/// <summary>Personalnummer der Lohnverrechnung je Person; neben den Stammdaten, damit die Personenverwaltung unverändert bleibt.</summary>
public sealed class EmployeePayroll
{
    public Guid EmployeeId { get; init; }
    public required string PersonnelNumber { get; set; }
}

internal static class PayrollModel
{
    public static async Task<PayrollSettings> SettingsAsync(this IQueryable<PayrollSettings> settings, CancellationToken ct) =>
        await settings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new PayrollSettings();

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<PayrollSettings>(e =>
        {
            e.Property(s => s.Country).HasConversion<string>().HasMaxLength(2);
            e.Property(s => s.DatevConsultantNumber).HasMaxLength(7);
            e.Property(s => s.DatevClientNumber).HasMaxLength(5);
            e.Property(s => s.HoursWageType).HasMaxLength(10);
            e.Property(s => s.SundayWageType).HasMaxLength(10);
            e.Property(s => s.HolidayWageType).HasMaxLength(10);
            e.Property(s => s.NightWageType).HasMaxLength(10);
            e.Property(s => s.VacationWageType).HasMaxLength(10);
            e.Property(s => s.SickWageType).HasMaxLength(10);
            e.Property(s => s.TrainingWageType).HasMaxLength(10);
        });
        builder.Entity<EmployeePayroll>(e =>
        {
            e.HasKey(p => p.EmployeeId);
            e.Property(p => p.PersonnelNumber).HasMaxLength(20);
            e.HasOne<Employee>().WithOne().HasForeignKey<EmployeePayroll>(p => p.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

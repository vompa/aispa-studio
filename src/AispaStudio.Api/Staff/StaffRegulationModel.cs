using AispaStudio.Api.HrFile;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>Was Hinweise, Sonderregelungen und Vollmachten gemeinsam haben: Sie gehören zu einer Person.</summary>
public interface IStaffRegulation
{
    Guid Id { get; }

    Guid EmployeeId { get; }
}

/// <summary>Hinweis zu einer Person (R05 Slice 111, Ä252): im Schema <c>hr</c>, damit der Verlauf nur Art, Person und Zeit schreibt.</summary>
public sealed class StaffNote : IStaffRegulation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public string Text { get; set; } = "";

    public StaffNoteVisibility Visibility { get; set; }

    public bool ShowAsSymbol { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }
}

/// <summary>Sonderregelung einer Person (E10): warnt beim Planen und Buchen, sperrt nie.</summary>
public sealed class StaffRule : IStaffRegulation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public StaffRuleKind Kind { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public TimeOnly? LatestEnd { get; set; }

    public decimal? MaxHours { get; set; }

    public List<DayOfWeek> DaysOff { get; set; } = [];

    public string? Reason { get; set; }
}

/// <summary>Vollmacht einer Person (E11): gilt zusätzlich zur Rolle und erweitert sie nie.</summary>
public sealed class StaffPower : IStaffRegulation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public StaffPowerKind Kind { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public string GrantedBy { get; set; } = "";
}

internal static class StaffRegulationModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StaffNote>(e =>
        {
            e.ToTable("StaffNotes", HrFileModel.Schema);
            e.Property(n => n.Id).ValueGeneratedNever();
            e.Property(n => n.Text).HasMaxLength(500);
            e.Property(n => n.Visibility).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(n => n.EmployeeId);
            e.HasOne(n => n.Employee).WithMany().HasForeignKey(n => n.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StaffRule>(e =>
        {
            e.ToTable("StaffRules", HrFileModel.Schema);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.MaxHours).HasPrecision(4, 2);
            e.Property(r => r.Reason).HasMaxLength(500);
            e.HasIndex(r => r.EmployeeId);
            e.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StaffPower>(e =>
        {
            e.ToTable("StaffPowers", HrFileModel.Schema);
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.GrantedBy).HasMaxLength(256);
            e.HasIndex(p => new { p.Kind, p.EmployeeId });
            e.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>Gilt der Eintrag an diesem Tag? „Bis“ einschließlich; offene Grenzen gelten immer.</summary>
    public static bool IsValidOn(DateOnly day, DateOnly? from, DateOnly? until) => (from is null || from <= day) && (until is null || until >= day);
}

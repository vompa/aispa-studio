using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.TimeTracking;

/// <summary>
/// Ein Abschnitt gearbeiteter Zeit (Ist-Zeit). Kommen öffnet einen Abschnitt, Pause und Gehen schließen ihn; Pausen sind die Lücken.
/// Gelöschte Abschnitte bleiben mit Protokoll erhalten (Aufzeichnungspflicht § 26 AZG, Nachweis gegenüber Arbeitsinspektorat und Zoll).
/// </summary>
public sealed class TimeRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public Guid LocationId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public TimeRecordSource Source { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TimeRecordChange> Changes { get; init; } = [];
}

/// <summary>Änderungsprotokoll: wer hat wann was geändert und warum. Wird nie geändert oder gelöscht.</summary>
public sealed class TimeRecordChange
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TimeRecordId { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public required string ChangedByUserId { get; init; }
    public required string ChangedByName { get; init; }
    public TimeRecordAction Action { get; init; }
    public DateTimeOffset? OldStart { get; init; }
    public DateTimeOffset? OldEnd { get; init; }
    public DateTimeOffset? NewStart { get; init; }
    public DateTimeOffset? NewEnd { get; init; }
    public string? Reason { get; init; }
}

internal static class TimeTrackingModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<TimeRecord>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(r => new { r.EmployeeId, r.Start });
            e.HasIndex(r => new { r.LocationId, r.Start });
            // Höchstens ein laufender Abschnitt je Person.
            e.HasIndex(r => r.EmployeeId).IsUnique().HasFilter("\"End\" IS NULL AND NOT \"IsDeleted\"").HasDatabaseName("IX_TimeRecords_Running");
            e.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Changes).WithOne().HasForeignKey(c => c.TimeRecordId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("TimeRecords", t => t.HasCheckConstraint("CK_TimeRecords_Order", "\"End\" IS NULL OR \"Start\" < \"End\""));
        });

        builder.Entity<TimeRecordChange>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.ChangedByUserId).HasMaxLength(450);
            e.Property(c => c.ChangedByName).HasMaxLength(200);
            e.Property(c => c.Action).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Reason).HasMaxLength(500);
        });
    }
}

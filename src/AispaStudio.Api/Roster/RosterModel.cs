using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Dienstplan einer Person für ein Datum. Gibt es ihn, gilt er für diesen Tag statt der Wochenarbeitszeit,
/// an allen Standorten. Ohne Einträge bedeutet er: an diesem Tag kein Dienst.
/// </summary>
public sealed class RosterDay
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
    public string? Note { get; set; }
    public List<RosterDayEntry> Entries { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}

public sealed class RosterDayEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RosterDayId { get; init; }
    public RosterKind Kind { get; init; }
    public Guid? LocationId { get; init; }
    public TimeOnly? Start { get; init; }
    public TimeOnly? End { get; init; }
}

/// <summary>
/// Entwurf für einen Tag einer Person: gilt erst, wenn die Standortleitung den Plan veröffentlicht. <c>Reset</c> heißt: zurück zur
/// Wochenarbeitszeit. <c>LocationId</c> ist der Standort, dessen Plan den Entwurf enthält und veröffentlicht.
/// </summary>
public sealed class RosterDraft
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public DateOnly Date { get; init; }
    public Guid LocationId { get; set; }
    public bool Reset { get; set; }
    public string? Note { get; set; }
    public List<RosterDraftEntry> Entries { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}

public sealed class RosterDraftEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RosterDraftId { get; init; }
    public RosterKind Kind { get; init; }
    public Guid? LocationId { get; init; }
    public TimeOnly? Start { get; init; }
    public TimeOnly? End { get; init; }
}

/// <summary>Veröffentlichung des Dienstplans eines Standorts für einen Zeitraum: belegt, wann das Team die Einteilung bekommen hat.</summary>
public sealed class RosterPublication
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }
    public int Days { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public string? PublishedByUserId { get; init; }
}

internal static class RosterModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<RosterDay>(e =>
        {
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Note).HasMaxLength(500);
            e.Property(d => d.UpdatedByUserId).HasMaxLength(450);
            e.HasIndex(d => new { d.EmployeeId, d.Date }).IsUnique();
            e.HasOne<Employee>().WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(d => d.Entries).WithOne().HasForeignKey(x => x.RosterDayId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RosterDraft>(e =>
        {
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Note).HasMaxLength(500);
            e.Property(d => d.UpdatedByUserId).HasMaxLength(450);
            e.HasIndex(d => new { d.EmployeeId, d.Date }).IsUnique();
            e.HasIndex(d => new { d.LocationId, d.Date });
            e.HasOne<Employee>().WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Location>().WithMany().HasForeignKey(d => d.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(d => d.Entries).WithOne().HasForeignKey(x => x.RosterDraftId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RosterDraftEntry>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RosterPublication>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.PublishedByUserId).HasMaxLength(450);
            e.HasIndex(p => new { p.LocationId, p.PublishedAt });
            e.HasOne<Location>().WithMany().HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RosterDayEntry>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("RosterDayEntries", t => t.HasCheckConstraint("CK_RosterDayEntries_Work",
                "(\"Kind\" = 'Work') = (\"LocationId\" IS NOT NULL AND \"Start\" IS NOT NULL AND \"End\" IS NOT NULL AND \"Start\" < \"End\")"));
        });
    }
}

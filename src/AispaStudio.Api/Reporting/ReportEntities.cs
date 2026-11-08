using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

// Daten für Behandlungskarte und Vorbereitungs-Todo, neben dem Kalendermodell: die Planung braucht sie nicht.

/// <summary>Schritt im Behandlungsrezept, z. B. „Steine auf 55 °C aufheizen“ 30 Minuten vor Beginn.</summary>
public sealed class TreatmentPreparationStep
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TreatmentId { get; init; }
    public int Position { get; set; }
    public required string Text { get; init; }
    public int? MinutesBefore { get; init; }

    /// <summary>Lagerartikel, den der Schritt braucht; Menge und Abbuchung kommen aus dem Rezept der Behandlung.</summary>
    public Guid? StockItemId { get; init; }
}

/// <summary>Vorlieben eines Gastes (keine Gesundheitsdaten), gelten an allen Standorten.</summary>
public sealed class CustomerPreference
{
    public Guid CustomerId { get; init; }
    public string? Pressure { get; set; }
    public string? Scent { get; set; }
    public string? RoomTemperature { get; set; }
    public string? Music { get; set; }
    public bool? PrefersQuiet { get; set; }
    public string? FocusAreas { get; set; }
    public string? AvoidAreas { get; set; }
    public string? Other { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Abgehakter Vorbereitungsschritt eines Termins.</summary>
public sealed class AppointmentPreparationCheck
{
    public Guid AppointmentId { get; init; }
    public Guid StepId { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
    public string? UserId { get; init; }
}

/// <summary>Nachbereitung eines Termins; die Notiz erscheint auf den nächsten Karten des Gastes.</summary>
public sealed class AppointmentFollowUp
{
    public Guid AppointmentId { get; init; }
    public required string Note { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UserId { get; set; }
}

internal static class ReportModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<TreatmentPreparationStep>(e =>
        {
            e.HasOne<Treatment>().WithMany().HasForeignKey(s => s.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.TreatmentId, s.Position });
            e.Property(s => s.Text).HasMaxLength(300);
            e.HasOne<StockItem>().WithMany().HasForeignKey(s => s.StockItemId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CustomerPreference>(e =>
        {
            e.HasKey(p => p.CustomerId);
            e.HasOne<Customer>().WithOne().HasForeignKey<CustomerPreference>(p => p.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Pressure).HasMaxLength(100);
            e.Property(p => p.Scent).HasMaxLength(100);
            e.Property(p => p.RoomTemperature).HasMaxLength(100);
            e.Property(p => p.Music).HasMaxLength(100);
            e.Property(p => p.FocusAreas).HasMaxLength(300);
            e.Property(p => p.AvoidAreas).HasMaxLength(300);
            e.Property(p => p.Other).HasMaxLength(1000);
        });

        builder.Entity<AppointmentPreparationCheck>(e =>
        {
            e.HasKey(c => new { c.AppointmentId, c.StepId });
            e.HasOne<Appointment>().WithMany().HasForeignKey(c => c.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TreatmentPreparationStep>().WithMany().HasForeignKey(c => c.StepId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.UserId).HasMaxLength(450);
        });

        builder.Entity<AppointmentFollowUp>(e =>
        {
            e.HasKey(f => f.AppointmentId);
            e.HasOne<Appointment>().WithOne().HasForeignKey<AppointmentFollowUp>(f => f.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(f => f.Note).HasMaxLength(2000);
            e.Property(f => f.UserId).HasMaxLength(450);
        });

        builder.Entity<BriefingSummary>(e =>
        {
            e.HasOne<Location>().WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.LocationId, s.Date }).IsUnique();
            e.Property(s => s.Fingerprint).HasMaxLength(64);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Text).HasMaxLength(600);
        });
    }
}

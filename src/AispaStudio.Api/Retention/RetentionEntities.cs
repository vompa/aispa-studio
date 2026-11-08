using AispaStudio.Api.Reminders;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Retention;

/// <summary>Erinnerungen an die nächste Behandlung. Ohne Eintrag ausgeschaltet: es ist Werbung und das Spa schaltet sie bewusst ein.</summary>
public sealed class RebookSettings
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public bool Enabled { get; set; }
}

/// <summary>Nach wie vielen Tagen seit der letzten abgeschlossenen Behandlung erinnert wird. Ohne Eintrag keine Erinnerung.</summary>
public sealed class TreatmentRebookInterval
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TreatmentId { get; init; }
    public int Days { get; set; }
}

/// <summary>
/// Einwilligung eines Gastes (ADR 0040). Gilt für genau die Adresse, an die angefragt wurde; ändert sich die Mailadresse des
/// Gastes, gehen keine Erinnerungen mehr, bis neu angefragt und bestätigt ist.
/// </summary>
public sealed class MarketingConsent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public required string Email { get; set; }
    public string Language { get; set; } = "de";
    public MarketingConsentState State { get; set; } = MarketingConsentState.Pending;
    public required string Source { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Zeitpunkt des Klicks auf „Bestätigen“: der Nachweis der Einwilligung.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? WithdrawnAt { get; set; }
}

/// <summary>Eine Erinnerung an die nächste Behandlung. Eindeutig je abgeschlossenem Termin, nach dem sie fällig wurde.</summary>
public sealed class RebookReminder
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public Guid TreatmentId { get; init; }
    public Guid AfterAppointmentId { get; init; }
    public ReminderState State { get; set; } = ReminderState.Queued;

    /// <summary>Der persönliche Teil, wie er verschickt wurde.</summary>
    public string? Text { get; set; }

    public bool AiWritten { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
}

internal static class RetentionModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<RebookSettings>(e => e.Property(s => s.Id).ValueGeneratedNever());
        builder.Entity<TreatmentRebookInterval>(e =>
        {
            e.Property(i => i.Id).ValueGeneratedNever();
            e.HasIndex(i => i.TreatmentId).IsUnique();
        });
        builder.Entity<MarketingConsent>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Email).HasMaxLength(320);
            e.Property(c => c.Language).HasMaxLength(8);
            e.Property(c => c.State).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Source).HasMaxLength(16);
            e.HasIndex(c => c.CustomerId).IsUnique();
        });
        builder.Entity<RebookReminder>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.State).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Text).HasMaxLength(1000);
            e.Property(r => r.Error).HasMaxLength(500);
            e.HasIndex(r => r.AfterAppointmentId).IsUnique();
            e.HasIndex(r => new { r.CustomerId, r.CreatedAt });
        });
    }
}

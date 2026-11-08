using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reminders;

/// <summary>Terminerinnerungen eines Spas. Ohne Eintrag gelten die Vorgaben (48 und 2 Stunden vorher, per Mail).</summary>
public sealed class ReminderSettings
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public bool Enabled { get; set; } = true;

    /// <summary>Erste Erinnerung so viele Stunden vor Beginn; 0 schaltet sie ab.</summary>
    public int FirstHoursBefore { get; set; } = 48;

    /// <summary>Zweite Erinnerung so viele Stunden vor Beginn; 0 schaltet sie ab.</summary>
    public int SecondHoursBefore { get; set; } = 2;

    /// <summary>Bevorzugter Kanal; fehlt dafür Nummer oder Adresse, wird der andere genommen.</summary>
    public ReminderChannel Channel { get; set; } = ReminderChannel.Email;

    /// <summary>
    /// Freigegebene WhatsApp-Vorlage: bei Meta ihr Name, bei Twilio die Content-SID (HX…). Außerhalb von 24 Stunden nach der
    /// letzten Nachricht des Gastes lässt WhatsApp nur Vorlagen zu. Ohne Vorlage geht die Erinnerung per SMS.
    /// </summary>
    public string? WhatsAppTemplate { get; set; }

    /// <summary>Sprache der Vorlage, wie bei Meta angelegt (z. B. „de“, „de_AT“).</summary>
    public string WhatsAppTemplateLanguage { get; set; } = "de";
}

/// <summary>Eine verschickte (oder versuchte) Erinnerung. Eindeutig je Termin, Stufe und Beginn: nach dem Verschieben gibt es neue.</summary>
public sealed class AppointmentReminder
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AppointmentId { get; init; }
    public int Stage { get; init; }
    public DateTimeOffset AppointmentStart { get; init; }
    public ReminderChannel Channel { get; set; }
    public ReminderState State { get; set; } = ReminderState.Queued;
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
}

public enum ReminderState
{
    Queued,
    Sent,
    Skipped,
    Failed,
}

internal static class ReminderModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ReminderSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.WhatsAppTemplate).HasMaxLength(200);
            e.Property(s => s.WhatsAppTemplateLanguage).HasMaxLength(16);
        });
        builder.Entity<AppointmentReminder>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.State).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Error).HasMaxLength(500);
            e.HasIndex(r => new { r.AppointmentId, r.Stage, r.AppointmentStart }).IsUnique();
        });
    }
}

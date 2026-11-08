using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Schließzeit eines Standorts (ADR 0073): Betriebsurlaub, Feiertag oder Sonderschließung, Ausfall durch einen Schaden. In dieser Zeit liefert der Planer keine
/// freien Zeiten und nimmt keine Buchung an (Online, Kalender, Assistenten, Warteliste); bestehende Termine bleiben unberührt, bis jemand sie in einer
/// Rückfrage absagt (<see cref="ClosureNotice"/>). Kalendertage in Ortszeit des Standorts; Uhrzeiten nur für halbe Tage.
/// </summary>
public sealed class Closure
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }

    /// <summary>Erster geschlossener Tag.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>Uhrzeit, ab der am ersten Tag geschlossen ist; leer: ab 0:00 Uhr.</summary>
    public TimeOnly? FromTime { get; set; }

    /// <summary>Letzter geschlossener Tag (einschließlich).</summary>
    public DateOnly UntilDate { get; set; }

    /// <summary>Uhrzeit, bis zu der am letzten Tag geschlossen ist; leer: bis zum Ende des Tages.</summary>
    public TimeOnly? UntilTime { get; set; }

    public ClosureKind Kind { get; set; }

    /// <summary>Grund für das Team, z. B. „Wasserschaden im Keller, Trocknung“; Gäste sehen ihn nie.</summary>
    public string? InternalReason { get; set; }

    /// <summary>Kurzer Text für Gäste (Kundenbereich, Assistent, Absage); leer: Standardtext der Art.</summary>
    public string? GuestText { get; set; }

    public string CreatedByUserId { get; init; } = "";

    /// <summary>Name der Person zum Zeitpunkt des Anlegens (bleibt stehen, auch wenn der Zugang entfällt).</summary>
    public string CreatedByName { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Beginn in UTC.</summary>
    public DateTimeOffset StartsAt(TimeZoneInfo zone) => LocalTime.ToUtc(FromDate, FromTime ?? TimeOnly.MinValue, zone);

    /// <summary>Ende in UTC (ausschließlich): die Uhrzeit des letzten Tages, sonst der Beginn des Folgetags.</summary>
    public DateTimeOffset EndsAt(TimeZoneInfo zone) =>
        UntilTime is { } time ? LocalTime.ToUtc(UntilDate, time, zone) : LocalTime.ToUtc(UntilDate.AddDays(1), TimeOnly.MinValue, zone);

    /// <summary>Zustand zu einem Zeitpunkt: vorbei, läuft oder beginnt später.</summary>
    public ClosurePhase PhaseAt(DateTimeOffset now, TimeZoneInfo zone) =>
        now >= EndsAt(zone) ? ClosurePhase.Ended : now >= StartsAt(zone) ? ClosurePhase.Active : ClosurePhase.Planned;
}

/// <summary>
/// Absage eines Termins wegen einer Schließzeit und die Nachricht an den Gast: Protokoll je Termin (wer, wann, Kanal, Text). Entsteht erst nach der Rückfrage
/// (ADR 0073); der Versand läuft über die Queue (<see cref="SendClosureNotice"/>). Dieselbe Zeile hält auch die Benachrichtigung eines Wartelisten-Eintrags fest
/// (<see cref="WaitlistEntryId"/> statt <see cref="AppointmentId"/>, Ä48): der Eintrag liegt im Schema „online“ und bleibt auf der Liste.
/// </summary>
public sealed class ClosureNotice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ClosureId { get; init; }

    /// <summary>Der abgesagte Termin; leer bei der Benachrichtigung zur Warteliste.</summary>
    public Guid? AppointmentId { get; init; }

    /// <summary>Der benachrichtigte Wartelisten-Eintrag (ohne Fremdschlüssel: er liegt in einem anderen Schema); leer bei der Absage eines Termins.</summary>
    public Guid? WaitlistEntryId { get; init; }

    /// <summary>Sprache der Nachricht aus den Kundendaten (de, en, fr, it); Deutsch, wenn der Gast keine oder eine nicht unterstützte hat.</summary>
    public string Language { get; init; } = "de";

    /// <summary>Kanal der Nachricht; <see cref="ClosureChannel.None"/>, wenn der Gast weder Mailadresse noch Telefonnummer hat.</summary>
    public ClosureChannel Channel { get; set; }

    public ClosureNoticeState State { get; set; } = ClosureNoticeState.Queued;
    public string? Error { get; set; }

    /// <summary>Betreff (nur Mail); für SMS leer.</summary>
    public string? Subject { get; init; }

    /// <summary>Der Text, wie er an diesen Gast ging (Platzhalter ersetzt, ohne den Hinweis auf die KI).</summary>
    public required string Text { get; init; }

    /// <summary>Der Text wurde mit KI umformuliert; die Nachricht trägt den Hinweis darauf.</summary>
    public bool AiWritten { get; init; }

    /// <summary>Die Nachricht enthält den Link zum Neubuchen im Kundenbereich.</summary>
    public bool OfferedRebooking { get; init; }

    /// <summary>Die Anzahlung, die mit der Absage über die Kassa retourniert wurde (Storno-Beleg <see cref="RefundReceiptId"/>); die Nachricht nennt sie dann.</summary>
    public decimal? RefundAmount { get; init; }

    public Guid? RefundReceiptId { get; init; }

    /// <summary>Auf welchem Zahlungsweg die Anzahlung zurückging, wie auf dem Storno-Beleg: „Bar“, „Karte“ (Ä53); leer ohne Rückzahlung.</summary>
    public string? RefundWay { get; init; }

    /// <summary>Warum die Anzahlung nicht retourniert werden konnte (z. B. Kassa außer Betrieb); sie bleibt dann zu erstatten, und die Nachricht nennt keine Rückzahlung.</summary>
    public string? RefundError { get; init; }

    /// <summary>Die Korrektur zu einer Absage, die das Team zurückgenommen hat (Ä95): der Gast bekommt sie zusätzlich zur Absage.</summary>
    public bool IsCorrection { get; init; }

    public string SentByUserId { get; init; } = "";
    public string SentByName { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Wann die Nachricht rausgeht (Wartezeit des Standorts, Ä95); leer bei Nachrichten ohne Kanal.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }
}

internal static class ClosureModel
{
    public const int InternalReasonLength = 200;
    public const int GuestTextLength = 200;
    public const int SubjectLength = 200;
    public const int TextLength = 2000;

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Closure>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.InternalReason).HasMaxLength(InternalReasonLength);
            e.Property(c => c.GuestText).HasMaxLength(GuestTextLength);
            e.Property(c => c.CreatedByUserId).HasMaxLength(450);
            e.Property(c => c.CreatedByName).HasMaxLength(200);
            e.HasIndex(c => new { c.LocationId, c.FromDate, c.UntilDate });
            e.HasOne<Location>().WithMany().HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClosureNotice>(e =>
        {
            e.Property(n => n.Id).ValueGeneratedNever();
            e.Property(n => n.Language).HasMaxLength(2);
            e.Property(n => n.RefundError).HasMaxLength(500);
            e.Property(n => n.RefundWay).HasMaxLength(100);
            e.Property(n => n.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(n => n.State).HasConversion<string>().HasMaxLength(16);
            e.Property(n => n.Error).HasMaxLength(500);
            e.Property(n => n.Subject).HasMaxLength(SubjectLength);
            e.Property(n => n.Text).HasMaxLength(TextLength + 400);
            e.Property(n => n.SentByUserId).HasMaxLength(450);
            e.Property(n => n.SentByName).HasMaxLength(200);
            // Ein Termin wird höchstens einmal wegen derselben Schließzeit abgesagt, ein Wartelisten-Eintrag höchstens einmal benachrichtigt.
            e.HasIndex(n => new { n.ClosureId, n.AppointmentId, n.IsCorrection }).IsUnique();
            e.HasIndex(n => new { n.ClosureId, n.WaitlistEntryId }).IsUnique();
            e.HasOne<Closure>().WithMany().HasForeignKey(n => n.ClosureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Appointment>().WithMany().HasForeignKey(n => n.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

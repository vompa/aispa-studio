using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Operations;

// Betrieb im Haus, neben dem Kalendermodell: die Planung braucht diese Daten nicht.

/// <summary>Zeile von „Zum Termin bereitstellen“ einer Behandlung, z. B. Früchteteller 10 Minuten vor Ende, optional mit Lagerartikel und Menge je Termin.</summary>
public sealed class TreatmentExtra
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TreatmentId { get; init; }
    public int Position { get; set; }
    public required string Name { get; init; }
    public ExtraTiming Timing { get; init; }
    public int? MinutesBeforeEnd { get; init; }
    public Guid? StockItemId { get; init; }
    public decimal Quantity { get; init; }
}

/// <summary>Abgehaktes „Bereitstellen“ eines Termins.</summary>
public sealed class AppointmentExtraCheck
{
    public Guid AppointmentId { get; init; }
    public Guid ExtraId { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
    public string? UserId { get; init; }
}

/// <summary>Eintrag im Rezeptionshandbuch des Spas; <see cref="Days"/> ist eine Wochentag-Bitmaske (Bit <c>1 &lt;&lt; (int)DayOfWeek</c>), 0 heißt täglich.</summary>
public sealed class HandbookEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Title { get; set; }
    public required string Text { get; set; }
    public string? Area { get; set; }
    public bool RemindAtDayStart { get; set; }
    public int Days { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }

    public bool AppliesOn(DayOfWeek day) => Days == 0 || (Days & (1 << (int)day)) != 0;
}

/// <summary>Raumpflege nach einem Termin abgehakt, mit Person und Zeit. Gilt für alle Termine desselben Raums mit gleichem Pflegebeginn.</summary>
public sealed class RoomCareCheck
{
    public Guid AppointmentId { get; init; }
    public Guid RoomId { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
    public string? UserId { get; init; }
}

/// <summary>
/// Antwort auf eine Zeile des Raumchecks nach der Behandlung (ADR 0068): „aufgefüllt“ oder „reicht noch“, mit Person und Zeit. <c>AppointmentId</c> ist der Termin,
/// an dem die Raumpflege-Aufgabe hängt; die Antwort bleibt, auch wenn der Bestand danach wieder über der Mindestmenge liegt.
/// </summary>
public sealed class RoomCareRestockAnswer
{
    public Guid AppointmentId { get; init; }
    public Guid ItemId { get; init; }
    public RestockState State { get; set; }
    public DateTimeOffset At { get; set; }
    public string? UserId { get; set; }
}

/// <summary>Punkt der Tagesstart-Kontrolle eines Raums, z. B. „Sauna aufheizen“. Entfernte Punkte werden ausgeschaltet, nicht gelöscht: das Hygieneprotokoll behält sie.</summary>
public sealed class RoomStartItem
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RoomId { get; init; }
    public int Position { get; set; }
    public required string Text { get; init; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Abgehakter Punkt der Tagesstart-Kontrolle an einem Tag (Ortszeit des Standorts), mit Person und Zeit.</summary>
public sealed class RoomStartCheck
{
    public Guid RoomId { get; init; }
    public DateOnly Date { get; init; }
    public Guid ItemId { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
    public string? UserId { get; init; }
}

/// <summary>Abnahme eines Raums an einem Tag durch die Standortleitung; <see cref="RecordedByUserId"/> ist, wer am Gerät angemeldet war (bei Abnahme per PIN).</summary>
public sealed class RoomApproval
{
    public Guid RoomId { get; init; }
    public DateOnly Date { get; init; }
    public RoomApprovalKind Kind { get; init; }
    public DateTimeOffset ApprovedAt { get; init; }
    public string? ApprovedByUserId { get; init; }
    public string? RecordedByUserId { get; init; }
}

internal static class OperationsModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<TreatmentExtra>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne<Treatment>().WithMany().HasForeignKey(x => x.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TreatmentId, x.Position });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Timing).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExtraTiming.OnArrival);
            e.Property(x => x.Quantity).HasPrecision(12, 3);
        });

        builder.Entity<AppointmentExtraCheck>(e =>
        {
            e.HasKey(c => new { c.AppointmentId, c.ExtraId });
            e.HasOne<Appointment>().WithMany().HasForeignKey(c => c.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TreatmentExtra>().WithMany().HasForeignKey(c => c.ExtraId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.UserId).HasMaxLength(450);
        });

        builder.Entity<RoomCareCheck>(e =>
        {
            e.HasKey(c => c.AppointmentId);
            e.HasOne<Appointment>().WithOne().HasForeignKey<RoomCareCheck>(c => c.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Room>().WithMany().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.RoomId);
            e.Property(c => c.UserId).HasMaxLength(450);
        });

        builder.Entity<RoomCareRestockAnswer>(e =>
        {
            e.HasKey(a => new { a.AppointmentId, a.ItemId });
            e.HasOne<Appointment>().WithMany().HasForeignKey(a => a.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<StockItem>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.State).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.UserId).HasMaxLength(450);
        });

        builder.Entity<RoomStartItem>(e =>
        {
            e.Property(i => i.Id).ValueGeneratedNever();
            e.HasOne<Room>().WithMany().HasForeignKey(i => i.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.RoomId, i.Position });
            e.Property(i => i.Text).HasMaxLength(200);
        });

        builder.Entity<RoomStartCheck>(e =>
        {
            e.HasKey(c => new { c.RoomId, c.Date, c.ItemId });
            e.HasOne<Room>().WithMany().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RoomStartItem>().WithMany().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(c => c.UserId).HasMaxLength(450);
        });

        builder.Entity<RoomApproval>(e =>
        {
            e.HasKey(a => new { a.RoomId, a.Date, a.Kind });
            e.HasOne<Room>().WithMany().HasForeignKey(a => a.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RoomApprovalKind.DayStart);
            e.Property(a => a.ApprovedByUserId).HasMaxLength(450);
            e.Property(a => a.RecordedByUserId).HasMaxLength(450);
        });

        builder.Entity<HandbookEntry>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Text).HasMaxLength(4000);
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        });
    }
}

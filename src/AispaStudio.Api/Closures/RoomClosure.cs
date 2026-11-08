using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Raumsperre (ADR 0076): ein Raum ist für Tage oder einen Zeitraum gesperrt (Wartung, Schaden, Umbau). Wirkt wie die Abwesenheit einer Person: in der Zeit bucht der Planer
/// den Raum nicht (Kalender, Online, Assistenten, Warteliste, Serien); bestehende Termine im Raum bleiben, der Kalender meldet sie, und die Umbuchungshilfe schlägt einen anderen
/// Raum vor. Kalendertage in Ortszeit des Standorts des Raums; Uhrzeiten nur für halbe Tage.
/// </summary>
public sealed class RoomClosure
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RoomId { get; init; }

    /// <summary>Erster gesperrter Tag.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>Uhrzeit, ab der am ersten Tag gesperrt ist; leer: ab 0:00 Uhr.</summary>
    public TimeOnly? FromTime { get; set; }

    /// <summary>Letzter gesperrter Tag (einschließlich).</summary>
    public DateOnly UntilDate { get; set; }

    /// <summary>Uhrzeit, bis zu der am letzten Tag gesperrt ist; leer: bis zum Ende des Tages.</summary>
    public TimeOnly? UntilTime { get; set; }

    public RoomClosureKind Kind { get; set; }

    /// <summary>Grund für das Team, z. B. „Wasserschaden, Trocknung“.</summary>
    public string? Reason { get; set; }

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

/// <summary>Eine Raumsperre als Zeitraum in UTC (<c>Until</c> ausschließlich) für den Planer, mit Art und Grund für die Ablehnung.</summary>
internal sealed record ClosedRoomSpan(Guid RoomId, DateTimeOffset From, DateTimeOffset Until, RoomClosureKind Kind, string? Reason);

internal static class RoomClosureModel
{
    public const int ReasonLength = 200;

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<RoomClosure>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Reason).HasMaxLength(ReasonLength);
            e.Property(c => c.CreatedByUserId).HasMaxLength(450);
            e.Property(c => c.CreatedByName).HasMaxLength(200);
            e.HasIndex(c => new { c.RoomId, c.FromDate, c.UntilDate });
            e.HasOne<Room>().WithMany().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

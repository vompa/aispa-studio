using AispaStudio.Contracts;

namespace AispaStudio.Api.Promotions;

/// <summary>
/// Aktion (ADR 0071): zeitlich begrenzter Rabatt auf Behandlungen oder Pakete, an allen oder ausgewählten Standorten. Sie ist nur ein
/// Angebot: die Kassa schlägt sie vor und setzt auf Klick den Rabatt der Position (<c>PosService.ApplyPromotionAsync</c>); der Umsatz läuft wie jeder andere
/// über Beleg und Signatur. Abgelaufene Aktionen bleiben als Verlauf erhalten und werden nur archiviert.
/// </summary>
public sealed class Promotion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    /// <summary>Kurzer Text für Gäste und den Assistenten.</summary>
    public required string GuestText { get; set; }

    /// <summary>Erster Tag, an dem die Aktion gilt (Kalendertag am Standort).</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Letzter Tag, an dem die Aktion gilt (einschließlich).</summary>
    public DateOnly ValidUntil { get; set; }

    public bool AllLocations { get; set; }

    /// <summary>Prozent vom Preis der Position; genau eines von <see cref="DiscountPercent"/> und <see cref="DiscountAmount"/> ist gesetzt.</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Fester Betrag je Position.</summary>
    public decimal? DiscountAmount { get; set; }

    public PromotionScope Scope { get; set; }

    /// <summary>Aktiv oder pausiert.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gesetzt, wenn archiviert; archivierte Aktionen laufen nie mehr.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<PromotionLocation> Locations { get; init; } = [];
    public List<PromotionTreatment> Treatments { get; init; } = [];
}

/// <summary>Ein Standort, an dem die Aktion gilt (nur wenn sie nicht für alle gilt).</summary>
public sealed class PromotionLocation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid PromotionId { get; init; }
    public Guid LocationId { get; init; }
}

/// <summary>Eine Behandlung, für die die Aktion gilt (bei „ausgewählte Behandlungen“) oder deren Pakete sie senkt (bei „ausgewählte Pakete“).</summary>
public sealed class PromotionTreatment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid PromotionId { get; init; }
    public Guid TreatmentId { get; init; }
}

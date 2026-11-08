using AispaStudio.Contracts;

namespace AispaStudio.Api.BusinessInfo;

/// <summary>
/// Eintrag der Betriebsinformationen (ADR 0074): Versicherung, Handwerker, Buchhaltung, Lohnverrechnung, Vertrauensperson, Notfallnummer … mit Ansprechperson,
/// Telefon, Mail und Zuständigkeit. Ein eigenes Verzeichnis im Kern, kein Adressbuch für Gäste (die stehen in der Kundenkartei). Einträge der Stufe
/// <see cref="BusinessContactVisibility.Management"/> liest nur die Leitung. Archivierte bleiben erhalten und werden nicht mehr genannt. Keine Passwörter.
/// </summary>
public sealed class BusinessContact
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public BusinessContactCategory Category { get; set; }

    /// <summary>Bezeichnung, z. B. „Installateur“.</summary>
    public required string Designation { get; set; }

    public string? Company { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Wofür der Kontakt zuständig ist (Freitext, z. B. „Wasserschaden, Heizung“); der Assistent sucht auch darin.</summary>
    public string? Responsibility { get; set; }

    /// <summary>Vertrags- oder Kundennummer beim Kontakt.</summary>
    public string? CustomerNumber { get; set; }

    /// <summary>Nur bei Versicherungen: Versicherer, Polizzennummer, Deckung (Freitext) und Schadenshotline.</summary>
    public string? Insurer { get; set; }

    public string? PolicyNumber { get; set; }
    public string? Coverage { get; set; }
    public string? ClaimsHotline { get; set; }

    public bool AllLocations { get; set; }
    public string? Notes { get; set; }
    public BusinessContactVisibility Visibility { get; set; }

    /// <summary>Gesetzt, wenn archiviert.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<BusinessContactLocation> Locations { get; init; } = [];
}

/// <summary>Ein Standort, an dem der Eintrag gilt (nur wenn er nicht für alle gilt).</summary>
public sealed class BusinessContactLocation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid BusinessContactId { get; init; }
    public Guid LocationId { get; init; }
}

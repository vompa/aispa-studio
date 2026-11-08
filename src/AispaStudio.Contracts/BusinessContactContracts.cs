namespace AispaStudio.Contracts;

/// <summary>
/// Wozu ein Eintrag der Betriebsinformationen gehört (ADR 0074). Die Kategorie ordnet die Liste und lässt den Assistenten eingrenzen
/// („Nummer der Versicherung“); nur bei <c>Insurance</c> gibt es Versicherer, Polizzennummer, Deckung und Schadenshotline.
/// </summary>
public enum BusinessContactCategory
{
    /// <summary>Versicherung (Haftpflicht, Inventar, Betriebsunterbrechung …).</summary>
    Insurance,

    /// <summary>Handwerker und Technik (Installateur, Elektriker, Haustechnik …).</summary>
    Craftsman,

    /// <summary>Lieferant (Wäsche, Produkte, Verbrauchsmaterial).</summary>
    Supplier,

    /// <summary>Behörde (Finanzamt, Gewerbebehörde, Arbeitsinspektorat).</summary>
    Authority,

    /// <summary>Buchhaltung.</summary>
    Accounting,

    /// <summary>Lohnverrechnung.</summary>
    Payroll,

    /// <summary>Steuerberatung.</summary>
    TaxAdvisor,

    /// <summary>Vertrauensperson außerhalb des Betriebs mit einer Zuständigkeit.</summary>
    TrustedPerson,

    /// <summary>Notfall (Notdienste, Störungsdienste).</summary>
    Emergency,

    /// <summary>Alles andere.</summary>
    Other,
}

/// <summary>
/// Wer einen Eintrag lesen darf: <c>Team</c> alle Zugänge, <c>Management</c> nur Inhaber, Manager, Standortleitung und Backoffice. Der Server filtert
/// Einträge der Stufe <c>Management</c> für alle anderen aus der Antwort und aus den Antworten des Assistenten heraus.
/// </summary>
public enum BusinessContactVisibility
{
    Team,
    Management,
}

/// <summary>
/// Eintrag anlegen oder ändern. <c>Designation</c> ist Pflicht („Installateur“); dazu mindestens eines von Firma, Ansprechperson, Telefon, Mobil und E-Mail.
/// <c>Insurer</c>, <c>PolicyNumber</c>, <c>Coverage</c> und <c>ClaimsHotline</c> gibt es nur bei der Kategorie <c>Insurance</c>. <c>LocationIds</c> bleibt bei
/// <c>AllLocations</c> leer. Keine Passwörter oder Zugangsdaten eintragen.
/// </summary>
/// <param name="Responsibility">Wofür der Kontakt zuständig ist, Freitext („Wasserschaden, Heizung“); der Assistent sucht darin.</param>
/// <param name="CustomerNumber">Vertrags- oder Kundennummer beim Kontakt.</param>
public sealed record BusinessContactInput(
    BusinessContactCategory Category,
    string Designation,
    string? Company,
    string? ContactPerson,
    string? Phone,
    string? Mobile,
    string? Email,
    string? Address,
    string? Responsibility,
    string? CustomerNumber,
    string? Insurer,
    string? PolicyNumber,
    string? Coverage,
    string? ClaimsHotline,
    bool AllLocations,
    IReadOnlyList<Guid>? LocationIds,
    string? Notes,
    BusinessContactVisibility Visibility);

/// <param name="IsArchived">Archiviert: bleibt erhalten, steht aber nicht mehr in der Liste und wird vom Assistenten nicht genannt.</param>
/// <param name="CanEdit">Die anfragende Person darf diesen Eintrag ändern und archivieren (Recht „Standort verwalten“; die Standortleitung nur bei Einträgen ihrer Standorte).</param>
public sealed record BusinessContactDto(
    Guid Id,
    BusinessContactCategory Category,
    string Designation,
    string? Company,
    string? ContactPerson,
    string? Phone,
    string? Mobile,
    string? Email,
    string? Address,
    string? Responsibility,
    string? CustomerNumber,
    string? Insurer,
    string? PolicyNumber,
    string? Coverage,
    string? ClaimsHotline,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    string? Notes,
    BusinessContactVisibility Visibility,
    bool IsArchived,
    bool CanEdit);

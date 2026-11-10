namespace AispaStudio.Contracts;

/// <summary>Ein Feld des Gastes, dem eine Spalte der Importdatei zugeordnet werden kann (Export/Import, Aufgabe E). Gesundheitsdaten, Wünsche und Vermerke gibt es hier bewusst nicht.</summary>
public enum CustomerImportField
{
    FirstName,
    LastName,
    Email,
    Phone,
    BirthDate,
    Salutation,
    Street,
    PostalCode,
    City,
    Country,

    /// <summary>Werbe-Einwilligung: nur „ja“ (ja, yes, x, 1, wahr …) wird zu einer bestätigten Einwilligung mit der Quelle „Import“.</summary>
    MarketingConsent,
}

/// <summary>Wie Daten in der Datei geschrieben sind; <see cref="Auto"/> erkennt TT.MM.JJJJ, JJJJ-MM-TT und TT/MM/JJJJ.</summary>
public enum ImportDateFormat
{
    Auto,
    DayMonthYear,
    YearMonthDay,
    MonthDayYear,
}

public enum ImportRowStatus
{
    New,
    Duplicate,
    Invalid,
}

/// <summary>Was mit einer Dublette geschieht: überspringen oder ergänzen (leere Felder füllen, nie überschreiben).</summary>
public enum ImportDuplicateAction
{
    Skip,
    Merge,
}

/// <summary>Spalte <paramref name="Column"/> (ab 0, in der Reihenfolge der Datei) gehört zu <paramref name="Field"/>.</summary>
public sealed record ImportColumnMapping(int Column, CustomerImportField Field);

/// <summary>Zuordnung der Spalten; ohne Zuordnung schlägt die Vorschau eine nach den Spaltenköpfen vor.</summary>
public sealed record CustomerImportMapping(List<ImportColumnMapping> Columns, ImportDateFormat DateFormat = ImportDateFormat.Auto);

/// <summary>Entscheidung für die Dublette in Zeile <paramref name="Row"/> (Zeilennummer der Datei, Kopfzeile ist 1).</summary>
public sealed record ImportDuplicateDecision(int Row, ImportDuplicateAction Action);

/// <summary>Eine Spalte der Datei mit bis zu drei Beispielwerten und dem vorgeschlagenen Feld.</summary>
public sealed record ImportColumn(int Index, string Header, List<string> Samples, CustomerImportField? Suggested);

/// <summary>
/// Eine Zeile der Vorschau. Bei einer Dublette nennt <paramref name="Reason"/> den Treffer („gleicher Nachname und gleiche Telefonnummer wie Anna Huber“, „doppelt in der Datei, Zeile 4“),
/// bei einer fehlerhaften Zeile den Grund („Telefon unleserlich“).
/// </summary>
public sealed record ImportRowPreview(int Row, ImportRowStatus Status, string Name, string? Email, string? Phone, string? Reason, Guid? ExistingCustomerId);

/// <summary>
/// Vorschau eines Imports: Spalten mit Vorschlägen, die verwendete Zuordnung, Zähler und die ersten 20 Zeilen; dazu alle Dubletten (zum Entscheiden) und alle fehlerhaften Zeilen.
/// </summary>
public sealed record CustomerImportPreview(
    string FileName,
    int RowCount,
    List<ImportColumn> Columns,
    CustomerImportMapping Mapping,
    int New,
    int Duplicates,
    int Invalid,
    List<ImportRowPreview> Rows,
    List<ImportRowPreview> DuplicateRows,
    List<ImportRowPreview> InvalidRows);

/// <summary>Ergebnis eines Imports oder Probelaufs (<paramref name="DryRun"/>: nichts gespeichert).</summary>
public sealed record CustomerImportResult(bool DryRun, int Rows, int Created, int Merged, int Skipped, int Failed);

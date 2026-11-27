namespace AispaStudio.Contracts;

/// <summary>Dateiformat eines Exports; der Name ist zugleich der Wert von <c>?format=</c> an den Export-Endpunkten.</summary>
public enum ExportFormat
{
    /// <summary>Excel-Arbeitsmappe (.xlsx), eine Tabelle je Datei.</summary>
    Xlsx,

    /// <summary>CSV mit Strichpunkt, UTF-8 mit BOM, Datum TT.MM.JJJJ, Beträge mit Komma.</summary>
    Csv,

    /// <summary>Kundenliste im Format der Buchungsportale (englische Spaltenköpfe, Komma).</summary>
    Portal,

    /// <summary>Kontakte als vCard 4.0 (.vcf) für Outlook, Apple und Google.</summary>
    Vcf,

    /// <summary>Termine als iCalendar (.ics).</summary>
    Ics,

    /// <summary>Dokument als PDF (Management-Bericht); steht im Export-Protokoll, die Listen gibt es nicht als PDF.</summary>
    Pdf,

    /// <summary>Dokument als Word-Datei (.docx, Management-Bericht); steht im Export-Protokoll.</summary>
    Docx,
}

/// <summary>
/// Welche Listen des Lager-Exports an einem Standort etwas enthalten (<c>GET /api/stock/locations/{id}/export/availability</c>): je Liste <c>List</c> (<c>levels</c>, <c>count</c>,
/// <c>movements</c>, <c>orders</c>, <c>expiry</c>), ob sie sich exportieren lässt und, wenn nicht, der Grund in einem Satz für die Maske.
/// </summary>
public sealed record StockExportAvailabilityDto(IReadOnlyList<StockExportListAvailabilityDto> Lists);

/// <summary>Eine Liste des Lager-Exports: <paramref name="Reason"/> nennt bei <paramref name="Available"/> = false, warum es sie noch nicht gibt.</summary>
public sealed record StockExportListAvailabilityDto(string List, bool Available, string? Reason = null);

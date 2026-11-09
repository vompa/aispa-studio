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
}

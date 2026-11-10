using System.Globalization;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Gemeinsames der Export-Menüs von Kassa, Lager, Personal und Berichten (Export/Import, Aufgabe D): je Liste ein Eintrag „… (Excel)“ und „… (CSV)“,
/// Datum im Pfad als <c>yyyy-MM-dd</c> unabhängig von der Sprache der App.
/// </summary>
internal static class ListExportOptions
{
    /// <summary>Die beiden Einträge einer Liste; <paramref name="path"/> bekommt „xlsx“ bzw. „csv“ und wird beim Klick ausgewertet.</summary>
    public static IEnumerable<ExportOption> Pair(string label, Func<string, string> path) =>
    [
        ExportOption.For(AispaStudio.Contracts.ExportFormat.Xlsx, () => path("xlsx"), $"{label} (Excel)"),
        ExportOption.For(AispaStudio.Contracts.ExportFormat.Csv, () => path("csv"), $"{label} (CSV)"),
    ];

    public static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

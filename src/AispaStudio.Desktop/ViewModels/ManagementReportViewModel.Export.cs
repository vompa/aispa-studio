using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Kennzahlen des Zeitraums (Export/Import, Aufgabe D) ----------
public sealed partial class ManagementReportViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ neben Von und Bis: die Kennzahlen des Zeitraums als Excel oder CSV. Wer den Reiter sieht, darf exportieren; ob Beträge dabei sind, entscheidet die API nach dem Recht.
    /// Fehlt ein Datum (Feld geleert), geht es leer mit und die API antwortet mit dem Hinweis zum Zeitraum.
    /// </summary>
    public ExportViewModel Export => _export ??= new ExportViewModel(Api, ListExportOptions.Pair("Kennzahlen", ExportPath).ToList())
    {
        Saved = text => ExportSaved = text,
        Failed = text =>
        {
            ExportSaved = null;
            Error = text;
        },
    };

    /// <summary>„Gespeichert: Kennzahlen_2026-10-01_bis_2026-10-31.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

    private string ExportPath(string format) =>
        $"api/reports/locations/{Location?.Id}/management/export?from={(From is { } from ? ListExportOptions.Day(from) : "")}&until={(Until is { } until ? ListExportOptions.Day(until) : "")}&format={format}";
}

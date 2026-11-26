using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Kennzahlen des Zeitraums (Export/Import, Aufgabe D) ----------
public sealed partial class ManagementReportViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ neben Von und Bis: der ganze Bericht als Excel-Arbeitsmappe und als Word-Datei (mit dem KI-Kommentar, wenn der Reiter ihn zeigt)
    /// und die Kennzahlen des Zeitraums als CSV. Das PDF steht im Knopfblock daneben (<see cref="Document"/>). Wer den Reiter sieht, darf exportieren;
    /// ob Beträge und Gäste dabei sind, entscheidet die API nach dem Recht. Fehlt ein Datum (Feld geleert), geht es leer mit und die API antwortet mit dem Hinweis zum Zeitraum.
    /// </summary>
    public ExportViewModel Export => _export ??= new ExportViewModel(Api,
    [
        ExportOption.For(ExportFormat.Xlsx, () => ExportPath("xlsx") + "&content=report" + AnalysisQuery(Analysis.AdoptedId), "Bericht (Excel)"),
        ExportOption.For(ExportFormat.Docx, () => ExportPath("docx") + (HasAiSummary ? "&ai=true" : "") + AnalysisQuery(Analysis.AdoptedId), "Bericht (Word)"),
        ExportOption.For(ExportFormat.Csv, () => ExportPath("csv"), "Kennzahlen (CSV)"),
    ])
    {
        Saved = text =>
        {
            ExportSaved = text;
            _savedPath = _export?.SavedPath;
            OnPropertyChanged(nameof(HasSavedPath));
        },
        Failed = text =>
        {
            ExportSaved = null;
            _savedPath = null;
            OnPropertyChanged(nameof(HasSavedPath));
            Error = text;
        },
    };

    /// <summary>„Gespeichert: Kennzahlen_2026-10-01_bis_2026-10-31.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

    private string? _savedPath;

    /// <summary>„Ordner öffnen“ gibt es, wenn der Speicherdialog den Ort der Datei genannt hat.</summary>
    public bool HasSavedPath => HasExportSaved && _savedPath is not null;

    public const string OpenFolderText = "Ordner öffnen";

    [RelayCommand]
    private void OpenFolder()
    {
        if (_savedPath is { } path)
        {
            FolderOpener.Reveal(path);
        }
    }

    private string ExportPath(string format) =>
        $"api/reports/locations/{Location?.Id}/management/export?from={(From is { } from ? ListExportOptions.Day(from) : "")}&until={(Until is { } until ? ListExportOptions.Day(until) : "")}&format={format}{(Comparison.IsDefault ? "" : $"&compare={Comparison.Key}")}";
}

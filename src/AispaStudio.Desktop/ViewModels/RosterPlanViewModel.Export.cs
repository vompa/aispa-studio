using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export des Dienstplans und der Abwesenheiten der Woche für die Leitung (Export/Import, Aufgabe D) ----------
public sealed partial class RosterPlanViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ in der Kopfzeile: Dienstplan und Abwesenheiten der angezeigten Woche am gewählten Standort. Nur für Inhaberin und Geschäftsführung, für alle anderen gibt es keinen Knopf.
    /// Standort und Woche werden beim Klick gelesen.
    /// </summary>
    public ExportViewModel? Export => _context.IsOwnerLevel
        ? _export ??= new ExportViewModel(_context.Api, ExportOptions())
        {
            Saved = text => ExportSaved = text,
            Failed = text =>
            {
                ExportSaved = null;
                Error = text;
            },
        }
        : null;

    /// <summary>„Gespeichert: Dienstplan_2027-03-01.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

    private List<ExportOption> ExportOptions() =>
    [
        .. ListExportOptions.Pair("Dienstplan der Woche", format => $"{StaffExportPath}?list=roster&from={ListExportOptions.Day(WeekStart)}&format={format}"),
        .. ListExportOptions.Pair("Abwesenheiten",
            format => $"{StaffExportPath}?list=absences&from={ListExportOptions.Day(WeekStart)}&until={ListExportOptions.Day(WeekStart.AddDays(6))}&format={format}"),
    ];

    private string StaffExportPath => $"api/locations/{SelectedLocation?.Id}/staff/export";
}

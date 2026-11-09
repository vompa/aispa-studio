using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Stundenliste und der Abwesenheiten für die Leitung (Export/Import, Aufgabe D) ----------
public sealed partial class TimesheetViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ in der Kopfzeile: Stundenliste und Abwesenheiten des Monats, in dem die angezeigte Woche beginnt, für den gewählten Standort; auf der Personenkarte nur diese Person.
    /// Nur für Inhaberin und Geschäftsführung, für alle anderen gibt es keinen Knopf. Standort und Woche werden beim Klick gelesen.
    /// </summary>
    public ExportViewModel? Export => _context.IsOwnerLevel
        ? _export ??= new ExportViewModel(_context.Api, ExportOptions())
        {
            Saved = text => ExportSaved = text,
            Failed = text =>
            {
                ExportSaved = null;
                LoadError = text;
            },
        }
        : null;

    /// <summary>„Gespeichert: Stunden_2027-03.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

    private List<ExportOption> ExportOptions() =>
    [
        .. ListExportOptions.Pair("Stundenliste", HoursPath),
        .. ListExportOptions.Pair("Abwesenheiten", AbsencesPath),
    ];

    private string HoursPath(string format) =>
        $"{StaffExportPath}?list=hours&month={ListExportOptions.Month(WeekStart)}{(_employeeId is { } one ? $"&employeeId={one}" : "")}&format={format}";

    private string AbsencesPath(string format)
    {
        var first = new DateOnly(WeekStart.Year, WeekStart.Month, 1);
        return $"{StaffExportPath}?list=absences&from={ListExportOptions.Day(first)}&until={ListExportOptions.Day(first.AddMonths(1).AddDays(-1))}&format={format}";
    }

    private string StaffExportPath => $"api/locations/{SelectedLocation?.Id}/staff/export";
}

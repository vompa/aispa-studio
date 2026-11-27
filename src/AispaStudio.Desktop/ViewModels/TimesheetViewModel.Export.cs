namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Stundenliste und der Abwesenheiten für die Leitung (Export/Import, Aufgabe D) ----------
public sealed partial class TimesheetViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Die Listen im Menü „Ausgabe“ der Kopfzeile: Stundenliste und Abwesenheiten des Monats, in dem die angezeigte Woche beginnt, für den gewählten Standort; auf der Personenkarte nur diese Person.
    /// Nur für Inhaberin und Geschäftsführung, für alle anderen fehlt die Gruppe. Standort und Woche werden beim Klick gelesen; die Bestätigung ist der Toast der Shell.
    /// </summary>
    public ExportViewModel? Export => _context.IsOwnerLevel
        ? _export ??= new ExportViewModel(_context.Api, ExportOptions())
        {
            Saved = text => _context.Toast.Show(text),
            Failed = text => LoadError = text,
            Files = Files,
        }
        : null;

    public bool HasExport => Export is not null;

    /// <summary>Zwischen den Gruppen „Stundenliste“ und „Liste“ steht ein Trenner, solange beide da sind.</summary>
    public bool ShowsOutputSeparator => ShowsDocumentGroup && HasExport;

    /// <summary>Der Speicherdialog des Fensters; die Ansicht setzt ihn beim Einhängen, er gilt für Listen und Stundenliste (auch wenn das Modell der Stundenliste neu gebaut wird).</summary>
    public IFileSaver? Files
    {
        get;
        set
        {
            field = value;
            if (_export is not null)
            {
                _export.Files = value;
            }

            if (Document is not null)
            {
                Document.Files = value;
            }
        }
    }

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

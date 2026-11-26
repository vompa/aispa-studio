namespace AispaStudio.Desktop.ViewModels;

// ---------- Export des Dienstplans und der Abwesenheiten der Woche für die Leitung (Export/Import, Aufgabe D) ----------
public sealed partial class RosterPlanViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Die Listen im Menü „Ausgabe“ der Kopfzeile: Dienstplan und Abwesenheiten der angezeigten Woche am gewählten Standort. Nur für Inhaberin und Geschäftsführung, für alle anderen fehlt die Gruppe.
    /// Standort und Woche werden beim Klick gelesen; die Bestätigung ist der Toast der Shell.
    /// </summary>
    public ExportViewModel? Export => _context.IsOwnerLevel
        ? _export ??= new ExportViewModel(_context.Api, ExportOptions())
        {
            Saved = text => _context.Toast.Show(text),
            Failed = text => Error = text,
            Files = Files,
        }
        : null;

    public bool HasExport => Export is not null;

    /// <summary>Zwischen den Gruppen „Aushang“ und „Liste“ steht ein Trenner, solange beide da sind.</summary>
    public bool ShowsOutputSeparator => ShowsDocumentGroup && HasExport;

    /// <summary>Der Speicherdialog des Fensters; die Ansicht setzt ihn beim Einhängen, er gilt für Listen und Aushang (auch wenn das Modell des Aushangs neu gebaut wird).</summary>
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
        .. ListExportOptions.Pair("Dienstplan der Woche", format => $"{StaffExportPath}?list=roster&from={ListExportOptions.Day(WeekStart)}&format={format}"),
        .. ListExportOptions.Pair("Abwesenheiten",
            format => $"{StaffExportPath}?list=absences&from={ListExportOptions.Day(WeekStart)}&until={ListExportOptions.Day(WeekStart.AddDays(6))}&format={format}"),
    ];

    private string StaffExportPath => $"api/locations/{SelectedLocation?.Id}/staff/export";
}

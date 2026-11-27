namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Kassa-Listen für die Leitung (Export/Import, Aufgabe D; Menü „Ausgabe“ T-125) ----------
public partial class PosViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Die Listen im Menü „Ausgabe“ der Kopfzeile: Belege, Umsatz, Tagesabschlüsse und Gutscheinbestand der gewählten Kasse. Nur für Inhaberin und Geschäftsführung (<see cref="OwnerLevel"/>) und mit gewählter Kasse;
    /// sonst fehlt die Gruppe. Der Zeitraum ist der Belegtag des Reiters „Belege“, der Pfad wird beim Klick gebildet; die Bestätigung ist der Toast der Shell.
    /// </summary>
    public ExportViewModel? Export => OwnerLevel && Register is not null
        ? _export ??= new ExportViewModel(_api, ExportOptions())
        {
            Saved = ShowToast,
            Failed = text => Error = text,
            Files = Files,
        }
        : null;

    public bool HasExport => Export is not null;

    /// <summary>Die Gruppe „Gutscheinliste“ im Menü: wer sie drucken darf (Kassenaufsicht oder „Berichte mit Beträgen“); der Server verlangt dasselbe.</summary>
    public bool ShowsVoucherListGroup => MayPrintVoucherList;

    /// <summary>Der Knopf „Ausgabe“ steht, wenn mindestens eine Gruppe da ist; sonst fehlt er.</summary>
    public bool HasOutput => ShowsVoucherListGroup || HasExport;

    /// <summary>Zwischen den Gruppen „Gutscheinliste“ und „Listen“ steht ein Trenner, solange beide da sind.</summary>
    public bool ShowsOutputSeparator => ShowsVoucherListGroup && HasExport;

    /// <summary>Der Speicherdialog des Fensters; die Ansicht setzt ihn beim Einhängen und er gilt für die Listen (auch wenn sie erst danach entstehen).</summary>
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
        }
    }

    private void NotifyOutputChanged()
    {
        OnPropertyChanged(nameof(ShowsVoucherListGroup));
        OnPropertyChanged(nameof(HasExport));
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(ShowsOutputSeparator));
    }

    private List<ExportOption> ExportOptions() =>
    [
        .. ListExportOptions.Pair("Belegliste", format => ExportPath("receipts", format, withDay: true)),
        .. ListExportOptions.Pair("Umsatz je Tag", format => ExportPath("revenue-days", format, withDay: true)),
        .. ListExportOptions.Pair("Umsatz je Zahlart", format => ExportPath("revenue-payments", format, withDay: true)),
        .. ListExportOptions.Pair("Umsatz je Behandlung und Person", format => ExportPath("revenue-treatments", format, withDay: true)),
        .. ListExportOptions.Pair("Tagesabschlüsse", format => ExportPath("day-closings", format, withDay: true)),
        .. ListExportOptions.Pair("Gutscheinbestand", format => ExportPath("vouchers", format, withDay: false)),
    ];

    private string ExportPath(string list, string format, bool withDay)
    {
        var range = withDay ? $"&from={ListExportOptions.Day(ReceiptDay)}&until={ListExportOptions.Day(ReceiptDay)}" : "";
        return $"api/pos/locations/{Register?.LocationId}/export?list={list}{range}&format={format}";
    }
}

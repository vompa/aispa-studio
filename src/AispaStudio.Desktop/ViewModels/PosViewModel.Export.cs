using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Kassa-Listen für die Leitung (Export/Import, Aufgabe D) ----------
public partial class PosViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ in der Kopfzeile: Belege, Umsatz, Tagesabschlüsse und Gutscheinbestand der gewählten Kasse. Nur für Inhaberin und Geschäftsführung (<see cref="OwnerLevel"/>) und mit gewählter Kasse;
    /// sonst gibt es keinen Knopf. Der Zeitraum ist der Belegtag des Reiters „Belege“, der Pfad wird beim Klick gebildet.
    /// </summary>
    public ExportViewModel? Export => OwnerLevel && Register is not null
        ? _export ??= new ExportViewModel(_api, ExportOptions())
        {
            Saved = text => ExportSaved = text,
            Failed = text =>
            {
                ExportSaved = null;
                Error = text;
            },
        }
        : null;

    /// <summary>„Gespeichert: Belege_2026-10-19.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

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

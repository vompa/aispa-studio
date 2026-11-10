using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Lager-Listen für die Leitung (Export/Import, Aufgabe D) ----------
public sealed partial class StockViewModel
{
    private ExportViewModel? _export;

    /// <summary>
    /// Der Knopf „Exportieren“ in der Kopfzeile: Bestand je Lagerort, Inventur, Lagerbewegungen, Bestellungen und Ablaufliste des gewählten Standorts. Nur für Inhaberin und Geschäftsführung,
    /// für alle anderen gibt es keinen Knopf. Der Standort wird beim Klick gelesen, die Bewegungen nehmen die letzten 30 Tage bis heute.
    /// </summary>
    public ExportViewModel? Export => _isLeadership
        ? _export ??= new ExportViewModel(Api, ExportOptions())
        {
            Saved = text => ExportSaved = text,
            Failed = text =>
            {
                ExportSaved = null;
                Error = text;
            },
        }
        : null;

    /// <summary>„Gespeichert: Bestand_2027-03-01.xlsx“, klein neben dem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportSaved))]
    public partial string? ExportSaved { get; private set; }

    public bool HasExportSaved => !string.IsNullOrEmpty(ExportSaved);

    private List<ExportOption> ExportOptions() =>
    [
        .. ListExportOptions.Pair("Bestand je Lagerort", format => ExportPath("levels", format, withRange: false)),
        .. ListExportOptions.Pair("Inventur", format => ExportPath("count", format, withRange: false)),
        .. ListExportOptions.Pair("Lagerbewegungen", format => ExportPath("movements", format, withRange: true)),
        .. ListExportOptions.Pair("Bestellungen", format => ExportPath("orders", format, withRange: false)),
        .. ListExportOptions.Pair("Ablaufliste", format => ExportPath("expiry", format, withRange: false)),
    ];

    private string ExportPath(string list, string format, bool withRange)
    {
        var range = "";
        if (withRange)
        {
            var today = DateOnly.FromDateTime(Time.GetLocalNow().DateTime);
            range = $"&from={ListExportOptions.Day(today.AddDays(-30))}&until={ListExportOptions.Day(today)}";
        }

        return $"api/stock/locations/{SelectedLocation?.Id}/export?list={list}{range}&format={format}";
    }
}

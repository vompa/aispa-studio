using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Export der Lager-Listen für die Leitung (Export/Import, Aufgabe D; Dialog seit P7) ----------
public sealed partial class StockViewModel : ISavesFiles
{
    public const string ExportErrorTitle = "Export nicht erstellt";
    public const string AvailabilityMissing = "Der Export ist gerade nicht möglich. Versuchen Sie es noch einmal.";
    public const string OpenFolderText = "Ordner öffnen";

    private string? _savedPath;

    /// <summary>Der Speicherdialog des Fensters; die Ansicht setzt ihn beim Einhängen.</summary>
    public IFileSaver? Files { get; set; }

    /// <summary>Öffnet den Ordner einer gespeicherten Datei („Ordner öffnen“ im Toast); Tests setzen einen eigenen.</summary>
    public IFolderOpener FolderOpener { get; set; } = new ExplorerFolderOpener();

    /// <summary>Der Knopf „Exportieren“ in der Kopfzeile: nur für Inhaberin und Geschäftsführung, für alle anderen gibt es ihn nicht.</summary>
    public bool CanExport => _isLeadership;

    /// <summary>Fehler des Exports (nicht der Buchungen): bleibt stehen, bis er geschlossen wird oder ein neuer Versuch beginnt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportError))]
    public partial string? ExportError { get; private set; }

    public bool HasExportError => ExportError is not null;

    [RelayCommand]
    private void CloseExportError() => ExportError = null;

    /// <summary>Die Liste, die zum offenen Bereich passt: Inventur → Inventur, Bestellen → Bestellungen, sonst Bestand je Lagerort.</summary>
    private string PreselectedExport => IsCountTab ? "count" : IsOrdersTab ? "orders" : "levels";

    /// <summary>
    /// Öffnet den Dialog: fragt die API, welche Listen es an diesem Standort gibt, lässt Inhalt, Format und Zeitraum wählen und holt die Datei. Das Format bleibt für das nächste Mal gemerkt.
    /// Ein Abbruch des Speicherdialogs meldet nichts; „Gespeichert: …“ erscheint nur, wenn die Datei wirklich geschrieben wurde.
    /// </summary>
    [RelayCommand]
    private async Task OpenExportAsync()
    {
        if (!_isLeadership || SelectedLocation is not { } location || Dialogs is not { } dialogs)
        {
            return;
        }

        ExportError = null;
        var availability = await Api.GetAsync<StockExportAvailabilityDto>($"api/stock/locations/{location.Id}/export/availability").ConfigureAwait(true);
        if (availability.Value is not { } lists)
        {
            ExportError = availability.Error ?? AvailabilityMissing;
            return;
        }

        var today = DateOnly.FromDateTime(Time.GetLocalNow().DateTime);
        var excel = _settings.Load().StockExportFormat != "csv";
        StockExportDialogViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done => dialog = new StockExportDialogViewModel(location.Name, lists.Lists, PreselectedExport, excel, today.AddDays(-30), today, done))
            .ConfigureAwait(true);
        if (!answer.IsConfirmed || dialog is not { Selected: { } list })
        {
            return;
        }

        _settings.Save(_settings.Load() with { StockExportFormat = dialog.Format });
        var range = list == StockExportDialogViewModel.Movements ? $"&from={ListExportOptions.Day(dialog.From!.Value)}&until={ListExportOptions.Day(dialog.Until!.Value)}" : "";
        var path = string.Create(CultureInfo.InvariantCulture, $"api/stock/locations/{location.Id}/export?list={list}{range}&format={dialog.Format}");
        var option = ExportOption.For(dialog.IsExcel ? ExportFormat.Xlsx : ExportFormat.Csv, () => path);
        ExportViewModel? export = null;
        export = new ExportViewModel(Api, [option])
        {
            Files = Files,
            Saved = text => ShowToast(text, export?.SavedPath),
            Failed = text => ExportError = text,
        };
        await export.ExportCommand.ExecuteAsync(option).ConfigureAwait(true);
    }

    // ---------- Meldung unten („Gespeichert: Lagerbestand_2027-03-01.xlsx“ mit „Ordner öffnen“): der Toast der Shell, mit Aktion 8 Sekunden (meldungen.md M1) ----------

    private void ShowToast(string text, string? savedPath)
    {
        _savedPath = savedPath;
        Toast.Show(text, OpenFolderText, savedPath is null ? null : OpenSavedFolderCommand);
    }

    [RelayCommand]
    private void OpenSavedFolder()
    {
        if (_savedPath is { } path)
        {
            FolderOpener.Reveal(path);
        }

        Toast.Close();
    }
}

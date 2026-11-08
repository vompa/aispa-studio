using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Handscanner im Lager (R05 Slice 114, E13): Der Scanner tippt den Code wie eine Tastatur und schließt mit Enter ab. Der Code findet das Material über die API; der Reiter bestimmt die Wirkung:
/// in der Inventur springt der Fokus ins Feld „Gezählt“ seiner Zeile, sonst wählt der Bestand die Zeile des Materials und, mit dem Recht zu buchen, öffnet sich die Leiste „Bewegung buchen“ (Wareneingang). Ein unbekannter Code fragt, ob ein Material
/// angelegt oder einem vorhandenen zugeordnet wird.
/// </summary>
public sealed partial class StockViewModel
{
    private string? _barcodeToAssign;

    /// <summary>
    /// Das Feld „Material suchen oder scannen“: Tippen filtert den Bestand, der Scanner tippt den Code hinein, und Enter löst die Suche über die API aus.
    /// </summary>
    [ObservableProperty]
    public partial string ScanText { get; set; } = "";

    /// <summary>Gescannt wird, wo gebucht oder gezählt wird.</summary>
    public bool CanScan => CanBook || CanManage;

    partial void OnScanTextChanged(string value)
    {
        Overview.Search = value;
        if (value.Length > 0 && Tab == StockTab.Overview)
        {
            // Die Übersicht zeigt keine Liste: wer dort tippt, will den Bestand sehen.
            SetTab(StockTab.Stock);
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!CanScan)
        {
            return;
        }

        var code = ScanText.Trim();
        var typedName = Overview.Rows.Count > 0;
        var assign = _barcodeToAssign;
        ScanText = "";
        _barcodeToAssign = null;
        if (code.Length == 0)
        {
            return;
        }

        var result = await Api.GetAsync<StockItemDto>($"api/stock/items/by-barcode?code={Uri.EscapeDataString(code)}").ConfigureAwait(true);
        if (result.Value is { } item)
        {
            Error = null;
            ApplyScanned(item);
        }
        else if (result.Status == System.Net.HttpStatusCode.NotFound && typedName)
        {
            // Kein Code, sondern ein Name, den die Liste kennt: Enter lässt die Suche stehen, es fragt nichts und verwirft kein „Zuordnen“.
            _barcodeToAssign = assign;
            ScanText = code;
        }
        else if (result.Status == System.Net.HttpStatusCode.NotFound)
        {
            await AskUnknownCodeAsync(code).ConfigureAwait(true);
        }
        else
        {
            ShowError(result.Error ?? "Der Code wurde nicht gesucht.");
        }
    }

    private void ApplyScanned(StockItemDto item)
    {
        if (Tab == StockTab.Count)
        {
            if (Count.FocusRow(item.Id))
            {
                Message = $"{item.Name} gefunden. Trage den gezählten Bestand ein.";
            }
            else
            {
                ShowError(Count.IsCounting ? $"{item.Name} steht nicht in der Zählliste dieses Lagerorts." : "Starte zuerst die Inventur, dann scanne die Artikel.");
            }

            return;
        }

        if (Tab != StockTab.Stock)
        {
            SetTab(StockTab.Stock);
        }

        Overview.Select(item.Id);
        if (!CanBook)
        {
            Message = $"{item.Name} gefunden.";
            return;
        }

        OpenBooking(new MovementPreset(StockMovementKind.GoodsReceipt, item.Id, PlaceFilterId ?? MainPlace?.Id));
        Booking.FocusTarget = MovementBookingViewModel.AmountField;
        Message = $"{item.Name} gefunden. Trage die gelieferte Menge ein.";
    }

    private async Task AskUnknownCodeAsync(string code)
    {
        if (!CanCatalog)
        {
            ShowError($"Der Code {code} gehört zu keinem Material. Material anlegen darf nur, wer den Katalog pflegt.");
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Material anlegen oder zuordnen?", $"Der Code {code} gehört zu keinem Material.", "Material anlegen",
            IsDestructive: false, AlternativeText: "Einem Material zuordnen")).ConfigureAwait(true);
        if (answer.Choice == ConfirmChoice.Cancelled || !await UnsavedChanges.ConfirmLeaveAsync(Dialogs, ItemForm).ConfigureAwait(true))
        {
            return;
        }

        SetTab(StockTab.Stock);
        if (answer.Choice == ConfirmChoice.Confirmed)
        {
            ItemForm.Clear();
            ItemForm.Barcode = code;
            ItemForm.FocusTarget = StockItemFormViewModel.NameField;
            return;
        }

        _barcodeToAssign = code;
        Error = null;
        Message = $"Wähle das Material, das den Code {code} bekommen soll: dann „Ändern“ im Seitenpanel.";
    }

    /// <summary>Der Code, den das nächste „Material ändern“ übernimmt (nach „Einem Material zuordnen“); danach ist er verbraucht.</summary>
    internal string? TakeBarcodeToAssign()
    {
        var code = _barcodeToAssign;
        _barcodeToAssign = null;
        return code;
    }
}

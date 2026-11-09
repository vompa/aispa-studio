using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Inventurliste und Zählliste als PDF (PDF-Ausgaben F, ADR 0091) ----------
public sealed partial class StockCountViewModel
{
    private bool _documentOpen;

    /// <summary>
    /// Der Knopfblock „Drucken · PDF speichern“; null, solange er zu ist. Vor dem Zählen und während einer eigenen Zählung ist es die Zählliste des Lagerorts mit leerer Spalte „Gezählt“,
    /// bei einer übernommenen Zählung vom Handy die Inventurliste mit Soll, Gezählt, Differenz und Wert. Beides rechnet die API; ohne Senden.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentOpen))]
    public partial DocumentActionsViewModel? Document { get; private set; }

    [ObservableProperty]
    public partial string? DocumentMessage { get; private set; }

    public bool IsDocumentOpen => Document is not null;

    public string DocumentButtonText => IsAdopted ? "Inventurliste drucken …" : "Zählliste drucken …";

    public string DocumentHint => IsAdopted
        ? "Druckt die Inventurliste dieser Zählung: Soll, Gezählt, Differenz, Wert und die Summe der Differenzen."
        : "Druckt alle Materialien des Lagerorts mit dem Soll und einer leeren Spalte „Gezählt“ zum Hinschreiben.";

    [RelayCommand]
    private void ToggleDocument()
    {
        _documentOpen = !_documentOpen;
        DocumentMessage = null;
        RefreshDocument();
    }

    /// <summary>Baut den Knopfblock für den Lagerort oder die übernommene Zählung neu, solange er offen ist; beim Wechsel von Lagerort oder Zählung.</summary>
    private void RefreshDocument()
    {
        OnPropertyChanged(nameof(DocumentButtonText));
        OnPropertyChanged(nameof(DocumentHint));
        Document = _documentOpen && _owner.Location is { } location && SelectedPlace is { } place
            ? new DocumentActionsViewModel(DocumentSourceFor(location.Id, place, _adopted?.Id, _owner.CanManage), _owner.Api, _owner.Dialogs) { ShowToast = text => DocumentMessage = text }
            : null;
    }

    /// <summary>Pfad der Inventurliste der übernommenen Zählung, sonst der Zählliste des Lagerorts; die Inventur druckt nur die Leitung (wie die API).</summary>
    internal static DocumentSource DocumentSourceFor(Guid locationId, StockPlaceOption place, Guid? sessionId, bool isAllowed) => sessionId is { } id
        ? new($"Inventurliste {place.Name}", $"api/locations/{locationId}/stock-counts/{id}/pdf", IsAllowed: isAllowed, DisabledHint: "Die Inventur druckt nur die Standortleitung.")
        : new($"Zählliste {place.Name}", $"api/locations/{locationId}/storage-places/{place.Id}/count-sheet/pdf", IsAllowed: isAllowed, DisabledHint: "Die Inventur druckt nur die Standortleitung.");
}

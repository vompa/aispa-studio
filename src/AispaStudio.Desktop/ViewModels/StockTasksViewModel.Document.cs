using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Ablaufliste als PDF (PDF-Ausgaben F, ADR 0091) ----------
public sealed partial class StockTasksViewModel
{
    private bool _documentOpen;

    /// <summary>Zeiträume der Ablaufliste in Tagen; die API nimmt 1 bis 365.</summary>
    public IReadOnlyList<int> DaysOptions { get; } = [7, 14, 30, 60, 90];

    /// <summary>Wie weit die Liste vorausschaut; die Wahl ersetzt den Pfad des Knopfblocks.</summary>
    [ObservableProperty]
    public partial int SelectedDays { get; set; } = 30;

    /// <summary>Der Knopfblock „Drucken · PDF speichern“ für die Chargen, die in den nächsten Tagen ablaufen; null, solange er zu ist. Die Liste rechnet die API; ohne Senden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentOpen))]
    public partial DocumentActionsViewModel? Document { get; private set; }

    [ObservableProperty]
    public partial string? DocumentMessage { get; private set; }

    public bool IsDocumentOpen => Document is not null;

    [RelayCommand]
    private void ToggleDocument()
    {
        _documentOpen = !_documentOpen;
        DocumentMessage = null;
        UpdateDocument();
    }

    partial void OnSelectedDaysChanged(int value) => UpdateDocument();

    /// <summary>Baut den Knopfblock für den Standort und den gewählten Zeitraum neu, solange er offen ist; auch nach dem Laden, denn dann kann der Standort ein anderer sein.</summary>
    private void UpdateDocument()
    {
        Document = _documentOpen && _owner.Location is { } location
            ? new DocumentActionsViewModel(DocumentSourceFor(location.Id, SelectedDays, _owner.CanManage), _owner.Api, _owner.Dialogs) { ShowToast = text => DocumentMessage = text }
            : null;
    }

    internal static DocumentSource DocumentSourceFor(Guid locationId, int days, bool isAllowed) =>
        new($"Ablaufliste {days} Tage", $"api/locations/{locationId}/stock/expiring/pdf?days={days}", IsAllowed: isAllowed, DisabledHint: "Die Ablaufliste druckt nur die Standortleitung.");
}

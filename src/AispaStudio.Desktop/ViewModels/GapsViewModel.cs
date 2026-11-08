using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ein Vorschlag des Servers: der Termin rückt um wenige Minuten, damit bei der Person keine unbrauchbare Lücke bleibt. Zeiten, Person und Grund
/// stammen vom Server; verschoben wird erst mit dem Knopf, nachdem die Rezeption mit dem Gast gesprochen hat.
/// </summary>
public sealed partial class GapRow(GapSuggestion suggestion, TimeZoneInfo zone, Func<GapRow, Task> move) : ViewModelBase
{
    public GapSuggestion Suggestion { get; } = suggestion;

    public string Title => $"{Suggestion.CustomerName}, {Suggestion.TreatmentName}";

    public string Detail => $"bei {Suggestion.EmployeeName} von {Time(Suggestion.CurrentStart)} auf {Time(Suggestion.SuggestedStart)}";

    public string Reason => Suggestion.Reason;

    public string MoveText => $"Auf {Time(Suggestion.SuggestedStart)} verschieben";

    public string NewTime => Time(Suggestion.SuggestedStart);

    private string Time(DateTimeOffset moment) => $"{DayLayout.Local(moment, zone):HH:mm}";

    [RelayCommand]
    private Task MoveAsync() => move(this);
}

/// <summary>
/// „Lücken schließen“ in der Seitenleiste des Kalenders: die Vorschläge des Servers für den angezeigten Tag und den Standort, je mit Knopf zum Verschieben.
/// </summary>
public sealed partial class GapsViewModel(string locationName, Action close) : ViewModelBase, ISidePanel
{
    public string PanelTitle => "Lücken schließen";

    public string? PanelSubtitle => locationName;

    public PanelKind PanelKind => PanelKind.Gaps;

    public string PanelIcon => Icons.Move;

    public string PanelTabText => "Lücken";

    public IRelayCommand CloseCommand => CloseGapsCommand;

    [RelayCommand]
    private void CloseGaps() => close();

    public string Hint { get; } = "Vorher mit dem Gast absprechen; verschoben wird erst mit dem Knopf.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(ShowRows))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(ShowRows))]
    public partial bool LoadFailed { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<GapRow> Rows { get; private set; } = [];

    /// <summary>Ergebnis des letzten Verschiebens oder die Ablehnung des Servers.</summary>
    [ObservableProperty]
    public partial string? Message { get; set; }

    public bool IsEmpty => IsLoaded && !LoadFailed && Rows.Count == 0;

    public bool ShowRows => IsLoaded && !LoadFailed;

    /// <summary>Der Tag wechselt: die alten Vorschläge verschwinden, bis die neuen da sind.</summary>
    public void Reset()
    {
        IsLoaded = false;
        Rows = [];
        Message = null;
    }

    public void Show(IReadOnlyList<GapRow>? rows)
    {
        LoadFailed = rows is null;
        Rows = rows ?? [];
        IsLoaded = true;
    }
}

using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ein Vorschlag des Servers: der Termin rückt um wenige Minuten, damit bei der Person keine unbrauchbare Lücke bleibt. Zeiten, Person und Grund
/// stammen vom Server; verschoben wird erst mit dem Knopf, nachdem die Rezeption mit dem Gast gesprochen hat.
/// </summary>
public sealed partial class GapRow(GapSuggestion suggestion, TimeZoneInfo zone, Func<GapRow, Task> move, bool showDay = false, Action<GapRow>? select = null) : ViewModelBase
{
    /// <summary>Der Vorschlag ist gewählt: der Kalender hebt den Termin hervor und zeigt das Ziel als Vorschau (Durchsicht, Befund 20).</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Name für Screenreader: Titel und Detail der Karte.</summary>
    public string AccessibleName => $"{Title}. {Detail}";

    [RelayCommand]
    private void Select() => select?.Invoke(this);

    public GapSuggestion Suggestion { get; } = suggestion;

    public string Title => $"{Suggestion.CustomerName}, {Suggestion.TreatmentName}";

    /// <summary>In der 7-Tage-Ansicht steht der Tag davor: „Di. 20.10., bei Lena Huber von 10:30 auf 10:10“.</summary>
    public string Detail => $"{(showDay ? $"{DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(Suggestion.CurrentStart, zone)))}, " : "")}bei {Suggestion.EmployeeName} von {Time(Suggestion.CurrentStart)} auf {Time(Suggestion.SuggestedStart)}";

    public string Reason => Suggestion.Reason;

    /// <summary>Der Termin kommt von Treatwell: Die App bucht dort nicht um (ADR 0062).</summary>
    public string? PlatformHint => Suggestion.Source == AppointmentSource.Treatwell ? PlatformText.MoveInPlatform : null;

    /// <summary>Kette: die Termine, die mitrücken, je „Danach: Anna Berg, Massage, 11:35 auf 11:15“.</summary>
    public IReadOnlyList<string> Followers => [.. (Suggestion.Chain ?? []).Select(c =>
        $"Danach: {c.CustomerName}, {c.TreatmentName}, {Time(c.CurrentStart)} auf {Time(c.SuggestedStart)}"
        + (c.Source == AppointmentSource.Treatwell ? $" ({PlatformText.MoveInPlatform})" : ""))];

    public bool IsChain => Suggestion.Chain is { Count: > 0 };

    public string MoveText => IsChain ? $"Alle {Suggestion.Chain!.Count + 1} verschieben" : $"Auf {Time(Suggestion.SuggestedStart)} verschieben";

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

    public string Hint { get; } = "Vorher mit dem Gast absprechen; verschoben wird erst mit dem Knopf. Ein Klick auf einen Vorschlag zeigt ihn im Kalender.";

    /// <summary>Der gewählte Vorschlag; der Kalender hebt seinen Termin hervor und zeigt das Ziel.</summary>
    public GapRow? SelectedRow
    {
        get => field;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.IsSelected = false;
            field = value;
            field?.IsSelected = true;
            OnPropertyChanged();
        }
    }

    /// <summary>Wählt den Vorschlag; ein zweiter Klick auf denselben hebt die Wahl auf.</summary>
    public void Select(GapRow row) => SelectedRow = ReferenceEquals(SelectedRow, row) ? null : row;

    /// <summary>In der 7-Tage-Ansicht: für welche Tage und welche Person die Vorschläge gelten; sonst leer (der angezeigte Tag).</summary>
    [ObservableProperty]
    public partial string? Scope { get; set; }

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
        SelectedRow = null;
        Rows = [];
        Message = null;
    }

    public void Show(IReadOnlyList<GapRow>? rows)
    {
        SelectedRow = null;
        LoadFailed = rows is null;
        Rows = rows ?? [];
        IsLoaded = true;
    }
}

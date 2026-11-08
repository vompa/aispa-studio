using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Zeitraum der Bedarfsprüfung.</summary>
public sealed record DemandPeriod(int Days, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Bedarf eines Materials im Zeitraum; alle Zahlen stammen aus der API.</summary>
public sealed record DemandRow(string Material, string OnHandText, string NeededText, string ShortfallText, bool IsShort, string AppointmentsText, string LeftText)
{
    public string Summary => $"{Material}, Bestand {OnHandText}, Bedarf {NeededText}, {(IsShort ? $"es fehlen {ShortfallText}, " : "")}{AppointmentsText}, danach noch für {LeftText}";

    public override string ToString() => Summary;
}

/// <summary>
/// Bedarfsprüfung (R05 Slice 123, Ä223) nach <c>StockDemand.tsx</c>: reicht der Bestand für die gebuchten, offenen Termine des Zeitraums? Rechnet in der API und bucht nichts.
/// </summary>
public sealed partial class StockDemandViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;
    private readonly TimeProvider _time;
    private int _version;

    internal StockDemandViewModel(StockViewModel owner, TimeProvider time)
    {
        _owner = owner;
        _time = time;
        SelectedPeriod = Periods[1];
    }

    public IReadOnlyList<DemandPeriod> Periods { get; } = [new(1, "Heute"), new(7, "Nächste 7 Tage"), new(14, "Nächste 14 Tage")];

    public ObservableCollection<DemandRow> Rows { get; } = [];

    [ObservableProperty]
    public partial DemandPeriod SelectedPeriod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial string? WarningText { get; private set; }

    public bool HasWarnings => WarningText is not null;

    /// <summary>Die eine Zeile über den Hinweisen („2 Hinweise zur Bedarfsprüfung“); die Hinweise selbst stehen aufgeklappt darunter, nicht als Textwand.</summary>
    [ObservableProperty]
    public partial string? WarningTitle { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    public partial string? SummaryText { get; private set; }

    public bool HasSummary => SummaryText is not null;

    public bool HasRows => Rows.Count > 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var version = ++_version;
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var query = string.Create(CultureInfo.InvariantCulture, $"from={today:yyyy-MM-dd}&until={today.AddDays(SelectedPeriod.Days - 1):yyyy-MM-dd}");
        var result = await _owner.Api.GetAsync<StockDemandDto>($"api/stock/locations/{location.Id}/demand?{query}").ConfigureAwait(true);
        if (version != _version)
        {
            return; // Zeitraum oder Standort wurden inzwischen gewechselt; die neuere Antwort gilt.
        }

        LoadError = result.Value is null ? result.Error ?? "Die Bedarfsprüfung wurde nicht geladen." : null;
        Show(result.Value);
    }

    /// <summary>Beim Standortwechsel: die Zahlen des alten Standorts und seine noch laufende Antwort gelten nicht mehr.</summary>
    internal void Reset()
    {
        _version++;
        LoadError = null;
        Show(null);
    }

    private void Show(StockDemandDto? demand)
    {
        Rows.Clear();
        foreach (var line in demand?.Lines ?? [])
        {
            Rows.Add(new DemandRow(line.ItemName, Amount(line.OnHand, line), Amount(line.Needed, line), line.Shortfall > 0 ? Amount(line.Shortfall, line) : "", line.Shortfall > 0,
                line.Appointments == 1 ? "1 Termin" : $"{line.Appointments} Termine", line.TreatmentsLeft == 1 ? "1 Behandlung" : $"{line.TreatmentsLeft} Behandlungen"));
        }

        WarningText = demand is { Warnings.Count: > 0 } ? string.Join(Environment.NewLine, demand.Warnings.Select(w => w.Message)) : null;
        WarningTitle = demand is { Warnings.Count: > 0 } ? (demand.Warnings.Count == 1 ? "1 Hinweis zur Bedarfsprüfung" : $"{demand.Warnings.Count} Hinweise zur Bedarfsprüfung") : null;
        SummaryText = demand is null or { Warnings.Count: > 0 } ? null : demand.Appointments switch
        {
            0 => "Keine gebuchten offenen Termine mit Lagermaterial in diesem Zeitraum.",
            1 => "Der Bestand reicht für den gebuchten Termin.",
            var count => $"Der Bestand reicht für die {count} gebuchten Termine.",
        };
        OnPropertyChanged(nameof(HasRows));
    }

    private static string Amount(decimal value, StockDemandLine line) => $"{StockFormat.Quantity(value)} {line.Unit}";

    partial void OnSelectedPeriodChanged(DemandPeriod value) => _ = LoadAsync();
}

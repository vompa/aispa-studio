using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Zeitraum des Verbrauchsberichts: die letzten Tage bis heute.</summary>
public sealed record UsagePeriod(int Days, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Eine Behandlung, die den Artikel nutzt, mit ihrem Soll im Zeitraum.</summary>
public sealed record UsageTreatmentRow(string Name, string PlannedText, string AppointmentsText)
{
    public string Summary => $"{Name}, Soll {PlannedText}, {AppointmentsText}";

    public override string ToString() => Summary;
}

/// <summary>
/// Eine Zeile der Tabelle: Soll, Ist, Abweichung in Menge und Prozent und der Wert der Abweichung; alle Zahlen stammen aus der API. Aufklappen zeigt die Behandlungen, die das Material nutzen.
/// </summary>
public sealed partial class UsageRow(string material, string plannedText, string actualText, string deviationText, bool isLoss, bool hasDeviation, string valueText, IReadOnlyList<UsageTreatmentRow> treatments)
    : ObservableObject
{
    public string Material { get; } = material;

    public string PlannedText { get; } = plannedText;

    public string ActualText { get; } = actualText;

    /// <summary>„+120 ml · 18 %“; ohne Abweichung die Menge 0.</summary>
    public string DeviationText { get; } = deviationText;

    public bool IsLoss { get; } = isLoss;

    public bool HasDeviation { get; } = hasDeviation;

    public string ValueText { get; } = valueText;

    public IReadOnlyList<UsageTreatmentRow> Treatments { get; } = treatments;

    public string Summary => $"{Material}, Soll {PlannedText}, Ist {ActualText}, Abweichung {DeviationText}{(ValueText.Length > 0 ? $", Wert {ValueText}" : "")}";

    public bool HasTreatments => Treatments.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph), nameof(ToggleName))]
    public partial bool IsExpanded { get; set; }

    public string Glyph => IsExpanded ? "▾" : "▸";

    public string ToggleName => $"{Material}, Behandlungen {(IsExpanded ? "ausblenden" : "zeigen")}";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    public override string ToString() => Summary;
}

/// <summary>
/// Verbrauch (R05 Slice 116, Ä271; seit ADR 0098 ein Abschnitt der Übersicht): je Material das Soll (gebuchter Verbrauch laut Rezept), das Ist (Soll plus Mehrverbrauch aus den Inventuren), die Abweichung
/// in Menge und Prozent und ihr Wert. Reihenfolge und Prozent liefert die API (größter Wert zuerst); als Standard zeigt die Tabelle nur Materialien mit Abweichung, die übrigen stehen als eine Zeile mit „Alle zeigen“.
/// Rechnet in der API (Recht ViewFinance) und bucht nichts; den Wert in Euro liefert sie nur mit dem Recht „Berichte mit Beträgen“. Die Summe zeigt allein die Kachel der Übersicht.
/// </summary>
public sealed partial class StockUsageViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;
    private readonly TimeProvider _time;
    private List<UsageRow> _all = [];
    private int _version;

    internal StockUsageViewModel(StockViewModel owner, TimeProvider time)
    {
        _owner = owner;
        _time = time;
        SelectedPeriod = Periods[1];
    }

    public IReadOnlyList<UsagePeriod> Periods { get; } = [new(7, "Letzte 7 Tage"), new(30, "Letzte 30 Tage"), new(90, "Letzte 90 Tage")];

    /// <summary>Die gezeigten Zeilen: mit „Nur Abweichungen“ ohne die Materialien, die genau nach Rezept verbraucht wurden.</summary>
    public ObservableCollection<UsageRow> Rows { get; } = [];

    [ObservableProperty]
    public partial UsagePeriod SelectedPeriod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Standard: nur Materialien mit Abweichung; ausgeschaltet stehen alle in der Tabelle.</summary>
    [ObservableProperty]
    public partial bool OnlyDeviations { get; set; } = true;

    /// <summary>Der Mehrverbrauch zum Einstandspreis als Betrag für die Kachel der Übersicht; ohne das Recht „Berichte mit Beträgen“ fehlt er.</summary>
    internal string? ExcessValueText { get; private set; }

    /// <summary>Ohne das Recht „Berichte mit Beträgen“ fehlen die Werte in Euro; die Maske sagt es einmal.</summary>
    [ObservableProperty]
    public partial bool AmountsHidden { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoDeviation))]
    public partial bool IsLoaded { get; private set; }

    public bool HasRows => Rows.Count > 0;

    /// <summary>Im Zeitraum wurde nichts verbraucht.</summary>
    public bool ShowEmpty => IsLoaded && _all.Count == 0 && !HasLoadError;

    /// <summary>Es gibt Verbrauch, aber keine Abweichung, und die übrigen Materialien sind ausgeblendet.</summary>
    public bool ShowNoDeviation => IsLoaded && Rows.Count == 0 && HiddenCount > 0;

    public int HiddenCount => _all.Count - Rows.Count;

    public bool HasHidden => HiddenCount > 0;

    public string HiddenText => HiddenCount == 1 ? "1 weiteres Material nach Rezept verbraucht" : $"{HiddenCount} weitere Materialien nach Rezept verbraucht";

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var version = ++_version;
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var query = string.Create(CultureInfo.InvariantCulture, $"from={today.AddDays(1 - SelectedPeriod.Days):yyyy-MM-dd}&until={today:yyyy-MM-dd}");
        var result = await _owner.Api.GetAsync<StockUsageReportDto>($"api/reports/locations/{location.Id}/stock-usage?{query}").ConfigureAwait(true);
        if (version != _version)
        {
            return; // Zeitraum oder Standort wurden inzwischen gewechselt; die neuere Antwort gilt.
        }

        LoadError = result.Value is null ? result.Error ?? "Der Verbrauch wurde nicht geladen." : null;
        Show(result.Value);
    }

    /// <summary>„Alle zeigen“ in der Zeile unter der Tabelle.</summary>
    [RelayCommand]
    private void ShowAll() => OnlyDeviations = false;

    /// <summary>Beim Standortwechsel: die Zahlen des alten Standorts und seine noch laufende Antwort gelten nicht mehr.</summary>
    internal void Reset()
    {
        _version++;
        LoadError = null;
        Show(null);
    }

    private void Show(StockUsageReportDto? report)
    {
        _all = [.. (report?.Lines ?? []).Select(Row)];
        AmountsHidden = report?.AmountsHidden == true;
        ExcessValueText = report?.ExcessValue is { } total ? Money.Format(total) : null;
        IsLoaded = report is not null || LoadError is not null;
        ApplyFilter();
        _owner.Todo.RefreshStats();
    }

    /// <summary>Füllt die gezeigten Zeilen in der Reihenfolge der API.</summary>
    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _all.Where(r => !OnlyDeviations || r.HasDeviation))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoDeviation));
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHidden));
        OnPropertyChanged(nameof(HiddenText));
    }

    private static UsageRow Row(StockUsageLineDto line) =>
        new(line.Name, Amount(line.Planned, line.Unit), Amount(line.Actual, line.Unit), Deviation(line), line.Excess > 0, line.Excess != 0,
            line.ExcessValue is { } value ? Money.Format(value) : "",
            [.. line.Treatments.Select(t => new UsageTreatmentRow(t.Name, Amount(t.Planned, line.Unit), t.Appointments == 1 ? "1 Termin" : $"{t.Appointments} Termine"))]);

    /// <summary>„+120 ml · 18 %“ bei Mehrverbrauch, „-50 ml · -17 %“ bei Fund, „0 ml“ ohne Abweichung; den Prozentanteil rechnet die API.</summary>
    private static string Deviation(StockUsageLineDto line)
    {
        var amount = $"{(line.Excess > 0 ? "+" : "")}{Amount(line.Excess, line.Unit)}";
        return line.Excess != 0 && line.ExcessPercent is { } percent ? $"{amount} · {StockFormat.Quantity(percent)} %" : amount;
    }

    private static string Amount(decimal value, string unit) => $"{StockFormat.Quantity(value)} {unit}";

    partial void OnSelectedPeriodChanged(UsagePeriod value) => _ = LoadAsync();

    partial void OnOnlyDeviationsChanged(bool value) => ApplyFilter();
}

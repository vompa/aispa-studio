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

/// <summary>Soll und Ist eines Artikels; alle Zahlen stammen aus der API.</summary>
public sealed record UsageRow(string Material, string PlannedText, string ActualText, string ExcessText, bool IsLoss, string ValueText, IReadOnlyList<UsageTreatmentRow> Treatments)
{
    public string Summary => $"{Material}, Soll {PlannedText}, Ist {ActualText}, Mehrverbrauch {ExcessText}{(ValueText.Length > 0 ? $", Wert {ValueText}" : "")}";

    public bool HasTreatments => Treatments.Count > 0;

    public override string ToString() => Summary;
}

/// <summary>
/// Verbrauch (R05 Slice 116, Ä271) nach <c>StockUsage.tsx</c>: je Artikel das Soll (gebuchter Verbrauch laut Rezept), das Ist (Soll plus Mehrverbrauch aus den Inventuren) und die Behandlungen,
/// die den Artikel nutzen. Rechnet in der API (Recht ViewFinance) und bucht nichts; den Wert in Euro liefert sie nur mit dem Recht „Berichte mit Beträgen“.
/// </summary>
public sealed partial class StockUsageViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;
    private readonly TimeProvider _time;
    private int _version;

    internal StockUsageViewModel(StockViewModel owner, TimeProvider time)
    {
        _owner = owner;
        _time = time;
        SelectedPeriod = Periods[1];
    }

    public IReadOnlyList<UsagePeriod> Periods { get; } = [new(7, "Letzte 7 Tage"), new(30, "Letzte 30 Tage"), new(90, "Letzte 90 Tage")];

    public ObservableCollection<UsageRow> Rows { get; } = [];

    [ObservableProperty]
    public partial UsagePeriod SelectedPeriod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTotal))]
    public partial string? TotalText { get; private set; }

    public bool HasTotal => TotalText is not null;

    /// <summary>Ohne das Recht „Berichte mit Beträgen“ fehlen die Werte in Euro; die Maske sagt es einmal.</summary>
    [ObservableProperty]
    public partial bool AmountsHidden { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsLoaded { get; private set; }

    public bool HasRows => Rows.Count > 0;

    public bool ShowEmpty => IsLoaded && Rows.Count == 0 && !HasLoadError;

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

    /// <summary>Beim Standortwechsel: die Zahlen des alten Standorts und seine noch laufende Antwort gelten nicht mehr.</summary>
    internal void Reset()
    {
        _version++;
        LoadError = null;
        Show(null);
    }

    private void Show(StockUsageReportDto? report)
    {
        Rows.Clear();
        foreach (var line in report?.Lines ?? [])
        {
            Rows.Add(new UsageRow(line.Name, Amount(line.Planned, line.Unit), Amount(line.Actual, line.Unit), Amount(line.Excess, line.Unit), line.Excess > 0,
                line.ExcessValue is { } value ? Money.Format(value) : "", [.. line.Treatments.Select(t => new UsageTreatmentRow(t.Name, Amount(t.Planned, line.Unit), t.Appointments == 1 ? "1 Termin" : $"{t.Appointments} Termine"))]));
        }

        AmountsHidden = report?.AmountsHidden == true;
        TotalText = report?.ExcessValue is { } total ? $"Mehrverbrauch zum Einstandspreis: {Money.Format(total)}" : null;
        IsLoaded = report is not null || LoadError is not null;
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private static string Amount(decimal value, string unit) => $"{StockFormat.Quantity(value)} {unit}";

    partial void OnSelectedPeriodChanged(UsagePeriod value) => _ = LoadAsync();
}

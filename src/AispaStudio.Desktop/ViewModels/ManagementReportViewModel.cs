using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

public sealed record KpiRow(string Label, string Value, string? Previous)
{
    public bool HasPrevious => Previous is not null;
}

public sealed record RevenueRow(string Label, string Gross, string Net);

public sealed record EmployeeKpiRow(string Name, string ShiftHours, string TreatmentHours, string Utilization, string RevenuePerHour);

public sealed record TreatmentKpiRow(string Name, string Appointments, string Revenue, string Margin, bool IsLoss);

public sealed record BenchmarkPeriod(int Days, string Label)
{
    public override string ToString() => Label;
}

public sealed record BenchmarkRow(string Label, string Own, string Rating, string Range, string? Tip)
{
    public bool HasTip => Tip is not null;
}

/// <summary>Reiter „Management“ nach web/src/reports/ManagementView.tsx und BenchmarkView.tsx: Kennzahlen eines Zeitraums, auf Wunsch mit dem Vergleich zu anderen Spas.</summary>
public sealed partial class ManagementReportViewModel(IWorkstationApi api, TimeProvider time) : ReportTabViewModel(api, time)
{
    public const string AmountsHint = "Beträge nur mit Berechtigung: Umsätze, Kosten und Margen sieht, wer das Recht „Berichte mit Beträgen“ hat. Das vergibt der Inhaber oder ein Manager unter „Zugänge“.";

    private bool _rangeIsSet;
    private int _benchmarkVersion;

    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    [ObservableProperty]
    public partial DateOnly? Until { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Alerts { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<KpiRow> Kpis { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<RevenueRow> RevenueByAccount { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<RevenueRow> RevenueByPayment { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<EmployeeKpiRow> Employees { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TreatmentKpiRow> Treatments { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAmounts))]
    public partial bool AmountsHidden { get; private set; }

    public bool ShowAmounts => !AmountsHidden;

    [ObservableProperty]
    public partial bool Compare { get; set; }

    public IReadOnlyList<BenchmarkPeriod> BenchmarkPeriods { get; } = [new(30, "30 Tage"), new(90, "90 Tage"), new(365, "12 Monate")];

    [ObservableProperty]
    public partial BenchmarkPeriod? SelectedPeriod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBenchmark), nameof(BenchmarkTitle), nameof(BenchmarkSummary), nameof(BenchmarkIsSample))]
    public partial BenchmarkReport? Benchmark { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<BenchmarkRow> BenchmarkRows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBenchmarkError))]
    public partial string? BenchmarkError { get; private set; }

    public bool HasBenchmark => Compare && Benchmark is not null;

    public bool HasBenchmarkError => BenchmarkError is not null;

    public bool BenchmarkIsSample => Benchmark?.IsSampleData == true;

    public string BenchmarkTitle => Benchmark is { } report
        ? $"{report.PeerGroup}, {report.PeerCount} Spas · {StockFormat.Date(report.From)} bis {StockFormat.Date(report.Until)}"
        : "";

    public string BenchmarkSummary
    {
        get
        {
            var metrics = Benchmark?.Metrics ?? [];
            var top = metrics.Count(m => m.Rating == BenchmarkRating.Top);
            var levers = metrics.Count(m => m.Rating is BenchmarkRating.BelowMedian or BenchmarkRating.Bottom);
            return $"{top} {(top == 1 ? "Kennzahl" : "Kennzahlen")} im stärksten Viertel, {levers} Hebel unter der Mitte.";
        }
    }

    public IReadOnlyList<string> BenchmarkNotes => Benchmark?.Notes ?? [];

    /// <summary>Zeitraum eines Sprungs („Im Bericht öffnen“); ohne Angabe bleibt der Standard Monatserster bis heute.</summary>
    public void SetRange(DateOnly? from, DateOnly? until)
    {
        _rangeIsSet = true;
        var today = Today();
        (From, Until) = (from ?? new DateOnly(today.Year, today.Month, 1), until ?? today);
    }

    protected override async Task ReloadAsync()
    {
        if (!_rangeIsSet)
        {
            SetRange(null, null);
        }

        if (Location is not { } location || From is not { } from || Until is not { } until)
        {
            return;
        }

        var version = NextVersion();
        LoadError = null;
        var result = await Api.GetAsync<ManagementReportDto>($"api/reports/locations/{location.Id}/management?from={Iso(from)}&until={Iso(until)}").ConfigureAwait(true);
        if (version != Version)
        {
            return;
        }

        if (result.Value is not { } report)
        {
            LoadError = result.Error ?? "Der Bericht konnte nicht geladen werden.";
            Alerts = [];
            Kpis = [];
            RevenueByAccount = [];
            RevenueByPayment = [];
            Employees = [];
            Treatments = [];
            return;
        }

        Apply(report);
        IsLoaded = true;
        await LoadBenchmarkAsync().ConfigureAwait(true);
    }

    partial void OnFromChanged(DateOnly? value) => ReloadAfterRangeChange();

    partial void OnUntilChanged(DateOnly? value) => ReloadAfterRangeChange();

    partial void OnCompareChanged(bool value)
    {
        OnPropertyChanged(nameof(HasBenchmark));
        _ = LoadBenchmarkAsync();
    }

    partial void OnSelectedPeriodChanged(BenchmarkPeriod? value)
    {
        if (!_settingDefaultPeriod)
        {
            _ = LoadBenchmarkAsync();
        }
    }

    partial void OnBenchmarkChanged(BenchmarkReport? value) => OnPropertyChanged(nameof(BenchmarkNotes));

    private void ReloadAfterRangeChange()
    {
        if (IsLoaded && From is not null && Until is not null)
        {
            _ = ReloadAsync();
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(Time.GetLocalNow().DateTime);

    private void Apply(ManagementReportDto report)
    {
        AmountsHidden = report.AmountsHidden;
        Alerts = report.Alerts;
        Kpis = [.. report.Kpis.Select(k => new KpiRow(k.Label, KpiValue(k.Value, k.Unit), k.Previous is { } previous ? $"Vorperiode {KpiValue(previous, k.Unit)}" : null))];
        RevenueByAccount = [.. report.RevenueByAccount.Select(r => new RevenueRow(r.CostCenter is { } center ? $"{r.Label} ({center})" : r.Label, Money.Format(r.Gross), $"netto {Money.Format(r.Net)}"))];
        RevenueByPayment = [.. report.RevenueByPayment.Select(r => new RevenueRow(r.Label, Money.Format(r.Gross), ""))];
        Employees = [.. report.Employees.Select(e => new EmployeeKpiRow(
            e.Name, Number(e.ShiftHours, 1), Number(e.TreatmentHours, 1), $"{e.Utilization} %", e.RevenuePerHour is { } revenue ? Money.Format(revenue) : "–"))];
        Treatments = [.. report.Treatments.Select(t => new TreatmentKpiRow(
            t.Name, Number(t.Appointments), t.NetRevenue is { } net ? Money.Format(net) : "–", t.MarginPerAppointment is { } margin ? Money.Format(margin) : "–", t.MarginPerAppointment < 0))];
    }

    private static string KpiValue(decimal value, string unit) => unit switch
    {
        "€" => Money.Format(value),
        "%" => $"{Number(value, 1)} %",
        "" => Number(value),
        _ => $"{Number(value)} {unit}",
    };

    private bool _settingDefaultPeriod;

    private async Task LoadBenchmarkAsync()
    {
        var version = ++_benchmarkVersion;
        BenchmarkError = null;
        if (!Compare || Location is not { } location)
        {
            return;
        }

        // Vorgabe wie im Web: 30 Tage. Das Setzen der Vorgabe löst keinen zweiten Abruf aus.
        if (SelectedPeriod is null)
        {
            _settingDefaultPeriod = true;
            SelectedPeriod = BenchmarkPeriods[0];
            _settingDefaultPeriod = false;
        }

        var result = await Api.GetAsync<BenchmarkReport>($"api/reports/locations/{location.Id}/benchmark?days={SelectedPeriod.Days}").ConfigureAwait(true);
        if (version != _benchmarkVersion)
        {
            return;
        }

        Benchmark = result.Value;
        BenchmarkError = result.Value is null ? result.Error ?? "Der Vergleich konnte nicht geladen werden." : null;
        BenchmarkRows = [.. (result.Value?.Metrics ?? []).Select(m => new BenchmarkRow(
            m.Label,
            m.Own is { } own ? BenchmarkValue(own, m.Unit, result.Value!.Currency) : "–",
            RatingText(m.Rating),
            $"Mitte {BenchmarkValue(m.Median, m.Unit, result.Value!.Currency)}, mittlere Hälfte {BenchmarkValue(Math.Min(m.P25, m.P75), m.Unit, result.Value!.Currency)} bis {BenchmarkValue(Math.Max(m.P25, m.P75), m.Unit, result.Value!.Currency)}{(m.HigherIsBetter ? "" : " (weniger ist besser)")}",
            m.Tip))];
    }

    private static string BenchmarkValue(decimal value, string unit, string currency) => unit switch
    {
        "€" or "money" => Money.Format(value, currency),
        "%" => $"{Number(value, 1)} %",
        "" => Number(value, 1),
        _ => $"{Number(value, 1)} {unit}",
    };

    private static string RatingText(BenchmarkRating? rating) => rating switch
    {
        BenchmarkRating.Top => "Stärkstes Viertel",
        BenchmarkRating.AboveMedian => "Über der Mitte",
        BenchmarkRating.BelowMedian => "Unter der Mitte",
        BenchmarkRating.Bottom => "Schwächstes Viertel",
        _ => "Keine Daten",
    };
}

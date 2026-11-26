using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kachel einer Kennzahl. <c>Change</c> ist die Veränderung zum Vergleich mit Pfeil („↓ 24 %“, bei Quoten in Prozentpunkten), <c>State</c> ihre Richtung (Haken: besser, Warnzeichen:
/// schlechter, sonst keines), <c>Spoken</c> der Satz für Screenreader („24 % unter der Vorperiode“). Pfeil, Text und Zeichen sagen dasselbe wie die Farbe.
/// </summary>
public sealed record KpiRow(string Label, string Value, string? Previous, string? Change = null, StatTileState State = StatTileState.None, string? Spoken = null)
{
    public bool HasPrevious => Previous is not null;

    /// <summary>Zeilen unter dem Wert: Veränderung und darunter der Vergleichswert („↓ 24 %“, „Vorjahr 25.371,00 €“).</summary>
    public string? Hint => Change is not null && Previous is not null ? $"{Change}{Environment.NewLine}{Previous}" : Change ?? Previous;
}

/// <summary>Wählbarer Vergleich (API-Parameter <c>compare</c>): Zeitraum davor, Vormonat bis zum selben Tag oder Vorjahr. <c>Noun</c> beschriftet den Vergleichswert, <c>Dative</c> den Satz für Screenreader.</summary>
public sealed record CompareChoice(string Key, string Label, string Noun, string Dative)
{
    public bool IsDefault => Key == "previous";

    public override string ToString() => Label;
}

/// <summary>Schnellwahl für den Zeitraum; <c>Range</c> rechnet Von und Bis aus dem heutigen Tag.</summary>
public sealed record QuickRange(string Label, Func<DateOnly, (DateOnly From, DateOnly Until)> Range)
{
    public override string ToString() => Label;
}

/// <summary>Kacheln einer Gruppe mit ihrer Überschrift („Umsatz und Ertrag“, „Auslastung“, „Gäste“, „Ausfälle“), wie die API sie ordnet.</summary>
public sealed record KpiGroupRow(string Title, IReadOnlyList<KpiRow> Kpis);

public sealed record RevenueRow(string Label, string Gross, string Net);

public sealed record EmployeeKpiRow(string Name, string ShiftHours, string TreatmentHours, string Utilization, string RevenuePerHour);

public sealed record TreatmentKpiRow(string Name, string Appointments, string Revenue, string Margin, bool IsLoss);

/// <summary>Wirkung einer Aktion: Positionen, Gäste (davon neu), Rabatt und Umsatz; „–“ ohne das Recht „Berichte mit Beträgen“.</summary>
public sealed record PromotionKpiRow(string Name, string Positions, string Guests, string NewGuests, string Discount, string Revenue);

/// <summary>Ein Feld der Tabelle Wochentag × Tageszeit: „33,3 % · schwach“ trägt die Kennzeichnung im Text, nicht nur in der Farbe; <c>Spoken</c> für Screenreader.</summary>
public sealed record LoadCell(string Text, bool IsWeak, string Spoken);

/// <summary>Zeile der Tabelle Wochentag × Tageszeit (Vormittag, Mittag, Nachmittag, Abend).</summary>
public sealed record WeekdayLoadRow(string Day, LoadCell Morning, LoadCell Midday, LoadCell Afternoon, LoadCell Evening);

/// <summary>Zeile einer einfachen Tabelle mit Name und bis zu drei Werten (Räume, Kanäle, Gäste, Vorschau); leere Spalten bleiben leer.</summary>
public sealed record ReportRow(string Name, string First, string Second = "", string Third = "");

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
    /// <summary>So oft fragt der Reiter nach dem KI-Kommentar, solange er aussteht (wie im Tagesstart).</summary>
    private const int AiAttempts = 15;

    /// <summary>Kommentar der KI aus genau diesen Zahlen (ab sieben Tagen, nur mit KI); die Zahlen selbst kommen nie von der KI.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiSummary))]
    public partial string? AiSummary { get; private set; }

    [ObservableProperty]
    public partial string? AiNotice { get; private set; }

    [ObservableProperty]
    public partial bool IsAiPending { get; private set; }

    public bool HasAiSummary => AiSummary is not null;

    // ---------- „Mit KI auswerten“ (T-096 E2) ----------

    private ManagementAnalysisViewModel? _analysis;

    /// <summary>Die Seitenleiste „Mit KI auswerten“; ihre übernommene Auswertung geht an PDF, Word und Excel.</summary>
    public ManagementAnalysisViewModel Analysis => _analysis ??= new ManagementAnalysisViewModel(Api, Time) { AdoptedChanged = OnAnalysisAdoptedChanged };

    /// <summary>Ohne Claude-Schlüssel im Server ist „Mit KI auswerten“ gesperrt (<see cref="AnalysisUnavailableHint"/>).</summary>
    [ObservableProperty]
    public partial bool AiAnalysisAvailable { get; private set; }

    public const string AnalysisUnavailableHint = "Verfügbar, sobald auf dem Server ein Claude-Schlüssel eingetragen ist.";

    private void OnAnalysisAdoptedChanged()
    {
        if (Location is { } location && From is { } from && Until is { } until)
        {
            UpdateDocument(location, from, until, Comparison);
        }
    }

    public static IReadOnlyList<CompareChoice> CompareChoices { get; } =
    [
        new("previous", "Zeitraum davor", "Vorperiode", "der Vorperiode"),
        new("month", "Vormonat", "Vormonat", "dem Vormonat"),
        new("year", "Vorjahr", "Vorjahr", "dem Vorjahr"),
    ];

    public static IReadOnlyList<QuickRange> QuickRanges { get; } =
    [
        new("Dieser Monat", today => (new DateOnly(today.Year, today.Month, 1), today)),
        new("Letzter Monat", today =>
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            return (first, first.AddMonths(1).AddDays(-1));
        }),
        new("Dieses Quartal", today => (new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), today)),
        new("Dieses Jahr", today => (new DateOnly(today.Year, 1, 1), today)),
        new("Letzte 12 Monate", today => (today.AddYears(-1).AddDays(1), today)),
    ];

    /// <summary>Womit der Zeitraum verglichen wird; wirkt auf alle Kacheln. Standard ist der gleich lange Zeitraum davor, wie bisher.</summary>
    [ObservableProperty]
    public partial CompareChoice? SelectedComparison { get; set; } = CompareChoices[0];

    private CompareChoice Comparison => SelectedComparison ?? CompareChoices[0];

    /// <summary>Schnellwahl für Von und Bis; sie bleibt stehen, solange die Daten ihrem Zeitraum entsprechen.</summary>
    [ObservableProperty]
    public partial QuickRange? SelectedQuickRange { get; set; }

    private bool _applyingQuickRange;

    /// <summary>Reicht „Bis“ bis heute oder darüber, fehlt dem heutigen Tag noch der Rest: Umsatz und Termine können noch wachsen.</summary>
    public bool IsTodayOpen => Until is { } until && until >= Today();

    public const string TodayOpenText = "Heute ist noch nicht abgeschlossen. Die Zahlen können sich noch ändern.";

    /// <summary>„Vergleich: Vormonat · 01.10.2026 bis 26.10.2026“, damit klar ist, womit die Kacheln rechnen.</summary>
    [ObservableProperty]
    public partial string? CompareInfo { get; private set; }

    /// <summary>Öffnet den Ordner der gespeicherten Datei („Ordner öffnen“ neben „Gespeichert: …“); Tests setzen einen eigenen.</summary>
    public IFolderOpener FolderOpener { get; set; } = new ExplorerFolderOpener();

    // ---------- Wochen- und Monatsbericht per Mail ----------

    /// <summary>Den Haken gibt es nur für Rollen, die den Bericht per Mail bekommen (Inhaber, Manager, Standortleitung).</summary>
    [ObservableProperty]
    public partial bool CanMailReports { get; private set; }

    /// <summary>Wochen- und Monatsbericht per Mail; ändern speichert sofort für die angemeldete Person.</summary>
    [ObservableProperty]
    public partial bool MailReports { get; set; }

    private bool _settingMail;

    private async Task LoadMailSubscriptionAsync()
    {
        var result = await Api.GetAsync<ReportMailSubscription>("api/reports/mail-subscription").ConfigureAwait(true);
        _settingMail = true;
        CanMailReports = result.Value?.Available == true;
        MailReports = result.Value?.Enabled == true;
        _settingMail = false;
    }

    partial void OnMailReportsChanged(bool value)
    {
        if (!_settingMail)
        {
            _ = SaveMailSubscriptionAsync(value);
        }
    }

    private async Task SaveMailSubscriptionAsync(bool enabled)
    {
        var result = await Api.PutAsync<ReportMailSubscription>("api/reports/mail-subscription", new ReportMailSubscriptionInput(enabled)).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            LoadError = result.Error ?? "Die Einstellung wurde nicht gespeichert.";
            _settingMail = true;
            MailReports = !enabled;
            _settingMail = false;
            return;
        }

        _settingMail = true;
        MailReports = saved.Enabled;
        _settingMail = false;
    }

    public const string AmountsHint = "Beträge nur mit Berechtigung: Umsätze, Kosten und Margen sieht, wer das Recht „Berichte mit Beträgen“ hat. Das vergibt der Inhaber oder ein Manager unter „Zugänge“.";

    private bool _rangeIsSet;
    private int _benchmarkVersion;

    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTodayOpen))]
    public partial DateOnly? Until { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Alerts { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<KpiRow> Kpis { get; private set; } = [];

    /// <summary>Die Kacheln nach Gruppen mit Überschrift; Kennzahlen ohne Gruppe (ältere API) stehen unter „Kennzahlen“.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<KpiGroupRow> KpiGroups { get; private set; } = [];

    /// <summary>Verlauf je Tag (über 62 Tage je Woche) als drei Reihen; der Umsatz fehlt ohne das Recht „Berichte mit Beträgen“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrend), nameof(HasTrendRevenue))]
    public partial IReadOnlyList<TrendPoint> Trend { get; private set; } = [];

    public bool HasTrend => Trend.Count > 0;

    public bool HasTrendRevenue => Trend.Any(p => p.RevenueNet is not null);

    public IReadOnlyList<TrendBar> TrendRevenue => [.. Trend.Select(p => new TrendBar(TrendLabel(p), (double)(p.RevenueNet ?? 0), Money.Format(p.RevenueNet ?? 0)))];

    public IReadOnlyList<TrendBar> TrendAppointments => [.. Trend.Select(p => new TrendBar(TrendLabel(p), p.Appointments, Number(p.Appointments)))];

    public IReadOnlyList<TrendBar> TrendUtilization => [.. Trend.Select(p => new TrendBar(TrendLabel(p), (double)p.StaffUtilization, $"{Number(p.StaffUtilization, 1)} %"))];

    /// <summary>„Mo 02.11.“ für einen Tag, „02.11.–08.11.“ für eine Woche.</summary>
    private static string TrendLabel(TrendPoint point) => point.From == point.Until
        ? $"{German.DateTimeFormat.GetAbbreviatedDayName(point.From.DayOfWeek)[..2]} {point.From:dd.MM.}"
        : $"{point.From:dd.MM.}–{point.Until:dd.MM.}";

    private static readonly global::System.Globalization.CultureInfo German = global::System.Globalization.CultureInfo.GetCultureInfo("de-AT");

    partial void OnTrendChanged(IReadOnlyList<TrendPoint> value)
    {
        OnPropertyChanged(nameof(TrendRevenue));
        OnPropertyChanged(nameof(TrendAppointments));
        OnPropertyChanged(nameof(TrendUtilization));
    }

    [ObservableProperty]
    public partial IReadOnlyList<RevenueRow> RevenueByAccount { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<RevenueRow> RevenueByPayment { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<EmployeeKpiRow> Employees { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TreatmentKpiRow> Treatments { get; private set; } = [];

    /// <summary>Aktionen an der Kassa im Zeitraum (ADR 0071); leer, wenn keine kassiert wurde.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PromotionKpiRow> Promotions { get; private set; } = [];

    /// <summary>Auslastung Personal je Wochentag und Tageszeit; schwache Felder (unter der Schwelle der API) tragen „schwach“ im Text.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WeekdayLoadRow> LoadByWeekday { get; private set; } = [];

    /// <summary>„Schwach: unter 50 % ausgelastet, Lücken für Aktionen.“</summary>
    [ObservableProperty]
    public partial string? WeakLoadInfo { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ReportRow> Rooms { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ReportRow> Channels { get; private set; } = [];

    /// <summary>Beste Gäste und abwandernde Stammgäste; leer ohne das Recht, Kunden zu sehen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReportRow> TopGuests { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ReportRow> LapsingGuests { get; private set; } = [];

    /// <summary>Gebuchter Stand der nächsten vier Wochen; <c>ForecastInfo</c> nennt den Stand („Stand heute, 26.11.2026“).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReportRow> Forecast { get; private set; } = [];

    [ObservableProperty]
    public partial string? ForecastInfo { get; private set; }

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
        var comparison = Comparison;
        var path = ReportPath(location, from, until, comparison);
        var result = await Api.GetAsync<ManagementReportDto>(path).ConfigureAwait(true);
        if (version != Version)
        {
            return;
        }

        if (result.Value is not { } report)
        {
            LoadError = result.Error ?? "Der Bericht konnte nicht geladen werden.";
            Alerts = [];
            AiSummary = null;
            AiAnalysisAvailable = false;
            IsAiPending = false;
            Kpis = [];
            KpiGroups = [];
            Trend = [];
            RevenueByAccount = [];
            RevenueByPayment = [];
            Employees = [];
            Treatments = [];
            Promotions = [];
            LoadByWeekday = [];
            WeakLoadInfo = null;
            Rooms = [];
            Channels = [];
            TopGuests = [];
            LapsingGuests = [];
            Forecast = [];
            ForecastInfo = null;
            CompareInfo = null;
            Document = null;
            return;
        }

        Apply(report, comparison);
        Analysis.SetScope(new AnalysisScope(location.Id, from, until, comparison.Key));
        UpdateDocument(location, from, until, comparison);
        IsLoaded = true;
        await LoadBenchmarkAsync().ConfigureAwait(true);
        await LoadMailSubscriptionAsync().ConfigureAwait(true);

        // Den KI-Kommentar schreibt der Server im Hintergrund; solange er aussteht, fragt der Reiter nach.
        for (var attempt = 1; report.AiSummaryPending && attempt <= AiAttempts; attempt++)
        {
            await Task.Delay(PollInterval, Time).ConfigureAwait(true);
            if (version != Version)
            {
                return;
            }

            var again = await Api.GetAsync<ManagementReportDto>(path).ConfigureAwait(true);
            if (version != Version || again.Value is not { } next)
            {
                return;
            }

            report = next;
            Apply(report, comparison);
            UpdateDocument(location, from, until, comparison);
        }
    }

    partial void OnFromChanged(DateOnly? value) => RangeEdited();

    partial void OnUntilChanged(DateOnly? value) => RangeEdited();

    partial void OnSelectedComparisonChanged(CompareChoice? value) => ReloadAfterRangeChange();

    partial void OnSelectedQuickRangeChanged(QuickRange? value)
    {
        if (_applyingQuickRange || value is null)
        {
            return;
        }

        _applyingQuickRange = true;
        _rangeIsSet = true;
        (From, Until) = value.Range(Today());
        _applyingQuickRange = false;
        ReloadAfterRangeChange();
    }

    private void RangeEdited()
    {
        if (_applyingQuickRange)
        {
            return;
        }

        if (SelectedQuickRange is { } quick && quick.Range(Today()) != (From ?? default, Until ?? default))
        {
            SelectedQuickRange = null;
        }

        ReloadAfterRangeChange();
    }

    private static string ReportPath(LocationDto location, DateOnly from, DateOnly until, CompareChoice comparison) =>
        $"api/reports/locations/{location.Id}/management?from={Iso(from)}&until={Iso(until)}{(comparison.IsDefault ? "" : $"&compare={comparison.Key}")}";

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

    private void Apply(ManagementReportDto report, CompareChoice comparison)
    {
        CompareInfo = report.CompareFrom is { } compareFrom && report.CompareUntil is { } compareUntil
            ? $"Vergleich: {comparison.Label} · {compareFrom:dd.MM.yyyy} bis {compareUntil:dd.MM.yyyy}"
            : null;
        AmountsHidden = report.AmountsHidden;
        Alerts = report.Alerts;
        AiSummary = report.AiSummary;
        AiNotice = report.AiNotice;
        IsAiPending = report.AiSummaryPending;
        AiAnalysisAvailable = report.AiAnalysisAvailable;
        Kpis = [.. report.Kpis.Select(k => KpiTile(k, comparison))];
        KpiGroups = [.. report.Kpis.Select((k, i) => (Kpi: k, Tile: Kpis[i])).GroupBy(k => k.Kpi.Group ?? "Kennzahlen")
            .Select(g => new KpiGroupRow(g.Key, [.. g.Select(k => k.Tile)]))];
        Trend = report.Trend ?? [];
        RevenueByAccount = [.. report.RevenueByAccount.Select(r => new RevenueRow(r.CostCenter is { } center ? $"{r.Label} ({center})" : r.Label, Money.Format(r.Gross), $"netto {Money.Format(r.Net)}"))];
        RevenueByPayment = [.. report.RevenueByPayment.Select(r => new RevenueRow(r.Label, Money.Format(r.Gross), ""))];
        Employees = [.. report.Employees.Select(e => new EmployeeKpiRow(
            e.Name, Number(e.ShiftHours, 1), Number(e.TreatmentHours, 1), $"{e.Utilization} %", e.RevenuePerHour is { } revenue ? Money.Format(revenue) : "–"))];
        Treatments = [.. report.Treatments.Select(t => new TreatmentKpiRow(
            t.Name, Number(t.Appointments), t.NetRevenue is { } net ? Money.Format(net) : "–", t.MarginPerAppointment is { } margin ? Money.Format(margin) : "–", t.MarginPerAppointment < 0))];
        Promotions = [.. (report.Promotions ?? []).Select(p => new PromotionKpiRow(p.Name, Number(p.Positions), Number(p.Guests), Number(p.NewGuests),
            p.Discount is { } discount ? Money.Format(discount) : "–", p.Revenue is { } revenue ? Money.Format(revenue) : "–"))];
        var weakBelow = report.WeakLoadBelow;
        LoadByWeekday = [.. (report.LoadByWeekday ?? []).Select(d =>
        {
            var day = German.DateTimeFormat.GetDayName(d.Day);
            return new WeekdayLoadRow(day, Cell(day, "Vormittag", d.Morning, weakBelow), Cell(day, "Mittag", d.Midday, weakBelow),
                Cell(day, "Nachmittag", d.Afternoon, weakBelow), Cell(day, "Abend", d.Evening, weakBelow));
        })];
        WeakLoadInfo = weakBelow is { } below ? $"Schwach: unter {Number(below)} % ausgelastet. Das sind die Lücken für Aktionen." : null;
        Rooms = [.. (report.Rooms ?? []).Select(r => new ReportRow(r.Name, $"{r.Utilization} %", Number(r.Appointments)))];
        Channels = [.. (report.Channels ?? []).Select(c => new ReportRow(c.Label, Number(c.Appointments)))];
        TopGuests = [.. (report.TopGuests ?? []).Select(g => new ReportRow(g.Name, Number(g.Visits), g.Spent is { } spent ? Money.Format(spent) : "–"))];
        LapsingGuests = [.. (report.LapsingGuests ?? []).Select(g => new ReportRow(g.Name, StockFormat.Date(g.LastVisit), Number(g.Visits)))];
        Forecast = [.. (report.Forecast ?? []).Select(w => new ReportRow($"{w.From:dd.MM.}–{w.Until:dd.MM.}", Number(w.Appointments),
            w.BookedRevenue is { } revenue ? Money.Format(revenue) : "–", $"{Number(w.StaffUtilization, 1)} %"))];
        ForecastInfo = report.ForecastAsOf is { } asOf ? $"Stand heute, {StockFormat.Date(asOf)}: nur, was schon gebucht ist." : null;
    }

    /// <summary>„33,3 %“, unter der Schwelle „33,3 % · schwach“, ohne Dienst „–“; der Screenreader hört Tag, Tageszeit und Wert.</summary>
    private static LoadCell Cell(string day, string daypart, decimal? value, decimal? weakBelow)
    {
        if (value is not { } load)
        {
            return new LoadCell("–", false, $"{day} {daypart}: kein Dienst");
        }

        var weak = load < weakBelow;
        var text = $"{Number(load, 1)} %{(weak ? " · schwach" : "")}";
        return new LoadCell(text, weak, $"{day} {daypart}: {text.Replace(" · ", ", ", StringComparison.Ordinal)}");
    }

    /// <summary>
    /// Kachel mit Veränderung zum Vergleich: Quoten (Einheit %) in Prozentpunkten, alles andere in Prozent des Vergleichswerts; ein Vergleichswert 0 hat keine Prozent.
    /// Der Haken (besser) oder das Warnzeichen (schlechter) kommt nur bei Kennzahlen, deren Richtung die API kennt (<c>HigherIsBetter</c>); gleiche Werte bleiben ohne Zeichen.
    /// </summary>
    private static KpiRow KpiTile(Kpi kpi, CompareChoice comparison)
    {
        var value = KpiValue(kpi.Value, kpi.Unit);
        if (kpi.Previous is not { } previous)
        {
            return new KpiRow(kpi.Label, value, null);
        }

        var shown = $"{comparison.Noun} {KpiValue(previous, kpi.Unit)}";
        var delta = kpi.Value - previous;
        var points = kpi.Unit == "%";
        var size = points ? Math.Abs(delta) : previous == 0 ? 0 : Math.Abs(delta / previous * 100);
        var digits = size < 10 ? 1 : 0;
        var same = delta == 0 || ((previous != 0 || points) && Math.Round(size, digits) == 0);
        if (same)
        {
            return new KpiRow(kpi.Label, value, shown, "→ unverändert", StatTileState.None, $"unverändert gegenüber {comparison.Dative}");
        }

        var up = delta > 0;
        var arrow = up ? "↑" : "↓";
        var state = kpi.HigherIsBetter is { } higherIsBetter ? up == higherIsBetter ? StatTileState.Ok : StatTileState.Warn : StatTileState.None;
        if (!points && previous == 0)
        {
            return new KpiRow(kpi.Label, value, shown, $"{arrow} von 0", state, $"{(up ? "gestiegen" : "gesunken")}, {comparison.Noun} war 0");
        }

        var amount = points ? $"{Number(size, 1)} Prozentpunkte" : $"{Number(size, digits)} %";
        return new KpiRow(kpi.Label, value, shown, $"{arrow} {amount}", state, $"{amount} {(up ? "über" : "unter")} {comparison.Dative}");
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

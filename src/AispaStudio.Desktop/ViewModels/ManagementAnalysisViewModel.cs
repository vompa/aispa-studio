using System.Text;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Schwerpunkt der KI-Auswertung zur Auswahl in der Seitenleiste.</summary>
public sealed record AnalysisFocusChoice(ManagementAnalysisFocus Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Auffälligkeit: Beobachtung, mögliche Ursache (die Ansicht stellt die Pille „Vermutung“ davor) und betroffene Kennzahl. Gibt es zur Kennzahl eine Kachel im Bericht, markiert
/// <paramref name="Mark"/> sie (Chip als Knopf); sonst ist die Kennzahl nur Text.
/// </summary>
public sealed record AnalysisFindingRow(string Observation, string Cause, string Kpi, IRelayCommand? Mark = null)
{
    public bool HasTile => Mark is not null;

    public string CauseText => $"Mögliche Ursache: {Cause}";

    /// <summary>Name des Chips für Screenreader: was ein Klick tut.</summary>
    public string MarkName => $"Kachel {Kpi} im Bericht zeigen";
}

/// <summary>Maßnahme mit „Woran Sie den Erfolg sehen“.</summary>
public sealed record AnalysisMeasureRow(string Action, string SuccessSign);

/// <summary>Wofür die Auswertung gilt: Standort, Zeitraum und Vergleich des gezeigten Berichts.</summary>
public sealed record AnalysisScope(Guid LocationId, DateOnly From, DateOnly Until, string Compare);

public enum AnalysisState
{
    Idle,
    Running,
    Done,
    Failed,
}

/// <summary>
/// Seitenleiste „Mit KI auswerten“ im Management-Bericht (T-096 E2, ADR 0066): Schwerpunkt und eigene Frage wählen, „Auswerten“ legt die Auswertung im Server an
/// (<c>POST …/management/analysis</c>), danach fragt die Leiste wie beim KI-Kommentar nach, bis sie fertig oder gescheitert ist. Die Zahlen rechnet nie die KI; jede
/// Ausgabe trägt Pille und Hinweis. „In den Bericht übernehmen“ merkt sich die Auswertung, der Bericht hängt sie dann an PDF, Word und Excel. Wechselt der Zeitraum,
/// der Vergleich oder der Standort, gilt die Auswertung nicht mehr und die Leiste beginnt neu.
/// </summary>
public sealed partial class ManagementAnalysisViewModel(IWorkstationApi api, TimeProvider time) : ObservableObject
{
    public const int MaxQuestionLength = 300;

    public const string IntroText = "Die KI liest die Zahlen dieses Berichts und schreibt Kernaussagen, Auffälligkeiten und Maßnahmen. Sie rechnet nichts; maßgeblich sind die Zahlen.";

    public const string QuestionHint = "Die Frage ohne Zahlen stellen. Die KI darf nur Zahlen aus dem Bericht nennen; übernimmt sie eine Zahl aus Ihrer Frage, scheitert die Auswertung.";

    public const string RunningText = "Die KI wertet die Zahlen des Berichts aus …";

    public const string FailedText = "Die KI war nicht erreichbar oder hat eine Zahl genannt, die nicht im Bericht steht. Erneut versuchen oder die Zahlen im Bericht selbst lesen.";

    public const string TooSlowText = "Die Auswertung dauert länger als üblich. Erneut versuchen oder die Zahlen im Bericht selbst lesen.";

    public const string AdoptedText = "Im Bericht übernommen: PDF, Word und Excel enthalten die Auswertung mit dem Hinweis der KI.";

    public const string RunningHint = "Die Auswertung läuft schon.";

    /// <summary>So oft fragt die Leiste nach, solange die Auswertung läuft (mit <see cref="PollDelay"/> rund zwei Minuten).</summary>
    private const int Attempts = 60;

    public static IReadOnlyList<AnalysisFocusChoice> FocusChoices { get; } =
    [
        new(ManagementAnalysisFocus.Overall, "Gesamt"),
        new(ManagementAnalysisFocus.RevenueAndProfit, "Umsatz und Ertrag"),
        new(ManagementAnalysisFocus.UtilizationAndRoster, "Auslastung und Dienstplan"),
        new(ManagementAnalysisFocus.GuestsAndLoyalty, "Gäste und Bindung"),
        new(ManagementAnalysisFocus.OfferAndPrices, "Angebot und Preise"),
    ];

    private AnalysisScope? _scope;
    private int _run;

    /// <summary>Abstand zwischen zwei Nachfragen; Tests setzen einen kurzen.</summary>
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Läuft, wenn eine Auswertung übernommen oder wieder herausgenommen wird; der Bericht baut dann seine Dateipfade neu.</summary>
    public Action? AdoptedChanged { get; set; }

    /// <summary>Der Bericht nennt die Beschriftung der Kachel zu einer Kennzahl der KI, oder null, wenn es keine gibt.</summary>
    public Func<string, string?>? FindTile { get; init; }

    /// <summary>Der Bericht markiert die Kachel mit dieser Beschriftung und rollt zu ihr.</summary>
    public Action<string>? MarkTile { get; init; }

    /// <summary>„Management-Bericht · 01.10.–19.10.2026“: Untertitel im Kopf der Leiste.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = ReportName;

    private const string ReportName = "Management-Bericht";

    /// <summary>
    /// Schwerpunkt, Frage und Hinweis stehen offen, bis eine Auswertung fertig ist; danach steht nur die Zeile „KI · Schwerpunkt … · Ändern“, und das Ergebnis bekommt die Höhe der Leiste.
    /// „Ändern“ klappt das Formular wieder auf.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormCollapsed))]
    public partial bool IsFormOpen { get; private set; } = true;

    public bool IsFormCollapsed => !IsFormOpen;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial AnalysisFocusChoice SelectedFocus { get; set; } = FocusChoices[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuestionCount))]
    public partial string? Question { get; set; }

    /// <summary>„120 von 300 Zeichen“ unter dem Feld.</summary>
    public string QuestionCount => $"{Question?.Length ?? 0} von {MaxQuestionLength} Zeichen";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsDone), nameof(IsFailed), nameof(CanAnalyze), nameof(RunLabel))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial AnalysisState State { get; private set; }

    public bool IsRunning => State == AnalysisState.Running;

    public bool IsDone => State == AnalysisState.Done;

    public bool IsFailed => State == AnalysisState.Failed;

    public bool CanAnalyze => State != AnalysisState.Running && _scope is not null;

    /// <summary>Der Knopf im Fuß: „Auswerten“, nach einer fertigen Auswertung „Neu auswerten“.</summary>
    public string RunLabel => IsDone ? "Neu auswerten" : "Auswerten";

    [ObservableProperty]
    public partial string? FailureText { get; private set; }

    /// <summary>„Schwerpunkt: Gesamt“ über dem Ergebnis, mit der eigenen Frage, wenn es eine gab.</summary>
    [ObservableProperty]
    public partial string? ResultTitle { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> KeyFindings { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<AnalysisFindingRow> Findings { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<AnalysisMeasureRow> Measures { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> NotShown { get; private set; } = [];

    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>„Kopiert.“ nach dem Kopieren, für den Screenreader als Live-Text.</summary>
    [ObservableProperty]
    public partial string? CopyStatus { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdopted), nameof(CanAdopt))]
    public partial Guid? AnalysisId { get; private set; }

    /// <summary>Die in den Bericht übernommene Auswertung; PDF, Word und Excel bekommen sie als <c>analysisId</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdopted), nameof(CanAdopt))]
    public partial Guid? AdoptedId { get; private set; }

    public bool IsAdopted => AdoptedId is not null;

    public bool CanAdopt => IsDone && AnalysisId is not null && AdoptedId != AnalysisId;

    partial void OnStateChanged(AnalysisState value) => OnPropertyChanged(nameof(CanAdopt));

    /// <summary>Der Bericht meldet, wofür er gerade steht. Ändert sich Standort, Zeitraum oder Vergleich, gilt die alte Auswertung nicht mehr.</summary>
    public void SetScope(AnalysisScope scope)
    {
        if (scope == _scope)
        {
            return;
        }

        _scope = scope;
        _run++;
        State = AnalysisState.Idle;
        Subtitle = $"{ReportName} · {Period(scope.From, scope.Until)}";
        IsFormOpen = true;
        Clear();
        if (AdoptedId is not null)
        {
            AdoptedId = null;
            AdoptedChanged?.Invoke();
        }

        OnPropertyChanged(nameof(CanAnalyze));
        AnalyzeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Open() => IsOpen = true;

    /// <summary>„Ändern“ in der eingeklappten Zeile: Schwerpunkt und Frage wieder zeigen.</summary>
    [RelayCommand]
    private void Edit() => IsFormOpen = true;

    /// <summary>„01.10.–19.10.2026“, über den Jahreswechsel „01.12.2025–19.01.2026“.</summary>
    private static string Period(DateOnly from, DateOnly until) => from.Year == until.Year
        ? $"{from:dd.MM.}–{until:dd.MM.yyyy}"
        : $"{from:dd.MM.yyyy}–{until:dd.MM.yyyy}";

    /// <summary>„Auswerten“ und „Erneut versuchen“: legt an (oder bekommt die gespeicherte Auswertung zu denselben Zahlen) und fragt nach, solange sie läuft.</summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        if (_scope is not { } scope)
        {
            return;
        }

        var run = ++_run;
        Clear();
        State = AnalysisState.Running;
        var question = string.IsNullOrWhiteSpace(Question) ? null : Question.Trim();
        var root = $"api/reports/locations/{scope.LocationId}/management/analysis";
        var created = await api.PostAsync<ManagementAnalysisDto>(root,
            new ManagementAnalysisRequest(scope.From, scope.Until, SelectedFocus.Value, question, scope.Compare)).ConfigureAwait(true);
        if (run != _run)
        {
            return;
        }

        if (created.Value is not { } analysis)
        {
            Fail(created.Error ?? FailedText);
            return;
        }

        for (var attempt = 1; analysis.Status == ManagementAnalysisStatus.Pending && attempt <= Attempts; attempt++)
        {
            await Task.Delay(PollDelay, time).ConfigureAwait(true);
            var next = await api.GetAsync<ManagementAnalysisDto>($"{root}/{analysis.Id}").ConfigureAwait(true);
            if (run != _run)
            {
                return;
            }

            analysis = next.Value ?? analysis;
        }

        switch (analysis)
        {
            case { Status: ManagementAnalysisStatus.Done, Result: { } result }:
                Show(analysis, result);
                break;
            case { Status: ManagementAnalysisStatus.Pending }:
                Fail(TooSlowText);
                break;
            default:
                Fail(FailedText);
                break;
        }
    }

    [RelayCommand]
    private void Adopt()
    {
        if (CanAdopt)
        {
            AdoptedId = AnalysisId;
            AdoptedChanged?.Invoke();
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (AdoptedId is not null)
        {
            AdoptedId = null;
            AdoptedChanged?.Invoke();
        }
    }

    /// <summary>Die Auswertung als Text mit Überschriften und dem Hinweis, für „Kopieren“.</summary>
    public string CopyText
    {
        get
        {
            var text = new StringBuilder();
            text.AppendLine("KI-Auswertung, " + ResultTitle);
            Part(text, "Kernaussagen", KeyFindings.Select(k => $"- {k}"));
            Part(text, "Auffälligkeiten", Findings.Select(f =>
                $"- {f.Observation}{Environment.NewLine}  Mögliche Ursache (Vermutung): {f.Cause}{Environment.NewLine}  Kennzahl: {f.Kpi}"));
            Part(text, "Maßnahmen", Measures.Select((m, i) => $"{i + 1}. {m.Action}{Environment.NewLine}   {m.SuccessSign}"));
            Part(text, "Was die Zahlen nicht zeigen", NotShown.Select(n => $"- {n}"));
            text.AppendLine();
            text.Append(Notice);
            return text.ToString();
        }
    }

    /// <summary>Die Ansicht hat <see cref="CopyText"/> in die Zwischenablage gelegt (oder es ging nicht).</summary>
    public void MarkCopied(bool ok) => CopyStatus = ok ? "Kopiert." : "Kopieren nicht möglich.";

    private static void Part(StringBuilder text, string title, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(title);
        list.ForEach(line => text.AppendLine(line));
    }

    private void Show(ManagementAnalysisDto analysis, ManagementAnalysisResult result)
    {
        ResultTitle = $"Schwerpunkt {FocusChoices.First(f => f.Value == analysis.Focus).Label}{(analysis.Question is { } q ? $" · Frage: {q}" : "")}";
        KeyFindings = result.KeyFindings;
        Findings = [.. result.Findings.Select(f => new AnalysisFindingRow(f.Observation, f.PossibleCause, f.Kpi, MarkFor(f.Kpi)))];
        Measures = [.. result.Measures.Select(m => new AnalysisMeasureRow(m.Action, $"Woran Sie den Erfolg sehen: {m.SuccessSign}"))];
        NotShown = result.NotShown;
        Notice = analysis.Notice;
        AnalysisId = analysis.Id;
        State = AnalysisState.Done;
        IsFormOpen = false;
    }

    /// <summary>Der Befehl des Kennzahl-Chips, wenn der Bericht eine Kachel dazu hat; sonst keiner.</summary>
    private RelayCommand? MarkFor(string kpi) => FindTile?.Invoke(kpi) is { } tile && MarkTile is { } mark ? new RelayCommand(() => mark(tile)) : null;

    private void Fail(string text)
    {
        FailureText = text;
        State = AnalysisState.Failed;
    }

    private void Clear()
    {
        FailureText = null;
        ResultTitle = null;
        KeyFindings = [];
        Findings = [];
        Measures = [];
        NotShown = [];
        Notice = null;
        CopyStatus = null;
        AnalysisId = null;
    }
}

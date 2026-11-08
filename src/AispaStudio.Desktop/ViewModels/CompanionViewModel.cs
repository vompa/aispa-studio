using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „KI und Hilfe“ rechts: ein ruhiger Knopf mit dem AISPA-Zeichen unten rechts, Klick oder Strg+Leertaste zeigt die Erklärung zur offenen Seite
/// und den KI-Assistenten. Im Kalender ist das ein Reiter der Seitenleiste (neben Warteliste, Meldungen …; Schließen lässt die übrigen Reiter stehen),
/// auf den übrigen Seiten, die keine Reiterschiene haben, die Leiste der Shell. Sie meldet sich nie von selbst; offen oder zu merkt sich die App.
/// </summary>
[AiOutput(AiFunctions.Assistant)]
public sealed partial class CompanionViewModel : ViewModelBase, ISidePanel, IDisposable
{
    private readonly IWorkstationApi _api;
    private readonly ISettingsStore _store;
    private readonly Func<Guid?> _location;
    private readonly Func<string, HelpTopic?> _find;
    private readonly TimeProvider _time;
    private readonly IUnansweredQuestionStore? _questions;
    private string? _page;

    /// <param name="questions">Ablage der Fragen ohne Treffer (Slice 56); ohne Angabe <see cref="UnansweredQuestionLog.Current"/>.</param>
    public CompanionViewModel(IWorkstationApi api, ISettingsStore store, bool canAsk, AssistantAudience audience, Func<Guid?>? location = null,
        Func<string, HelpTopic?>? find = null, TimeProvider? time = null, IUnansweredQuestionStore? questions = null)
    {
        _api = api;
        _time = time ?? TimeProvider.System;
        _questions = questions;
        Presence = new PanelPresence(_time);
        _store = store;
        _location = location ?? (() => null);
        _find = find ?? HelpContent.Find;
        CanAsk = canAsk;
        Examples = audience switch
        {
            AssistantAudience.Management => ["Wie lange haben wir heute offen?", "Top-Behandlungen diesen Monat", "Wie war der Umsatz letzte Woche?", "Wie lade ich eine neue Person ein?"],
            AssistantAudience.Therapist => ["Wer arbeitet morgen?", "Was steht heute an?", "Welcher Raum ist jetzt frei?", "Wie hake ich die Vorbereitung ab?"],
            _ => ["Wie lange haben wir heute offen?", "Wer arbeitet morgen?", "Wann ist morgen Nachmittag eine Massage frei?", "Welche Rabatte oder Aktionen laufen gerade?", "Top-Behandlungen diesen Monat"],
        };
        IsOpen = store.Load().CompanionOpen;
        Presence.Update(IsOpen && !InCalendar);
    }

    /// <summary>Wann die Leiste der Shell im Layout steht und ob sie sich bewegt (Maus) oder nicht (Kurzbefehl); im Kalender gilt die der Seitenleiste dort.</summary>
    public PanelPresence Presence { get; }

    /// <summary>Der Assistent geht auf (<c>true</c>) oder zu, durch Klick, Esc oder Strg+Leertaste; die Ansicht setzt den Fokus.</summary>
    public event Action<bool>? Toggled;

    private bool _showingPage;

    /// <summary>
    /// Die offene Seite ist der Kalender: der Assistent steht dort als Reiter der Seitenleiste, die Leiste der Shell bleibt zu.
    /// Die Seiten ohne Reiterschiene (Kunden, Kassa, Verwaltung, Hilfe …) zeigen ihn in der Leiste der Shell.
    /// </summary>
    public bool InCalendar
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ShellContent));
                OnPropertyChanged(nameof(ShellOpen));
                Presence.Snap(IsOpen && !value);
            }
        }
    }

    /// <summary>Die Leiste der Shell ist offen: der Assistent ist offen und die Seite hat keine Reiterschiene.</summary>
    public bool ShellOpen => IsOpen && !InCalendar;

    /// <summary>Der Inhalt für die Leiste der Shell; im Kalender <c>null</c>, damit der Assistent nur einmal aufgebaut ist (dort steht er im Reiter).</summary>
    public CompanionViewModel? ShellContent => InCalendar ? null : this;

    /// <summary>KI-Assistent eingeschaltet und für diese Person erlaubt; sonst zeigt die Leiste nur die Erklärung.</summary>
    public bool CanAsk { get; }

    /// <summary>Was gerade offen ist (Termin oder Kundenkarte): die API löst „sie“, „er“ und „der Gast“ darauf auf (Ä315).</summary>
    public Func<AssistantReference?>? ReferenceSource { get; set; }

    /// <summary>Der Gast der letzten Antwort über einen Gast; nur in dieser Leiste und nur im Speicher.</summary>
    private Guid? _lastGuest;

    public IReadOnlyList<string> Examples { get; private set; }

    /// <summary>
    /// Zugang ohne Kalender (Backoffice, ADR 0070): der Assistent beantwortet nur Auswertungen und Fragen zur Bedienung; die Beispiele nennen deshalb nur diese.
    /// </summary>
    public bool ReportsOnly
    {
        get => _reportsOnly;
        init
        {
            _reportsOnly = value;
            if (value)
            {
                Examples = ["Top-Behandlungen diesen Monat", "Umsatz nach Behandlung im letzten Monat", "Wie viele Nicht-erschienen hatten wir?", "Wie lade ich einen Export?"];
            }
        }
    }

    private readonly bool _reportsOnly;

    /// <summary>Der Assistent ist zu sehen: als gewählter Reiter im Kalender, sonst in der Leiste der Shell.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShellOpen))]
    public partial bool IsOpen { get; set; }

    partial void OnIsOpenChanged(bool value)
    {
        if (!_showingPage)
        {
            // Offen oder zu gilt je Seite: wer auf „Kassa“ die Hilfe zu lässt, findet sie dort wieder zu.
            var settings = _store.Load();
            var pages = new Dictionary<string, bool>(settings.CompanionPages ?? new Dictionary<string, bool>());
            if (_page is not null)
            {
                pages[_page] = value;
            }

            _store.Save(settings with { CompanionOpen = value, CompanionPages = pages });

            // Zuerst die Ansicht fragen, dann ausblenden: sie muss noch wissen, ob der Fokus in der Leiste war.
            Toggled?.Invoke(value);

            // Zurückgesetzt wird nur beim ausdrücklichen Schließen (<see cref="Dismiss"/>): im Kalender schaltet auch der Wechsel des Reiters „offen“ aus,
            // dann bleibt der Stand des Assistenten im Hintergrund erhalten.
            if (value)
            {
                LoadHistory();
            }
        }

        Presence.Update(value && !InCalendar);
    }

    // ISidePanel: der Assistent als Reiter der Seitenleiste im Kalender (Design-Brief 5.28).
    public string PanelTitle => "KI und Hilfe";

    public string? PanelSubtitle => null;

    public PanelKind PanelKind => PanelKind.Assistant;

    public string PanelIcon => AispaStudio.Desktop.Controls.Icons.Logo;

    public string PanelTabText => "KI und Hilfe";

    public string PanelCloseTip => "Schließen (Esc oder Strg+Leertaste)";

    /// <summary>Hilfethema der offenen Seite; ohne Thema bietet die Leiste alle Hilfethemen an.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTopic))]
    public partial HelpTopic? Topic { get; private set; }

    public bool HasTopic => Topic is not null;

    /// <summary>Absätze vor dem ersten Abschnitt: was die Seite tut.</summary>
    public ObservableCollection<HelpParagraph> Intro { get; } = [];

    /// <summary>Seite gewechselt, z. B. „kassa“: die Erklärung kommt aus dem Seitenthema der Hilfe („seite-kassa“).</summary>
    public void ShowPage(string page)
    {
        _page = page;
        OnPropertyChanged(nameof(ShowsPageSection));
        if (_store.Load().CompanionPages is { } pages && pages.TryGetValue(page, out var open) && open != IsOpen)
        {
            _showingPage = true;
            IsOpen = open;
            _showingPage = false;
        }

        Topic = _find($"seite-{page}");
        Intro.Clear();
        foreach (var block in Topic?.Blocks.TakeWhile(b => b is not HelpHeading) ?? [])
        {
            if (block is HelpParagraph p)
            {
                Intro.Add(p);
            }
        }
    }

    [RelayCommand]
    public void Toggle()
    {
        if (IsOpen)
        {
            Dismiss();
        }
        else
        {
            IsOpen = true;
        }
    }

    [RelayCommand]
    private void Close() => Dismiss();

    [RelayCommand]
    private void OpenHelp() => HelpContent.Open(Topic?.Id);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    public partial string Question { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    [NotifyPropertyChangedFor(nameof(ShowsAnswer))]
    public partial bool IsAsking { get; private set; }

    /// <summary>Die Frage der angezeigten Antwort (Kopf der Karte).</summary>
    [ObservableProperty]
    public partial string? Asked { get; private set; }

    /// <summary>Die Frage, auf deren Antwort die Leiste gerade wartet; steht als Kopf über dem Ladezustand (Ä316).</summary>
    [ObservableProperty]
    public partial string? Pending { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnswer), nameof(FirstHelp), nameof(MoreHelp), nameof(Suggestions), nameof(HelpAction))]
    [NotifyPropertyChangedFor(nameof(Block), nameof(BlockTable), nameof(SourceText), nameof(LinkLabel), nameof(CopyText), nameof(HasBlockLink), nameof(HasCopy))]
    [NotifyPropertyChangedFor(nameof(FollowUps), nameof(WhyText), nameof(WhyTitle), nameof(HasBlockActions), nameof(Gaps))]
    [NotifyPropertyChangedFor(nameof(Rows), nameof(HasRows), nameof(HasMoreRows), nameof(ShowAllText), nameof(ShowsAnswer))]
    public partial AssistantAnswer? Answer { get; private set; }

    public bool HasAnswer => Answer is not null;

    /// <summary>Die Karte mit der Antwort steht da, außer während eine neue Frage läuft; dann steht dort die neue Frage mit dem Ladezustand.</summary>
    public bool ShowsAnswer => HasAnswer && !IsAsking;

    /// <summary>„Wie mache ich …?“: der beste Treffer der Hilfe, mit Schritten und Bildern gezeigt.</summary>
    public AssistantHelpLink? FirstHelp => Answer?.Help is [var first, ..] ? first : null;

    /// <summary>Weitere Treffer als Verweis in die Hilfe.</summary>
    public IReadOnlyList<AssistantHelpLink> MoreHelp => Answer?.Help.Skip(1).ToList() ?? [];

    [RelayCommand]
    private static void OpenHelpLink(AssistantHelpLink link) => HelpContent.Open(link.Topic, link.Section);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    private bool CanAskNow() => CanAsk && !IsAsking && !string.IsNullOrWhiteSpace(Question);

    [RelayCommand(CanExecute = nameof(CanAskNow), AllowConcurrentExecutions = true)]
    private Task AskAsync() => AskAsync(null);

    /// <summary>
    /// Stellt die Frage mit dem Bezug auf das, was offen ist, und dem zuletzt genannten Gast; <paramref name="guest"/> ist ein aus einer Auswahl gewählter Gast.
    /// Die Frage bleibt im Feld, bis die Antwort angezeigt ist (Ä316); ein Fehler oder eine leere Antwort zeigt eine Meldung mit „Erneut fragen“, die vorige Antwort bleibt.
    /// </summary>
    private async Task AskAsync(Guid? guest, bool remember = true)
    {
        var question = Question.Trim();
        var generation = _generation;
        if (remember)
        {
            Remember(question);
        }

        var shown = ReferenceSource?.Invoke();
        var reference = shown is null && _lastGuest is null ? null : new AssistantReference(shown?.AppointmentId, shown?.CustomerId, _lastGuest);
        Pending = question;
        IsAsking = true;
        Error = null;
        try
        {
            var result = await _api.PostAsync<AssistantAnswer>("api/assistant", new AssistantQuestion(question, _location(), _page, HelpContent.Surface, reference, guest));
            if (generation != _generation)
            {
                // Das Panel wurde inzwischen geschlossen oder neu begonnen: die späte Antwort gehört nicht mehr dazu.
                return;
            }

            if (result.Ok && result.Value is { } answer && !IsEmpty(answer))
            {
                Asked = question;
                Pick = null;
                Guest = null;
                BookedText = null;
                CopiedText = null;
                SavedNotice = null;
                _awaitingSave = false;
                ShowsAllRows = false;
                Answer = answer;
                _lastGuest = answer.Block?.GuestId ?? _lastGuest;
                Question = "";
                if (answer.Unanswered is { } open)
                {
                    // Ohne Treffer: für „Offene Fragen an die Hilfe“, so wie der Server sie bereinigt hat, nie die Eingabe selbst (Slice 56).
                    (_questions ?? UnansweredQuestionLog.Current).Record(new UnansweredQuestion(open, _page, _time.GetUtcNow()));
                }
            }
            else
            {
                Error = result.Ok ? EmptyAnswer : result.Status == 0 ? "Der Assistent ist gerade nicht erreichbar." : "Der Assistent konnte die Frage gerade nicht beantworten.";
            }
        }
        finally
        {
            if (generation == _generation)
            {
                Pending = null;
                IsAsking = false;
            }
        }
    }

    /// <summary>Meldung, wenn die Antwort nichts enthält, was die Leiste zeigen könnte.</summary>
    public const string EmptyAnswer = "Der Assistent hat keine Antwort geliefert.";

    private static bool IsEmpty(AssistantAnswer answer) =>
        string.IsNullOrWhiteSpace(answer.Text) && answer.Block is null && answer.Help.Count == 0 && answer.Suggestions.Count == 0 && answer.Figures.Count == 0;

    /// <summary>Beispiel antippen stellt die Frage gleich.</summary>
    [RelayCommand]
    private Task AskExampleAsync(string example)
    {
        Question = example;
        return CanAskNow() ? AskAsync() : Task.CompletedTask;
    }
}

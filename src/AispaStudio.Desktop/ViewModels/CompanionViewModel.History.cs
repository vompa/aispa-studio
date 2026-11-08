using System.Collections.ObjectModel;
using System.ComponentModel;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Zurücksetzen und „Zuletzt gefragt“ (Slice 138, Ä347). Der Verlauf liegt nur auf diesem Gerät in den Einstellungen, gehört der angemeldeten Person,
/// gilt nur für den heutigen Kalendertag und enthält nur die Fragen, nie die Antworten.
/// </summary>
public sealed partial class CompanionViewModel
{
    /// <summary>So viele Fragen zeigt „Zuletzt gefragt“.</summary>
    public const int HistorySize = 5;

    /// <summary>Zählmarke: ein Zurücksetzen macht eine noch laufende Anfrage ungültig.</summary>
    private int _generation;

    /// <summary>Die angemeldete Person (<c>CurrentUser.Id</c>), der der Verlauf gehört; ohne sie bleibt er im Speicher.</summary>
    public string? HistoryOwner
    {
        get;
        init
        {
            field = value;
            LoadHistory();
            if (value is not null)
            {
                _ = WatchMidnightAsync();
            }
        }
    }

    /// <summary>„Neu beginnen“ ist geschehen: die Ansicht setzt den Fokus ins Fragefeld.</summary>
    public event Action? Restarted;

    /// <summary>Die letzten Fragen von heute, die neueste zuerst; doppelte stehen einmal.</summary>
    public ObservableCollection<string> RecentQuestions { get; } = [];

    public bool HasRecentQuestions => RecentQuestions.Count > 0;

    /// <summary>Der Teil zur Seite („Diese Seite“) entfällt auf der Seite „Hilfe“: dort bleibt nur der Assistent.</summary>
    public bool ShowsPageSection => _page != "hilfe";

    /// <summary>Es gibt etwas, das „Neu beginnen“ zurücksetzt.</summary>
    public bool CanReset => HasAnswer || HasError || IsBooking || IsAsking || Question.Length > 0 || BookedText is not null || SavedNotice is not null || CopiedText is not null;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Answer) or nameof(Error) or nameof(Pick) or nameof(IsAsking) or nameof(Question) or nameof(BookedText) or nameof(SavedNotice) or nameof(CopiedText))
        {
            OnPropertyChanged(nameof(CanReset));
            OnPropertyChanged(nameof(HasPanelAction));
        }
    }

    // ISidePanel: „Neu beginnen“ steht im Kopf der Leiste neben dem Kreuz (Ä359, T-018), in der Shell und im Kalender-Reiter.
    public IRelayCommand PanelAction => ResetCommand;

    public string PanelActionText => "Neu beginnen";

    public string PanelActionTip => "Frage und Antwort löschen, die Leiste bleibt offen";

    public bool HasPanelAction => CanReset;

    /// <summary>„Neu beginnen“: wie nach dem Schließen, aber das Panel bleibt offen.</summary>
    [RelayCommand]
    private void Reset()
    {
        ClearState();
        Restarted?.Invoke();
    }

    /// <summary>
    /// Ausdrückliches Schließen (Kreuz, Esc, Strg+Leertaste, Knopf unten rechts): der Assistent beginnt beim nächsten Öffnen wie beim ersten Aufruf (Ä347).
    /// Die Ansicht setzt dabei den Fokus zurück; der Wechsel des Reiters im Kalender ruft das nicht auf.
    /// </summary>
    public void Dismiss()
    {
        ClearState();
        IsOpen = false;
    }

    /// <summary>Der Reiter des Assistenten im Kalender wurde geschlossen: zurücksetzen, ohne das Panel selbst anzufassen.</summary>
    public void StartFresh() => ClearState();

    /// <summary>Eine Frage aus „Zuletzt gefragt“ noch einmal stellen: frische Antwort der API, nie eine gespeicherte.</summary>
    [RelayCommand]
    private Task AskRecentAsync(string question) => AskExampleAsync(question);

    /// <summary>Frage, Antwort, Fehler, Buchungsauswahl und gemerkte Gäste weg; eine noch laufende Antwort wird verworfen.</summary>
    private void ClearState()
    {
        _generation++;
        Question = "";
        Pending = null;
        IsAsking = false;
        IsBookingBusy = false;
        Answer = null;
        Asked = null;
        Error = null;
        Pick = null;
        Guest = null;
        BookedText = null;
        CopiedText = null;
        SavedNotice = null;
        _awaitingSave = false;
        ShowsAllRows = false;
        _lastGuest = null;
    }

    /// <summary>Der Kalendertag, von dem die Liste „Zuletzt gefragt“ im Speicher stammt.</summary>
    private DateOnly _historyDay;

    /// <summary>Höchstens so lange schläft der Zeitgeber am Stück: nach Ruhezustand oder Uhrsprung rechnet er spätestens dann neu.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromHours(1);

    /// <summary>Beendet den Zeitgeber für Mitternacht (beim Abmelden).</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Beim Abmelden: der Zeitgeber für Mitternacht hört auf.</summary>
    public void Dispose()
    {
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
        }

        _stop.Dispose();
    }

    /// <summary>
    /// Leert „Zuletzt gefragt“ um Mitternacht auch bei offener Leiste (T-029); läuft, solange die Leiste lebt. Gewartet wird bis zur nächsten
    /// lokalen Mitternacht (Offset dieses Tages, auch bei Zeitumstellung), höchstens <see cref="LongestWait"/> am Stück.
    /// </summary>
    private async Task WatchMidnightAsync()
    {
        var token = _stop.Token;
        try
        {
            while (true)
            {
                await Task.Delay(UntilMidnight(), _time, token).ConfigureAwait(true);
                ExpireHistory();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Abgemeldet: nichts mehr zu tun.
        }
    }

    private TimeSpan UntilMidnight()
    {
        var now = _time.GetUtcNow();
        var next = Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var midnight = new DateTimeOffset(next, _time.LocalTimeZone.GetUtcOffset(next));
        var wait = midnight - now + TimeSpan.FromSeconds(1);
        return wait > LongestWait ? LongestWait : wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
    }

    /// <summary>Der Verlauf ist nicht mehr von heute: aus der Liste nehmen und, wenn er dieser Person gehört, vom Gerät. Fremde Einträge bleiben.</summary>
    private void ExpireHistory()
    {
        var stored = _store.Load().AssistantHistory;
        if (stored is not null && stored.UserId == HistoryOwner && stored.Day != Today)
        {
            _store.Save(_store.Load() with { AssistantHistory = null });
        }

        if (RecentQuestions.Count > 0 && _historyDay != Today)
        {
            RecentQuestions.Clear();
            OnPropertyChanged(nameof(HasRecentQuestions));
        }
    }

    /// <summary>Beim Abmelden und beim Personenwechsel: der Verlauf ist weg, im Speicher und auf dem Gerät.</summary>
    public void ForgetHistory()
    {
        RecentQuestions.Clear();
        OnPropertyChanged(nameof(HasRecentQuestions));
        _store.Save(_store.Load() with { AssistantHistory = null });
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>Liest den Verlauf dieser Person von heute; von anderen Personen oder Tagen bleibt nichts stehen.</summary>
    private void LoadHistory()
    {
        if (HistoryOwner is null)
        {
            return;
        }

        var stored = _store.Load().AssistantHistory;
        var valid = stored is not null && stored.UserId == HistoryOwner && stored.Day == Today;
        if (stored is not null && !valid)
        {
            // Gestern oder eine andere Person: nicht nur ausblenden, auch vom Gerät nehmen.
            _store.Save(_store.Load() with { AssistantHistory = null });
        }

        var today = valid ? stored!.Questions : [];
        _historyDay = Today;
        RecentQuestions.Clear();
        foreach (var question in today.Take(HistorySize))
        {
            RecentQuestions.Add(question);
        }

        OnPropertyChanged(nameof(HasRecentQuestions));
    }

    /// <summary>Merkt eine gestellte Frage: nach oben, doppelte (ohne Rücksicht auf Groß- und Kleinschreibung) einmal, höchstens fünf.</summary>
    private void Remember(string question)
    {
        LoadHistory();
        ExpireHistory();
        _historyDay = Today;
        var known = RecentQuestions.FirstOrDefault(q => string.Equals(q, question, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
        {
            RecentQuestions.Remove(known);
        }

        RecentQuestions.Insert(0, question);
        while (RecentQuestions.Count > HistorySize)
        {
            RecentQuestions.RemoveAt(RecentQuestions.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentQuestions));
        if (HistoryOwner is { } owner)
        {
            _store.Save(_store.Load() with { AssistantHistory = new AssistantHistory(owner, Today, [.. RecentQuestions]) });
        }
    }
}

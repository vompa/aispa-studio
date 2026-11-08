using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Die Rückfrage nach einem Klick auf eine Spalte ohne Dienst („Trotzdem buchen“): eine einzelne InfoBar über dem Kalender, keine Meldung der Seitenleiste.</summary>
public sealed record OffDutyPrompt(string Message, string ActionText, RelayCommand Action);

/// <summary>
/// Meldungen des Kalenders (docs/gestaltung/meldungen.md, Schritt 16): Fehler, Warnungen und Hinweise des Tages aus Pausenprüfung, Lager, Raumpflege, Tagesstart,
/// Raumausstattung, Handbuch und Abwesenheiten als Karten der Seitenleiste „Meldungen“ (<see cref="MessagesPanelViewModel"/>), dazu der Zähler der Schaltfläche in der
/// Kopfzeile und das Symbol in den Spaltenköpfen. „Wegklicken“ blendet eine Meldung bis zum nächsten Auslöser aus (je Standort und Tag im Speicher); was in der Sitzung
/// erledigt wurde, steht blass in der Gruppe „Erledigt“. Warnungen sind nie abhakbar: sie verschwinden, wenn der Zustand behoben ist.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Wie viele erledigte Meldungen die Gruppe „Erledigt“ höchstens zeigt.</summary>
    private const int MaxDoneMessages = 10;

    /// <summary>Was die Sitzung zu einem Tag an einem Standort über Meldungen weiß: Weggeklicktes, den letzten Stand, Erledigtes und wann sie eine Meldung zuerst sah.</summary>
    private sealed class MessageState
    {
        public Dictionary<string, string> Dismissed { get; } = [];

        public Dictionary<string, MessageItem> Last { get; } = [];

        public List<MessageItem> Done { get; } = [];

        public Dictionary<string, DateTimeOffset> FirstSeen { get; } = [];
    }

    private readonly Dictionary<(Guid, DateOnly), MessageState> _messageStates = [];
    private readonly List<(string Key, string Text)> _posted = [];
    private MessagesPanelViewModel? _messages;
    private CalendarDay? _messageDay;

    /// <summary>Alle offenen Meldungen, schwerste zuerst; Weggeklicktes fehlt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageCount), nameof(HasMessages), nameof(MessagesButtonText), nameof(MessagesCompact), nameof(MessagesLabel), nameof(MessagesTip), nameof(MessagesHaveError), nameof(MessagesHaveWarning))]
    public partial IReadOnlyList<MessageItem> Messages { get; private set; } = [];

    /// <summary>In dieser Sitzung erledigte Meldungen des Tages.</summary>
    public IReadOnlyList<MessageItem> DoneMessages { get; private set; } = [];

    public int MessageCount => Messages.Count;

    public bool HasMessages => Messages.Count > 0;

    /// <summary>Beschriftung der Schaltfläche in der Kopfzeile: „Meldungen“ ohne, „Meldungen 2“ mit offenen Meldungen.</summary>
    public string MessagesButtonText => Messages.Count == 0 ? "Meldungen" : $"Meldungen {Messages.Count}";

    /// <summary>Kompakte Kopfzeile: nur die Zahl, ohne Meldungen nur das Symbol.</summary>
    public string MessagesCompact => Messages.Count == 0 ? "" : Messages.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string MessagesLabel => IsCompactHeader ? MessagesCompact : MessagesButtonText;

    public string MessagesTip => HasDayNotes
        ? $"{ChipTip(MessagesButtonText, "Hinweise und Notizen des Tages (M)")}{Environment.NewLine}{DayNotesText}"
        : ChipTip(MessagesButtonText, "Hinweise und Notizen des Tages (M)");

    /// <summary>Name der Schaltfläche für Screenreader: mit Zähler und, wenn es welche gibt, den Notizen des Tages (der Punkt allein sagt es nicht).</summary>
    public string MessagesName => HasDayNotes ? $"{MessagesButtonText}, {DayNotesText}" : MessagesButtonText;

    public bool MessagesHaveError => Messages.Any(m => m.Severity == MessageSeverity.Error);

    /// <summary>Warnung ohne Fehler: die Schaltfläche steht in der Warnfarbe (bei Fehlern in der Fehlerfarbe).</summary>
    public bool MessagesHaveWarning => !MessagesHaveError && Messages.Any(m => m.Severity == MessageSeverity.Warning);

    /// <summary>Die Seitenleiste „Meldungen“, solange sie offen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial MessagesPanelViewModel? MessagesPanel { get; private set; }

    /// <summary>Die Detailansicht einer Meldung, solange sie offen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial MessageDetailViewModel? MessageDetail { get; private set; }

    /// <summary>Die Rückfrage zu einem Klick auf eine Spalte ohne Dienst; sonst leer.</summary>
    [ObservableProperty]
    public partial OffDutyPrompt? OffDuty { get; private set; }

    /// <summary>Der Assistent beantwortet „Was tun?“ in der Detailansicht; die Shell schaltet es aus, wenn das Modul fehlt.</summary>
    public bool CanAskAssistant { get; set; } = true;

    /// <summary>Die Ansicht soll die Spalte so weit ins Bild scrollen; der Wert ist ihr Abstand vom linken Rand der Spalten.</summary>
    public event Action<double>? ColumnRevealRequested;

    /// <summary>Die Frage, ob der Klick auf eine Spalte ohne Dienst doch buchen soll, wurde mit „Trotzdem buchen“ beantwortet.</summary>
    private bool _offDutyConfirmed;

    /// <summary>
    /// Aktion der Meldung „Pause fehlt“: das Fenster öffnet für diese Person (auch aus der Raumansicht) mit den fehlenden Minuten
    /// in der ersten Lücke ihres Dienstes, in die sie passen; sonst in der längsten Lücke, sonst zu Dienstbeginn.
    /// </summary>
    private void OpenBreakFor(CalendarBreak info)
    {
        if (ByRoom)
        {
            ShowTeam();
        }

        if (_day is null || Columns.FirstOrDefault(c => c.Id == info.EmployeeId) is not { } column)
        {
            return;
        }

        var minutes = Math.Max(5, info.RequiredMinutes - info.PlannedMinutes);
        OpenNewEntry(NewEntryKind.Break, new EntryAnchor(column, BreakSlotFor(column.Id, minutes), minutes));
    }

    private DateTimeOffset BreakSlotFor(Guid employeeId, int minutes)
    {
        var shifts = _day!.Shifts.Where(s => s.EmployeeId == employeeId).OrderBy(s => s.Start).ToList();
        var from = shifts.Count > 0 ? shifts[0].Start : _dayStart;
        var until = shifts.Count > 0 ? shifts[^1].End : _dayStart.AddDays(1);
        DateTimeOffset? longest = null;
        var longestFill = 0;
        for (var at = from; at < until; at = at.AddMinutes(5))
        {
            if (DayLayout.GapAt(_day, false, employeeId, at) is not { } gap)
            {
                continue;
            }

            var fill = gap.FillMinutes(at);
            if (fill >= minutes)
            {
                return at;
            }

            if (fill > longestFill)
            {
                (longest, longestFill) = (at, fill);
            }

            at = gap.Until > at ? gap.Until.AddMinutes(-5) : at;
        }

        return longest ?? from;
    }

    private bool OfflineView => OfflineNote is not null || _api.IsOffline;

    private MessageState StateOfDay()
    {
        var key = (Location?.Id ?? Guid.Empty, Date);
        if (!_messageStates.TryGetValue(key, out var state))
        {
            _messageStates[key] = state = new MessageState();
        }

        return state;
    }

    // ---------- Öffnen ----------

    /// <summary>Schaltfläche „Meldungen“ und Taste M: öffnet die Seitenleiste, bei einem zweiten Aufruf schließt sie sie wieder.</summary>
    [RelayCommand]
    private void OpenMessages()
    {
        if (MessagesPanel is not null)
        {
            CloseActivePanel();
            return;
        }

        ShowMessagesPanel(null);
    }

    /// <summary>Symbol im Spaltenkopf: die Meldungen zu dieser Person bzw. diesem Raum.</summary>
    public void OpenMessagesFor(CalendarColumn column) =>
        ShowMessagesPanel(new MessageSubject(column.IsRoom ? MessageSubjectKind.Room : MessageSubjectKind.Person, column.Id, column.Title));

    private void ShowMessagesPanel(MessageSubject? filter)
    {
        var panel = _messages ??= new MessagesPanelViewModel(() => MessagesPanel = null, NoteActions, RequestPanelFocus);
        panel.ShowNotesOf(DayNotes);
        panel.Show(Messages, DoneMessages, OfflineView);
        panel.ApplyFilter(filter);
        ClosePanels();
        Ghost = null;
        MessagesPanel = panel;
        Rebuild();
    }

    private void OpenMessageDetail(string key)
    {
        if (Messages.Concat(DoneMessages).FirstOrDefault(m => m.Key == key) is not { } item)
        {
            return;
        }

        ClosePanels();
        Ghost = null;
        var detail = new MessageDetailViewModel(item, _api, Location?.Id, CanAskAssistant, OpenSubject, ResolveSuggestion, Operations, () => MessageDetail = null);
        MessageDetail = detail;
        Rebuild();
        _ = detail.LoadHelpAsync();
    }

    /// <summary>Link in der Detailansicht: die Spalte der Person bzw. des Raums kommt ins Bild, ein Material öffnet das Lager in der Verwaltung.</summary>
    private void OpenSubject(MessageSubject subject)
    {
        if (subject.Kind == MessageSubjectKind.Material)
        {
            AdminRequested?.Invoke("stock", null, subject.Id);
            return;
        }

        var room = subject.Kind == MessageSubjectKind.Room;
        if (ByRoom != room)
        {
            ByRoom = room;
        }

        var index = Columns.ToList().FindIndex(c => c.Id == subject.Id);
        if (index >= 0)
        {
            ColumnRevealRequested?.Invoke(DayLayout.ColumnLeft(index, Scale));
        }
    }

    /// <summary>Vorschläge des Assistenten in „Was tun?“, die der Kalender ausführen kann; gebucht wird hier nie (das macht „KI und Hilfe“).</summary>
    private DetailSuggestion? ResolveSuggestion(AssistantSuggestion s)
    {
        var day = s.Start is { } start ? DateOnly.FromDateTime(DayLayout.Local(start, _zone)) : Date;
        return s.Kind switch
        {
            AssistantSuggestionKind.OpenCustomer when s.CustomerId is { } customer && CustomerRequested is { } open =>
                new DetailSuggestion(s, "Kunde öffnen", () => open(customer)),
            AssistantSuggestionKind.OpenAppointment when s.AppointmentId is { } appointment =>
                new DetailSuggestion(s, "Im Kalender zeigen", () =>
                {
                    OpenSearchHit(new CalendarSearchHit("", "", day, appointment));
                    return Task.CompletedTask;
                }),
            AssistantSuggestionKind.ShowCustomerAppointments when s.CustomerId is { } owner =>
                new DetailSuggestion(s, "Alle Termine zeigen", () => FilterByCustomerAsync(owner)),
            AssistantSuggestionKind.SearchFreeTime when CanBook =>
                new DetailSuggestion(s, "Suche öffnen", () => ShowFreeTimeAsync(new FreeTimePrefill(s.TreatmentId, s.EmployeeId, s.RoomId), s.Start is null ? null : day)),
            _ => null,
        };
    }

    // ---------- Aufbau ----------

    /// <summary>Neuer Tag oder Standort: Hinweise zum alten Tag fallen weg, Weggeklicktes bleibt je Tag im Speicher.</summary>
    private void ResetMessages()
    {
        _posted.Clear();
        OffDuty = null;
        _messageDay = null;
        RefreshMessages();
    }

    partial void OnErrorChanged(string? value) => RefreshMessages();

    /// <summary>Hinweise aus der Antwort auf Buchen oder Einchecken (Lagergrenze, Tagesstart, Raumpflege): stehen als Warnung, bis sie weggeklickt werden.</summary>
    private void PostWarning(string text)
    {
        var key = $"posted:{text}";
        StateOfDay().Dismissed.Remove(key);
        if (_posted.All(p => p.Key != key))
        {
            _posted.Add((key, text));
        }

        RefreshMessages();
    }

    /// <summary>Wegklicken: die Meldung bleibt weg, bis sich ihr Auslöser ändert.</summary>
    private void DismissMessage(string key, string trigger)
    {
        StateOfDay().Dismissed[key] = trigger;
        RefreshMessages();
    }

    /// <summary>
    /// Klick auf eine Spalte ohne Dienst: statt sofort zu buchen fragt der Kalender nach („Trotzdem buchen“). Ob der Server dann bucht, entscheidet
    /// seine Belegungsprüfung; sie verlangt für eine gewünschte Person einen Dienst.
    /// </summary>
    private void AskOffDuty(CalendarColumn column, Action proceed) =>
        OffDuty = new OffDutyPrompt($"{column.Title} hat an diesem Tag keinen Dienst hier ({column.Subtitle}). Trotzdem einen Termin eintragen?", "Trotzdem buchen",
            new RelayCommand(() =>
            {
                OffDuty = null;
                proceed();
            }));

    [RelayCommand]
    private void DismissOffDuty() => OffDuty = null;

    private void RefreshMessages()
    {
        var state = StateOfDay();
        var current = BuildMessages(state);
        var visible = current.Where(m => !state.Dismissed.TryGetValue(m.Key, out var trigger) || trigger != m.Trigger).OrderByDescending(m => m.Severity).ToList();
        NoteDone(state, current);
        state.Last.Clear();
        foreach (var message in current)
        {
            state.Last[message.Key] = message;
        }

        Messages = visible;
        DoneMessages = [.. state.Done];
        _messages?.Show(visible, DoneMessages, OfflineView);
        MessageDetail?.Update(visible.Concat(DoneMessages).FirstOrDefault(m => m.Key == MessageDetail.Item.Key));
        ApplyAlerts();
    }

    /// <summary>Meldungen, die seit dem letzten Stand verschwunden sind, weil ihr Zustand behoben ist, stehen als erledigt da.</summary>
    private void NoteDone(MessageState state, IReadOnlyList<MessageItem> current)
    {
        var keys = current.Select(m => m.Key).ToHashSet();
        state.Done.RemoveAll(m => keys.Contains(m.Key));
        var now = _time.GetUtcNow();
        foreach (var gone in state.Last.Values.Where(m => !keys.Contains(m.Key) && IsTracked(m) && !state.Dismissed.ContainsKey(m.Key)))
        {
            state.Done.Insert(0, gone.AsDone($"erledigt um {DayLayout.Local(now, _zone):HH:mm}"));
        }

        if (state.Done.Count > MaxDoneMessages)
        {
            state.Done.RemoveRange(MaxDoneMessages, state.Done.Count - MaxDoneMessages);
        }
    }

    /// <summary>Fehler, Hinweise des Handbuchs und der Buchung enden nicht „erledigt“; Meldungen aus den Betriebsdaten zählen nur, solange diese geladen sind.</summary>
    private bool IsTracked(MessageItem message) => message.Kind switch
    {
        MessageKind.Error or MessageKind.Reminder or MessageKind.Posted or MessageKind.Subscription => false,
        MessageKind.Break or MessageKind.RoomCare or MessageKind.Absence or MessageKind.Closure or MessageKind.RoomClosure => _messageDay is not null,
        _ => OperationsOfDay() is not null,
    };

    private DayOperationsViewModel? OperationsOfDay() => Operations is { } operations && operations.Scope == (Location?.Id ?? Guid.Empty, Date) ? operations : null;

    private MessageItem Message(MessageState state, string key, MessageKind kind, MessageSeverity severity, string title, string text, string shortText,
        string trigger = "", IReadOnlyList<MessageSubject>? subjects = null, DateTimeOffset? since = null, IReadOnlyList<string>? lines = null,
        string? actionLabel = null, Action? action = null, bool needsConnection = true)
    {
        // Ohne Auslöserzeit gilt, wann die Sitzung die Meldung zuerst sah.
        var seen = state.FirstSeen.TryGetValue(key, out var first) ? first : state.FirstSeen[key] = _time.GetUtcNow();
        var at = since ?? seen;
        var offline = OfflineView;
        return new MessageItem
        {
            Key = key,
            Trigger = trigger,
            Kind = kind,
            Severity = severity,
            Title = title,
            Text = text,
            ShortText = shortText,
            Lines = lines ?? [],
            Subjects = subjects ?? [],
            Since = at,
            SinceText = $"seit {DayLayout.Local(at, _zone):HH:mm}",
            Help = MessageHelp(kind),
            ActionLabel = action is null ? null : actionLabel,
            ActionCommand = action is null ? null : new RelayCommand(action, () => !(needsConnection && offline)),
            DismissCommand = new RelayCommand(() => DismissMessage(key, trigger)),
            OpenCommand = new RelayCommand(() => OpenMessageDetail(key)),
        };
    }

    /// <summary>Thema und Abschnitt des Handbuchs zur Art der Meldung; Antwort auf „Was tun?“, wenn der Assistent nicht antwortet.</summary>
    private static (string Topic, string? Section)? MessageHelp(MessageKind kind) => kind switch
    {
        MessageKind.Break => ("kalender-pausen-und-notizen", "pause"),
        MessageKind.Stock => ("lager-bedarf-pruefen", null),
        MessageKind.RoomCare => ("kalender-betrieb", "raumpflege"),
        MessageKind.DayStart => ("hygiene-tagesstart-abhaken", null),
        MessageKind.RoomsBelowMinimum => ("kalender-raumcheck", null),
        MessageKind.Reminder or MessageKind.Posted => ("kalender-betrieb", "hinweise"),
        MessageKind.Absence => ("kalender-krankmeldung", "ausfall-assistent"),
        MessageKind.Closure => ("schliesszeiten-termine-absagen", "absagen"),
        MessageKind.RoomClosure => ("raumsperren-termine-umbuchen", "umbuchen"),
        MessageKind.Subscription => ("kalender-abos-anzeigen", "meldungen"),
        MessageKind.Hold => ("anfragen-annehmen", "keine-antwort"),
        MessageKind.HrFollowUp => ("personalakte", "folgetermin"),
        MessageKind.Certificate => ("zustaendigkeiten-zertifikate", "erinnerung"),
        MessageKind.StockDuty => ("zustaendigkeiten-zertifikate", "lager"),
        _ => null,
    };

    private List<MessageItem> BuildMessages(MessageState state)
    {
        var all = new List<MessageItem>();
        if (Error is { Length: > 0 } error)
        {
            all.Add(Message(state, "error", MessageKind.Error, MessageSeverity.Error, "Kalender nicht geladen", error, "Kalender nicht geladen", trigger: error,
                actionLabel: "Erneut versuchen", action: () => _ = LoadAsync(), needsConnection: false));
        }

        if (!IsFiltered && _messageDay is { } day && _resources is not null)
        {
            AddBreaks(state, all, day);
            AddAbsences(state, all, day);
            AddRoomCare(state, all, day);
        }

        if (OperationsOfDay() is { } operations)
        {
            AddStock(state, all, operations);
            AddDayStart(state, all, operations);
            AddRoomsBelowMinimum(state, all, operations);
            foreach (var reminder in operations.Reminders)
            {
                all.Add(Message(state, $"reminder:{reminder.Title}", MessageKind.Reminder, MessageSeverity.Notice, reminder.Title, reminder.Text, reminder.Title,
                    trigger: reminder.Text, lines: reminder.Area is null ? [] : [$"Bereich: {reminder.Area}"]));
            }
        }

        if (_day is not null)
        {
            AddClosures(state, all);
            AddRoomClosures(state, all);
        }

        AddHolds(state, all);
        AddHrFollowUps(state, all);
        AddDutyReminders(state, all);

        AddSubscriptionNotices(state, all);
        foreach (var (key, text) in _posted)
        {
            all.Add(Message(state, key, MessageKind.Posted, MessageSeverity.Warning, "Hinweis", text, "Hinweis zur Buchung"));
        }

        return all;
    }

    private static string Num(decimal value) => value.ToString("0.###", CultureInfo.GetCultureInfo("de-AT"));

    private string NameOfEmployee(Guid id) => _resources!.Employees.FirstOrDefault(e => e.Id == id)?.InHouseName() ?? "Person";

    private void AddBreaks(MessageState state, List<MessageItem> all, CalendarDay day)
    {
        foreach (var info in (day.Breaks ?? []).Where(b => b.Warning is not null && _resources!.Employees.Any(e => e.Id == b.EmployeeId)))
        {
            var name = NameOfEmployee(info.EmployeeId);
            all.Add(Message(state, $"break:{info.EmployeeId}", MessageKind.Break, MessageSeverity.Warning, "Pause", $"{name}: {info.Warning}", "Pause fehlt",
                trigger: info.Warning!, subjects: [new(MessageSubjectKind.Person, info.EmployeeId, name)],
                lines: [$"Verlangt: {info.RequiredMinutes} Min., eingeplant: {info.PlannedMinutes} Min."],
                actionLabel: CanBook ? "Pause eintragen" : null, action: CanBook ? () => OpenBreakFor(info) : null));
        }
    }

    /// <summary>Abwesenheit mit offenen Terminen: „3 Termine umbuchen“ bleibt zusätzlich im Spaltenkopf; ohne Termine genügt die Spalte (mit „Alle Mitarbeitenden“ samt Grund).</summary>
    private void AddAbsences(MessageState state, List<MessageItem> all, CalendarDay day)
    {
        foreach (var absence in day.Absences ?? [])
        {
            var open = day.Appointments.Count(a => a.EmployeeId == absence.EmployeeId && DayLayout.IsOpen(a));
            if (open == 0 || _resources!.Employees.All(e => e.Id != absence.EmployeeId))
            {
                continue;
            }

            var name = NameOfEmployee(absence.EmployeeId);
            var termine = open == 1 ? "1 Termin" : $"{open} Termine";
            all.Add(Message(state, $"absence:{absence.EmployeeId}", MessageKind.Absence, MessageSeverity.Warning, "Abwesenheit",
                $"{name} ist {DayLayout.AbsenceText(absence, day.Date)}: {termine} umbuchen.", $"{termine} umbuchen", trigger: open.ToString(CultureInfo.InvariantCulture),
                subjects: [new(MessageSubjectKind.Person, absence.EmployeeId, name)], actionLabel: CanBook ? "Ersatz suchen" : null,
                action: CanBook ? () => OpenAbsenceOf(absence.EmployeeId) : null));
        }
    }

    /// <summary>Raumpflege je Aufgabe: überfällig ist eine Warnung, offen (die Pflegezeit hat begonnen) ein Hinweis; die Uhr des Kalenders entscheidet, nicht der Abruf.</summary>
    private void AddRoomCare(MessageState state, List<MessageItem> all, CalendarDay day)
    {
        var now = _time.GetUtcNow();
        foreach (var task in day.RoomCare?.Tasks ?? [])
        {
            var overdue = task.State == RoomCareState.Overdue || task is { State: RoomCareState.Open } && now > task.Until;
            if (task.State == RoomCareState.Done || !overdue && now < task.From)
            {
                continue;
            }

            var restock = task.Restock?.Count(l => l.State == RestockState.Open) ?? 0;
            var title = overdue ? "Raumpflege überfällig" : "Raumpflege offen";
            all.Add(Message(state, $"care:{task.AppointmentId}", MessageKind.RoomCare, overdue ? MessageSeverity.Warning : MessageSeverity.Notice, title,
                restock == 0 ? task.Text : $"{task.Text}; {(restock == 1 ? "1 Material" : $"{restock} Materialien")} nachfüllen", title,
                trigger: overdue ? "overdue" : "open", subjects: [new(MessageSubjectKind.Room, task.RoomId, task.RoomName)], since: overdue ? task.Until : task.From,
                lines: [.. (task.Restock ?? []).Where(l => l.State == RestockState.Open).Select(l => $"{l.ItemName}: {Num(l.Quantity)} von {Num(l.Minimum)} {l.Unit}")],
                actionLabel: CanCheckCare ? "Abhaken" : null, action: CanCheckCare ? () => _ = CheckCareAsync(task) : null));
        }
    }

    private void AddStock(MessageState state, List<MessageItem> all, DayOperationsViewModel operations)
    {
        foreach (var line in operations.StockWarnings)
        {
            var text = line.Message ?? $"{line.ItemName}: Bestand knapp";
            all.Add(Message(state, $"stock:{line.ItemId}", MessageKind.Stock, MessageSeverity.Warning, "Lager", text, $"{line.ItemName}: Lager", trigger: text,
                subjects: [new(MessageSubjectKind.Material, line.ItemId, line.ItemName)], since: line.ShortFrom,
                lines: [$"Bestand {Num(line.OnHand)} {line.Unit}, Bedarf {Num(line.Needed)} {line.Unit} aus {line.Appointments} Terminen."],
                actionLabel: AdminRequested is null ? null : "Anzeigen", action: AdminRequested is null ? null : () => AdminRequested("stock", null, line.ItemId)));
        }
    }

    /// <summary>Tagesstart offen je Raum (nicht im Voraus); die Kontrolle selbst steht im Detail der Meldung.</summary>
    private void AddDayStart(MessageState state, List<MessageItem> all, DayOperationsViewModel operations)
    {
        if (Date > DateOnly.FromDateTime(_time.GetLocalNow().DateTime))
        {
            return;
        }

        foreach (var room in operations.Rooms.Where(r => !r.IsChecked))
        {
            var text = room.Room.State == RoomStartState.Partial
                ? $"{room.Name}: {room.Room.Items.Count(i => i.Done)} von {room.Room.Items.Count} Punkten abgehakt."
                : $"{room.Name}: noch nichts abgehakt.";
            var key = $"daystart:{room.Room.RoomId}";
            all.Add(Message(state, key, MessageKind.DayStart, MessageSeverity.Warning, "Tagesstart offen", text, "Tagesstart offen", trigger: room.Room.State.ToString(),
                subjects: [new(MessageSubjectKind.Room, room.Room.RoomId, room.Name)], actionLabel: "Tagesstart öffnen", action: () => OpenMessageDetail(key)));
        }
    }

    private void AddRoomsBelowMinimum(MessageState state, List<MessageItem> all, DayOperationsViewModel operations)
    {
        var rooms = operations.RoomsBelowMinimum;
        var count = Math.Max(rooms.Count, _day?.RoomCare?.RoomsBelowMinimum ?? 0);
        if (count == 0)
        {
            return;
        }

        var title = count == 1 ? "1 Raum unter Mindestbestand" : $"{count} Räume unter Mindestbestand";
        all.Add(Message(state, "belowminimum", MessageKind.RoomsBelowMinimum, MessageSeverity.Warning, "Räume unter Mindestbestand",
            rooms.Count > 0 ? $"{string.Join(", ", rooms.Select(r => r.RoomName))}: Material im Raumschrank unter der Mindestmenge." : $"{title}.", title,
            trigger: string.Join(",", rooms.Select(r => r.RoomId)), subjects: [.. rooms.Select(r => new MessageSubject(MessageSubjectKind.Room, r.RoomId, r.RoomName))],
            lines: [.. rooms.SelectMany(r => (r.Equipment?.Below ?? []).Select(l => $"{r.RoomName}: {l.ItemName} {Num(l.Quantity)} von {Num(l.Minimum)} {l.Unit}"))],
            actionLabel: "Raumansicht", action: () => ByRoom = true));
    }

    // ---------- Symbol im Spaltenkopf ----------

    /// <summary>Setzt in jedem Spaltenkopf das Symbol (Dreieck bei Warnungen, Kreis bei Fehlern) mit der Kurzfassung der Meldungen zu dieser Person bzw. diesem Raum.</summary>
    private void ApplyAlerts()
    {
        foreach (var column in Columns)
        {
            var related = Messages.Where(m => m.Subjects.Any(s => s.Id == column.Id)).ToList();
            if (!related.Any(m => m.Severity >= MessageSeverity.Warning))
            {
                column.Alert = null;
                continue;
            }

            var tip = string.Join(" · ", related.GroupBy(m => m.ShortText).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));
            column.Alert = new ColumnAlert(related.Max(m => m.Severity), tip);
        }
    }
}

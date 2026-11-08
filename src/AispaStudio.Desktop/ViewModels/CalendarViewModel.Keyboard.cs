using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Tastatur, Suche und Rechtsklick im Tageskalender. Ein Klick markiert einen Termin, Doppelklick oder Enter öffnet ihn;
/// die Tasten wirken auf den markierten Termin. Was eine Taste tut, entscheidet hier das ViewModel, die Ansicht reicht sie nur durch.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Abstand für den Folgetermin, solange die Kundenkarte noch keinen üblichen Abstand kennt.</summary>
    private static readonly int FollowUpDays = 28;

    private (double X, double Y)? _hoverAt;
    private Guid? _markAfterLoad;
    private Guid? _openAfterLoad;
    private FollowUpDraft? _followUp;
    private int _searchVersion;

    /// <summary>Folgetermin, der nach dem Laden des Zieltags als neue Buchung geöffnet wird.</summary>
    private sealed record FollowUpDraft(DateOnly Date, CustomerDto Customer, Guid TreatmentId, Guid EmployeeId, Guid RoomId, TimeOnly Time);

    /// <summary>Markierter Termin: Ziel der Tasten, hervorgehoben im Kalender.</summary>
    [ObservableProperty]
    public partial Guid? MarkedId { get; private set; }

    public AppointmentDto? Marked => MarkedId is { } id ? _day?.Appointments.FirstOrDefault(a => a.Id == id) : null;

    /// <summary>Lage des markierten Termins im Kalender, damit die Ansicht ihn ins Bild scrollt.</summary>
    public CalendarSpan? MarkedBounds => MarkedId is { } id && ColumnOf(id) is var column and >= 0
        && Columns[column].Items.FirstOrDefault(i => i.Appointment?.Id == id) is { } item
            ? new CalendarSpan(item.Top, item.Height)
            : null;

    public double MarkedLeft => MarkedId is { } id && ColumnOf(id) is var column and >= 0 ? DayLayout.ColumnLeft(column, Scale) : 0;

    /// <summary>Alle Tasten anzeigen („?“ oder F1).</summary>
    [ObservableProperty]
    public partial bool ShowKeyHelp { get; set; }

    /// <summary>Die wichtigsten Tasten für die Unterleiste, passend zum markierten Termin; „Strg+Z Rückgängig“ steht vorn, solange es etwas zurückzunehmen gibt.</summary>
    public IReadOnlyList<KeyHint> KeyHints
    {
        get
        {
            var hints = CanUndo ? [new KeyHint("Strg+Z", "Rückgängig", Key.Z, KeyModifiers.Control), .. BaseKeyHints] : BaseKeyHints;
            return CanResetView ? [.. hints, new KeyHint("Strg+R", "Ansicht zurücksetzen", Key.R, KeyModifiers.Control)] : hints;
        }
    }

    private const string KeyboardOnly = "Nur mit der Tastatur";

    /// <summary>Ein Klick auf einen Eintrag der Tastenleiste tut dasselbe wie die Taste.</summary>
    [RelayCommand]
    private void PressKeyHint(KeyHint? hint)
    {
        if (hint is { IsEnabled: true, Press: { } key })
        {
            HandleKey(key, hint.Modifiers);
        }
    }

    private IReadOnlyList<KeyHint> BaseKeyHints
    {
        get
        {
            if (NewEntry is not null)
            {
                return [new("Alt+1 … 6", "Art wechseln", BlockedReason: KeyboardOnly), new("Tab", "Nächstes Feld", BlockedReason: KeyboardOnly), new("Esc", "Schließen", Key.Escape), new("?", "Alle Tasten", Key.F1)];
            }

            if (Request is not null)
            {
                return [new("Enter", "Ersten buchen", Key.Enter), new("Strg+2 / 3", "Andere buchen", BlockedReason: KeyboardOnly), new("↑ ↓", "Vorschlag wechseln", BlockedReason: KeyboardOnly), new("Esc", "Schließen", Key.Escape)];
            }

            if (FreeTime is not null)
            {
                return [new("↑ ↓", "Freie Zeit wechseln", BlockedReason: KeyboardOnly), new("Enter", "Buchen", Key.Enter), new("Esc", "Suche schließen", Key.Escape), new("?", "Alle Tasten", Key.F1)];
            }

            if (Marked is not { } marked)
            {
                return
                [
                    new("N", "Neuer Termin", Key.N), new("Strg+N", "Freie Zeit", Key.N, KeyModifiers.Control), new("Strg+F", "Suchen", Key.F, KeyModifiers.Control),
                    new("← →", "Tag", BlockedReason: "Mit den Pfeilen oben wechselst du den Tag"),
                    new("↓", "Termine durchgehen", Key.Down, BlockedReason: HasAppointments ? null : "Heute gibt es keine Termine"),
                    new("?", "Alle Tasten", Key.F1),
                ];
            }

            var hints = new List<KeyHint> { new("Enter", "Details", Key.Enter) };
            if (marked.Status == AppointmentStatus.Booked && CanChangeStatus(marked))
            {
                hints.Add(IsOnToday(marked) ? new("E", "Einchecken", Key.E) : new("E", "Einchecken", BlockedReason: "Einchecken: nur am Tag des Termins"));
            }
            else if (CheckoutRequested is not null && CanBook && marked.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed)
            {
                hints.Add(new("K", "Kassieren", Key.K));
            }

            if (CanMoveAppointment(marked))
            {
                hints.Add(new("Alt+Pfeile", "Verschieben", BlockedReason: "Ziehen mit der Maus oder Alt + Pfeiltasten"));
            }

            if (CanBook && marked.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
            {
                hints.Add(new("Entf", "Stornieren", Key.Delete));
            }

            hints.Add(new("Esc", "Markierung aufheben", Key.Escape));
            hints.Add(new("?", "Alle Tasten", Key.F1));
            return hints;
        }
    }

    /// <summary>
    /// Tastenhilfe: alle Tasten in Gruppen. Die Beschreibungen sind so kurz, dass sie in der Tastenhilfe höchstens zwei Zeilen brauchen (Runde 5 Ä35);
    /// das Handbuch (<see cref="KeyManualTopic"/>) nennt den Rest.
    /// </summary>
    public static IReadOnlyList<KeyHintGroup> AllKeys { get; } =
    [
        new("Tag", [
            new("← →", "Tag zurück / vor (ohne Markierung)"),
            new("Pos1", "Heute"),
            new("G", "Datum wählen"),
            new("F5", "Aktualisieren"),
            new("Strg + / −", "Zoom, auch Strg + Mausrad"),
            new("Strg 0", "Tag einpassen"),
            new("Strg+R", "Ansicht zurücksetzen (Filter, Team oder Räume, Tag)"),
        ]),
        new("Buchen", [
            new("N", "Neuer Termin (an der Stelle unter der Maus oder ab jetzt)"),
            new("Strg+N", "Freie Zeit suchen: ↑ ↓ wechseln, Enter bucht"),
            new("Strg+K", "Wunsch eintippen (KI): Enter bucht Vorschlag 1, Strg+2/3 die anderen"),
            new("P", "Paar oder Spa-Tag"),
            new("S", "Sauna-Eintritt"),
            new("B", "Sperrzeit (z. B. Arzt 10 bis 12)"),
            new("Umschalt+B", "Pause der Person"),
            new(",", "Notiz an einer Uhrzeit"),
            new(".", "Tagesnotiz für den Standort"),
            new("Alt+1 … 6", "Art im Eingabefenster wechseln (Termin, Paar/Spa, Pause …)"),
            new("Strg+F", "Suchen: Gast, Telefon oder Behandlung"),
        ]),
        new("Markierter Termin", [
            new("↑ ↓", "Voriger / nächster Termin der Spalte"),
            new("← →", "Termin in der Nachbarspalte"),
            new("Enter", "Details, auch Doppelklick oder Alt+Enter"),
            new("Strg+Enter", "Kundenkarte"),
            new("E", "Einchecken"),
            new("K", "Kassieren"),
            new("F", "Folgetermin in vier Wochen"),
            new("Alt+↑ ↓", "Um eine Viertelstunde verschieben"),
            new("Alt+← →", "Zur Nachbarspalte verschieben"),
            new("Entf", "Stornieren (mit Rückfrage und Grund)"),
            new("Strg+Z", "Verschieben, Stornieren, Einchecken zurücknehmen"),
        ]),
        new("Seitenleiste und Reiter", [
            new("Esc", "Gewählte Seite schließen, sonst Markierung aufheben"),
            new("Strg+S", "Speichern wie der Knopf der Maske"),
            new("Strg+Tab", "Nächster Reiter (mit Umschalt der vorige)"),
            new("Strg+W", "Gewählte Seite schließen (fragt bei Eingaben)"),
            new("Strg+1 … 9", "Reiter 1 bis 8 wählen, Strg+9 den letzten"),
            new("F6", "Bereich wechseln (mit Umschalt rückwärts)"),
        ]),
        new("Allgemein", [
            new("Rechtsklick", "Passende Schritte für Termin oder freie Stelle"),
            new("Rechtsklick Kopf", "Ersatz für die Termine einer Person, krank melden"),
            new("M", "Meldungen öffnen oder schließen"),
            new("? oder F1", "Diese Hilfe"),
        ]),
    ];

    /// <summary>Tastenhilfe in zwei gleich breiten Spalten: Tag und Buchen links, der markierte Termin, die Seitenleiste und Allgemein rechts (Reihenfolge wie in <see cref="AllKeys"/>).</summary>
    public static IReadOnlyList<KeyHintGroup> KeysLeft { get; } = [AllKeys[0], AllKeys[1]];

    public static IReadOnlyList<KeyHintGroup> KeysRight { get; } = [AllKeys[2], AllKeys[3], AllKeys[4]];

    /// <summary>Hilfe-Thema mit allen Tasten, zu dem „Alle Tasten im Handbuch“ führt.</summary>
    public const string KeyManualTopic = "kalender-tastenkuerzel";

    /// <summary>Link „Alle Tasten im Handbuch“ unten in der Tastenhilfe: öffnet das Thema und schließt die Tastenhilfe.</summary>
    [RelayCommand]
    private void OpenKeyManual()
    {
        ShowKeyHelp = false;
        HelpContent.Open(KeyManualTopic);
    }

    /// <summary>Die Ansicht soll das Suchfeld bzw. den Datumswähler öffnen.</summary>
    public event Action? SearchRequested;

    public event Action? DatePickerRequested;

    /// <summary>Die Markierung ist mit den Pfeiltasten zu diesem Termin gewechselt: die Ansicht gibt seiner Karte den Fokus, damit ein Screenreader ihn ansagt.</summary>
    public event Action<Guid>? MarkMovedByKey;

    // ---------- Markieren ----------

    /// <summary>Einfacher Klick auf einen Termin: markieren, ohne das Panel zu öffnen. Ist ein Termin offen, zeigt das Panel den neuen.</summary>
    public void Mark(CalendarItem item)
    {
        if (item.Appointment is not { } appointment)
        {
            return;
        }

        if (Selected is not null)
        {
            OpenAppointment(appointment);
            return;
        }

        MarkedId = appointment.Id;
        Reselect();
    }

    /// <summary>
    /// Termin aus einer Konfliktmeldung im Kalender markieren und ins Bild holen; liegt er an einem anderen Tag, wechselt der Kalender dorthin.
    /// Ein offenes Fenster (neuer Eintrag, Termin) bleibt stehen, damit die Meldung sichtbar bleibt.
    /// </summary>
    public void ShowAppointment(Guid id, DateTimeOffset start)
    {
        if (_day?.Appointments.Any(a => a.Id == id) == true)
        {
            if (MarkedId == id)
            {
                // Schon markiert: trotzdem wieder ins Bild scrollen.
                OnPropertyChanged(nameof(MarkedId));
            }

            MarkedId = id;
            Reselect();
            return;
        }

        var date = DateOnly.FromDateTime(DayLayout.Local(start, _zone));
        if (date != Date)
        {
            _markAfterLoad = id;
            Date = date;
        }
    }

    /// <summary>
    /// „Zuletzt“ im Abschnitt „Kunde“: den Termin eines anderen Tages öffnen. Der Kalender springt zu dem Tag und zeigt dort sein Panel; ein Termin des
    /// aktuellen Tages öffnet gleich.
    /// </summary>
    public void OpenAppointmentOn(Guid id, DateTimeOffset start)
    {
        if (_day?.Appointments.FirstOrDefault(a => a.Id == id) is { } here)
        {
            OpenAppointment(here);
            return;
        }

        var date = DateOnly.FromDateTime(DayLayout.Local(start, _zone));
        if (IsFiltered && !InRange(date))
        {
            Filter = null;
        }

        _openAfterLoad = id;
        Date = date;
    }

    private void Unmark()
    {
        MarkedId = null;
        Reselect();
    }

    /// <summary>Nach dem Laden eines Tages: Treffer der Suche markieren oder einen Folgetermin öffnen.</summary>
    private void AfterDayLoaded()
    {
        if (_markAfterLoad is { } id && _day?.Appointments.Any(a => a.Id == id) == true)
        {
            _markAfterLoad = null;
            MarkedId = id;
            Rebuild();
        }
        else if (MarkedId is { } marked && _day?.Appointments.All(a => a.Id != marked) == true)
        {
            MarkedId = null;
        }

        if (_openAfterLoad is { } open && _day?.Appointments.FirstOrDefault(a => a.Id == open) is { } found)
        {
            _openAfterLoad = null;
            OpenAppointment(found);
        }

        if (_followUp is { } followUp && followUp.Date == Date)
        {
            _followUp = null;
            StartFollowUp(followUp);
        }
    }

    private int ColumnOf(Guid appointmentId) =>
        Columns.ToList().FindIndex(c => c.Items.Any(i => i.Appointment?.Id == appointmentId));

    private bool CanChangeStatus(AppointmentDto appointment) =>
        _user.Rights.Contains(AccessRight.ChangeStatus)
        && (_user.Access?.Role != StaffRole.Therapist || appointment.EmployeeId == _user.EmployeeId);

    private bool CanMoveAppointment(AppointmentDto appointment) =>
        CanBook && HasResourceColumns && appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn;

    /// <summary>Einchecken nur am Tag des Termins (L1), gerechnet in der Zeitzone des Standorts wie am Server.</summary>
    private bool IsOnToday(AppointmentDto appointment) =>
        DateOnly.FromDateTime(DayLayout.Local(appointment.Start, _zone)) == DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), _zone));

    // ---------- Tasten ----------

    /// <summary>
    /// Eine Taste im Kalender. Die Ansicht ruft das nur, wenn kein Eingabefeld den Fokus hat.
    /// Gibt zurück, ob die Taste etwas bewirkt hat.
    /// </summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var marked = Marked;

        // Esc beim Ziehen bricht nur das Ziehen ab: der Termin bleibt, Markierung und Panels bleiben auch.
        if (key == Key.Escape && IsDragging)
        {
            CancelDrag();
            return true;
        }

        // Esc in den Zahlenfeldern für Vor- und Nachbereitung: nur das Ändern abbrechen, das Panel bleibt.
        if (key == Key.Escape && Selected is { IsEditingTimes: true } editing)
        {
            editing.CancelEditTimes();
            return true;
        }

        // Esc im Notizfeld (Terminnotiz oder Tagesnotiz): nur das Schreiben abbrechen, die Seite bleibt.
        if (key == Key.Escape && Selected is { IsEditingNote: true } writing)
        {
            writing.CancelEditNote();
            return true;
        }

        if (key == Key.Escape && MessagesPanel is { IsEditingNote: true } notes)
        {
            notes.CancelNote();
            return true;
        }

        if (FreeTime is { HasSlots: true } freeTime && !ctrl && !alt)
        {
            switch (key)
            {
                case Key.Up or Key.Left:
                    freeTime.Move(-1);
                    return true;
                case Key.Down or Key.Right:
                    freeTime.Move(1);
                    return true;
                case Key.Enter:
                    _ = freeTime.BookActiveAsync();
                    return true;
            }
        }

        if (Request is { HasProposals: true } request && !alt)
        {
            switch (key)
            {
                case Key.Up when !ctrl:
                    request.Move(-1);
                    return true;
                case Key.Down when !ctrl:
                    request.Move(1);
                    return true;
                case Key.Enter when !ctrl:
                    _ = request.BookActiveCommand.ExecuteAsync(null);
                    return true;
                case Key.D1 or Key.NumPad1 or Key.D2 or Key.NumPad2 or Key.D3 or Key.NumPad3:
                    _ = request.BookAsync(key is Key.D1 or Key.NumPad1 ? 1 : key is Key.D2 or Key.NumPad2 ? 2 : 3);
                    return true;
            }
        }

        if (ctrl)
        {
            switch (key)
            {
                case Key.K when CanBook:
                    OpenRequest();
                    return true;
                case Key.N when CanBook:
                    OpenFreeTime();
                    return true;
                case Key.F:
                    SearchRequested?.Invoke();
                    return true;
                case Key.Tab:
                    return CyclePanelTab(modifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                case Key.W or (>= Key.D1 and <= Key.D9) or (>= Key.NumPad1 and <= Key.NumPad9):
                    return HandleTabShortcut(key);
                case Key.R when CanResetView:
                    ResetView();
                    return true;
                case Key.Z when CanUndo:
                    _ = UndoMoveCommand.ExecuteAsync(null);
                    return true;
                case Key.OemPlus or Key.Add:
                    ZoomIn();
                    return true;
                case Key.OemMinus or Key.Subtract:
                    ZoomOut();
                    return true;
                case Key.D0 or Key.NumPad0:
                    ZoomFit();
                    return true;
                case Key.Enter when marked is not null && CustomerRequested is not null:
                    _ = CustomerRequested(marked.CustomerId);
                    return true;
                default:
                    return false;
            }
        }

        if (alt && !ctrl && NewEntry is not null && NumberOf(key) is { } number)
        {
            return SelectNewEntry(number);
        }

        if (alt && marked is not null)
        {
            switch (key)
            {
                case Key.Up:
                    _ = MoveMarkedAsync(-15, 0);
                    return true;
                case Key.Down:
                    _ = MoveMarkedAsync(15, 0);
                    return true;
                case Key.Left:
                    _ = MoveMarkedAsync(0, -1);
                    return true;
                case Key.Right:
                    _ = MoveMarkedAsync(0, 1);
                    return true;
                case Key.Enter:
                    OpenAppointment(marked);
                    return true;
            }
        }

        if (modifiers != KeyModifiers.None && modifiers != KeyModifiers.Shift)
        {
            return false;
        }

        switch (key)
        {
            case Key.Escape:
                if (ShowKeyHelp)
                {
                    ShowKeyHelp = false;
                }
                else if (HasPanel)
                {
                    _ = RequestClosePanelAsync();
                }
                else if (HasFocus)
                {
                    ClearFocus();
                }
                else if (IsFiltered)
                {
                    ClearFilter();
                }
                else if (marked is not null)
                {
                    Unmark();
                }
                else
                {
                    return false;
                }

                return true;
            case Key.F1:
                ShowKeyHelp = !ShowKeyHelp;
                return true;
            case Key.F5:
                _ = LoadAsync();
                return true;
            case Key.Home:
                Today();
                return true;
            case Key.G:
                DatePickerRequested?.Invoke();
                return true;
            case Key.N:
                NewAppointmentHere();
                return true;
            case Key.P when CanBook:
                OpenGroupBooking();
                return true;
            case Key.S when HasAreas:
                OpenAreas();
                return true;
            case Key.B when CanBook:
                OpenKind(modifiers.HasFlag(KeyModifiers.Shift) ? NewEntryKind.Break : NewEntryKind.Block);
                return true;
            case Key.OemComma when CanBook:
                OpenKind(NewEntryKind.Note);
                return true;
            case Key.OemPeriod or Key.Decimal when CanBook:
                OpenDayNote();
                return true;
            case Key.M:
                OpenMessages();
                return true;
            case Key.Left or Key.Right when marked is null:
                Date = Date.AddDays(key == Key.Left ? -SpanDays : SpanDays);
                return true;
            case Key.Left or Key.Right:
                MarkNeighbour(marked, key == Key.Left ? -1 : 1);
                return true;
            case Key.Up or Key.Down:
                MarkNext(marked, key == Key.Up ? -1 : 1);
                return true;
        }

        if (marked is null)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
                OpenAppointment(marked);
                return true;
            case Key.E when marked.Status == AppointmentStatus.Booked && CanChangeStatus(marked) && IsOnToday(marked):
                _ = RunOnAppointmentAsync(marked, panel => panel.CheckInCommand.ExecuteAsync(null), $"{marked.CustomerName} eingecheckt.");
                return true;
            case Key.K when CheckoutRequested is not null && CanBook && marked.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed:
                _ = CheckoutRequested(marked);
                return true;
            case Key.F when CanBook:
                _ = FollowUpAsync(marked);
                return true;
            case Key.V when CanMoveAppointment(marked):
                ShowToast("Verschieben: ziehen oder Alt + Pfeiltasten, zurück mit Strg+Z");
                return true;
            case Key.Delete when CanBook && marked.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn:
                AskCancel(marked);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Ziffer 1 bis 6 (auch Ziffernblock) für Alt+Ziffer im Eingabefenster.</summary>
    public static int? NumberOf(Key key) => key switch
    {
        >= Key.D1 and <= Key.D6 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad6 => key - Key.NumPad0,
        _ => null,
    };

    /// <summary>„?“ kommt je nach Tastatur als Texteingabe, nicht als eigene Taste.</summary>
    public bool HandleText(string? text)
    {
        if (text == "?")
        {
            ShowKeyHelp = !ShowKeyHelp;
            return true;
        }

        return false;
    }

    [RelayCommand]
    private void ToggleKeyHelp() => ShowKeyHelp = !ShowKeyHelp;

    /// <summary>N: an der Stelle unter dem Mauszeiger, sonst ab jetzt in der ersten Spalte (Walk-in).</summary>
    private void NewAppointmentHere()
    {
        if (_hoverAt is { } at)
        {
            StartBookingAt(at.X, at.Y);
        }
        else
        {
            NewAppointment();
        }
    }

    /// <summary>↑/↓: voriger oder nächster Termin der Spalte. Ohne Markierung der nächste anstehende (heute) bzw. der erste des Tages.</summary>
    private void MarkNext(AppointmentDto? marked, int direction)
    {
        var column = marked is null ? -1 : ColumnOf(marked.Id);
        if (marked is null || column < 0)
        {
            var now = IsToday ? _time.GetUtcNow() : DateTimeOffset.MinValue;
            var first = Columns.SelectMany(c => c.Items).Select(i => i.Appointment).OfType<AppointmentDto>()
                .Where(a => a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.End <= now)
                .ThenBy(a => a.Start)
                .FirstOrDefault();
            if (first is not null)
            {
                MarkByKey(first.Id);
            }

            return;
        }

        var items = Columns[column].Items.Where(i => i.Appointment is not null).OrderBy(i => i.Top).ToList();
        var index = items.FindIndex(i => i.Appointment!.Id == marked.Id) + direction;
        if (index >= 0 && index < items.Count)
        {
            MarkByKey(items[index].Appointment!.Id);
        }
    }

    /// <summary>Markieren mit den Pfeiltasten; nur dieser Weg verschiebt den Fokus auf die Karte (Klick, Suche und Meldungen nicht).</summary>
    private void MarkByKey(Guid id)
    {
        MarkedId = id;
        Reselect();
        MarkMovedByKey?.Invoke(id);
    }

    /// <summary>←/→ mit Markierung: der zeitlich nächste Termin in der nächsten Spalte, die Termine hat.</summary>
    private void MarkNeighbour(AppointmentDto marked, int direction)
    {
        var column = ColumnOf(marked.Id);
        var top = Columns.ElementAtOrDefault(column)?.Items.FirstOrDefault(i => i.Appointment?.Id == marked.Id)?.Top ?? 0;
        for (var c = column + direction; c >= 0 && c < Columns.Count; c += direction)
        {
            var nearest = Columns[c].Items.Where(i => i.Appointment is not null).MinBy(i => Math.Abs(i.Top - top));
            if (nearest is not null)
            {
                MarkByKey(nearest.Appointment!.Id);
                return;
            }
        }
    }

    /// <summary>Alt + Pfeile: um eine Viertelstunde oder in die Nachbarspalte, über denselben Weg wie das Ziehen.</summary>
    private async Task MoveMarkedAsync(int minutes, int columns)
    {
        if (Marked is not { } marked || !CanMoveAppointment(marked))
        {
            return;
        }

        var column = ColumnOf(marked.Id) + columns;
        if (column < 0 || column >= Columns.Count)
        {
            return;
        }

        var start = marked.Start.AddMinutes(minutes);
        if (columns != 0 && Columns[column].Date is { } date)
        {
            // 7-Tage-Ansicht: die Nachbarspalte ist ein anderer Tag, die Uhrzeit bleibt.
            start = DayLayout.LocalToUtc(date, TimeOnly.FromDateTime(DayLayout.Local(start, _zone)), _zone);
        }

        await MoveAsync(marked, start, column).ConfigureAwait(true);
    }

    /// <summary>Entf: Termin öffnen und mit dem Rückfragedialog nach Grund und Bestätigung fragen.</summary>
    private void AskCancel(AppointmentDto appointment)
    {
        OpenAppointment(appointment);
        if (Selected is { } panel)
        {
            _ = panel.AskCancelAsync();
        }
    }

    /// <summary>Statuswechsel ohne das Panel zu öffnen; Fehler erscheinen als Rückmeldung.</summary>
    private async Task RunOnAppointmentAsync(AppointmentDto appointment, Func<AppointmentViewModel, Task> action, string done)
    {
        var panel = PanelFor(appointment);
        var undoBefore = _undo;
        await action(panel).ConfigureAwait(true);
        if (panel.Error is { } error)
        {
            ShowProblem("Status nicht geändert", error, action: () => _ = RunOnAppointmentAsync(appointment, action, done));
            return;
        }

        // Stornieren und Einchecken melden sich schon selbst, mit „Rückgängig“ (OfferUndo).
        if (ReferenceEquals(_undo, undoBefore))
        {
            ShowToast(done);
        }
    }

    // ---------- Folgetermin ----------

    /// <summary>F: gleicher Gast, gleiche Behandlung, gleiche Person und Uhrzeit in vier Wochen; die Buchung öffnet sich auf dem Zieltag.</summary>
    private async Task FollowUpAsync(AppointmentDto appointment)
    {
        var customer = await _api.GetAsync<CustomerDto>($"api/customers/{appointment.CustomerId}").ConfigureAwait(true);
        if (customer.Value is null)
        {
            ShowProblem("Folgetermin nicht möglich", customer.Error, action: () => _ = FollowUpAsync(appointment));
            return;
        }

        var local = DayLayout.Local(appointment.Start, _zone);
        var draft = new FollowUpDraft(DateOnly.FromDateTime(local).AddDays(FollowUpDays), customer.Value, appointment.TreatmentId,
            appointment.EmployeeId, appointment.RoomId, TimeOnly.FromDateTime(local));
        if (draft.Date == Date && !IsFiltered)
        {
            StartFollowUp(draft);
            return;
        }

        _followUp = draft;
        Filter = null;
        Date = draft.Date;
    }

    private void StartFollowUp(FollowUpDraft draft)
    {
        var column = Columns.FirstOrDefault(c => c.Id == (ByRoom ? draft.RoomId : draft.EmployeeId)) ?? (Columns.Count > 0 ? Columns[0] : null);
        if (column is null || !CanBook || _resources is null)
        {
            return;
        }

        // Kann die Spalte die Behandlung nicht (andere Person am Tag, Qualifikation geändert), sucht die freie Zeit, wer sie kann.
        if (_resources.Treatments.FirstOrDefault(t => t.Id == draft.TreatmentId) is { } treatment && !TreatmentFits.Suits(_resources, column.Id, ByRoom)(treatment))
        {
            OpenFreeTime(new FreeTimePrefill(treatment.Id, Guest: draft.Customer));
            return;
        }

        var start = DayLayout.LocalToUtc(draft.Date, draft.Time, _zone);
        StartBooking(column, DayLayout.Top(_dayStart, start, Scale));
        if (Booking is { } booking)
        {
            booking.Treatment = booking.Treatments.FirstOrDefault(t => t.Id == draft.TreatmentId) ?? booking.Treatment;
            booking.Customers.Insert(0, draft.Customer);
            booking.Customer = draft.Customer;
            booking.Note = "Folgetermin";
        }
    }

    // ---------- Rechtsklick ----------

    /// <summary>Rechtsklick auf einen Termin: die nächsten sinnvollen Schritte je Status, Sonderfälle unter „Mehr“, Stornieren zuletzt.</summary>
    public IReadOnlyList<CalendarAction> ActionsFor(CalendarItem item)
    {
        if (item.Appointment is not { } appointment)
        {
            return item.Block is { } block ? ActionsForBlock(block) : [];
        }

        var actions = new List<CalendarAction> { new("Details öffnen", "Enter", () => OpenAppointment(appointment), Icon: Icons.Info) };
        if (CanBook && appointment.PlaceholderGuest is not null && appointment.Status != AppointmentStatus.Cancelled)
        {
            actions.Add(new("Namen nachtragen", null, () => OpenAppointment(appointment)));
        }

        var canStatus = CanChangeStatus(appointment);
        if (appointment.Status == AppointmentStatus.Booked && canStatus && IsOnToday(appointment))
        {
            actions.Add(new("Einchecken", "E", () => _ = RunOnAppointmentAsync(appointment, p => p.CheckInCommand.ExecuteAsync(null),
                $"{appointment.CustomerName} eingecheckt."), Icon: Icons.SignIn));
        }

        if (CheckoutRequested is not null && CanBook && appointment.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed)
        {
            actions.Add(new("Kassieren", "K", () => _ = CheckoutRequested(appointment)));
        }

        if (CustomerRequested is not null)
        {
            actions.Add(new("Kundenkarte", "Strg+Enter", () => _ = CustomerRequested(appointment.CustomerId), Icon: Icons.User));
        }

        if (CanBook)
        {
            actions.Add(new("Folgetermin", "F", () => _ = FollowUpAsync(appointment), Icon: Icons.CalendarPlus));
        }

        if (CanMoveAppointment(appointment))
        {
            actions.Add(new("Verschieben", "Alt+Pfeile", () => ShowToast(
                "Termin ziehen oder mit Alt + Pfeiltasten verschieben. Strg+Z nimmt die Verschiebung zurück."), Icon: Icons.Move));
        }

        // Keine Untermenüs (Design-Brief 5.21): jede Verspätung ist ein eigener Eintrag.
        if (CanBook && appointment.Status == AppointmentStatus.Booked)
        {
            actions.AddRange(LateActions(appointment).Select(a => a.Header == "Doch pünktlich" ? a : a with { Header = $"Kommt {a.Header}" }));
        }

        actions.AddRange(PreparationActions(appointment));
        if (canStatus && appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
        {
            actions.Add(new("Erledigt", null, () => _ = RunOnAppointmentAsync(appointment, p => p.CompleteCommand.ExecuteAsync(null),
                $"{appointment.CustomerName}: erledigt."), Icon: Icons.Check));
        }

        if (canStatus && appointment.Status == AppointmentStatus.Booked)
        {
            actions.Add(new("Nicht erschienen", null, () => _ = RunOnAppointmentAsync(appointment, p => p.NoShowCommand.ExecuteAsync(null),
                $"{appointment.CustomerName}: nicht erschienen."), Icon: Icons.UserX));
        }

        // Wiederholen und die ganze Buchung stehen wie im Panel hinter „Mehr“, hier flach (Brief 5.21).
        if (CanBook && appointment is { SeriesId: null, GroupId: null, Status: AppointmentStatus.Booked or AppointmentStatus.CheckedIn or AppointmentStatus.Completed })
        {
            actions.Add(new("Wiederholen …", null, () =>
            {
                OpenAppointment(appointment);
                Selected?.EditSeriesCommand.Execute(null);
            }));
        }

        if (appointment.GroupId is not null)
        {
            actions.Add(new("Ganze Buchung verschieben oder stornieren", null, () => OpenAppointment(appointment)));
        }

        if (CanBook && appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
        {
            actions.Add(new("Stornieren …", "Entf", () => AskCancel(appointment), IsDanger: true, Icon: Icons.Close));
        }

        return actions;
    }

    /// <summary>Rechtsklick auf eine freie Stelle: buchen an dieser Stelle und die Sonderfälle.</summary>
    public IReadOnlyList<CalendarAction> ActionsAt(double x, double y)
    {
        if (!CanBook || !HasResourceColumns || Columns.Count == 0)
        {
            return [];
        }

        var anchor = AnchorAt(x, y);
        var column = anchor.Column!;
        var start = anchor.Start;
        // In der 7-Tage-Ansicht gehört zum Ort auch der Tag.
        var when = column.Date is { } day ? $"{DayLayout.ShortDay(day)} {DayLayout.Local(start, _zone):HH:mm}" : $"{DayLayout.Local(start, _zone):HH:mm}";
        var actions = new List<CalendarAction>
        {
            new($"Termin hier ({when}, {column.Title})", "N", () => StartBookingAt(x, y, suggest: false)),
            new("Paar oder Spa-Tag", "P", () => OpenNewEntry(NewEntryKind.Group, AnchorAt(x, y))),
            new("Freie Zeit suchen", "Strg+N", OpenFreeTime),
            new("Wunsch eintippen (KI)", "Strg+K", OpenRequest),
        };
        if (!column.IsRoom)
        {
            var gap = _day is null ? null : DayLayout.GapAt(_day, ByRoom, column.Id, start);
            actions.Add(new($"Pause hier ({BreakMinutes(column, gap, start, fillGap: false)} Min.)", "Umschalt+B", () => OpenBreakAt(x, y)));
        }

        actions.Add(new("Notiz hier", ",", () => OpenNoteAt(x, y)));
        actions.Add(new("Sperrzeit hier", "B", () => OpenBlockAt(x, y)));
        if (HasAreas)
        {
            actions.Add(new("Sauna-Eintritt", "S", () => OpenNewEntry(NewEntryKind.Area, AnchorAt(x, y))));
        }

        if (!IsFiltered)
        {
            actions.Add(new("Tagesnotiz", ".", OpenDayNote));
        }

        return actions;
    }

    // ---------- Suche ----------

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchHits))]
    public partial IReadOnlyList<CalendarSearchHit> SearchHits { get; private set; } = [];

    /// <summary>Die Treffer stehen als Liste unter dem Suchfeld; Esc schließt sie, ohne den Suchtext zu leeren.</summary>
    public bool HasSearchHits => SearchHits.Count > 0 && !_searchListDismissed;

    private bool _searchListDismissed;

    /// <summary>Esc im Suchfeld, erste Stufe: die Trefferliste schließen, der Text bleibt; false, wenn keine Liste offen ist.</summary>
    public bool DismissSearchList()
    {
        if (!HasSearchHits)
        {
            return false;
        }

        _searchListDismissed = true;
        OnPropertyChanged(nameof(HasSearchHits));
        return true;
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(value);

    /// <summary>
    /// Sucht zuerst im angezeigten Tag (Gast, Behandlung, Person), dann ab zwei Zeichen über die Kundensuche
    /// (Name, Telefon, E-Mail) nach den nächsten Terminen dieser Gäste an diesem Standort.
    /// </summary>
    public async Task SearchAsync(string query)
    {
        var version = ++_searchVersion;
        _searchListDismissed = false;
        var text = query.Trim();
        if (text.Length == 0 || _day is null || _resources is null)
        {
            SearchHits = [];
            return;
        }

        bool Matches(string? value) => value?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true;
        var day = _day.Appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled)
            .Where(a => Matches(a.CustomerName) || Matches(a.TreatmentName)
                || Matches(_resources.Employees.FirstOrDefault(e => e.Id == a.EmployeeId)?.DisplayName)
                || Matches(_resources.Employees.FirstOrDefault(e => e.Id == a.EmployeeId)?.PreferredName))
            .OrderBy(a => a.Start)
            .Select(Hit)
            .Take(8)
            .ToList();
        SearchHits = day;
        if (text.Length < 2 || _api.IsOffline)
        {
            return;
        }

        var customers = await _api.GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString(text)}").ConfigureAwait(true);
        if (version != _searchVersion || customers.Value is null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var more = new List<CalendarSearchHit>();
        foreach (var customer in customers.Value.Take(3))
        {
            var appointments = await _api.GetAsync<List<AppointmentDto>>($"api/customers/{customer.Id}/appointments").ConfigureAwait(true);
            if (version != _searchVersion)
            {
                return;
            }

            more.AddRange((appointments.Value ?? [])
                .Where(a => a.LocationId == _day?.LocationId && a.Status != AppointmentStatus.Cancelled)
                .Where(a => DateOnly.FromDateTime(DayLayout.Local(a.Start, _zone)) >= today && day.All(h => h.AppointmentId != a.Id))
                .OrderBy(a => a.Start)
                .Take(2)
                .Select(Hit));
        }

        SearchHits = [.. day, .. more.OrderBy(h => h.Date).Take(8)];
    }

    private CalendarSearchHit Hit(AppointmentDto appointment)
    {
        var local = DayLayout.Local(appointment.Start, _zone);
        var date = DateOnly.FromDateTime(local);
        var who = _resources?.Employees.FirstOrDefault(e => e.Id == appointment.EmployeeId)?.InHouseName();
        var when = date == Date ? $"{local:HH:mm}" : $"{DayLayout.DateText(date)}, {local:HH:mm}";
        return new CalendarSearchHit(appointment.CustomerName, $"{when} · {appointment.TreatmentName}{(who is null ? "" : $" · {who}")}", date, appointment.Id);
    }

    /// <summary>Treffer wählen: zum Tag springen und den Termin markieren.</summary>
    public void OpenSearchHit(CalendarSearchHit? hit)
    {
        hit ??= SearchHits.Count > 0 ? SearchHits[0] : null;
        if (hit is null)
        {
            return;
        }

        ClearSearch();
        if (IsFiltered && !InRange(hit.Date))
        {
            Filter = null;
        }

        if (hit.Date == Date || InRange(hit.Date))
        {
            MarkedId = hit.AppointmentId;
            Reselect();
            return;
        }

        _markAfterLoad = hit.AppointmentId;
        Date = hit.Date;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        _searchVersion++;
        SearchText = "";
        SearchHits = [];
    }
}

/// <summary>
/// Taste und Wirkung für Unterleiste und Tastenhilfe. In der Unterleiste ist der Eintrag ein Knopf, der <see cref="Press"/> mit <see cref="Modifiers"/> auslöst;
/// ohne <see cref="Press"/> oder mit <see cref="BlockedReason"/> ist er gesperrt und der Hinweis nennt den Grund.
/// </summary>
public sealed record KeyHint(string Key, string Text, Avalonia.Input.Key? Press = null, KeyModifiers Modifiers = KeyModifiers.None, string? BlockedReason = null)
{
    public bool IsEnabled => Press is not null && BlockedReason is null;

    public string Tip => BlockedReason ?? $"{Text} ({Key})";

    public string AccessibleName => $"{Text}, Taste {Key}";
}

public sealed record KeyHintGroup(string Title, IReadOnlyList<KeyHint> Keys);

/// <summary>Eintrag im Rechtsklick-Menü (Design-Brief 5.21: keine Untermenüs); mit <paramref name="IsDanger"/> steht er unten nach einem Trenner in danger.</summary>
public sealed record CalendarAction(string Header, string? Gesture, Action? Run, bool IsDanger = false, string? Icon = null);

/// <summary>Treffer der Kalendersuche.</summary>
public sealed record CalendarSearchHit(string Title, string Detail, DateOnly Date, Guid AppointmentId);

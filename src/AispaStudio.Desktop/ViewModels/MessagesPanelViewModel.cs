using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Seitenleiste „Meldungen“ (Design-Brief 5.12, Schritt 16): ganz oben die Gruppe „Notizen heute“ mit den Tagesnotizen ohne Uhrzeit (Runde 5, Ä31),
/// darunter alles, was am Tag Aufmerksamkeit braucht, in Gruppen nach Schwere (Fehler, Warnungen, Hinweise) und was in dieser Sitzung erledigt wurde.
/// Je Meldung eine Karte mit einer Aktion und „Wegklicken“; ein Klick auf die Karte öffnet die Detailansicht. Notizen sind keine Meldungen und zählen
/// nicht mit. Der Kalender hält Meldungen und Notizen und gibt sie nach jeder Änderung weiter; ein Filter auf eine Person, einen Raum oder ein Material
/// zeigt nur deren Meldungen (ohne die Notizen des Tages).
/// </summary>
/// <param name="close">Schließt die Seite.</param>
/// <param name="noteActions">Anlegen, Ändern, Abhaken und Löschen von Tagesnotizen; ohne sie (kein Buchungsrecht) sind die Notizen nur zu lesen.</param>
/// <param name="focus">Bittet die Ansicht, ein Element der Seite zu fokussieren (Name der Konstanten <see cref="NoteBox"/> und <see cref="AddNoteButton"/>).</param>
public sealed partial class MessagesPanelViewModel(Action close, DayNoteActions? noteActions = null, Action<string>? focus = null) : ViewModelBase, ISidePanel
{
    /// <summary>Name des Notizfelds in der Ansicht.</summary>
    public const string NoteBox = "DayNoteBox";

    /// <summary>Name des Knopfs „+ Notiz“ in der Ansicht.</summary>
    public const string AddNoteButton = "AddNoteButton";

    /// <summary>Längste Notiz in Zeichen (Server und Brief 5.16).</summary>
    public const int MaxNoteLength = 500;

    private IReadOnlyList<MessageItem> _active = [];
    private IReadOnlyList<MessageItem> _done = [];
    private CalendarNoteDto? _editing;
    private string _noteAtOpen = "";

    public string PanelTitle => "Meldungen";

    [ObservableProperty]
    public partial string? PanelSubtitle { get; set; }

    public PanelKind PanelKind => PanelKind.Messages;

    public string PanelIcon => Icons.Bell;

    public string PanelTabText => "Meldungen";

    /// <summary>Beim Schließen geht ein angefangener Notiztext verloren; dann fragt der Kalender „Änderungen verwerfen?“.</summary>
    public bool HasUnsavedChanges => IsEditingNote && NoteText != _noteAtOpen;

    /// <summary>Die Gruppen mit Karten, nach Filter; leere Gruppen fehlen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<MessageGroup> Groups { get; private set; } = [];

    public bool IsEmpty => Groups.Count == 0;

    public string EmptyText => Filter is null ? "Keine Meldungen." : $"Keine Meldungen zu {Filter.Name}.";

    /// <summary>Nur Meldungen zu dieser Person, diesem Raum oder Material (Klick auf das Symbol im Spaltenkopf).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilter), nameof(FilterText), nameof(EmptyText), nameof(ShowNotes))]
    public partial MessageSubject? Filter { get; private set; }

    public bool HasFilter => Filter is not null;

    public string FilterText => Filter is null ? "" : $"Gefiltert: {Filter.Name}";

    /// <summary>Ohne Verbindung: letzter Stand, Aktionen gehen nur mit Verbindung (Design-Brief 5.12, Punkt 8).</summary>
    [ObservableProperty]
    public partial bool IsOffline { get; private set; }

    public static string OfflineText => "Ohne Verbindung: letzter Stand. Aktionen gehen nur mit Verbindung.";

    // ---------- Notizen heute ----------

    /// <summary>Die Tagesnotizen ohne Uhrzeit in der Reihenfolge des Kalenders.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotes), nameof(NotesCount), nameof(ShowNotes))]
    public partial IReadOnlyList<DayNoteRow> Notes { get; private set; } = [];

    public bool HasNotes => Notes.Count > 0;

    public int NotesCount => Notes.Count;

    /// <summary>Wer buchen darf, legt Notizen an, ändert, hakt ab und löscht sie.</summary>
    public bool CanEditNotes => noteActions is not null;

    /// <summary>Die Gruppe steht ganz oben; ohne Notizen und ohne Recht zum Eintragen fehlt sie, und bei einem Filter auf eine Spalte auch.</summary>
    public bool ShowNotes => Filter is null && (HasNotes || CanEditNotes);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddNote), nameof(CanChangeNotes))]
    public partial bool IsEditingNote { get; private set; }

    /// <summary>„+ Notiz“ steht da, solange nicht schon eine Notiz geschrieben wird.</summary>
    public bool CanAddNote => CanEditNotes && !IsEditingNote;

    /// <summary>Ändern und Löschen sind gesperrt, solange ein Notizfeld offen ist: so geht kein angefangener Text verloren.</summary>
    public bool CanChangeNotes => !IsEditingNote;

    [ObservableProperty]
    public partial string NoteText { get; set; } = "";

    [ObservableProperty]
    public partial string? NoteError { get; private set; }

    [ObservableProperty]
    public partial bool IsSavingNote { get; private set; }

    /// <summary>„Notiz speichern“ beim Anlegen, „Änderung speichern“ beim Ändern.</summary>
    [ObservableProperty]
    public partial string NoteSaveText { get; private set; } = "Notiz speichern";

    /// <summary>Übernimmt die Tagesnotizen des Kalenders.</summary>
    public void ShowNotesOf(IReadOnlyList<CalendarNoteDto> notes) =>
        Notes = [.. notes.Select(n => new DayNoteRow(n)
        {
            ToggleCommand = noteActions is null ? null : new AsyncRelayCommand(() => noteActions.Toggle(n)),
            EditCommand = noteActions is null ? null : new RelayCommand(() => BeginEditNote(n)),
            DeleteCommand = noteActions is null ? null : new AsyncRelayCommand(() => noteActions.Delete(n)),
        })];

    /// <summary>„+ Notiz“ und die Taste „.“: das Notizfeld geht auf und bekommt den Fokus. Ein schon offenes Feld bleibt mit seinem Text, wie es ist.</summary>
    [RelayCommand]
    public void AddNote()
    {
        if (!CanEditNotes)
        {
            return;
        }

        if (!IsEditingNote)
        {
            BeginEditing(null, "");
        }

        focus?.Invoke(NoteBox);
    }

    private void BeginEditNote(CalendarNoteDto note)
    {
        if (IsEditingNote)
        {
            return;
        }

        BeginEditing(note, note.Text);
        focus?.Invoke(NoteBox);
    }

    private void BeginEditing(CalendarNoteDto? note, string text)
    {
        _editing = note;
        _noteAtOpen = text;
        NoteText = text;
        NoteError = null;
        NoteSaveText = note is null ? "Notiz speichern" : "Änderung speichern";
        IsEditingNote = true;
    }

    /// <summary>Speichert die Notiz (Knopf, Strg+Enter); ein leerer Text oder eine Ablehnung des Servers bleibt als Fehler am Feld stehen, der Text bleibt.</summary>
    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (noteActions is null || IsSavingNote)
        {
            return;
        }

        var text = NoteText.Trim();
        if (text.Length == 0)
        {
            NoteError = "Bitte einen Text eingeben.";
            return;
        }

        if (text.Length > MaxNoteLength)
        {
            NoteError = $"Höchstens {MaxNoteLength} Zeichen.";
            return;
        }

        IsSavingNote = true;
        try
        {
            NoteError = await (_editing is { } note ? noteActions.Change(note, text) : noteActions.Add(text)).ConfigureAwait(true);
        }
        finally
        {
            IsSavingNote = false;
        }

        if (NoteError is null)
        {
            EndEditing();
        }
    }

    /// <summary>Esc und „Abbrechen“: das Feld schließt, der Text ist verworfen, der Fokus geht zu „+ Notiz“ zurück.</summary>
    [RelayCommand]
    public void CancelNote() => EndEditing();

    private void EndEditing()
    {
        IsEditingNote = false;
        _editing = null;
        NoteText = "";
        _noteAtOpen = "";
        NoteError = null;
        focus?.Invoke(AddNoteButton);
    }

    // ---------- Meldungen ----------

    /// <summary>Übernimmt den Stand des Kalenders.</summary>
    public void Show(IReadOnlyList<MessageItem> active, IReadOnlyList<MessageItem> done, bool offline)
    {
        _active = active;
        _done = done;
        IsOffline = offline;
        Rebuild();
    }

    public void ApplyFilter(MessageSubject? subject)
    {
        Filter = subject;
        Rebuild();
    }

    [RelayCommand]
    private void ClearFilter() => ApplyFilter(null);

    private void Rebuild()
    {
        var groups = new List<MessageGroup>
        {
            new("Fehler", Filtered(_active.Where(m => m.Severity == MessageSeverity.Error))),
            new("Warnungen", Filtered(_active.Where(m => m.Severity == MessageSeverity.Warning))),
            new("Hinweise", Filtered(_active.Where(m => m.Severity == MessageSeverity.Notice))),
            new("Erledigt", Filtered(_done)),
        };
        Groups = [.. groups.Where(g => g.Count > 0)];
    }

    private IReadOnlyList<MessageItem> Filtered(IEnumerable<MessageItem> items) =>
        [.. Filter is { } filter ? items.Where(m => m.Subjects.Any(s => s.Id == filter.Id)) : items];

    [RelayCommand]
    private void Close() => close();
}

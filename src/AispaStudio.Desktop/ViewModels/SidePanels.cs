using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Die Art einer Seite in der Seitenleiste des Kalenders; je Art gibt es höchstens einen Reiter (Design-Brief 5.12, Punkt 7).</summary>
public enum PanelKind
{
    Appointment,
    NewEntry,
    Waitlist,
    Gaps,
    FreeTime,
    Absence,
    Request,
    RoomCare,
    Messages,
    MessageDetail,

    /// <summary>„KI und Hilfe“: der Assistent als Reiter (Design-Brief 5.28).</summary>
    Assistant,
}

/// <summary>
/// Eine rechte Seitenleiste im Kalender (Termin, Neuer Eintrag, Freie Zeit, Wunsch eintippen, Warteliste, Ausfall-Assistent …).
/// Der Rahmen zeigt für alle denselben Kopf: Titel und Untertitel links, das Schließen-Kreuz rechts oben; der Inhalt darunter scrollt.
/// Jede offene Seite ist unten ein Reiter mit Symbol und kurzem Text.
/// </summary>
public interface ISidePanel
{
    string PanelTitle { get; }

    string? PanelSubtitle { get; }

    IRelayCommand CloseCommand { get; }

    PanelKind PanelKind { get; }

    /// <summary>Pfad des Symbols (16 px) im Reiter.</summary>
    string PanelIcon { get; }

    /// <summary>Kurzer Text im Reiter, z. B. „Termin“ oder „Warteliste“.</summary>
    string PanelTabText { get; }

    /// <summary>Kennzeichen „in Bearbeitung“ am Reiter: hier entsteht etwas, das noch nicht gebucht ist (Ä149).</summary>
    bool PanelInProgress => false;

    /// <summary>Hinweis am Schließen-Kreuz: wie die Seite noch zu schließen ist.</summary>
    string PanelCloseTip => "Schließen (Esc)";

    /// <summary>Die Seite enthält Eingaben, die beim Schließen verloren gingen; dann fragt der Kalender „Änderungen verwerfen?“ (Design-Brief 5.27).</summary>
    bool HasUnsavedChanges => false;

    /// <summary>Aktion im Kopf links vom Kreuz, z. B. „Neu beginnen“ bei KI und Hilfe (Ä359); die meisten Seiten haben keine.</summary>
    IRelayCommand? PanelAction => null;

    /// <summary>Text des Knopfs der Aktion im Kopf.</summary>
    string? PanelActionText => null;

    /// <summary>Hinweis am Knopf der Aktion im Kopf.</summary>
    string? PanelActionTip => null;

    /// <summary>Die Aktion im Kopf ist gerade sinnvoll und wird gezeigt.</summary>
    bool HasPanelAction => false;
}

public partial class AppointmentViewModel : ISidePanel
{
    public string PanelTitle => Title;

    public string? PanelSubtitle => Treatment;

    public PanelKind PanelKind => PanelKind.Appointment;

    public string PanelIcon => Icons.Calendar;

    public string PanelTabText => "Termin";

    /// <summary>Geändert und noch nicht übernommen: Zeiten, Notiz, Kundendaten.</summary>
    public bool HasUnsavedChanges => (IsEditingTimes && (TimesPreparation != PreparationAtOpen || TimesFollowUp != FollowUpAtOpen)) || NoteChanged || CustomerForm is { HasChanges: true };
}

public partial class BookingViewModel : ISidePanel
{
    public string PanelTitle => "Neuer Termin";

    public string? PanelSubtitle => ColumnText;

    public PanelKind PanelKind => PanelKind.NewEntry;

    public string PanelIcon => Icons.Plus;

    /// <summary>„Neuer Termin · Andrea Auer“, sobald ein Gast gewählt ist (Ä149).</summary>
    public string PanelTabText => Customer is { } customer ? $"Neuer Termin · {customer.InHouseName}" : "Neuer Termin";

    /// <summary>Solange „Neuer Termin“ offen ist, ist er nicht gebucht; nach dem Buchen schließt die Seite.</summary>
    public bool PanelInProgress => true;

    public bool HasUnsavedChanges => Customer is not null || !string.IsNullOrWhiteSpace(CustomerQuery) || !string.IsNullOrWhiteSpace(Note);
}

public partial class GroupBookingViewModel : ISidePanel
{
    public string PanelTitle => "Paar oder Spa-Tag";

    public string? PanelSubtitle => DateText;

    public PanelKind PanelKind => PanelKind.NewEntry;

    public string PanelIcon => Icons.Plus;

    public string PanelTabText => "Paar oder Spa-Tag";

    public bool HasUnsavedChanges => Guests.Any(g => g.Search.HasCustomer) || !string.IsNullOrWhiteSpace(Note);
}

public partial class AreasViewModel : ISidePanel
{
    public string PanelTitle => "Sauna und Ruheraum";

    public string? PanelSubtitle => DateText;

    public PanelKind PanelKind => PanelKind.NewEntry;

    public string PanelIcon => Icons.Plus;

    public string PanelTabText => "Sauna";

    public bool HasUnsavedChanges => Search.HasCustomer || !string.IsNullOrWhiteSpace(Note);
}

public partial class CalendarEntryViewModel : ISidePanel
{
    public string PanelTitle => Title;

    public string? PanelSubtitle => Where;

    public PanelKind PanelKind => PanelKind.NewEntry;

    public string PanelIcon => Icons.Plus;

    public string PanelTabText => Title;

    /// <summary>Ein eingetragener Text oder geänderte Zeiten.</summary>
    public bool HasUnsavedChanges => !string.IsNullOrWhiteSpace(Text) || StartText != _startAtOpen || EndText != _endAtOpen;
}

public partial class AbsenceAssistantViewModel : ISidePanel
{
    public string PanelTitle => Title;

    public string? PanelSubtitle => Where;

    public PanelKind PanelKind => PanelKind.Absence;

    public string PanelIcon => Icons.Warning;

    public string PanelTabText => IsRoomClosure ? "Umbuchung" : "Ausfall";
}

public partial class FreeTimeViewModel : ISidePanel
{
    public string PanelTitle => "Freie Zeit suchen";

    public string? PanelSubtitle => DateText;

    public PanelKind PanelKind => PanelKind.FreeTime;

    public string PanelIcon => Icons.Search;

    public string PanelTabText => "Freie Zeit";
}

public partial class CalendarRequestViewModel : ISidePanel
{
    public string PanelTitle => "Wunsch eintippen";

    public string? PanelSubtitle => "Die KI liest den Wunsch, die Zeiten rechnet der Kalender.";

    public PanelKind PanelKind => PanelKind.Request;

    public string PanelIcon => Icons.Pencil;

    public string PanelTabText => "Wunsch";

    /// <summary>Der Wunsch ist eingetippt, aber noch nicht gesucht oder gebucht.</summary>
    public bool HasUnsavedChanges => !string.IsNullOrWhiteSpace(Text);
}

public sealed partial class WaitlistViewModel : ISidePanel
{
    public string PanelTitle => "Warteliste";

    public string? PanelSubtitle => LocationName;

    public PanelKind PanelKind => PanelKind.Waitlist;

    public string PanelIcon => Icons.Clock;

    public string PanelTabText => "Warteliste";
}

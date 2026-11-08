using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Art eines neuen Eintrags im Kalender; die Reihenfolge ist die der Umschaltleiste und der Tasten Alt+1 bis Alt+6.</summary>
public enum NewEntryKind
{
    Appointment,
    Group,
    Break,
    Block,
    Note,
    Area,
}

/// <summary>Ein Knopf der Umschaltleiste.</summary>
public sealed partial class NewEntryChoice(NewEntryKind kind, Action<NewEntryKind> select) : ViewModelBase
{
    public NewEntryKind Kind { get; } = kind;

    public string Label { get; } = NewEntryViewModel.LabelOf(kind);

    public string ToolTip { get; } = $"{NewEntryViewModel.TitleOf(kind)} (Alt+{(int)kind + 1})";

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(Kind);
}

/// <summary>
/// Das gemeinsame Eingabefenster für neue Einträge an einer Stelle des Kalenders. Die Leiste oben wechselt die Art; die eigentliche Eingabe
/// ist das jeweilige vorhandene ViewModel (<see cref="Content"/>). Eine erstmals gewählte Art übernimmt, wofür sie ein Feld hat (Beginn, Datum, Gast,
/// Behandlung, Notiz, Länge, getippter Text); eine schon besuchte Art steht beim Zurückwechseln unverändert wieder da, solange das Fenster offen ist (Ä153).
/// </summary>
public sealed partial class NewEntryViewModel : ViewModelBase
{
    private readonly Func<NewEntryKind, DateOnly?, ViewModelBase?> _create;
    private readonly Dictionary<NewEntryKind, ViewModelBase?> _visited = [];
    private Carry _carry = new();

    /// <summary>Was beim Wechsel mitgenommen wird; <see cref="Text"/> ist der Text von Notiz und Sperrzeit, <see cref="Note"/> die Notiz zum Termin.</summary>
    private sealed record Carry(string? StartText = null, string? Text = null, CustomerDto? Customer = null, DateOnly? Date = null,
        Guid? TreatmentId = null, string? Note = null, int? Minutes = null);

    /// <param name="create">Baut die Eingabe einer Art; das Datum ist das der bisherigen Eingabe, wenn sie eines hatte.</param>
    public NewEntryViewModel(IReadOnlyList<NewEntryKind> kinds, NewEntryKind kind, Func<NewEntryKind, DateOnly?, ViewModelBase?> create)
    {
        _create = create;
        Choices = [.. kinds.Select(k => new NewEntryChoice(k, kind => Select(kind)))];
        Kind = kind;
        Content = _visited[kind] = create(kind, null);
        UpdateChoices();
    }

    public IReadOnlyList<NewEntryChoice> Choices { get; }

    /// <summary>Die angeklickte Stelle (Spalte und Beginn), an der die Vorschau steht, wenn die Art selbst keinen Beginn kennt (Paar/Spa).</summary>
    public (CalendarColumn Column, DateTimeOffset Start)? Anchor { get; set; }

    /// <summary>Die Umschaltleiste gibt es nur, wenn es etwas umzuschalten gibt (nicht beim Bearbeiten eines Zettels).</summary>
    public bool CanSwitch => Choices.Count > 1;

    [ObservableProperty]
    public partial NewEntryKind Kind { get; private set; }

    /// <summary>Die Eingabe der gewählten Art.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Content { get; private set; }

    public bool Has(NewEntryKind kind) => Choices.Any(c => c.Kind == kind);

    /// <summary>Eine der besuchten Arten enthält Eingaben, die beim Schließen verloren gingen, auch wenn sie gerade nicht angezeigt wird.</summary>
    public bool HasUnsavedChanges => _visited.Values.OfType<ISidePanel>().Any(p => p.HasUnsavedChanges);

    /// <summary>Der angefangene Termin, auch wenn gerade eine andere Art angezeigt wird.</summary>
    internal BookingViewModel? VisitedBooking => _visited.GetValueOrDefault(NewEntryKind.Appointment) as BookingViewModel;

    /// <summary>
    /// Der Termin wird an eine andere Stelle verlegt (Ä85): er wird wieder angezeigt, und Pause, Sperrzeit und Notiz, die an der alten Spalte hingen,
    /// werden beim nächsten Wechsel neu gebaut.
    /// </summary>
    internal void ShowBookingForMove()
    {
        Select(NewEntryKind.Appointment);
        foreach (var kind in _visited.Where(v => v.Value is CalendarEntryViewModel).Select(v => v.Key).ToList())
        {
            _visited.Remove(kind);
        }
    }

    /// <summary>Wechselt die Art; false, wenn es sie hier nicht gibt (z. B. Pause in der Raumansicht).</summary>
    public bool Select(NewEntryKind kind)
    {
        if (!Has(kind))
        {
            return false;
        }

        if (kind != Kind)
        {
            _carry = TakeFrom(Content, _carry);
            Kind = kind;
            if (_visited.TryGetValue(kind, out var visited))
            {
                Content = visited;
            }
            else
            {
                var created = _create(kind, _carry.Date);
                GiveTo(created, _carry);
                Content = _visited[kind] = created;
            }

            UpdateChoices();
        }

        return true;
    }

    /// <summary>Alt+1 bis Alt+6.</summary>
    public bool SelectNumber(int number) => Enum.IsDefined(typeof(NewEntryKind), number - 1) && Select((NewEntryKind)(number - 1));

    private void UpdateChoices()
    {
        foreach (var choice in Choices)
        {
            choice.IsOn = choice.Kind == Kind;
        }
    }

    private static Carry TakeFrom(ViewModelBase? content, Carry before) => content switch
    {
        BookingViewModel b => before with
        {
            StartText = b.StartText,
            Customer = b.Customer ?? before.Customer,
            Date = b.Date,
            TreatmentId = b.Treatment?.Id ?? before.TreatmentId,
            Note = TextOf(b.Note),
            // Die Länge zählt erst, wenn der Termin angefangen ist (Gast gewählt); sonst behält die Pause ihren Vorschlag (gesetzliche Pause, Lücke).
            Minutes = b.Customer is not null && b.Treatment is { } treatment ? treatment.DurationMinutes : before.Minutes,
        },
        GroupBookingViewModel g => before with
        {
            StartText = g.StartText ?? before.StartText,
            Customer = g.Guests.FirstOrDefault()?.Search.Customer ?? before.Customer,
            Date = g.Date,
            TreatmentId = g.FirstTreatmentId ?? before.TreatmentId,
            Note = TextOf(g.Note),
        },
        AreasViewModel a => before with
        {
            StartText = a.FromText,
            Customer = a.Search.Customer ?? before.Customer,
            Date = a.Date,
            Note = TextOf(a.Note),
        },
        // Eine Pause trägt keinen eigenen Text; was vorher getippt war, bleibt für Notiz und Sperrzeit erhalten.
        CalendarEntryViewModel { Kind: CalendarEntryKind.Break } e => before with { StartText = e.StartText },
        // Der Text der Art „Notiz“ ist zugleich die Notiz zum Termin; der Grund einer Sperrzeit nicht.
        CalendarEntryViewModel e => before with
        {
            StartText = e.StartText,
            Text = TextOf(e.Text),
            Note = e.IsNote ? TextOf(e.Text) : before.Note,
        },
        _ => before,
    };

    /// <summary>Ein geleertes Feld zählt: die nächste Art bekommt dann nichts, auch wenn eine frühere Art einen Text hatte.</summary>
    private static string? TextOf(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static void GiveTo(ViewModelBase? content, Carry carry)
    {
        switch (content)
        {
            case BookingViewModel b:
                b.Adopt(carry.Date, carry.StartText, carry.Customer, carry.TreatmentId, carry.Note);
                break;
            case GroupBookingViewModel g:
                g.Adopt(carry.Date, carry.StartText, carry.Customer, carry.TreatmentId, carry.Note);
                break;
            case AreasViewModel a:
                a.Adopt(carry.StartText, carry.Customer, carry.Note);
                break;
            case CalendarEntryViewModel e:
                if (carry.StartText is { } carriedStart)
                {
                    e.MoveStartTo(carriedStart);
                }

                // Pause und Sperrzeit belegen so lange wie die Behandlung des Termins.
                if (e.HasEnd && carry.Minutes is { } minutes)
                {
                    e.LastFor(minutes);
                }

                if (e.HasText && (carry.Text ?? (e.IsNote ? carry.Note : null)) is { } text)
                {
                    e.Text = text;
                }

                break;
        }
    }

    public static string LabelOf(NewEntryKind kind) => kind switch
    {
        NewEntryKind.Appointment => "Termin",
        NewEntryKind.Group => "Paar/Spa",
        NewEntryKind.Break => "Pause",
        NewEntryKind.Block => "Sperrzeit",
        NewEntryKind.Note => "Notiz",
        _ => "Sauna",
    };

    public static string TitleOf(NewEntryKind kind) => kind switch
    {
        NewEntryKind.Group => "Paar oder Spa-Tag",
        NewEntryKind.Area => "Sauna-Eintritt",
        _ => LabelOf(kind),
    };
}

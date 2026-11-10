using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Verlaufsliste: ein Tageskopf oder ein Eintrag. Nur Einträge lassen sich wählen.</summary>
public abstract class ActivityListItem
{
    public virtual bool CanSelect => false;

    /// <summary>Name der Zeile für den Screenreader.</summary>
    public abstract string AccessibleName { get; }
}

/// <summary>Kopf eines Tages in der Liste, „Heute, Montag, 9. November“.</summary>
public sealed class ActivityDayHeader(DateOnly day, string title) : ActivityListItem
{
    public DateOnly Day { get; } = day;

    public string Title { get; } = title;

    public override string AccessibleName => Title;
}

/// <summary>Ein Eintrag des Verlaufs mit dem, was die Zeile zeigt: Uhrzeit, Symbol, Satz, Gast und Person, Pillen.</summary>
public sealed class ActivityRow(ActivityEntry entry, string time, string? detail) : ActivityListItem
{
    public ActivityEntry Entry { get; } = entry;

    public string Time { get; } = time;

    public string Text => Entry.Text;

    /// <summary>Gast und Person in einer Zeile, „Anna Huber · von Lena“; leer, wenn beides fehlt.</summary>
    public string? Detail { get; } = detail;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public override bool CanSelect => true;

    public override string AccessibleName => Entry.Text;

    public bool IsLate => Entry.LateCancellation == true;

    public bool IsOnline => Entry.Channel == ActivityChannel.Online;

    public bool IsAssistant => Entry.Channel == ActivityChannel.Assistant;

    public bool IsBackground => Entry.Channel == ActivityChannel.Background;

    public bool IsWarn => Entry.Kind is ActivityKind.Cancelled;

    public bool IsOk => Entry.Kind is ActivityKind.Paid or ActivityKind.Deposit;

    public bool IsAccent => Entry.Kind is ActivityKind.Booked or ActivityKind.CustomerCreated;

    /// <summary>Das Symbol der Art; die Farbe kommt aus der Bedeutung (Storno warn, Zahlung ok, Neu Akzent, sonst neutral).</summary>
    public string Icon => Entry.Kind switch
    {
        ActivityKind.Booked => Icons.CalendarPlus,
        ActivityKind.Moved => Icons.Move,
        ActivityKind.Cancelled => Icons.Close,
        ActivityKind.Restored => Icons.Undo,
        ActivityKind.StatusChanged => Icons.CheckCircle,
        ActivityKind.AppointmentChanged => Icons.Pencil,
        ActivityKind.Paid => Icons.Card,
        ActivityKind.Deposit => Icons.Card,
        ActivityKind.ReceiptVoided => Icons.Receipt,
        ActivityKind.CustomerCreated => Icons.User,
        ActivityKind.CustomerChanged => Icons.Pencil,
        ActivityKind.TreatmentChanged => Icons.Sliders,
        ActivityKind.RosterChanged => Icons.Calendar,
        ActivityKind.AccessChanged => Icons.Lock,
        ActivityKind.Request => Icons.Mail,
        _ => Icons.Info,
    };
}

/// <summary>Art-Chip der Seite: eine Gruppe von Arten, die zusammen an- und abgewählt werden.</summary>
public sealed partial class ActivityKindChip(string label, string tip, IReadOnlyList<ActivityKind> kinds, Action changed) : ObservableObject
{
    public string Label { get; } = label;

    public string Tip { get; } = tip;

    public IReadOnlyList<ActivityKind> Kinds { get; } = kinds;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Toggle()
    {
        IsOn = !IsOn;
        changed();
    }
}

/// <summary>Zeitraum-Chip „Heute“ bis „Zeitraum“.</summary>
public enum ActivityPeriod
{
    Today,
    Yesterday,
    Week,
    Month,
    Custom,
}

/// <summary>Eine Person im Auswahlfeld „Person“; <c>UserId</c> ist leer bei „Alle Personen“.</summary>
public sealed record ActivityPersonOption(string? UserId, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ein Kanal im Auswahlfeld „Kanal“; <c>null</c> bei „Alle Kanäle“.</summary>
public sealed record ActivityChannelOption(ActivityChannel? Channel, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Seite „Verlauf“: was im Betrieb passiert ist, neueste zuerst, nach Tagen gruppiert. Filtern, Suchen und Seitenweise rechnet der Server
/// (<c>GET api/locations/{id}/history</c>); die Seite zeigt nur an. Welche Arten es gibt, folgt den Rechten der Sitzung wie auf dem Server.
/// </summary>
public sealed partial class ActivityViewModel : ViewModelBase
{
    /// <summary>Wartezeit nach dem letzten Tastendruck, bevor die Suche den Server fragt.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private readonly IWorkstationApi _api;
    private readonly CurrentUser _user;
    private readonly TimeProvider _time;
    private int _version;
    private long? _nextBefore;
    private bool _suspended;
    private int _searchVersion;

    public ActivityViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time)
    {
        _api = api;
        _user = user;
        _time = time;
        KindChips = [.. BuildChips()];
        Channels = [new(null, "Alle Kanäle"), new(ActivityChannel.Reception, "Rezeption"), new(ActivityChannel.Online, "Online"),
            new(ActivityChannel.Assistant, "Terminannahme"), new(ActivityChannel.Background, "Hintergrund")];
        Channel = Channels[0];
        Person = People[0];
    }

    /// <summary>Der Standort, dessen Verlauf gezeigt wird: der im Kalender gewählte (setzt die Shell).</summary>
    public Func<LocationDto?>? LocationSource { get; set; }

    /// <summary>Die Liste mit Tagesköpfen und Einträgen, neueste zuerst.</summary>
    public ObservableCollection<ActivityListItem> Rows { get; } = [];

    /// <summary>Die Art-Chips, nur für Arten, die der Zugang sehen darf.</summary>
    public IReadOnlyList<ActivityKindChip> KindChips { get; }

    public IReadOnlyList<ActivityChannelOption> Channels { get; }

    /// <summary>„Alle Personen“ und die Personen, die im Zeitraum etwas getan haben (aus der Antwort des Servers, für jede Rolle).</summary>
    public ObservableCollection<ActivityPersonOption> People { get; } = [new(null, "Alle Personen")];

    public bool HasPersonChoice => People.Count > 1;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomPeriod), nameof(IsModified))]
    public partial ActivityPeriod Period { get; set; } = ActivityPeriod.Today;

    [ObservableProperty]
    public partial DateOnly? CustomFrom { get; set; }

    [ObservableProperty]
    public partial DateOnly? CustomUntil { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilter), nameof(IsModified))]
    public partial ActivityPersonOption Person { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilter), nameof(IsModified))]
    public partial ActivityChannelOption Channel { get; set; }

    [ObservableProperty]
    public partial ActivityRow? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoading), nameof(ShowEmpty), nameof(ShowMore))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowEmpty))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMore))]
    public partial bool HasMore { get; private set; }

    /// <summary>Wird einmal ausgelöst, wenn ein Eintrag mit Enter oder Doppelklick geöffnet wird; die Ansicht setzt hier den Fokus ins Seitenpanel.</summary>
    public event Action<ActivityEntry>? EntryOpened;

    public bool IsCustomPeriod => Period == ActivityPeriod.Custom;

    public bool HasError => Error is not null;

    public bool ShowLoading => IsLoading && Rows.Count == 0;

    public bool ShowMore => HasMore && !IsLoading;

    /// <summary>Ein Filter außer dem Zeitraum ist gesetzt: Art, Person, Kanal oder Suchtext.</summary>
    public bool HasFilter => KindChips.Any(c => c.IsOn) || Person.UserId is not null || Channel.Channel is not null || Query.Trim().Length > 0;

    /// <summary>Etwas weicht vom Anfangszustand ab (auch der Zeitraum); dann gibt es den Chip „Zurücksetzen“.</summary>
    public bool IsModified => HasFilter || Period != ActivityPeriod.Today;

    public bool ShowEmpty => !IsLoading && Error is null && _loaded && Rows.Count == 0;

    public string EmptyText => HasFilter ? "Nichts gefunden. Filter zurücksetzen?" : "Im gewählten Zeitraum ist nichts passiert.";

    private bool _loaded;

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(EmptyText));
        if (!_suspended)
        {
            _ = SearchLaterAsync();
        }
    }

    partial void OnPeriodChanged(ActivityPeriod value) => FilterChanged();

    partial void OnCustomFromChanged(DateOnly? value) => FilterChanged(customOnly: true);

    partial void OnCustomUntilChanged(DateOnly? value) => FilterChanged(customOnly: true);

    partial void OnPersonChanged(ActivityPersonOption value) => FilterChanged();

    partial void OnChannelChanged(ActivityChannelOption value) => FilterChanged();

    partial void OnSelectedChanged(ActivityRow? value)
    {
        OnPropertyChanged(nameof(SelectedItem));
        // Ist das Panel schon offen, folgt es der Auswahl (Pfeiltasten, Klick); geöffnet wird es erst mit Enter oder Doppelklick.
        if (value is not null && HasPanel)
        {
            ShowPanel(value.Entry);
        }
    }

    /// <summary>Die Auswahl der Liste; Tagesköpfe lassen sich nicht wählen.</summary>
    public ActivityListItem? SelectedItem
    {
        get => Selected;
        set
        {
            if (value is ActivityRow row)
            {
                Selected = row;
            }
            else if (value is null)
            {
                Selected = null;
            }
        }
    }

    private void FilterChanged(bool customOnly = false)
    {
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(EmptyText));
        if (_suspended || (customOnly && Period != ActivityPeriod.Custom))
        {
            return;
        }

        _searchVersion++;
        if (_loaded)
        {
            _ = ReloadAsync();
        }
    }

    private List<ActivityKindChip> BuildChips()
    {
        var chips = new List<ActivityKindChip>
        {
            new("Buchungen", "Neue Termine, gebucht von der Rezeption oder online.", [ActivityKind.Booked], OnKindChipChanged),
            new("Verschoben", "Termine, die auf eine andere Zeit, Person oder einen anderen Raum gelegt wurden.", [ActivityKind.Moved], OnKindChipChanged),
            new("Stornos", "Abgesagte Termine und Stornierungen, die zurückgenommen wurden.", [ActivityKind.Cancelled, ActivityKind.Restored], OnKindChipChanged),
            new("Status", "Einchecken, Erledigt, Nicht erschienen und andere Änderungen am Termin.", [ActivityKind.StatusChanged, ActivityKind.AppointmentChanged], OnKindChipChanged),
        };
        if (_user.Rights.Contains(AccessRight.Book))
        {
            chips.Add(new("Zahlungen", "Bezahlte Termine, Anzahlungen und stornierte Belege.", [ActivityKind.Paid, ActivityKind.Deposit, ActivityKind.ReceiptVoided], OnKindChipChanged));
            chips.Add(new("Anfragen", "Eingegangene Anfragen und was daraus wurde: gebucht, Zeiten vorgeschlagen, erledigt, Antwort freigegeben.", [ActivityKind.Request],
                OnKindChipChanged));
        }

        chips.Add(new("Neue Kunden", "Gäste, die neu angelegt wurden.", [ActivityKind.CustomerCreated], OnKindChipChanged));
        chips.Add(new("Kundendaten", "Änderungen an Kontakt, Notiz und Einstellungen eines Gastes.", [ActivityKind.CustomerChanged], OnKindChipChanged));
        chips.Add(new("Behandlungen", "Änderungen an Behandlungen und ihren Preisen.", [ActivityKind.TreatmentChanged], OnKindChipChanged));
        if (_user.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager)
        {
            chips.Add(new("Dienstplan", "Dienste, die eingetragen, geändert oder gelöscht wurden.", [ActivityKind.RosterChanged], OnKindChipChanged));
            chips.Add(new("Zugänge", "Änderungen an Zugängen, Rollen und Firmendaten.", [ActivityKind.AccessChanged, ActivityKind.CompanyChanged], OnKindChipChanged));
        }

        return chips;
    }

    private void OnKindChipChanged() => FilterChanged();

    /// <summary>Wartet nach dem letzten Tastendruck <see cref="SearchDelay"/>, dann folgt genau eine Anfrage.</summary>
    private async Task SearchLaterAsync()
    {
        var version = ++_searchVersion;
        await Task.Delay(SearchDelay, _time).ConfigureAwait(true);
        if (version == _searchVersion && _loaded)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void SetPeriod(ActivityPeriod period)
    {
        if (period == ActivityPeriod.Custom && CustomFrom is null)
        {
            _suspended = true;
            CustomFrom = CustomUntil = Today();
            _suspended = false;
        }

        Period = period;
    }

    /// <summary>Alle Filter zurück: Heute, keine Arten, alle Personen und Kanäle, kein Suchtext; dann genau ein Neuladen.</summary>
    [RelayCommand]
    public async Task ResetAsync()
    {
        _searchVersion++;
        _suspended = true;
        Query = "";
        Period = ActivityPeriod.Today;
        Person = People[0];
        Channel = Channels[0];
        foreach (var chip in KindChips)
        {
            chip.IsOn = false;
        }

        _suspended = false;
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(EmptyText));
        if (_loaded)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Esc im Suchfeld: Suchtext leeren (die Anfrage folgt nach der Wartezeit).</summary>
    public void ClearSearch() => Query = "";

    /// <summary>Enter oder Doppelklick auf einen Eintrag: meldet ihn dem Seitenpanel.</summary>
    [RelayCommand]
    public void OpenSelected()
    {
        if (Selected is { } row)
        {
            ShowPanel(row.Entry);
            EntryOpened?.Invoke(row.Entry);
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Zone()).DateTime);

    private TimeZoneInfo Zone() => LocationSource?.Invoke() is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : _time.LocalTimeZone;

    /// <summary>Von und bis als Tage der Ortszeit des Standorts, beide inklusive.</summary>
    private (DateOnly From, DateOnly Until) Range()
    {
        var today = Today();
        return Period switch
        {
            ActivityPeriod.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            ActivityPeriod.Week => (today.AddDays(-6), today),
            ActivityPeriod.Month => (today.AddDays(-29), today),
            ActivityPeriod.Custom => (CustomFrom ?? today, CustomUntil ?? today),
            _ => (today, today),
        };
    }
}

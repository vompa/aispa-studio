using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Auswahl in einem Abschnitt: eine Behandlung, Zeit in Sauna oder Ruheraum (<c>AreaId</c>) oder „nichts“ für diesen Gast.</summary>
public sealed record TreatmentChoice(Guid? Id, string Text, Guid? AreaId = null, int Minutes = 0)
{
    public static readonly TreatmentChoice None = new(null, "– nichts –");

    public static TreatmentChoice Of(TreatmentDto t) => new(t.Id, $"{t.Name} · {t.DurationMinutes} Min.");

    public static TreatmentChoice Area(RoomDto area, int minutes) => new(null, $"{area.Name} · {minutes} Min.", area.Id, minutes);

    public bool IsNone => Id is null && AreaId is null;
}

/// <summary>Ein Gast der gemeinsamen Buchung; bei Gruppen auch ohne Namen („Name folgt“).</summary>
public partial class GroupGuestViewModel(CustomerSearchViewModel search, Action<GroupGuestViewModel> remove, Action? nameLaterChanged = null)
    : ViewModelBase
{
    public CustomerSearchViewModel Search { get; } = search;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial int Number { get; set; }

    public string Label => $"Gast {Number}";

    /// <summary>Behandlung des ersten Abschnitts; sie steht in der Karte des Gastes.</summary>
    [ObservableProperty]
    public partial GroupEntryViewModel? FirstEntry { get; set; }

    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    /// <summary>Gast ohne Namen: läuft auf die buchende Person, der Name wird im Termin nachgetragen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSearch))]
    public partial bool NameLater { get; set; }

    public bool ShowSearch => !NameLater;

    partial void OnNameLaterChanged(bool value)
    {
        NameChanged();
        nameLaterChanged?.Invoke();
    }

    /// <summary>Im Ablauf: Vorname, solange niemand gewählt ist „Gast 1“.</summary>
    public string ShortName => !NameLater && Search.Customer?.FirstName is { Length: > 0 } first ? first : Label;

    /// <summary>In den Abschnitten und Meldungen: der volle Name, solange niemand gewählt ist „Gast 2“.</summary>
    public string FullName => !NameLater && Search.Customer is { } customer ? customer.InHouseName : Label;

    public void NameChanged()
    {
        OnPropertyChanged(nameof(ShortName));
        OnPropertyChanged(nameof(FullName));
    }

    [RelayCommand]
    private void Remove() => remove(this);
}

/// <summary>Behandlung eines Gastes in einem Abschnitt.</summary>
public partial class GroupEntryViewModel(GroupGuestViewModel guest, IReadOnlyList<TreatmentChoice> choices, TreatmentChoice choice, Action changed)
    : ViewModelBase
{
    public GroupGuestViewModel Guest { get; } = guest;

    public IReadOnlyList<TreatmentChoice> Choices { get; } = choices;

    [ObservableProperty]
    public partial TreatmentChoice Choice { get; set; } = choice;

    partial void OnChoiceChanged(TreatmentChoice value) => changed();
}

/// <summary>Abschnitt: was darin steht, beginnt gleichzeitig; der nächste Abschnitt folgt nach dem Ende plus Pause.</summary>
public partial class GroupStepViewModel(Action<GroupStepViewModel> remove) : ViewModelBase
{
    public ObservableCollection<GroupEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Behandlungen";

    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    /// <summary>Der erste Abschnitt steht in den Karten der Gäste, nur die weiteren haben eine eigene Karte.</summary>
    [ObservableProperty]
    public partial bool IsFirst { get; set; }

    [RelayCommand]
    private void Remove() => remove(this);
}

/// <summary>Freie Startzeit zum Anklicken.</summary>
public sealed record GroupSlotOption(GroupSlot Slot, string Text);

/// <summary>
/// Paar- oder Spa-Tag-Buchung an der Rezeption: Gäste wählen, je Abschnitt ihre Behandlungen. Der Server sucht Zeiten, zu denen
/// alles zusammenpasst (Personen, Räume, Paarsuite), und bucht alles oder nichts (ADR 0005, Belegung nur im Backend).
/// </summary>
public partial class GroupBookingViewModel : ViewModelBase
{
    public const int MaxGuests = 8;
    public const int MaxSteps = 4;

    private readonly IWorkstationApi _api;
    private readonly LocationResources _resources;
    private readonly DateOnly _today;
    private DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly Func<GroupBookingResult, bool, Task> _booked;
    private readonly Action _close;
    private readonly IReadOnlyList<TreatmentChoice> _choices;
    private int _searchVersion;
    private string? _searchedKey;
    private Task? _search;

    public GroupBookingViewModel(IWorkstationApi api, LocationResources resources, DateOnly date, TimeZoneInfo zone,
        Func<GroupBookingResult, bool, Task> booked, Action close, TimeProvider? time = null)
    {
        _api = api;
        _resources = resources;
        _date = date;
        _zone = zone;
        _today = DateOnly.FromDateTime(DayLayout.Local((time ?? TimeProvider.System).GetUtcNow(), zone));
        DateText = DateInputParser.Format(date);
        _booked = booked;
        _close = close;
        _choices = [TreatmentChoice.None, .. resources.Treatments.Select(TreatmentChoice.Of),
            .. resources.Rooms.Where(r => r.IsArea).SelectMany(r => AreaMinutes.Select(m => TreatmentChoice.Area(r, m)))];
        Guests.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanAddGuest));
        Steps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanAddStep));
        BookCommand.CanExecuteChanged += (_, _) => OnPropertyChanged(nameof(BookBlocker));

        AddGuest();
        AddGuest();
        AddStep();
        var first = _choices.Count > 1 ? _choices[1] : TreatmentChoice.None;
        foreach (var entry in Steps[0].Entries)
        {
            entry.Choice = first;
        }
    }

    /// <summary>Das Datumsfeld; ein unlesbarer oder früherer Tag lässt den bisherigen Tag stehen und sperrt das Buchen.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial string DateText { get; set; } = "";

    /// <summary>Der erste wählbare Tag: heute am Standort.</summary>
    public DateOnly Today => _today;

    partial void OnDateTextChanged(string value)
    {
        if (DateInputParser.TryParse(value, _today) is { } date && date >= _today && date != _date)
        {
            _date = date;
            _ = SearchSlotsAsync();
        }
    }

    /// <summary>Der Tag der Suche (der zuletzt gültige im Datumsfeld).</summary>
    public DateOnly Date => _date;

    /// <summary>Beginn der gewählten Zeit, ohne Verbindung der eingetragene; ohne beides <c>null</c>.</summary>
    public string? StartText => Slot?.Text ?? (IsOffline ? OfflineStartText : null);

    /// <summary>Die Behandlung von Gast 1 im ersten Abschnitt.</summary>
    public Guid? FirstTreatmentId => Steps.FirstOrDefault()?.Entries.FirstOrDefault()?.Choice.Id;

    /// <summary>Beginn aus „Neuer Termin“: unter den gefundenen Zeiten ist diese gewählt, wenn es sie gibt (Ä153).</summary>
    private string? _preferredStart;

    /// <summary>
    /// Beim Wechsel der Art (Ä153): der Gast wird Gast 1 mit der Behandlung im ersten Abschnitt, dazu Datum, Beginn und Notiz; was schon gewählt ist, bleibt.
    /// </summary>
    internal void Adopt(DateOnly? date, string? startText, CustomerDto? customer, Guid? treatmentId, string? note)
    {
        if (date is { } day)
        {
            DateText = DateInputParser.Format(day);
        }

        if (startText is not null)
        {
            _preferredStart = startText;
            OfflineStartText = startText;
        }

        if (Guests.FirstOrDefault() is { } guest)
        {
            if (customer is not null)
            {
                guest.Search.Customer = customer;
            }

            if (treatmentId is { } id && guest.FirstEntry is { } entry && entry.Choices.FirstOrDefault(c => c.Id == id) is { } choice)
            {
                entry.Choice = choice;
            }
        }

        if (string.IsNullOrWhiteSpace(Note))
        {
            Note = note;
        }
    }

    public ObservableCollection<GroupGuestViewModel> Guests { get; } = [];

    public ObservableCollection<GroupStepViewModel> Steps { get; } = [];

    public ObservableCollection<GroupSlotOption> Slots { get; } = [];

    public IReadOnlyList<int> GapOptions { get; } = [0, 5, 10, 15, 20, 30, 45, 60];

    /// <summary>Wie lange Sauna oder Ruheraum im Baukasten dauern können.</summary>
    public static IReadOnlyList<int> AreaMinutes { get; } = [30, 60, 90];

    [ObservableProperty]
    public partial int Gap { get; set; } = 15;

    /// <summary>Baukasten: der Planer stellt die Abschnitte so um, dass der Tag am frühesten endet, und lässt kurz warten.</summary>
    [ObservableProperty]
    public partial bool FlexibleOrder { get; set; }

    partial void OnFlexibleOrderChanged(bool value) => _ = SearchSlotsAsync();

    public bool CanAddGuest => Guests.Count < MaxGuests;

    public bool CanAddStep => Steps.Count < MaxSteps;

    public bool ShowGap => Steps.Count > 1;

    /// <summary>Hinweis über den freien Zeiten: zu wenig gewählt, Suche läuft, nichts frei.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlotsNote))]
    public partial string? SlotsNote { get; private set; }

    public bool HasSlotsNote => SlotsNote is not null;

    partial void OnSlotsNoteChanged(string? value) => BookCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Plan), nameof(SharedRoom), nameof(BookText))]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial GroupSlotOption? Slot { get; set; }

    /// <summary>Ablauf zur gewählten Zeit: wer wann was bei wem in welchem Raum.</summary>
    public IReadOnlyList<string> Plan
    {
        get
        {
            if (Slot is null)
            {
                return [];
            }

            var entries = Entries();
            var treatments = Slot.Slot.Items.Zip(entries.Where(e => e.TreatmentId is not null), (p, e) => (p.Start,
                Text: $"{DayLayout.TimeText(p.Start, p.End, _zone)}  {e.Guest.ShortName}: {TreatmentName(e.TreatmentId!.Value)} bei {EmployeeName(p.EmployeeId)}, {RoomName(p.RoomId)}"));
            var areas = (Slot.Slot.Areas ?? []).Zip(entries.Where(e => e.AreaId is not null), (a, e) => (Start: a.From,
                Text: $"{DayLayout.TimeText(a.From, a.Until, _zone)}  {e.Guest.ShortName}: {RoomName(a.RoomId)}"));
            return [.. treatments.Concat(areas).OrderBy(p => p.Start).Select(p => p.Text)];
        }
    }

    public bool SharedRoom => Slot?.Slot.SharedRoom == true;

    public string BookText => Slot is not null ? $"{Slot.Text} alles buchen" : IsOffline ? $"{OfflineStartText} vormerken" : "Alles buchen";

    /// <summary>Server nicht erreichbar: freie Zeiten gibt es nicht, die Rezeption trägt den Beginn selbst ein.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookText))]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial bool IsOffline { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookText))]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial string OfflineStartText { get; set; } = "10:00";

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Die Zeit ist nicht frei: Liste der Belegungen in Ortszeit statt eines Fehlertextes.</summary>
    [ObservableProperty]
    public partial ConflictNotice? Conflict { get; private set; }

    /// <summary>Was der Kalender der Konfliktmeldung mitgibt: Termin markieren, „Zum Konflikt“, Gegenstück von Person und Raum.</summary>
    public ConflictLinks? Links { get; init; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial bool Busy { get; set; }

    partial void OnGapChanged(int value) => _ = SearchSlotsAsync();

    [RelayCommand(CanExecute = nameof(CanAddGuest))]
    private void AddGuest()
    {
        var search = new CustomerSearchViewModel(_api);
        var guest = new GroupGuestViewModel(search, RemoveGuest, () =>
        {
            OnPropertyChanged(nameof(Plan));
            OnPropertyChanged(nameof(NameLaterNote));
            BookCommand.NotifyCanExecuteChanged();
        });
        search.Changed += () =>
        {
            guest.NameChanged();
            OnPropertyChanged(nameof(Plan));
            OnPropertyChanged(nameof(NameLaterNote));
            BookCommand.NotifyCanExecuteChanged();
        };
        Guests.Add(guest);
        foreach (var step in Steps)
        {
            step.Entries.Add(new GroupEntryViewModel(guest, _choices, TreatmentChoice.None, ChoicesChanged));
        }

        Renumber();
        AddGuestCommand.NotifyCanExecuteChanged();
    }

    private void RemoveGuest(GroupGuestViewModel guest)
    {
        if (Guests.Count <= 1)
        {
            return;
        }

        Guests.Remove(guest);
        foreach (var step in Steps)
        {
            foreach (var entry in step.Entries.Where(e => e.Guest == guest).ToList())
            {
                step.Entries.Remove(entry);
            }
        }

        Renumber();
        AddGuestCommand.NotifyCanExecuteChanged();
        ChoicesChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddStep))]
    private void AddStep()
    {
        var step = new GroupStepViewModel(RemoveStep);
        foreach (var guest in Guests)
        {
            step.Entries.Add(new GroupEntryViewModel(guest, _choices, TreatmentChoice.None, ChoicesChanged));
        }

        Steps.Add(step);
        Renumber();
        AddStepCommand.NotifyCanExecuteChanged();
    }

    private void RemoveStep(GroupStepViewModel step)
    {
        if (Steps.Count <= 1)
        {
            return;
        }

        Steps.Remove(step);
        Renumber();
        AddStepCommand.NotifyCanExecuteChanged();
        ChoicesChanged();
    }

    private void Renumber()
    {
        for (var i = 0; i < Guests.Count; i++)
        {
            Guests[i].Number = i + 1;
            Guests[i].CanRemove = Guests.Count > 1;
            Guests[i].FirstEntry = Steps.Count > 0 ? Steps[0].Entries.FirstOrDefault(e => e.Guest == Guests[i]) : null;
            Guests[i].NameChanged();
        }

        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].Title = Steps.Count > 1 ? $"Abschnitt {i + 1}" : "Behandlungen";
            Steps[i].CanRemove = Steps.Count > 1;
            Steps[i].IsFirst = i == 0;
        }

        OnPropertyChanged(nameof(ShowGap));
        OnPropertyChanged(nameof(ShowFlexible));
    }

    /// <summary>Umstellen lohnt erst ab zwei Abschnitten; mehr als vier rechnet der Planer nicht durch.</summary>
    public bool ShowFlexible => Steps.Count > 1;

    private void ChoicesChanged()
    {
        BookCommand.NotifyCanExecuteChanged();
        _ = SearchSlotsAsync();
    }

    private sealed record Entry(GroupGuestViewModel Guest, Guid? TreatmentId, Guid? AreaId, int Minutes, int Step);

    /// <summary>Gewählte Teile in der Reihenfolge der Anfrage; leere Abschnitte fallen weg, die übrigen werden durchnummeriert.</summary>
    private List<Entry> Entries() =>
    [
        .. Steps.Where(s => s.Entries.Any(e => !e.Choice.IsNone))
            .SelectMany((s, step) => s.Entries.Where(e => !e.Choice.IsNone)
                .Select(e => new Entry(e.Guest, e.Choice.Id, e.Choice.AreaId, e.Choice.Minutes, step))),
    ];

    private Guid CustomerOf(Entry e, bool placeholders) =>
        placeholders ? Placeholder(Guests.IndexOf(e.Guest))
        : e.Guest.NameLater && Booker is { } booker ? booker.Id
        : e.Guest.Search.Customer is { } c ? c.Id : Placeholder(Guests.IndexOf(e.Guest));

    /// <summary>Wer bucht: der erste Gast mit Namen. Gäste ohne Namen laufen bis zum Nachtragen auf diese Person.</summary>
    private CustomerDto? Booker => Guests.FirstOrDefault(g => !g.NameLater && g.Search.Customer is not null)?.Search.Customer;

    /// <summary>Hinweis unter den Gästen, sobald jemand „Name folgt“ hat.</summary>
    public string? NameLaterNote => !Guests.Any(g => g.NameLater) ? null
        : Booker is { } booker
            ? $"Gäste ohne Namen laufen auf {$"{booker.FirstName} {booker.LastName}".Trim()}. Die Namen lassen sich später im Termin nachtragen; "
                + "Sauna und Ruheraum nur für Gäste mit Namen."
            : "Mindestens ein Gast braucht einen Namen: wer bucht.";

    /// <summary>Für die Suche reicht es, die Gäste zu unterscheiden; Gewählte gehen mit ihrer Id mit.</summary>
    private List<GroupBookingItem> Items(bool placeholders) =>
        [.. Entries().Where(e => e.TreatmentId is not null).Select(e => new GroupBookingItem(e.TreatmentId!.Value, CustomerOf(e, placeholders), e.Step,
            PlaceholderGuest: !placeholders && e.Guest.NameLater ? e.Guest.Number : null))];

    private List<GroupAreaItem> Areas(bool placeholders) =>
        [.. Entries().Where(e => e.AreaId is not null).Select(e => new GroupAreaItem(e.AreaId!.Value, CustomerOf(e, placeholders), e.Step, e.Minutes))];

    private bool Flexible => FlexibleOrder && Entries().Select(e => e.Step).Distinct().Count() > 1;

    private static Guid Placeholder(int guest) => Guid.Parse($"00000000-0000-4000-8000-{guest + 1:000000000000}");

    /// <summary>
    /// Sucht freie Startzeiten neu, wenn sich Behandlungen, Abschnitte oder Pause geändert haben; die neueste Antwort gewinnt.
    /// Ist dieselbe Suche schon unterwegs, wird auf sie gewartet.
    /// </summary>
    public Task SearchSlotsAsync()
    {
        var items = Items(placeholders: true);
        var areas = Areas(placeholders: true);
        var key = string.Join(";", items.Select(i => $"{i.TreatmentId}/{i.CustomerId}/{i.Step}"))
            + "|" + string.Join(";", areas.Select(a => $"{a.RoomId}/{a.CustomerId}/{a.Step}/{a.Minutes}")) + $"|{Gap}|{Flexible}|{_date:O}";
        if (key == _searchedKey && _search is not null)
        {
            return _search;
        }

        _searchedKey = key;
        return _search = SearchAsync(items, areas, ++_searchVersion);
    }

    private async Task SearchAsync(List<GroupBookingItem> items, List<GroupAreaItem> areas, int version)
    {
        Slots.Clear();
        Slot = null;
        if (GuestsWithoutChoice().Any())
        {
            SlotsNote = "Bitte erst für jeden Gast eine Behandlung wählen.";
            return;
        }

        if (items.Count == 0 || items.Count + areas.Count < 2)
        {
            SlotsNote = items.Count == 0 && areas.Count > 0
                ? "Mindestens eine Behandlung wählen; Sauna oder Ruheraum allein bucht „Sauna und Ruheraum“."
                : "Mindestens zwei Teile wählen, davon eine Behandlung.";
            return;
        }

        SlotsNote = "Suche Zeiten, zu denen alles passt …";
        var result = await _api.PostAsync<List<GroupSlot>>($"api/locations/{_resources.Location.Id}/appointment-groups/availability",
            new GroupAvailabilityRequest(_date, items, Gap, areas.Count == 0 ? null : areas, Flexible)).ConfigureAwait(true);
        if (version != _searchVersion)
        {
            return;
        }

        IsOffline = result.Status == 0 && _api.Offline is not null;
        if (result.Value is null)
        {
            _searchedKey = null;
            SlotsNote = IsOffline
                ? "Ohne Verbindung: Beginn selbst eintragen. Ob alles zusammenpasst und wer behandelt, prüft der Server beim Abgleich."
                : result.Error;
            return;
        }

        foreach (var slot in result.Value)
        {
            Slots.Add(new GroupSlotOption(slot, DayLayout.Local(slot.Start, _zone).ToString("HH:mm", CultureInfo.InvariantCulture)));
        }

        SlotsNote = Slots.Count == 0 ? NoCommonTimeNote : null;
        Slot = Slots.FirstOrDefault(s => s.Text == _preferredStart) ?? Slots.FirstOrDefault();
        // Der übernommene Beginn gilt nur für die erste Suche mit Zeiten; danach wählt jede neue Suche wie sonst die erste Zeit.
        if (Slots.Count > 0)
        {
            _preferredStart = null;
        }
    }

    private const string NoCommonTimeNote = "An diesem Tag ist keine gemeinsame Zeit frei. Anderen Tag wählen.";

    private bool NoCommonTime => SlotsNote == NoCommonTimeNote;

    private bool CanBook => BookBlocker is null;

    private IEnumerable<GroupGuestViewModel> GuestsWithoutChoice() =>
        Guests.Where(g => !Steps.Any(step => step.Entries.Any(e => e.Guest == g && !e.Choice.IsNone)));

    /// <summary>Der erste fehlende Punkt in der Reihenfolge der Maske, solange „Alles buchen“ gesperrt ist; sonst <c>null</c>.</summary>
    public string? BookBlocker
    {
        get
        {
            var day = DateInputParser.TryParse(DateText, _today);
            if (day is null)
            {
                return "Verfügbar, sobald ein gültiges Datum eingetragen ist.";
            }

            if (day < _today)
            {
                return "Verfügbar, sobald ein Tag ab heute gewählt ist.";
            }

            if (Guests.FirstOrDefault(g => !g.NameLater && !g.Search.HasCustomer) is { } unnamed)
            {
                return $"Verfügbar, sobald {unnamed.Label} einen Namen hat oder „Name folgt“ gilt.";
            }

            if (Booker is null)
            {
                return "Verfügbar, sobald mindestens ein Gast einen Namen hat.";
            }

            if (GuestsWithoutChoice().FirstOrDefault() is { } without)
            {
                return $"Verfügbar, sobald für {without.FullName} eine Behandlung gewählt ist.";
            }

            var entries = Entries();
            if (entries.Count < 2 || entries.All(e => e.TreatmentId is null))
            {
                return "Verfügbar, sobald mindestens zwei Teile gewählt sind, davon eine Behandlung.";
            }

            if (entries.Any(e => e.Guest.NameLater && e.AreaId is not null))
            {
                return "Verfügbar, sobald Sauna und Ruheraum nur bei Gästen mit Namen stehen.";
            }

            if (Slot is null && !(IsOffline && TryOfflineStart(out _)))
            {
                return IsOffline ? "Verfügbar, sobald der Beginn eingetragen ist, z. B. 10:00." : NoCommonTime ? "Verfügbar, sobald eine gemeinsame Zeit frei ist; anderen Tag wählen." : "Verfügbar, sobald eine freie Zeit gewählt ist.";
            }

            return Busy ? "Verfügbar, sobald die laufende Buchung fertig ist." : null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookAsync()
    {
        DateTimeOffset start;
        if (Slot is not null)
        {
            start = Slot.Slot.Start;
        }
        else if (!TryOfflineStart(out start))
        {
            return;
        }

        Error = null;
        Conflict = null;
        Busy = true;
        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        var names = string.Join(" und ", Guests.Select(g => g.NameLater ? g.Label : g.Search.Name));
        var areas = Areas(placeholders: false);
        var request = new GroupBookingRequest(_resources.Location.Id, start, Items(placeholders: false), Gap, note, Guid.CreateVersion7(),
            areas.Count == 0 ? null : areas, Flexible);
        var result = await _api.BookGroupAsync(request,
            $"{names}, {DayLayout.DateText(_date)} ab {DayLayout.Local(start, _zone):HH:mm}: "
            + $"{request.Items.Count} Behandlungen{(areas.Count > 0 ? $" und {areas.Count}× Sauna oder Ruheraum" : "")} gemeinsam buchen")
            .ConfigureAwait(true);
        Busy = false;
        if (result.Value is null)
        {
            Conflict = ConflictNotice.From(result.Rejection, _zone, _date, Links ?? new ConflictLinks(null));
            Error = Conflict is null ? result.Error : null;
            return;
        }

        await _booked(result.Value, result.Queued).ConfigureAwait(true);
    }

    private bool TryOfflineStart(out DateTimeOffset start)
    {
        start = default;
        if (TimeInputParser.TryParse(OfflineStartText) is not { } time)
        {
            return false;
        }

        start = DayLayout.LocalToUtc(_date, time, _zone);
        return true;
    }

    [RelayCommand]
    private void Close() => _close();

    private string TreatmentName(Guid id) => _resources.Treatments.FirstOrDefault(t => t.Id == id)?.Name ?? "–";

    private string EmployeeName(Guid id) => _resources.Employees.FirstOrDefault(e => e.Id == id)?.InHouseName() ?? "–";

    private string RoomName(Guid id) => _resources.Rooms.FirstOrDefault(r => r.Id == id)?.Name ?? "–";
}

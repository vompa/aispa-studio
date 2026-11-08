using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine gewählte freie Zeit, mit dem Gast (wenn gewählt) und ob sie gleich gebucht werden soll (Gast gewählt) oder erst im Fenster „Neuer Termin“ landet;
/// <c>Note</c> ist die Notiz, die aus „Neuer Termin“ mitkam.
/// </summary>
public sealed record FreeTimeBooking(AvailableSlot Slot, TreatmentDto Treatment, CustomerDto? Guest, bool Direct, string? Note = null);

/// <summary>
/// Was „Freie Zeit suchen“ schon ausgefüllt hat, wenn es aus der Warteliste, dem Posteingang oder dem Assistenten aufgerufen wird.
/// <c>Until</c> begrenzt die Suche auf den Zeitraum des Wunsches (Warteliste).
/// </summary>
/// <param name="Note">Aus „Neuer Termin“: die schon getippte Notiz; sie landet im gebuchten Termin.</param>
/// <param name="BookLabel">Aus dem Posteingang: Text des Hauptknopfs („Vormerken und vorschlagen“, ADR 0083); der Gast ist dann die Anfrage, das Feld „Gast“ entfällt.</param>
public sealed record FreeTimePrefill(Guid? TreatmentId = null, Guid? EmployeeId = null, Guid? RoomId = null, CustomerDto? Guest = null,
    DayPart Part = DayPart.Any, DateOnly? Until = null, string? Note = null, string? BookLabel = null);

/// <summary>
/// „Freie Zeit suchen“ (Strg+N): Behandlung wählen, alle passenden Zeiten des Tages leuchten im Kalender. Es gibt immer mindestens drei Vorschläge:
/// ist am Tag weniger frei, sucht die Liste an den folgenden Tagen (bis 14) weiter und nennt dann Tag und Uhrzeit. Person, Raum, Sprache,
/// Wunsch Therapeutin/Therapeut und Tageszeit grenzen ein; ein gewählter Gast bringt seine Wünsche mit (feste Person, Sprache, Wochentage und Uhrzeiten)
/// und lässt sich mit einem Klick direkt buchen.
/// Welche Zeit mit welcher Person, welchem Raum und Gerät geht, rechnet der Server (<c>ResourcePlanner</c>); hier wird nur gezeigt und gewählt.
/// </summary>
public partial class FreeTimeViewModel : ViewModelBase
{
    /// <summary>So viele Vorschläge liefert die Suche mindestens, solange irgendwo etwas frei ist.</summary>
    public const int MinProposals = 3;

    /// <summary>So weit in die Zukunft sucht die Liste höchstens (Tage ab dem gezeigten Tag).</summary>
    public const int SearchDays = 14;

    private readonly IWorkstationApi _api;
    private readonly LocationResources _resources;
    private readonly DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly DateTimeOffset _notBefore;
    private readonly Func<FreeTimeBooking, Task> _book;
    private readonly Action _close;
    private readonly DateOnly? _until;
    private readonly string? _bookLabel;
    private int _version;
    private int _suspended;
    private readonly string? _note;

    public FreeTimeViewModel(IWorkstationApi api, LocationResources resources, DateOnly date, TimeZoneInfo zone, DateTimeOffset notBefore,
        Func<FreeTimeBooking, Task> book, Action close, TreatmentDto? treatment = null, FreeTimePrefill? prefill = null)
    {
        _api = api;
        _resources = resources;
        _date = date;
        _zone = zone;
        _notBefore = notBefore;
        _book = book;
        _close = close;
        _until = prefill?.Until;
        _bookLabel = prefill?.BookLabel;
        _note = prefill?.Note;
        Treatments = [.. resources.Treatments.OrderBy(t => t.Category).ThenBy(t => t.Name)];
        People = [new PersonChoice(null, "Alle"), .. resources.Employees.Select(e => new PersonChoice(e.Id, e.InHouseName()))];
        Person = People[0];
        Rooms = RoomsFor(null);
        Room = Rooms[0];
        var spoken = resources.Employees.SelectMany(e => e.Languages ?? []).ToHashSet();
        Languages = [new LanguageChoice(null, "egal"), .. GuestLanguages.All.Where(l => spoken.Contains(l.Code)).Select(l => new LanguageChoice(l.Code, l.Name))];
        Language = Languages[0];
        HasGenders = resources.Employees.Any(e => e.Gender is not null);
        Gender = GenderChoice.All[0];
        Part = DayPartChoice.All[0];
        Guest = new CustomerSearchViewModel(api);
        Guest.Changed += OnGuestChanged;

        _suspended++;
        if (prefill?.Guest is { } guest)
        {
            Guest.Customer = guest;
            ApplyGuestWishes(guest);
        }

        if (prefill?.EmployeeId is { } employeeId && People.FirstOrDefault(p => p.Id == employeeId) is { } person)
        {
            Person = person;
        }

        if (prefill?.Part is { } part and not DayPart.Any)
        {
            Part = DayPartChoice.All.First(p => p.Value == part);
        }

        Treatment = treatment ?? (prefill?.TreatmentId is { } treatmentId ? resources.Treatments.FirstOrDefault(t => t.Id == treatmentId) : null);
        if (prefill?.RoomId is { } roomId && Rooms.FirstOrDefault(r => r.Id == roomId) is { } room)
        {
            Room = room;
        }

        _suspended--;
        _ = LoadAsync();
    }

    public IReadOnlyList<TreatmentDto> Treatments { get; }

    public IReadOnlyList<PersonChoice> People { get; }

    public string DateText => DayLayout.DateText(_date);

    /// <summary>Gast, für den gesucht und gebucht wird (Kundensuche wie sonst); ohne Gast öffnet die Buchung das Fenster „Neuer Termin“.</summary>
    public CustomerSearchViewModel Guest { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookText), nameof(GuestHint), nameof(HasGuestHint))]
    public partial CustomerDto? GuestCustomer { get; private set; }

    /// <summary>Wünsche des Gastes in einer Zeile, z. B. „Feste Therapeut:in: Lena Huber · lieber Do.“.</summary>
    public string? GuestHint => DayLayout.BookingPreferenceText(GuestCustomer?.Booking, id => _resources.Employees.FirstOrDefault(e => e.Id == id)?.InHouseName());

    public bool HasGuestHint => GuestHint is not null;

    [ObservableProperty]
    public partial TreatmentDto? Treatment { get; set; }

    [ObservableProperty]
    public partial PersonChoice Person { get; set; }

    /// <summary>Räume der gewählten Behandlung (ohne Behandlung alle Behandlungsräume); „Alle“ lässt den Server frei wählen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RoomChoice> Rooms { get; private set; }

    [ObservableProperty]
    public partial RoomChoice Room { get; set; }

    /// <summary>Sprachen, die jemand im Team spricht; nur sichtbar, wenn es welche gibt.</summary>
    public IReadOnlyList<LanguageChoice> Languages { get; }

    public bool HasLanguages => Languages.Count > 1;

    /// <summary>Das Feld „Sprache des Gastes“ ist ausgeblendet am 27.10.2026 (Auftraggeber), für eine mögliche spätere Nutzung; Auswahl, Filter und Vermerk an den Zeiten bleiben.</summary>
    public bool ShowLanguage { get; }

    /// <summary>Gewählte Sprache des Gastes: Zeiten bei Personen, die sie sprechen, stehen zuerst und sind markiert.</summary>
    [ObservableProperty]
    public partial LanguageChoice Language { get; set; }

    /// <summary>„Therapeutin“ oder „Therapeut“ zur Auswahl, sobald im Team jemand eine Angabe hat.</summary>
    public bool HasGenders { get; }

    /// <summary>Wunsch des Gastes: Zeiten bei Personen mit anderer Angabe fallen weg, Personen ohne Angabe bleiben.</summary>
    [ObservableProperty]
    public partial GenderChoice Gender { get; set; }

    /// <summary>Tageszeit des Wunsches (vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr).</summary>
    [ObservableProperty]
    public partial DayPartChoice Part { get; set; }

    /// <summary>Freie Zeiten ab jetzt (heute) bzw. ab Tagesbeginn, bei wenig Platz auch an den Folgetagen; zuerst die, die zum Gast passen und lückenlos anschließen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSlots), nameof(Summary), nameof(Active), nameof(DayHint), nameof(HasNoSlots), nameof(NoSlotsMessage), nameof(RelaxText))]
    public partial IReadOnlyList<FreeSlotRow> Slots { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Active), nameof(BookText))]
    public partial int ActiveIndex { get; private set; } = -1;

    public FreeSlotRow? Active => ActiveIndex >= 0 && ActiveIndex < Slots.Count ? Slots[ActiveIndex] : null;

    public bool HasSlots => Slots.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSlots))]
    public partial bool Busy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSlots))]
    public partial string? Error { get; private set; }

    /// <summary>Kleiner Hinweis über der Liste, wenn am gezeigten Tag nichts frei ist und die Vorschläge von späteren Tagen stammen.</summary>
    public string? DayHint => Treatment is not null && !Busy && Slots.Count > 0 && Slots.All(s => !s.OnThisDay) ? "An diesem Tag ist nichts frei." : null;

    public bool HasDayHint => DayHint is not null;

    public string Summary => Treatment is null
        ? "Behandlung wählen; die freien Zeiten leuchten im Kalender."
        : Slots.Count == 0 ? (Busy ? "Suche läuft …" : "")
        : $"{Slots.Count} freie Zeiten. ↑ ↓ wechseln, Enter bucht.";

    /// <summary>Die Suche ist fertig und hat in allen Tagen nichts gefunden: die Meldung (InfoBar) mit Ursache und Weg ersetzt die Zusammenfassung.</summary>
    public bool HasNoSlots => Treatment is not null && !Busy && Error is null && Slots.Count == 0;

    /// <summary>Ursache: nichts frei, mit den Einschränkungen, die die Suche enger machen („nachmittags, bei Lena“); ohne Einschränkung der Rat zur anderen Behandlung.</summary>
    public string NoSlotsMessage
    {
        get
        {
            var limits = new List<string>();
            if (Part.Value != DayPart.Any)
            {
                limits.Add(Part.Label);
            }

            if (Person.Id is not null)
            {
                limits.Add($"bei {Person.Name}");
            }

            if (Room.Id is not null)
            {
                limits.Add($"im {Room.Name}");
            }

            var head = $"In den nächsten {SearchDays} Tagen ist nichts frei";
            return limits.Count > 0 ? $"{head}: {string.Join(", ", limits)}." : $"{head}. Eine andere Behandlung kann mehr Platz haben.";
        }
    }

    /// <summary>Knopf der Meldung: die erste Einschränkung, die sich aufheben lässt; ohne Einschränkung gibt es keinen.</summary>
    public string? RelaxText => Part.Value != DayPart.Any ? "Tageszeit: egal" : Person.Id is not null ? "Alle Personen" : Room.Id is not null ? "Alle Räume" : null;

    [RelayCommand]
    private void Relax()
    {
        if (Part.Value != DayPart.Any)
        {
            Part = DayPartChoice.All[0];
        }
        else if (Person.Id is not null)
        {
            Person = People[0];
        }
        else if (Room.Id is not null)
        {
            Room = Rooms[0];
        }
    }

    /// <summary>Text des Hauptknopfs: mit Gast „Für Brigitte Müller buchen“, sonst „Diese Zeit buchen“; im Posteingang „Vormerken und vorschlagen“.</summary>
    public string BookText => _bookLabel ?? (GuestCustomer is { } guest ? $"Für {guest.InHouseName} buchen (Enter)" : "Diese Zeit buchen (Enter)");

    /// <summary>Das Feld „Gast“: nur im Kalender; im Posteingang ist der Gast die Anfrage.</summary>
    public bool ShowGuest => _bookLabel is null;

    partial void OnTreatmentChanged(TreatmentDto? value)
    {
        Rooms = RoomsFor(value);
        if (Rooms.All(r => r.Id != Room.Id))
        {
            _suspended++;
            Room = Rooms[0];
            _suspended--;
        }

        Reload();
    }

    partial void OnPersonChanged(PersonChoice value)
    {
        OnPropertyChanged(nameof(NoSlotsMessage));
        OnPropertyChanged(nameof(RelaxText));
        Reload();
    }

    partial void OnRoomChanged(RoomChoice value)
    {
        OnPropertyChanged(nameof(NoSlotsMessage));
        OnPropertyChanged(nameof(RelaxText));
        Reload();
    }

    partial void OnLanguageChanged(LanguageChoice value) => Reload();

    partial void OnGenderChanged(GenderChoice value) => Reload();

    partial void OnPartChanged(DayPartChoice value)
    {
        OnPropertyChanged(nameof(NoSlotsMessage));
        OnPropertyChanged(nameof(RelaxText));
        Reload();
    }

    private void Reload()
    {
        if (_suspended == 0)
        {
            _ = LoadAsync();
        }
    }

    private IReadOnlyList<RoomChoice> RoomsFor(TreatmentDto? treatment) =>
        [new RoomChoice(null, "Alle"), .. _resources.Rooms.Where(r => !r.IsArea && (treatment is null || treatment.RoomIds.Contains(r.Id))).Select(r => new RoomChoice(r.Id, r.Name))];

    private void OnGuestChanged()
    {
        GuestCustomer = Guest.Customer;
        if (Guest.Customer is { } customer)
        {
            _suspended++;
            ApplyGuestWishes(customer);
            _suspended--;
        }

        Reload();
    }

    /// <summary>Feste Person und Wunsch Therapeutin/Therapeut, Sprache und Tageszeit-Vorlieben des Gastes in die Suche übernehmen.</summary>
    private void ApplyGuestWishes(CustomerDto customer)
    {
        GuestCustomer = customer;
        if (customer.Booking is { } wishes)
        {
            if (wishes is { EmployeeFixed: true, EmployeeId: { } fixedId } && People.FirstOrDefault(p => p.Id == fixedId) is { } person)
            {
                Person = person;
            }

            if (wishes.Gender is { } gender && HasGenders)
            {
                Gender = GenderChoice.All.First(g => g.Value == gender);
            }
        }

        if (GuestLanguages.Normalize(customer.Language) is { } code && Languages.FirstOrDefault(l => l.Code == code) is { } language)
        {
            Language = language;
        }
    }

    /// <summary>
    /// Sucht ab dem gezeigten Tag. Der Server liefert den Tag ganz und bei <c>minSlots</c> auch Folgetage, bis genug Zeiten da sind; was hier nach
    /// Wunsch (Tageszeit, Therapeutin/Therapeut, ab jetzt) wegfällt, wird mit den nächsten Tagen nachgeholt, bis es drei Vorschläge gibt.
    /// </summary>
    public async Task LoadAsync()
    {
        var version = ++_version;
        Error = null;
        if (Treatment is not { } treatment)
        {
            Slots = [];
            ActiveIndex = -1;
            return;
        }

        Busy = true;
        OnPropertyChanged(nameof(Summary));
        var employees = _resources.Employees.ToDictionary(e => e.Id);
        var rooms = _resources.Rooms.ToDictionary(r => r.Id);
        var span = _until is { } until ? Math.Clamp(until.DayNumber - _date.DayNumber + 1, 1, SearchDays) : SearchDays;
        var extra = (Person.Id is { } person ? $"&employeeId={person}" : "") + (Room.Id is { } room ? $"&roomId={room}" : "")
            + (GuestCustomer is { } guest ? $"&customerId={guest.Id}" : "");
        var (slots, error) = await FreeTimeFinder.FindAsync(_api, _resources.Location.Id, treatment.Id, _date, span, extra, MinProposals,
            s => Keeps(s, employees), Day, () => version == _version).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (slots is null)
        {
            Busy = false;
            Error = error;
            Slots = [];
            ActiveIndex = -1;
            return;
        }

        var found = slots;
        Busy = false;
        bool Speaks(AvailableSlot s) => Language.Code is { } code && employees.GetValueOrDefault(s.EmployeeId)?.Languages?.Contains(code) == true;
        Slots = [.. found
            .DistinctBy(s => (s.Start, s.EmployeeId, s.RoomId))
            .OrderBy(s => Speaks(s) ? 0 : 1)
            .ThenBy(s => WishRank(s))
            .ThenBy(s => s.Start)
            .ThenBy(s => s.Fit switch { SlotFit.Snug => 0, SlotFit.Neutral => 1, _ => 2 })
            .Select(s => new FreeSlotRow(s,
                DayLayout.TimeText(s.Start, s.End, _zone),
                employees.GetValueOrDefault(s.EmployeeId)?.InHouseName() ?? "",
                employees.GetValueOrDefault(s.EmployeeId)?.Color ?? "#2f6f5e",
                rooms.GetValueOrDefault(s.RoomId)?.Name ?? "",
                s.Fit == SlotFit.Snug,
                s.Fit == SlotFit.LeavesGap ? s.FitReason ?? "lässt eine Lücke" : null)
            {
                SpeaksLanguage = Speaks(s) ? $"spricht {Language.Name}" : null,
                DayText = Day(s.Start) == _date ? null : DayLayout.ShortDay(Day(s.Start)),
                OnThisDay = Day(s.Start) == _date,
                OutsideWish = WishRank(s) >= 2 ? "außerhalb der Wunschzeit des Gastes" : null,
                IsWishPerson = GuestCustomer?.Booking is { EmployeeId: { } wished } && wished == s.EmployeeId,
            })];
        ActiveIndex = Slots.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(DayHint));
        OnPropertyChanged(nameof(HasDayHint));
    }

    private DateOnly Day(DateTimeOffset start) => DateOnly.FromDateTime(DayLayout.Local(start, _zone));

    private bool Keeps(AvailableSlot slot, Dictionary<Guid, EmployeeDto> employees)
    {
        return DayPartChoice.Contains(Part.Value, DayLayout.Local(slot.Start, _zone).Hour) && slot.Start >= _notBefore
            && (Gender.Value is not { } wish || employees.GetValueOrDefault(slot.EmployeeId)?.Gender is not { } gender || gender == wish);
    }

    /// <summary>
    /// 0 passt zu allem, was der Gast sich wünscht; +1 bei einer anderen Person als der bevorzugten (nicht festen), +2 außerhalb von
    /// Wochentag und Uhrzeit. Nur Reihenfolge, nichts fällt weg: der Gast darf immer auch anders.
    /// </summary>
    private int WishRank(AvailableSlot slot)
    {
        if (GuestCustomer?.Booking is not { } wishes)
        {
            return 0;
        }

        var local = DayLayout.Local(slot.Start, _zone);
        var rank = wishes.EmployeeId is { } wished && wished != slot.EmployeeId ? 1 : 0;
        var outsideDay = wishes.Weekdays is { Count: > 0 } days && !days.Contains(local.DayOfWeek);
        var time = TimeOnly.FromDateTime(local);
        var outsideTime = (wishes.From is { } start && time < start) || (wishes.Until is { } end && time > end);
        return rank + (outsideDay || outsideTime ? 2 : 0);
    }

    /// <summary>Nächste oder vorige freie Zeit; an den Enden bleibt die Auswahl stehen.</summary>
    public void Move(int direction)
    {
        if (Slots.Count > 0)
        {
            ActiveIndex = Math.Clamp(ActiveIndex + direction, 0, Slots.Count - 1);
        }
    }

    public void Choose(FreeSlotRow row)
    {
        var index = Slots.ToList().IndexOf(row);
        if (index >= 0)
        {
            ActiveIndex = index;
        }
    }

    /// <summary>Die ersten <paramref name="count"/> Vorschläge, z. B. für den Antwortentwurf im Posteingang.</summary>
    public IReadOnlyList<FreeSlotRow> Top(int count = MinProposals) => [.. Slots.Take(count)];

    /// <summary>Enter: die gewählte Zeit buchen. Mit Gast gleich für ihn, sonst öffnet „Neuer Termin“ mit Behandlung, Person und Uhrzeit.</summary>
    [RelayCommand]
    public async Task BookActiveAsync()
    {
        if (Active is { } row && Treatment is { } treatment)
        {
            await _book(new FreeTimeBooking(row.Slot, treatment, GuestCustomer, Direct: GuestCustomer is not null, _note)).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Close() => _close();
}

public sealed record PersonChoice(Guid? Id, string Name);

public sealed record RoomChoice(Guid? Id, string Name);

/// <summary>Tageszeit zur Auswahl; „ganztags“ schränkt nicht ein.</summary>
public sealed record DayPartChoice(DayPart Value, string Label)
{
    public static IReadOnlyList<DayPartChoice> All { get; } =
        [new(DayPart.Any, "ganztags"), new(DayPart.Morning, "vormittags"), new(DayPart.Afternoon, "nachmittags"), new(DayPart.Evening, "abends")];

    /// <summary>Liegt die Ortszeit-Stunde in der Tageszeit? Vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr (wie beim Server).</summary>
    public static bool Contains(DayPart part, int hour) => part switch
    {
        DayPart.Morning => hour < 12,
        DayPart.Afternoon => hour is >= 12 and < 17,
        DayPart.Evening => hour >= 17,
        _ => true,
    };

    public override string ToString() => Label;
}

/// <summary>Eine freie Zeit in der Liste: Uhrzeit (an einem anderen Tag mit Tag darüber), Person, Raum; „passt genau“, wenn sie lückenlos anschließt.</summary>
public sealed record FreeSlotRow(AvailableSlot Slot, string Time, string Who, string WhoColor, string Room, bool IsSnug, string? GapNote)
{
    public bool HasGapNote => GapNote is not null;

    /// <summary>Name für Screenreader: alles, was die Zeile zeigt, in Lesereihenfolge („Do. 22.10., 09:00–10:20, Lena, Lotus, passt genau“).</summary>
    public string AccessibleName =>
        AccessibleNames.Join(DayText, Time, Who, Room, SpeaksLanguage, OutsideWish, GapNote, IsSnug ? "passt genau" : null, IsWishPerson ? "Wunsch" : null);

    /// <summary>„spricht Italienisch“, wenn eine Sprache gewählt ist und die Person sie spricht.</summary>
    public string? SpeaksLanguage { get; init; }

    public bool HasLanguage => SpeaksLanguage is not null;

    /// <summary>„Do. 23.10.“, wenn die Zeit nicht am gezeigten Tag liegt.</summary>
    public string? DayText { get; init; }

    public bool HasDay => DayText is not null;

    /// <summary>Liegt am Tag, den der Kalender zeigt, und leuchtet dort.</summary>
    public bool OnThisDay { get; init; } = true;

    /// <summary>„außerhalb der Wunschzeit des Gastes“ (Wochentag oder Uhrzeit), nur mit Gast.</summary>
    public string? OutsideWish { get; init; }

    public bool HasOutsideWish => OutsideWish is not null;

    /// <summary>Die bevorzugte oder feste Person des Gastes.</summary>
    public bool IsWishPerson { get; init; }
}

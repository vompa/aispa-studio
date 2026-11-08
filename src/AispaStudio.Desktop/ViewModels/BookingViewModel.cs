using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Neuer Termin von einer freien Stelle im Kalender: Behandlung, Kundin (suchen oder neu anlegen), Uhrzeit.
/// Person bzw. Raum der angeklickten Spalte stehen fest (Auftraggeber, 27.10.2026); die Auswahl bietet nur Behandlungen an, die dort möglich sind.
/// </summary>
public partial class BookingViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly LocationResources _resources;
    private readonly DateOnly _today;
    private readonly TimeZoneInfo _zone;
    private readonly Func<AppointmentDto, Task> _booked;
    private readonly Action _close;
    private int _searchVersion;
    private int _suggestionVersion;
    private bool _settingQuery;

    public BookingViewModel(IWorkstationApi api, LocationResources resources, DateOnly date, DateTimeOffset start, TimeZoneInfo zone,
        CalendarColumn column, bool columnIsRoom, Func<AppointmentDto, Task> booked, Action close, FreeGap? gap = null, string? dayNotice = null,
        TimeProvider? time = null, CalendarDay? day = null)
    {
        DayNotice = dayNotice;
        _api = api;
        _resources = resources;
        _zone = zone;
        _today = DateOnly.FromDateTime(DayLayout.Local((time ?? TimeProvider.System).GetUtcNow(), zone));
        Date = date;
        DateText = DateInputParser.Format(date);
        _booked = booked;
        _close = close;
        ColumnTitle = column.Title;
        ColumnIsRoom = columnIsRoom;
        ColumnId = column.Id;
        Customers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsPicking));
        // Nur was die angeklickte Person bzw. der Raum kann; darin zuerst, was in die freie Zeit passt.
        Options = OptionsFor(column.Id, columnIsRoom, gap, start);
        Treatment = Treatments.Count > 0 ? Treatments[0] : null;
        StartText = DayLayout.Local(start, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
        Duplicates = new DuplicateHintViewModel(api, AdoptAsync);
        UpdateOpeningDay(day);
    }

    /// <summary>Tag, Öffnungsfenster (ohne Schließzeiten), Schließzeiten und ob jemand Dienst hat: der geladene Tag für die Vorabprüfung.</summary>
    private sealed record OpeningDay(DateOnly Date, IReadOnlyList<TimeRange> Windows, IReadOnlyList<ClosedRange> Closed, bool AnyShift);

    /// <summary>Der Tag für die Vorabprüfung (Slice 78); ohne ihn oder an einem anderen Tag als <see cref="Date"/> prüft nur der Server.</summary>
    private OpeningDay? _openingDay;

    /// <summary>Nach dem Laden eines Tages (auch nach einem Tageswechsel in der Maske): die Vorabprüfung rechnet mit diesem Tag; <c>null</c> schaltet sie ab.</summary>
    internal void UpdateOpeningDay(CalendarDay? day)
    {
        if (day is not null)
        {
            var closed = ClosedRanges(day);
            _openingDay = new OpeningDay(day.Date, OpeningRules.Windows(day.Opening, closed), closed, day.Shifts.Count > 0);
        }
        else
        {
            _openingDay = null;
        }

        RefreshClosedHint();
    }

    /// <summary>
    /// Vorabprüfung ohne Server (Slice 78, Ä84): liegt die Zeit außerhalb der Öffnungszeit oder in einer Schließzeit, die Meldung des Servers, sonst <c>null</c>.
    /// Dieselbe Regel wie der Raumplaner (<see cref="OpeningRules"/>); der Schließtag sperrt nichts. Belegung prüft weiter nur der Server.
    /// </summary>
    internal string? ClosedHint()
    {
        if (_openingDay is not { } day || day.Date != Date || Treatment is null || !TryStart(out var start))
        {
            return null;
        }

        var end = start.AddMinutes(Treatment.DurationMinutes);
        return OpeningRules.Check(day.Windows, day.AnyShift, house: true, start, end) == OpeningState.Closed
            ? OpeningRules.ClosedMessage(day.Closed, start, end)
            : null;
    }

    /// <summary>Die Warnung der Vorabprüfung folgt jeder Änderung von Tag und Beginn; eine Warnung des Servers gilt nur für die Zeit, zu der sie kam.</summary>
    private void RefreshClosedHint()
    {
        _staffRuleWarned = false;
        OutsideHours = ClosedHint();
    }

    partial void OnStartTextChanged(string value) => RefreshClosedHint();

    partial void OnDateChanged(DateOnly value) => RefreshClosedHint();

    private static List<ClosedRange> ClosedRanges(CalendarDay day) => [.. (day.Closures ?? []).Select(c => new ClosedRange(c.From, c.Until, c.Title))];

    /// <summary>
    /// Hinweis, wenn der Tag ein Schließtag ist (kein Öffnungsfenster nach den Schließzeiten) oder niemand Dienst hat: die Rezeption trägt trotzdem ein,
    /// die Maske sperrt nichts (ADR 0073, Nachtrag Runde R03).
    /// </summary>
    public string? DayNotice { get; private set; }

    /// <summary>Nach einem Tageswechsel in der Maske: der Hinweis gilt für den neuen Tag.</summary>
    internal void UpdateDayNotice(string? notice)
    {
        DayNotice = notice;
        OnPropertyChanged(nameof(DayNotice));
        OnPropertyChanged(nameof(HasDayNotice));
    }

    public bool HasDayNotice => DayNotice is not null;

    /// <summary>Der Hinweis für den Tag, sonst <c>null</c>. Ohne Öffnungsfenster nach den Schließzeiten (dieselbe Regel wie der Server); ein halber Tag lässt den Rest offen.</summary>
    internal static string? NoticeFor(CalendarDay day) =>
        day.Shifts.Count == 0 || OpeningRules.Windows(day.Opening, ClosedRanges(day)).Count == 0
            ? BookingNotices.ClosedDay
            : null;

    /// <summary>„Gibt es schon?“ beim Anlegen einer neuen Kundin.</summary>
    public DuplicateHintViewModel Duplicates { get; }

    partial void OnNewFirstNameChanged(string value) => CheckDuplicates();

    partial void OnNewLastNameChanged(string value) => CheckDuplicates();

    partial void OnNewPhoneChanged(string value) => CheckDuplicates();

    partial void OnNewEmailChanged(string value) => CheckDuplicates();

    private void CheckDuplicates() => _ = Duplicates.CheckAsync(NewFirstName, NewLastName, NewEmail, NewPhone);

    private async Task AdoptAsync(Guid id)
    {
        var result = await _api.GetAsync<CustomerDto>($"api/customers/{id}").ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        Customers.Insert(0, result.Value);
        Customer = result.Value;
        ShowNewCustomer = false;
        Duplicates.Clear();
    }

    public IReadOnlyList<TreatmentDto> Treatments => [.. Options.Select(o => o.Treatment)];

    /// <summary>
    /// Was die Person bzw. der Raum der Spalte kann, in der Reihenfolge der Auswahl, mit Hinweis „passt genau“ oder „länger als die freie Zeit“.
    /// Andere Behandlungen stehen nicht zur Wahl: gebucht wird immer in der Spalte (Auftraggeber, 27.10.2026).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Treatments), nameof(ColumnScopeText), nameof(SuggestionIsDoable), nameof(SuggestedTreatmentText))]
    public partial IReadOnlyList<TreatmentOption> Options { get; private set; }

    private IReadOnlyList<TreatmentOption> OptionsFor(Guid columnId, bool columnIsRoom, FreeGap? gap, DateTimeOffset start) =>
        TreatmentFits.Rank(_resources.Treatments.Where(TreatmentFits.Suits(_resources, columnId, columnIsRoom)), gap, start);

    /// <summary>Unter der Auswahl: warum nicht alle Behandlungen dastehen, z. B. „Nur Behandlungen, die Lena Huber kann.“.</summary>
    public string ColumnScopeText => (ColumnIsRoom, Options.Count > 0) switch
    {
        (false, true) => $"Nur Behandlungen, die {ColumnTitle} kann.",
        (false, false) => $"{ColumnTitle} kann keine der Behandlungen.",
        (true, true) => $"Nur Behandlungen für den Raum {ColumnTitle}.",
        (true, false) => $"Für den Raum {ColumnTitle} gibt es keine Behandlung.",
    };

    /// <summary>Der Tag, an dem gebucht wird (Vorschau im Kalender); folgt dem Datumsfeld.</summary>
    [ObservableProperty]
    public partial DateOnly Date { get; private set; }

    /// <summary>Das Datumsfeld neben „Beginn“; ein unlesbarer Text lässt <see cref="Date"/> stehen und sperrt das Buchen.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand), nameof(BookAnywayCommand))]
    public partial string DateText { get; set; }

    partial void OnDateTextChanged(string value)
    {
        if (DateInputParser.TryParse(value, _today) is { } date && date != Date)
        {
            Date = date;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColumnText), nameof(PanelSubtitle), nameof(PreferenceHint), nameof(PreferenceIsWarning))]
    public partial string ColumnTitle { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColumnText), nameof(PanelSubtitle), nameof(PreferenceHint), nameof(PreferenceIsWarning))]
    public partial bool ColumnIsRoom { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreferenceIsWarning))]
    public partial Guid ColumnId { get; private set; }

    /// <summary>
    /// Ein Klick auf einen anderen Slot verlegt den angefangenen Termin dorthin (Ä85): Spalte, Tag und Beginn wechseln, alle Eingaben bleiben.
    /// Die Auswahl zeigt dann, was die neue Spalte kann; eine Behandlung, die dort nicht geht, ist danach nicht mehr gewählt.
    /// </summary>
    internal void MoveTo(CalendarColumn column, bool columnIsRoom, DateOnly date, DateTimeOffset start, FreeGap? gap)
    {
        var chosen = Treatment;
        ColumnId = column.Id;
        ColumnTitle = column.Title;
        ColumnIsRoom = columnIsRoom;
        Options = OptionsFor(column.Id, columnIsRoom, gap, start);
        Treatment = Treatments.FirstOrDefault(t => t.Id == chosen?.Id);
        DateText = DateInputParser.Format(date);
        Date = date;
        StartText = DayLayout.Local(start, _zone).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Beim Wechsel der Art in „Neuer Termin“ (Ä153): was die vorige Art schon hatte. Eine Behandlung, die die Spalte nicht kann, bleibt ungewählt;
    /// eine schon getippte Notiz bleibt stehen.
    /// </summary>
    internal void Adopt(DateOnly? date, string? startText, CustomerDto? customer, Guid? treatmentId, string? note)
    {
        if (date is { } day && day != Date)
        {
            DateText = DateInputParser.Format(day);
        }

        StartText = startText ?? StartText;
        if (customer is not null)
        {
            Customers.Insert(0, customer);
            Customer = customer;
        }

        if (treatmentId is { } id && Treatments.FirstOrDefault(t => t.Id == id) is { } treatment)
        {
            Treatment = treatment;
        }

        if (string.IsNullOrWhiteSpace(Note))
        {
            Note = note;
        }
    }

    public string ColumnText => ColumnIsRoom ? $"Raum {ColumnTitle}" : $"bei {ColumnTitle}";

    public ObservableCollection<CustomerDto> Customers { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand), nameof(BookAnywayCommand))]
    [NotifyPropertyChangedFor(nameof(HasTreatmentSuggestion))]
    public partial TreatmentDto? Treatment { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand), nameof(BookAnywayCommand))]
    public partial string StartText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChosenCustomer), nameof(IsPicking))]
    public partial string CustomerQuery { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand), nameof(BookAnywayCommand))]
    [NotifyPropertyChangedFor(nameof(PreferenceHint), nameof(HasPreferenceHint), nameof(PreferenceIsWarning), nameof(HasChosenCustomer), nameof(IsPicking), nameof(PanelTabText))]
    public partial CustomerDto? Customer { get; set; }

    /// <summary>Der Treffer ist gewählt, sein Name steht im Suchfeld (Ä82); die Trefferliste ist dann zu.</summary>
    public bool HasChosenCustomer => Customer is not null && CustomerQuery == Customer.InHouseName;

    /// <summary>Die Trefferliste ist offen: es gibt Treffer und noch keine Wahl.</summary>
    public bool IsPicking => Customers.Count > 0 && !HasChosenCustomer;

    /// <summary>„Ändern“ an der Zeile des gewählten Kunden: Suchfeld leeren, neu suchen.</summary>
    [RelayCommand]
    private void ChangeCustomer()
    {
        Customer = null;
        CustomerQuery = "";
    }

    /// <summary>Vorlieben der gewählten Kundin, z. B. „Feste Therapeut:in: Lena Huber · lieber Do.“.</summary>
    public string? PreferenceHint => DayLayout.BookingPreferenceText(Customer?.Booking,
        id => _resources.Employees.FirstOrDefault(e => e.Id == id)?.InHouseName()) is { } text
        ? PreferenceIsWarning ? $"{text}. Gebucht wird {ColumnText}." : text
        : null;

    public bool HasPreferenceHint => PreferenceHint is not null;

    /// <summary>Feste Therapeut:in, aber die Spalte gehört jemand anderem.</summary>
    public bool PreferenceIsWarning => Customer?.Booking is { EmployeeFixed: true, EmployeeId: { } fixedId }
        && !ColumnIsRoom && ColumnId != fixedId;

    /// <summary>„Anamnese prüfen“: Behandlung verlangt es, und der Bogen des Gastes enthält Angaben. Der Inhalt bleibt im Bogen.</summary>
    [ObservableProperty]
    public partial bool CheckAnamnesis { get; private set; }

    private int _anamnesisVersion;

    partial void OnTreatmentChanged(TreatmentDto? value)
    {
        RefreshClosedHint();
        _ = LoadAnamnesisCheckAsync();
    }

    partial void OnCustomerChanged(CustomerDto? value)
    {
        _ = LoadAnamnesisCheckAsync();
        if (value is not null)
        {
            // Eine noch laufende Suche darf die Wahl nicht mehr überschreiben.
            _searchVersion++;
            // Der Name steht im Suchfeld, ohne dass die Suche die Treffer ersetzt.
            _settingQuery = true;
            try
            {
                CustomerQuery = value.InHouseName;
            }
            finally
            {
                _settingQuery = false;
            }
        }

        _ = LoadSuggestionsAsync();
    }

    /// <summary>Fragt nur bei Behandlungen mit „Anamnese prüfen“; ohne Verbindung oder Kundenbereich bleibt der Hinweis aus.</summary>
    internal async Task LoadAnamnesisCheckAsync()
    {
        var version = ++_anamnesisVersion;
        CheckAnamnesis = false;
        if (Treatment is not { CheckAnamnesis: true } treatment || Customer is not { } customer)
        {
            return;
        }

        var result = await _api.GetAsync<AnamnesisCheck>($"api/customers/{customer.Id}/anamnesis/check?treatmentId={treatment.Id}").ConfigureAwait(true);
        if (version == _anamnesisVersion)
        {
            CheckAnamnesis = result.Value?.Check == true;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWishSuggestion))]
    public partial string? Note { get; set; }

    /// <summary>Hinweis, wenn „KI und Hilfe“ die Maske vorausgefüllt hat (Ä319); gebucht wird erst mit „Termin buchen“.</summary>
    [ObservableProperty]
    public partial string? AssistantNote { get; set; }

    /// <summary>Die zuletzt gebuchte Behandlung des gewählten Kunden, auch wenn die Spalte sie nicht kann (dann mit Grund und „Freie Zeit suchen“).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTreatmentSuggestion), nameof(SuggestedTreatmentText), nameof(HasSuggestions), nameof(SuggestionIsDoable))]
    public partial TreatmentDto? SuggestedTreatment { get; private set; }

    public bool HasTreatmentSuggestion => SuggestedTreatment is not null && Treatment?.Id != SuggestedTreatment.Id;

    /// <summary>Die Spalte kann den Vorschlag: „Übernehmen“; sonst „Freie Zeit suchen“.</summary>
    public bool SuggestionIsDoable => SuggestedTreatment is { } treatment && Treatments.Any(t => t.Id == treatment.Id);

    public string SuggestedTreatmentText => SuggestedTreatment is not { } treatment ? ""
        : SuggestionIsDoable ? $"Zuletzt gebucht: {treatment.Name}"
        : $"Zuletzt gebucht: {treatment.Name}. {(ColumnIsRoom ? $"Im Raum {ColumnTitle}" : $"Bei {ColumnTitle}")} nicht möglich.";

    /// <summary>„Meist bei Lena Huber“: nur ein Hinweis, die Person ergibt sich aus der Spalte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHabitualPerson), nameof(HasSuggestions))]
    public partial string? HabitualPersonText { get; private set; }

    public bool HasHabitualPerson => HabitualPersonText is not null;

    /// <summary>Die Wünsche aus der Kundenkarte (Druck, Duft, Musik …) als Text für die Notiz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWishSuggestion), nameof(SuggestedWishText), nameof(HasSuggestions))]
    public partial string? SuggestedWish { get; private set; }

    public bool HasWishSuggestion => SuggestedWish is not null && Note?.Contains(SuggestedWish, StringComparison.Ordinal) != true;

    public string SuggestedWishText => SuggestedWish is { } wish ? $"Wunsch laut Kundenkarte: {wish}" : "";

    public bool HasSuggestions => SuggestedTreatment is not null || HabitualPersonText is not null || SuggestedWish is not null;

    [RelayCommand]
    private void AcceptTreatment()
    {
        if (SuggestionIsDoable)
        {
            Treatment = SuggestedTreatment;
        }
    }

    [RelayCommand]
    private void AcceptWish()
    {
        if (HasWishSuggestion)
        {
            Note = string.IsNullOrWhiteSpace(Note) ? SuggestedWish : Note + Environment.NewLine + SuggestedWish;
        }
    }

    /// <summary>
    /// Nach der Kundenwahl: zuletzt gebuchte Behandlung, gewohnte Person und Wünsche aus der Kundenkarte als Vorschläge (Ä86). Was fehlt oder nicht
    /// ankommt, bleibt leer; eine späte Antwort eines früheren Kunden zählt nicht.
    /// </summary>
    internal async Task LoadSuggestionsAsync()
    {
        var version = ++_suggestionVersion;
        SuggestedTreatment = null;
        HabitualPersonText = null;
        SuggestedWish = null;
        if (Customer is not { } customer)
        {
            return;
        }

        var summary = _api.GetAsync<CustomerSummary>($"api/customers/{customer.Id}/summary");
        var wishes = _api.GetAsync<CustomerPreferences>($"api/reports/customers/{customer.Id}/preferences");
        await Task.WhenAll(summary, wishes).ConfigureAwait(true);
        if (version != _suggestionVersion)
        {
            return;
        }

        var recent = summary.Result.Value?.Recent ?? [];
        SuggestedTreatment = recent.Count == 0 ? null
            : _resources.Treatments.FirstOrDefault(t => string.Equals(t.Name, recent[0].TreatmentName, StringComparison.OrdinalIgnoreCase));
        if (recent.GroupBy(v => v.EmployeeName).OrderByDescending(g => g.Count()).FirstOrDefault() is { } habitual)
        {
            HabitualPersonText = $"{(habitual.Count() >= 2 ? "Meist" : "Zuletzt")} bei {habitual.Key}";
        }

        SuggestedWish = wishes.Result.Value is { } preferences ? CustomerOverviewViewModel.WishLine(preferences) : null;
    }

    [ObservableProperty]
    public partial bool ShowNewCustomer { get; set; }

    [ObservableProperty]
    public partial string NewFirstName { get; set; } = "";

    [ObservableProperty]
    public partial string NewLastName { get; set; } = "";

    [ObservableProperty]
    public partial string NewPhone { get; set; } = "";

    [ObservableProperty]
    public partial string NewEmail { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Die Zeit ist nicht frei: Liste der Belegungen in Ortszeit statt eines Fehlertextes.</summary>
    [ObservableProperty]
    public partial ConflictNotice? Conflict { get; private set; }

    /// <summary>Was der Kalender der Konfliktmeldung mitgibt: Termin markieren und Gegenstück von Person und Raum.</summary>
    public ConflictLinks? Links { get; init; }

    /// <summary>„Freie Zeit suchen“ im Kalender, vorbelegt und ab dem Tag der Maske; schließt die Maske.</summary>
    public Func<FreeTimePrefill, DateOnly, Task>? FreeTimeSearch { get; init; }

    public bool CanSearchFreeTime => FreeTimeSearch is not null;

    /// <summary>Beschriftung des Wegs aus der Maske zur freien Zeit.</summary>
    public const string FreeTimeLabel = "Freie Zeit suchen";

    /// <summary>
    /// „Freie Zeit suchen“ mit Behandlung (sonst der gewählten) und Gast: zeigt nur Zeiten, in denen jemand mit der Qualifikation und ein Raum frei sind.
    /// Der Weg aus „passt hier nicht“, statt in eine Woche, in der niemand kann.
    /// </summary>
    [RelayCommand]
    private Task SearchFreeTimeAsync(TreatmentDto? treatment) =>
        FreeTimeSearch?.Invoke(new FreeTimePrefill((treatment ?? Treatment)?.Id, Guest: Customer, Note: string.IsNullOrWhiteSpace(Note) ? null : Note), Date) ?? Task.CompletedTask;

    [ObservableProperty]
    public partial bool Busy { get; set; }

    partial void OnCustomerQueryChanged(string value)
    {
        if (!_settingQuery)
        {
            _ = SearchAsync(value);
        }
    }

    /// <summary>Sucht ab zwei Zeichen; eine ältere, langsamere Antwort überschreibt keine neuere.</summary>
    public async Task SearchAsync(string query)
    {
        var version = ++_searchVersion;
        if (query.Trim().Length < 2)
        {
            Customers.Clear();
            return;
        }

        var result = await _api.GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString(query.Trim())}").ConfigureAwait(true);
        if (version != _searchVersion || result.Value is null)
        {
            return;
        }

        Customers.Clear();
        foreach (var customer in result.Value)
        {
            Customers.Add(customer);
        }
    }

    /// <summary>Das „+“ im Suchfeld: öffnet die Neuanlage; was schon getippt ist, steht als Name darin (letztes Wort Nachname, davor Vorname).</summary>
    [RelayCommand]
    private void StartNewCustomer()
    {
        ShowNewCustomer = true;
        if (!string.IsNullOrWhiteSpace(NewFirstName) || !string.IsNullOrWhiteSpace(NewLastName))
        {
            return;
        }

        var words = CustomerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || words.Any(w => w.Any(char.IsDigit) || w.Contains('@')))
        {
            return;
        }

        NewLastName = words[^1];
        NewFirstName = string.Join(' ', words[..^1]);
    }

    [RelayCommand]
    private void CancelNewCustomer()
    {
        ShowNewCustomer = false;
        Duplicates.Clear();
    }

    [RelayCommand]
    private async Task CreateCustomerAsync()
    {
        Error = null;
        var result = await _api.CreateCustomerAsync(new CustomerInput(NewFirstName.Trim(), NewLastName.Trim(),
            string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail.Trim(), string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(), null, null))
            .ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Customers.Insert(0, result.Value);
        Customer = result.Value;
        ShowNewCustomer = false;
        Duplicates.Clear();
    }

    private bool CanBook => Treatment is not null && Customer is not null && DateInputParser.TryParse(DateText, _today) is { } day && day >= _today && TryStart(out _) && !Busy;

    /// <summary>Heute am Standort: früher lässt sich nicht buchen, das Datumsfeld nimmt keinen früheren Tag.</summary>
    public DateOnly Today => _today;

    [RelayCommand(CanExecute = nameof(CanBook))]
    private Task BookAsync() => BookCoreAsync(confirmed: false);

    /// <summary>
    /// Warnung „Der Standort hat zu dieser Zeit geschlossen.“ oder „… ist zu dieser Zeit nicht im Dienst.“ (Slice 85, Ä151): die Rezeption kann mit „Trotzdem buchen“
    /// außerhalb der Öffnungszeit und des Dienstes buchen; sonst <c>null</c>.
    /// </summary>
    [ObservableProperty]
    public partial string? OutsideHours { get; private set; }

    private bool _staffRuleWarned;
    private bool _outsideConfirmed;

    /// <summary>„Trotzdem buchen“: dieselbe Buchung mit ausdrücklicher Bestätigung, ohne Vorabprüfung; Belegung prüft weiter der Server.</summary>
    [RelayCommand(CanExecute = nameof(CanBook))]
    private Task BookAnywayAsync() => _staffRuleWarned ? BookCoreAsync(_outsideConfirmed, rulesConfirmed: true) : BookCoreAsync(confirmed: true);

    private async Task BookCoreAsync(bool confirmed, bool rulesConfirmed = false)
    {
        _staffRuleWarned = false;
        _outsideConfirmed = confirmed;
        if (!TryStart(out var start))
        {
            return;
        }

        Conflict = null;
        if (!confirmed && ClosedHint() is { } closed)
        {
            OutsideHours = closed;
            return;
        }

        Error = null;
        OutsideHours = null;
        Busy = true;
        var employee = ColumnIsRoom ? (Guid?)null : ColumnId;
        var room = ColumnIsRoom ? ColumnId : (Guid?)null;
        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        var request = new AppointmentRequest(_resources.Location.Id, Treatment!.Id, Customer!.Id, start, employee, room, null, note,
            ClientId: Guid.CreateVersion7(), OutsideHoursConfirmed: confirmed, RulesConfirmed: rulesConfirmed);
        var result = await _api.PostOrQueueAsync("api/appointments", request,
            $"{Customer.InHouseName}, {DayLayout.DateText(Date)} {DayLayout.Local(start, _zone):HH:mm}: {Treatment.Name} buchen",
            Preview(request, Treatment, Customer),
            // Beim Abgleich fragt niemand mehr nach: Eine Sonderregelung darf den vorgemerkten Termin nicht sperren (E10, Ä256).
            queuedBody: request with { RulesConfirmed = true }).ConfigureAwait(true);
        Busy = false;
        if (result.Rejection is { OutsideHours: true } outside)
        {
            OutsideHours = OutsideHoursText(outside);
            return;
        }

        // Sonderregelung der Person (R05 Slice 111, E10): dieselbe Warnung mit „Trotzdem buchen“; ein schon bestätigtes „außerhalb der Öffnungszeit“ bleibt bestätigt.
        if (result.Rejection is { StaffRule: true } rule)
        {
            _staffRuleWarned = true;
            OutsideHours = rule.Message;
            return;
        }

        if (result.Value is null)
        {
            // Statt „Zum Konflikt“ (führte in eine Woche, in der niemand den Termin übernehmen kann) die freie Zeit: sie ist immer buchbar.
            var links = Links ?? new ConflictLinks(null);
            Conflict = FreeTimeSearch is null
                ? ConflictNotice.From(result.Rejection, _zone, Date, links)
                : ConflictNotice.From(result.Rejection, _zone, Date, links with { GoTo = null }, (FreeTimeLabel, SearchFreeTimeCommand));
            Error = Conflict is null ? result.Error : null;
            return;
        }

        await _booked(result.Value).ConfigureAwait(true);
    }

    /// <summary>
    /// So steht die Buchung bis zum Abgleich im Kalender: in der angeklickten Spalte, mit Vor- und Nachbereitung.
    /// Welche Person und welcher Raum es wird, entscheidet beim Abgleich der Server.
    /// </summary>
    private AppointmentDto Preview(AppointmentRequest request, TreatmentDto treatment, CustomerDto customer)
    {
        var end = request.Start.AddMinutes(treatment.DurationMinutes);
        return new AppointmentDto(request.ClientId!.Value, request.LocationId, treatment.Id, treatment.Name, customer.Id,
            customer.InHouseName, ColumnIsRoom ? Guid.Empty : ColumnId, ColumnIsRoom ? ColumnId : Guid.Empty, null,
            request.Start, end, request.Start.AddMinutes(-treatment.PreparationMinutes), end.AddMinutes(treatment.FollowUpMinutes),
            AppointmentStatus.Booked, treatment.Price, request.Note, DateTimeOffset.MinValue,
            PreparationMinutes: treatment.PreparationMinutes, FollowUpMinutes: treatment.FollowUpMinutes,
            HandsOnMinutes: treatment.HandsOnMinutes, ProcessingMinutes: treatment.ProcessingMinutes);
    }

    /// <summary>Der Grund für die Warnung: die Konflikte („Anna ist zu dieser Zeit nicht im Dienst.“), ohne sie die Meldung des Servers.</summary>
    private static string OutsideHoursText(BookingRejected rejected) =>
        rejected.Conflicts.Count == 0 ? rejected.Message : string.Join(" ", rejected.Conflicts.Select(c => c.ResourceId == Guid.Empty ? $"{c.Reason}." : $"{c.ResourceName} {c.Reason}."));

    [RelayCommand]
    private void Close() => _close();

    private bool TryStart(out DateTimeOffset start)
    {
        start = default;
        if (TimeInputParser.TryParse(StartText) is not { } time)
        {
            return false;
        }

        start = DayLayout.LocalToUtc(Date, time, _zone);
        return true;
    }
}

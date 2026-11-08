using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Tageskalender der Rezeption: ein Standort, ein Tag, Spalten nach Team oder Räumen.
/// Änderungen anderer Arbeitsplätze kommen live; eigene Buchungen erscheinen sofort nach der Antwort des Servers.
/// </summary>
public partial class CalendarViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly CurrentUser _user;
    private readonly ILiveUpdates? _live;
    private readonly TimeProvider _time;
    private LocationResources? _resources;
    private CalendarDay? _day;
    private TimeZoneInfo _zone = TimeZoneInfo.Utc;
    private DateTimeOffset _dayStart;
    private DragState? _drag;
    private MoveUndo? _undo;
    private int _toastClock;
    private bool _toastHeld;
    private HashSet<Guid> _pending = [];
    private readonly ISettingsStore _settings;
    private double _viewportWidth;
    private double _viewportHeight;

    /// <summary>So lange bleibt eine Bestätigung stehen (meldungen.md, M1); Fehlermeldungen bleiben bis zum Schließen.</summary>
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);

    /// <summary>Mit „Rückgängig“ bleibt die Bestätigung länger, damit man die Aktion erreicht.</summary>
    private static readonly TimeSpan ToastUndoDuration = TimeSpan.FromSeconds(8);

    /// <summary>Stufen für Strg + Mausrad bzw. Strg +/−, in Pixeln je Minute.</summary>
    private static readonly double[] ZoomSteps = [0.8, 1.0, 1.2, 1.4, 1.6, 2.0, 2.4, 3.0];

    /// <summary>Ein Termin, der gerade gezogen wird: Ausgangslage und wo er losgelassen würde.</summary>
    private sealed record DragState(AppointmentDto Appointment, double GrabOffset, int Column, DateTimeOffset Start);

    /// <summary>Was „Rückgängig“ zurücknimmt: die Verschiebung, die Stornierung oder das Einchecken (Ä95, Ä97).</summary>
    private enum UndoKind
    {
        Move,
        Cancel,
        CheckIn,
    }

    /// <summary>Letzte Verschiebung, Stornierung oder letztes Einchecken, um sie mit „Rückgängig“ zurückzunehmen; <paramref name="Before"/> ist der Termin davor.</summary>
    /// <param name="After">Version des Termins nach der eigenen Änderung (Ä281): „Rückgängig“ schickt sie mit, hat inzwischen jemand anders geändert, meldet der Server den Konflikt.</param>
    private sealed record MoveUndo(AppointmentDto Before, UndoKind Kind = UndoKind.Move, uint? After = null);

    public CalendarViewModel(IWorkstationApi api, CurrentUser user, ILiveUpdates? live, TimeProvider time, ISettingsStore? settings = null)
    {
        _api = api;
        _user = user;
        _live = live;
        _time = time;
        _settings = settings ?? new MemorySettingsStore();
        Zoom = _settings.Load().CalendarZoom;
        ShowAllStaff = _settings.Load().CalendarAllStaff;
        Date = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        OpenPanels.CollectionChanged += (_, _) => RefreshVisibleTabs();
        if (live is not null)
        {
            live.AppointmentChanged += ApplyLive;
            live.OperationsChanged += OnOperationsLive;
        }
    }

    /// <summary>Ohne Verbindung: von wann der angezeigte Stand ist; sonst leer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOfflineView))]
    public partial string? OfflineNote { get; private set; }

    public bool IsOfflineView => OfflineNote is not null;

    public bool CanBook => _user.Rights.Contains(AccessRight.Book);

    /// <summary>„Kassieren“ im Termin; die Arbeitsfläche wechselt dafür zur Kassa.</summary>
    public Func<AppointmentDto, Task>? CheckoutRequested { get; set; }

    /// <summary>„Mit kassieren“ bei einem Vorschlag „Passt dazu“: Kassa mit dem Termin und dem Artikel (Slice 115).</summary>
    public Func<AppointmentDto, Guid, Task>? CheckoutWithArticleRequested { get; set; }

    /// <summary>„Kassieren“ bei einem Eintritt in einen Bereich.</summary>
    public Func<AreaVisitDto, Task>? CheckoutVisitRequested { get; set; }

    /// <summary>„Kundenkarte“ im Termin.</summary>
    public Func<Guid, Task>? CustomerRequested { get; set; }

    public IReadOnlyList<LocationDto> Locations { get; private set; } = [];

    [ObservableProperty]
    public partial LocationDto? Location { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateText), nameof(IsToday), nameof(DayTitle), nameof(DateShort), nameof(PickerDate), nameof(KeyHints), nameof(CanResetView), nameof(DayFactsText), nameof(HasDayFactsText), nameof(DayFactsTip), nameof(OverviewDayLine), nameof(HasOverviewDayLine))]
    public partial DateOnly Date { get; set; }

    /// <summary>Für den Datumswähler, der mit <see cref="DateTime"/> arbeitet.</summary>
    public DateTime? PickerDate
    {
        get => Date.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value is { } picked)
            {
                Date = DateOnly.FromDateTime(picked);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyHints), nameof(CanResetView))]
    public partial bool ByRoom { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CalendarColumn> Columns { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<HourMark> Hours { get; private set; } = [];

    [ObservableProperty]
    public partial double DayHeight { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial AppointmentViewModel? Selected { get; private set; }

    /// <summary>
    /// Das gemeinsame Eingabefenster für neue Einträge (Termin, Paar oder Spa-Tag, Pause, Sperrzeit, Notiz, Sauna-Eintritt) mit Umschaltleiste.
    /// Welche Art gerade offen ist, steht in <see cref="NewEntryViewModel.Content"/>; die Zugriffe unten sind dafür Abkürzungen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel), nameof(Booking), nameof(GroupBooking), nameof(Areas), nameof(Entry), nameof(KeyHints))]
    public partial NewEntryViewModel? NewEntry { get; private set; }

    public BookingViewModel? Booking => NewEntry?.Content as BookingViewModel;

    /// <summary>Paar- oder Spa-Tag-Buchung.</summary>
    public GroupBookingViewModel? GroupBooking => NewEntry?.Content as GroupBookingViewModel;

    /// <summary>Sauna und Ruheraum.</summary>
    public AreasViewModel? Areas => NewEntry?.Content as AreasViewModel;

    /// <summary>Warteliste des Standorts.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial WaitlistViewModel? WaitlistPanel { get; private set; }

    public bool HasPanel => Selected is not null || NewEntry is not null || WaitlistPanel is not null || GapsPanel is not null
        || FreeTime is not null || Absence is not null || Request is not null || RoomCarePanel is not null || MessagesPanel is not null || MessageDetail is not null
        || CompanionPanel is not null;

    /// <summary>Offene Wünsche der Warteliste am Standort, nach gewünschtem Beginn.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWaitlist), nameof(WaitlistPreview), nameof(WaitlistTitle), nameof(WaitlistMore), nameof(HasWaitlistMore), nameof(WaitlistTip), nameof(WaitlistLabel))]
    public partial IReadOnlyList<WaitlistRow> Waitlist { get; private set; } = [];

    public bool HasWaitlist => Waitlist.Count > 0;

    public IReadOnlyList<WaitlistRow> WaitlistPreview => [.. Waitlist.Take(3)];

    public string WaitlistTitle => $"WARTELISTE · {Waitlist.Count}";

    public string WaitlistMore => $"und {Waitlist.Count - 3} weitere";

    public bool HasWaitlistMore => Waitlist.Count > 3;

    /// <summary>Sauna und Ruheraum auf einen Blick in der Seitenleiste; leer ohne Bereiche oder ohne Verbindung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAreaGlances), nameof(AreaChip), nameof(AreaChipCompact), nameof(AreaTip), nameof(AreaLabel))]
    public partial IReadOnlyList<AreaGlance> AreaGlances { get; private set; } = [];

    public bool HasAreaGlances => AreaGlances.Count > 0;

    /// <summary>Der Standort hat Bereiche (Sauna, Ruheraum), in die Eintritte gebucht werden.</summary>
    public bool HasAreas => _resources?.Rooms.Any(r => r.IsArea) == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverviewChip), nameof(OverviewCompact), nameof(OverviewTip), nameof(OverviewLabel))]
    public partial DayOverview? Overview { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<UpcomingItem> Upcoming { get; private set; } = [];

    /// <summary>„Überfällig“ im Tagesüberblick: bis zu fünf Termine, jeder öffnet den Termin (Ä100).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<UpcomingItem> OverdueList { get; private set; } = [];

    /// <summary>„und 2 weitere“ über der Liste; leer, wenn alle überfälligen Termine darin stehen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverdueMore))]
    public partial string OverdueMoreText { get; private set; } = "";

    public bool HasOverdueMore => OverdueMoreText.Length > 0;

    /// <summary>„Geburtstag heute: Brigitte Müller (10:30)“ im Tagesüberblick über „Als Nächstes“; ohne Geburtstag am Tag <c>null</c>.</summary>
    [ObservableProperty]
    public partial string? BirthdayLine { get; private set; }

    /// <summary>Vorschau unter dem Mauszeiger (freie Viertelstunde) oder beim Ziehen eines Termins.</summary>
    [ObservableProperty]
    public partial CalendarGhost? Ghost { get; private set; }

    [ObservableProperty]
    public partial double ColumnsWidth { get; private set; }

    /// <summary>Maßstab: Minutenhöhe aus dem Zoom, Spaltenbreite aus der Fensterbreite.</summary>
    [ObservableProperty]
    public partial CalendarScale Scale { get; private set; } = CalendarScale.Default;

    /// <summary>Gewählte Minutenhöhe; leer heißt „Tag einpassen“: der sichtbare Tag passt ohne Scrollen ins Fenster.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFitted))]
    public partial double? Zoom { get; private set; }

    public bool IsFitted => Zoom is null;

    /// <summary>Kurzfassung des Tagesüberblicks für die Kopfzeile, z. B. „17 Termine · 2 da · 1 offen · 3 unbestätigt · 1 überfällig“.</summary>
    public string OverviewChip => Overview is { } o
        ? $"{o.Appointments} Termine · {o.CheckedIn} da · {o.Open} offen{(o is { UsesConfirmation: true, Unconfirmed: > 0 } ? $" · {o.Unconfirmed} unbestätigt" : "")}{(o.Overdue > 0 ? $" · {o.Overdue} überfällig" : "")}"
        : "";

    /// <summary>Kompakte Kopfzeile: Zahl der Termine statt der Kurzfassung.</summary>
    public string OverviewCompact => Overview is { } o ? o.Appointments.ToString(System.Globalization.CultureInfo.CurrentCulture) : "–";

    /// <summary>Hinweis am Zähler; in der kompakten Kopfzeile steht hier der Text, den der Chip nicht zeigt.</summary>
    /// <summary>Text des Zählers in der Kopfzeile: die Kurzfassung, kompakt nur die Zahl der Termine.</summary>
    public string OverviewLabel => IsCompactHeader ? OverviewCompact : OverviewChip;

    public string OverviewTip => ChipTip(OverviewChip, "Tagesüberblick und als Nächstes");

    /// <summary>Hinweis eines Chips der Kopfzeile: ein Satz ohne den Namen des Chips; nur kompakt, wo der Chip bloß Symbol und Zahl zeigt, steht der Name davor.</summary>
    private string ChipTip(string name, string text) => IsCompactHeader ? $"{name}: {text}" : text;

    /// <summary>Umsatz laut Kalender nur mit dem Recht „Berichte mit Beträgen“.</summary>
    public bool CanSeeRevenue => _user.Rights.Contains(AccessRight.ViewAmountReports);

    /// <summary>Belegung der Bereiche für die Kopfzeile, z. B. „Sauna 10 / 12 +1“; alle Bereiche zeigt das Seitenpanel.</summary>
    public string AreaChip => AreaGlances.Count switch
    {
        0 => "",
        1 => $"{AreaGlances[0].Name} {AreaGlances[0].Text}",
        _ => $"{AreaGlances[0].Name} {AreaGlances[0].Text} +{AreaGlances.Count - 1}",
    };

    /// <summary>Kompakte Kopfzeile: nur die Belegung des ersten Bereichs.</summary>
    public string AreaChipCompact => AreaGlances.Count == 0 ? "" : AreaGlances[0].Text;

    public string AreaLabel => IsCompactHeader ? AreaChipCompact : AreaChip;

    public string AreaTip => ChipTip(AreaChip, "Belegung und Eintritte von Sauna und Ruheraum");

    /// <summary>Text des Chips in der Kopfzeile: „Warteliste 3“, kompakt nur die Zahl.</summary>
    public string WaitlistLabel => IsCompactHeader ? Waitlist.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) : $"Warteliste {Waitlist.Count}";

    public string WaitlistTip => ChipTip("Warteliste", Waitlist.Count == 1 ? "1 offener Wunsch" : $"{Waitlist.Count} offene Wünsche");

    /// <summary>Standortwahl nur, wenn es mehr als einen Standort gibt.</summary>
    public bool HasLocationChoice => Locations.Count > 1;

    /// <summary>Rote Linie „jetzt“, nur am heutigen Tag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNow))]
    public partial double? NowTop { get; private set; }

    public bool ShowNow => NowTop is not null;

    [ObservableProperty]
    public partial string NowText { get; private set; } = "";

    /// <summary>Kurze Rückmeldung unten im Kalender, z. B. nach dem Verschieben.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyHints))]
    public partial bool CanUndo { get; private set; }

    public bool HasToast => Toast is not null;

    public bool IsToday => Date == DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public string DayTitle => Filter is { } filter ? filter.Title : IsToday ? "Heute" : Date.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("de-AT"));

    /// <summary>Datum unter dem Tagestitel; bei „Heute“ mit Wochentag, sonst steht der schon im Titel.</summary>
    public string DateShort => Filter is not null ? RangeText
        : Date.ToString(IsToday ? "dddd, d. MMMM yyyy" : "d. MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-AT"));

    public string LocationName => Location?.Name ?? "";

    /// <summary>Standort hinter dem Datum, nur wenn es mehrere gibt.</summary>
    public string LocationSuffix => HasLocationChoice && Location is { } l ? $"· {l.Name}" : "";

    public string DateText => DayLayout.DateText(Date);

    public async Task InitializeAsync()
    {
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is null)
        {
            Error = locations.Error;
            return;
        }

        Locations = locations.Value;
        OnPropertyChanged(nameof(Locations));
        OnPropertyChanged(nameof(HasLocationChoice));
        if (Locations.Count == 0)
        {
            Error = "Noch kein Standort angelegt. Standorte, Räume und Team pflegst du unter „Verwaltung“ → „Stammdaten“.";
            return;
        }

        _openedByRoom = ByRoom;
        Location = Locations[0];
    }

    partial void OnLocationChanged(LocationDto? value)
    {
        OnPropertyChanged(nameof(LocationName));
        OnPropertyChanged(nameof(LocationSuffix));
        ResetMessages();
        _resources = null;
        _subscriptionsFor = null;
        _subscriptionNotices = [];
        Subscriptions = [];
        Waitlist = [];
        WaitlistPanel = null;
        GapsPanel = null;
        _ = LoadAsync();
        if (value is not null && _live is not null)
        {
            _ = _live.WatchAsync(value.Id);
        }
    }

    partial void OnDateChanged(DateOnly value)
    {
        // Die gemeinsame Buchung gilt für einen Tag; die Bereiche zeigen den neuen Tag.
        if (GroupBooking is not null)
        {
            NewEntry = null;
        }

        ResetMessages();
        GapsPanel?.Reset();
        if (!_changingDay)
        {
            _ = LoadAsync();
        }
    }

    partial void OnByRoomChanged(bool value)
    {
        // Ein Filter auf eine Person passt nicht zur Raumansicht und umgekehrt.
        if (Filter is { } filter && filter.ByRoom != value)
        {
            Filter = null;
            return;
        }

        Rebuild();
        if (value)
        {
            MoveBookingToRoomView();
        }
    }

    /// <summary>
    /// „Alle zeigen“: zusätzlich Mitarbeitende ohne Dienst an diesem Standort, ihre Spalten schraffiert mit dem Grund. Gilt je Gerät
    /// (wie der Zoom) und nur in der Teamansicht.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllStaffText), nameof(AllStaffCompact), nameof(AllStaffTip), nameof(AllStaffLabel))]
    public partial bool ShowAllStaff { get; set; }

    /// <summary>Personen, die ohne „Alle zeigen“ keine Spalte hätten (kein Dienst hier, kein Termin; auch Dienst an einem anderen Standort); in der Raumansicht und in der 7-Tage-Ansicht 0.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleAllStaff))]
    [NotifyPropertyChangedFor(nameof(AllStaffText), nameof(AllStaffCompact), nameof(AllStaffTip), nameof(AllStaffLabel))]
    public partial int HiddenStaffCount { get; private set; }

    /// <summary>Die Schaltfläche „Alle zeigen“ gibt es nur, wenn sie etwas ändert.</summary>
    public bool CanToggleAllStaff => HiddenStaffCount > 0;

    public string AllStaffText => ShowAllStaff ? "Nur mit Dienst" : $"Alle zeigen ({HiddenStaffCount})";

    /// <summary>Kompakte Kopfzeile: die Zahl der verborgenen Personen, mit „Alle zeigen“ nur das Symbol.</summary>
    public string AllStaffCompact => ShowAllStaff ? "" : HiddenStaffCount.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string AllStaffLabel => IsCompactHeader ? AllStaffCompact : AllStaffText;

    public string AllStaffTip => ChipTip(AllStaffText, ShowAllStaff ? "Nur Mitarbeitende mit Dienst an diesem Standort" : "Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund");

    partial void OnShowAllStaffChanged(bool value)
    {
        if (_settings.Load().CalendarAllStaff != value)
        {
            _settings.Save(_settings.Load() with { CalendarAllStaff = value });
        }

        Rebuild();
    }

    [RelayCommand]
    private void ToggleAllStaff() => ShowAllStaff = !ShowAllStaff;

    [RelayCommand]
    private void ShowTeam()
    {
        Filter = null;
        ByRoom = false;
    }

    [RelayCommand]
    private void ShowRooms()
    {
        Filter = null;
        ByRoom = true;
    }

    /// <summary>Einen Tag zurück, in der 7-Tage-Ansicht eine Woche.</summary>
    [RelayCommand]
    private void PreviousDay() => Date = Date.AddDays(-SpanDays);

    [RelayCommand]
    private void NextDay() => Date = Date.AddDays(SpanDays);

    [RelayCommand]
    private void Today() => Date = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    [RelayCommand]
    private void ZoomIn() => SetZoom(ZoomSteps.FirstOrDefault(z => z > Scale.PixelsPerMinute + 0.01, ZoomSteps[^1]));

    [RelayCommand]
    private void ZoomOut() => SetZoom(ZoomSteps.LastOrDefault(z => z < Scale.PixelsPerMinute - 0.01, ZoomSteps[0]));

    /// <summary>Zurück zu „Tag einpassen“.</summary>
    [RelayCommand]
    private void ZoomFit() => SetZoom(null);

    private void SetZoom(double? zoom)
    {
        Zoom = zoom;
        _settings.Save(_settings.Load() with { CalendarZoom = zoom });
        Rebuild();
    }

    /// <summary>Unter dieser Breite der Spalten werden die Chips der Kopfzeile kompakt (Design-Brief 7, Ä16).</summary>
    public const double CompactHeaderWidth = 1100;

    /// <summary>Kompakte Chips (Symbol und Zahl, Text im Hinweis); vor der ersten Meldung der Größe bleibt es bei der vollen Form.</summary>
    public bool IsCompactHeader => _viewportWidth is > 0 and < CompactHeaderWidth;

    /// <summary>Das Suchfeld wird in der kompakten Kopfzeile schmaler, damit der Datumstitel Platz behält.</summary>
    public double SearchWidth => IsCompactHeader ? 130 : 200;

    /// <summary>Sichtbare Fläche der Spalten; die Ansicht meldet sie bei jeder Größenänderung.</summary>
    public void SetViewport(double width, double height)
    {
        if (Math.Abs(width - _viewportWidth) < 1 && Math.Abs(height - _viewportHeight) < 1)
        {
            return;
        }

        var onlyWidth = Math.Abs(height - _viewportHeight) < 1 && Columns.Count > 0;
        var wasCompact = IsCompactHeader;
        (_viewportWidth, _viewportHeight) = (width, height);
        if (wasCompact != IsCompactHeader)
        {
            // Die Chips der Kopfzeile zeigen kompakt Symbol und Zahl, voll Symbol und Text.
            foreach (var name in new[] { nameof(IsCompactHeader), nameof(SearchWidth), nameof(OverviewLabel), nameof(OverviewTip), nameof(AreaLabel), nameof(AreaTip), nameof(WaitlistLabel), nameof(WaitlistTip), nameof(AllStaffLabel), nameof(AllStaffTip), nameof(RoomFilterLabel), nameof(RoomFilterTip), nameof(MessagesLabel), nameof(MessagesTip), nameof(SubscriptionsTip), nameof(ClosureChipLabel) })
            {
                OnPropertyChanged(name);
            }
        }

        if (onlyWidth)
        {
            // Nur die Breite: Minutenhöhe und Lage der Termine bleiben, also nur Spaltenbreite und was daran hängt neu rechnen
            // (das Panel öffnet oder schließt sich, ein Neuaufbau aller Karten wäre spürbar langsam).
            ResizeColumns();
            return;
        }

        Rebuild();
    }

    /// <summary>Zahl der Personenspalten des Tages, solange die Raumansicht läuft (bestimmt deren Spaltenbreite); sonst 0.</summary>
    private int _teamColumns;

    private void ResizeColumns()
    {
        Scale = new CalendarScale(Scale.PixelsPerMinute, ColumnWidthFor(Columns.Count));
        ColumnsWidth = ColumnsWidthFor(Columns.Count, Scale);
        UpdateSlotMarks();
        UpdateGhost();
        UpdateEdges();
    }

    private static double ColumnsWidthFor(int columns, CalendarScale scale) =>
        Math.Max(0, columns * (scale.ColumnWidth + DayLayout.ColumnGap) - DayLayout.ColumnGap);

    /// <summary>
    /// Minutenhöhe: gewählter Zoom oder so, dass der sichtbare Tag ins Fenster passt (nicht kleiner als die kleinste Stufe).
    /// Spaltenbreite: Spalten teilen sich die Breite, aber nie schmaler als 160 Pixel; dann scrollt der Kalender waagrecht.
    /// </summary>
    private double MinuteHeight(CalendarDay day) =>
        Zoom ?? (_viewportHeight > 0
            ? Math.Clamp(_viewportHeight / Math.Max(60, DayLayout.VisibleMinutes(day, _zone)), ZoomSteps[0], ZoomSteps[^1])
            : DayLayout.PixelsPerMinute);

    private double ColumnWidthFor(int columns)
    {
        // Die Raumansicht hat dieselbe Spaltenbreite wie die Teamansicht (Ä65): sie richtet sich nach den Personenspalten des Tages, nicht nach der Zahl der Räume;
        // bei vielen Räumen scrollt der Kalender waagrecht wie bei vielen Personen.
        // Mit „Alle zeigen“ behalten die Spalten die Breite, die sie ohne die zusätzlichen hätten: nichts wird gestaucht, der Kalender scrollt waagrecht.
        var shown = _teamColumns > 0 ? _teamColumns : ShowAllStaff ? columns - HiddenStaffCount : columns;
        return shown > 0 && _viewportWidth > 0
            ? Math.Max(160, Math.Floor((_viewportWidth - (shown - 1) * DayLayout.ColumnGap) / shown))
            : DayLayout.ColumnWidth;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Location is not { } location)
        {
            return;
        }

        Error = null;
        if (_resources?.Location.Id != location.Id)
        {
            var resources = await _api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources").ConfigureAwait(true);
            if (resources.Value is null)
            {
                Error = resources.Error;
                return;
            }

            _resources = resources.Value;
            _zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
            OnPropertyChanged(nameof(HasAreas));
        }

        var date = Date;
        var span = SpanDays;
        var loaded = await LoadDaysAsync(location, date, span).ConfigureAwait(true);
        if (loaded.Days is null)
        {
            Error = loaded.Error;
            return;
        }

        // Inzwischen auf einen anderen Tag, Standort oder Filter gewechselt: diese Antwort nicht mehr anzeigen.
        if (date != Date || location.Id != Location?.Id || span != SpanDays)
        {
            return;
        }

        OfflineNote = loaded.CachedAt is { } savedAt
            ? $"Ohne Verbindung · Stand {DayLayout.Local(savedAt, _zone):dd.MM. HH:mm}"
            : null;
        _week = span > 1 ? loaded.Days : null;
        _day = span > 1 ? Merge(loaded.Days) : loaded.Days[0];
        ShowPending();
        if (Booking is { } booking && booking.Date == Date)
        {
            booking.UpdateDayNotice(BookingViewModel.NoticeFor(_day));
            booking.UpdateOpeningDay(SingleDay(booking.Date));
        }

        AfterDayLoaded();
        NotifyDayFacts();
        await LoadOperationsAsync(location, date).ConfigureAwait(true);
        await LoadSubscriptionsAsync(location).ConfigureAwait(true);
        await LoadHoldsAsync(location).ConfigureAwait(true);
        await LoadHrFollowUpsAsync().ConfigureAwait(true);
        await LoadDutyRemindersAsync(location.Id).ConfigureAwait(true);
        await LoadNoteSymbolsAsync().ConfigureAwait(true);
        await LoadWaitlistAsync(location.Id).ConfigureAwait(true);
        await LoadGapsAsync().ConfigureAwait(true);
        if (Areas is not null)
        {
            OpenNewEntry(NewEntryKind.Area, AnchorHere());
        }
        else if (HasAreas)
        {
            await LoadAreaGlancesAsync(location.Id, date).ConfigureAwait(true);
        }
        else
        {
            AreaGlances = [];
            _areaVisits = [];
        }
    }

    private async Task LoadAreaGlancesAsync(Guid locationId, DateOnly date)
    {
        var areas = await _api.GetAsync<List<AreaDay>>($"api/locations/{locationId}/areas?date={date:yyyy-MM-dd}").ConfigureAwait(true);
        if (date == Date && locationId == Location?.Id)
        {
            ShowAreaGlances(areas.Value ?? []);
        }
    }

    private void ShowAreaGlances(IReadOnlyList<AreaDay> areas) => ShowAreas(areas);

    /// <summary>
    /// Vorgemerkte Änderungen über den geladenen Stand legen, damit der Kalender zeigt, was die Rezeption schon eingetragen hat.
    /// Wird auch nach jedem Abgleich aufgerufen.
    /// </summary>
    public void ShowPending()
    {
        if (_day is null)
        {
            return;
        }

        var pending = _api.Offline?.Pending ?? [];
        _pending = [.. pending.Where(p => p.Preview is not null).Select(p => p.Preview!.Id)];
        var list = _day.Appointments.ToList();
        foreach (var preview in pending.Select(p => p.Preview).OfType<AppointmentDto>().Where(p => p.LocationId == _day.LocationId))
        {
            list.RemoveAll(a => a.Id == preview.Id);
            if (InRange(DateOnly.FromDateTime(DayLayout.Local(preview.Start, _zone))))
            {
                list.Add(preview);
            }
        }

        _day = _day with { Appointments = list };
        Rebuild();
    }

    /// <summary>Klick auf eine Stelle in einer Spalte: freie Stelle bucht, ein Termin wird geöffnet.</summary>
    public void Open(CalendarItem item)
    {
        if (item.Appointment is { } appointment)
        {
            OpenAppointment(appointment);
        }
    }

    public void OpenAppointment(AppointmentDto appointment)
    {
        if (_resources is null)
        {
            return;
        }

        ClosePanels();
        MarkedId = appointment.Id;
        Selected = PanelFor(appointment);
        Reselect();
    }

    /// <summary>Macht der nächsten Seite Platz: die angezeigte Seite bleibt als Reiter stehen (<see cref="OpenPanels"/>).</summary>
    private void ClosePanels()
    {
        _makingRoom = true;
        try
        {
            Selected = null;
            NewEntry = null;
            WaitlistPanel = null;
            GapsPanel = null;
            Absence = null;
            RoomCarePanel = null;
            MessagesPanel = null;
            MessageDetail = null;
            CompanionPanel = null;
            CloseFreeTime();
            CloseRequest();
        }
        finally
        {
            _makingRoom = false;
        }

        _shown = null;
        foreach (var tab in OpenPanels)
        {
            tab.IsSelected = false;
        }
    }

    /// <summary>„Warteliste“ in der Seitenleiste: alle Wünsche mit Sprung zum gewünschten Tag.</summary>
    [RelayCommand]
    private void OpenWaitlist()
    {
        ClosePanels();
        WaitlistPanel = new WaitlistViewModel(LocationName, Waitlist, () => WaitlistPanel = null);
        Rebuild();
        _ = LoadWaitlistFreeAsync(Waitlist);
    }

    private async Task LoadWaitlistAsync(Guid locationId)
    {
        var result = await _api.GetAsync<List<WaitlistStaffEntry>>($"api/locations/{locationId}/waitlist").ConfigureAwait(true);
        if (locationId != Location?.Id || result.Value is null)
        {
            return;
        }

        Waitlist = [.. result.Value.Where(e => e.Status == WaitlistStatus.Waiting).Select(WaitlistRowOf)];
        if (WaitlistPanel is not null)
        {
            WaitlistPanel = new WaitlistViewModel(LocationName, Waitlist, () => WaitlistPanel = null);
            _ = LoadWaitlistFreeAsync(Waitlist);
        }
    }

    /// <summary>Live-Update eines anderen Arbeitsplatzes; ältere Stände als der angezeigte werden verworfen.</summary>
    public void ApplyLive(AppointmentDto appointment) => Apply(appointment);

    /// <summary>Eigene Buchung (z. B. aus dem Assistenten): der Termin erscheint, Hinweise der Antwort bleiben sichtbar.</summary>
    public void ApplyBooked(AppointmentDto appointment)
    {
        Apply(appointment);
        ShowWarnings(appointment);
    }

    private void Apply(AppointmentDto appointment)
    {
        if (_day is null || appointment.LocationId != _day.LocationId)
        {
            return;
        }

        var existing = _day.Appointments.FirstOrDefault(a => a.Id == appointment.Id);
        if (existing is not null && (existing.UpdatedAt > appointment.UpdatedAt || IsSameState(existing, appointment)))
        {
            return;
        }

        var onThisDay = InRange(DateOnly.FromDateTime(DayLayout.Local(appointment.Start, _zone)));
        var list = _day.Appointments.Where(a => a.Id != appointment.Id).ToList();
        if (onThisDay)
        {
            list.Add(appointment);
        }

        _day = _day with { Appointments = list };
        // Ä281: Während eine Karte im Panel bearbeitet wird, bleibt das Panel mit dem Stand des Bearbeitungsbeginns; das Speichern meldet dann den Konflikt.
        if (Selected?.Appointment.Id == appointment.Id && !(onThisDay && Selected.IsEditingCard))
        {
            Selected = onThisDay ? PanelFor(appointment) : null;
        }

        Rebuild();
    }

    /// <summary>
    /// Ä281: Eine Änderung aus dem Panel kam von einem veralteten Stand. Der Kalender übernimmt den aktuellen Termin; ist er offen, steht er mit
    /// der Meldung oben im Panel neu da (die Bearbeitungskarte ist danach zu), sonst steht die Meldung über dem Kalender.
    /// </summary>
    private void ShowStale(AppointmentDto current, string message)
    {
        Apply(current);
        if (Selected?.Appointment.Id != current.Id || _day?.Appointments.FirstOrDefault(a => a.Id == current.Id) is not { } shown)
        {
            ShowProblem("Nicht geändert", message);
            return;
        }

        Selected = PanelFor(shown);
        Selected.Error = message;
        Rebuild();
    }

    /// <summary>
    /// Der Server meldet die eigene Änderung auch an diesen Arbeitsplatz zurück (Live). Derselbe Stand baut den Tag nicht noch einmal auf:
    /// sonst entstehen die Karten neu und ein Flyout an einer Karte (Vorschläge nach „Kommt später“) ginge nach einem Augenblick wieder zu.
    /// </summary>
    private static bool IsSameState(AppointmentDto current, AppointmentDto incoming) =>
        current with { Warnings = null } == incoming with { Warnings = null }
        && (current.Warnings ?? []).SequenceEqual(incoming.Warnings ?? []);

    private AppointmentViewModel PanelFor(AppointmentDto appointment)
    {
        var employee = _resources!.Employees.FirstOrDefault(e => e.Id == appointment.EmployeeId)?.InHouseName() ?? "";
        var room = _resources.Rooms.FirstOrDefault(r => r.Id == appointment.RoomId)?.Name ?? "";
        var canStatus = _user.Rights.Contains(AccessRight.ChangeStatus)
            && (_user.Access?.Role != StaffRole.Therapist || appointment.EmployeeId == _user.EmployeeId);
        var panel = new AppointmentViewModel(_api, appointment, employee, room, _zone, canStatus, CanBook,
            async changed =>
            {
                Apply(changed);
                ShowPending();
                ShowWarnings(changed);
                OfferUndo(appointment, changed);
                await Task.CompletedTask.ConfigureAwait(true);
            },
            () =>
            {
                Selected = null;
                Reselect();
            },
            CanBook ? CheckoutRequested : null,
            CustomerRequested,
            appointment.GroupId is { } groupId ? [.. _day!.Appointments.Where(a => a.GroupId == groupId && a.Id != appointment.Id)] : null,
            async result =>
            {
                ApplyAll(result.Appointments);
                ShowWarnings(result.Appointments);
                await Task.CompletedTask.ConfigureAwait(true);
            },
            Location?.Currency,
            SeriesFor(appointment),
            CanBook ? SeriesChangedAsync : null,
            CanBook ? start => MoveToDayAsync(appointment, start) : null)
        {
            Symbol = appointment.PlaceholderGuest is null && _day!.Guests?.FirstOrDefault(g => g.CustomerId == appointment.CustomerId) is { } guest
                ? PersonNames.Symbol(guest.Salutation)
                : null,
            Birthday = appointment.PlaceholderGuest is null && _day!.Guests?.FirstOrDefault(g => g.CustomerId == appointment.CustomerId) is { } birthdayGuest
                ? BirthdayText.Line(birthdayGuest.Birthday)
                : null,
            GroupParts = GroupPartsOf(appointment),
            OpenPart = OpenAppointment,
            CheckoutGroupRequested = CanBook ? CheckoutGroupRequested : null,
            CheckoutWithArticleRequested = CanBook ? CheckoutWithArticleRequested : null,
            Countries = Countries(),
            CustomerChanged = () => CustomerChangedAsync(appointment.Id),
            RoomInfo = RoomInfoOf(appointment),
            Time = _time,
            Servings = Operations?.ServingsFor(appointment.Id, CanCheckCare, ReloadOperationsAsync),
            CatalogTreatment = _resources.Treatments.FirstOrDefault(t => t.Id == appointment.TreatmentId),
            RoomCare = () => ByRoom ? (_resources.Rooms.FirstOrDefault(r => r.Id == appointment.RoomId)?.CleaningMinutes ?? 0, DayLayout.CareTaskOf(_day!, appointment)) : (0, null),
            ReportProblem = (title, message) => ShowProblem(title, message),
            Stale = ShowStale,
            Links = ConflictLinksOf,
            FocusRequested = RequestPanelFocus,
            DefaultCountry = Location is { } place ? PersonNames.CountryOfTimeZone(place.TimeZone) : FiscalCountry.AT,
            OpenOtherAppointment = OpenAppointmentOn,
            ShowToast = text => ShowToast(text),
            ContactActions = () => ContactActions,
            Files = Files,
            Dialogs = Dialogs,
            MayReturnDeposit = _user.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager,
            MayChargeFee = _user.Rights.Contains(AccessRight.SuperviseCash),
            MayReadHealthData = _user.Rights.Contains(AccessRight.ViewHealthData),
            IsBilled = _day!.BilledAppointmentIds?.Contains(appointment.Id) == true,
        };
        panel.Loads = Task.WhenAll(panel.LoadDepositAsync(), panel.LoadCardGuaranteeAsync(), panel.LoadRemindersAsync(), panel.LoadConsentAsync(), panel.LoadAnamnesisAsync(),
            panel.LoadRecommendationsAsync(Selected?.Appointment.Id == appointment.Id ? Selected : null));
        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppointmentViewModel.IsEditingCard))
            {
                CatchUpAfterEditing(panel);
            }
        };

        return panel;
    }

    /// <summary>
    /// Ä281: Während der Bearbeitung blieb das Panel beim Stand des Bearbeitungsbeginns. Endet sie ohne Speichern und hat der Tag inzwischen
    /// einen neueren Stand des Termins, zeigt das Panel ihn jetzt.
    /// </summary>
    private void CatchUpAfterEditing(AppointmentViewModel panel)
    {
        if (panel.IsEditingCard || Selected != panel
            || _day?.Appointments.FirstOrDefault(a => a.Id == panel.Appointment.Id) is not { } now || now.Version == panel.Appointment.Version)
        {
            return;
        }

        Selected = PanelFor(now);
        Rebuild();
    }

    /// <summary>Öffnet Links und kopiert Text für die Kontaktzeilen im Termin-Panel; das Fenster setzt es beim Anzeigen.</summary>
    public IContactActions? ContactActions { get; set; }

    /// <summary>Der Dateidialog für das Foto eines Anamnesebogens; das Fenster setzt ihn beim Anzeigen.</summary>
    public IFilePicker? Files { get; set; }

    /// <summary>„Alle kassieren“ bei einer gemeinsamen Buchung.</summary>
    public Func<IReadOnlyList<AppointmentDto>, Task>? CheckoutGroupRequested { get; set; }

    /// <summary>Länder der Standorte für die Vorschau der Anrede.</summary>
    private IReadOnlyList<FiscalCountry> Countries() =>
        Locations.Count == 0 ? [FiscalCountry.AT] : [.. Locations.Select(l => PersonNames.CountryOfTimeZone(l.TimeZone)).Distinct().Order()];

    /// <summary>Alle Teile der gemeinsamen Buchung nach Beginn, auch die stornierten (sie stehen durchgestrichen dabei).</summary>
    private IReadOnlyList<GroupPartRow> GroupPartsOf(AppointmentDto appointment)
    {
        if (appointment.GroupId is not { } groupId || _resources is null)
        {
            return [];
        }

        return [.. _day!.Appointments.Where(a => a.GroupId == groupId).OrderBy(a => a.Start).ThenBy(a => a.CustomerName, StringComparer.Ordinal).ThenBy(a => a.Id).Select(a => new GroupPartRow(a,
            DayLayout.TimeText(a.Start, a.End, _zone), AppointmentViewModel.GuestName(a),
            a.PlaceholderGuest is null && _day.Guests?.FirstOrDefault(g => g.CustomerId == a.CustomerId) is { } guest ? PersonNames.Symbol(guest.Salutation) : null,
            a.TreatmentName, _resources.Employees.FirstOrDefault(e => e.Id == a.EmployeeId)?.InHouseName() ?? "",
            _resources.Rooms.FirstOrDefault(r => r.Id == a.RoomId)?.Name ?? "", a.Id == appointment.Id))];
    }

    /// <summary>Infos zum Raum des Termins: Raumart, Raumpflege, Behandlungen und die Termine des Raums an dem Tag (der im Panel ist dabei).</summary>
    private RoomInfoViewModel? RoomInfoOf(AppointmentDto appointment)
    {
        if (_resources?.Rooms.FirstOrDefault(r => r.Id == appointment.RoomId) is not { } room)
        {
            return null;
        }

        var day = DateOnly.FromDateTime(DayLayout.Local(appointment.Start, _zone));
        var rows = _day!.Appointments
            .Where(a => a.RoomId == room.Id && (a.Id == appointment.Id || a.Status != AppointmentStatus.Cancelled)
                && DateOnly.FromDateTime(DayLayout.Local(a.Start, _zone)) == day)
            .OrderBy(a => a.Start).ThenBy(a => a.Id)
            .Select(a => new RoomDayRow(DayLayout.TimeText(a.Start, a.End, _zone), AppointmentViewModel.GuestName(a), a.TreatmentName, a.Id == appointment.Id)).ToList();
        var dayTitle = day == DateOnly.FromDateTime(_time.GetLocalNow().DateTime) ? "Heute" : $"Am {day:dd.MM.yyyy}";
        return new RoomInfoViewModel(room, DayLayout.RoomFactsOf(room, _resources), rows, dayTitle,
            CanEditRooms ? () => AdminRequested!("masterData", "locations", room.Id) : null);
    }

    /// <summary>Kundendaten im Panel geändert: neu laden und den Termin mit den neuen Daten zeigen.</summary>
    private async Task CustomerChangedAsync(Guid appointmentId)
    {
        await LoadAsync().ConfigureAwait(true);
        if (_day?.Appointments.FirstOrDefault(a => a.Id == appointmentId) is { } current)
        {
            Selected = PanelFor(current);
            Reselect();
        }
    }

    /// <summary>Mehrere Termine einer gemeinsamen Buchung übernehmen; der offene Termin zeigt danach den Stand aller.</summary>
    private void ApplyAll(IEnumerable<AppointmentDto> appointments)
    {
        foreach (var appointment in appointments)
        {
            Apply(appointment);
        }

        if (Selected is { } selected && _day?.Appointments.FirstOrDefault(a => a.Id == selected.Appointment.Id) is { } current)
        {
            Selected = PanelFor(current);
            Rebuild();
        }
    }

    /// <summary>Alle Leisten sind 360 Pixel breit (wie KI und Hilfe), nur das Formular für die Kundendaten ist breiter.</summary>
    public double PanelWidth => Selected is { IsEditingCustomer: true } ? 560 : 360;

    /// <summary>Der Termin hat im Rahmen der Seitenleiste einen eigenen Kopf (Initialen, Gastname, Behandlung, Status); alle anderen Leisten den einfachen.</summary>
    public bool HasAppointmentHeader => Selected is not null;

    partial void OnSelectedChanged(AppointmentViewModel? oldValue, AppointmentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnSelectedPanelChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnSelectedPanelChanged;
        }

        OnPropertyChanged(nameof(PanelWidth));
        OnPropertyChanged(nameof(HasAppointmentHeader));
    }

    private void OnSelectedPanelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppointmentViewModel.IsEditingCustomer))
        {
            OnPropertyChanged(nameof(PanelWidth));
        }
    }

    private void Rebuild()
    {
        if (_day is null || _resources is null)
        {
            return;
        }

        // Die Spaltenbreite ändert die Lage der Termine nicht, deshalb erst nach dem Aufbau aus der Spaltenzahl.
        var ppm = MinuteHeight(_day);
        var layoutScale = new CalendarScale(ppm, DayLayout.ColumnWidth);
        // Filter aufgehoben, der Tag aber noch nicht neu geladen (Antwort unterwegs oder ohne Verbindung): aus den sieben geladenen Tagen nur den
        // angezeigten nehmen; der zusammengefasste Tag enthält Personen sonst mehrfach.
        var day = _week is not null && Filter is null ? WeekDays().FirstOrDefault(d => d.Date == Date) ?? WeekDays()[0] : _day;
        var layout = _week is not null && Filter is { } filter
            ? WeekLayout.Build(WeekDays(), _resources, filter, _zone, Selected?.Appointment.Id ?? MarkedId, _drag?.Appointment.Id, _pending, layoutScale,
                _time.GetUtcNow())
            : DayLayout.Build(day, _resources, ByRoom, _zone, Selected?.Appointment.Id ?? MarkedId, _drag?.Appointment.Id, _pending, layoutScale,
                _time.GetUtcNow(), servings: Operations?.ServingTexts, allStaff: ShowAllStaff, areaVisits: _areaVisits, onlyAvailableRooms: OnlyAvailableRooms);
        HiddenStaffCount = ByRoom || Filter is not null ? 0 : DayLayout.HiddenStaffCount(day, _resources);
        UnavailableRoomCount = ByRoom && Filter is null ? DayLayout.UnavailableRoomCount(day, _resources) : 0;
        _teamColumns = ByRoom && Filter is null ? DayLayout.TeamColumnCount(day, _resources) : 0;
        var scale = new CalendarScale(ppm, ColumnWidthFor(layout.Columns.Count));
        Scale = scale;
        _dayStart = layout.DayStart;
        Hours = layout.Hours;
        DayHeight = layout.Height;
        ApplyFocus(layout.Columns);
        ApplyNoteSymbols(layout.Columns);
        Columns = layout.Columns;
        ColumnsWidth = ColumnsWidthFor(layout.Columns.Count, scale);
        UpdateEdges();
        RefreshClosures(_day, layout.DayStart, layout.Height, scale);
        var scoped = Scoped(_day);
        Overview = DayLayout.Overview(scoped, Location?.Currency, _time.GetUtcNow());
        Upcoming = DayLayout.Upcoming(scoped, _resources, IsToday ? _time.GetUtcNow() : DateTimeOffset.MinValue, _zone);
        var overdue = DayLayout.Overdue(scoped, _resources, _time.GetUtcNow(), _zone);
        OverdueList = overdue.Items;
        OverdueMoreText = overdue.More > 0 ? $"und {overdue.More} weitere" : "";
        // In der 7-Tage-Ansicht trägt jeder Tag sein eigenes „heute“; die Zeile gilt nur für den einen Tag.
        BirthdayLine = _week is null ? DayLayout.BirthdayLine(scoped, _zone) : null;
        RefreshNow();
        UpdateSlotMarks();
        _messageDay = day;
        DayNotes = DayLayout.DayNotes(day);
        RefreshMessages();
        OnPropertyChanged(nameof(KeyHints));
        UpdateEntryPreview();
    }

    /// <summary>
    /// Auswahlring und Hervorhebung der Gruppe in den vorhandenen Terminen nachführen. Eine Auswahl ändert weder die Lage noch die Inhalte,
    /// also bleiben die Spalten stehen (ein Neuaufbau kostete bei jedem Klick den Aufbau aller Karten).
    /// </summary>
    private void Reselect()
    {
        var selected = Selected?.Appointment.Id ?? MarkedId;
        var group = _day?.Appointments.FirstOrDefault(a => a.Id == selected)?.GroupId;
        foreach (var item in Columns.SelectMany(c => c.Items))
        {
            item.IsSelected = item.Id == selected;
            item.IsGroupHighlighted = group is not null && item.Appointment?.GroupId == group && item.Id != selected;
        }

        OnPropertyChanged(nameof(KeyHints));
    }

    /// <summary>Alle 30 Sekunden: Jetzt-Linie und, am heutigen Tag, wer überfällig ist.</summary>
    public void Tick()
    {
        RefreshHoldsIfDue();
        RefreshHrFollowUpsIfDue();
        RefreshDutyRemindersIfDue();
        if (IsToday && !IsDragging)
        {
            Rebuild();
        }
        else
        {
            RefreshNow();
        }
    }

    /// <summary>Lage der Jetzt-Linie.</summary>
    public void RefreshNow()
    {
        var now = _time.GetUtcNow();
        var top = DayLayout.Top(_dayStart, now, Scale);
        var visible = _day is not null && !IsFiltered && IsToday && top >= 0 && top <= DayHeight;
        NowTop = visible ? top : null;
        NowText = visible ? $"{DayLayout.Local(now, _zone):HH:mm}" : "";
    }

    // ---------- Freie Stelle unter dem Mauszeiger ----------

    /// <summary>
    /// Einträge und Verschieben gehen in jeder Spalte einer Person oder eines Raums, auch in den sieben Tagesspalten ihrer 7-Tage-Ansicht;
    /// nur die Ansicht eines Gastes hat keine Spalte je Person oder Raum.
    /// </summary>
    private bool HasResourceColumns => Filter is not { Kind: CalendarFilterKind.Customer };

    /// <summary>
    /// Beginn der Zeitachse einer Spalte. Im Tageskalender haben alle Spalten denselben Tag; in der 7-Tage-Ansicht trägt jede Spalte ihren Tag,
    /// mit derselben Uhrzeit am oberen Rand (die Tage teilen sich die sichtbaren Stunden).
    /// </summary>
    private DateTimeOffset DayStartOf(int column) =>
        Filter is not null && Columns.ElementAtOrDefault(column)?.Date is { } date
            ? DayLayout.LocalToUtc(date, TimeOnly.FromDateTime(DayLayout.Local(_dayStart, _zone)), _zone)
            : _dayStart;

    /// <summary>Der Tag, an dem ein Eintrag in dieser Spalte liegt.</summary>
    private DateOnly DateOf(CalendarColumn column) => column.Date ?? Date;

    private DateOnly LocalDateOf(DateTimeOffset time) => DateOnly.FromDateTime(DayLayout.Local(time, _zone));

    /// <summary>Zeigt die Viertelstunde unter dem Mauszeiger, an der ein Klick buchen würde.</summary>
    public void Hover(double x, double y)
    {
        // Solange das Fenster eine Vorschau zeigt, gehört die Karte dem geplanten Eintrag.
        if (IsDragging || _preview is not null)
        {
            return;
        }

        if (!CanBook || !HasResourceColumns || Columns.Count == 0 || y < 0 || y > DayHeight || x < 0 || x > ColumnsWidth)
        {
            Ghost = null;
            _hoverAt = null;
            return;
        }

        var index = DayLayout.ColumnAt(x, Columns.Count, Scale);
        var start = DayLayout.TimeAt(DayStartOf(index), y, Scale);
        _hoverAt = (x, y);
        // Über belegter Zeit (Termin mit Vor- und Nachbereitung, Sperrzeit) gibt es nichts zu buchen. Sonst blitzt die Vorschau
        // beim Wechsel zwischen zwei Terminen in den Streifen und Rändern dazwischen auf.
        if (IsOccupied(Columns[index].Id, start, start.AddMinutes(15)))
        {
            Ghost = null;
            return;
        }

        Ghost = new CalendarGhost(DayLayout.ColumnLeft(index, Scale), DayLayout.Top(DayStartOf(index), start, Scale), 15 * Scale.PixelsPerMinute,
            $"{DayLayout.Local(start, _zone):HH:mm}", IsMove: false);
    }

    private bool IsOccupied(Guid columnId, DateTimeOffset from, DateTimeOffset until) =>
        _day is { } day && DayLayout.IsOccupied(day, ByRoom, columnId, from, until);

    public void ClearHover()
    {
        if (!IsDragging && _preview is null)
        {
            Ghost = null;
            _hoverAt = null;
        }
    }

    // ---------- Termin ziehen ----------

    /// <summary>Verschieben ist erlaubt, wenn man buchen darf und der Termin noch nicht erledigt ist.</summary>
    public bool CanMove(CalendarItem item) =>
        CanBook && HasResourceColumns && item.Appointment is { Status: AppointmentStatus.Booked or AppointmentStatus.CheckedIn };

    /// <summary>Beginnt das Ziehen; <paramref name="grabOffset"/> ist der Abstand des Mauszeigers zum oberen Rand des Termins.</summary>
    public void BeginDrag(CalendarItem item, double grabOffset)
    {
        if (!CanMove(item) || item.Appointment is not { } appointment)
        {
            return;
        }

        var column = Columns.ToList().FindIndex(c => c.Items.Contains(item));
        _drag = new DragState(appointment, grabOffset, column, appointment.Start);
        NotifyDragging();
        Rebuild();
        UpdateGhost();
    }

    /// <summary>
    /// Ein Termin, die Vorschau des neuen Termins oder der Rand einer Notiz bzw. Pause wird gerade gezogen; währenddessen zeigen die Termine keine Hover-Karten.
    /// </summary>
    public bool IsDragging => _drag is not null || _previewDrag is not null || _edgeDrag is not null;

    private void NotifyDragging()
    {
        OnPropertyChanged(nameof(IsDragging));
        OnPropertyChanged(nameof(HoverCardsEnabled));
    }

    /// <summary>Nach einem Klick auf einen Termin keine Hover-Karte, bis die Maus sich bewegt: sie liegt sonst an derselben Stelle wieder über dem Termin.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoverCardsEnabled))]
    public partial bool HoverCardsPaused { get; set; }

    /// <summary>Hover-Karten der Termine: nicht beim Ziehen und nicht direkt nach einem Klick.</summary>
    public bool HoverCardsEnabled => !IsDragging && !HoverCardsPaused;

    /// <summary>Mauszeiger bewegt: Zielspalte und auf fünf Minuten gerundeter Beginn.</summary>
    public void DragTo(double x, double y)
    {
        if (_previewDrag is not null)
        {
            DragPreviewTo(x, y);
            return;
        }

        if (_edgeDrag is not null)
        {
            DragEdgeTo(y);
            return;
        }

        if (_drag is null)
        {
            return;
        }

        var column = DayLayout.ColumnAt(x, Columns.Count, Scale);
        _drag = _drag with
        {
            Column = column,
            Start = DayLayout.SnapStart(DayStartOf(column), y - _drag.GrabOffset, Scale),
        };
        UpdateGhost();
    }

    /// <summary>Esc, verlorene Maus oder Mauszeiger außerhalb der Kalenderfläche: alles bleibt, wie es vor dem Ziehen war.</summary>
    public void CancelDrag()
    {
        if (!IsDragging)
        {
            return;
        }

        _drag = null;
        _previewDrag = null;
        _edgeDrag = null;
        NotifyDragging();
        Ghost = _preview;
        Rebuild();
    }

    /// <summary>
    /// Loslassen: der Termin steht sofort an der neuen Stelle, der Server prüft im Hintergrund.
    /// Lehnt er ab (Person, Raum oder Gerät belegt), springt der Termin zurück und die Begründung erscheint.
    /// </summary>
    public async Task DropAsync()
    {
        if (_previewDrag is { } preview)
        {
            DropPreview(preview);
            return;
        }

        if (_edgeDrag is { } edge)
        {
            await DropEdgeAsync(edge).ConfigureAwait(true);
            return;
        }

        if (_drag is not { } drag || _resources is null || drag.Column < 0 || drag.Column >= Columns.Count)
        {
            CancelDrag();
            return;
        }

        CancelDrag();
        await MoveAsync(drag.Appointment, drag.Start, drag.Column).ConfigureAwait(true);
    }

    /// <summary>Verschiebt einen Termin an einen neuen Beginn in einer Spalte (Ziehen oder Alt + Pfeiltasten).</summary>
    private async Task MoveAsync(AppointmentDto before, DateTimeOffset start, int column)
    {
        if (_resources is null || column < 0 || column >= Columns.Count)
        {
            return;
        }

        var target = Columns[column];
        var employeeId = ByRoom ? before.EmployeeId : target.Id;
        var roomId = ByRoom ? target.Id : before.RoomId;
        if (start == before.Start && employeeId == before.EmployeeId && roomId == before.RoomId)
        {
            return;
        }

        var shift = start - before.Start;
        var moved = before with
        {
            Start = before.Start + shift,
            End = before.End + shift,
            BlockedFrom = before.BlockedFrom + shift,
            BlockedUntil = before.BlockedUntil + shift,
            EmployeeId = employeeId,
            RoomId = roomId,
        };
        Apply(moved);

        var result = await _api.PostOrQueueAsync($"api/appointments/{before.Id}/move",
            new MoveAppointmentRequest(start, ByRoom ? null : employeeId, ByRoom ? roomId : null, null, before.Version),
            $"{before.CustomerName}, {DayLayout.DateText(LocalDateOf(before.Start))}: verschieben auf {DescribeMoved(before, moved)}", moved).ConfigureAwait(true);
        if (result.Value is null)
        {
            Apply(before);
            if (result.Rejection is { Current: { } current } stale)
            {
                Apply(current);
                ShowProblem("Nicht verschoben", stale.Message);
                return;
            }

            var notice = ConflictNotice.From(result.Rejection, _zone, Date, ConflictLinksOf);
            ShowProblem("Nicht verschoben", notice?.Summary ?? result.Error,
                actionText: notice?.ActionText, action: notice?.ActionCommand is { } go ? () => go.Execute(null) : null);
            return;
        }

        if (result.Queued)
        {
            ShowPending();
            ShowToast("Ohne Verbindung vorgemerkt, Server prüft beim Abgleich");
            return;
        }

        Apply(result.Value);
        // Feste Therapeut:in: nicht stillschweigend zu jemand anderem; der Hinweis bleibt mit „Rückgängig“ stehen.
        var fixedTo = _day?.Guests?.FirstOrDefault(g => g.CustomerId == before.CustomerId)?.Booking is { EmployeeFixed: true, EmployeeId: { } id }
            && id != result.Value.EmployeeId && id == before.EmployeeId
            ? _resources?.Employees.FirstOrDefault(e => e.Id == id)?.InHouseName() ?? "eine andere Person"
            : null;
        var undo = new MoveUndo(before, After: result.Value.Version);
        ShowToast($"Verschoben auf {DescribeMoved(before, result.Value)}", undo);
        if (fixedTo is not null)
        {
            ShowProblem("Feste Therapeut:in", $"Die feste Therapeut:in des Gastes ist {fixedTo}; der Termin liegt jetzt bei einer anderen Person.",
                isError: false, actionText: "Rückgängig", action: () => _ = UndoAsync(undo));
        }
    }

    /// <summary>
    /// Verschiebt einen Termin auf einen anderen Tag oder eine andere Uhrzeit (aus dem Terminfenster). Person, Raum und Gerät bleiben,
    /// wenn sie dann frei sind, sonst wählt der Server passende. Danach zeigt der Kalender den neuen Tag mit dem Termin markiert.
    /// </summary>
    /// <returns>Die Begründung, wenn der Server ablehnt, sonst <c>null</c>.</returns>
    private async Task<string?> MoveToDayAsync(AppointmentDto before, DateTimeOffset start)
    {
        if (start == before.Start)
        {
            return null;
        }

        var shift = start - before.Start;
        var preview = before with
        {
            Start = start,
            End = before.End + shift,
            BlockedFrom = before.BlockedFrom + shift,
            BlockedUntil = before.BlockedUntil + shift,
        };
        var target = DateOnly.FromDateTime(DayLayout.Local(start, _zone));
        var result = await _api.PostOrQueueAsync($"api/appointments/{before.Id}/move", new MoveAppointmentRequest(start, null, null, null, before.Version),
            $"{before.CustomerName}, {DayLayout.DateText(Date)}: verschieben auf {DescribeDay(preview)}", preview).ConfigureAwait(true);
        if (result.Rejection is { Current: { } current } stale)
        {
            ShowStale(current, stale.Message);
            return null;
        }

        if (result.Value is not { } moved)
        {
            return ConflictNotice.From(result.Rejection, _zone, Date, ConflictLinksOf)?.Summary ?? result.Error;
        }

        Selected = null;
        Apply(moved);
        if (result.Queued)
        {
            ShowPending();
            ShowToast("Ohne Verbindung vorgemerkt, Server prüft beim Abgleich");
        }
        else
        {
            ShowToast($"Verschoben auf {DescribeDay(moved)}", new MoveUndo(before, After: moved.Version));
        }

        if (target != Date)
        {
            _markAfterLoad = moved.Id;
            Date = target;
        }

        return null;
    }

    /// <summary>Mit dem Tag, wenn der Termin auf einen anderen Tag gerückt ist (7-Tage-Ansicht).</summary>
    private string DescribeMoved(AppointmentDto before, AppointmentDto moved) =>
        LocalDateOf(before.Start) == LocalDateOf(moved.Start) ? Describe(moved) : DescribeDay(moved);

    private string DescribeDay(AppointmentDto appointment) =>
        $"{DayLayout.DateText(DateOnly.FromDateTime(DayLayout.Local(appointment.Start, _zone)))}, {Describe(appointment)}";

    /// <summary>Nimmt die letzte Verschiebung zurück (wieder über den Server, der erneut prüft).</summary>
    [RelayCommand]
    private async Task UndoMoveAsync()
    {
        if (_undo is { } undo)
        {
            await UndoAsync(undo).ConfigureAwait(true);
        }
    }

    private async Task UndoAsync(MoveUndo undo)
    {
        var before = undo.Before;
        var result = undo.Kind switch
        {
            UndoKind.Cancel => await _api.PostAsync<AppointmentDto>($"api/appointments/{before.Id}/restore",
                new RestoreAppointmentRequest(ExpectedVersion: undo.After)).ConfigureAwait(true),
            UndoKind.CheckIn => await _api.PostAsync<AppointmentDto>($"api/appointments/{before.Id}/status",
                new AppointmentStatusChange(AppointmentStatus.Booked, undo.After)).ConfigureAwait(true),
            _ => await _api.PostAsync<AppointmentDto>($"api/appointments/{before.Id}/move",
                new MoveAppointmentRequest(before.Start, before.EmployeeId, before.RoomId, before.EquipmentId, undo.After)).ConfigureAwait(true),
        };
        if (result.Value is null)
        {
            // Ä281: inzwischen anderswo geändert; ein neuer Versuch bekäme denselben Konflikt, deshalb ohne „Erneut versuchen“.
            if (result.Rejection is { Current: { } current } stale)
            {
                Apply(current);
                ShowProblem("Nicht zurückgenommen", stale.Message);
                return;
            }

            // Ist die Zeit der Stornierung inzwischen belegt, nennt die Meldung den Konflikt; das Team nimmt dann im Panel zurück und verschiebt dabei.
            ShowProblem("Nicht zurückgenommen", ConflictNotice.ToastText(result, _zone, Date), action: () => _ = UndoAsync(undo));
            return;
        }

        Apply(result.Value);
        Problem = null;
        ShowToast(undo.Kind switch
        {
            UndoKind.Cancel => $"Termin wieder gebucht: {Describe(result.Value)}",
            UndoKind.CheckIn => $"Einchecken zurückgenommen: {result.Value.CustomerName}",
            _ => $"Zurück auf {Describe(result.Value)}",
        });
    }

    /// <summary>
    /// Nach einer Stornierung oder dem Einchecken (Knopf im Panel, Taste, Rechtsklick) steht die Meldung mit „Rückgängig“; Strg+Z macht dasselbe. Was nur vorgemerkt ist (ohne Verbindung),
    /// lässt sich nicht zurücknehmen. Eine zurückgenommene Stornierung meldet sich ohne „Rückgängig“.
    /// </summary>
    private void OfferUndo(AppointmentDto before, AppointmentDto after)
    {
        if (_pending.Contains(after.Id))
        {
            return;
        }

        if (before.Status != AppointmentStatus.Cancelled && after.Status == AppointmentStatus.Cancelled)
        {
            ShowToast("Termin storniert", new MoveUndo(before, UndoKind.Cancel, after.Version));
        }
        else if (before.Status == AppointmentStatus.Booked && after.Status == AppointmentStatus.CheckedIn)
        {
            ShowToast($"{after.CustomerName} eingecheckt", new MoveUndo(before, UndoKind.CheckIn, after.Version));
        }
        else if (before.Status == AppointmentStatus.Cancelled && after.Status == AppointmentStatus.Booked)
        {
            ShowToast("Stornierung zurückgenommen");
        }
    }

    [RelayCommand]
    private void CloseToast() => ShowToast(null);

    /// <summary>Die Maus ist über der Rückmeldung (<paramref name="held"/>) bzw. wieder weg: solange sie darüber steht, läuft die Frist nicht.</summary>
    public void HoldToast(bool held)
    {
        _toastHeld = held;
        StartToastClock();
    }

    /// <summary>Startet die Frist neu; eine ältere, noch laufende Frist erkennt an der Nummer, dass sie überholt ist.</summary>
    private void StartToastClock()
    {
        var clock = ++_toastClock;
        if (Toast is not null && !_toastHeld)
        {
            _ = ExpireToastAsync(clock, CanUndo ? ToastUndoDuration : ToastDuration);
        }
    }

    private async Task ExpireToastAsync(int clock, TimeSpan duration)
    {
        await Task.Delay(duration, _time).ConfigureAwait(true);
        if (clock == _toastClock)
        {
            CloseToast();
        }
    }

    private void ShowToast(string? text, MoveUndo? undo = null)
    {
        Toast = text is null ? null : FitToast(text);
        _undo = undo;
        CanUndo = undo is not null;
        _toastHeld &= text is not null;
        StartToastClock();
    }

    private string Describe(AppointmentDto appointment)
    {
        var who = _resources?.Employees.FirstOrDefault(e => e.Id == appointment.EmployeeId)?.InHouseName();
        var room = _resources?.Rooms.FirstOrDefault(r => r.Id == appointment.RoomId)?.Name;
        var where = string.Join(", ", new[] { who, room }.Where(s => !string.IsNullOrEmpty(s)));
        return $"{DayLayout.TimeText(appointment.Start, appointment.End, _zone)}{(where.Length > 0 ? $" ({where})" : "")}";
    }

    private void UpdateGhost()
    {
        if (_previewDrag is { } preview)
        {
            Ghost = PreviewDragGhost(preview);
            return;
        }

        if (_edgeDrag is { } edge)
        {
            Ghost = EdgeGhost(edge);
            return;
        }

        if (_drag is not { } drag || drag.Column < 0)
        {
            // Ohne Ziehen bleibt nur die Vorschau des offenen Fensters stehen (neu gerechnet, der Maßstab kann sich geändert haben).
            _preview = BuildEntryPreview();
            Ghost = _drag is null ? _preview : null;
            return;
        }

        var length = drag.Appointment.End - drag.Appointment.Start;
        var end = drag.Start + length;
        var before = (drag.Appointment.Start - drag.Appointment.BlockedFrom).TotalMinutes;
        var after = (drag.Appointment.BlockedUntil - drag.Appointment.End).TotalMinutes;
        Ghost = new CalendarGhost(DayLayout.ColumnLeft(drag.Column, Scale), DayLayout.Top(DayStartOf(drag.Column), drag.Start, Scale),
            Math.Max(18, length.TotalMinutes * Scale.PixelsPerMinute),
            $"{DayLayout.TimeText(drag.Start, end, _zone)} · {Columns[drag.Column].Title}", IsMove: true,
            BeforeHeight: Math.Max(0, before) * Scale.PixelsPerMinute, AfterHeight: Math.Max(0, after) * Scale.PixelsPerMinute);
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Liste der Abos: Name, Zustände, Art mit Stand des Abrufs und Geltung.</summary>
public sealed record SubscriptionRow(Guid Id, string Title, string Badges, string Detail, string Places)
{
    public bool HasBadges => Badges.Length > 0;

    /// <summary>Alles in einer Zeile für Screenreader; sonst läse die Liste den Typnamen.</summary>
    public string Name => $"{Title}{(HasBadges ? $", {Badges}" : "")}, {Detail}, {Places}";
}

/// <summary>Eine Art in der Auswahl.</summary>
public sealed record KindChoice(SubscriptionKind Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Ein Vorschlag für das Feld „Region“: Ländercode oder Bundesland, Kanton mit Namen; die Eingabe bleibt frei.</summary>
public sealed record RegionSuggestion(string Code, string Name)
{
    public override string ToString() => Name.Length == 0 ? Code : $"{Code} · {Name}";
}

/// <summary>Ein Quellenvorschlag in der Auswahl; ohne Quelle heißt es „eigene Adresse“.</summary>
public sealed record SourceChoice(SubscriptionSource? Source, string Label)
{
    public static SourceChoice Own { get; } = new(null, "– eigene Adresse –");

    public override string ToString() => Label;
}

/// <summary>Ein Widerspruch zwischen Abo und Feiertagsregeln mit „Übernehmen“ und „Ignorieren“; dieselbe Wahl noch einmal nimmt die Entscheidung zurück.</summary>
public sealed partial class ConflictRowViewModel : ObservableObject
{
    public ConflictRowViewModel(SubscriptionConflictDto dto, Func<ConflictRowViewModel, SubscriptionDecision, Task> decide)
    {
        Dto = dto;
        Date = dto.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        Title = dto.Name;
        Detail = $"{(dto.Kind == SubscriptionConflictKind.OnlySubscription ? "Nur im Abo, nicht in den Feiertagsregeln" : "Nur in den Feiertagsregeln, nicht im Abo")}"
            + (dto.Locations.Count > 0 ? $" · {string.Join(", ", dto.Locations)}" : "");
        Decision = dto.Decision;
        AcceptCommand = new AsyncRelayCommand(() => decide(this, SubscriptionDecision.Accepted));
        IgnoreCommand = new AsyncRelayCommand(() => decide(this, SubscriptionDecision.Ignored));
    }

    public SubscriptionConflictDto Dto { get; }

    public string Date { get; }

    public string Title { get; }

    public string Detail { get; }

    public string GroupName => $"Entscheidung für {Date} {Title}";

    public string AcceptName => $"Übernehmen, {Date} {Title}";

    public string IgnoreName => $"Ignorieren, {Date} {Title}";

    public IAsyncRelayCommand AcceptCommand { get; }

    public IAsyncRelayCommand IgnoreCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccepted), nameof(IsIgnored))]
    public partial SubscriptionDecision? Decision { get; set; }

    /// <summary>Meldet den Stand erneut, wenn die Entscheidung nicht gespeichert wurde: das Kästchen springt auf den Stand des Modells zurück.</summary>
    public void Resync()
    {
        OnPropertyChanged(nameof(IsAccepted));
        OnPropertyChanged(nameof(IsIgnored));
    }

    public bool IsAccepted => Decision == SubscriptionDecision.Accepted;

    public bool IsIgnored => Decision == SubscriptionDecision.Ignored;
}

/// <summary>
/// Kalender-Abos (R05 Slice 95, ADR 0075): native Maske nach <c>web/src/subscriptions/SubscriptionsPage.tsx</c>. Links die Abos mit Art, Stand des Abrufs und Geltung, rechts das Formular
/// (Quelle aus Vorschlägen, Name, Adresse, Art, Standort, Region, Aktiv; <c>POST</c> bzw. <c>PUT api/calendar-subscriptions</c>, Strg+S). Beim bestehenden Abo kommen „Jetzt abrufen“ (der Server holt im
/// Hintergrund; die Maske fragt in kurzen Abständen nach, bis ein Ergebnis da ist), „Datei importieren“, „Im Kalender zeigen“ (nur für diesen Zugang) und bei Feiertags-Abos die Widersprüche zu den
/// Feiertagsregeln mit „Übernehmen“ und „Ignorieren“ dazu. Wer mit ungespeicherten Änderungen das Abo oder die Seite wechselt, wird gefragt (<see cref="UnsavedChanges"/>). Prüfen und Speichern macht
/// die API; vorab prüft die Maske die Regeln des Servers, damit der Fehler am Feld steht.
/// </summary>
public sealed partial class SubscriptionsViewModel : ViewModelBase, IEditableArea, ITargetedArea
{
    public const string NameField = "NameField";

    public const string UrlField = "UrlField";

    public const string RegionField = "RegionField";

    /// <summary>Größe der Import-Datei: höchstens 2 MB, wie am Server.</summary>
    public const long MaxImportBytes = 2_000_000;

    /// <summary>Abfragen nach „Jetzt abrufen“ oder dem Anlegen, bevor die Maske aufgibt.</summary>
    private const int WatchTries = 20;

    private const string SaveFailed = "Das Abo konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    private static readonly string[] ImportPatterns = ["*.ics", "*.csv"];

    private static readonly Regex RegionPattern = new("^[A-Za-z]{2}(-[A-Za-z0-9]{1,3})?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex SchemePattern = new("^(https|webcals?)://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex CredentialsPattern = new("^[a-z]+://[^/?#]*@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Dictionary<SubscriptionKind, string> KindEffects = new()
    {
        [SubscriptionKind.PublicHoliday] = "Die Tage erscheinen im Kalender, in der Feiertagsliste und beim Assistenten. Die Buchung ändern sie nicht: an einem Feiertag wird gebucht wie an jedem Tag.",
        [SubscriptionKind.SchoolHoliday] = "Die Ferien erscheinen im Kalender und beim Assistenten („Wann sind Ferien?“). Die Buchung ändern sie nicht.",
        [SubscriptionKind.Notice] = "Die Hinweise erscheinen im Kalender am Datum. Die Buchung ändern sie nicht.",
        [SubscriptionKind.Other] = "Die Einträge erscheinen im Kalender am Datum. Die Buchung ändern sie nicht.",
    };

    private readonly IWorkstationApi _api;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Dictionary<Guid, string> _locationNames = [];
    private List<CalendarSubscriptionDto> _all = [];
    private CalendarSubscriptionDto? _current;
    private AdminTarget? _pending;
    private Task? _loading;
    private int _watchGeneration;
    private string? _savedSnapshot;
    private bool _applying;
    private bool _reverting;
    private bool _switching;

    public SubscriptionsViewModel(IWorkstationApi api, TimeProvider time)
    {
        _api = api;
        _time = time;
        Kinds = [.. Enum.GetValues<SubscriptionKind>().Select(k => new KindChoice(k, SubscriptionTexts.Kind(k)))];
        SelectedKind = Kinds[0];
        SelectedSource = SourceChoice.Own;
    }

    public string Title => "Kalender-Abos";

    /// <summary>Abstand der Abfragen nach einem Abruf; Tests verkürzen ihn.</summary>
    public TimeSpan WatchInterval { get; init; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>Die laufende Beobachtung eines Abrufs (für Tests); leer, wenn keine läuft.</summary>
    public Task? Watching { get; private set; }

    /// <summary>Der Dateidialog; die Ansicht setzt den von Windows, Tests einen eigenen.</summary>
    public IFilePicker? Files { get; set; }

    public ObservableCollection<SubscriptionRow> Rows { get; } = [];

    public IReadOnlyList<KindChoice> Kinds { get; }

    public ObservableCollection<SourceChoice> Sources { get; } = [SourceChoice.Own];

    /// <summary>„Alle Standorte“ und die Standorte des Betriebs.</summary>
    public ObservableCollection<LocationChoice> Locations { get; } = [new LocationChoice(null, "Alle Standorte")];

    /// <summary>Vorschläge für „Region“: AT, DE, CH und die Regionen, für die die Software Feiertage kennt (wie die Vorschlagsliste im Web).</summary>
    public ObservableCollection<RegionSuggestion> RegionSuggestions { get; } = [];

    public ObservableCollection<ConflictRowViewModel> Conflicts { get; } = [];

    /// <summary>Das Abo wurde gespeichert, gelöscht oder importiert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    public bool HasSources => Sources.Count > 1;

    public bool HasConflicts => Conflicts.Count > 0;

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    public bool ShowSelectHint => IsLoaded && !HasForm;

    public bool HasLoadError => LoadError is not null;

    public bool HasError => Error is not null;

    public bool HasMessage => Message is not null;

    public bool HasLastError => LastErrorText is not null;

    public bool CanChooseKind => !IsPreset;

    /// <summary>Das Formular zeigt ein gespeichertes Abo (nicht ein neues): nur dann gibt es Stand, Abruf, Import und Anzeige im Kalender.</summary>
    public bool IsExisting => HasForm && !IsCreating;

    public bool CanDelete => HasForm && !IsCreating && !IsPreset;

    public bool HasUnsavedChanges => IsLoaded && HasForm && _savedSnapshot is not null && Snapshot() != _savedSnapshot;

    /// <summary>Was die gewählte Art bewirkt.</summary>
    public string KindEffect => SelectedKind is { } kind ? KindEffects[kind.Kind] : "";

    /// <summary>Hinweis unter der Quellenwahl: Anmerkung und Prüfdatum des Vorschlags, sonst was ein Vorschlag tut.</summary>
    public string SourceHint => SelectedSource?.Source is { } source
        ? $"{source.Note} Adresse geprüft am {source.CheckedOn.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}; der Betrieb bestätigt oder tauscht die Quelle."
        : "Ein Vorschlag füllt Adresse, Art und Region aus. Alles bleibt änderbar.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowSelectHint))]
    [NotifyCanExecuteChangedFor(nameof(NewCommand), nameof(SaveCommand))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    public partial SubscriptionRow? SelectedRow { get; set; }

    /// <summary>Ein neues Formular ist offen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete), nameof(IsExisting))]
    public partial bool IsCreating { get; private set; }

    /// <summary>Das Abo, dessen Formular offen ist; leer bei einem neuen.</summary>
    [ObservableProperty]
    public partial Guid? CurrentId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSelectHint), nameof(CanDelete), nameof(IsExisting))]
    public partial bool HasForm { get; private set; }

    [ObservableProperty]
    public partial string HeadingText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseKind), nameof(CanDelete))]
    public partial bool IsPreset { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Url { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindEffect))]
    public partial KindChoice? SelectedKind { get; set; }

    [ObservableProperty]
    public partial string Region { get; set; } = "";

    [ObservableProperty]
    public partial LocationChoice? SelectedLocation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceHint))]
    public partial SourceChoice? SelectedSource { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>Der Zugang sieht das Abo im Kalender; wirkt nur auf die Anzeige und sofort (<see cref="ToggleShowCommand"/>).</summary>
    [ObservableProperty]
    public partial bool ShowInCalendar { get; private set; } = true;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? UrlError { get; private set; }

    [ObservableProperty]
    public partial string? RegionError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(FetchNowCommand), nameof(ImportCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Eine Rückmeldung zu einer Aktion (Abruf, Import, Entscheidung, Löschen) oder ein Fehler dabei, über dem Formular.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Success;

    /// <summary>Das Feld, das den Fokus bekommen soll (Feldkennung); die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Stand und Umfang des gespeicherten Abos: „Stand heute · 136 Tage · 01.01.2027 bis 26.12.2028“.</summary>
    [ObservableProperty]
    public partial string StatusLine { get; private set; } = "";

    /// <summary>Der Fehler des letzten Abrufs; leer, wenn er gelang.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastError))]
    public partial string? LastErrorText { get; private set; }

    [ObservableProperty]
    public partial string ConflictsHint { get; private set; } = "";

    private TimeZoneInfo Zone => _time.LocalTimeZone;

    // ---------- Laden ----------

    /// <summary>Lädt die Abos, Quellenvorschläge und Standorte; beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var list = await _api.GetAsync<List<CalendarSubscriptionDto>>("api/calendar-subscriptions").ConfigureAwait(true);
        if (list.Value is not { } subscriptions)
        {
            LoadError = list.Error ?? "Die Kalender-Abos konnten nicht geladen werden.";
            return;
        }

        // Wie im Web: was fehlt (Vorschläge, Standorte), bleibt leer, der Rest der Maske steht trotzdem.
        var sources = await _api.GetAsync<List<SubscriptionSource>>("api/calendar-subscriptions/sources").ConfigureAwait(true);
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        var regions = await _api.GetAsync<List<HolidayRegionDto>>("api/holidays/regions").ConfigureAwait(true);
        RegionSuggestions.Clear();
        foreach (var country in new[] { "AT", "DE", "CH" })
        {
            RegionSuggestions.Add(new RegionSuggestion(country, ""));
        }

        foreach (var region in regions.Value ?? [])
        {
            RegionSuggestions.Add(new RegionSuggestion(region.Code, region.Name));
        }

        _locationNames.Clear();
        Locations.Clear();
        Locations.Add(new LocationChoice(null, "Alle Standorte"));
        foreach (var location in locations.Value ?? [])
        {
            _locationNames[location.Id] = location.Name;
            Locations.Add(new LocationChoice(location.Id, location.Name));
        }

        Sources.Clear();
        Sources.Add(SourceChoice.Own);
        foreach (var source in sources.Value ?? [])
        {
            Sources.Add(new SourceChoice(source, source.Label));
        }

        OnPropertyChanged(nameof(HasSources));
        SetList(subscriptions);
        IsLoaded = true;
        await SelectFirstOrNoneAsync().ConfigureAwait(true);
        await ApplyPendingAsync().ConfigureAwait(true);
    }

    /// <summary>Lädt nur, wenn noch nichts geladen ist (beim Öffnen des Bereichs); ein vorgemerktes Sprungziel gilt danach.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            await ApplyPendingAsync().ConfigureAwait(true);
            return;
        }

        // Die Seite und ein Sprung öffnen den Bereich fast gleichzeitig: ein zweiter Aufruf wartet auf das Laden des ersten.
        var load = _loading ??= LoadAsync();
        await load.ConfigureAwait(true);
        if (ReferenceEquals(_loading, load))
        {
            _loading = null;
        }
    }

    /// <summary>Sprung aus dem Assistenten: wählt das Abo (<see cref="AdminTarget.Id"/>); steht die Maske noch nicht, gilt das Ziel nach dem Laden.</summary>
    public void Open(AdminTarget target)
    {
        _pending = target;
        if (IsLoaded)
        {
            _ = ApplyPendingAsync();
        }
    }

    /// <summary>Der Sprung ohne Standort und Zeitraum (die Schnittstelle aller nativen Bereiche).</summary>
    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, null, null, null, field));

    private async Task ApplyPendingAsync()
    {
        if (_pending is not { } target || !IsLoaded)
        {
            return;
        }

        _pending = null;
        if (target.Id is { } id && id != CurrentId && Rows.FirstOrDefault(r => r.Id == id) is { } row && await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            await SelectRowAsync(row).ConfigureAwait(true);
        }
    }

    /// <summary>Holt die Liste neu und zeigt danach das Abo mit der Kennung (sonst das erste); die Felder des Formulars werden neu befüllt.</summary>
    private async Task ReloadAsync(Guid? keep = null)
    {
        var list = await _api.GetAsync<List<CalendarSubscriptionDto>>("api/calendar-subscriptions").ConfigureAwait(true);
        if (list.Value is not { } subscriptions)
        {
            LoadError = list.Error ?? "Die Kalender-Abos konnten nicht geladen werden.";
            return;
        }

        SetList(subscriptions);
        if (Rows.FirstOrDefault(r => r.Id == keep) is { } row)
        {
            await SelectRowAsync(row).ConfigureAwait(true);
        }
        else
        {
            await SelectFirstOrNoneAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Holt die Liste neu, ohne das Formular anzufassen: nur die Zeilen und die Angaben zum gespeicherten Abo ändern sich (nach Abruf, Import, Entscheidung).</summary>
    private async Task RefreshListAsync()
    {
        var list = await _api.GetAsync<List<CalendarSubscriptionDto>>("api/calendar-subscriptions").ConfigureAwait(true);
        if (list.Value is not { } subscriptions)
        {
            return;
        }

        SetList(subscriptions);
        if (CurrentId is { } id && _all.FirstOrDefault(s => s.Id == id) is { } current)
        {
            _current = current;
            ShowInfo(current);
            _switching = true;
            try
            {
                SelectedRow = Rows.FirstOrDefault(r => r.Id == id);
            }
            finally
            {
                _switching = false;
            }
        }

        FetchNowCommand.NotifyCanExecuteChanged();
    }

    private void SetList(List<CalendarSubscriptionDto> subscriptions)
    {
        _all = subscriptions;
        _switching = true;
        try
        {
            Rows.Clear();
            foreach (var subscription in subscriptions)
            {
                Rows.Add(NewRow(subscription));
            }
        }
        finally
        {
            _switching = false;
        }

        OnPropertyChanged(nameof(ShowEmpty));
    }

    private SubscriptionRow NewRow(CalendarSubscriptionDto dto)
    {
        var badges = new List<string>();
        if (!dto.IsActive)
        {
            badges.Add("Aus");
        }
        else if (dto.LastResult == SubscriptionFetchResult.Failed)
        {
            badges.Add("Fehler");
        }

        if (dto.OpenConflicts > 0)
        {
            badges.Add(dto.OpenConflicts == 1 ? "1 Widerspruch" : $"{dto.OpenConflicts} Widersprüche");
        }

        var where = dto.LocationId is { } id ? _locationNames.GetValueOrDefault(id, "Standort") : "Alle Standorte";
        return new SubscriptionRow(dto.Id, dto.Name, string.Join(" · ", badges), $"{SubscriptionTexts.Kind(dto.Kind)} · {SubscriptionTexts.Status(dto, _time.GetUtcNow(), Zone)}",
            string.IsNullOrEmpty(dto.Region) ? where : $"{where} · {dto.Region}");
    }

    // ---------- Anzeigen ----------

    private async Task SelectFirstOrNoneAsync()
    {
        if (Rows.FirstOrDefault() is { } first)
        {
            await SelectRowAsync(first).ConfigureAwait(true);
        }
        else
        {
            ClearForm();
        }
    }

    private async Task SelectRowAsync(SubscriptionRow row)
    {
        _switching = true;
        try
        {
            SelectedRow = row;
        }
        finally
        {
            _switching = false;
        }

        await ShowRowAsync(row).ConfigureAwait(true);
    }

    private async Task ShowRowAsync(SubscriptionRow row)
    {
        var dto = _all.First(s => s.Id == row.Id);
        IsCreating = false;
        CurrentId = dto.Id;
        HasForm = true;
        _current = dto;
        Message = null;
        ResetFeedback();
        Apply(() => Fill(dto));
        ShowInfo(dto);
        RaiseFormState();
        await LoadConflictsAsync(dto).ConfigureAwait(true);
    }

    private void Fill(CalendarSubscriptionDto dto)
    {
        Name = dto.Name;
        Url = dto.Url;
        SelectedKind = Kinds.First(k => k.Kind == dto.Kind);
        Region = dto.Region ?? "";
        SelectedLocation = Locations.FirstOrDefault(l => l.Id == dto.LocationId) ?? Locations[0];
        SelectedSource = SourceChoice.Own;
        IsActive = dto.IsActive;
    }

    private void FillBlank()
    {
        Name = "";
        Url = "";
        SelectedKind = Kinds[0];
        Region = "";
        // Mit nur einem Standort gilt das Abo für ihn.
        SelectedLocation = Locations.Count == 2 ? Locations[1] : Locations[0];
        SelectedSource = SourceChoice.Own;
        IsActive = true;
    }

    /// <summary>Überschrift, Stand und Fehler des gespeicherten Abos.</summary>
    private void ShowInfo(CalendarSubscriptionDto dto)
    {
        HeadingText = dto.Name;
        IsPreset = dto.IsPreset;
        ShowInCalendar = dto.ShowInCalendar;
        var status = SubscriptionTexts.Status(dto, _time.GetUtcNow(), Zone);
        StatusLine = dto is { FirstDate: { } first, LastDate: { } last }
            ? $"{status} · {first.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} bis {last.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}"
            : status;
        LastErrorText = dto is { LastResult: SubscriptionFetchResult.Failed, LastError: { Length: > 0 } error } ? error : null;
    }

    private void ClearForm()
    {
        _switching = true;
        try
        {
            SelectedRow = null;
        }
        finally
        {
            _switching = false;
        }

        IsCreating = false;
        CurrentId = null;
        HasForm = false;
        _current = null;
        _savedSnapshot = null;
        Conflicts.Clear();
        UpdateConflictsInfo();
        ResetFeedback();
        RaiseFormState();
    }

    private void StartNew()
    {
        _switching = true;
        try
        {
            SelectedRow = null;
        }
        finally
        {
            _switching = false;
        }

        IsCreating = true;
        CurrentId = null;
        HasForm = true;
        _current = null;
        Message = null;
        ResetFeedback();
        Apply(FillBlank);
        HeadingText = "Neues Abo";
        IsPreset = false;
        StatusLine = "";
        LastErrorText = null;
        Conflicts.Clear();
        UpdateConflictsInfo();
        RaiseFormState();
        FocusTarget = NameField;
    }

    private void ResetFeedback()
    {
        StatusText = null;
        Error = null;
        NameError = null;
        UrlError = null;
        RegionError = null;
    }

    private void RaiseFormState()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        FetchNowCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        ToggleShowCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Wendet <paramref name="fill"/> an, ohne dass es als Änderung der Person gilt, und merkt den Stand für „Verwerfen“ und die Rückfrage.</summary>
    private void Apply(Action fill)
    {
        _applying = true;
        try
        {
            fill();
        }
        finally
        {
            _applying = false;
        }

        _savedSnapshot = Snapshot();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string Snapshot() => string.Join('|', Name, Url, SelectedKind?.Kind, Region, SelectedLocation?.Id, IsActive);

    /// <summary>Eine Eingabe der Maske hat sich geändert: der Stand „Gespeichert.“ gilt nicht mehr, und „Verwerfen“ wird bedienbar.</summary>
    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        FetchNowCommand.NotifyCanExecuteChanged();
    }

    // ---------- Widersprüche ----------

    private async Task LoadConflictsAsync(CalendarSubscriptionDto dto)
    {
        Conflicts.Clear();
        UpdateConflictsInfo();
        if (dto.Kind != SubscriptionKind.PublicHoliday)
        {
            return;
        }

        var result = await _api.GetAsync<List<SubscriptionConflictDto>>($"api/calendar-subscriptions/{dto.Id}/conflicts").ConfigureAwait(true);
        if (CurrentId != dto.Id)
        {
            return;
        }

        foreach (var conflict in result.Value ?? [])
        {
            Conflicts.Add(new ConflictRowViewModel(conflict, DecideAsync));
        }

        UpdateConflictsInfo();
    }

    private void UpdateConflictsInfo()
    {
        var open = Conflicts.Count(c => c.Decision is null);
        ConflictsHint = open == 0
            ? "Alle Widersprüche zu den Feiertagsregeln sind entschieden."
            : $"{(open == 1 ? "1 Tag" : $"{open} Tage")} ohne Entscheidung. Nichts wird überschrieben: Tage nur im Abo gelten, bis sie ignoriert werden; die Feiertagsregeln bleiben für die Lohnverrechnung.";
        OnPropertyChanged(nameof(HasConflicts));
    }

    /// <summary>Dieselbe Entscheidung noch einmal nimmt sie zurück.</summary>
    private async Task DecideAsync(ConflictRowViewModel row, SubscriptionDecision decision)
    {
        if (CurrentId is not { } id)
        {
            return;
        }

        SubscriptionDecision? next = row.Decision == decision ? null : decision;
        var result = await _api.PutAsync<object>($"api/calendar-subscriptions/{id}/decisions", new SubscriptionDecisionInput(row.Dto.Date, next)).ConfigureAwait(true);
        if (!result.Ok)
        {
            Notify(result.Error ?? "Die Entscheidung wurde nicht gespeichert.", InfoBarSeverity.Error);
            row.Resync();
            return;
        }

        row.Decision = next;
        UpdateConflictsInfo();
        Notify(next switch { SubscriptionDecision.Accepted => "Tag übernommen", SubscriptionDecision.Ignored => "Tag ignoriert", _ => "Entscheidung zurückgenommen" });
        await RefreshListAsync().ConfigureAwait(true);
    }

    // ---------- Auswahl ----------

    partial void OnSelectedRowChanged(SubscriptionRow? oldValue, SubscriptionRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Abo, bis die Antwort da ist.
            Revert(() => SelectedRow = oldValue);
            _ = LeaveToRowAsync(newValue);
            return;
        }

        _ = ShowRowAsync(newValue);
    }

    private async Task LeaveToRowAsync(SubscriptionRow row)
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            await SelectRowAsync(row).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Zurück zur alten Auswahl. Kam der Wechsel aus der Liste, übernimmt sie einen Wert, den das Modell noch während ihrer eigenen Auswahl zurücksetzt, nicht; deshalb erst danach
    /// (gepostet), dann sieht sie eine echte Änderung.
    /// </summary>
    private void Revert(Action restore)
    {
        void Restore()
        {
            _reverting = true;
            try
            {
                restore();
            }
            finally
            {
                _reverting = false;
            }
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    private Task<bool> ConfirmLeaveAsync() => UnsavedChanges.ConfirmLeaveAsync(Dialogs, this);

    // ---------- Eingaben ----------

    partial void OnNameChanged(string value)
    {
        NameError = null;
        Touch();
    }

    partial void OnUrlChanged(string value)
    {
        UrlError = null;
        Touch();
    }

    partial void OnSelectedKindChanged(KindChoice? value) => Touch();

    partial void OnRegionChanged(string value)
    {
        RegionError = null;
        Touch();
    }

    partial void OnSelectedLocationChanged(LocationChoice? value) => Touch();

    partial void OnIsActiveChanged(bool value) => Touch();

    /// <summary>Ein Vorschlag übernimmt Adresse, Art und Region; der Name nur, wenn noch keiner steht. Danach lässt sich alles ändern.</summary>
    partial void OnSelectedSourceChanged(SourceChoice? value)
    {
        if (_applying || value?.Source is not { } source)
        {
            return;
        }

        Url = source.Url;
        SelectedKind = Kinds.First(k => k.Kind == source.Kind);
        Region = source.Region ?? "";
        if (Name.Trim().Length == 0)
        {
            Name = source.Label;
        }
    }

    // ---------- Befehle ----------

    private bool CanNew => IsLoaded;

    /// <summary>Ein neues Formular; die Wahl der Zeile fragt vorher, wenn etwas ungespeichert ist.</summary>
    [RelayCommand(CanExecute = nameof(CanNew))]
    private async Task NewAsync()
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            StartNew();
        }
    }

    private bool CanSave => !IsSaving && IsLoaded && HasForm;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !IsLoaded || !HasForm)
        {
            return false;
        }

        Error = null;
        if (Validate() is { } invalid)
        {
            Error = SaveFailed;
            FocusTarget = invalid;
            return false;
        }

        IsSaving = true;
        var body = new CalendarSubscriptionInput(Name.Trim(), Url.Trim(), SelectedKind!.Kind, Region.Trim().Length == 0 ? null : Region.Trim().ToUpperInvariant(), SelectedLocation?.Id, IsActive);
        var result = CurrentId is { } id
            ? await _api.PutAsync<CalendarSubscriptionDto>($"api/calendar-subscriptions/{id}", body).ConfigureAwait(true)
            : await _api.PostAsync<CalendarSubscriptionDto>("api/calendar-subscriptions", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? SaveFailed;
            return false;
        }

        await ReloadAsync(saved.Id).ConfigureAwait(true);
        StatusText = "Gespeichert.";
        Saved?.Invoke();
        if (saved.IsActive && saved.Url.Length > 0)
        {
            // Ein neues oder eingeschaltetes Abo mit Adresse wird im Hintergrund abgerufen.
            Watch(saved.Id, saved.LastFetchAt);
        }

        return true;
    }

    /// <summary>Die Regeln des Servers vorab, damit der Fehler am Feld steht; liefert die Kennung des ersten fehlerhaften Felds oder nichts.</summary>
    private string? Validate()
    {
        NameError = Name.Trim().Length is 0 or > 120 ? "Name mit höchstens 120 Zeichen angeben." : null;
        var url = Url.Trim();
        UrlError = url.Length == 0 ? null
            : !SchemePattern.IsMatch(url) ? "Die Adresse muss mit https:// oder webcal:// beginnen."
            : CredentialsPattern.IsMatch(url) ? "Die Adresse darf keinen Benutzernamen und kein Passwort enthalten."
            : url.Length > 500 ? "Höchstens 500 Zeichen."
            : null;
        var region = Region.Trim();
        RegionError = region.Length > 0 && !RegionPattern.IsMatch(region) ? "Region als Ländercode oder ISO-Code, z. B. „AT“ oder „CH-ZH“." : null;
        return NameError is not null ? NameField : UrlError is not null ? UrlField : RegionError is not null ? RegionField : null;
    }

    /// <summary>Setzt alle Felder auf den gespeicherten Stand zurück.</summary>
    [RelayCommand]
    public void Discard()
    {
        if (_current is { } dto)
        {
            Apply(() => Fill(dto));
        }
        else if (IsCreating)
        {
            Apply(FillBlank);
        }

        Error = null;
        NameError = null;
        UrlError = null;
        RegionError = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    private bool CanFetch => !IsSaving && !IsCreating && !HasUnsavedChanges && _current is { IsActive: true, Url.Length: > 0 };

    /// <summary>Der Server holt das Abo im Hintergrund; die Maske fragt danach in kurzen Abständen, bis ein Ergebnis da ist.</summary>
    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchNowAsync()
    {
        if (_current is not { } dto)
        {
            return;
        }

        var result = await _api.PostAsync<object>($"api/calendar-subscriptions/{dto.Id}/fetch", null).ConfigureAwait(true);
        if (!result.Ok)
        {
            Notify(result.Error ?? "Der Abruf wurde nicht gestartet.", InfoBarSeverity.Error);
            return;
        }

        Notify("Der Abruf läuft im Hintergrund.");
        Watch(dto.Id, dto.LastFetchAt);
    }

    private bool CanImport => !IsSaving && HasForm && !IsCreating;

    /// <summary>Wählt eine ICS- oder CSV-Datei (höchstens 2 MB) und schickt ihren Text an den Server.</summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (CurrentId is not { } id || Files is null)
        {
            return;
        }

        var file = await Files.PickTextAsync("Datei zum Import wählen", "Kalenderdatei (ICS oder CSV)", ImportPatterns, MaxImportBytes).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        if (file.Size > MaxImportBytes)
        {
            Notify("Die Datei ist größer als 2 MB.", InfoBarSeverity.Error);
            return;
        }

        IsSaving = true;
        var result = await _api.PostAsync<SubscriptionRefreshResult>($"api/calendar-subscriptions/{id}/import", new SubscriptionImportInput(file.Name, file.Content)).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } imported)
        {
            Notify(result.Error ?? "Die Datei wurde nicht importiert.", InfoBarSeverity.Error);
            return;
        }

        Notify(imported.Message, imported.Result == SubscriptionFetchResult.Failed ? InfoBarSeverity.Error : InfoBarSeverity.Success);
        await RefreshListAsync().ConfigureAwait(true);
        if (_current is { } dto)
        {
            await LoadConflictsAsync(dto).ConfigureAwait(true);
        }

        Saved?.Invoke();
    }

    /// <summary>„Im Kalender zeigen“: nur die Anzeige dieses Zugangs, wirkt sofort; bei einem Fehler bleibt der Stand, wie er war.</summary>
    [RelayCommand(CanExecute = nameof(CanToggleShow))]
    private async Task ToggleShowAsync()
    {
        if (CurrentId is not { } id)
        {
            return;
        }

        var result = await _api.PutAsync<CalendarSubscriptionDto>($"api/calendar-subscriptions/{id}/show", new SubscriptionShowInput(!ShowInCalendar)).ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            Notify(result.Error ?? "Die Anzeige im Kalender wurde nicht umgestellt.", InfoBarSeverity.Error);
            OnPropertyChanged(nameof(ShowInCalendar));
            return;
        }

        ShowInCalendar = changed.ShowInCalendar;
    }

    private bool CanToggleShow => HasForm && !IsCreating;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (CurrentId is not { } id || _current is not { } dto)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Abo löschen?",
            $"Das Abo „{dto.Name}“ und seine gespeicherte Kopie werden gelöscht. Die Feiertagsregeln des Programms bleiben. Ausschalten behält die Kopie.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _api.DeleteAsync<object>($"api/calendar-subscriptions/{id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            Notify(result.Error ?? "Das Abo konnte nicht gelöscht werden.", InfoBarSeverity.Error);
            return;
        }

        await ReloadAsync().ConfigureAwait(true);
        Notify("Gelöscht.");
        Saved?.Invoke();
    }

    private void Notify(string text, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        Message = text;
        MessageSeverity = severity;
    }

    // ---------- Beobachten ----------

    /// <summary>Fragt in kurzen Abständen nach dem Abo, bis der Abruf ein Ergebnis hat (neuer Stand oder Fehler), und meldet es.</summary>
    private void Watch(Guid id, DateTimeOffset? before)
    {
        // Eine neue Beobachtung löst die laufende ab.
        Watching = WatchAsync(id, before, ++_watchGeneration);
    }

    private async Task WatchAsync(Guid id, DateTimeOffset? before, int generation)
    {
        for (var attempt = 0; attempt < WatchTries; attempt++)
        {
            await Task.Delay(WatchInterval).ConfigureAwait(true);
            if (generation != _watchGeneration)
            {
                return;
            }

            await RefreshListAsync().ConfigureAwait(true);
            if (_all.FirstOrDefault(s => s.Id == id) is not { } sub)
            {
                return;
            }

            if (sub.LastResult == SubscriptionFetchResult.Failed)
            {
                Notify($"Abruf fehlgeschlagen: {sub.LastError ?? "unbekannter Fehler"}", InfoBarSeverity.Error);
                return;
            }

            if (sub.LastResult == SubscriptionFetchResult.Ok && sub.LastFetchAt != before)
            {
                Notify($"Abgerufen: {SubscriptionTexts.Status(sub, _time.GetUtcNow(), Zone)}");
                if (CurrentId == id)
                {
                    await LoadConflictsAsync(sub).ConfigureAwait(true);
                }

                return;
            }
        }
    }
}

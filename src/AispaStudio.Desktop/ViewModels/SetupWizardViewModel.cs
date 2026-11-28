using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Der Hauptknopf des Assistenten im aktuellen Zustand: genau einer ist Standardknopf (Enter).</summary>
public enum SetupWizardDefault
{
    None,
    Save,
    AddRoom,
    ApplyCatalog,
    AddPerson,
    OpenAdmin,
    OpenCalendar,
    Back,
    Next,
    Close,
}

/// <summary>
/// Assistent „Betrieb einrichten“ (ADR 0104, Entscheidungen 4 bis 6): führt durch die Schritte, die <c>GET api/setup/progress</c> liefert. Eigene Eingabe haben nur
/// Öffnungszeiten und Räume (vorhandene Aufrufe für Standort und Räume); die übrigen Schritte führen mit „In der Verwaltung öffnen“ in die Maske, in die auch die Karte springt.
/// Nach jedem Speichern wird der Stand neu geladen. Kein Schritt hält auf: „Überspringen“ geht immer.
/// </summary>
public sealed partial class SetupWizardViewModel : ViewModelBase
{
    internal const string ProgressPath = "api/setup/progress?includeRegister=false";

    public const string HoursKey = "hours";
    public const string RoomsKey = "rooms";
    public const string IncompleteHours = "Trage für jeden geöffneten Tag Beginn und Ende ein, z. B. 09:00 und 18:00.";
    public const string RoomNameMissing = "Der Name des Raums fehlt. Trage ihn ein, damit Team und Gäste den Raum erkennen.";
    public const string NoOpenDay = "Wähle mindestens einen Tag, an dem geöffnet ist.";
    public const string NotLoaded = "Der Stand der Einrichtung wurde nicht geladen.";

    /// <summary>Raumpflege nach jedem Termin, wie bei einem neuen Raum in „Standorte &amp; Räume“.</summary>
    private const int DefaultCleaningMinutes = 10;

    private readonly IWorkstationApi _api;
    private readonly ToastState _toast;
    private readonly TimeProvider _time;
    private IReadOnlyList<SetupStepDto> _steps = [];
    private LocationDto? _location;
    private bool _multipleLocations;
    private bool _teamForbidden;
    private string _savedHours = "";

    public SetupWizardViewModel(IWorkstationApi api, ToastState toast, TimeProvider? time = null)
    {
        _api = api;
        _toast = toast;
        _time = time ?? TimeProvider.System;
        Days = [.. OpeningDayViewModel.Weekdays.Select(w => new OpeningDayViewModel(w.Day, w.Label))];
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => RaiseStateChanged();
        }
        _savedHours = HoursSignature();
    }

    /// <summary>Der Assistent wird geschlossen (Schließen, Esc ohne Eingabe, „In der Verwaltung öffnen“).</summary>
    public event Action? CloseRequested;

    /// <summary>„In der Verwaltung öffnen“: die Verwaltung springt in die Maske des Schritts (<c>Page</c> und <c>Tab</c>).</summary>
    public event Action<SetupStepDto>? AdminRequested;

    public ObservableCollection<OpeningDayViewModel> Days { get; }

    /// <summary>Die Behandlungsräume des Standorts, in dem die Räume eingetragen werden.</summary>
    public ObservableCollection<string> Rooms { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Ein Aufruf läuft; solange sind Speichern, Hinzufügen und jede Navigation gesperrt und Esc schließt nicht.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackCommand), nameof(NextCommand), nameof(CloseCommand), nameof(OpenAdminCommand), nameof(SaveHoursCommand), nameof(AddRoomCommand), nameof(ApplyCatalogCommand), nameof(AddPersonCommand), nameof(OpenCalendarCommand), nameof(LoadReadinessCommand), nameof(GoToStepCommand))]
    public partial bool IsBusy { get; private set; }

    public bool IsLoading => !IsLoaded && LoadError is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    /// <summary>Meldung des Schritts, wenn ein Aufruf scheitert; wechselt mit dem Schritt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial string NewRoomName { get; set; } = "";

    [ObservableProperty]
    public partial int Index { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public SetupStepDto? Current => Index >= 0 && Index < _steps.Count ? _steps[Index] : null;

    public string StepText => $"Schritt {Index + 1} von {_steps.Count}";

    public bool HasStep => Current is not null;

    public bool IsDone => Current?.Done == true;

    public bool IsOpen => Current is { Done: false };

    public string? Missing => Current is { Done: false } step ? step.Missing : null;

    public string? Note => Current is { Done: true } step ? step.Note : null;

    public bool HasBack => Index > 0;

    public bool HasNext => Index < _steps.Count;

    public bool ShowNext => HasNext && IsDone;

    public bool ShowSkip => HasNext && IsOpen;

    public bool IsHoursStep => Current?.Key == HoursKey && _location is not null;

    public bool IsRoomsStep => Current?.Key == RoomsKey && _location is not null;

    public bool IsAdminStep => HasStep && !IsHoursStep && !IsRoomsStep && !IsTreatmentsStep && !IsPeopleStep;

    /// <summary>„In der Verwaltung öffnen“ steht bei jedem Schritt ohne Zeiten und Räume: bei Behandlungen und Team als Nebenweg.</summary>
    public bool ShowOpenAdmin => HasStep && !IsHoursStep && !IsRoomsStep;

    /// <summary>Bei mehreren Standorten nennt die Überschrift den, der gerade bearbeitet wird.</summary>
    public string LocationHeading => _location is not null && _multipleLocations && (IsHoursStep || IsRoomsStep || IsTreatmentsStep || IsPeopleStep) ? $"{Current?.Title} · {_location.Name}" : Current?.Title ?? "";

    public bool HasUnsavedHours => HoursSignature() != _savedHours;

    public bool HasUnsavedInput => !IsFinishStep && (HasUnsavedHours || (IsRoomsStep && NewRoomName.Trim().Length > 0) || (IsTreatmentsStep && HasPackageSelection) || (IsPeopleStep && HasPersonInput));

    public SetupWizardDefault DefaultButton
    {
        get
        {
            if (IsFinishStep)
            {
                return ReadinessLoaded && IsReady ? SetupWizardDefault.OpenCalendar : ReadinessLoaded && HasBack ? SetupWizardDefault.Back : SetupWizardDefault.Close;
            }

            // Das Zeitfeld übernimmt erst bei Enter; deshalb ist Enter im Schritt immer „Speichern“ (ein erneutes Speichern unveränderter Zeiten schadet nicht).
            if (IsHoursStep)
            {
                return SetupWizardDefault.Save;
            }

            if (IsRoomsStep && (NewRoomName.Trim().Length > 0 || IsOpen))
            {
                return SetupWizardDefault.AddRoom;
            }

            // Mit geladenen Paketen ist „Behandlungen anlegen“ immer der Standardknopf (ohne Auswahl gesperrt): Enter auf einem Häkchen verlässt den Assistenten nicht.
            if (IsTreatmentsStep)
            {
                return SetupWizardDefault.ApplyCatalog;
            }

            if (IsPeopleStep && (HasPersonInput || IsOpen))
            {
                return SetupWizardDefault.AddPerson;
            }

            if (ShowOpenAdmin && IsOpen)
            {
                return SetupWizardDefault.OpenAdmin;
            }

            return ShowNext ? SetupWizardDefault.Next : SetupWizardDefault.Close;
        }
    }

    public bool SaveIsDefault => DefaultButton == SetupWizardDefault.Save;

    public bool AddRoomIsDefault => DefaultButton == SetupWizardDefault.AddRoom;

    public bool ApplyCatalogIsDefault => DefaultButton == SetupWizardDefault.ApplyCatalog;

    public bool AddPersonIsDefault => DefaultButton == SetupWizardDefault.AddPerson;

    public bool OpenAdminIsDefault => DefaultButton == SetupWizardDefault.OpenAdmin;

    public bool OpenCalendarIsDefault => DefaultButton == SetupWizardDefault.OpenCalendar;

    public bool BackIsDefault => DefaultButton == SetupWizardDefault.Back;

    public bool NextIsDefault => DefaultButton == SetupWizardDefault.Next;

    public bool CloseIsDefault => DefaultButton == SetupWizardDefault.Close;

    /// <summary>Lädt Stand, Standort und Räume und springt zum ersten offenen Schritt; scheitert der Stand, bleibt die Meldung mit „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        IsBusy = true;
        var loaded = await RefreshAsync().ConfigureAwait(true);
        IsBusy = false;
        if (!loaded)
        {
            LoadError = NotLoaded;
            return;
        }

        var first = _steps.ToList().FindIndex(s => !s.Done);
        SetIndex(first < 0 ? _steps.Count : first);
        IsLoaded = true;
        if (IsFinishStep)
        {
            await LoadReadinessAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Holt den Stand der Schritte und den ersten offenen Standort mit seinen Räumen; der gewählte Schritt bleibt. Ungespeicherte Zeiten bleiben stehen.
    /// Alles wird erst nach den Aufrufen in einem Zug übernommen; <c>false</c>, wenn ein Aufruf nichts lieferte.
    /// </summary>
    private async Task<bool> RefreshAsync()
    {
        var progress = (await _api.GetAsync<SetupProgressDto>(ProgressPath).ConfigureAwait(true)).Value;
        var locations = (await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true)).Value;
        if (progress is null || locations is null)
        {
            return false;
        }

        var location = locations.FirstOrDefault(IsOpenLocation);
        IReadOnlyList<RoomDto> rooms = [];
        if (location is not null)
        {
            var resources = (await _api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources").ConfigureAwait(true)).Value;
            if (resources is null)
            {
                return false;
            }

            rooms = resources.Rooms;
        }

        var catalog = await GetListAsync<SetupCatalogPackageDto>("api/setup/catalog").ConfigureAwait(true);
        var employees = await GetListAsync<EmployeeDto>("api/employees").ConfigureAwait(true);
        var qualifications = await GetListAsync<QualificationDto>("api/qualifications").ConfigureAwait(true);
        if (catalog is null || employees is null || qualifications is null)
        {
            return false;
        }

        var key = Current?.Key;
        _steps = progress.Steps;
        _multipleLocations = locations.Count(IsOpenLocation) > 1;
        _location = location;
        _allLocations = locations;
        Rooms.Clear();
        foreach (var room in rooms.Where(r => !r.IsArea))
        {
            Rooms.Add(room.Name);
        }

        _teamForbidden = employees.Forbidden || qualifications.Forbidden;
        ApplyExtras(rooms, catalog.Items, employees.Items, qualifications.Items);
        if (location is not null && !HasUnsavedHours)
        {
            foreach (var day in Days)
            {
                day.Apply(location.OpeningHours);
            }

            _savedHours = HoursSignature();
            if (location.OpeningHours.Count == 0)
            {
                // Noch keine Zeiten (so legt „Spa einrichten“ den Standort an): Vorschlag Montag bis Samstag, gilt als ungespeichert.
                foreach (var day in Days)
                {
                    day.Apply(SuggestedHours);
                }
            }
        }

        if (key is not null)
        {
            var kept = _steps.ToList().FindIndex(s => s.Key == key);
            Index = kept < 0 ? Index : kept;
        }

        RaiseStateChanged();
        return true;
    }

    /// <summary>
    /// Übernimmt Paket-, Raum-, Personen- und Qualifikationsliste in einem Zug; Häkchen, die schon gesetzt waren, bleiben. Fehlt eine Antwort
    /// (etwa ohne das Recht dafür), bleibt die Liste leer und der Schritt führt nur in die Verwaltung.
    /// </summary>
    /// <summary>Eine der drei Listen neben Stand und Standort: ohne Recht (HTTP 403) gilt sie als leer, jeder andere Fehlschlag ist ein Ladefehler (<c>null</c>).</summary>
    private async Task<OptionalList<T>?> GetListAsync<T>(string path)
    {
        var result = await _api.GetAsync<List<T>>(path).ConfigureAwait(true);
        if (result.Value is not null)
        {
            return new OptionalList<T>(result.Value, false);
        }

        return result.Status == System.Net.HttpStatusCode.Forbidden ? new OptionalList<T>([], true) : null;
    }

    private sealed record OptionalList<T>(List<T> Items, bool Forbidden);

    private void ApplyExtras(IReadOnlyList<RoomDto> rooms, List<SetupCatalogPackageDto> catalog, List<EmployeeDto> employees, List<QualificationDto> qualifications)
    {
        var packageKeys = Packages.Where(p => p.IsSelected).Select(p => p.Key).ToHashSet();
        var deselected = RoomChoices.Where(r => !r.IsSelected).Select(r => r.Id).ToHashSet();
        var deselectedQualifications = QualificationChoices.Where(q => !q.IsSelected).Select(q => q.Id).ToHashSet();
        Packages.Clear();
        foreach (var package in catalog)
        {
            Packages.Add(Watch(new SetupPackageChoice(package, packageKeys.Contains(package.Key))));
        }

        RoomChoices.Clear();
        foreach (var room in rooms.Where(r => !r.IsArea))
        {
            RoomChoices.Add(Watch(new SetupRoomChoice(room.Id, room.Name, !deselected.Contains(room.Id))));
        }

        QualificationChoices.Clear();
        foreach (var qualification in qualifications.OrderBy(q => q.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            QualificationChoices.Add(Watch(new SetupQualificationChoice(qualification.Id, qualification.Name, !deselectedQualifications.Contains(qualification.Id))));
        }

        People.Clear();
        foreach (var employee in employees)
        {
            People.Add(employee.PreferredName ?? employee.DisplayName);
        }
    }

    private T Watch<T>(T choice)
        where T : System.ComponentModel.INotifyPropertyChanged
    {
        choice.PropertyChanged += (_, _) =>
        {
            RaiseStateChanged();
            ApplyCatalogCommand.NotifyCanExecuteChanged();
        };
        return choice;
    }

    private static IEnumerable<OpeningHours> SuggestedHours { get; } =
        [.. OpeningDayViewModel.Weekdays.Take(6).Select(w => new OpeningHours(w.Day, new TimeOnly(9, 0), new TimeOnly(18, 0)))];

    /// <summary>Offen wie in der API (<c>SetupFacts</c>): ohne Schließdatum oder mit einem Schließdatum nach heute in der Zeitzone des Standorts.</summary>
    private bool IsOpenLocation(LocationDto location)
    {
        if (location.ClosedFrom is not { } closedFrom)
        {
            return true;
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return closedFrom > DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).DateTime);
    }

    private void SetIndex(int index)
    {
        Index = Math.Clamp(index, 0, _steps.Count);
        Error = null;
        CatalogMessage = null;
        RaiseStateChanged();
    }

    private bool CanBack => HasBack && !IsBusy;

    private bool CanNext => HasNext && !IsBusy;

    private bool CanAct => !IsBusy;

    private bool CanAddRoom => !IsBusy && NewRoomName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back() => SetIndex(Index - 1);

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        SetIndex(Index + 1);
        if (IsFinishStep)
        {
            await LoadReadinessAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Schließen ist auch während der Abfrage der Bereitschaft möglich; deren Ergebnis wird danach verworfen.</summary>
    private bool CanClose => !IsBusy || _readinessLoading;

    private void RequestClose()
    {
        _closed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => RequestClose();

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void OpenAdmin()
    {
        if (Current is { } step)
        {
            AdminRequested?.Invoke(step);
        }
    }

    /// <summary>Übernimmt die Zeiten des ersten geöffneten Tages für alle geöffneten Tage.</summary>
    [RelayCommand]
    private void CopyFirstDay()
    {
        if (Days.FirstOrDefault(d => d.IsOpen) is not { } source)
        {
            return;
        }

        foreach (var day in Days.Where(d => d.IsOpen && d != source))
        {
            day.OpensText = source.OpensText;
            day.ClosesText = source.ClosesText;
        }
    }

    /// <summary>Speichert die Öffnungszeiten über <c>PUT api/locations/{id}</c>; Kontakt und Unternehmensangaben bleiben unverändert (weggelassen).</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task SaveHoursAsync()
    {
        if (IsBusy || _location is not { } location || !IsHoursStep)
        {
            return;
        }

        if (Days.Any(d => d.IsIncomplete))
        {
            Error = IncompleteHours;
            return;
        }

        if (!Days.Any(d => d.IsOpen))
        {
            Error = NoOpenDay;
            return;
        }

        Error = null;
        IsBusy = true;
        try
        {
            var input = new LocationInput(location.Name, location.TimeZone, location.Currency, [.. Days.SelectMany(d => d.Hours())], location.Region);
            var result = await _api.PutAsync<LocationDto>($"api/locations/{location.Id}", input).ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error ?? "Die Öffnungszeiten wurden nicht gespeichert.";
                return;
            }

            _savedHours = HoursSignature();
            RaiseStateChanged();
            _toast.Show("Gespeichert");
            if (!await RefreshAsync().ConfigureAwait(true))
            {
                Error = NotLoaded;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Legt einen Behandlungsraum an (<c>POST api/locations/{id}/rooms</c>) und lädt den Stand neu.</summary>
    [RelayCommand(CanExecute = nameof(CanAddRoom))]
    private async Task AddRoomAsync()
    {
        if (IsBusy || _location is not { } location || !IsRoomsStep)
        {
            return;
        }

        var name = NewRoomName.Trim();
        if (name.Length == 0)
        {
            Error = RoomNameMissing;
            return;
        }

        Error = null;
        IsBusy = true;
        try
        {
            var result = await _api.PostAsync<RoomDto>($"api/locations/{location.Id}/rooms", new RoomInput(name, DefaultCleaningMinutes, 1, false)).ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error ?? "Der Raum wurde nicht gespeichert.";
                return;
            }

            Rooms.Add(name);
            NewRoomName = "";
            _toast.Show("Raum hinzugefügt");
            if (!await RefreshAsync().ConfigureAwait(true))
            {
                Error = NotLoaded;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Esc schließt nur, wenn keine Eingabe verloren ginge; sonst bleibt der Assistent stehen.</summary>
    public bool TryCloseWithEscape()
    {
        if ((IsBusy && !_readinessLoading) || HasUnsavedInput)
        {
            return false;
        }

        RequestClose();
        return true;
    }

    partial void OnNewRoomNameChanged(string value) => RaiseStateChanged();

    private string HoursSignature() => string.Join(';', Days.Select(d => d.Signature));

    private void RaiseStateChanged()
    {
        foreach (var name in new[]
        {
            nameof(Current), nameof(StepText), nameof(HasStep), nameof(IsDone), nameof(IsOpen), nameof(Missing), nameof(Note), nameof(HasBack), nameof(HasNext),
            nameof(ShowNext), nameof(ShowSkip), nameof(IsHoursStep), nameof(IsRoomsStep), nameof(IsAdminStep), nameof(IsTreatmentsStep), nameof(IsPeopleStep),
            nameof(ShowOpenAdmin), nameof(ShowNoRoomHint), nameof(HasTreatmentRoom), nameof(HasPackageSelection), nameof(HasPersonInput), nameof(ApplyCatalogIsDefault),
            nameof(AddPersonIsDefault), nameof(HasQualifications), nameof(IsFinishStep), nameof(IsReady), nameof(IsNotReady), nameof(HasOpenSteps),
            nameof(OpenCalendarIsDefault), nameof(BackIsDefault), nameof(LocationHeading), nameof(HasUnsavedHours),
            nameof(HasUnsavedInput), nameof(DefaultButton), nameof(SaveIsDefault), nameof(AddRoomIsDefault), nameof(OpenAdminIsDefault), nameof(NextIsDefault),
            nameof(CloseIsDefault),
        })
        {
            OnPropertyChanged(name);
        }

        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        ApplyCatalogCommand.NotifyCanExecuteChanged();
        AddRoomCommand.NotifyCanExecuteChanged();
        AddPersonCommand.NotifyCanExecuteChanged();
    }
}

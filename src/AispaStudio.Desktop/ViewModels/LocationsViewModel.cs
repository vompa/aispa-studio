using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eintrag der Standortliste: ein Standort oder „Standort anlegen“ (ohne Id). Der Titel folgt dem gespeicherten Namen.</summary>
public sealed partial class LocationChoice(Guid? id, string title) : ObservableObject
{
    public Guid? Id { get; } = id;

    [ObservableProperty]
    public partial string Title { get; set; } = title;

    public override string ToString() => Title;
}

/// <summary>Bundesland oder Kanton in der Auswahl: ohne Kürzel heißt „nur landesweite Feiertage“.</summary>
public sealed record RegionOption(string? Code, string Label)
{
    public static RegionOption None { get; } = new(null, "– nur landesweite Feiertage –");

    public override string ToString() => Label;
}

/// <summary>
/// Standorte &amp; Räume (R05 Slice 95): native Maske nach <c>web/src/admin/LocationsTab.tsx</c>. Links die Standorte (und „Standort anlegen“, nur mit ManageOrganization),
/// rechts das Formular des gewählten Standorts mit Name, Zeitzone, Währung, Bundesland/Kanton, Anschrift und Kontakt, Anreise, eigener Gesellschaft und Öffnungszeiten
/// (<c>PUT api/locations/{id}</c>, Strg+S). Darunter stehen die Abschnitte wie im Web mit eigenem Speichern: Online-Buchung (<see cref="OnlineRules"/>), Räume
/// (<see cref="Rooms"/>, je Zeile), Tagesstart-Kontrolle (<see cref="Checklist"/>) und Geräte. Sie zählen nicht zu den ungespeicherten Änderungen der Maske. Prüfen und
/// Speichern macht die API; hier wird nur der Pflichtname vorab geprüft, damit der Fehler am Feld steht.
/// </summary>
public sealed partial class LocationsViewModel : ViewModelBase, IEditableArea, ITargetedArea
{
    /// <summary>Feldkennung des Namens, für den Fokus nach einem Fehler.</summary>
    public const string NameField = "name";

    /// <summary>Feldkennung der Öffnungszeiten aus „Jetzt ergänzen“ des Assistenten (<c>FieldMasks</c>); der Fokus liegt auf dem ersten Wochentag.</summary>
    public const string OpeningHoursField = "location.openingHours";

    public const string StreetField = "location.street";
    public const string PhoneField = "location.phone";
    public const string EmailField = "location.email";
    public const string DirectionsField = "location.directions";
    public const string RegionField = "location.region";
    public const string RoomsField = "location.rooms";
    public const string AreasField = "location.areas";
    public const string CancellationPolicyField = "location.cancellationPolicy";

    private static readonly (DayOfWeek Day, string Label)[] Weekdays =
    [
        (DayOfWeek.Monday, "Montag"), (DayOfWeek.Tuesday, "Dienstag"), (DayOfWeek.Wednesday, "Mittwoch"), (DayOfWeek.Thursday, "Donnerstag"),
        (DayOfWeek.Friday, "Freitag"), (DayOfWeek.Saturday, "Samstag"), (DayOfWeek.Sunday, "Sonntag"),
    ];

    private static readonly string[] Inputs =
    [
        nameof(Name), nameof(TimeZone), nameof(Currency), nameof(Region), nameof(Street), nameof(PostalCode), nameof(City), nameof(Phone), nameof(Email),
        nameof(Directions), nameof(LegalName), nameof(VatId), nameof(RegisterNumber), nameof(RegisterCourt),
    ];

    private static readonly Dictionary<FiscalCountry, string> CountryNames = new()
    {
        [FiscalCountry.AT] = "Österreich",
        [FiscalCountry.DE] = "Deutschland",
        [FiscalCountry.CH] = "Schweiz",
    };

    private readonly IWorkstationApi _api;
    private readonly CurrentUser _user;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly List<LocationDto> _locations = [];
    private readonly Dictionary<Guid, LocationResources> _resources = [];
    private readonly LocationChoice? _createChoice;
    private IReadOnlyList<ArticleOption> _articles = [ArticleOption.None];
    private string? _savedSnapshot;
    private AdminTarget? _pending;
    private Task? _loading;
    private bool _applying;
    private bool _reverting;
    private bool _switching;

    public LocationsViewModel(IWorkstationApi api, CurrentUser user)
    {
        _api = api;
        _user = user;
        CanCreate = user.Rights.Contains(AccessRight.ManageOrganization);
        _createChoice = CanCreate ? new LocationChoice(null, "Standort anlegen") : null;
        Days = [.. Weekdays.Select(w => new OpeningDayViewModel(w.Day, w.Label))];
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => FormChanged();
        }
    }

    public string Title => "Standorte & Räume";

    public static IReadOnlyList<string> Currencies { get; } = ["EUR", "CHF"];

    /// <summary>Wer Standorte anlegen darf (ManageOrganization); das Anpassen vorhandener braucht ManageLocation, das der Bereich ohnehin verlangt.</summary>
    public bool CanCreate { get; }

    /// <summary>Die Standorte und, wenn erlaubt, „Standort anlegen“ am Ende.</summary>
    public ObservableCollection<LocationChoice> Choices { get; } = [];

    /// <summary>Zeitzonen wie im Web; die des Standorts steht immer dabei.</summary>
    public ObservableCollection<string> TimeZones { get; } = ["Europe/Vienna", "Europe/Berlin", "Europe/Zurich"];

    public ObservableCollection<RegionOption> Regions { get; } = [RegionOption.None];

    public IReadOnlyList<OpeningDayViewModel> Days { get; }

    /// <summary>Die Räume des gewählten Standorts, eine Zeile je Raum; die neue Zeile steht in <see cref="NewRoom"/>.</summary>
    public ObservableCollection<RoomRowViewModel> Rooms { get; } = [];

    public ObservableCollection<EquipmentRowViewModel> Equipment { get; } = [];

    /// <summary>Die Standorte wurden gespeichert (auch Räume, Checkliste und Online-Regeln); die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew))]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    [NotifyPropertyChangedFor(nameof(HeadingText))]
    public partial LocationChoice? SelectedChoice { get; set; }

    /// <summary>„Standort anlegen“ ist gewählt.</summary>
    public bool IsNew => SelectedChoice is { Id: null };

    /// <summary>Ein gespeicherter Standort ist gewählt: nur dann gibt es die Abschnitte darunter.</summary>
    public bool HasLocation => SelectedChoice is { Id: not null };

    public string HeadingText => IsNew ? "Neuer Standort" : SelectedChoice?.Title ?? "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string TimeZone { get; set; } = "Europe/Vienna";

    [ObservableProperty]
    public partial string Currency { get; set; } = "EUR";

    [ObservableProperty]
    public partial RegionOption Region { get; set; } = RegionOption.None;

    [ObservableProperty]
    public partial string Street { get; set; } = "";

    [ObservableProperty]
    public partial string PostalCode { get; set; } = "";

    [ObservableProperty]
    public partial string City { get; set; } = "";

    [ObservableProperty]
    public partial string Phone { get; set; } = "";

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    /// <summary>Anreise und Parkplätze, höchstens 500 Zeichen.</summary>
    [ObservableProperty]
    public partial string Directions { get; set; } = "";

    [ObservableProperty]
    public partial string LegalName { get; set; } = "";

    [ObservableProperty]
    public partial string VatId { get; set; } = "";

    [ObservableProperty]
    public partial string RegisterNumber { get; set; } = "";

    [ObservableProperty]
    public partial string RegisterCourt { get; set; } = "";

    /// <summary>Fehler am Namensfeld (Pflichtfeld).</summary>
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    /// <summary>Fehler unter den Öffnungszeiten: ein geöffneter Tag ohne lesbaren Beginn oder lesbares Ende.</summary>
    [ObservableProperty]
    public partial string? HoursError { get; private set; }

    /// <summary>Fehler der API beim Speichern, über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Das Feld, das den Fokus bekommen soll (Feldkennung oder <c>room:{id}</c>); die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    public partial OnlineRulesViewModel? OnlineRules { get; private set; }

    [ObservableProperty]
    public partial RoomRowViewModel? NewRoom { get; private set; }

    [ObservableProperty]
    public partial RoomChecklistViewModel? Checklist { get; private set; }

    /// <summary>Fehler beim Speichern oder Deaktivieren eines Raums.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomMessage))]
    public partial string? RoomMessage { get; private set; }

    public bool HasRoomMessage => RoomMessage is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEquipmentCommand))]
    public partial string NewEquipmentName { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEquipmentMessage))]
    public partial string? EquipmentMessage { get; private set; }

    public bool HasEquipmentMessage => EquipmentMessage is not null;

    public bool HasUnsavedChanges => IsLoaded && _savedSnapshot is not null && Snapshot() != _savedSnapshot;

    /// <summary>Lädt Standorte, Räume und Geräte aller Standorte, Bundesländer und Artikel; beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is not { } list)
        {
            LoadError = locations.Error ?? "Die Standorte wurden nicht geladen.";
            return;
        }

        // Wie im Web: was fehlt (Bundesländer, Artikel, Räume eines Standorts), bleibt leer, der Rest der Maske steht trotzdem.
        var regions = await _api.GetAsync<List<HolidayRegionDto>>("api/holidays/regions").ConfigureAwait(true);
        var articles = await _api.GetAsync<List<ArticleDto>>("api/pos/articles").ConfigureAwait(true);
        var resources = await Task.WhenAll(list.Select(l => _api.GetAsync<LocationResources>($"api/locations/{l.Id}/resources"))).ConfigureAwait(true);

        _locations.Clear();
        _locations.AddRange(list);
        _resources.Clear();
        for (var i = 0; i < list.Count; i++)
        {
            _resources[list[i].Id] = resources[i].Value ?? Empty(list[i]);
        }

        _articles = [ArticleOption.None, .. (articles.Value ?? []).Select(ArticleOption.From)];
        Regions.Clear();
        Regions.Add(RegionOption.None);
        foreach (var region in regions.Value ?? [])
        {
            Regions.Add(new RegionOption(region.Code, $"{CountryNames.GetValueOrDefault(region.Country, region.Country.ToString())}: {region.Name}"));
        }

        Choices.Clear();
        foreach (var location in list)
        {
            Choices.Add(new LocationChoice(location.Id, location.Name));
        }

        if (_createChoice is not null)
        {
            Choices.Add(_createChoice);
        }

        IsLoaded = true;
        SelectWithoutAsking(Choices.FirstOrDefault());
        ApplyPending();
    }

    /// <summary>Lädt nur, wenn noch nichts geladen ist (beim Öffnen des Bereichs); ein vorgemerktes Sprungziel gilt danach.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            ApplyPending();
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

    /// <summary>Sprung aus Kalender („Raum bearbeiten“: <c>Id</c> ist der Raum) oder Assistent (<c>Location</c> und <c>Field</c>).</summary>
    public void Open(AdminTarget target)
    {
        _pending = target;
        if (IsLoaded)
        {
            ApplyPending();
        }
    }

    /// <summary>Der Sprung ohne Standort und Zeitraum (die Schnittstelle aller nativen Bereiche).</summary>
    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, null, null, null, field));

    private void ApplyPending()
    {
        if (_pending is not { } target || !IsLoaded)
        {
            return;
        }

        _pending = null;
        var locationId = target.Location;
        var focus = target.Field;
        if (target.Id is { } id)
        {
            if (_resources.FirstOrDefault(r => r.Value.Rooms.Any(room => room.Id == id)) is { Value: not null } byRoom)
            {
                locationId = byRoom.Key;
                focus = $"room:{id}";
            }
            else if (_locations.Any(l => l.Id == id))
            {
                locationId = id;
            }
        }

        if (locationId is { } wanted && Choices.FirstOrDefault(c => c.Id == wanted) is { } choice)
        {
            SelectedChoice = choice;
        }

        if (focus is not null)
        {
            FocusTarget = focus;
        }
    }

    private bool CanSave => !IsSaving && IsLoaded;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !IsLoaded || SelectedChoice is not { } choice)
        {
            return false;
        }

        if (Name.Trim().Length == 0)
        {
            NameError = "Der Name des Standorts fehlt. Trage ihn ein, damit Team und Gäste den Standort erkennen.";
            FocusTarget = NameField;
            return false;
        }

        if (Days.Any(d => d.IsIncomplete))
        {
            HoursError = "Trage für jeden geöffneten Tag Beginn und Ende ein, z. B. 09:00 und 18:00.";
            FocusTarget = OpeningHoursField;
            return false;
        }

        NameError = null;
        HoursError = null;
        Error = null;
        IsSaving = true;
        var input = Input() with { Name = Name.Trim() };
        var result = choice.Id is { } id
            ? await _api.PutAsync<LocationDto>($"api/locations/{id}", input).ConfigureAwait(true)
            : await _api.PostAsync<LocationDto>("api/locations", input).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Standort wurde nicht gespeichert.";
            return false;
        }

        if (choice.Id is null)
        {
            AddLocation(saved);
        }
        else
        {
            _locations[_locations.FindIndex(l => l.Id == saved.Id)] = saved;
            choice.Title = saved.Name;
            _resources[saved.Id] = _resources[saved.Id] with { Location = saved };
            Apply(saved);
            OnPropertyChanged(nameof(HeadingText));
        }

        StatusText = "Gespeichert.";
        Saved?.Invoke();
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (SelectedChoice is not { } choice)
        {
            return;
        }

        if (choice.Id is { } id && _locations.Find(l => l.Id == id) is { } location)
        {
            Apply(location);
        }
        else
        {
            ApplyNew();
        }

        NameError = null;
        HoursError = null;
        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    partial void OnSelectedChoiceChanged(LocationChoice? oldValue, LocationChoice? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        if (!_switching && oldValue is not null && HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Standort, bis die Antwort da ist.
            ReturnTo(oldValue);
            _ = LeaveAsync(newValue);
            return;
        }

        ApplyChoice(newValue);
    }

    private async Task LeaveAsync(LocationChoice target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            SelectWithoutAsking(target);
        }
    }

    private void SelectWithoutAsking(LocationChoice? choice)
    {
        _switching = true;
        try
        {
            if (SelectedChoice == choice && choice is not null)
            {
                ApplyChoice(choice);
            }
            else
            {
                SelectedChoice = choice;
            }
        }
        finally
        {
            _switching = false;
        }
    }

    /// <summary>
    /// Zurück zum Standort, der gefragt hat. Kam der Wechsel aus der Liste, übernimmt sie einen Wert, den das Modell noch während ihrer eigenen Auswahl zurücksetzt,
    /// nicht; deshalb erst danach (gepostet), dann sieht sie eine echte Änderung.
    /// </summary>
    private void ReturnTo(LocationChoice choice)
    {
        void Return()
        {
            _reverting = true;
            SelectedChoice = choice;
            _reverting = false;
        }

        if (_ui is null)
        {
            Return();
        }
        else
        {
            _ui.Post(_ => Return(), null);
        }
    }

    private void ApplyChoice(LocationChoice choice)
    {
        StatusText = null;
        NameError = null;
        HoursError = null;
        Error = null;
        RoomMessage = null;
        EquipmentMessage = null;
        NewEquipmentName = "";
        Rooms.Clear();
        Equipment.Clear();
        if (choice.Id is { } id && _locations.Find(l => l.Id == id) is { } location)
        {
            Apply(location);
            OnlineRules = new OnlineRulesViewModel(_api, id, _user.Rights.Contains(AccessRight.ManageLocation)) { Dialogs = Dialogs };
            OnlineRules.Saved += () => Saved?.Invoke();
            Checklist = new RoomChecklistViewModel(_api) { Dialogs = Dialogs };
            Checklist.Saved += () => Saved?.Invoke();
            FillSections(id);
            _ = OnlineRules.LoadAsync();
        }
        else
        {
            ApplyNew();
            OnlineRules = null;
            Checklist = null;
            NewRoom = null;
        }
    }

    /// <summary>Baut Räume, neue Zeile, Checkliste und Geräte aus dem Stand des Standorts; Zeilen mit eigenen Änderungen behalten ihre Eingabe.</summary>
    private void FillSections(Guid locationId)
    {
        var resources = _resources[locationId];
        var wanted = resources.Rooms.Select(r => r.Id).ToHashSet();
        for (var i = Rooms.Count - 1; i >= 0; i--)
        {
            if (Rooms[i].Id is not { } id || !wanted.Contains(id))
            {
                Rooms.RemoveAt(i);
            }
        }

        foreach (var room in resources.Rooms)
        {
            if (Rooms.FirstOrDefault(r => r.Id == room.Id) is { } row)
            {
                row.Rebase(room);
            }
            else
            {
                Rooms.Add(new RoomRowViewModel(room, _articles, SaveRoomAsync, DeactivateRoomAsync));
            }
        }

        NewRoom = new RoomRowViewModel(null, _articles, SaveRoomAsync, null);
        Checklist?.SetRooms(resources.Rooms);
        Equipment.Clear();
        foreach (var item in resources.Equipment)
        {
            Equipment.Add(new EquipmentRowViewModel(item, RemoveEquipmentAsync));
        }
    }

    private async Task SaveRoomAsync(RoomRowViewModel row)
    {
        if (SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        RoomMessage = null;
        var result = row.Id is { } id
            ? await _api.PutAsync<RoomDto>($"api/rooms/{id}", row.Input()).ConfigureAwait(true)
            : await _api.PostAsync<RoomDto>($"api/locations/{locationId}/rooms", row.Input()).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            RoomMessage = result.Error ?? "Der Raum wurde nicht gespeichert.";
            return;
        }

        var rooms = _resources[locationId].Rooms.ToList();
        var index = rooms.FindIndex(r => r.Id == saved.Id);
        if (index >= 0)
        {
            rooms[index] = saved;
        }
        else
        {
            rooms.Add(saved);
        }

        _resources[locationId] = _resources[locationId] with { Rooms = rooms };
        FillSections(locationId);
        Saved?.Invoke();
    }

    private async Task DeactivateRoomAsync(RoomRowViewModel row)
    {
        if (SelectedChoice?.Id is not { } locationId || row.Id is not { } id)
        {
            return;
        }

        RoomMessage = null;
        var result = await _api.DeleteAsync<object>($"api/rooms/{id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            RoomMessage = result.Error ?? "Der Raum wurde nicht deaktiviert.";
            return;
        }

        _resources[locationId] = _resources[locationId] with { Rooms = [.. _resources[locationId].Rooms.Where(r => r.Id != id)] };
        FillSections(locationId);
    }

    private bool CanAddEquipment => NewEquipmentName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAddEquipment))]
    private async Task AddEquipmentAsync()
    {
        if (SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        EquipmentMessage = null;
        var result = await _api.PostAsync<EquipmentDto>($"api/locations/{locationId}/equipment", new EquipmentInput(NewEquipmentName.Trim())).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            EquipmentMessage = result.Error ?? "Das Gerät wurde nicht gespeichert.";
            return;
        }

        _resources[locationId] = _resources[locationId] with { Equipment = [.. _resources[locationId].Equipment, saved] };
        NewEquipmentName = "";
        Equipment.Add(new EquipmentRowViewModel(saved, RemoveEquipmentAsync));
    }

    private async Task RemoveEquipmentAsync(EquipmentRowViewModel row)
    {
        if (SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        EquipmentMessage = null;
        var result = await _api.DeleteAsync<object>($"api/equipment/{row.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            EquipmentMessage = result.Error ?? "Das Gerät wurde nicht entfernt.";
            return;
        }

        _resources[locationId] = _resources[locationId] with { Equipment = [.. _resources[locationId].Equipment.Where(e => e.Id != row.Id)] };
        Equipment.Remove(row);
    }

    /// <summary>Ein neu angelegter Standort kommt vor „Standort anlegen“ in die Liste und ist gewählt; die Maske steht danach auf seinem gespeicherten Stand.</summary>
    private void AddLocation(LocationDto saved)
    {
        _locations.Add(saved);
        _resources[saved.Id] = Empty(saved);
        var choice = new LocationChoice(saved.Id, saved.Name);
        Choices.Insert(_createChoice is null ? Choices.Count : Choices.IndexOf(_createChoice), choice);
        SelectWithoutAsking(choice);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        if (name == nameof(Name))
        {
            NameError = null;
        }

        FormChanged();
    }

    private void FormChanged()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        HoursError = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Apply(LocationDto location)
    {
        _applying = true;
        if (!TimeZones.Contains(location.TimeZone))
        {
            TimeZones.Add(location.TimeZone);
        }

        if (location.Region is { } code && Regions.All(r => r.Code != code))
        {
            Regions.Add(new RegionOption(code, code));
        }

        var contact = location.Contact ?? LocationContact.Empty;
        var company = location.Company ?? LocationCompany.Empty;
        Name = location.Name;
        TimeZone = location.TimeZone;
        Currency = location.Currency;
        Region = Regions.FirstOrDefault(r => r.Code == location.Region) ?? RegionOption.None;
        Street = contact.Street ?? "";
        PostalCode = contact.PostalCode ?? "";
        City = contact.City ?? "";
        Phone = contact.Phone ?? "";
        Email = contact.Email ?? "";
        Directions = contact.Directions ?? "";
        LegalName = company.LegalName ?? "";
        VatId = company.VatId ?? "";
        RegisterNumber = company.CompanyRegisterNumber ?? "";
        RegisterCourt = company.CompanyRegisterCourt ?? "";
        foreach (var day in Days)
        {
            day.Apply(location.OpeningHours);
        }

        _savedSnapshot = Snapshot();
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Ein neuer Standort startet wie im Web: Wien, Euro, Montag bis Samstag 09:00 bis 20:00.</summary>
    private void ApplyNew() => Apply(new LocationDto(Guid.Empty, "", "Europe/Vienna", "EUR",
        [.. Weekdays.Take(6).Select(w => new OpeningHours(w.Day, new TimeOnly(9, 0), new TimeOnly(20, 0)))], null, LocationContact.Empty, LocationCompany.Empty));

    /// <summary>Was gespeichert würde; leere Felder heißen „keine Angabe“ wie im Web (<c>e.target.value || null</c>).</summary>
    private LocationInput Input() => new(Name, TimeZone, Currency, [.. Days.SelectMany(d => d.Hours())], Region.Code,
        new LocationContact(Value(Street), Value(PostalCode), Value(City), Value(Phone), Value(Email), Value(Directions)),
        new LocationCompany(Value(LegalName), Value(VatId), Value(RegisterNumber), Value(RegisterCourt)));

    private string Snapshot() => JsonSerializer.Serialize(Input(), ApiJson.Options) + "|" + string.Join(';', Days.Select(d => d.Signature));

    private static LocationResources Empty(LocationDto location) => new(location, [], [], [], [], []);

    private static string? Value(string text) => text.Length == 0 ? null : text;
}

/// <summary>Ein Gerät des Standorts in der Liste; „Entfernen“ löscht es.</summary>
public sealed partial class EquipmentRowViewModel(EquipmentDto equipment, Func<EquipmentRowViewModel, Task> remove) : ObservableObject
{
    public Guid Id { get; } = equipment.Id;

    public string Name { get; } = equipment.Name;

    public string RemoveName => $"Gerät {Name} entfernen";

    [RelayCommand]
    private Task RemoveAsync() => remove(this);
}

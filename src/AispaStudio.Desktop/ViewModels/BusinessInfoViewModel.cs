using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Kategorie der Betriebsinformationen.</summary>
public sealed record BusinessCategoryChoice(BusinessContactCategory? Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ein Eintrag der Liste; <c>GroupHeader</c> steht über dem ersten Eintrag einer Kategorie.</summary>
public sealed record BusinessContactRow(Guid Id, string Designation, string Detail, string? GroupHeader, bool IsArchived, bool IsManagementOnly)
{
    public bool HasGroupHeader => GroupHeader is not null;

    public bool HasDetail => Detail.Length > 0;

    public string SpokenName => string.Join(", ", new[] { Designation, Detail, IsArchived ? "Archiviert" : "", IsManagementOnly ? "Nur Leitung" : "" }.Where(p => p.Length > 0));

    public override string ToString() => Designation;
}

/// <summary>Ein Weg zum Kontakt: anrufen oder schreiben per Link, und kopieren.</summary>
public sealed class BusinessReachEntry
{
    public BusinessReachEntry(string label, string value, Uri? link, Func<Uri, Task> launch, Func<string, string, Task> copy)
    {
        Label = label;
        Value = value;
        LaunchCommand = new AsyncRelayCommand(() => link is null ? Task.CompletedTask : launch(link));
        CopyCommand = new AsyncRelayCommand(() => copy(value, label));
    }

    public string Label { get; }

    public string Value { get; }

    public string LaunchName => $"{Label} {Value}";

    public string CopyName => $"{Label} {Value} kopieren";

    public IAsyncRelayCommand LaunchCommand { get; }

    public IAsyncRelayCommand CopyCommand { get; }
}

/// <summary>Eine Angabe beim Nachschlagen.</summary>
public sealed record BusinessDetailLine(string Label, string Value);

/// <summary>Ein Standort zum Ankreuzen.</summary>
public sealed partial class BusinessLocationChoice(Guid id, string name) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Betriebsinformationen (R05 Slice 102, ADR 0074), native Maske nach <c>BusinessInfoPage.tsx</c>: links die Liste nach Kategorie mit Suche, rechts der Eintrag, für alle zum Nachschlagen (anrufen,
/// schreiben, kopieren), wer ihn ändern darf (<c>CanEdit</c> der API) sieht das Formular. Die Maske prüft vorab dieselben Regeln wie der Server, damit der Fehler am Feld steht.
/// Einträge der Stufe „Leitung“ schickt der Server nur an die Leitung.
/// </summary>
public sealed partial class BusinessInfoViewModel(IWorkstationApi api, CurrentUser user) : ViewModelBase, INativeArea, IFocusTarget
{
    public const string NewEntryField = "NewBusinessContactButton";

    public const string DesignationField = "BusinessDesignationField";

    public const string PhoneField = "BusinessPhoneField";

    public const string MobileField = "BusinessMobileField";

    public const string EmailField = "BusinessEmailField";

    public const string HotlineField = "BusinessHotlineField";

    public const string CompanyField = "BusinessCompanyField";

    public const string SomeLocationsField = "BusinessSomeLocationsOption";

    private const string PhoneRule = "Keine gültige Telefonnummer. Erlaubt sind Ziffern, Leerzeichen und + ( ) / . -";

    private static readonly IReadOnlyList<BusinessCategoryChoice> Categories =
    [
        new(BusinessContactCategory.Insurance, "Versicherung"),
        new(BusinessContactCategory.Craftsman, "Handwerker und Technik"),
        new(BusinessContactCategory.Supplier, "Lieferant"),
        new(BusinessContactCategory.Authority, "Behörde"),
        new(BusinessContactCategory.Accounting, "Buchhaltung"),
        new(BusinessContactCategory.Payroll, "Lohnverrechnung"),
        new(BusinessContactCategory.TaxAdvisor, "Steuerberatung"),
        new(BusinessContactCategory.TrustedPerson, "Vertrauensperson"),
        new(BusinessContactCategory.Emergency, "Notfall"),
        new(BusinessContactCategory.Other, "Sonstiges"),
    ];

    private static readonly string[] Inputs =
    [
        nameof(Category), nameof(Designation), nameof(Company), nameof(ContactPerson), nameof(Responsibility), nameof(Phone), nameof(Mobile), nameof(Email), nameof(Address), nameof(Insurer),
        nameof(PolicyNumber), nameof(ClaimsHotline), nameof(Coverage), nameof(IsTeamVisible), nameof(AllLocations), nameof(CustomerNumber), nameof(Notes),
    ];

    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private List<BusinessContactDto> _all = [];
    private List<LocationDto> _places = [];
    private BusinessContactDto? _current;
    private string? _savedSignature;
    private string? _pendingField;
    private Guid? _pendingOpen;
    private Task? _loading;
    private bool _applying;
    private bool _switching;
    private bool _reverting;

    public string Title => "Betriebsinfo";

    /// <summary>Öffnet Links und kopiert über das Fenster; die Ansicht setzt es.</summary>
    public IContactActions? ContactActions { get; set; }

    public bool CanManage => user.Rights.Contains(AccessRight.ManageLocation);

    public IReadOnlyList<BusinessCategoryChoice> CategoryFilters { get; } = [new(null, "Alle Kategorien"), .. Categories];

    public IReadOnlyList<BusinessCategoryChoice> CategoryChoices { get; } = Categories;

    public ObservableCollection<BusinessContactRow> Rows { get; } = [];

    public ObservableCollection<BusinessReachEntry> Reach { get; } = [];

    public ObservableCollection<BusinessDetailLine> Details { get; } = [];

    public ObservableCollection<BusinessLocationChoice> Locations { get; } = [];

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial BusinessCategoryChoice? CategoryFilter { get; set; }

    [ObservableProperty]
    public partial bool ShowArchived { get; set; }

    [ObservableProperty]
    public partial BusinessContactRow? SelectedRow { get; set; }

    public string EmptyText => _all.Count == 0 ? CanManage ? "Noch nichts eingetragen. Mit „Neuer Eintrag“ beginnen." : "Noch nichts eingetragen." : "Nichts gefunden.";

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    // ---------- Was rechts steht ----------

    /// <summary>Das Formular für einen neuen Eintrag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForm), nameof(ShowLookup), nameof(ShowPlaceholder), nameof(HeadingText), nameof(CanMenu))]
    public partial bool IsCreating { get; private set; }

    public bool ShowForm => IsLoaded && (IsCreating || _current is { CanEdit: true });

    public bool ShowLookup => IsLoaded && !IsCreating && _current is { CanEdit: false };

    public bool ShowPlaceholder => IsLoaded && !IsCreating && _current is null;

    public string PlaceholderText => _all.Count == 0 ? "Hier stehen Versicherung, Handwerker, Buchhaltung und andere Kontakte des Betriebs." : "Links einen Eintrag wählen.";

    public string HeadingText => IsCreating ? "Neuer Eintrag" : _current?.Designation ?? "";

    public bool IsArchived => _current?.IsArchived == true;

    public bool IsEditable => !IsArchived;

    public bool IsManagementOnly => _current?.Visibility == BusinessContactVisibility.Management;

    public bool CanMenu => !IsCreating && _current is { CanEdit: true };

    public bool CanArchive => CanMenu && !IsArchived;

    public bool CanUnarchive => CanMenu && IsArchived;

    public bool HasReach => Reach.Count > 0;

    // ---------- Formular ----------

    [ObservableProperty]
    public partial BusinessCategoryChoice? Category { get; set; }

    public bool IsInsurance => Category?.Value == BusinessContactCategory.Insurance;

    [ObservableProperty]
    public partial string Designation { get; set; } = "";

    [ObservableProperty]
    public partial string Company { get; set; } = "";

    [ObservableProperty]
    public partial string ContactPerson { get; set; } = "";

    [ObservableProperty]
    public partial string Responsibility { get; set; } = "";

    [ObservableProperty]
    public partial string Phone { get; set; } = "";

    [ObservableProperty]
    public partial string Mobile { get; set; } = "";

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    [ObservableProperty]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    public partial string Insurer { get; set; } = "";

    [ObservableProperty]
    public partial string PolicyNumber { get; set; } = "";

    [ObservableProperty]
    public partial string ClaimsHotline { get; set; } = "";

    [ObservableProperty]
    public partial string Coverage { get; set; } = "";

    [ObservableProperty]
    public partial string CustomerNumber { get; set; } = "";

    [ObservableProperty]
    public partial string Notes { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibilityHint))]
    public partial bool IsTeamVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsManagementVisible { get; set; }

    public string VisibilityHint => IsTeamVisible ? "Alle Zugänge lesen den Eintrag." : "Nur Inhaber, Manager, Standortleitung und Backoffice lesen den Eintrag.";

    [ObservableProperty]
    public partial bool AllLocations { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLocationList))]
    public partial bool SomeLocations { get; set; }

    public bool ShowLocationList => SomeLocations;

    [ObservableProperty]
    public partial string? DesignationError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReachError))]
    public partial string? ReachError { get; private set; }

    public bool HasReachError => ReachError is not null;

    [ObservableProperty]
    public partial string? PhoneError { get; private set; }

    [ObservableProperty]
    public partial string? MobileError { get; private set; }

    [ObservableProperty]
    public partial string? EmailError { get; private set; }

    [ObservableProperty]
    public partial string? HotlineError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocationsError))]
    public partial string? LocationsError { get; private set; }

    public bool HasLocationsError => LocationsError is not null;

    /// <summary>Das Feld, das nach einem Fehler oder Sprung den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool HasUnsavedChanges => _savedSignature is not null && ShowForm && Signature() != _savedSignature;

    // ---------- Laden ----------

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : _loading ??= LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            LoadError = null;
            var contacts = api.GetAsync<List<BusinessContactDto>>(ListPath);
            var places = api.GetAsync<List<LocationDto>>("api/locations");
            await Task.WhenAll(contacts, places).ConfigureAwait(true);
            if (contacts.Result.Value is not { } list || places.Result.Value is not { } locations)
            {
                LoadError = contacts.Result.Error ?? places.Result.Error ?? "Die Betriebsinformationen konnten nicht geladen werden.";
                return;
            }

            _all = list;
            _places = locations;
            _switching = true;
            CategoryFilter = CategoryFilters[0];
            _switching = false;
            IsLoaded = true;
            Rebuild();
            var wanted = _pendingOpen;
            _pendingOpen = null;
            Select(wanted ?? Rows.FirstOrDefault()?.Id);
            ApplyPendingField();
        }
        finally
        {
            _loading = null;
        }
    }

    private string ListPath => $"api/business-contacts?includeArchived={(CanManage && ShowArchived ? "true" : "false")}";

    private async Task<bool> ReloadAsync(Guid? select)
    {
        var result = await api.GetAsync<List<BusinessContactDto>>(ListPath).ConfigureAwait(true);
        if (result.Value is not { } list)
        {
            Error = result.Error ?? "Die Betriebsinformationen wurden nicht geladen.";
            return false;
        }

        _all = list;
        Rebuild();
        Select(select ?? Rows.FirstOrDefault()?.Id);
        return true;
    }

    public void Open(Guid? id, string? section, string? field)
    {
        if (!IsLoaded)
        {
            _pendingOpen = id;
            _pendingField = field;
            return;
        }

        if (id is { } wanted)
        {
            Select(wanted);
        }

        _pendingField = field;
        ApplyPendingField();
    }

    private void ApplyPendingField()
    {
        if (_pendingField == "businessInfo.contacts")
        {
            FocusTarget = NewEntryField;
        }

        _pendingField = null;
    }

    // ---------- Liste ----------

    private static string CategoryName(BusinessContactCategory category) => Categories.First(c => c.Value == category).Name;

    private bool Matches(BusinessContactDto contact)
    {
        var query = Query.Trim();
        return query.Length == 0 || new[] { contact.Designation, contact.Company, contact.ContactPerson, contact.Responsibility, contact.Insurer, CategoryName(contact.Category) }
            .Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void Rebuild()
    {
        _switching = true;
        var keep = SelectedRow?.Id;
        Rows.Clear();
        foreach (var category in Categories)
        {
            var group = _all.Where(c => c.Category == category.Value && (CategoryFilter?.Value is null || CategoryFilter.Value == c.Category) && Matches(c)).ToList();
            for (var i = 0; i < group.Count; i++)
            {
                var contact = group[i];
                Rows.Add(new BusinessContactRow(contact.Id, contact.Designation, string.Join(" · ", new[] { contact.Company, contact.ContactPerson }.Where(p => !string.IsNullOrEmpty(p))),
                    i == 0 ? $"{category.Name} ({group.Count})" : null, contact.IsArchived, contact.Visibility == BusinessContactVisibility.Management));
            }
        }

        SelectedRow = Rows.FirstOrDefault(r => r.Id == keep);
        _switching = false;
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(PlaceholderText));
    }

    partial void OnQueryChanged(string value)
    {
        if (IsLoaded)
        {
            Rebuild();
        }
    }

    partial void OnCategoryFilterChanged(BusinessCategoryChoice? value)
    {
        if (IsLoaded && !_switching)
        {
            Rebuild();
        }
    }

    partial void OnShowArchivedChanged(bool value)
    {
        if (!IsLoaded || _reverting)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann neu laden: Das Häkchen bleibt, wie es war, bis die Antwort da ist.
            RevertShowArchived(!value);
            _ = LeaveToShowArchivedAsync(value);
            return;
        }

        _ = ReloadAsync(_current?.Id);
    }

    private void RevertShowArchived(bool old)
    {
        void Restore()
        {
            _reverting = true;
            ShowArchived = old;
            _reverting = false;
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

    private async Task LeaveToShowArchivedAsync(bool target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            ShowArchived = target;
        }
    }

    partial void OnSelectedRowChanged(BusinessContactRow? oldValue, BusinessContactRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Eintrag, bis die Antwort da ist.
            Revert(oldValue);
            _ = LeaveToRowAsync(newValue.Id);
            return;
        }

        Select(newValue.Id);
    }

    private async Task LeaveToRowAsync(Guid id)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            Select(id);
        }
    }

    private void Revert(BusinessContactRow? old)
    {
        void Restore()
        {
            _reverting = true;
            SelectedRow = old;
            _reverting = false;
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

    private void Select(Guid? id)
    {
        _switching = true;
        SelectedRow = id is { } wanted ? Rows.FirstOrDefault(r => r.Id == wanted) : null;
        _switching = false;
        IsCreating = false;
        Apply(id is { } known ? _all.FirstOrDefault(c => c.Id == known) : null);
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            return;
        }

        _switching = true;
        SelectedRow = null;
        _switching = false;
        Message = null;
        IsCreating = true;
        Apply(null);
    }

    // ---------- Formular füllen ----------

    private void Apply(BusinessContactDto? contact)
    {
        _applying = true;
        _current = contact;
        var single = _places.Count == 1;
        Category = Categories.First(c => c.Value == (contact?.Category ?? BusinessContactCategory.Craftsman));
        Designation = contact?.Designation ?? "";
        Company = contact?.Company ?? "";
        ContactPerson = contact?.ContactPerson ?? "";
        Responsibility = contact?.Responsibility ?? "";
        Phone = contact?.Phone ?? "";
        Mobile = contact?.Mobile ?? "";
        Email = contact?.Email ?? "";
        Address = contact?.Address ?? "";
        Insurer = contact?.Insurer ?? "";
        PolicyNumber = contact?.PolicyNumber ?? "";
        ClaimsHotline = contact?.ClaimsHotline ?? "";
        Coverage = contact?.Coverage ?? "";
        CustomerNumber = contact?.CustomerNumber ?? "";
        Notes = contact?.Notes ?? "";
        IsTeamVisible = contact is null || contact.Visibility == BusinessContactVisibility.Team;
        IsManagementVisible = !IsTeamVisible;
        AllLocations = contact?.AllLocations ?? !single;
        SomeLocations = !AllLocations;
        Locations.Clear();
        foreach (var place in _places)
        {
            var choice = new BusinessLocationChoice(place.Id, place.Name) { IsSelected = contact is null ? single : contact.LocationIds.Contains(place.Id) };
            choice.PropertyChanged += OnLocationChanged;
            Locations.Add(choice);
        }

        FillReach(contact);
        FillDetails(contact);
        _applying = false;
        _savedSignature = Signature();
        DesignationError = ReachError = PhoneError = MobileError = EmailError = HotlineError = LocationsError = null;
        StatusText = null;
        Error = null;
        foreach (var name in new[]
                 {
                     nameof(ShowForm), nameof(ShowLookup), nameof(ShowPlaceholder), nameof(HeadingText), nameof(IsArchived), nameof(IsEditable), nameof(IsManagementOnly), nameof(CanMenu), nameof(CanArchive),
                     nameof(CanUnarchive), nameof(HasReach), nameof(IsInsurance), nameof(HasUnsavedChanges),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnLocationChanged(object? sender, PropertyChangedEventArgs e)
    {
        LocationsError = null;
        Touch();
    }

    private void FillReach(BusinessContactDto? contact)
    {
        Reach.Clear();
        if (contact is null)
        {
            return;
        }

        void Add(string label, string? value, Uri? link)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Reach.Add(new BusinessReachEntry(label, value, link, LaunchAsync, CopyAsync));
            }
        }

        Add("Telefon", contact.Phone, PhoneLinks.Tel(contact.Phone));
        Add("Mobil", contact.Mobile, PhoneLinks.Tel(contact.Mobile));
        if (contact.Category == BusinessContactCategory.Insurance)
        {
            Add("Schadenshotline", contact.ClaimsHotline, PhoneLinks.Tel(contact.ClaimsHotline));
        }

        Add("E-Mail", contact.Email, string.IsNullOrEmpty(contact.Email) ? null : new Uri($"mailto:{contact.Email}"));
    }

    private void FillDetails(BusinessContactDto? contact)
    {
        Details.Clear();
        if (contact is null)
        {
            return;
        }

        var lines = new (string Label, string? Value)[]
        {
            ("Kategorie", CategoryName(contact.Category)),
            ("Firma", contact.Company),
            ("Ansprechperson", contact.ContactPerson),
            ("Adresse", contact.Address),
            ("Zuständig für", contact.Responsibility),
            ("Versicherer", contact.Insurer),
            ("Polizzennummer", contact.PolicyNumber),
            ("Deckung", contact.Coverage),
            ("Kunden-/Vertragsnummer", contact.CustomerNumber),
            ("Gilt an", PlacesLabel(contact)),
            ("Sichtbar für", contact.Visibility == BusinessContactVisibility.Team ? "Team" : "Leitung"),
            ("Notizen", contact.Notes),
        };
        foreach (var (label, value) in lines)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Details.Add(new BusinessDetailLine(label, value));
            }
        }
    }

    private string PlacesLabel(BusinessContactDto contact)
    {
        if (contact.AllLocations)
        {
            return "alle Standorte";
        }

        var names = contact.LocationIds.Select(id => _places.FirstOrDefault(l => l.Id == id)?.Name ?? "?").ToList();
        return names.Count <= 2 ? string.Join(", ", names) : $"{names.Count} Standorte";
    }

    private Task LaunchAsync(Uri uri) => ContactActions?.LaunchAsync(uri) ?? Task.CompletedTask;

    private async Task CopyAsync(string text, string what)
    {
        if (ContactActions is null)
        {
            return;
        }

        await ContactActions.CopyAsync(text).ConfigureAwait(true);
        Message = $"{what} kopiert.";
    }

    /// <summary>Eine Angabe je Zeile, alle befüllten Felder.</summary>
    public static string CopyText(BusinessContactDto contact)
    {
        var lines = new (string Label, string? Value)[]
        {
            ("Bezeichnung", contact.Designation),
            ("Firma", contact.Company),
            ("Ansprechperson", contact.ContactPerson),
            ("Telefon", contact.Phone),
            ("Mobil", contact.Mobile),
            ("E-Mail", contact.Email),
            ("Adresse", contact.Address),
            ("Zuständig für", contact.Responsibility),
            ("Versicherer", contact.Insurer),
            ("Polizzennummer", contact.PolicyNumber),
            ("Deckung", contact.Coverage),
            ("Schadenshotline", contact.ClaimsHotline),
            ("Kunden-/Vertragsnummer", contact.CustomerNumber),
        };
        return string.Join('\n', lines.Where(l => !string.IsNullOrEmpty(l.Value)).Select(l => $"{l.Label}: {l.Value}"));
    }

    [RelayCommand]
    private Task CopyAllAsync() => _current is { } contact ? CopyAsync(CopyText(contact), "Angaben") : Task.CompletedTask;

    // ---------- Eingaben ----------

    partial void OnIsTeamVisibleChanged(bool value)
    {
        if (IsManagementVisible == value)
        {
            IsManagementVisible = !value;
        }
    }

    partial void OnIsManagementVisibleChanged(bool value)
    {
        if (IsTeamVisible == value)
        {
            IsTeamVisible = !value;
        }
    }

    partial void OnAllLocationsChanged(bool value)
    {
        if (SomeLocations == value)
        {
            SomeLocations = !value;
        }
    }

    partial void OnSomeLocationsChanged(bool value)
    {
        if (AllLocations == value)
        {
            AllLocations = !value;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        switch (name)
        {
            case nameof(Designation):
                DesignationError = null;
                break;
            case nameof(Company) or nameof(ContactPerson):
                ReachError = null;
                break;
            case nameof(Phone):
                PhoneError = ReachError = null;
                break;
            case nameof(Mobile):
                MobileError = ReachError = null;
                break;
            case nameof(Email):
                EmailError = ReachError = null;
                break;
            case nameof(ClaimsHotline):
                HotlineError = null;
                break;
            case nameof(AllLocations):
                LocationsError = null;
                break;
            case nameof(Category):
                OnPropertyChanged(nameof(IsInsurance));
                break;
        }

        Touch();
    }

    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string Signature() => string.Join('\u001f', Category?.Value, Designation, Company, ContactPerson, Responsibility, Phone, Mobile, Email, Address, Insurer, PolicyNumber, ClaimsHotline, Coverage,
        CustomerNumber, Notes, IsTeamVisible, AllLocations, string.Join(',', Locations.Where(l => l.IsSelected).Select(l => l.Id)));

    // ---------- Prüfen und Speichern ----------

    private static bool PhoneValid(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || (trimmed.Length <= 40 && PhonePattern().IsMatch(trimmed) && trimmed.Count(char.IsAsciiDigit) >= 3);
    }

    [GeneratedRegex(@"^\+?[0-9 ()/.-]+$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();

    /// <summary>Dieselben Regeln wie am Server (BusinessContactService.ValidateAsync); der erste Fehler bekommt den Fokus.</summary>
    private bool Validate()
    {
        var designation = Designation.Trim();
        var email = Email.Trim();
        DesignationError = designation.Length is 0 or > 80 ? "Bezeichnung mit höchstens 80 Zeichen angeben, z. B. „Installateur“." : null;
        PhoneError = PhoneValid(Phone) ? null : PhoneRule;
        MobileError = PhoneValid(Mobile) ? null : PhoneRule;
        HotlineError = !IsInsurance || PhoneValid(ClaimsHotline) ? null : PhoneRule;
        EmailError = email.Length > 0 && (!EmailPattern().IsMatch(email) || email.Length > 200) ? "Keine gültige E-Mail-Adresse." : null;
        ReachError = new[] { Company, ContactPerson, Phone, Mobile, Email }.All(string.IsNullOrWhiteSpace) ? "Mindestens Firma, Ansprechperson, Telefon, Mobil oder E-Mail angeben." : null;
        LocationsError = !AllLocations && Locations.All(l => !l.IsSelected) ? "Mindestens einen Standort wählen." : null;
        var firstInvalid = DesignationError is not null ? DesignationField
            : ReachError is not null ? CompanyField
            : PhoneError is not null ? PhoneField
            : MobileError is not null ? MobileField
            : EmailError is not null ? EmailField
            : HotlineError is not null ? HotlineField
            : LocationsError is not null ? SomeLocationsField
            : null;
        FocusTarget = firstInvalid;
        return firstInvalid is null;
    }

    private static string? OrNull(string value) => value.Trim() is { Length: > 0 } text ? text : null;

    private BusinessContactInput InputOf() => new(Category!.Value!.Value, Designation.Trim(), OrNull(Company), OrNull(ContactPerson), OrNull(Phone), OrNull(Mobile), OrNull(Email), OrNull(Address),
        OrNull(Responsibility), OrNull(CustomerNumber), IsInsurance ? OrNull(Insurer) : null, IsInsurance ? OrNull(PolicyNumber) : null, IsInsurance ? OrNull(Coverage) : null,
        IsInsurance ? OrNull(ClaimsHotline) : null, AllLocations, AllLocations ? [] : [.. Locations.Where(l => l.IsSelected).Select(l => l.Id)], OrNull(Notes),
        IsTeamVisible ? BusinessContactVisibility.Team : BusinessContactVisibility.Management);

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !ShowForm || IsArchived || !Validate())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        var body = InputOf();
        var result = _current is { } contact
            ? await api.PutAsync<BusinessContactDto>($"api/business-contacts/{contact.Id}", body).ConfigureAwait(true)
            : await api.PostAsync<BusinessContactDto>("api/business-contacts", body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Eintrag wurde nicht gespeichert.";
            return false;
        }

        Message = null;
        if (!await ReloadAsync(saved.Id).ConfigureAwait(true))
        {
            // Gespeichert ist gespeichert: der Eintrag gilt mit seiner Kennung weiter, sonst legt ein zweites Speichern einen neuen an.
            var reloadError = Error;
            _all = [.. _all.Where(c => c.Id != saved.Id), saved];
            Rebuild();
            Select(saved.Id);
            Error = reloadError;
        }

        StatusText = "Gespeichert.";
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (IsCreating)
        {
            IsCreating = false;
            Select(Rows.FirstOrDefault()?.Id);
        }
        else
        {
            Apply(_current);
        }
    }

    /// <summary>Archivieren löscht nichts; wie im Web trotzdem mit Rückfrage, weil der Assistent den Eintrag danach nicht mehr nennt.</summary>
    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (_current is not { IsArchived: false } contact || !await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Eintrag archivieren?",
            $"Der Eintrag „{contact.Designation}“ bleibt erhalten, steht aber nicht mehr in der Liste, und der Assistent nennt ihn nicht mehr. Im Archiv lässt er sich zurückholen.", "Archivieren", IsDestructive: false)).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await ChangeAsync(api.PostAsync<BusinessContactDto>($"api/business-contacts/{contact.Id}/archive", null), "Archiviert.").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task UnarchiveAsync()
    {
        if (_current is { IsArchived: true } contact)
        {
            await ChangeAsync(api.PostAsync<BusinessContactDto>($"api/business-contacts/{contact.Id}/unarchive", null), "Zurückgeholt.").ConfigureAwait(true);
        }
    }

    private async Task ChangeAsync(Task<ApiResult<BusinessContactDto>> call, string done)
    {
        var result = await call.ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            Error = result.Error ?? "Der Eintrag wurde nicht geändert.";
            return;
        }

        await ReloadAsync(_all.Any(c => c.Id == changed.Id) && (ShowArchived || !changed.IsArchived) ? changed.Id : null).ConfigureAwait(true);
        Message = done;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;
}

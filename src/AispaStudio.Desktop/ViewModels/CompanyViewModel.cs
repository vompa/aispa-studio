using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Land des Unternehmenssitzes in der Auswahl: Kürzel für die API, Name für die Oberfläche.</summary>
public sealed record CountryOption(string Code, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Firmendaten des Spas (ADR 0052), die erste native Maske der Verwaltung (R05 Slice 93, Vorbild der übrigen): dieselben Felder, Prüfungen und Hilfetexte wie
/// <c>web/src/admin/CompanyTab.tsx</c>. Gespeichert wird alles gemeinsam (<c>PUT api/tenant</c>); leere Felder gehen als „keine Angabe“. Prüfen und Speichern
/// macht die API; hier wird nur der Pflichtname vorab geprüft, damit der Fehler am Feld steht.
/// </summary>
public sealed partial class CompanyViewModel : ViewModelBase, INativeArea
{
    /// <summary>Feldkennung aus „Jetzt ergänzen“ des Assistenten (<c>FieldMasks</c>).</summary>
    public const string WebsiteField = "company.website";

    /// <summary>Feldkennung des Namens, für den Fokus nach einem Fehler.</summary>
    public const string NameField = "name";

    /// <summary>Feldkennung der Websites, die den Kundenbereich einbetten dürfen.</summary>
    public const string EmbedField = "embed";

    private static readonly string[] Inputs =
    [
        nameof(Name), nameof(Country), nameof(LegalName), nameof(VatId), nameof(CompanyRegisterNumber), nameof(CompanyRegisterCourt), nameof(Street),
        nameof(PostalCode), nameof(City), nameof(Phone), nameof(Email), nameof(Website), nameof(AccountHolder), nameof(Iban), nameof(Bic),
        nameof(BusinessPurpose), nameof(SupervisoryAuthority), nameof(ChamberAndRules), nameof(EmbedOrigins),
    ];

    private readonly IWorkstationApi _api;
    private TenantDto? _saved;
    private TenantInput? _savedInput;
    private string _savedEmbed = "";
    private string? _bookingUrl;
    private bool _applying;

    public CompanyViewModel(IWorkstationApi api, TimeZoneInfo zone)
    {
        _api = api;
        History = new HistorySectionViewModel(api, "api/tenant/history", zone, "Wer die Firmendaten wann geändert hat");
    }

    public string Title => "Firmendaten";

    public static IReadOnlyList<CountryOption> Countries { get; } =
        [new("AT", "Österreich"), new("DE", "Deutschland"), new("CH", "Schweiz")];

    /// <summary>Abschnitt „Verlauf“ (ADR 0082): wer die Firmendaten wann geändert hat.</summary>
    public HistorySectionViewModel History { get; }

    /// <summary>Die Firmendaten wurden gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial CountryOption Country { get; set; } = Countries[0];

    [ObservableProperty]
    public partial string LegalName { get; set; } = "";

    [ObservableProperty]
    public partial string VatId { get; set; } = "";

    [ObservableProperty]
    public partial string CompanyRegisterNumber { get; set; } = "";

    [ObservableProperty]
    public partial string CompanyRegisterCourt { get; set; } = "";

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

    [ObservableProperty]
    public partial string Website { get; set; } = "";

    [ObservableProperty]
    public partial string AccountHolder { get; set; } = "";

    [ObservableProperty]
    public partial string Iban { get; set; } = "";

    [ObservableProperty]
    public partial string Bic { get; set; } = "";

    [ObservableProperty]
    public partial string BusinessPurpose { get; set; } = "";

    [ObservableProperty]
    public partial string SupervisoryAuthority { get; set; } = "";

    [ObservableProperty]
    public partial string ChamberAndRules { get; set; } = "";

    /// <summary>
    /// Websites, die den Kundenbereich einbetten dürfen (ADR 0080, Nachtrag), eine Adresse je Zeile; eigene Einstellung des Spas
    /// (<c>api/tenant/embedding</c>), im Web ein eigener Abschnitt unter den Firmendaten (<c>EmbedSection.tsx</c>).
    /// </summary>
    [ObservableProperty]
    public partial string EmbedOrigins { get; set; } = "";

    /// <summary>Der Code zum Einbetten des Kundenbereichs (iframe mit der Adresse, die die API nennt); leer, solange die Adresse fehlt.</summary>
    public string? EmbedCode => _bookingUrl is null
        ? null
        : $"<iframe src=\"{_bookingUrl}\" title=\"Online-Buchung\" width=\"100%\" height=\"900\" style=\"border:0\" loading=\"lazy\"></iframe>";

    public bool HasEmbedCode => EmbedCode is not null;

    /// <summary>„Code kopiert.“ neben dem Knopf, bis die Maske neu geladen oder die Einbett-Eingabe geändert wird.</summary>
    [ObservableProperty]
    public partial string? EmbedStatus { get; set; }

    /// <summary>Fehler am Feld der Websites (die API nennt Zeile und Grund).</summary>
    [ObservableProperty]
    public partial string? EmbedError { get; private set; }

    /// <summary>Fehler am Namensfeld (Pflichtfeld).</summary>
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    /// <summary>Fehler der API beim Speichern (z. B. ungültige IBAN), über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Das Feld, das den Fokus bekommen soll (<see cref="WebsiteField"/>, <see cref="NameField"/>); die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    public bool HasUnsavedChanges => _savedInput is not null && (!Equals(Input(), _savedInput) || EmbedOrigins != _savedEmbed);

    /// <summary>Lädt die Firmendaten, beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await _api.GetAsync<TenantDto>("api/tenant").ConfigureAwait(true);
        if (result.Value is not { } tenant)
        {
            LoadError = result.Error ?? "Die Firmendaten wurden nicht geladen.";
            return;
        }

        var embedding = await _api.GetAsync<EmbeddingSettings>("api/tenant/embedding").ConfigureAwait(true);
        if (embedding.Value is not { } settings)
        {
            LoadError = embedding.Error ?? "Die Firmendaten wurden nicht geladen.";
            return;
        }

        Apply(tenant);
        ApplyEmbed(settings);
        IsLoaded = true;
    }

    /// <summary>Lädt nur, wenn noch nichts geladen ist (beim Öffnen des Bereichs).</summary>
    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        if (Name.Trim().Length == 0)
        {
            NameError = "Der Name des Spas fehlt. Trage ihn ein, wie Gäste und Team ihn sehen.";
            FocusTarget = NameField;
            return false;
        }

        NameError = null;
        Error = null;
        IsSaving = true;
        var result = await _api.PutAsync<TenantDto>("api/tenant", Input() with { Name = Name.Trim() }).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } tenant)
        {
            Error = result.Error ?? "Die Firmendaten wurden nicht gespeichert.";
            return false;
        }

        Apply(tenant);
        if (EmbedOrigins != _savedEmbed)
        {
            var lines = EmbedOrigins.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            var embedded = await _api.PutAsync<EmbeddingSettings>("api/tenant/embedding", new EmbeddingSettings(lines)).ConfigureAwait(true);
            if (embedded.Value is not { } settings)
            {
                EmbedError = embedded.Error ?? "Die Websites wurden nicht gespeichert.";
                FocusTarget = EmbedField;
                return false;
            }

            ApplyEmbed(settings);
        }

        StatusText = "Gespeichert.";
        Saved?.Invoke();
        if (History.IsOpen)
        {
            await History.LoadAsync().ConfigureAwait(true);
        }

        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (_saved is not null)
        {
            Apply(_saved);
        }

        _applying = true;
        EmbedOrigins = _savedEmbed;
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        NameError = null;
        EmbedError = null;
        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>„Jetzt ergänzen“ des Assistenten: das Feld bekommt den Fokus, sobald die Maske steht.</summary>
    public void FocusField(string? field) => FocusTarget = field;

    /// <summary>Die Firmendaten haben nur einen Eintrag und keine Abschnitte; es zählt nur das Feld.</summary>
    public void Open(Guid? id, string? section, string? field) => FocusField(field);

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
        else if (name == nameof(EmbedOrigins))
        {
            EmbedError = null;
            EmbedStatus = null;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Apply(TenantDto tenant)
    {
        _applying = true;
        _saved = tenant;
        var company = tenant.Company ?? CompanyProfile.Empty;
        Name = tenant.Name;
        Country = Countries.FirstOrDefault(c => c.Code == tenant.Country) ?? Countries[0];
        LegalName = company.LegalName ?? "";
        VatId = company.VatId ?? "";
        CompanyRegisterNumber = company.CompanyRegisterNumber ?? "";
        CompanyRegisterCourt = company.CompanyRegisterCourt ?? "";
        Street = company.Street ?? "";
        PostalCode = company.PostalCode ?? "";
        City = company.City ?? "";
        Phone = company.Phone ?? "";
        Email = company.Email ?? "";
        Website = company.Website ?? "";
        AccountHolder = company.AccountHolder ?? "";
        Iban = company.Iban ?? "";
        Bic = company.Bic ?? "";
        BusinessPurpose = company.BusinessPurpose ?? "";
        SupervisoryAuthority = company.SupervisoryAuthority ?? "";
        ChamberAndRules = company.ChamberAndRules ?? "";
        _savedInput = Input();
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void ApplyEmbed(EmbeddingSettings settings)
    {
        EmbedStatus = null;
        _applying = true;
        _savedEmbed = string.Join('\n', settings.AllowedOrigins);
        EmbedOrigins = _savedEmbed;
        _applying = false;
        if (settings.BookingUrl is not null)
        {
            _bookingUrl = settings.BookingUrl;
            OnPropertyChanged(nameof(EmbedCode));
            OnPropertyChanged(nameof(HasEmbedCode));
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Was gespeichert würde; leere Felder heißen „keine Angabe“ wie im Web (<c>e.target.value || null</c>).</summary>
    private TenantInput Input() => new(Name, Country.Code, new CompanyProfile(
        Value(LegalName), Value(Street), Value(PostalCode), Value(City), Value(Phone), Value(Email), Value(Website), Value(VatId), Value(CompanyRegisterNumber),
        Value(CompanyRegisterCourt), Value(AccountHolder), Value(Iban), Value(Bic), Value(BusinessPurpose), Value(SupervisoryAuthority), Value(ChamberAndRules)));

    private static string? Value(string text) => text.Length == 0 ? null : text;
}

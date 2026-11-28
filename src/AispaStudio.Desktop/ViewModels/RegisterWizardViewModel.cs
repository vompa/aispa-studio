using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public sealed partial class RegisterWizardViewModel(IWorkstationApi api, CurrentAccess access, Guid? preferredLocation,
    ToastState? toast = null, TimeProvider? time = null) : ViewModelBase
{
    private const int PlaceStep = 1;
    private const int ProviderStep = 2;
    private const int DetailsStep = 3;
    private const int CreateStep = 4;
    private const int FinishStep = 5;

    public event Action? CloseRequested;
    public event Func<CashRegisterDto, Task>? RegisterCreated;
    public event Action<CashRegisterDto>? RegisterUpdated;

    public static IReadOnlyList<CountryOption> Countries => CompanyViewModel.Countries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLocations))]
    public partial IReadOnlyList<LocationDto> Locations { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoLocations))]
    public partial bool LocationsLoaded { get; private set; }

    public bool ShowNoLocations => LocationsLoaded && Locations.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial LocationDto? Location { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial CountryOption Country { get; set; } = Countries[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoProviders))]
    public partial IReadOnlyList<FiscalProviderDto> Providers { get; private set; } = [];

    public bool ShowNoProviders => Providers.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPracticeChoice), nameof(IsPractice))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial FiscalProviderDto? Provider { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCredentialFields))]
    public partial IReadOnlyList<FiscalCredentialEntry> CredentialFields { get; private set; } = [];

    public bool HasCredentialFields => CredentialFields.Count != 0;

    public bool ShowPracticeChoice => Provider is { IsTest: false };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPractice))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial bool PracticeMode { get; set; }

    public bool IsPractice => Provider?.IsTest == true || PracticeMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial bool PracticeConfirmed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CodeError))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial string Code { get; set; } = "";

    public string? CodeError => Code.Length != 0 && !RegisterCode().IsMatch(Code.Trim())
        ? "Buchstaben, Ziffern und Bindestrich verwenden, höchstens 50 Zeichen."
        : null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(CreateCommand))]
    public partial string ReceiptHeader { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText), nameof(StepTitle), nameof(ShowPlace), nameof(ShowProvider), nameof(ShowDetails), nameof(ShowCreate), nameof(ShowFinish), nameof(ShowNext), nameof(ShowBack))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand), nameof(CreateCommand))]
    public partial int Step { get; private set; } = PlaceStep;

    public string StepText => $"Schritt {Step} von {FinishStep}";

    public string StepTitle => Step switch
    {
        PlaceStep => "Standort und Land",
        ProviderStep => "Art der Kassa",
        DetailsStep => "Kassen-ID und Belegkopf",
        CreateStep => "Kassa und Startbeleg",
        _ => "Kassa angelegt",
    };

    public bool ShowPlace => Step == PlaceStep;
    public bool ShowProvider => Step == ProviderStep;
    public bool ShowDetails => Step == DetailsStep;
    public bool ShowCreate => Step == CreateStep;
    public bool ShowFinish => Step == FinishStep;
    public bool ShowNext => Step < CreateStep;
    public bool ShowBack => Step is > PlaceStep and < FinishStep;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(NextCommand), nameof(BackCommand), nameof(CreateCommand), nameof(CloseCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FinishText), nameof(ShowPracticeFinish))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial CashRegisterDto? Created { get; private set; }

    [ObservableProperty]
    public partial RegisterFiscalViewModel? Fiscal { get; private set; }

    public bool ShowPracticeFinish => Created?.IsPractice == true;

    public string FinishText => (Created?.Country, ShowPracticeFinish) switch
    {
        (FiscalCountry.AT, true) => "Diese Übungskassa nicht in FinanzOnline registrieren. Für den Echtbetrieb eine eigene Kassa einrichten.",
        (FiscalCountry.AT, false) => "Die Kassa in FinanzOnline registrieren und den Startbeleg prüfen.",
        (FiscalCountry.DE, true) => "Diese Übungskassa nicht über ELSTER melden. Für den Echtbetrieb eine eigene Kassa einrichten.",
        (FiscalCountry.DE, false) => "Die Kassa über ELSTER melden. Die Anleitung steht in der Hilfe zur Kasseneinrichtung.",
        (_, true) => "Für den Echtbetrieb eine eigene Kassa einrichten.",
        _ => "Die Kassa ist für den Echtbetrieb eingerichtet.",
    };

    partial void OnCountryChanged(CountryOption value)
    {
        Provider = null;
        Providers = [];
        Error = null;
    }

    partial void OnProviderChanged(FiscalProviderDto? value)
    {
        ClearCredentials();
        CredentialFields = [.. (value?.Fields ?? []).Select(field => new FiscalCredentialEntry(field, NotifyCredentialChanged))];
        NotifyCredentialChanged();
        PracticeMode = false;
        PracticeConfirmed = false;
    }

    partial void OnPracticeModeChanged(bool value) => PracticeConfirmed = false;

    private bool CanLoad() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var result = await api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error ?? "Die Standorte wurden nicht geladen.";
                return;
            }

            Locations = [.. result.Value.Where(location => access.AllLocations || access.LocationIds.Contains(location.Id))];
            Location = Locations.FirstOrDefault(location => location.Id == preferredLocation) ?? (Locations.Count > 0 ? Locations[0] : null);
            if (Location is { } selected)
            {
                var region = selected.Region?.Split('-', 2)[0];
                var country = region ?? (selected.Currency == "CHF" ? "CH" : null);
                Country = Countries.FirstOrDefault(option => option.Code == country) ?? Countries[0];
            }

            LocationsLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanNext() => !IsBusy && (Step switch
    {
        PlaceStep => Location is not null,
        ProviderStep => ValidProvider,
        DetailsStep => ValidDetails,
        _ => false,
    });

    private bool ValidProvider => Provider is not null && (!IsPractice || PracticeConfirmed)
        && CredentialFields.All(entry => !string.IsNullOrWhiteSpace(entry.Value));

    private bool ValidDetails => RegisterCode().IsMatch(Code.Trim()) && !string.IsNullOrWhiteSpace(ReceiptHeader);

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        if (!CanNext())
        {
            return;
        }

        Error = null;
        if (ShowPlace)
        {
            IsBusy = true;
            try
            {
                var result = await api.GetAsync<List<FiscalProviderDto>>($"api/pos/fiscal/providers?country={Country.Code}").ConfigureAwait(true);
                if (result.Value is null)
                {
                    Error = result.Error ?? "Die Anbieter wurden nicht geladen.";
                    return;
                }

                var selected = Provider;
                Providers = result.Value;
                Provider = Providers.FirstOrDefault(provider => provider.Id == selected?.Id)
                    ?? (Providers.Count == 1 ? Providers[0] : null);
            }
            finally
            {
                IsBusy = false;
            }
        }

        Step++;
    }

    private bool CanBack() => !IsBusy && ShowBack;

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back()
    {
        if (CanBack())
        {
            Error = null;
            Step--;
        }
    }

    private bool CanCreate() => !IsBusy && ShowCreate && Created is null && Location is not null && ValidProvider && ValidDetails;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (!CanCreate() || Location is not { } location || Provider is not { } provider)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var country = Enum.Parse<FiscalCountry>(Country.Code);
            var input = new CashRegisterInput(location.Id, Code.Trim(), country, ReceiptHeader.Trim(), null, null, null, provider.Id, IsPractice,
                FiscalCredentialEntry.Values(CredentialFields));
            var result = await api.PostAsync<CashRegisterDto>("api/pos/registers", input).ConfigureAwait(true);
            if (result.Value is not { } created)
            {
                Error = result.Error ?? "Die Kassa wurde nicht angelegt.";
                return;
            }

            Created = created;
            ClearCredentials();
            var clock = time ?? TimeProvider.System;
            Fiscal = new RegisterFiscalViewModel(api, created, toast ?? new ToastState(clock), clock.LocalTimeZone, showProvider: false)
            {
                Dialogs = Dialogs,
            };
            Fiscal.RegisterChanged += refreshed => RegisterUpdated?.Invoke(refreshed);
            Step = FinishStep;
            if (RegisterCreated is { } afterCreate)
            {
                await afterCreate(created).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanClose() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanClose))]
    internal void Close()
    {
        if (CanClose())
        {
            ClearCredentials();
            Fiscal?.Stop();
            CloseRequested?.Invoke();
        }
    }

    private void NotifyCredentialChanged()
    {
        NextCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void ClearCredentials()
    {
        foreach (var field in CredentialFields)
        {
            field.Value = "";
        }
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,50}$")]
    private static partial Regex RegisterCode();
}

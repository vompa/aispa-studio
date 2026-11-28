using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public sealed partial class FiscalCredentialEntry(FiscalCredentialFieldDto definition, Action changed) : ObservableObject
{
    public string Key => definition.Key;
    public string Label => definition.Label;
    public char PasswordChar => definition.Secret ? '●' : '\0';

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    public partial bool IsConfigured { get; set; }

    public string? Status => IsConfigured ? "Gespeichert" : null;

    partial void OnValueChanged(string value) => changed();

    internal static IReadOnlyDictionary<string, string>? Values(IReadOnlyList<FiscalCredentialEntry> fields) =>
        fields.Count == 0 ? null : fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
}

public sealed record RegistrationField(string Label, string Value)
{
    public string CopyName => $"{Label} kopieren";
}

public sealed record FiscalConfirmationItem(RegisterConfirmationKind Kind, string Label, int? Year, Guid? ReceiptId,
    RegisterConfirmationDto? Confirmation, string Status)
{
    public bool IsConfirmed => Confirmation is not null;
    public bool HasReceipt => ReceiptId is not null;
    public bool CanToggle => Kind != RegisterConfirmationKind.YearlyReceiptCheck || HasReceipt;
    public string ToggleText => IsConfirmed ? "Zurücknehmen" : "Bestätigen";
    public string ToggleName => $"{Label} – Bestätigung {(IsConfirmed ? "zurücknehmen" : "setzen")}";
}

public sealed partial class RegisterFiscalViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly ToastState _toast;
    private readonly bool _showProvider;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private bool _stopped;
    private bool _observingConnection;
    private int _secretVersion;
    private CashRegisterDto _register;

    public RegisterFiscalViewModel(IWorkstationApi api, CashRegisterDto register, ToastState toast, TimeZoneInfo zone, bool showProvider)
    {
        _api = api;
        _register = register;
        _toast = toast;
        _zone = zone;
        _showProvider = showProvider;
        ShowCurrentRegister(register);
    }

    public event Action<CashRegisterDto>? RegisterChanged;

    public bool ShowProvider => _showProvider && !_register.OtherSite;
    public bool ShowProviderActions => ShowProvider && _register.IsActive;
    public string ProviderName => Provider?.Name ?? _register.FiscalProvider ?? "Standard des Landes";
    public bool ShowAustria => !_register.OtherSite && _register.Country == FiscalCountry.AT;
    public bool ShowGermany => !_register.OtherSite && _register.Country == FiscalCountry.DE;
    public bool IsPractice => _register.IsPractice;
    public bool ShowConfirmationRows => Confirmations.Count != 0;
    public bool ShowYearlyReceipt => YearlyReceipt is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProviderName))]
    public partial FiscalProviderDto? Provider { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFields))]
    public partial IReadOnlyList<FiscalCredentialEntry> Fields { get; private set; } = [];

    public bool HasFields => Fields.Count != 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCredentialsCommand), nameof(DeleteCredentialsCommand), nameof(CreateYearlyCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCredentialsCommand))]
    public partial bool CanDeleteCredentials { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial FiscalCheckDto? Check { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegistration))]
    public partial IReadOnlyList<RegistrationField> Registration { get; private set; } = [];

    public bool HasRegistration => Registration.Count != 0;

    internal bool MayCopy(string value) => !_stopped && !_api.IsOffline && Registration.Any(field => field.Value == value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConfirmationRows))]
    public partial IReadOnlyList<FiscalConfirmationItem> Confirmations { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowYearlyReceipt))]
    public partial ReceiptViewModel? YearlyReceipt { get; private set; }

    private string Path(string suffix) => $"api/pos/registers/{_register.Id}/{suffix}";

    public Task LoadProviderAsync() => !ShowProvider ? Task.CompletedTask : RunAsync(async () =>
    {
        var providers = await _api.GetAsync<List<FiscalProviderDto>>($"api/pos/fiscal/providers?country={_register.Country}").ConfigureAwait(true);
        if (_stopped)
        {
            return;
        }

        if (providers.Value is not { } catalog)
        {
            Error = providers.Error ?? "Die Signatureinrichtung wurde nicht geladen.";
            return;
        }

        var provider = _register.FiscalProvider is { } id ? catalog.Find(item => item.Id == id) : catalog.FirstOrDefault();
        Provider = provider;
        if (provider is null && _register.FiscalProvider is not null)
        {
            Error = "Diese Signatureinrichtung ist im Anbieterkatalog nicht verfügbar.";
            return;
        }

        Fields = [.. (provider?.Fields ?? []).Select(field => new FiscalCredentialEntry(field, SaveCredentialsCommand.NotifyCanExecuteChanged))];
        SaveCredentialsCommand.NotifyCanExecuteChanged();
        if (!HasFields || !ShowProviderActions)
        {
            return;
        }

        var status = await _api.GetAsync<FiscalCredentialsDto>(Path("fiscal-credentials")).ConfigureAwait(true);
        if (!_stopped)
        {
            UpdateCredentialStatus(status.Value, status.Error);
        }
    });

    private void UpdateCredentialStatus(FiscalCredentialsDto? status, string? error)
    {
        Error = error;
        CanDeleteCredentials = status?.ConfiguredFields.Count > 0 || error is not null;
        foreach (var field in Fields)
        {
            field.IsConfigured = status?.ConfiguredFields.Contains(field.Key, StringComparer.Ordinal) == true;
        }
    }

    private bool CanSaveCredentials() => !IsBusy && ShowProviderActions && HasFields && Fields.All(field => !string.IsNullOrWhiteSpace(field.Value));
    private bool CanRemoveCredentials() => !IsBusy && ShowProviderActions && CanDeleteCredentials;

    [RelayCommand(CanExecute = nameof(CanSaveCredentials))]
    private Task SaveCredentialsAsync() => !CanSaveCredentials() ? Task.CompletedTask : RunAsync(async () =>
    {
        var values = FiscalCredentialEntry.Values(Fields)!;
        ClearInputs();
        var result = await _api.PutAsync<FiscalCredentialsDto>(Path("fiscal-credentials"), new FiscalCredentialsInput(values)).ConfigureAwait(true);
        if (!_stopped)
        {
            UpdateCredentialStatus(result.Value, result.Error);
        }

        if (result.Value is not null)
        {
            Check = null;
            await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
        }
    });

    [RelayCommand(CanExecute = nameof(CanRemoveCredentials))]
    private Task DeleteCredentialsAsync() => !CanRemoveCredentials() ? Task.CompletedTask : RunAsync(async () =>
    {
        var answer = await AskAsync(new ConfirmRequest("Zugangsdaten löschen?",
            "Die Signatureinrichtung verwendet danach keine gespeicherten Zugangsdaten mehr.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed || _stopped)
        {
            return;
        }

        ClearInputs();
        var result = await _api.DeleteAsync<object>(Path("fiscal-credentials")).ConfigureAwait(true);
        if (!_stopped)
        {
            UpdateCredentialStatus(null, result.Error);
        }

        if (result.Ok)
        {
            Check = null;
            await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
        }
    });

    [RelayCommand]
    private Task CheckConnectionAsync() => !ShowProviderActions ? Task.CompletedTask : RunAsync(async () =>
    {
        Check = null;
        var result = await _api.PostAsync<FiscalCheckDto>(Path("fiscal-check"), null).ConfigureAwait(true);
        if (!_stopped)
        {
            Check = result.Value;
            Error = result.Error;
        }

        if (result.Value is not null)
        {
            await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
        }
    });

    [RelayCommand]
    private Task LoadRegistrationAsync() => !ShowAustria ? Task.CompletedTask : RunAsync(async () =>
    {
        var version = _secretVersion;
        Registration = [];
        var result = await _api.GetAsync<FiscalRegistrationDto>(Path("fiscal-registration")).ConfigureAwait(true);
        if (_stopped || version != _secretVersion)
        {
            return;
        }

        if (result.Value is not { } registration)
        {
            Error = result.Error ?? "Die Registrierungsdaten wurden nicht geladen.";
            return;
        }

        List<RegistrationField> fields =
        [
            new("Kassen-ID", registration.RegisterCode),
            new("Umsatzzählerschlüssel", registration.TurnoverKey),
            new("Prüfwert", registration.Checksum),
            new("Anbieterkennung", registration.FiscalProvider),
        ];
        if (registration.CertificateSerial is { Length: > 0 } serial)
        {
            fields.Add(new("Zertifikatsseriennummer", serial));
        }

        Registration = fields;
    });

    [RelayCommand]
    private Task ToggleConfirmationAsync(FiscalConfirmationItem? item) =>
        item is not { CanToggle: true } || !Confirmations.Contains(item) ? Task.CompletedTask : RunAsync(async () =>
        {
            var suffix = item.Year is { } year && item.ReceiptId is { } id ? $"?year={year}&receiptId={id:D}" : "";
            var route = Path($"confirmations/{item.Kind}{suffix}");
            var confirmations = (_register.Confirmations ?? []).Where(value => value.Kind != item.Kind || value.Year != item.Year).ToList();
            if (item.IsConfirmed)
            {
                var result = await _api.DeleteAsync<object>(route).ConfigureAwait(true);
                if (!result.Ok)
                {
                    Error = result.Error;
                    await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
                    return;
                }
            }
            else
            {
                var result = await _api.PutAsync<RegisterConfirmationDto>(route, null).ConfigureAwait(true);
                if (result.Value is not { } confirmed)
                {
                    Error = result.Error;
                    await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
                    return;
                }

                confirmations.Add(confirmed);
            }

            var update = _register with { Confirmations = confirmations };
            if (item.Kind == RegisterConfirmationKind.YearlyReceiptCheck)
            {
                YearlyReceiptStatusDto ConfirmedState(YearlyReceiptStatusDto status) =>
                    status.Year == item.Year && status.ReceiptId == item.ReceiptId ? status with { Confirmed = !item.IsConfirmed } : status;
                var pending = update.PendingYearlyReceipts?.Select(ConfirmedState).ToList() ?? [];
                if (item.IsConfirmed && item.Year is { } pastYear && item.ReceiptId is { } pastReceipt && update.YearlyReceipt?.Year != pastYear)
                {
                    pending.RemoveAll(status => status.Year == pastYear);
                    pending.Add(new(pastYear, pastReceipt, Confirmed: false, CanCreate: false));
                }

                update = update with
                {
                    YearlyReceipt = update.YearlyReceipt is { } current ? ConfirmedState(current) : null,
                    PendingYearlyReceipts = pending,
                };
            }

            await RefreshAfterMutationAsync(update).ConfigureAwait(true);
        });

    private bool CanCreateYearly() => !IsBusy && ShowAustria && _register.IsActive && _register.YearlyReceipt?.CanCreate == true;

    [RelayCommand(CanExecute = nameof(CanCreateYearly))]
    private Task CreateYearlyAsync() => !CanCreateYearly() ? Task.CompletedTask : RunAsync(async () =>
    {
        var result = await _api.PostAsync<ReceiptDto>(Path("yearly-receipt"), null).ConfigureAwait(true);
        if (result.Value is not { } receipt)
        {
            Error = result.Error ?? "Der Jahresbeleg wurde nicht erstellt.";
            await RefreshAfterMutationAsync(_register).ConfigureAwait(true);
            return;
        }

        ShowReceipt(receipt);
        var current = _register.YearlyReceipt!;
        var receiptId = receipt.SignatureDeviceFailed || receipt.FiscalCode is null ? (Guid?)null : receipt.Id;
        var updated = _register with
        {
            YearlyReceipt = current with { ReceiptId = receiptId, Confirmed = receiptId.HasValue && current.ReceiptId == receiptId && current.Confirmed, CanCreate = false },
        };
        await RefreshAfterMutationAsync(updated).ConfigureAwait(true);
    });

    [RelayCommand]
    private Task OpenYearlyAsync(FiscalConfirmationItem? item) =>
        item?.ReceiptId is not { } id || !Confirmations.Contains(item) ? Task.CompletedTask : RunAsync(async () =>
        {
            var result = await _api.GetAsync<ReceiptDto>($"api/pos/receipts/{id}").ConfigureAwait(true);
            if (result.Value is { } receipt && receipt.RegisterId == _register.Id && receipt.Kind == ReceiptKind.Yearly)
            {
                ShowReceipt(receipt);
            }
            else
            {
                Error = result.Error ?? "Der Jahresbeleg wurde nicht geladen.";
            }
        });

    private void ShowReceipt(ReceiptDto receipt)
    {
        if (!_stopped)
        {
            YearlyReceipt = new ReceiptViewModel(receipt, _zone, _register.Country)
            {
                Document = new DocumentActionsViewModel(PosViewModel.ReceiptDocumentSource(receipt, copy: false), _api, Dialogs)
                {
                    ShowToast = text => _toast.Show(text),
                },
            };
        }
    }

    private async Task RefreshAfterMutationAsync(CashRegisterDto fallback)
    {
        if (!_stopped)
        {
            ShowCurrentRegister(fallback);
        }

        var result = await _api.GetAsync<CashRegisterDto>($"api/pos/registers/{fallback.Id}").ConfigureAwait(true);
        var fresh = result.CachedAt is null && result.Value is { } value ? value : fallback;
        if (!_stopped)
        {
            ShowCurrentRegister(fresh);
            Error ??= result.Error ?? (result.CachedAt is not null ? "Der aktuelle Kassenstatus ist ohne Verbindung nicht verfügbar." : null);
        }

        RegisterChanged?.Invoke(fresh);
    }

    private void ShowCurrentRegister(CashRegisterDto register)
    {
        _register = register;
        var items = new List<FiscalConfirmationItem>();
        if (ShowAustria)
        {
            if (!IsPractice)
            {
                items.Add(Item(RegisterConfirmationKind.FinanzOnlineRegistration, "Registrierung in FinanzOnline"));
            }

            items.Add(Item(RegisterConfirmationKind.StartReceiptCheck, "Prüfung des Startbelegs"));
            var years = (register.Confirmations ?? []).Where(value => value.Kind == RegisterConfirmationKind.YearlyReceiptCheck && value.Year.HasValue)
                .ToDictionary(value => value.Year!.Value, value => new YearlyReceiptStatusDto(value.Year!.Value, value.ReceiptId, true, false));
            foreach (var pending in register.PendingYearlyReceipts ?? [])
            {
                years[pending.Year] = pending;
            }
            if (register.YearlyReceipt is { } current)
            {
                years[current.Year] = current;
            }

            items.AddRange(years.Values.OrderByDescending(status => status.Year).Select(status =>
                Item(RegisterConfirmationKind.YearlyReceiptCheck, $"Prüfung des Jahresbelegs {status.Year}", status)));
        }
        else if (ShowGermany && !IsPractice)
        {
            items.Add(Item(RegisterConfirmationKind.ElsterRegistration, "Meldung über ELSTER"));
        }

        Confirmations = items;
        OnPropertyChanged(nameof(ShowProviderActions));
        CreateYearlyCommand.NotifyCanExecuteChanged();
        SaveCredentialsCommand.NotifyCanExecuteChanged();
        DeleteCredentialsCommand.NotifyCanExecuteChanged();

        FiscalConfirmationItem Item(RegisterConfirmationKind kind, string label, YearlyReceiptStatusDto? year = null)
        {
            var confirmed = year is { Confirmed: false } ? null : (register.Confirmations ?? []).FirstOrDefault(
                value => value.Kind == kind && value.Year == year?.Year && value.ReceiptId == year?.ReceiptId);
            var status = confirmed is not null
                ? $"Bestätigt am {DayLayout.Local(confirmed.ConfirmedAt, _zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-AT"))} von {confirmed.ConfirmedByName ?? confirmed.ConfirmedBy}"
                : year is { ReceiptId: null } ? "Gültiger signierter Jahresbeleg fehlt." : "Bestätigung offen";
            return new(kind, label, year?.Year, year?.ReceiptId, confirmed, status);
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_stopped || IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await action().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearInputs()
    {
        foreach (var field in Fields)
        {
            field.Value = "";
        }
    }

    internal void HideSecrets()
    {
        _secretVersion++;
        Registration = [];
        ClearInputs();
    }

    internal void ObserveConnection(bool observe)
    {
        if (_observingConnection == observe)
        {
            return;
        }

        _observingConnection = observe;
        if (observe)
        {
            _api.ConnectivityChanged += ConnectionChanged;
        }
        else
        {
            _api.ConnectivityChanged -= ConnectionChanged;
        }
    }

    private void ConnectionChanged()
    {
        if (!_api.IsOffline)
        {
            return;
        }

        if (_ui is null)
        {
            HideSecrets();
        }
        else
        {
            _ui.Post(_ => HideSecrets(), null);
        }
    }

    internal void ApplySnapshot(CashRegisterDto register)
    {
        if (register.Id == _register.Id && register != _register)
        {
            if (!register.IsActive)
            {
                HideSecrets();
            }

            ShowCurrentRegister(register);
        }
    }

    internal void Stop()
    {
        _stopped = true;
        ObserveConnection(false);
        HideSecrets();
    }
}

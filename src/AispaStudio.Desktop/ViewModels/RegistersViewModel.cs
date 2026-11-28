using System.Collections.ObjectModel;
using System.Security.Cryptography;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public sealed class RegisterRow(CashRegisterDto register, string location) : ObservableObject
{
    private CashRegisterDto _register = register;

    public CashRegisterDto Register => _register;

    public string Location { get; } = location;

    public string Code => Register.Code;

    public string Country => Register.Country switch
    {
        FiscalCountry.AT => "Österreich",
        FiscalCountry.DE => "Deutschland",
        FiscalCountry.CH => "Schweiz",
        _ => Register.Country.ToString(),
    };

    public string Place => $"{Location} · {Country}";

    public string Status => (Register.IsActive, Register.OtherSite, Register.Readiness?.State) switch
    {
        (false, _, _) => "Außer Betrieb",
        (_, true, _) => "Andere Kassenstelle",
        (_, _, FiscalReadinessState.Ready) => "Bereit",
        (_, _, FiscalReadinessState.Practice) => "Übungsbetrieb",
        (_, _, FiscalReadinessState.NotReady) => "Nicht bereit",
        _ => "Aktiv",
    };

    public bool IsPractice => Register.IsPractice;

    public string Name => $"{Code}, {Place}, {Status}{(IsPractice ? ", Übungskassa" : "")}";

    public IReadOnlyList<string> Reasons => Register.Readiness?.Reasons ?? [];

    internal void MarkDecommissioned(CashRegisterDto value)
    {
        _register = value;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Reasons));
    }
}

public sealed partial class RegistersViewModel(IWorkstationApi api, ToastState toast) : ViewModelBase, INativeArea
{
    private Guid? _requestedRegisterId;

    public ObservableCollection<RegisterRow> Rows { get; } = [];

    public event Action? RegisterWizardRequested;

    public event Action<CashRegisterDto>? RegisterChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DecommissionCommand))]
    public partial RegisterRow? SelectedRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecommissionCommand), nameof(LoadCommand))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    public bool HasSelection => SelectedRow is not null;

    public string Title => "Kassen";

    public bool HasUnsavedChanges => false;

    public Task<bool> SaveChangesAsync() => Task.FromResult(true);

    public void Discard() { }

    public void Open(Guid? id, string? section, string? field)
    {
        if (id is not { } registerId)
        {
            return;
        }

        if (IsLoaded)
        {
            SelectedRow = Rows.FirstOrDefault(row => row.Register.Id == registerId) ?? SelectedRow;
        }
        else
        {
            _requestedRegisterId = registerId;
        }
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        LoadError = null;
        try
        {
            var registersTask = api.GetAsync<List<CashRegisterDto>>("api/pos/registers");
            var locationsTask = api.GetAsync<List<LocationDto>>("api/locations");
            await Task.WhenAll(registersTask, locationsTask).ConfigureAwait(true);
            var registers = await registersTask.ConfigureAwait(true);
            var locations = await locationsTask.ConfigureAwait(true);
            if (registers.Value is null || locations.Value is null)
            {
                LoadError = registers.Error ?? locations.Error;
                return;
            }

            var selectedId = _requestedRegisterId ?? SelectedRow?.Register.Id;
            _requestedRegisterId = null;
            var names = locations.Value.ToDictionary(location => location.Id, location => location.Name);
            Rows.Clear();
            foreach (var register in registers.Value)
            {
                Rows.Add(new RegisterRow(register, names.GetValueOrDefault(register.LocationId, "Unbekannter Standort")));
            }

            SelectedRow = Rows.FirstOrDefault(row => row.Register.Id == selectedId) ?? Rows.FirstOrDefault();
            IsLoaded = true;
            OnPropertyChanged(nameof(ShowEmpty));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoad() => !IsLoading;

    [RelayCommand]
    private void StartRegisterWizard() => RegisterWizardRequested?.Invoke();

    private bool CanDecommission() => !IsLoading && SelectedRow?.Register is { IsActive: true, OtherSite: false };

    [RelayCommand(CanExecute = nameof(CanDecommission))]
    private async Task DecommissionAsync()
    {
        if (!CanDecommission() || SelectedRow is not { } row)
        {
            return;
        }

        var register = row.Register;
        Error = null;
        if (HasUnresolvedSubstitute(register.Id))
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Kassa außer Betrieb nehmen?",
            $"An der Kassa {register.Code} kann danach niemand mehr kassieren.", "Außer Betrieb nehmen",
            Notice: register.Country == FiscalCountry.AT && !register.IsPractice ? "Die Kassa danach in FinanzOnline abmelden." : null)).ConfigureAwait(true);
        if (!answer.IsConfirmed || HasUnresolvedSubstitute(register.Id))
        {
            return;
        }

        var result = await api.PostAsync<ReceiptDto>($"api/pos/registers/{register.Id}/decommission", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Die Kassa konnte nicht außer Betrieb genommen werden.";
            return;
        }

        var updated = register with { IsActive = false, Readiness = null };
        Rows.FirstOrDefault(item => item.Register.Id == register.Id)?.MarkDecommissioned(updated);
        DecommissionCommand.NotifyCanExecuteChanged();
        RegisterChanged?.Invoke(updated);

        try
        {
            api.Offline?.MarkRegisterDecommissioned(register.Id);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Error = "Die Kassa ist außer Betrieb. Der lokale Stand konnte nicht gespeichert werden.";
            return;
        }

        toast.Show("Kassa außer Betrieb genommen");
    }

    private bool HasUnresolvedSubstitute(Guid registerId)
    {
        if (api.Offline?.HasUnresolvedSubstitute(registerId) != true)
        {
            return false;
        }

        Error = "Vor der Außerbetriebnahme im Bereich „Abgleich“ alle Ersatzbelege dieser Kassa nacherfassen.";
        return true;
    }
}

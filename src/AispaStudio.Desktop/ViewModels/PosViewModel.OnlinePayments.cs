using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

public partial class PosViewModel
{
    /// <summary>Die Karte „Online-Zahlungen“, solange sie offen ist; sonst null.</summary>
    [ObservableProperty]
    public partial OnlinePaymentsViewModel? OnlinePayments { get; private set; }

    [RelayCommand]
    private async Task ToggleOnlinePaymentsAsync()
    {
        if (OnlinePayments is not null || Register is not { } register)
        {
            OnlinePayments = null;
            return;
        }

        var card = new OnlinePaymentsViewModel(register.LocationId, _api, Zone, () => OnlinePayments = null);
        OnlinePayments = card;
        await card.LoadAsync().ConfigureAwait(true);
    }
}

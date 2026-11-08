using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Mehrplatz im eigenen Netz (ADR 0041), am Haupt-PC: die API per HTTPS für die anderen PCs im Spa freigeben und Adresse samt
/// Verbindungscode zeigen. Die Datenbank bleibt nur auf diesem PC erreichbar. Freigeben darf, wer den Standort verwalten darf.
/// </summary>
public partial class NetworkViewModel(ILocalServer local) : ViewModelBase
{
    /// <summary>Freigeben und zurücknehmen (Recht „Standort verwalten“).</summary>
    public bool CanConfigure { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShared), nameof(AddressesText), nameof(Code), nameof(StateText))]
    public partial NetworkShareInfo? Share { get; private set; } = local.Network;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShareCommand), nameof(StopSharingCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

    public bool IsShared => Share is not null;

    public string StateText => IsShared
        ? "Freigegeben. Die anderen PCs im Spa verbinden sich mit einer dieser Adressen und dem Verbindungscode."
        : "Nicht freigegeben. AISPA Studio läuft nur auf diesem PC.";

    public string AddressesText => Share is { } share ? string.Join(Environment.NewLine, share.Addresses) : "";

    public string Code => Share?.Code ?? "";

    private bool CanShare => CanConfigure && !IsBusy && !IsShared;

    [RelayCommand(CanExecute = nameof(CanShare))]
    private Task ShareAsync() => ApplyAsync(shared: true);

    private bool CanStopSharing => CanConfigure && !IsBusy && IsShared;

    [RelayCommand(CanExecute = nameof(CanStopSharing))]
    private Task StopSharingAsync() => ApplyAsync(shared: false);

    private async Task ApplyAsync(bool shared)
    {
        IsBusy = true;
        Message = shared ? "Gebe frei und starte neu …" : "Nehme die Freigabe zurück …";
        try
        {
            var restarted = await local.SetSharedAsync(shared).ConfigureAwait(true);
            Share = local.Network;
            Message = !restarted ? "Gespeichert. Gilt ab dem nächsten Start der App."
                : shared ? "Freigegeben. Fragt Windows nach der Firewall, bitte für private Netzwerke erlauben."
                : "Die anderen PCs sind getrennt.";
        }
        catch (Exception ex) when (ex is LocalServerException or IOException or UnauthorizedAccessException)
        {
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsShared));
            ShareCommand.NotifyCanExecuteChanged();
            StopSharingCommand.NotifyCanExecuteChanged();
        }
    }
}

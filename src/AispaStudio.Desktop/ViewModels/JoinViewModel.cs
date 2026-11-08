using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Neuer PC eines bestehenden Spas (ADR 0041, ADR 0048): statt ein Spa einzurichten die zentrale Datenbank und den Schlüsselcode
/// des Spas eintragen. Der erste Abgleich holt den ganzen Bestand samt Zugängen; danach meldet sich das Team wie gewohnt an.
/// </summary>
/// <param name="joined">Danach den Startbildschirm neu laden; er wartet, bis das Spa da ist.</param>
public partial class JoinViewModel(ILocalServer local, Func<Task> joined) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string ConnectionInput { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string ShareCodeInput { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Aufgeklappt, sobald jemand verbinden will.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [RelayCommand]
    private void Open() => IsOpen = true;

    private bool CanJoin => !IsBusy && ConnectionInput.Trim().Length > 0 && KeySeal.IsComplete(ShareCodeInput);

    [RelayCommand(CanExecute = nameof(CanJoin))]
    private async Task JoinAsync()
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            local.SetShareCode(ShareCodeInput);
            await local.SetCentralAsync(ConnectionInput).ConfigureAwait(true);
            ConnectionInput = ShareCodeInput = "";
            await joined().ConfigureAwait(true);
        }
        catch (LocalServerException ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

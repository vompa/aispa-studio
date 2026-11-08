using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Wiederherstellen aus der täglichen Sicherung (ADR 0041, Schritt 5): auf einem neuen PC vor der Einrichtung oder wenn die
/// Zugangsdaten nicht mehr lesbar sind. Braucht die Sicherung (<c>aispa-….dump</c>, die Schlüsseldatei daneben) und den
/// Wiederherstellungsschlüssel.
/// </summary>
/// <param name="restored">Danach neu verbinden; die Anmeldung gilt wie vor der Sicherung.</param>
public partial class RestoreViewModel(ILocalServer local, Func<Task> restored) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial string DumpFile { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Aufgeklappt, sobald jemand wiederherstellen will.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [RelayCommand]
    private void Open() => IsOpen = true;

    private bool CanRestore => !IsBusy && DumpFile.Trim().Length > 0 && Code.Count(char.IsLetterOrDigit) == 24;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            await local.RestoreAsync(DumpFile.Trim().Trim('"'), Code, new Progress<string>(step => Message = step)).ConfigureAwait(true);
            Code = "";
            Message = "Wiederhergestellt. Anmelden wie gewohnt.";
            await restored().ConfigureAwait(true);
        }
        catch (LocalServerException ex)
        {
            ErrorText = ex.Message;
            Message = null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

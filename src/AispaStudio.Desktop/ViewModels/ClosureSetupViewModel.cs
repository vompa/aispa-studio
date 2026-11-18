using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Einrichtung der Seite „Schließzeiten“ (ADR 0098, Regel 4): die Wartezeit vor der Nachricht an den Gast, je Standort. Sagt das Team Termine einer Schließzeit ab, geht die Nachricht erst
/// danach raus; beginnt der Termin in weniger als 2 Stunden, geht sie sofort, 0 sendet immer sofort (ADR 0081). Eine Einstellung, die man selten ändert, steht deshalb hinter „Einrichtung“
/// und nicht mehr als Karte über der Liste. Gültig sind ganze Zahlen von 0 bis 60.
/// </summary>
public sealed partial class ClosureSetupViewModel(IWorkstationApi api) : ObservableObject
{
    public const int MaxDelayMinutes = 60;

    public const string GuestDelayHint = "Sagt das Team ab, geht die Nachricht erst danach raus. Beginnt der Termin in weniger als 2 Stunden, geht sie sofort. 0 sendet immer sofort.";

    private const string DelayError = "Bitte eine ganze Zahl von 0 bis 60 eingeben.";

    private Guid? _locationId;
    private int? _savedDelay;

    /// <summary>Die Wartezeit wurde gespeichert; die Seite zeigt den Toast.</summary>
    public event Action<string>? ToastRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveGuestDelayCommand))]
    public partial decimal? GuestDelayMinutes { get; set; }

    /// <summary>Die Wartezeit des Standorts ist geladen; sonst fehlt die Einstellung (kein Recht, Standort nicht lesbar).</summary>
    [ObservableProperty]
    public partial bool HasGuestDelay { get; private set; }

    [ObservableProperty]
    public partial string? GuestDelayError { get; private set; }

    /// <summary>Lädt die Wartezeit des Standorts; ohne Standort steht keine Einstellung da.</summary>
    public async Task LoadAsync(Guid? locationId)
    {
        _locationId = locationId;
        var delay = locationId is { } id ? await api.GetAsync<GuestMessageDelayDto>($"api/locations/{id}/guest-message-delay").ConfigureAwait(true) : null;
        HasGuestDelay = delay?.Value is not null;
        _savedDelay = delay?.Value?.Minutes;
        GuestDelayMinutes = delay?.Value?.Minutes;
        GuestDelayError = null;
        SaveGuestDelayCommand.NotifyCanExecuteChanged();
    }

    partial void OnGuestDelayMinutesChanged(decimal? value) => GuestDelayError = null;

    private bool CanSaveGuestDelay => HasGuestDelay && GuestDelayMinutes != _savedDelay;

    /// <summary>„Wartezeit speichern“: eine ganze Zahl von 0 bis 60; Gäste, die selbst auslösen, bekommen ihre Nachricht immer sofort.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveGuestDelay))]
    private async Task SaveGuestDelayAsync()
    {
        if (_locationId is not { } locationId)
        {
            return;
        }

        if (GuestDelayMinutes is not { } minutes || minutes != decimal.Truncate(minutes) || minutes is < 0 or > MaxDelayMinutes)
        {
            GuestDelayError = DelayError;
            return;
        }

        var result = await api.PutAsync<GuestMessageDelayDto>($"api/locations/{locationId}/guest-message-delay", new GuestMessageDelayDto((int)minutes)).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            GuestDelayError = result.Error ?? "Die Wartezeit wurde nicht gespeichert. Bitte noch einmal versuchen.";
            return;
        }

        _savedDelay = saved.Minutes;
        GuestDelayError = null;
        GuestDelayMinutes = saved.Minutes;
        SaveGuestDelayCommand.NotifyCanExecuteChanged();
        ToastRequested?.Invoke("Wartezeit gespeichert");
    }
}

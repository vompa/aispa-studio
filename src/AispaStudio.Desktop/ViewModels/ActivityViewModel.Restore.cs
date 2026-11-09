using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „Stornierung zurücknehmen“ im Seitenpanel eines Stornos (Aufgabe E): derselbe Ablauf wie im Termin-Panel (<see cref="AppointmentRestorer"/>). Ist die Zeit frei, steht der
/// Termin danach wieder im Kalender; sonst übergibt „Neue Zeit wählen“ den Wunsch an die Shell, die den Kalender mit der Verschieben-Karte öffnet.
/// </summary>
public sealed partial class ActivityViewModel
{
    /// <summary>„Neue Zeit wählen“ in der Rückfrage: die Shell wechselt in den Kalender auf den nächsten freien Tag und öffnet den stornierten Termin mit den Vorschlägen.</summary>
    public Func<NewTimeRequest, Task>? NewTimeRequested { get; set; }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        if (PanelEntry is not { Kind: ActivityKind.Cancelled, AppointmentId: { } id } || !CanRestore)
        {
            return;
        }

        PanelError = null;
        var appointment = await _api.GetAsync<AppointmentDto>($"api/appointments/{id}").ConfigureAwait(true);
        if (appointment.Value is not { } found)
        {
            PanelError = appointment.Error ?? "Der Termin konnte nicht geladen werden.";
            return;
        }

        var zone = Zone();
        var restorer = new AppointmentRestorer(_api, AskAsync, zone, ReportedStale);
        var shortDate = DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(found.Start, zone)));
        var outcome = await restorer.AskAsync(found, found.CustomerName, shortDate, DayLayout.TimeText(found.Start, found.End, zone)).ConfigureAwait(true);
        switch (outcome.Step)
        {
            case RestoreStep.Restored:
                var restored = outcome.Restored!;
                ShowToast($"Termin wiederhergestellt: {restored.CustomerName}, {DayLayout.WhenText(restored.Start, zone, Today())}");
                await ReloadAsync().ConfigureAwait(true);
                break;
            case RestoreStep.NewTimeWanted when NewTimeRequested is { } request:
                await request(new NewTimeRequest(found, outcome.SendCorrection, outcome.Rejection)).ConfigureAwait(true);
                break;
            case RestoreStep.Failed:
                PanelError = outcome.Error ?? "Die Stornierung ließ sich nicht zurücknehmen.";
                break;
        }
    }

    /// <summary>Der Termin hat sich inzwischen geändert: die Meldung des Servers steht als Fehler im Panel.</summary>
    private bool ReportedStale(ApiResult<AppointmentDto> result)
    {
        if (result.Rejection is not { Current: not null } rejection)
        {
            return false;
        }

        PanelError = rejection.Message;
        return true;
    }
}

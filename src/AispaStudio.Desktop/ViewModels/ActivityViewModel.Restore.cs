using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „Stornierung zurücknehmen“ im Seitenpanel eines Stornos: dieselbe Rückfrage wie im Termin-Panel (<see cref="AppointmentViewModel.RestoreText"/>), dann <c>POST restore</c>.
/// Antwortet der Server mit einer Ablehnung (Zeit belegt, Vergangenheit), steht seine Meldung als Fehler im Panel; den Weg zu einer neuen Zeit baut die Aufgabe E auf diesem Ablauf.
/// </summary>
public sealed partial class ActivityViewModel
{
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

        var info = await _api.GetAsync<AppointmentRestoration>($"api/appointments/{id}/restoration").ConfigureAwait(true);
        if (info.Value is not { } restoration)
        {
            PanelError = info.Error ?? "Die Stornierung ließ sich nicht prüfen.";
            return;
        }

        var zone = Zone();
        var shortDate = DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(found.Start, zone)));
        var sent = restoration.Notice == CancellationNoticeState.Sent;
        var answer = await AskAsync(new ConfirmRequest("Stornierung zurücknehmen?",
            AppointmentViewModel.RestoreText(restoration, found.CustomerName, shortDate, DayLayout.TimeText(found.Start, found.End, zone)),
            sent ? "Mit Korrektur zurücknehmen" : "Zurücknehmen", IsDestructive: false, AlternativeText: sent ? "Ohne Korrektur" : null)).ConfigureAwait(true);
        if (answer.Choice == ConfirmChoice.Cancelled)
        {
            return;
        }

        var result = await _api.PostAsync<AppointmentDto>($"api/appointments/{id}/restore",
            new RestoreAppointmentRequest(null, sent && answer.Choice == ConfirmChoice.Confirmed, found.Version)).ConfigureAwait(true);
        if (result.Value is not { } restored)
        {
            PanelError = result.Rejection?.Message ?? result.Error ?? "Die Stornierung ließ sich nicht zurücknehmen.";
            return;
        }

        var today = DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), zone));
        ShowToast($"Termin wiederhergestellt: {restored.CustomerName}, {DayLayout.WhenText(restored.Start, zone, today)}");
        await ReloadAsync().ConfigureAwait(true);
    }
}

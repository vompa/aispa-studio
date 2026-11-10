using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Der Wunsch, eine Stornierung zu einer neuen Zeit zurückzunehmen: der stornierte Termin, ob der Gast eine Korrektur der Absage bekommt und die Belegungen,
/// die der Server meldete (leer bei einem vergangenen Termin). Die Seite „Verlauf“ reicht ihn an den Kalender weiter.
/// </summary>
public sealed record NewTimeRequest(AppointmentDto Appointment, bool SendCorrection, BookingRejected? Conflict);

/// <summary>Wie eine Rücknahme ausging, siehe <see cref="AppointmentRestorer"/>.</summary>
internal enum RestoreStep
{
    /// <summary>Der Termin ist wieder gebucht (<see cref="RestoreOutcome.Restored"/>).</summary>
    Restored,

    /// <summary>Das Team hat in einer Rückfrage abgebrochen; es ändert sich nichts.</summary>
    Declined,

    /// <summary>Die Stornierung lässt sich nur zu einer neuen Zeit zurücknehmen, und das Team will eine wählen.</summary>
    NewTimeWanted,

    /// <summary>Es ging nicht; <see cref="RestoreOutcome.Error"/> nennt den Grund, falls der Server einen nannte.</summary>
    Failed,

    /// <summary>Der Termin hat sich inzwischen geändert; die Meldung hat der Aufrufer schon gezeigt.</summary>
    Stale,
}

/// <param name="Restored">Der wiederhergestellte Termin.</param>
/// <param name="Error">Der Fehler des Servers.</param>
/// <param name="Rejection">Die Belegungen, die eine neue Zeit nötig machen.</param>
/// <param name="SendCorrection">Ob der Gast eine Korrektur der Absage bekommt (gilt auch für die neue Zeit).</param>
internal sealed record RestoreOutcome(RestoreStep Step, AppointmentDto? Restored = null, string? Error = null, BookingRejected? Rejection = null, bool SendCorrection = false);

/// <summary>
/// „Stornierung zurücknehmen“ für Termin-Panel und Seite „Verlauf“ (Aufgabe E): holt, was zu beachten ist (<c>GET restoration</c>), entscheidet, ob die alte Zeit noch geht
/// oder eine neue nötig ist (Vergangenheit, Belegung), stellt die Rückfragen und schickt <c>POST restore</c>. Was danach auf dem Bildschirm geschieht, entscheidet der Aufrufer.
/// </summary>
/// <param name="ask">Stellt eine Rückfrage (der Dialog des Aufrufers).</param>
/// <param name="handledStale">Zeigt eine Ablehnung wegen eines veralteten Stands und meldet, ob sie damit erledigt ist.</param>
internal sealed class AppointmentRestorer(IWorkstationApi api, Func<ConfirmRequest, Task<ConfirmResult>> ask, TimeZoneInfo zone, Func<ApiResult<AppointmentDto>, bool> handledStale)
{
    /// <summary>
    /// Prüft die Rücknahme und fragt: ist die Zeit frei, „Stornierung zurücknehmen?“ mit den Hinweisen zu Absage und Anzahlung und dann <c>restore</c>;
    /// liegt der Termin in der Vergangenheit oder ist die Zeit belegt, „Zu neuer Zeit zurücknehmen?“ mit dem Grund, ohne <c>restore</c>.
    /// </summary>
    public async Task<RestoreOutcome> AskAsync(AppointmentDto appointment, string guest, string shortDate, string time)
    {
        var info = await api.GetAsync<AppointmentRestoration>($"api/appointments/{appointment.Id}/restoration").ConfigureAwait(true);
        if (info.Value is not { } restoration)
        {
            return new RestoreOutcome(RestoreStep.Failed, Error: info.Error);
        }

        var sent = restoration.Notice == CancellationNoticeState.Sent;
        if (restoration.InPast || restoration.Conflict is not null)
        {
            var reason = restoration.InPast ? "Der Termin liegt in der Vergangenheit." : ConflictReason(restoration.Conflict, appointment);
            var wish = await ask(new ConfirmRequest("Zu neuer Zeit zurücknehmen?", NewTimeText(reason, restoration), "Neue Zeit wählen", IsDestructive: false)).ConfigureAwait(true);
            return wish.IsConfirmed
                ? new RestoreOutcome(RestoreStep.NewTimeWanted, Rejection: restoration.Conflict, SendCorrection: sent)
                : new RestoreOutcome(RestoreStep.Declined);
        }

        var answer = await ask(new ConfirmRequest("Stornierung zurücknehmen?", TextOf(restoration, guest, shortDate, time), sent ? "Mit Korrektur zurücknehmen" : "Zurücknehmen",
            IsDestructive: false, AlternativeText: sent ? "Ohne Korrektur" : null)).ConfigureAwait(true);
        return answer.Choice == ConfirmChoice.Cancelled
            ? new RestoreOutcome(RestoreStep.Declined)
            : await RestoreAtAsync(appointment, null, sent && answer.Choice == ConfirmChoice.Confirmed).ConfigureAwait(true);
    }

    /// <summary>
    /// Schickt <c>restore</c> (mit <paramref name="start"/> zu einer neuen Zeit). Lehnt der Server wegen Zeit, Person oder Raum ab, fragt „Zu anderer Zeit zurücknehmen?“;
    /// auch dann ist ein Wunsch nach einer neuen Zeit das Ergebnis, mit den Belegungen.
    /// </summary>
    public async Task<RestoreOutcome> RestoreAtAsync(AppointmentDto appointment, DateTimeOffset? start, bool sendCorrection)
    {
        var result = await api.PostAsync<AppointmentDto>($"api/appointments/{appointment.Id}/restore", new RestoreAppointmentRequest(start, sendCorrection, appointment.Version))
            .ConfigureAwait(true);
        if (result.Value is { } restored)
        {
            return new RestoreOutcome(RestoreStep.Restored, restored, SendCorrection: sendCorrection);
        }

        if (handledStale(result))
        {
            return new RestoreOutcome(RestoreStep.Stale);
        }

        if (result.Rejection is not { } rejection)
        {
            return new RestoreOutcome(RestoreStep.Failed, Error: result.Error);
        }

        var notice = ConflictNotice.From(rejection, zone, DayOf(appointment), new ConflictLinks(null));
        var reason = notice?.Summary ?? result.Error ?? rejection.Message;
        var answer = await ask(new ConfirmRequest("Zu anderer Zeit zurücknehmen?",
            $"{reason} Die Stornierung lässt sich nur zu einer anderen Zeit zurücknehmen.", "Verschieben …", IsDestructive: false)).ConfigureAwait(true);
        return answer.IsConfirmed
            ? new RestoreOutcome(RestoreStep.NewTimeWanted, Rejection: rejection, SendCorrection: sendCorrection)
            : new RestoreOutcome(RestoreStep.Declined, Rejection: rejection, SendCorrection: sendCorrection);
    }

    /// <summary>Der Text der Rückfrage „Stornierung zurücknehmen?“ bei freier Zeit.</summary>
    internal static string TextOf(AppointmentRestoration restoration, string guest, string shortDate, string time) =>
        NoticeText(restoration, $"Der Termin von {guest} am {shortDate}, {time}, ist danach wieder gebucht. Das Programm prüft zuerst, ob Zeit, Person und Raum noch frei sind.",
            " Der Gast hat die Absage schon bekommen. Soll er eine Korrektur erhalten?");

    /// <summary>Der Text der Rückfrage „Zu neuer Zeit zurücknehmen?“: der Grund, dann die Hinweise zu Absage und Anzahlung. Mit neuer Zeit bekommt der Gast die Korrektur ohne Rückfrage.</summary>
    private static string NewTimeText(string reason, AppointmentRestoration restoration) =>
        NoticeText(restoration, $"{reason} Die Stornierung lässt sich nur zu einer neuen Zeit zurücknehmen.",
            " Der Gast hat die Absage schon bekommen und erhält mit der neuen Zeit eine Korrektur.");

    private static string NoticeText(AppointmentRestoration restoration, string text, string sentText)
    {
        text += restoration.Notice switch
        {
            CancellationNoticeState.Waiting => " Die Absage an den Gast ist noch nicht raus und wird nicht mehr gesendet.",
            CancellationNoticeState.Sent => sentText,
            _ => "",
        };
        return restoration.DepositReturned ? text + " Die Anzahlung ging mit der Absage zurück; sie ist an der Kassa neu zu verlangen." : text;
    }

    private string ConflictReason(BookingRejected? conflict, AppointmentDto appointment) =>
        ConflictNotice.From(conflict, zone, DayOf(appointment), new ConflictLinks(null)) is { } notice
            ? $"Die Zeit ist inzwischen belegt: {string.Join("; ", notice.Lines.Select(l => l.Text))}."
            : conflict?.Message ?? "Die Zeit ist inzwischen belegt.";

    private DateOnly DayOf(AppointmentDto appointment) => DateOnly.FromDateTime(DayLayout.Local(appointment.Start, zone));
}

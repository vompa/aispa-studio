using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>„Stornierung zu neuer Zeit zurücknehmen“ aus der Seite „Verlauf“ (Aufgabe E): der Kalender zeigt den nächsten freien Tag und den stornierten Termin mit offener Verschieben-Karte.</summary>
public partial class CalendarViewModel
{
    /// <summary>So viele Vorschläge stehen unter der Verschieben-Karte.</summary>
    private const int RestoreProposals = 5;

    /// <summary>
    /// Sucht mit der bestehenden „Freien Zeit“ (Gast, Behandlung, Person und Raum des Termins) ab dem Tag des Termins, frühestens heute, wechselt auf den Tag des ersten
    /// Vorschlags (ohne Vorschlag bleibt der Suchtag) und öffnet das Panel des stornierten Termins mit der Verschieben-Karte und den Vorschlägen.
    /// </summary>
    public async Task ShowRestoreAtNewTimeAsync(NewTimeRequest request)
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        var cancelled = request.Appointment;
        var now = _time.GetUtcNow();
        var today = DateOnly.FromDateTime(DayLayout.Local(now, _zone));
        var appointmentDay = DateOnly.FromDateTime(DayLayout.Local(cancelled.Start, _zone));
        var searchFrom = appointmentDay > today ? appointmentDay : today;
        var guest = (await _api.GetAsync<CustomerDto>($"api/customers/{cancelled.CustomerId}").ConfigureAwait(true)).Value;
        var search = new FreeTimeViewModel(_api, _resources, searchFrom, _zone, now, _ => Task.CompletedTask, () => { }, null,
            new FreeTimePrefill(cancelled.TreatmentId, cancelled.EmployeeId, cancelled.RoomId, guest));
        await search.Loading.ConfigureAwait(true);

        var proposals = search.Top(RestoreProposals);
        var target = proposals.Count > 0 ? DateOnly.FromDateTime(DayLayout.Local(proposals[0].Slot.Start, _zone)) : searchFrom;
        if (target != Date)
        {
            Date = target;
            await LoadAsync().ConfigureAwait(true);
        }

        OpenAppointment(cancelled);
        Selected?.ProposeRestoreTime(request, proposals);
    }
}

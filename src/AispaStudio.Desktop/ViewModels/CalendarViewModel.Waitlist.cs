using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Warteliste im Kalender: je Wunsch „Frei: Do. 23.10. 17:00“, „Freie Zeiten zeigen“ (Suche mit Behandlung, Gast, Zeitraum und Tageszeit vorbelegt),
/// „Buchen“ (die freie Zeit gleich für den Gast; der Server nimmt ihn dann selbst von der Liste) und „Anfrage abschließen“ mit Grund.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>So viele Einträge suchen beim Öffnen der Liste ihre freie Zeit; die weiteren zeigen sie über „Freie Zeiten zeigen“.</summary>
    private const int MaxFreeLookups = 20;

    private WaitlistRow WaitlistRowOf(WaitlistStaffEntry entry) =>
        new(entry, new WaitlistActions(ShowWaitlistFreeAsync, BookWaitlistAsync, CustomerRequested, CloseWaitlistEntryAsync));

    /// <summary>Ab wann der Wunsch gilt: sein Beginn, frühestens heute (vergangene Tage haben keine freie Zeit).</summary>
    private DateOnly WishFrom(WaitlistRow row) =>
        DateOnly.FromDateTime(_time.GetLocalNow().DateTime) is var today && row.Entry.From < today ? today : row.Entry.From;

    /// <summary>„Freie Zeiten zeigen“: der Wunsch ab heute (oder ab seinem Beginn), nur im Zeitraum und zur Tageszeit, mit dem Gast.</summary>
    private async Task ShowWaitlistFreeAsync(WaitlistRow row)
    {
        var customer = await _api.GetAsync<CustomerDto>($"api/customers/{row.Entry.CustomerId}").ConfigureAwait(true);
        await ShowFreeTimeAsync(new FreeTimePrefill(row.Entry.TreatmentId, Guest: customer.Value, Part: row.Entry.DayPart, Until: row.Entry.Until), WishFrom(row))
            .ConfigureAwait(true);
    }

    /// <summary>„Buchen“: die freie Zeit gleich für den Gast; gebucht, verlässt der Eintrag die Liste (der Server schließt ihn bei der Buchung selbst).</summary>
    private async Task BookWaitlistAsync(WaitlistRow row)
    {
        if (row.FreeSlot is not { } slot || _resources?.Treatments.FirstOrDefault(t => t.Id == row.Entry.TreatmentId) is not { } treatment)
        {
            return;
        }

        var customer = await _api.GetAsync<CustomerDto>($"api/customers/{row.Entry.CustomerId}").ConfigureAwait(true);
        if (customer.Value is not { } guest)
        {
            ShowProblem("Gast nicht gefunden", customer.Error, action: () => _ = BookWaitlistAsync(row));
            return;
        }

        if (await TryBookSlotAsync(slot, treatment, guest, direct: true).ConfigureAwait(true))
        {
            RemoveFromWaitlist(row);
        }
    }

    private async Task CloseWaitlistEntryAsync(WaitlistRow row, WaitlistCloseReason reason)
    {
        var result = await _api.PostAsync<WaitlistEntryDto>($"api/waitlist/{row.Entry.Id}/close", new WaitlistCloseInput(reason)).ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Nicht von der Warteliste genommen", result.Error, action: () => _ = CloseWaitlistEntryAsync(row, reason));
            return;
        }

        RemoveFromWaitlist(row);
        ShowToast(reason == WaitlistCloseReason.Arranged
            ? $"{row.Name} von der Warteliste, Termin vereinbart"
            : $"{row.Name} von der Warteliste genommen");
    }

    private void RemoveFromWaitlist(WaitlistRow row)
    {
        Waitlist = [.. Waitlist.Where(r => r.Entry.Id != row.Entry.Id)];
        if (WaitlistPanel is not null)
        {
            WaitlistPanel = NewWaitlistPanel();
        }
    }

    /// <summary>Sucht je Wunsch die erste freie Zeit im Zeitraum und zur Tageszeit und trägt sie als „Frei: …“ ein; ohne Verbindung oder ohne Treffer bleibt der Eintrag ohne.</summary>
    private async Task LoadWaitlistFreeAsync(IReadOnlyList<WaitlistRow> rows)
    {
        if (Location is not { } location || _resources is null)
        {
            return;
        }

        var now = _time.GetUtcNow();
        await Task.WhenAll(rows.Take(MaxFreeLookups).Select(async row =>
        {
            var from = WishFrom(row);
            var span = Math.Clamp(row.Entry.Until.DayNumber - from.DayNumber + 1, 1, FreeTimeViewModel.SearchDays);
            var (slots, _) = await FreeTimeFinder.FindAsync(_api, location.Id, row.Entry.TreatmentId, from, span, $"&customerId={row.Entry.CustomerId}", 1,
                s => s.Start >= now && DayPartChoice.Contains(row.Entry.DayPart, DayLayout.Local(s.Start, _zone).Hour),
                start => DateOnly.FromDateTime(DayLayout.Local(start, _zone)), () => Location?.Id == location.Id).ConfigureAwait(true);
            if (slots is [var first, ..])
            {
                var who = _resources.Employees.FirstOrDefault(e => e.Id == first.EmployeeId)?.InHouseName();
                row.FreeLabel = $"{DayLayout.ShortDayTime(first.Start, _zone)}{(who is null ? "" : $" bei {who}")}";
                row.FreeSlot = first;
            }
        })).ConfigureAwait(true);
    }
}

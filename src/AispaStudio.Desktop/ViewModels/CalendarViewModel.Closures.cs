using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Schließzeiten im Kalender (ADR 0073): das Schließband über allen Spalten, der Chip „Geschlossen · Betriebsurlaub“ in der Kopfzeile, die Meldung „n Termine in der Schließzeit“ mit
/// dem Knopf „Termine absagen …“ und die Rückfrage dazu. Der Kalender zeichnet nur, was der Server liefert; abgesagt und gesendet wird erst nach dem Klick in der Rückfrage.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Die Schließbänder des gezeigten Tages (leer in der 7-Tage-Ansicht einer Person).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ClosureBandItem> ClosureBands { get; private set; } = [];

    /// <summary>Der Chip der Kopfzeile; leer an Tagen ohne Schließzeit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClosureChip), nameof(ClosureChipLabel), nameof(ClosureChipTip), nameof(DayFactsText), nameof(HasDayFactsText))]
    public partial ClosureChipInfo? ClosureChip { get; private set; }

    public bool HasClosureChip => ClosureChip is not null;

    public string ClosureChipLabel => ClosureChip is null ? "" : IsCompactHeader ? ClosureChip.Compact : ClosureChip.Text;

    public string ClosureChipTip => ClosureChip?.Tip ?? "";

    /// <summary>Absagen brauchen die Rechte, Standorte zu verwalten und zu buchen (Inhaber, Manager, Standortleitung).</summary>
    public bool CanCancelClosure => _user.Rights.Contains(AccessRight.ManageLocation) && _user.Rights.Contains(AccessRight.Book);

    /// <summary>Band und Chip aus dem Tag nachführen; nach jedem Aufbau der Spalten.</summary>
    private void RefreshClosures(CalendarDay day, DateTimeOffset dayStart, double dayHeight, CalendarScale scale)
    {
        var shown = Filter is null ? day.Closures : null;
        ClosureBands = DayLayout.ClosureBands(shown, dayStart, dayHeight, scale, _zone);
        ClosureChip = DayLayout.ClosureChip(shown, _zone);
    }

    /// <summary>Klick auf den Chip: die Schließzeiten in der Verwaltung; wer sie nicht verwalten darf, sieht den Hinweis.</summary>
    [RelayCommand]
    private void OpenClosures()
    {
        if (AdminRequested is not null && _user.Rights.Contains(AccessRight.ManageLocation))
        {
            AdminRequested("masterData", "closures", null);
        }
    }

    /// <summary>
    /// Die Meldungen „n Termine in der Schließzeit“: je Schließzeit des Tages und je fällige mit offenen Terminen oder Wartelisten-Einträgen, gleich wie fern sie beginnt (Ä49). Eine Warnung,
    /// solange die Schließzeit später als in 14 Tagen beginnt, ein Fehler, wenn sie läuft oder in 14 Tagen beginnt (<see cref="ClosureBand.Urgent"/>, der Server rechnet es). Die Meldung bleibt,
    /// bis die Termine abgesagt und die Einträge benachrichtigt sind; sie sagt nichts von selbst ab (ADR 0073).
    /// </summary>
    private void AddClosures(MessageState state, List<MessageItem> all)
    {
        var bands = (_day?.Closures ?? []).Concat(_day?.ClosuresDue ?? []).Where(b => b.OpenAppointments > 0 || b.WaitlistEntries > 0).GroupBy(b => b.Id).Select(g => g.First()).OrderBy(b => b.From);
        foreach (var band in bands)
        {
            var what = ClosureWhat(band.OpenAppointments, band.WaitlistEntries);
            var period = DayLayout.ClosurePeriod(band.From, band.Until, _zone);
            var key = $"closure:{band.Id}";
            var lines = new List<string> { band.Text };
            if (!string.IsNullOrWhiteSpace(band.InternalReason))
            {
                lines.Add($"Grund: {band.InternalReason}");
            }

            all.Add(Message(state, key, MessageKind.Closure, band.Urgent ? MessageSeverity.Error : MessageSeverity.Warning, "Schließzeit", $"{what} in der Schließzeit ({band.Title}, {period}).",
                $"{what} in der Schließzeit", trigger: $"{band.OpenAppointments}/{band.WaitlistEntries}", lines: lines,
                actionLabel: CanCancelClosure ? "Termine absagen …" : null, action: CanCancelClosure ? () => _ = CancelClosureAppointmentsAsync(band.Id) : null));
        }
    }

    /// <summary>„3 Termine“, „1 Eintrag der Warteliste“ oder „2 Termine und 1 Eintrag der Warteliste“.</summary>
    internal static string ClosureWhat(int appointments, int waitlist)
    {
        var termine = appointments == 1 ? "1 Termin" : $"{appointments} Termine";
        var eintraege = waitlist == 1 ? "1 Eintrag der Warteliste" : $"{waitlist} Einträge der Warteliste";
        return (appointments, waitlist) switch
        {
            (_, 0) => termine,
            (0, _) => eintraege,
            _ => $"{termine} und {eintraege}",
        };
    }

    /// <summary>
    /// „Termine absagen …“ aus der Meldung: holt die offenen Termine und den Textvorschlag, fragt in einer Rückfrage nach (Auswahl, Text, Zahl der Nachrichten) und sagt erst nach dem Klick
    /// auf den roten Knopf ab. Danach lädt der Kalender neu, damit Termine, Band und Meldung stimmen.
    /// </summary>
    public async Task CancelClosureAppointmentsAsync(Guid closureId)
    {
        if (!CanCancelClosure || Location is not { } location || Dialogs is not { } dialogs)
        {
            return;
        }

        var path = $"api/locations/{location.Id}/closures/{closureId}";
        var preview = await _api.GetAsync<ClosureCancellationPreview>($"{path}/cancellation").ConfigureAwait(true);
        if (preview.Value is null)
        {
            ShowProblem("Termine nicht geladen", preview.Error, action: () => _ = CancelClosureAppointmentsAsync(closureId));
            return;
        }

        CancelClosureDialogViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done => dialog = new CancelClosureDialogViewModel(preview.Value, _zone, done, async (kind, text) =>
        {
            // Der bearbeitbare Text ist die deutsche Vorlage: die KI behält Deutsch (Ä46).
            var revised = await _api.PostAsync<ReviseClosureTextResult>($"{path}/cancellation/revise", new ReviseClosureTextRequest(text, kind, "de")).ConfigureAwait(true);
            return (revised.Value?.Text, revised.Value is null ? revised.Error : null);
        }, Location?.Currency)).ConfigureAwait(true);
        if (!answer.IsConfirmed || dialog is null)
        {
            return;
        }

        var request = new CancelClosureAppointmentsRequest([.. dialog.Selected.Select(r => r.Appointment.AppointmentId)], dialog.Subject.Trim(), dialog.Text.Trim(),
            dialog.OfferRebooking, dialog.AiWritten, [.. dialog.SelectedWaitlist.Select(r => r.Entry.EntryId)]);
        var result = await _api.PostAsync<CancelClosureAppointmentsResult>($"{path}/cancellation", request).ConfigureAwait(true);
        if (result.Value is null)
        {
            ShowProblem("Termine nicht abgesagt", result.Error, action: () => _ = CancelClosureAppointmentsAsync(closureId));
            return;
        }

        await LoadAsync().ConfigureAwait(true);
        ShowToast(FitToast(CancelledText(result.Value)));
        if (result.Value.Failed > 0)
        {
            ShowProblem("Nicht alle Termine abgesagt", $"{result.Value.Failed} Termine ließen sich nicht absagen, weil sie sich inzwischen geändert haben. Sie stehen weiter in der Meldung.", isError: false);
        }

        if (result.Value.RefundsFailed > 0)
        {
            ShowProblem("Anzahlung nicht retourniert",
                $"Bei {(result.Value.RefundsFailed == 1 ? "1 Termin" : $"{result.Value.RefundsFailed} Terminen")} ließ sich die Anzahlung nicht über die Kassa retournieren (z. B. Kassa außer Betrieb). "
                + "Die Nachricht nennt keine Rückzahlung. Bitte an der Kassa erstatten.", isError: false);
        }
    }

    /// <summary>
    /// „2 Termine abgesagt, 2 Nachrichten, 1 Wartelisten-Eintrag benachrichtigt, 2 Anzahlungen retourniert“; Gäste ohne Kontakt sind abgesagt, aber nicht benachrichtigt. Die Nachrichten
    /// zählen Termine und Einträge der Warteliste zusammen.
    /// </summary>
    internal static string CancelledText(CancelClosureAppointmentsResult result)
    {
        var parts = new List<string>();
        if (result.Cancelled > 0)
        {
            parts.Add(result.Cancelled == 1 ? "1 Termin abgesagt" : $"{result.Cancelled} Termine abgesagt");
        }

        if (result.Queued > 0)
        {
            parts.Add(result.Queued == 1 ? "1 Nachricht" : $"{result.Queued} Nachrichten");
        }

        if (result.WaitlistNotified > 0)
        {
            parts.Add(result.WaitlistNotified == 1 ? "1 Wartelisten-Eintrag benachrichtigt" : $"{result.WaitlistNotified} Wartelisten-Einträge benachrichtigt");
        }

        if (result.Refunded > 0)
        {
            parts.Add(result.Refunded == 1 ? "1 Anzahlung retourniert" : $"{result.Refunded} Anzahlungen retourniert");
        }

        if (result.WithoutContact > 0)
        {
            parts.Add($"{result.WithoutContact} ohne Kontakt");
        }

        return string.Join(", ", parts);
    }
}

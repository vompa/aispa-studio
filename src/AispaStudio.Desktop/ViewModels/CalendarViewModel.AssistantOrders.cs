using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Aufträge aus „KI und Hilfe“ (Ä319, ADR 0072 Nachtrag Slice 147): „buche …“ öffnet „Neuer Termin“, „verschiebe …“ das Verschieben im Termin, „sag … ab“ die
/// Rückfrage zum Stornieren mit der Gebühr, „schick … den Bogen“ den Termin mit dem Abschnitt Anamnese, „biete … an“ die freien Zeiten zum Wunsch der Warteliste.
/// Die Masken sind vorausgefüllt und tragen den Hinweis auf den Vorschlag der KI; gespeichert wird erst mit ihrem Knopf. Was der Auftrag nicht nennt, bleibt leer.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Hinweis in der Maske, die ein Auftrag geöffnet hat.</summary>
    internal const string AssistantBookingNote = "Vorschlag des KI-Assistenten, bitte prüfen. Gebucht wird erst mit „Termin buchen“.";

    internal const string AssistantAppointmentNote = "Vom KI-Assistenten geöffnet, bitte prüfen. Geändert wird erst mit dem Knopf im Termin.";

    public async Task OpenAssistantOrderAsync(AssistantSuggestion order)
    {
        switch (order.Kind)
        {
            case AssistantSuggestionKind.NewAppointment:
                await OpenAssistantBookingAsync(order).ConfigureAwait(true);
                break;
            case AssistantSuggestionKind.OfferWaitlist:
                await OfferFromWaitlistAsync(order).ConfigureAwait(true);
                break;
            case AssistantSuggestionKind.MoveAppointment or AssistantSuggestionKind.CancelAppointment or AssistantSuggestionKind.SendQuestionnaire:
                await OpenAssistantAppointmentAsync(order).ConfigureAwait(true);
                break;
        }
    }

    /// <summary>„Neuer Termin“ am genannten Tag bei der genannten Person (sonst in der ersten Spalte) mit Gast, Behandlung und Beginn; ohne Uhrzeit bleibt „Beginn“ leer.</summary>
    private async Task OpenAssistantBookingAsync(AssistantSuggestion order)
    {
        var day = order.Start is { } start ? DateOnly.FromDateTime(DayLayout.Local(start, _zone)) : order.Day;
        if (day is { } wanted && wanted != Date)
        {
            Date = wanted;
            await LoadAsync().ConfigureAwait(true);
        }

        var column = Columns.FirstOrDefault(c => !ByRoom && c.Id == order.EmployeeId) ?? Columns.FirstOrDefault(c => !c.IsArea);
        if (column is null)
        {
            return;
        }

        StartBooking(column, order.Start is { } at ? DayLayout.Top(_dayStart, at, Scale) : 0);
        if (Booking is not { } booking)
        {
            return;
        }

        booking.Treatment = order.TreatmentId is { } treatment ? booking.Treatments.FirstOrDefault(t => t.Id == treatment) : null;
        booking.StartText = order.Start is { } begin ? $"{DayLayout.Local(begin, _zone):HH:mm}" : "";
        booking.AssistantNote = AssistantBookingNote;
        if (order.CustomerId is { } customer && (await _api.GetAsync<CustomerDto>($"api/customers/{customer}").ConfigureAwait(true)).Value is { } guest)
        {
            booking.Customers.Insert(0, guest);
            booking.Customer = guest;
        }
    }

    /// <summary>
    /// Den Termin des Auftrags an seinem Tag öffnen, dann Verschieben oder die Rückfrage zum Stornieren; beim Bogen springt das Panel zum Abschnitt Anamnese
    /// (T-022). Einen Weg (Tablet, Papier, Foto) wählt die App nicht vor: der Auftrag nennt keinen, die Person entscheidet.
    /// </summary>
    private async Task OpenAssistantAppointmentAsync(AssistantSuggestion order)
    {
        if (order.AppointmentId is not { } id || order.Start is not { } start)
        {
            return;
        }

        if (_day?.Appointments.Any(a => a.Id == id) != true)
        {
            Filter = null;
            Date = DateOnly.FromDateTime(DayLayout.Local(start, _zone));
            await LoadAsync().ConfigureAwait(true);
        }

        if (_day?.Appointments.FirstOrDefault(a => a.Id == id) is not { } appointment)
        {
            ShowToast("Diesen Termin zeigt der Kalender am gewählten Standort nicht.");
            return;
        }

        OpenAppointment(appointment);
        if (Selected is not { } panel)
        {
            return;
        }

        panel.AssistantNote = AssistantAppointmentNote;
        if (order.Kind == AssistantSuggestionKind.MoveAppointment)
        {
            panel.ProposeMove(order.Day, order.NewStart);
        }
        else if (order.Kind == AssistantSuggestionKind.CancelAppointment)
        {
            await panel.AskCancelWithFeeAsync(order.Note).ConfigureAwait(true);
        }
        else if (order.Kind == AssistantSuggestionKind.SendQuestionnaire)
        {
            await panel.ShowAnamnesisAsync().ConfigureAwait(true);
        }
    }

    /// <summary>„Biete … an“: die freien Zeiten zum Wunsch, wie „Freie Zeiten zeigen“ in der Warteliste.</summary>
    private async Task OfferFromWaitlistAsync(AssistantSuggestion order)
    {
        if (Waitlist.FirstOrDefault(r => r.Entry.Id == order.TargetId) is { } row)
        {
            await ShowWaitlistFreeAsync(row).ConfigureAwait(true);
            return;
        }

        var guest = order.CustomerId is { } customer ? (await _api.GetAsync<CustomerDto>($"api/customers/{customer}").ConfigureAwait(true)).Value : null;
        await ShowFreeTimeAsync(new FreeTimePrefill(order.TreatmentId, Guest: guest), order.Day).ConfigureAwait(true);
    }
}

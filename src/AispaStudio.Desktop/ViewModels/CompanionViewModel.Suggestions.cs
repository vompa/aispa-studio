using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Knopf unter einer Hilfeantwort, der die beschriebene Funktion öffnet.</summary>
public sealed record CompanionAction(string Label, string Tip, Func<Task> Run);

/// <summary>Vorschlag des Assistenten als Knopf; <c>Action</c> sagt, was ein Klick tut.</summary>
public sealed record CompanionSuggestion(AssistantSuggestion Suggestion, string Action)
{
    public string Label => Suggestion.Label;
}

/// <summary>
/// Vorschläge des Assistenten als Knöpfe: Kunde öffnen, Termin im Kalender zeigen, freie Zeit buchen. Der Assistent führt nichts
/// selbst aus; gebucht wird erst, wenn die Person einen Kunden wählt und bestätigt, über dieselbe Buchung wie im Kalender.
/// </summary>
public sealed partial class CompanionViewModel
{
    /// <summary>Kundenkarte öffnen (Shell: Bereich „Kunden“).</summary>
    public Func<Guid, Task>? CustomerRequested { get; set; }

    /// <summary>Termin im Kalender zeigen: Id und Beginn, damit der Kalender zum Tag springt.</summary>
    public Action<Guid, DateTimeOffset?>? AppointmentRequested { get; set; }

    /// <summary>„Alle Termine von …“: der Kalender zeigt sie über sieben Tage (Kunden-Filter).</summary>
    public Func<Guid, Task>? CustomerAppointmentsRequested { get; set; }

    /// <summary>„Freie Zeit suchen“ im Kalender, mit Behandlung, Person, Raum und Gast vorbelegt; zweiter Wert ist der Tag, ab dem gesucht wird.</summary>
    public Func<FreeTimePrefill, DateTimeOffset?, Task>? FreeTimeRequested { get; set; }

    /// <summary>„Wunsch eintippen“ im Kalender.</summary>
    public Action? RequestRequested { get; set; }

    /// <summary>Warteliste im Kalender.</summary>
    public Action? WaitlistRequested { get; set; }

    /// <summary>Gebuchter Termin für den Kalender.</summary>
    public Action<AppointmentDto>? Booked { get; set; }

    /// <summary>
    /// Auftrag „buche …“, „verschiebe …“, „sag … ab“, „schick … den Bogen“, „biete … an“ oder „offene Rechnung …“ (Ä319): die Shell öffnet die passende Maske
    /// vorausgefüllt; gespeichert wird erst mit dem Knopf der Maske.
    /// </summary>
    public Func<AssistantSuggestion, Task>? OrderRequested { get; set; }

    /// <summary>Was ein Klick auf den Auftrag öffnet (Tooltip des Knopfs); <c>null</c> für Vorschläge, die keine Aufträge sind.</summary>
    private static string? OrderAction(AssistantSuggestionKind kind) => kind switch
    {
        AssistantSuggestionKind.NewAppointment => "Öffnet „Neuer Termin“ vorausgefüllt; gebucht wird erst mit „Termin buchen“",
        AssistantSuggestionKind.MoveAppointment => "Öffnet „Verschieben“ im Termin; verschoben wird erst mit „Verschieben“",
        AssistantSuggestionKind.CancelAppointment => "Öffnet die Rückfrage zum Stornieren; storniert wird erst mit „Stornieren“",
        AssistantSuggestionKind.SendQuestionnaire => "Öffnet den Termin mit dem Abschnitt „Anamnese“",
        AssistantSuggestionKind.OfferWaitlist => "Zeigt die freien Zeiten zum Wunsch auf der Warteliste",
        AssistantSuggestionKind.OpenBill => "Öffnet die Rechnung in der Kassa",
        _ => null,
    };

    /// <summary>Buchungsrecht: ohne gibt es keine Buchungsknöpfe (der Server schlägt dann auch keine vor).</summary>
    public bool CanBook { get; init; }

    public IReadOnlyList<CompanionSuggestion> Suggestions => Answer?.Suggestions
        .Select(s => s.Kind switch
        {
            AssistantSuggestionKind.BookSlot when CanBook && s is { LocationId: not null, TreatmentId: not null, Start: not null } =>
                new CompanionSuggestion(s, "Buchen …"),
            AssistantSuggestionKind.OpenCustomer when s.CustomerId is not null && CustomerRequested is not null =>
                new CompanionSuggestion(s, "Kunde öffnen"),
            AssistantSuggestionKind.OpenAppointment when s.AppointmentId is not null && AppointmentRequested is not null =>
                new CompanionSuggestion(s, "Im Kalender zeigen"),
            AssistantSuggestionKind.ShowCustomerAppointments when s.CustomerId is not null && CustomerAppointmentsRequested is not null =>
                new CompanionSuggestion(s, "Alle Termine zeigen"),
            AssistantSuggestionKind.SearchFreeTime when CanBook && FreeTimeRequested is not null =>
                new CompanionSuggestion(s, "Suche öffnen"),
            _ when CanBook && OrderRequested is not null && OrderAction(s.Kind) is { } action => new CompanionSuggestion(s, action),
            _ => null,
        })
        .OfType<CompanionSuggestion>()
        .ToList() ?? [];

    /// <summary>Gewählte freie Zeit; darunter die Kundensuche und „Termin buchen“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBooking))]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial CompanionSuggestion? Pick { get; private set; }

    public bool IsBooking => Pick is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial CustomerSearchViewModel? Guest { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial bool IsBookingBusy { get; private set; }

    /// <summary>„Gebucht: …“ nach erfolgreicher Buchung.</summary>
    [ObservableProperty]
    public partial string? BookedText { get; private set; }

    [RelayCommand]
    private async Task UseSuggestionAsync(CompanionSuggestion item)
    {
        BookedText = null;
        var s = item.Suggestion;
        switch (s.Kind)
        {
            case AssistantSuggestionKind.BookSlot:
                var guest = new CustomerSearchViewModel(_api);
                guest.Changed += BookCommand.NotifyCanExecuteChanged;
                Guest = guest;
                Pick = item;
                break;
            case AssistantSuggestionKind.OpenCustomer when s.CustomerId is { } customer && CustomerRequested is { } open:
                await open(customer);
                break;
            case AssistantSuggestionKind.OpenAppointment when s.AppointmentId is { } appointment:
                AppointmentRequested?.Invoke(appointment, s.Start);
                break;
            case AssistantSuggestionKind.ShowCustomerAppointments when s.CustomerId is { } owner && CustomerAppointmentsRequested is { } show:
                await show(owner);
                break;
            case AssistantSuggestionKind.SearchFreeTime when FreeTimeRequested is { } search:
                await search(new FreeTimePrefill(s.TreatmentId, s.EmployeeId, s.RoomId), s.Start);
                break;
            case var kind when OrderAction(kind) is not null && OrderRequested is { } order:
                await order(s);
                break;
        }
    }

    /// <summary>
    /// Knopf unter einer Hilfeantwort, der die beschriebene Funktion gleich öffnet: „Jetzt suchen“ unter „Wie finde ich freie Zeiten?“.
    /// Die Hilfe zeigt den Weg, geöffnet wird erst nach dem Klick.
    /// </summary>
    public CompanionAction? HelpAction => FirstHelp?.Topic switch
    {
        "kalender-freie-zeit-suchen" when CanBook && FreeTimeRequested is { } search =>
            new CompanionAction("Jetzt suchen", "Öffnet „Freie Zeit suchen“ im Kalender", () => search(new FreeTimePrefill(), null)),
        "kalender-ki-anfrage" when CanBook && RequestRequested is { } request =>
            new CompanionAction("Wunsch eintippen", "Öffnet „Wunsch eintippen“ im Kalender", () =>
            {
                request();
                return Task.CompletedTask;
            }),
        "kalender-warteliste" when WaitlistRequested is { } waitlist =>
            new CompanionAction("Warteliste öffnen", "Öffnet die Warteliste im Kalender", () =>
            {
                waitlist();
                return Task.CompletedTask;
            }),
        _ => null,
    };

    [RelayCommand]
    private Task RunHelpActionAsync() => HelpAction?.Run() ?? Task.CompletedTask;

    [RelayCommand]
    private void CancelBooking()
    {
        Pick = null;
        Guest = null;
    }

    private bool CanBookNow() => Pick is not null && Guest?.Customer is not null && !IsBookingBusy;

    [RelayCommand(CanExecute = nameof(CanBookNow))]
    private async Task BookAsync()
    {
        if (Pick?.Suggestion is not { LocationId: { } location, TreatmentId: { } treatment, Start: { } start } slot || Guest?.Customer is not { } customer)
        {
            return;
        }

        var generation = _generation;
        IsBookingBusy = true;
        Error = null;
        try
        {
            // Bewusst nicht vormerken: ohne Verbindung kann die vorgeschlagene Zeit längst vergeben sein.
            var result = await _api.PostAsync<AppointmentDto>("api/appointments",
                new AppointmentRequest(location, treatment, customer.Id, start, slot.EmployeeId, null, null, null, ClientId: Guid.CreateVersion7()));
            if (result.Ok && result.Value is { } booked)
            {
                // Gebucht ist gebucht: der Kalender erfährt es auch, wenn das Panel inzwischen zurückgesetzt wurde.
                if (generation != _generation)
                {
                    Booked?.Invoke(booked);
                    return;
                }

                BookedText = $"Gebucht: {slot.Label} für {$"{customer.FirstName} {customer.LastName}".Trim()}.";
                Pick = null;
                Guest = null;
                Booked?.Invoke(booked);
            }
            else if (generation == _generation)
            {
                Error = result.Status == 0
                    ? "Ohne Verbindung wird nicht gebucht. Bitte im Kalender buchen oder später noch einmal fragen."
                    : result.Rejection is { StaffRule: true } rule
                        ? $"{rule.Message} Trotzdem buchen geht im Kalender."
                        : "Diese Zeit ist inzwischen vergeben. Bitte die Frage noch einmal stellen.";
            }
        }
        finally
        {
            if (generation == _generation)
            {
                IsBookingBusy = false;
            }
        }
    }
}

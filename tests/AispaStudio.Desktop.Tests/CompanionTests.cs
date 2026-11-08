using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Help;

namespace AispaStudio.Desktop.Tests;

/// <summary>KI und Hilfe rechts: Erklärung zur offenen Seite und der KI-Assistent, offen oder zu bleibt gemerkt.</summary>
public sealed class CompanionTests
{
    private static readonly AssistantAnswer Answer = new(AssistantAudience.Reception, "Heute stehen 5 Termine an.",
        [new AssistantFigure("Termine", "5", "4")], [], "Antwort einer KI. Zahlen aus Kalender und Kassa.", []);

    [Fact]
    public void Leiste_zeigt_die_Erklaerung_zur_Seite_und_merkt_sich_offen_oder_zu()
    {
        var store = new MemorySettingsStore();
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(store), "Test", () => Task.CompletedTask);

        Assert.False(shell.Companion.IsOpen);

        Assert.Equal("seite-kalender", shell.Companion.Topic?.Id);
        shell.SelectedPage = Assert.Single(shell.Pages, p => p.Title == "Kassa");
        Assert.Equal("seite-kassa", shell.Companion.Topic?.Id);
        Assert.NotEmpty(shell.Companion.Intro);

        shell.Companion.ToggleCommand.Execute(null);
        Assert.True(store.Load().CompanionOpen);
        var again = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(store), "Test", () => Task.CompletedTask);
        Assert.True(again.Companion.IsOpen);
        shell.Companion.CloseCommand.Execute(null);
        Assert.False(store.Load().CompanionOpen);
    }

    [Fact]
    public void Erklaerung_kommt_aus_dem_Seitenthema_der_Hilfe()
    {
        var page = new HelpTopic("seite-kassa", "Kassa-Seite", [new HelpParagraph([new HelpSpan("Hier wird kassiert.")]), new HelpHeading(2, "Mehr", "mehr")]);
        var companion = new CompanionViewModel(new StubApi().Client(), new MemorySettingsStore(), true, AssistantAudience.Reception,
            find: id => id == page.Id ? page : null);

        companion.ShowPage("kassa");

        Assert.Same(page, companion.Topic);
        Assert.Equal("Hier wird kassiert.", Assert.Single(companion.Intro).Spans[0].Text);

        // Seite ohne Thema: die Leiste bietet alle Hilfethemen an.
        companion.ShowPage("kunden-ohne-thema");
        Assert.False(companion.HasTopic);
        Assert.Empty(companion.Intro);
        Assert.Equal("im-netz", ShellViewModel.PageKey("Im Netz"));
        Assert.Equal("geraete", ShellViewModel.PageKey("Geräte"));
    }

    [Fact]
    public async Task Fragt_den_Assistenten_mit_dem_Standort_des_Kalenders()
    {
        var location = Guid.NewGuid();
        var stub = new StubApi().On("POST /api/assistant", Answer);
        var companion = new CompanionViewModel(stub.Client(), new MemorySettingsStore(), true, AssistantAudience.Reception, () => location);

        Assert.False(companion.AskCommand.CanExecute(null));
        companion.ShowPage("kassa");
        await companion.AskExampleCommand.ExecuteAsync("Was steht heute an?");

        Assert.Equal("Was steht heute an?", companion.Asked);
        Assert.Equal("Heute stehen 5 Termine an.", companion.Answer?.Text);
        Assert.Equal("", companion.Question);
        var sent = JsonSerializer.Deserialize<AssistantQuestion>(Assert.Single(stub.Calls, c => c.Route == "POST /api/assistant").Body!, ApiClient.Json);
        Assert.Equal(new AssistantQuestion("Was steht heute an?", location, "kassa", "windows-app"), sent);
    }

    [Fact]
    public async Task Ohne_KI_bleibt_nur_die_Erklaerung_und_Fehler_sagen_es_deutlich()
    {
        var off = new CompanionViewModel(new StubApi().Client(), new MemorySettingsStore(), false, AssistantAudience.Therapist);
        off.Question = "Was steht heute an?";
        Assert.False(off.AskCommand.CanExecute(null));
        Assert.Contains("Wie hake ich die Vorbereitung ab?", off.Examples);
        Assert.DoesNotContain(off.Examples, e => e.Contains("stempel", StringComparison.OrdinalIgnoreCase));

        var broken = new CompanionViewModel(new StubApi().Client(), new MemorySettingsStore(), true, AssistantAudience.Management);
        broken.Question = "Umsatz letzte Woche?";
        await broken.AskCommand.ExecuteAsync(null);
        Assert.Null(broken.Answer);
        Assert.Equal("Der Assistent konnte die Frage gerade nicht beantworten.", broken.Error);
        Assert.Equal("Umsatz letzte Woche?", broken.Question);
    }

    private static readonly Guid Location = Guid.NewGuid();
    private static readonly Guid Treatment = Guid.NewGuid();
    private static readonly Guid Lena = Guid.NewGuid();
    private static readonly DateTimeOffset Slot = new(2026, 10, 21, 13, 0, 0, TimeSpan.Zero);

    private static AssistantAnswer WithSuggestions(params AssistantSuggestion[] suggestions) =>
        new(AssistantAudience.Reception, "Morgen ist um 15:00 eine Massage frei.", [], suggestions, "Antwort einer KI.", []);

    private static AssistantSuggestion Free() =>
        new(AssistantSuggestionKind.BookSlot, "Mi., 15:00 Klassische Massage bei Lena", Location, Treatment, Lena, null, null, Slot);

    [Fact]
    public async Task Vorschlaege_sind_Knoepfe_und_oeffnen_Kunde_oder_Termin()
    {
        var customer = Guid.NewGuid();
        var appointment = Guid.NewGuid();
        var stub = new StubApi().On("POST /api/assistant", WithSuggestions(Free(),
            new AssistantSuggestion(AssistantSuggestionKind.OpenCustomer, "Maria Brunner", null, null, null, customer, null, null),
            new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, "Termin morgen 10:00", null, null, null, null, appointment, Slot)));
        Guid? opened = null;
        (Guid Id, DateTimeOffset? Start)? shown = null;
        var companion = new CompanionViewModel(stub.Client(), new MemorySettingsStore(), true, AssistantAudience.Therapist)
        {
            CustomerRequested = id =>
            {
                opened = id;
                return Task.CompletedTask;
            },
            AppointmentRequested = (id, start) => shown = (id, start),
        };

        await companion.AskExampleCommand.ExecuteAsync("Wer ist Frau Brunner?");

        // Ohne Buchungsrecht kein Buchungsknopf.
        Assert.Equal(["Kunde öffnen", "Im Kalender zeigen"], companion.Suggestions.Select(s => s.Action));
        await companion.UseSuggestionCommand.ExecuteAsync(companion.Suggestions[0]);
        Assert.Equal(customer, opened);
        await companion.UseSuggestionCommand.ExecuteAsync(companion.Suggestions[1]);
        Assert.Equal((appointment, (DateTimeOffset?)Slot), shown);
    }

    [Fact]
    public async Task Freie_Zeit_bucht_erst_nach_Wahl_des_Kunden()
    {
        var maria = new CustomerDto(Guid.NewGuid(), "Maria", "Brunner", "maria@example.at", "+43 660 1234567", null, null, null);
        var booked = new List<AppointmentDto>();
        var stub = new StubApi()
            .On("POST /api/assistant", WithSuggestions(Free()))
            .On("GET /api/customers?search=br", new List<CustomerDto> { maria })
            .On("POST /api/appointments", new BookingRejected("Zu dieser Zeit ist nicht alles frei.", []), HttpStatusCode.Conflict);
        var companion = new CompanionViewModel(stub.Client(), new MemorySettingsStore(), true, AssistantAudience.Reception)
        {
            CanBook = true,
            Booked = booked.Add,
        };
        await companion.AskExampleCommand.ExecuteAsync("Wann ist morgen eine Massage frei?");

        var slot = Assert.Single(companion.Suggestions);
        Assert.Equal("Buchen …", slot.Action);
        await companion.UseSuggestionCommand.ExecuteAsync(slot);
        Assert.True(companion.IsBooking);
        Assert.False(companion.BookCommand.CanExecute(null));

        await companion.Guest!.SearchAsync("br");
        companion.Guest.Customer = Assert.Single(companion.Guest.Results);
        Assert.True(companion.BookCommand.CanExecute(null));

        // Inzwischen vergeben: Hinweis, Auswahl bleibt.
        await companion.BookCommand.ExecuteAsync(null);
        Assert.Equal("Diese Zeit ist inzwischen vergeben. Bitte die Frage noch einmal stellen.", companion.Error);
        Assert.True(companion.IsBooking);
        Assert.Empty(booked);

        // Sonderregelung (R05 Slice 111): nennt die Regel, nicht „vergeben“.
        stub.On("POST /api/appointments", new BookingRejected("Maria hat eine Sonderregelung.", [], StaffRule: true), HttpStatusCode.Conflict);
        await companion.BookCommand.ExecuteAsync(null);
        Assert.Equal("Maria hat eine Sonderregelung. Trotzdem buchen geht im Kalender.", companion.Error);
        Assert.Empty(booked);

        stub.On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));
        await companion.BookCommand.ExecuteAsync(null);
        Assert.False(companion.IsBooking);
        Assert.Equal("Gebucht: Mi., 15:00 Klassische Massage bei Lena für Maria Brunner.", companion.BookedText);
        Assert.Single(booked);
        var sent = JsonSerializer.Deserialize<AppointmentRequest>(stub.Calls.Last(c => c.Route == "POST /api/appointments").Body!, ApiClient.Json)!;
        Assert.Equal((Location, Treatment, maria.Id, Slot, (Guid?)Lena), (sent.LocationId, sent.TreatmentId, sent.CustomerId, sent.Start, sent.EmployeeId));
        Assert.NotNull(sent.ClientId);
    }

    [Fact]
    public void Strg_Leertaste_aus_dem_eingebetteten_Browser_schaltet_die_Leiste_ohne_Bewegung_und_nur_auf_der_eigenen_Seite()
    {
        // R02 Slice 31 (Ä74): die Hilfe meldet die Taste als Nachricht; sie zählt wie der Kurzbefehl, aber nur, solange die Seite offen ist.
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.Help.OnWebMessage("""{"type":"companion"}""");
        Assert.False(shell.Companion.IsOpen);

        shell.SelectedPage = Assert.Single(shell.Pages, p => p.Title == "Hilfe");
        shell.Help.OnWebMessage("""{"type":"companion"}""");
        Assert.True(shell.Companion.IsOpen);
        Assert.True(shell.Companion.Presence.UsingKeyboard);
        shell.Help.OnWebMessage("""{"type":"companion"}""");
        Assert.False(shell.Companion.IsOpen);
    }
}

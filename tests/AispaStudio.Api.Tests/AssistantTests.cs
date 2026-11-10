using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Assistent je Rolle (hier regelbasiert, ohne API-Schlüssel): was er beantwortet und was er nur vorschlägt.</summary>
public sealed class AssistantTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rezeption_bekommt_freie_Zeiten_als_Buchungsvorschlaege_und_bucht_erst_selbst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var answer = await AskAsync(reception, "Ist am 01.03.2027 um 10 Uhr eine Massage bei Anna frei?", spa.Location.Id);
        Assert.Equal(AssistantAudience.Reception, answer.Audience);
        Assert.NotEmpty(answer.Disclosure);
        Assert.Contains("Massage", answer.Text, StringComparison.Ordinal);
        // Ä316: die Zeiten stehen als Zeilen der Antwort; ein Klick öffnet die Buchung.
        var slots = answer.Block!.Items!.Select(i => i.Suggestion!).ToList();
        Assert.All(slots, s => Assert.Equal(AssistantSuggestionKind.BookSlot, s.Kind));
        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.Equal(spa.MassageTreatment.Id, s.TreatmentId);
            Assert.Equal(spa.Location.Id, s.LocationId);
        });
        // Anna ist um 10 Uhr schon gebucht: der Vorschlag ist mit Ben oder zu einer anderen Zeit.
        Assert.DoesNotContain(slots, s => s.EmployeeId == spa.Anna.Id && s.Start == TestSpa.At(10));
        // Zuletzt steht „Freie Zeit suchen“ mit dem, was die Frage nennt; ausgeführt wird erst nach dem Klick.
        var search = answer.Suggestions[^1];
        Assert.Equal(AssistantSuggestionKind.SearchFreeTime, search.Kind);
        Assert.Equal(spa.MassageTreatment.Id, search.TreatmentId);
        Assert.Equal(spa.Anna.Id, search.EmployeeId);
        Assert.Equal(TestSpa.At(0), search.Start);

        // Der Assistent hat nichts gebucht; die Rezeption bucht den Vorschlag über die normale Buchung.
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Single(day.Appointments);
        var pick = slots[0];
        var booked = await reception.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
            spa.Location.Id, pick.TreatmentId!.Value, spa.Customer.Id, pick.Start!.Value, pick.EmployeeId, null, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
    }

    [Fact]
    public async Task Rezeption_findet_Kunden_sieht_aber_keine_Auswertungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var found = await AskAsync(reception, $"Wer ist Frau {spa.Customer.LastName}?", spa.Location.Id);
        // Ä316: der Gast steht als Zeile (Kontakt und nächster Termin darunter), ein Klick öffnet die Kundenkarte.
        Assert.Equal("1 Gast gefunden.", found.Text);
        var row = Assert.Single(found.Block!.Items!);
        Assert.Contains("eva@example.at", row.Detail, StringComparison.Ordinal);
        Assert.Contains("nächster Termin Massage", row.Detail, StringComparison.Ordinal);
        Assert.Equal((AssistantSuggestionKind.OpenCustomer, spa.Customer.Id), (row.Suggestion!.Kind, row.Suggestion.CustomerId!.Value));
        // Bei einem Treffer dazu: der nächste Termin im Kalender und alle Termine über sieben Tage.
        Assert.Equal([AssistantSuggestionKind.OpenAppointment, AssistantSuggestionKind.ShowCustomerAppointments], found.Suggestions.Select(s => s.Kind));
        Assert.All(found.Suggestions, s => Assert.Equal(spa.Customer.Id, s.CustomerId));
        Assert.Equal(TestSpa.At(14), found.Suggestions[0].Start);
        Assert.Contains("Alle Termine von", found.Suggestions[1].Label, StringComparison.Ordinal);

        var figures = await AskAsync(reception, "Wie war der Umsatz letzte Woche?", spa.Location.Id);
        Assert.Empty(figures.Figures);
        Assert.Contains("Berichte mit Beträgen", figures.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Management_bekommt_Auswertung_mit_Vergleich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var noShow = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);
        await spa.SetStatusAsync(noShow.Id, AppointmentStatus.NoShow, Ct);

        var answer = await AskAsync(spa.Client, "Auswertung für den 01.03.2027: Umsatz und Auslastung?", spa.Location.Id);
        Assert.Equal(AssistantAudience.Management, answer.Audience);
        Assert.Equal("2", Figure(answer, "Termine").Value);
        Assert.Equal("0", Figure(answer, "Termine").Previous);
        Assert.StartsWith("79,00 EUR", Figure(answer, "Terminwert").Value, StringComparison.Ordinal);
        Assert.Equal("1 (50 %)", Figure(answer, "No-Shows").Value);
        // Anna und Ben arbeiten montags je 8 Stunden: 2 × 50 Minuten von 960 Minuten.
        Assert.Equal("10 %", Figure(answer, "Auslastung (laut Arbeitszeiten)").Value);
        Assert.Contains("No-Show-Quote ist hoch", answer.Text, StringComparison.Ordinal);
        Assert.Empty(answer.Suggestions);
    }

    [Fact]
    public async Task Therapeutin_sieht_nur_eigene_Termine_und_bekommt_keine_Buchungsknoepfe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var own = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(11), Ct, spa.Ben.Id);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var overview = await AskAsync(anna, "Was steht am 01.03.2027 an?", spa.Location.Id);
        Assert.Equal(AssistantAudience.Therapist, overview.Audience);
        Assert.Equal(own.Id, Assert.Single(overview.Block!.Items!).Suggestion!.AppointmentId);
        Assert.Contains("1 Termin", overview.Text, StringComparison.Ordinal);

        var slots = await AskAsync(anna, "Ist am 01.03.2027 um 15 Uhr eine Massage frei?", spa.Location.Id);
        Assert.Contains("Freie Zeiten", slots.Text, StringComparison.Ordinal);
        Assert.Empty(slots.Suggestions);
        // Ohne Buchungsrecht stehen die Zeiten als Zeilen ohne Ziel.
        Assert.All(slots.Block!.Items!, i => Assert.Null(i.Suggestion));

        var customer = await AskAsync(anna, $"Telefonnummer von Frau {spa.Customer.LastName}", spa.Location.Id);
        Assert.DoesNotContain("+43", customer.Text, StringComparison.Ordinal);

        // Fremder Standort: wie überall abgewiesen.
        var other = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        var forbidden = await anna.PostAsJsonAsync("/api/assistant", new AssistantQuestion("Was steht heute an?", other.Id), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Kunde_bekommt_in_der_Online_Buchung_drei_freie_Zeiten_vorgeschlagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.PostAsJsonAsync($"/api/online/locations/{spa.Location.Id}/assistant",
            new OnlineAssistantQuestion("Hätten Sie am 01.03.2027 gegen 10 Uhr Zeit für eine Massage?"), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        var answer = (await response.Content.ReadFromJsonAsync<OnlineAssistantAnswer>(TestSpa.Json, Ct))!;
        Assert.Equal("de", answer.Language);
        Assert.Equal(3, answer.Suggestions.Count);
        Assert.All(answer.Suggestions, s => Assert.Equal(spa.MassageTreatment.Id, s.TreatmentId));
        Assert.Contains(answer.Suggestions, s => s.Start == TestSpa.At(10));
        Assert.Contains("bestätigen", answer.Disclosure, StringComparison.Ordinal);

        var english = await anonymous.PostAsJsonAsync($"/api/online/locations/{spa.Location.Id}/assistant",
            new OnlineAssistantQuestion("Do you have time for a treatment on 01.03.2027?"), TestSpa.Json, Ct);
        var englishAnswer = (await english.Content.ReadFromJsonAsync<OnlineAssistantAnswer>(TestSpa.Json, Ct))!;
        Assert.Empty(englishAnswer.Suggestions);
        Assert.Contains("Massage", englishAnswer.Text, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync($"/api/online/locations/{Guid.NewGuid()}/assistant", new OnlineAssistantQuestion("Massage?"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync($"/api/online/locations/{spa.Location.Id}/assistant", new OnlineAssistantQuestion(new string('x', 501)), TestSpa.Json, Ct)).StatusCode);
    }

    [Theory]
    [InlineData("Wie war der Umsatz letzte Woche?", AssistantTask.Figures)]
    [InlineData("Auslastung diesen Monat", AssistantTask.Figures)]
    [InlineData("Wer ist Frau Gruber?", AssistantTask.FindCustomer)]
    [InlineData("Was steht morgen an?", AssistantTask.DayOverview)]
    [InlineData("Wann ist eine Hot-Stone frei?", AssistantTask.FreeSlots)]
    [InlineData("Wie spät ist es in Tokio?", AssistantTask.Unknown)]
    public async Task Regelbasierte_Fragen_werden_eingeordnet(string question, AssistantTask task)
    {
        var intent = await new RuleBasedAssistantClassifier().ClassifyAsync(question, new DateTimeOffset(2027, 3, 3, 9, 0, 0, TimeSpan.Zero), "Europe/Vienna", Ct);
        Assert.Equal(task, intent.Task);
    }

    [Fact]
    public void Zeitraeume_und_Suchbegriffe()
    {
        var wednesday = new DateOnly(2027, 3, 3);
        Assert.Equal((new DateOnly(2027, 2, 22), new DateOnly(2027, 2, 28)), RuleBasedAssistantClassifier.Period("umsatz letzte woche", wednesday));
        Assert.Equal((new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28)), RuleBasedAssistantClassifier.Period("auslastung letzten monat", wednesday));
        Assert.Equal((new DateOnly(2027, 2, 25), wednesday), RuleBasedAssistantClassifier.Period("umsatz", wednesday));
        Assert.Equal(new DateOnly(2028, 1, 5), RuleBasedAssistantClassifier.Date("termine am 5.1.", new DateOnly(2027, 3, 3)));
        Assert.Equal("Gruber", RuleBasedAssistantClassifier.CustomerQuery("Wer ist Frau Gruber?"));
        Assert.Equal("lena@example.at", RuleBasedAssistantClassifier.CustomerQuery("Kunde mit lena@example.at"));
        Assert.Equal("+43 660 1234567", RuleBasedAssistantClassifier.CustomerQuery("Wer hat die Nummer +43 660 1234567?"));
        // Ziffern in einem Wort sind keine Telefonnummer.
        Assert.Equal("Muster ab cd", RuleBasedAssistantClassifier.CustomerQuery("Wer ist Frau Muster1234567ab890cd?"));
    }

    [Fact]
    public async Task Wie_mache_ich_Fragen_beantwortet_das_Handbuch_mit_Verweis_auf_Schritte_und_Bilder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var moved = await AskAsync(reception, "Wie verschiebe ich einen Termin auf eine andere Uhrzeit?", spa.Location.Id);
        Assert.Empty(moved.Suggestions);
        Assert.Equal("kalender-termin-verschieben", moved.Help[0].Topic);
        Assert.StartsWith("So geht es:", moved.Text, StringComparison.Ordinal);

        var invoice = await AskAsync(reception, "Wie erstelle ich eine Rechnung mit Empfänger?", spa.Location.Id);
        Assert.Equal("kassa-rechnung-mit-empfaenger", invoice.Help[0].Topic);

        // Allgemeine Frage von einer Seite aus: die Erklärung zu dieser Seite.
        var response = await reception.PostAsJsonAsync("/api/assistant",
            new AssistantQuestion("Was kann ich hier machen?", spa.Location.Id, "kassa", "windows-app"), TestSpa.Json, Ct);
        var page = (await response.Content.ReadFromJsonAsync<AssistantAnswer>(TestSpa.Json, Ct))!;
        Assert.Equal("seite-kassa", Assert.Single(page.Help).Topic);

        // Andere Fragen bleiben, wie sie waren.
        Assert.Empty((await AskAsync(reception, "Was steht heute an?", spa.Location.Id)).Help);
    }

    [Theory]
    [InlineData("Wie verschiebe ich einen Termin?", AssistantTask.Help)]
    [InlineData("Wo stelle ich den Barzahlungsrabatt ein?", AssistantTask.Help)]
    [InlineData("How do I void a receipt?", AssistantTask.Help)]
    [InlineData("Wie viele Termine habe ich heute?", AssistantTask.DayOverview)]
    [InlineData("Wie war der Umsatz letzte Woche?", AssistantTask.Figures)]
    public async Task Erkennt_Fragen_nach_dem_Wie(string question, AssistantTask expected)
    {
        var intent = await new RuleBasedAssistantClassifier().ClassifyAsync(question, DateTimeOffset.UtcNow, "Europe/Vienna", Ct);
        Assert.Equal(expected, intent.Task);
    }

    private static async Task<AssistantAnswer> AskAsync(HttpClient client, string question, Guid location)
    {
        var response = await client.PostAsJsonAsync("/api/assistant", new AssistantQuestion(question, location), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssistantAnswer>(TestSpa.Json, Ct))!;
    }

    private static AssistantFigure Figure(AssistantAnswer answer, string label) => Assert.Single(answer.Figures, f => f.Label == label);
}

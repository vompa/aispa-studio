using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Listen-Antworten des Assistenten (Ä316): oben ein Kopfsatz mit der Zahl, darunter eine einzige Liste; die ganze Zeile führt in Kalender, Kundenkarte oder Buchung.
/// Eine nicht verstandene Frage nennt, was erkannt wurde, und bietet passende Fragen an.
/// </summary>
public sealed class AssistantListTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sonntag, 28. Februar 2027, 12:00 Uhr in Wien: „morgen“ ist der Testmontag (<see cref="TestSpa.Monday"/>).</summary>
    private static readonly DateTimeOffset SundayNow = new(2027, 2, 28, 11, 0, 0, TimeSpan.Zero);

    private readonly AnswerKit _kit = new(fixture);

    [Fact]
    public async Task Termine_morgen_mit_15_Terminen_ergibt_Kopfsatz_mit_Zahl_und_15_Zeilen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lisa", "Berger", null, null, null, null), Ct);
        var brief = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Kurzmassage", 25, 0, 0, 39m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);
        var anna = await BookSeriesAsync(spa, brief, guest.Id, spa.Anna.Id, 8);
        var ben = await BookSeriesAsync(spa, brief, guest.Id, spa.Ben.Id, 7);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("termine morgen", null), role: StaffRole.Reception, now: SundayNow);

        Assert.Equal("Montag, 1. März, Wien: 15 Termine.", answer.Text);
        var items = answer.Block!.Items!;
        Assert.Equal(15, items.Count);
        Assert.Equal(("09:00", "Lisa Berger", "Kurzmassage · Anna", "Im Kalender zeigen"), (items[0].Lead, items[0].Title, items[0].Detail, items[0].Action));
        Assert.Equal(AssistantSuggestionKind.OpenAppointment, items[0].Suggestion!.Kind);
        Assert.Equal(anna[0].Id, items[0].Suggestion!.AppointmentId);
        Assert.Equal(anna.Concat(ben).Select(a => a.Id).Order(), items.Select(i => i.Suggestion!.AppointmentId!.Value).Order());
        // Keine zweite Liste: weder Vorschläge für dieselben Termine noch Termine im Text.
        Assert.DoesNotContain(answer.Suggestions, s => s.Kind == AssistantSuggestionKind.OpenAppointment);
        Assert.Null(answer.Block.Table);
    }

    [Fact]
    public async Task Freie_Zeiten_stehen_als_Zeilen_und_nicht_im_Text()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Ist am 01.03.2027 um 10 Uhr eine Massage bei Anna frei?", null), role: StaffRole.Reception,
            now: SundayNow);

        var items = answer.Block!.Items!;
        Assert.Equal($"Freie Zeiten für Massage in Wien mit Anna: {items.Count} Vorschläge.", answer.Text);
        Assert.Contains(("10:00", "Montag, 1. März", "Anna", "Buchen …"), items.Select(i => (i.Lead, i.Title, i.Detail, i.Action)));
        Assert.All(items, i => Assert.Equal(AssistantSuggestionKind.BookSlot, i.Suggestion!.Kind));
        Assert.Equal([AssistantSuggestionKind.SearchFreeTime], answer.Suggestions.Select(s => s.Kind));
    }

    [Fact]
    public async Task Kundensuche_mit_zwei_Treffern_ergibt_zwei_Zeilen_zur_Kundenkarte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lisa = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lisa", "Bergmeister", null, "+43 660 111", null, null), Ct);
        var lukas = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lukas", "Bergmeister", null, null, null, null), Ct);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Kunde Bergmeister", null), role: StaffRole.Reception, now: SundayNow);

        Assert.Equal("2 Gäste gefunden.", answer.Text);
        var items = answer.Block!.Items!;
        Assert.Equal([("Lisa Bergmeister", "+43 660 111 · kein kommender Termin"), ("Lukas Bergmeister", "kein kommender Termin")],
            items.Select(i => (i.Title, i.Detail)));
        Assert.Equal([lisa.Id, lukas.Id], items.Select(i => i.Suggestion!.CustomerId!.Value));
        Assert.All(items, i => Assert.Equal((AssistantSuggestionKind.OpenCustomer, "Kundenkarte öffnen"), (i.Suggestion!.Kind, i.Action)));
        Assert.Empty(answer.Suggestions);
    }

    [Fact]
    public async Task Unverstaendliche_Frage_mit_morgen_nennt_es_und_bietet_drei_Fragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Kaffee morgen mit Anna?", null), role: StaffRole.Reception);

        Assert.Equal(AssistantBlockKind.NoIntent, answer.Block!.Kind);
        Assert.Equal("Dazu habe ich nichts gefunden. Erkannt: morgen, Anna. Meinen Sie vielleicht eine dieser Fragen?", answer.Text);
        Assert.Equal(["Was steht morgen an?", "Wer arbeitet morgen?", "Ist Anna morgen da?"], answer.Block.FollowUps.Select(f => f.Question));
        // Die Angebote sind Fragen, die der Assistent beantwortet.
        Assert.NotNull(BusinessIntents.Detect("Ist Anna morgen da?", new DateOnly(2027, 3, 3)));
        Assert.NotNull(BusinessIntents.Detect("Wer arbeitet morgen?", new DateOnly(2027, 3, 3)));
    }

    [Fact]
    public async Task Unverstaendliche_Frage_im_Backoffice_nennt_weder_Tag_noch_Team()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Kaffee morgen mit Anna?", null), role: StaffRole.Backoffice);

        Assert.Equal(AssistantBlockKind.NoIntent, answer.Block!.Kind);
        Assert.Equal("Dazu habe ich nichts gefunden. Meinen Sie vielleicht eine dieser Fragen?", answer.Text);
        Assert.Equal(["Wie lange haben wir heute offen?", "Top-Behandlungen diesen Monat"], answer.Block.FollowUps.Select(f => f.Question));
    }

    /// <summary>Bucht <paramref name="count"/> Termine am Testmontag ab 9:00 Uhr im Abstand von 45 Minuten (Raum A braucht 15 Minuten Pflege).</summary>
    private static async Task<List<AppointmentDto>> BookSeriesAsync(TestSpa spa, TreatmentDto treatment, Guid customer, Guid employee, int count)
    {
        var booked = new List<AppointmentDto>();
        for (var i = 0; i < count; i++)
        {
            var response = await spa.BookAsync(TestSpa.At(9).AddMinutes(45 * i), Ct, employee, treatment: treatment, customer: customer);
            booked.Add(await TestSpa.ReadAsync<AppointmentDto>(response, Ct));
        }

        return booked;
    }
}

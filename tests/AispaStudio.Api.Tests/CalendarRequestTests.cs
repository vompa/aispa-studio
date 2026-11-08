using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Anfrage im Kalender (Strg+K), hier regelbasiert: Bausteine, drei Vorschläge, Ergänzen, Ausschließen. Gebucht wird nichts.</summary>
public sealed class CalendarRequestTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Wunsch_frei_eingetippt_ergibt_Bausteine_und_drei_Vorschlaege_ohne_Buchung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var result = await AskAsync(reception, spa, new CalendarRequestInput("Massage am 01.03.2027 um 10 Uhr, nicht bei Anna", null));

        Assert.NotEmpty(result.Disclosure);
        Assert.Equal(TestSpa.Monday, result.Wish.Date);
        Assert.Equal(new TimeOnly(10, 0), result.Wish.From);
        Assert.Equal(spa.MassageTreatment.Id, result.Wish.TreatmentId);
        Assert.Null(result.Wish.EmployeeId);
        Assert.Equal([spa.Anna.Id], result.Wish.ExcludedEmployeeIds);
        Assert.Equal(
            [CalendarWishPart.Day, CalendarWishPart.Time, CalendarWishPart.Treatment, CalendarWishPart.Excluded],
            result.Chips.Select(c => c.Part));
        Assert.Equal("nicht Anna", result.Chips[^1].Text);

        Assert.Equal(3, result.Proposals.Count);
        Assert.All(result.Proposals, p =>
        {
            Assert.Equal(spa.Ben.Id, p.EmployeeId);
            Assert.Equal(79m, p.Price);
            Assert.NotEmpty(p.RoomName);
        });
        Assert.Equal(TestSpa.At(10), result.Proposals[0].Start);
        Assert.Equal(0, result.Proposals[0].MinutesFromWish);
        Assert.Contains("zur Wunschzeit", result.Proposals[0].Reason, StringComparison.Ordinal);
        // Bei derselben Person mindestens 30 Minuten Abstand, damit die drei Vorschläge sich unterscheiden.
        Assert.True(result.Proposals.Zip(result.Proposals.Skip(1)).All(p => Math.Abs((p.Second.Start - p.First.Start).TotalMinutes) >= 30));

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Empty(day.Appointments);
    }

    [Fact]
    public async Task Wunsch_laesst_sich_ergaenzen_und_abgelehnte_Zeiten_kommen_nicht_wieder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var first = await AskAsync(spa.Client, spa, new CalendarRequestInput("Massage am 01.03.2027 um 10 Uhr, nicht bei Anna", null));
        Assert.All(first.Proposals, p => Assert.Equal(spa.Ben.Id, p.EmployeeId));

        // „lieber bei Anna“ ergänzt den Wunsch: Anna ist nicht mehr ausgeschlossen, Tag, Uhrzeit und Behandlung bleiben.
        var anna = await AskAsync(spa.Client, spa, new CalendarRequestInput("lieber bei Anna", first.Wish));
        Assert.Equal(spa.Anna.Id, anna.Wish.EmployeeId);
        Assert.Empty(anna.Wish.ExcludedEmployeeIds);
        Assert.Equal(new TimeOnly(10, 0), anna.Wish.From);
        Assert.Contains(anna.Chips, c => c is { Part: CalendarWishPart.Employee, Text: "bei Anna" });
        // Um 10 ist Anna gebucht: der nächste Vorschlag ist bei ihr zu einer anderen Zeit, nicht bei Ben um 10.
        var best = anna.Proposals[0];
        Assert.Equal(spa.Anna.Id, best.EmployeeId);
        Assert.NotEqual(0, best.MinutesFromWish);
        Assert.Contains("Wunschperson", best.Reason, StringComparison.Ordinal);

        var again = await AskAsync(spa.Client, spa, new CalendarRequestInput(null, anna.Wish, Rejected: [.. anna.Proposals.Select(p => p.Start)]));
        Assert.NotEmpty(again.Proposals);
        Assert.DoesNotContain(again.Proposals, p => anna.Proposals.Any(old => old.Start == p.Start));

        // Baustein „Behandlung“ entfernt: Rückfrage statt Vorschlägen.
        var open = await AskAsync(spa.Client, spa, new CalendarRequestInput(null, anna.Wish with { TreatmentId = null }));
        Assert.Empty(open.Proposals);
        Assert.Contains("Welche Behandlung?", open.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_und_ohne_Wunsch_abgewiesen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var forbidden = await anna.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-request",
            new CalendarRequestInput("Massage morgen", null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var empty = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-request",
            new CalendarRequestInput(" ", null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Fehlt_die_Behandlung_kommen_die_passendsten_als_Auswahl_statt_einer_Textwand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var foot = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Fußbad", 20, 0, 0, 25m, [], [spa.RoomA.Id], []), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Anna.Id);

        var open = await AskAsync(spa.Client, spa, new CalendarRequestInput("am 01.03.2027 um 15 Uhr", null));

        Assert.Empty(open.Proposals);
        Assert.Contains("Welche Behandlung?", open.Text, StringComparison.Ordinal);
        // Die am häufigsten gebuchte steht zuerst; höchstens fünf.
        Assert.Equal(spa.MassageTreatment.Id, open.TreatmentChoices![0]);
        Assert.Contains(foot.Id, open.TreatmentChoices);
        Assert.True(open.TreatmentChoices.Count <= 5);

        // Ein Wort des Textes zählt mehr als die Beliebtheit.
        var named = await AskAsync(spa.Client, spa, new CalendarRequestInput("Fußbad bitte", null));
        Assert.Equal(foot.Id, named.Wish.TreatmentId);
    }

    [Fact]
    public async Task Ist_am_Tag_weniger_als_drei_frei_geht_die_Suche_an_den_Folgetagen_weiter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Bis 15:55 sind beide Personen gesperrt: am Montag bleibt je Person nur 16:00 (Dienst bis 17 Uhr); Dienst haben sie nur montags, also kommt der dritte Vorschlag eine Woche später.
        foreach (var employee in new[] { spa.Anna, spa.Ben })
        {
            var block = await spa.Client.PostAsJsonAsync("/api/blocks",
                new BlockInput(ResourceKind.Employee, employee.Id, TestSpa.At(9), TestSpa.At(15, 55), "Schulung"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        }

        var result = await AskAsync(spa.Client, spa, new CalendarRequestInput("Massage am 01.03.2027", null));

        Assert.Equal(3, result.Proposals.Count);
        var days = result.Proposals.Select(p => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(p.Start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime)).ToList();
        Assert.Equal([TestSpa.Monday, TestSpa.Monday, TestSpa.Monday.AddDays(7)], days);
        Assert.Contains("Mo 8.3.", result.Proposals[2].Reason, StringComparison.Ordinal);
        Assert.Contains("2 am Mo 1.3. und 1 am Mo 8.3.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Dienst_am_Wunschtag_beginnen_die_Vorschlaege_am_naechsten_Diensttag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var result = await AskAsync(spa.Client, spa, new CalendarRequestInput("Massage am 03.03.2027", null));

        // Dienst nur montags: der 03.03. ist ein Mittwoch, der Montag danach liegt im Suchfenster.
        Assert.NotEmpty(result.Proposals);
        Assert.All(result.Proposals, p => Assert.True(p.Start >= TestSpa.At(0, date: TestSpa.Monday.AddDays(7))));
    }

    [Fact]
    public void Raum_und_Zahl_der_Gaeste_werden_im_Text_erkannt()
    {
        var lotus = new AispaStudio.Api.Scheduling.Room { Name = "Raum Lotus", LocationId = Guid.NewGuid() };
        var thai = new AispaStudio.Api.Scheduling.Room { Name = "Thai-Raum", LocationId = lotus.LocationId };
        var rooms = new[] { lotus, thai };

        Assert.Equal(lotus.Id, CalendarRequestService.RoomNamed("Hot-Stone morgen im Raum Lotus", rooms));
        Assert.Equal(lotus.Id, CalendarRequestService.RoomNamed("lieber in Lotus", rooms));
        Assert.Equal(thai.Id, CalendarRequestService.RoomNamed("Massage im Thai-Raum", rooms));
        // Ohne „im“, „in“ oder „Raum“ davor kein Raumwunsch, und „Raum“ allein unterscheidet nichts.
        Assert.Null(CalendarRequestService.RoomNamed("Lotus Massage", rooms));
        Assert.Null(CalendarRequestService.RoomNamed("im Raum", rooms));

        Assert.Equal(2, CalendarRequestService.GuestsIn("Massage zu zweit am Samstag"));
        Assert.Equal(2, CalendarRequestService.GuestsIn("ein Paar möchte kommen"));
        Assert.Equal(1, CalendarRequestService.GuestsIn("nur eine Person, allein"));
        Assert.Null(CalendarRequestService.GuestsIn("Massage am Samstag"));
    }

    [Fact]
    public async Task Bei_zwei_Gaesten_gelten_nur_Paarraeume_und_ohne_Angabe_fragt_der_Dienst_nach()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suite = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Paarsuite", 10, Places: 2), Ct);
        var coupleTreatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Paarmassage", 50, 0, 0, 150m, [spa.Massage.Id], [suite.Id, spa.RoomA.Id], []), Ct);

        var ask = await AskAsync(spa.Client, spa, new CalendarRequestInput("Paarmassage am 01.03.2027 um 10 Uhr", null));
        var question = Assert.Single(ask.Questions!);
        Assert.Equal("Für eine oder zwei Personen?", question.Text);
        Assert.Equal(["Eine Person", "Zwei Personen (Paarraum)"], question.Answers.Select(a => a.Label));
        Assert.Equal(coupleTreatment.Id, ask.Wish.TreatmentId);

        // Die Antwort ist der geänderte Wunsch und kommt als Baustein mit Paarraum-Vorschlägen zurück.
        var two = await AskAsync(spa.Client, spa, new CalendarRequestInput(null, question.Answers[1].Wish));
        Assert.Equal(2, two.Wish.Guests);
        Assert.Contains(two.Chips, c => c.Part == CalendarWishPart.Guests);
        Assert.Empty(two.Questions!);
        Assert.NotEmpty(two.Proposals);
        Assert.All(two.Proposals, p => Assert.Equal(suite.Id, p.RoomId));
    }

    [Fact]
    public async Task Raumwunsch_im_Text_wird_Baustein_und_filtert_die_Vorschlaege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lotus = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Raum Lotus", 0), Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Hot-Stone", 50, 0, 0, 99m, [spa.Massage.Id], [lotus.Id, spa.RoomA.Id], []), Ct);

        var result = await AskAsync(spa.Client, spa, new CalendarRequestInput("Hot-Stone am 01.03.2027 im Raum Lotus", null));

        Assert.Equal(lotus.Id, result.Wish.RoomId);
        Assert.Contains(result.Chips, c => c is { Part: CalendarWishPart.Room, Text: "Raum Lotus" });
        Assert.NotEmpty(result.Proposals);
        Assert.All(result.Proposals, p => Assert.Equal(lotus.Id, p.RoomId));

        // Der Baustein lässt sich entfernen: der Wunsch ohne Raum bietet wieder alle Räume an.
        var any = await AskAsync(spa.Client, spa, new CalendarRequestInput(null, result.Wish with { RoomId = null }));
        Assert.DoesNotContain(any.Chips, c => c.Part == CalendarWishPart.Room);
    }

    private static async Task<CalendarRequestResult> AskAsync(HttpClient client, TestSpa spa, CalendarRequestInput input)
    {
        var response = await client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-request", input, TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CalendarRequestResult>(TestSpa.Json, Ct))!;
    }
}

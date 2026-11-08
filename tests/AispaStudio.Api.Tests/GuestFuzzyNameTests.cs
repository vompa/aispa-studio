using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gastfragen, wie die Rezeption sie tippt (Ä315): klein geschrieben, mit Anrede oder Tippfehler; bei mehreren passenden Gästen eine Auswahl, deren Klick die Frage
/// beantwortet; „sie“ und „der Gast“ meinen den offenen Termin, sonst die offene Kundenkarte, sonst den zuletzt genannten Gast. „Jetzt“ ist Mittwoch, 03.03.2027,
/// 11:00 Uhr; das Team arbeitet montags.
/// </summary>
public sealed class GuestFuzzyNameTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly NextMonday = TestSpa.Monday.AddDays(7);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string first, string last, CustomerSalutation salutation = CustomerSalutation.None, string? city = null) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, null, null, null, null, Salutation: salutation,
            Address: city is null ? null : new CustomerAddress(null, null, city, null)), Ct);

    private static Task<AppointmentDto> BookAsync(TestSpa spa, CustomerDto guest, DateOnly day, int hour) =>
        spa.BookOkAsync(TestSpa.At(hour, date: day), Ct, spa.Ben.Id, customer: guest.Id);

    /// <summary>Ein Besuch am Testmontag (vor „jetzt“), eingecheckt.</summary>
    private static async Task VisitAsync(TestSpa spa, CustomerDto guest, int hour)
    {
        var visit = await BookAsync(spa, guest, TestSpa.Monday, hour);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{visit.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), Ct);
    }

    private async Task<AssistantBlock> AskAsync(TestSpa spa, string question, AssistantReference? reference = null, Guid? guest = null, Guid? location = null,
        IReadOnlySet<Guid>? locations = null) =>
        (await _kit.StaffAsync(spa, new AssistantQuestion(question, location, Reference: reference, GuestId: guest), locations: locations)).Block
        ?? throw new InvalidOperationException($"Keine Betriebsantwort auf „{question}“.");

    [Fact]
    public async Task Given_Christian_und_Eva_Steiner_When_herr_steiner_Then_Termin_von_Christian()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner", CustomerSalutation.Male);
        var eva = await GuestAsync(spa, "Eva", "Steiner", CustomerSalutation.Female);
        await BookAsync(spa, eva, NextMonday, 10);
        await BookAsync(spa, christian, NextMonday, 14);

        var block = await AskAsync(spa, "wann hat herr steiner seinen termin");

        Assert.Equal("Christian Steiner hat den nächsten Termin am Montag, 08.03.2027, 14:00 Uhr (Massage, Ben).", block.Text);
        Assert.Equal(christian.Id, block.GuestId);
    }

    [Fact]
    public async Task Given_zwei_Steiner_When_wann_kommt_steiner_Then_Auswahl_mit_beiden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner", city: "Wien");
        var eva = await GuestAsync(spa, "Eva", "Steiner");
        await BookAsync(spa, christian, NextMonday, 14);

        var block = await AskAsync(spa, "wann kommt steiner");

        Assert.Equal("Der Name passt zu 2 Gästen. Bitte den Gast wählen.", block.Text);
        Assert.Equal(["Gast", "Ort", "Nächster Termin"], block.Table!.Columns);
        Assert.Equal(["Christian Steiner", "Wien", "Montag, 08.03.2027, 14:00 Uhr"], block.Table.Rows[0]);
        Assert.Equal(["Eva Steiner", "–", "kein Termin geplant"], block.Table.Rows[1]);
        Assert.Equal([new AssistantFollowUp("Christian Steiner, Wien", "wann kommt steiner", christian.Id), new AssistantFollowUp("Eva Steiner", "wann kommt steiner", eva.Id)],
            block.FollowUps);
        Assert.Null(block.GuestId);
    }

    [Fact]
    public async Task Given_Auswahl_When_Gast_gewaehlt_Then_Antwort_fuer_diesen_Gast()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner");
        var eva = await GuestAsync(spa, "Eva", "Steiner");
        await BookAsync(spa, christian, NextMonday, 14);
        await BookAsync(spa, eva, NextMonday.AddDays(7), 10);

        var block = await AskAsync(spa, "wann kommt steiner", guest: eva.Id);

        Assert.Equal("Eva Steiner hat den nächsten Termin am Montag, 15.03.2027, 10:00 Uhr (Massage, Ben).", block.Text);
        Assert.Equal(eva.Id, block.GuestId);
    }

    [Fact]
    public async Task Given_Christian_Steiner_When_termin_herr_steinr_Then_Christian_erkannt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner", CustomerSalutation.Male);
        await BookAsync(spa, christian, NextMonday, 14);

        var block = await AskAsync(spa, "termin herr steinr");

        Assert.Equal("Christian Steiner hat den nächsten Termin am Montag, 08.03.2027, 14:00 Uhr (Massage, Ben).", block.Text);
    }

    [Fact]
    public async Task Given_offener_Termin_von_Petra_Schwarz_When_wann_war_sie_zuletzt_da_Then_Petra()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var petra = await GuestAsync(spa, "Petra", "Schwarz", CustomerSalutation.Female);
        await VisitAsync(spa, petra, 10);
        var open = await BookAsync(spa, petra, NextMonday, 10);

        var block = await AskAsync(spa, "wann war sie zuletzt da", new AssistantReference(open.Id, null, null));

        Assert.Equal("Petra Schwarz war zuletzt am Montag, 01.03.2027, 10:00 Uhr da (Massage).", block.Text);
        Assert.Equal(petra.Id, block.GuestId);
    }

    [Fact]
    public async Task Given_Kundenkarte_und_letzter_Gast_When_der_Gast_Then_gilt_die_Kundenkarte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner");
        var eva = await GuestAsync(spa, "Eva", "Steiner");

        var block = await AskAsync(spa, "wann war der gast zuletzt da", new AssistantReference(null, christian.Id, eva.Id));

        Assert.Equal("Für Christian Steiner ist kein abgeschlossener Besuch im Kalender eingetragen.", block.Text);
    }

    [Fact]
    public async Task Given_nur_der_letzte_Gast_When_sie_Then_gilt_der_letzte_Gast()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var eva = await GuestAsync(spa, "Eva", "Steiner");

        var block = await AskAsync(spa, "wann war sie zuletzt da", new AssistantReference(null, null, eva.Id));

        Assert.Equal("Für Eva Steiner ist kein abgeschlossener Besuch im Kalender eingetragen.", block.Text);
    }

    [Fact]
    public async Task Given_kein_Bezug_When_wann_war_sie_zuletzt_da_Then_Frage_nach_dem_Gast()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await AskAsync(spa, "wann war sie zuletzt da");

        Assert.Equal(AssistantBlockKind.NotFound, block.Kind);
        Assert.Equal("Welcher Gast ist gemeint? Bitte den Namen nennen, zum Beispiel „Frau Müller“.", block.Text);
    }

    [Fact]
    public async Task Given_kein_kommender_Termin_When_seinen_Termin_Then_der_letzte_Besuch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var christian = await GuestAsync(spa, "Christian", "Steiner", CustomerSalutation.Male);
        await VisitAsync(spa, christian, 10);

        var block = await AskAsync(spa, "wann hat herr steiner seinen termin");

        Assert.Equal("Christian Steiner hat keinen Termin geplant. Letzter Besuch: Montag, 01.03.2027, 10:00 Uhr (Massage).", block.Text);
    }

    [Fact]
    public async Task Given_kein_Gast_Steiner_When_gefragt_Then_Keinen_Gast_mit_Gast_suchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await AskAsync(spa, "wann kommt steiner");

        Assert.Equal(AssistantBlockKind.NotFound, block.Kind);
        Assert.Equal("Keinen Gast „steiner“ gefunden.", block.Text);
        Assert.Equal([new AssistantFollowUp("Gast suchen", "Kunde steiner")], block.FollowUps);
    }

    [Fact]
    public async Task Given_Christian_Steinhauser_When_wann_kommt_stein_Then_kein_Teilwort_Treffer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await GuestAsync(spa, "Christian", "Steinhauser");

        var block = await AskAsync(spa, "wann kommt stein");

        Assert.Equal("Keinen Gast „stein“ gefunden.", block.Text);
    }

    [Fact]
    public async Task Given_Christian_Steiner_und_Christian_Moser_When_christian_steinr_Then_Christian_Steiner()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        await GuestAsync(spa, "Christian", "Moser");
        await BookAsync(spa, steiner, NextMonday, 14);

        var block = await AskAsync(spa, "wann kommt christian steinr");

        Assert.Equal(steiner.Id, block.GuestId);
    }

    [Fact]
    public async Task Given_offener_Termin_When_Zusatzwort_ohne_Gast_Then_gilt_der_Bezug()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var petra = await GuestAsync(spa, "Petra", "Schwarz");
        var open = await BookAsync(spa, petra, NextMonday, 10);

        var block = await AskAsync(spa, "wann hat sie demnächst ihren termin", new AssistantReference(open.Id, null, null));

        Assert.Equal(petra.Id, block.GuestId);
    }

    [Fact]
    public async Task Given_offener_Termin_When_unbekannter_Name_ohne_Pronomen_Then_kein_Gast_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var petra = await GuestAsync(spa, "Petra", "Schwarz");
        var open = await BookAsync(spa, petra, NextMonday, 10);

        var block = await AskAsync(spa, "wann kommt zzyzx", new AssistantReference(open.Id, null, null));

        Assert.Equal("Keinen Gast „zzyzx“ gefunden.", block.Text);
    }

    [Fact]
    public async Task Given_Termin_an_fremdem_Standort_When_Bezug_Then_kein_Gast_aus_dem_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var petra = await GuestAsync(spa, "Petra", "Schwarz");
        var open = await BookAsync(spa, petra, NextMonday, 10);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);

        var block = await AskAsync(spa, "wann war sie zuletzt da", new AssistantReference(open.Id, null, null), location: graz.Id,
            locations: new HashSet<Guid> { graz.Id });

        Assert.Equal("Welcher Gast ist gemeint? Bitte den Namen nennen, zum Beispiel „Frau Müller“.", block.Text);
    }
}

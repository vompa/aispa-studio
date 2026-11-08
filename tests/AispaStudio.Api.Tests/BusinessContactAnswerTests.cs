using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Fragen zu den Kontakten des Betriebs (ADR 0074, Thema „Betrieb/Kontakte“): Absicht aus Schlüsselwörtern, Treffer nach Kategorie, Bezeichnung und Zuständigkeit,
/// einer oder mehrere Treffer, Stufe „Leitung“ nur mit Recht, nichts hinterlegt mit Link, Antwort über die API. „Heute“ ist Mittwoch, der 3. März 2027.
/// </summary>
public sealed class BusinessContactAnswerTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2027, 3, 3);

    private readonly AnswerKit _kit = new(fixture);

    [Theory]
    [InlineData("Wer ist unser Installateur?", "Craftsman")]
    [InlineData("Nummer vom Rauchfangkehrer", "Craftsman")]
    [InlineData("Handwerker-Nummern", "Craftsman")]
    [InlineData("Nummer der Versicherung", "Insurance")]
    [InlineData("Welche Versicherungen haben wir?", "Insurance")]
    [InlineData("Wer ist unser Ansprechpartner bei der Polizze?", "Insurance")]
    [InlineData("Kontakt der Lohnverrechnung", "Payroll")]
    [InlineData("Wer kümmert sich um die Buchhaltung?", "Accounting")]
    [InlineData("Telefonnummer vom Steuerberater", "TaxAdvisor")]
    [InlineData("Wer ist unsere Vertrauensperson?", "TrustedPerson")]
    [InlineData("Notfallnummer", "Emergency")]
    [InlineData("Wer ist für die Heizung zuständig?", null)]
    [InlineData("Wen rufe ich bei Wasserschaden an?", null)]
    [InlineData("Nummer vom Gärtner", null)]
    public void Kontaktfragen_werden_dem_Thema_und_der_Kategorie_zugeordnet(string question, string? category)
    {
        var intent = BusinessIntents.Detect(question, Today);
        Assert.NotNull(intent);
        Assert.Equal(BusinessTopic.BusinessContacts, intent.Topic);
        Assert.Equal(category, BusinessIntents.Question(intent, question, Today)!.Detail);
    }

    [Theory]
    [InlineData("Unsere Kontaktdaten", BusinessTopic.Contact)]
    [InlineData("Wie lautet unsere Telefonnummer?", BusinessTopic.Contact)]
    [InlineData("Welche Mailadresse haben wir?", BusinessTopic.Contact)]
    public void Die_Kontaktdaten_des_Standorts_bleiben_beim_Thema_Kontakt(string question, BusinessTopic topic) =>
        Assert.Equal(topic, BusinessIntents.Detect(question, Today)!.Topic);

    [Theory]
    [InlineData("Telefonnummer von Frau Gruber")]
    [InlineData("Wie erstelle ich die Buchhaltung für den Monat?")]
    [InlineData("Wie ändere ich die Versicherung eines Gastes?")]
    [InlineData("Buchhaltung exportieren")]
    [InlineData("Nummer des Gutscheins")]
    [InlineData("Wie ist die Nummer des Spas?")]
    public void Bedienung_und_Gaeste_sind_keine_Kontaktfragen(string question) =>
        Assert.NotEqual(BusinessTopic.BusinessContacts, BusinessIntents.Detect(question, Today)?.Topic);

    /// <summary>Acht Einträge quer durch die Kategorien, zwei Versicherungen und zwei Wasser-Zuständige.</summary>
    private static async Task SeedAsync(TestSpa spa)
    {
        BusinessContactInput Contact(string designation, BusinessContactCategory category, string company, string? person, string? phone, string? responsibility,
            BusinessContactVisibility visibility = BusinessContactVisibility.Team) =>
            BusinessContactTests.Input(designation, category, company, person, phone, null, responsibility, visibility: visibility);

        await Create(Contact("Installateur", BusinessContactCategory.Craftsman, "Haustechnik Muster GmbH", "Franz Muster", "+43 1 234 56 78", "Wasserschaden, Heizung") with
        {
            Email = "office@haustechnik-muster.example",
            Mobile = "+43 664 234 56 78",
            CustomerNumber = "K-4711",
        });
        await Create(Contact("Elektriker", BusinessContactCategory.Craftsman, "Strom Muster KG", "Gerda Strom", "+43 1 234 56 79", "Stromausfall, Beleuchtung"));
        await Create(Contact("Betriebshaftpflicht", BusinessContactCategory.Insurance, "Muster Versicherung AG", "Eva Polizze", "+43 1 111 22 33", "Haftpflichtschäden",
            BusinessContactVisibility.Management) with
        {
            Insurer = "Muster Versicherung AG",
            PolicyNumber = "POL-123456",
            Coverage = "Haftpflicht bis 2 Mio. EUR",
            ClaimsHotline = "+43 800 123 456",
        });
        await Create(Contact("Inventarversicherung", BusinessContactCategory.Insurance, "Sicher & Sound Versicherungen", null, "+43 1 222 33 44", "Einbruch, Brand"));
        await Create(Contact("Lohnverrechnung", BusinessContactCategory.Payroll, "Lohn & Gehalt Muster OG", "Petra Lohn", "+43 1 333 44 55", "Gehälter, Lohnzettel",
            BusinessContactVisibility.Management));
        await Create(Contact("Buchhaltung", BusinessContactCategory.Accounting, "Buchführung Muster", "Bernd Bilanz", "+43 1 444 55 66", "Belege, Monatsabschluss"));
        await Create(Contact("Steuerberater", BusinessContactCategory.TaxAdvisor, "Steuerberatung Muster GmbH", "Dr. Sabine Steuer", "+43 1 555 66 77", "Jahresabschluss",
            BusinessContactVisibility.Management));
        await Create(Contact("Wasser-Notdienst", BusinessContactCategory.Emergency, "Rohrbruch Express", null, "+43 800 987 654", "Wasserschaden, Rohrbruch"));

        Task Create(BusinessContactInput input) => spa.PostAsync<BusinessContactDto>("/api/business-contacts", input, Ct);
    }

    [Fact]
    public async Task Wer_ist_unser_Installateur_nennt_den_einen_Eintrag_mit_allen_Angaben_zum_Kopieren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var block = await _kit.AnswerAsync(spa, "Wer ist unser Installateur?");

        Assert.Equal(AssistantBlockKind.Answer, block.Kind);
        Assert.Equal(nameof(BusinessTopic.BusinessContacts), block.Topic);
        Assert.Equal("Installateur: Haustechnik Muster GmbH, Ansprechperson Franz Muster. Telefon +43 1 234 56 78; Mobil +43 664 234 56 78; E-Mail office@haustechnik-muster.example. "
            + "Zuständig für: Wasserschaden, Heizung.", block.Text);
        Assert.Equal("Betriebsinformationen", block.Source);
        Assert.Equal(["Angabe", "Wert"], block.Table!.Columns);
        Assert.Equal(["Telefon", "+43 1 234 56 78"], block.Table.Rows[3]);
        Assert.Contains("Kunden-/Vertragsnummer: K-4711", block.Copy, StringComparison.Ordinal);
        Assert.Contains("Telefon: +43 1 234 56 78", block.Copy, StringComparison.Ordinal);
        Assert.Equal(("Betriebsinformationen öffnen", "businessInfo"), (block.Link!.Label, block.Link.Page));
    }

    [Fact]
    public async Task Zustaendigkeit_und_Notfall_finden_alle_passenden_Eintraege_als_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var heating = await _kit.AnswerAsync(spa, "Wer ist für die Heizung zuständig?");
        var water = await _kit.AnswerAsync(spa, "Wen rufe ich bei Wasserschaden an?");

        Assert.StartsWith("Installateur: Haustechnik Muster GmbH", heating.Text, StringComparison.Ordinal);
        Assert.Equal("Dazu sind 2 Einträge hinterlegt: Installateur, Wasser-Notdienst.", water.Text);
        Assert.Equal(["Bezeichnung", "Firma", "Ansprechperson", "Telefon"], water.Table!.Columns);
        Assert.Equal(["Installateur", "Haustechnik Muster GmbH", "Franz Muster", "+43 1 234 56 78"], water.Table.Rows[0]);
        Assert.Equal(["Wasser-Notdienst", "Rohrbruch Express", "–", "+43 800 987 654"], water.Table.Rows[1]);
        Assert.Equal("Installateur, Haustechnik Muster GmbH, Franz Muster, +43 1 234 56 78\nWasser-Notdienst, Rohrbruch Express, +43 800 987 654", water.Copy);
    }

    [Fact]
    public async Task Versicherung_nennt_Polizze_und_Hotline_und_bei_zwei_Eintraegen_eine_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var both = await _kit.AnswerAsync(spa, "Nummer der Versicherung");
        var haftpflicht = await _kit.AnswerAsync(spa, "Wer ist unser Ansprechpartner bei der Haftpflicht?");

        Assert.Equal("Dazu sind 2 Einträge hinterlegt: Betriebshaftpflicht, Inventarversicherung.", both.Text);
        Assert.Equal(["Betriebshaftpflicht", "Muster Versicherung AG", "Eva Polizze", "+43 1 111 22 33"], both.Table!.Rows[0]);
        Assert.Contains("Schadenshotline +43 800 123 456; Polizzennummer POL-123456.", haftpflicht.Text, StringComparison.Ordinal);
        Assert.Equal(["Versicherer", "Muster Versicherung AG"], haftpflicht.Table!.Rows[1]);
        Assert.Contains("Deckung: Haftpflicht bis 2 Mio. EUR", haftpflicht.Copy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Stufe_Leitung_nennt_der_Assistent_nur_wer_sie_lesen_darf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var owner = await _kit.AnswerAsync(spa, "Kontakt der Lohnverrechnung");
        var location = await _kit.AnswerAsync(spa, "Kontakt der Lohnverrechnung", role: StaffRole.Manager);
        var backoffice = await _kit.AnswerAsync(spa, "Kontakt der Lohnverrechnung", role: StaffRole.Backoffice);
        var reception = await _kit.AnswerAsync(spa, "Kontakt der Lohnverrechnung", role: StaffRole.Reception);
        var therapist = await _kit.AnswerAsync(spa, "Wer ist unser Steuerberater?", role: StaffRole.Therapist);

        foreach (var allowed in new[] { owner, location, backoffice })
        {
            Assert.Equal(AssistantBlockKind.Answer, allowed.Kind);
            Assert.StartsWith("Lohnverrechnung: Lohn & Gehalt Muster OG, Ansprechperson Petra Lohn.", allowed.Text, StringComparison.Ordinal);
        }

        foreach (var denied in new[] { reception, therapist })
        {
            Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
            Assert.StartsWith("Dafür fehlt die Berechtigung.", denied.Text, StringComparison.Ordinal);
            Assert.Null(denied.Table);
            Assert.DoesNotContain("Petra", denied.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Mit_gemischten_Treffern_zeigt_die_Rezeption_nur_die_Stufe_Team()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var reception = await _kit.AnswerAsync(spa, "Nummer der Versicherung", role: StaffRole.Reception);

        // Die Haftpflicht (Leitung) bleibt unerwähnt; übrig ist die Inventarversicherung als einziger Treffer.
        Assert.Equal(AssistantBlockKind.Answer, reception.Kind);
        Assert.StartsWith("Inventarversicherung: Sicher & Sound Versicherungen.", reception.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("POL-123456", reception.Copy + reception.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Treffer_steht_Dazu_ist_nichts_hinterlegt_mit_Link_fuer_die_Standortleitung_sonst_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var owner = await _kit.AnswerAsync(spa, "Wer ist unser Rauchfangkehrer?");
        var reception = await _kit.AnswerAsync(spa, "Wer ist unser Rauchfangkehrer?", role: StaffRole.Reception);
        var noCategory = await _kit.AnswerAsync(spa, "Wer ist für die Sauna zuständig?");

        Assert.Equal(AssistantBlockKind.NotStored, owner.Kind);
        Assert.Equal("Dazu ist nichts hinterlegt: „Rauchfangkehrer“.", owner.Text);
        Assert.Equal(("Betriebsinformationen öffnen", "businessInfo"), (owner.Link!.Label, owner.Link.Page));
        Assert.Equal("Dazu ist nichts hinterlegt: „Rauchfangkehrer“. Eintragen kann das die Standortleitung.", reception.Text);
        Assert.Equal("businessInfo", reception.Link!.Page);
        Assert.Equal("Dazu ist nichts hinterlegt: „Sauna“.", noCategory.Text);
    }

    [Fact]
    public async Task Archivierte_und_Eintraege_anderer_Standorte_kommen_nicht_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        await spa.PostAsync<BusinessContactDto>("/api/business-contacts", BusinessContactTests.Input("Installateur Wien", all: false, locations: [spa.Location.Id]), Ct);
        await spa.PostAsync<BusinessContactDto>("/api/business-contacts", BusinessContactTests.Input("Installateur Graz", all: false, locations: [other.Id]), Ct);
        var old = await spa.PostAsync<BusinessContactDto>("/api/business-contacts", BusinessContactTests.Input("Installateur alt"), Ct);
        await spa.PostAsync<BusinessContactDto>($"/api/business-contacts/{old.Id}/archive", new { }, Ct);

        var wien = await _kit.AnswerAsync(spa, "Wer ist unser Installateur?");
        var graz = await _kit.AnswerAsync(spa, "Wer ist unser Installateur?", locationId: other.Id);

        Assert.StartsWith("Installateur Wien:", wien.Text, StringComparison.Ordinal);
        Assert.StartsWith("Installateur Graz:", graz.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_Frage_ohne_Stichwort_bittet_um_eine_Angabe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);

        var block = await _kit.AnswerAsync(spa, "Wer ist zuständig?");

        Assert.Equal(AssistantBlockKind.NotFound, block.Kind);
        Assert.StartsWith("Bitte nennen, wofür oder wen", block.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ueber_die_API_antwortet_der_Assistent_dem_Backoffice_und_der_Rezeption_nach_ihren_Rechten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SeedAsync(spa);
        using var backoffice = await _kit.JoinAsync(spa, StaffRole.Backoffice);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        var accounting = await AnswerKit.BlockAsync(backoffice, "Wer kümmert sich um die Buchhaltung?", spa.Location.Id);
        var payroll = await AnswerKit.BlockAsync(backoffice, "Kontakt der Lohnverrechnung", spa.Location.Id);
        var denied = await AnswerKit.BlockAsync(reception, "Kontakt der Lohnverrechnung", spa.Location.Id);

        Assert.StartsWith("Buchhaltung: Buchführung Muster, Ansprechperson Bernd Bilanz.", accounting.Text, StringComparison.Ordinal);
        Assert.StartsWith("Lohnverrechnung: Lohn & Gehalt Muster OG", payroll.Text, StringComparison.Ordinal);
        Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
    }
}

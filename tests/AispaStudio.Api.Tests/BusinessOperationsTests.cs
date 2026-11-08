using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Betriebsfragen zum Standort (ADR 0072, Szenario 1): Öffnungszeiten, Kontaktdaten, Feiertage nach Land und Region, Anreise und Stornoregel, jeweils aus den
/// Einstellungen des Standorts, mit Quelle, mit Hinweis und Verweis auf die Einstellung, wenn das Feld fehlt. Alles regelbasiert, ohne KI-Schlüssel.
/// </summary>
public sealed class BusinessOperationsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    // ---------- Öffnungszeiten ----------

    [Fact]
    public async Task Oeffnungszeiten_nennen_Tag_Zeiten_und_Quelle_und_Sonntag_ist_geschlossen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var saturday = await _kit.AnswerAsync(spa, "Haben wir am Samstag offen?");
        Assert.Equal((AssistantBlockKind.Answer, "OpeningHours"), (saturday.Kind, saturday.Topic));
        Assert.Equal("Ja, am Samstag, 06.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", saturday.Text);
        Assert.Equal("Öffnungszeiten des Standorts Wien", saturday.Source);

        var sunday = await _kit.AnswerAsync(spa, "Haben wir am Sonntag offen?");
        Assert.StartsWith("Nein, am Sonntag, 07.03.2027 ist geschlossen", sunday.Text, StringComparison.Ordinal);

        var closing = await _kit.AnswerAsync(spa, "Wann sperren wir morgen zu?");
        Assert.Equal("Am Donnerstag, 04.03.2027 wird um 20:00 Uhr zugesperrt (geöffnet 09:00 bis 20:00 Uhr, 11 Stunden).", closing.Text);

        var length = await _kit.AnswerAsync(spa, "Wie lange haben wir heute offen?");
        Assert.Equal("Am Mittwoch, 03.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", length.Text);
    }

    [Fact]
    public async Task Oeffnungszeiten_der_Woche_stehen_in_einer_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var week = await _kit.AnswerAsync(spa, "Was sind unsere Öffnungszeiten?");

        Assert.Equal(["Wochentag", "Geöffnet"], week.Table!.Columns);
        Assert.Equal(7, week.Table.Rows.Count);
        Assert.Equal(["Montag", "09:00 bis 20:00"], week.Table.Rows[0]);
        Assert.Equal(["Sonntag", "geschlossen"], week.Table.Rows[6]);
    }

    [Fact]
    public async Task Am_Feiertag_nennt_die_Antwort_den_Feiertag_und_dass_wie_an_einem_Werktag_geoeffnet_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        // Allerheiligen 2027 ist ein Montag; das Wiener Spa hat montags offen.
        var block = await _kit.AnswerAsync(spa, "Ist am 1. November offen?");

        Assert.Contains("ist von 09:00 bis 20:00 Uhr geöffnet", block.Text, StringComparison.Ordinal);
        Assert.Contains("gesetzlicher Feiertag (Allerheiligen)", block.Text, StringComparison.Ordinal);
        Assert.Contains("geöffnet wie an einem Montag, sofern keine Schließzeit eingetragen ist", block.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlen_die_Oeffnungszeiten_sagt_die_Antwort_es_und_verweist_auf_die_Einstellung_nur_mit_Recht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var empty = await AnswerKit.LocationAsync(spa, "Ohne Zeiten", null, withHours: false);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        var owner = await AnswerKit.BlockAsync(spa.Client, "Wie lange haben wir heute offen?", empty.Id);
        var asked = await AnswerKit.BlockAsync(reception, "Wie lange haben wir heute offen?", empty.Id);

        Assert.Equal(AssistantBlockKind.NotStored, owner.Kind);
        Assert.StartsWith("Dazu ist nichts hinterlegt: Öffnungszeiten.", owner.Text, StringComparison.Ordinal);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Öffnungszeiten eintragen", "masterData", "locations", empty.Id, Field: "location.openingHours"), owner.Link);
        Assert.Equal(AssistantBlockKind.NotStored, asked.Kind);
        Assert.Null(asked.Link);
        Assert.Contains("Eintragen kann das die Standortleitung", asked.Text, StringComparison.Ordinal);
    }

    // ---------- Kontakt und Anreise ----------

    [Fact]
    public async Task Kontaktdaten_kommen_aus_dem_Standort_mit_Kopiertext()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Wien Innere Stadt", "AT-9",
            new LocationContact("Graben 1", "1010", "Wien", "+43 1 5120000", "wien@spa.example"));

        var all = await AnswerKit.BlockAsync(spa.Client, "Unsere Kontaktdaten", location.Id);
        var phone = await AnswerKit.BlockAsync(spa.Client, "Wie lautet unsere Telefonnummer?", location.Id);
        var mail = await AnswerKit.BlockAsync(spa.Client, "Welche Mailadresse haben wir?", location.Id);
        var address = await AnswerKit.BlockAsync(spa.Client, "Wie ist unsere Adresse?", location.Id);

        Assert.Equal("Kontaktdaten des Standorts Wien Innere Stadt: Telefon +43 1 5120000; E-Mail wien@spa.example; Adresse Graben 1, 1010 Wien.", all.Text);
        Assert.Equal("Kontaktdaten des Standorts Wien Innere Stadt", all.Source);
        Assert.Equal("Wien Innere Stadt\nTelefon: +43 1 5120000\nE-Mail: wien@spa.example\nAdresse: Graben 1, 1010 Wien", all.Copy);
        Assert.Equal(3, all.Table!.Rows.Count);
        Assert.Equal(("Telefon des Standorts Wien Innere Stadt: +43 1 5120000", "+43 1 5120000"), (phone.Text, phone.Copy));
        Assert.Equal("wien@spa.example", mail.Copy);
        Assert.Equal("Graben 1, 1010 Wien", address.Copy);
    }

    [Fact]
    public async Task Fehlt_ein_Feld_der_Kontaktdaten_steht_es_unter_Nicht_hinterlegt_mit_Link()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Salzburg", "AT-5", new LocationContact(null, null, null, "+43 662 1", null));

        var block = await AnswerKit.BlockAsync(spa.Client, "Unsere Kontaktdaten", location.Id);
        var mail = await AnswerKit.BlockAsync(spa.Client, "Welche Mailadresse haben wir?", location.Id);

        Assert.Equal(AssistantBlockKind.Answer, block.Kind);
        Assert.Contains("Nicht hinterlegt: E-Mail, Adresse.", block.Text, StringComparison.Ordinal);
        Assert.Equal("Kontaktdaten eintragen", block.Link!.Label);
        Assert.Equal(AssistantBlockKind.NotStored, mail.Kind);
        Assert.Equal("Dazu ist nichts hinterlegt: E-Mail.", mail.Text);
        Assert.Equal(("masterData", "locations", location.Id), (mail.Link!.Page, mail.Link.Tab, mail.Link.LocationId));
    }

    [Fact]
    public async Task Anreise_und_Parkplaetze_kommen_aus_dem_Freitext_am_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var without = await AnswerKit.BlockAsync(spa.Client, "Wie kommt der Gast zu uns, Parkplätze?", spa.Location.Id);
        var contact = new LocationContact("Graben 1", "1010", "Wien", null, null, "U1 bis Stephansplatz; Garage Am Graben, 2 Minuten zu Fuß.");
        var location = await AnswerKit.LocationAsync(spa, "Wien Mitte", "AT-9", contact);

        var block = await AnswerKit.BlockAsync(spa.Client, "Wie kommt der Gast zu uns, Parkplätze?", location.Id);
        var stored = await spa.GetAsync<List<LocationDto>>("/api/locations", Ct);

        Assert.Equal(AssistantBlockKind.NotStored, without.Kind);
        Assert.Equal("Dazu ist nichts hinterlegt: Anreise und Parkplätze.", without.Text);
        Assert.Equal("Anreise eintragen", without.Link!.Label);
        Assert.Equal("Anreise zum Standort Wien Mitte: U1 bis Stephansplatz; Garage Am Graben, 2 Minuten zu Fuß.", block.Text);
        Assert.Equal("Feld „Anreise“ des Standorts Wien Mitte", block.Source);
        Assert.Equal(contact.Directions, block.Copy);
        Assert.Equal(contact.Directions, stored.Single(l => l.Id == location.Id).Contact!.Directions);
    }

    [Fact]
    public async Task Die_Anreise_darf_hoechstens_500_Zeichen_lang_sein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var input = new LocationInput("Lang", "Europe/Vienna", "EUR", [], null, new LocationContact(null, null, null, null, null, new string('x', 501)));

        var response = await spa.Client.PostAsJsonAsync("/api/locations", input, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Stornoregel ----------

    [Fact]
    public async Task Stornoregel_kommt_aus_den_Buchungseinstellungen_mit_Freitext_und_festen_Werten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var none = await AnswerKit.BlockAsync(spa.Client, "Was ist unsere Stornoregel?", spa.Location.Id);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 0, false, 30, "Absage bis 24 Stunden vorher ist kostenlos."), Ct);

        var block = await AnswerKit.BlockAsync(spa.Client, "Was ist unsere Stornoregel?", spa.Location.Id);

        Assert.Equal(AssistantBlockKind.NotStored, none.Kind);
        Assert.Equal("Stornoregel eintragen", none.Link!.Label);
        Assert.Equal("Stornoregel des Standorts Wien: Absage bis 24 Stunden vorher ist kostenlos.", block.Text);
        Assert.Equal("Buchungseinstellungen des Standorts Wien", block.Source);
        Assert.Equal(
            [["Absage online", "bis 24 Stunden vor dem Termin"], ["Ausfallgebühr", "50 % des Preises bei Nichterscheinen oder später Absage"], ["Anzahlung", "30 % des Preises beim Online-Buchen"]],
            block.Table!.Rows);
    }

    [Fact]
    public async Task Die_Stornoregel_als_Freitext_wird_gespeichert_und_ist_auf_500_Zeichen_begrenzt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/locations/{spa.Location.Id}/online-rules";

        var saved = await spa.PutAsync<LocationOnlineRules>(path, new LocationOnlineRules(true, 60, 365, 24, 15, null, false, CancellationPolicy: "  Bis 24 Stunden vorher kostenlos.  "), Ct);
        var tooLong = await spa.Client.PutAsJsonAsync(path, new LocationOnlineRules(true, 60, 365, 24, 15, null, false, CancellationPolicy: new string('x', 501)), TestSpa.Json, Ct);

        Assert.Equal("Bis 24 Stunden vorher kostenlos.", saved.CancellationPolicy);
        Assert.Equal("Bis 24 Stunden vorher kostenlos.", (await spa.GetAsync<LocationOnlineRules>(path, Ct)).CancellationPolicy);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    // ---------- Feiertage ----------

    [Theory]
    [InlineData("AT-9", "Ist der 4. Juni 2026 ein Feiertag?", "Ja, am Donnerstag, 04.06.2026 ist Fronleichnam")]
    [InlineData("CH-ZH", "Ist der 4. Juni 2026 ein Feiertag?", "Nein, am Donnerstag, 04.06.2026 ist in Zürich (Schweiz) kein gesetzlicher Feiertag")]
    [InlineData("DE-BY", "Ist der 4. Juni 2026 ein Feiertag?", "Ja, am Donnerstag, 04.06.2026 ist Fronleichnam")]
    [InlineData("DE-BE", "Ist der 4. Juni 2026 ein Feiertag?", "Nein, am Donnerstag, 04.06.2026 ist in Berlin (Deutschland) kein gesetzlicher Feiertag")]
    [InlineData("AT-9", "Ist der 29.3.2027 ein Feiertag?", "Ja, am Montag, 29.03.2027 ist Ostermontag")]
    [InlineData("CH-ZH", "Ist der 29.3.2027 ein Feiertag?", "Ja, am Montag, 29.03.2027 ist Ostermontag")]
    [InlineData("DE-BB", "Ist der 31. Oktober 2027 ein Feiertag?", "Ja, am Sonntag, 31.10.2027 ist Reformationstag")]
    [InlineData("DE-BY", "Ist der 31. Oktober 2027 ein Feiertag?", "Nein, am Sonntag, 31.10.2027 ist in Bayern (Deutschland) kein gesetzlicher Feiertag")]
    [InlineData("DE-NW", "Ist der 31. Oktober 2027 ein Feiertag?", "Nein, am Sonntag, 31.10.2027 ist in Nordrhein-Westfalen (Deutschland) kein gesetzlicher Feiertag")]
    [InlineData("DE-HH", "Ist der 31. Oktober 2027 ein Feiertag?", "Ja, am Sonntag, 31.10.2027 ist Reformationstag")]
    [InlineData("AT-9", "Ist der 26. Oktober 2027 ein Feiertag?", "Ja, am Dienstag, 26.10.2027 ist Nationalfeiertag")]
    public async Task Feiertage_richten_sich_nach_Land_und_Region_des_Standorts(string region, string question, string expected)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, $"Spa {region}", region);

        var block = await _kit.AnswerAsync(spa, question, locationId: location.Id);

        Assert.StartsWith(expected, block.Text, StringComparison.Ordinal);
        Assert.Contains("Feiertagstabelle für", block.Source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_naechste_Feiertag_kommt_mit_Abstand_und_den_folgenden_Feiertagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");

        // Mittwoch, 3. März 2027: der nächste Feiertag in Wien ist Ostermontag, 29. März.
        var block = await _kit.AnswerAsync(spa, "Wann ist der nächste Feiertag?", locationId: location.Id);

        Assert.StartsWith("Der nächste Feiertag in Wien (Österreich) ist Ostermontag, Montag, 29.03.2027 (in 26 Tagen).", block.Text, StringComparison.Ordinal);
        Assert.Equal(["Montag, 29.03.2027", "Ostermontag"], block.Table!.Rows[0]);
        Assert.Equal(["Samstag, 01.05.2027", "Staatsfeiertag"], block.Table.Rows[1]);
        Assert.Equal(5, block.Table.Rows.Count);
    }

    [Fact]
    public async Task Feiertage_eines_Monats_und_ohne_Region_nur_die_landesweiten_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var wien = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");
        var ohne = await AnswerKit.LocationAsync(spa, "Ohne Region", null);

        var december = await _kit.AnswerAsync(spa, "Feiertage im Dezember", locationId: wien.Id);
        var noRegion = await _kit.AnswerAsync(spa, "Ist der 1. November ein Feiertag?", locationId: ohne.Id);

        Assert.Equal(["Mittwoch, 08.12.2027", "Mariä Empfängnis"], december.Table!.Rows[0]);
        Assert.Equal(3, december.Table.Rows.Count);
        Assert.Equal("Dezember 2027", december.Period);
        Assert.Contains("kein Bundesland oder Kanton eingetragen", noRegion.Text, StringComparison.Ordinal);
        Assert.StartsWith("Ja, am Montag, 01.11.2027 ist Allerheiligen", noRegion.Text, StringComparison.Ordinal);
        Assert.Equal("Bundesland oder Kanton eintragen", noRegion.Link!.Label);
    }

    [Fact]
    public async Task Das_Backoffice_bekommt_keine_Betriebsfragen_beantwortet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var backoffice = await _kit.JoinAsync(spa, StaffRole.Backoffice);

        var block = await AnswerKit.BlockAsync(backoffice, "Wie lange haben wir heute offen?", spa.Location.Id);

        Assert.Equal(AssistantBlockKind.Denied, block.Kind);
        Assert.StartsWith("Dafür fehlt die Berechtigung.", block.Text, StringComparison.Ordinal);
        Assert.Empty(block.FollowUps);
    }
}

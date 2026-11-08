using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Abschnitt „Unvollständig“ der Antwortblöcke (ADR 0072, Nachtrag Runde 5b, Ä44): fehlt dem Spa eine Angabe zum Thema, obwohl die Antwort möglich war, nennt der Block
/// sie mit der Maske, in der sie steht, und mit einem Verweis, der die Maske am Feld öffnet. Ohne das Recht, die Maske zu ändern, steht „Nur Leitung“ statt des Verweises.
/// Die Verweise entstehen alle aus der zentralen Tabelle <see cref="FieldMasks"/>.
/// </summary>
public sealed class BusinessIncompleteTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    // ---------- Tabelle ----------

    [Fact]
    public void Jede_Feldkennung_steht_in_der_Tabelle_mit_Seite_Ort_und_Recht()
    {
        var ids = typeof(FieldIds).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();

        Assert.Equal(ids.Order(StringComparer.Ordinal), FieldMasks.All.Select(m => m.Id).Order(StringComparer.Ordinal));
        Assert.All(FieldMasks.All, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Page));
            // Die Maske liegt in den Stammdaten oder ist eine eigene Seite (Aktionen, Betriebsinformationen) und trägt ihren Namen.
            Assert.False(string.IsNullOrWhiteSpace(m.Where));
            if (m.Page == "masterData")
            {
                Assert.StartsWith("Stammdaten › ", m.Where, StringComparison.Ordinal);
            }
        });
        Assert.Throws<ArgumentException>(() => FieldMasks.Get("location.unbekannt"));
    }

    [Fact]
    public async Task Jeder_Antwortgeber_nennt_nur_Feldkennungen_der_Tabelle()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var givers = scope.ServiceProvider.GetServices<IAnswerGiver>().ToList();
        var known = FieldMasks.All.Select(m => m.Id).ToHashSet();

        foreach (var giver in givers)
        {
            foreach (var topic in giver.Topics)
            {
                var ids = giver.Fields(new BusinessQuestion(topic, "", null, null, null));
                Assert.All(ids, id => Assert.Contains(id, known));
            }
        }

        // Themen aus den Einstellungen melden Felder; Themen aus Kalender, Kassa und Berichten brauchen keine.
        var withFields = givers.SelectMany(g => g.Topics.Where(t => g.Fields(new BusinessQuestion(t, "", null, null, null)).Count > 0)).ToHashSet();
        Assert.Equal(
            [BusinessTopic.OpeningHours, BusinessTopic.Contact, BusinessTopic.Holiday, BusinessTopic.Directions, BusinessTopic.CancellationRule, BusinessTopic.Promotions],
            withFields.Order());
    }

    // ---------- Standort ----------

    [Fact]
    public async Task Oeffnungszeiten_ohne_Bundesland_nennen_die_Luecke_und_fuehren_zum_Feld()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Graz", null);

        var owner = await _kit.AnswerAsync(spa, "Wie lange haben wir heute offen?", locationId: location.Id);
        var reception = await _kit.AnswerAsync(spa, "Wie lange haben wir heute offen?", locationId: location.Id, role: StaffRole.Reception);

        Assert.Equal(AssistantBlockKind.Answer, owner.Kind);
        var gap = Assert.Single(owner.Incomplete!);
        Assert.Equal("Unvollständig: Bundesland oder Kanton fehlt beim Standort Graz.", gap.Text);
        Assert.Equal("Stammdaten › Standorte › Bundesland / Kanton", gap.Where);
        Assert.Equal(["location.region"], gap.Fields);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "locations", location.Id, Field: "location.region"), gap.Link);
        Assert.Null(gap.Hint);

        var hint = Assert.Single(reception.Incomplete!);
        Assert.Equal(gap.Text, hint.Text);
        Assert.Null(hint.Link);
        Assert.Equal("Nur Leitung", hint.Hint);
    }

    [Fact]
    public async Task Vollstaendige_Angaben_haben_keinen_Abschnitt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");

        var block = await _kit.AnswerAsync(spa, "Wie lange haben wir heute offen?", locationId: location.Id);
        var team = await _kit.AnswerAsync(spa, "Wer arbeitet morgen?", locationId: location.Id);

        Assert.Equal(AssistantBlockKind.Answer, block.Kind);
        Assert.True(block.Incomplete is null or { Count: 0 });
        Assert.True(team.Incomplete is null or { Count: 0 });
    }

    [Fact]
    public async Task Kontaktdaten_ohne_Anreise_und_Website_nennen_beides_mit_je_einer_Maske()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Wien", "AT-9", new LocationContact("Graben 1", "1010", "Wien", "+43 1 5120000", "wien@spa.example"));

        var block = await _kit.AnswerAsync(spa, "Unsere Kontaktdaten", locationId: location.Id);
        var reception = await _kit.AnswerAsync(spa, "Unsere Kontaktdaten", locationId: location.Id, role: StaffRole.Reception);

        Assert.Equal(["Unvollständig: Anreise fehlt beim Standort Wien.", "Unvollständig: Website fehlt in den Firmendaten."], block.Incomplete!.Select(g => g.Text));
        Assert.Equal(["Stammdaten › Standorte › Anreise und Parkplätze", "Stammdaten › Firmendaten › Sitz und Kontakt"], block.Incomplete!.Select(g => g.Where));
        Assert.Equal(("masterData", "locations", "location.directions"), (block.Incomplete![0].Link!.Page, block.Incomplete[0].Link!.Tab, block.Incomplete[0].Link!.Field));
        Assert.Equal(("masterData", "company", "company.website"), (block.Incomplete[1].Link!.Page, block.Incomplete[1].Link!.Tab, block.Incomplete[1].Link!.Field));
        Assert.All(reception.Incomplete!, g =>
        {
            Assert.Null(g.Link);
            Assert.Equal("Nur Leitung", g.Hint);
        });
    }

    [Fact]
    public async Task Fehlende_Kontaktfelder_mit_Verweis_der_Antwort_stehen_nicht_doppelt_im_Abschnitt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Salzburg", "AT-5", new LocationContact(null, null, null, "+43 662 1", null, "Bus 3."));

        var block = await _kit.AnswerAsync(spa, "Unsere Kontaktdaten", locationId: location.Id);

        // E-Mail und Adresse fehlen: der Verweis „Kontaktdaten eintragen“ der Antwort führt schon zum ersten fehlenden Feld.
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Kontaktdaten eintragen", "masterData", "locations", location.Id, Field: "location.email"), block.Link);
        Assert.Equal(["Unvollständig: Website fehlt in den Firmendaten."], block.Incomplete!.Select(g => g.Text));
    }

    [Fact]
    public async Task Die_Anreise_nennt_die_fehlende_Adresse()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Wien Mitte", "AT-9", new LocationContact(null, null, null, null, null, "U1 bis Stephansplatz."));

        var block = await _kit.AnswerAsync(spa, "Wie kommt der Gast zu uns, Parkplätze?", locationId: location.Id);

        Assert.Equal(["Unvollständig: Adresse fehlt beim Standort Wien Mitte."], block.Incomplete!.Select(g => g.Text));
        Assert.Equal("location.street", Assert.Single(block.Incomplete!).Link!.Field);
    }

    [Fact]
    public async Task Nichts_hinterlegt_bekommt_denselben_Verweis_aus_der_Tabelle_mit_Feld()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var directions = await _kit.AnswerAsync(spa, "Wie kommt der Gast zu uns, Parkplätze?");
        var cancellation = await _kit.AnswerAsync(spa, "Was ist unsere Stornoregel?");

        Assert.Equal(AssistantBlockKind.NotStored, directions.Kind);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Anreise eintragen", "masterData", "locations", spa.Location.Id, Field: "location.directions"), directions.Link);
        Assert.Equal("location.cancellationPolicy", cancellation.Link!.Field);
        Assert.Null(directions.Incomplete);
    }

    [Fact]
    public async Task Feiertage_ohne_Region_ohne_Recht_zeigen_den_Hinweis_Nur_Leitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Ohne Region", null);

        var owner = await _kit.AnswerAsync(spa, "Wann ist der nächste Feiertag?", locationId: location.Id);
        var reception = await _kit.AnswerAsync(spa, "Wann ist der nächste Feiertag?", locationId: location.Id, role: StaffRole.Reception);

        Assert.Equal("location.region", owner.Link!.Field);
        Assert.True(owner.Incomplete is null or { Count: 0 });
        Assert.Null(reception.Link);
        Assert.Equal("Nur Leitung", Assert.Single(reception.Incomplete!).Hint);
    }

    [Fact]
    public async Task Stornoregel_mit_festen_Werten_aber_ohne_Freitext_nennt_den_Freitext()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50, 0, false, 30, null), Ct);

        var reception = await _kit.AnswerAsync(spa, "Was ist unsere Stornoregel?", role: StaffRole.Reception);

        Assert.Equal(AssistantBlockKind.Answer, reception.Kind);
        var gap = Assert.Single(reception.Incomplete!);
        Assert.Equal("Unvollständig: Stornoregel fehlt beim Standort Wien.", gap.Text);
        Assert.Equal("Stammdaten › Standorte › Online-Buchung", gap.Where);
    }

    // ---------- Katalog und Aktionen ----------

    [Fact]
    public async Task Behandlung_ohne_Preis_nennt_die_Luecke_und_fuehrt_zur_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var free = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Kennenlernen", 30, 0, 0, 0m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);

        var owner = await _kit.AnswerAsync(spa, "Was kostet Kennenlernen?");
        var reception = await _kit.AnswerAsync(spa, "Was kostet Kennenlernen?", role: StaffRole.Reception);
        var withPrice = await _kit.AnswerAsync(spa, "Was kostet eine Massage?");

        var gap = Assert.Single(owner.Incomplete!);
        Assert.Equal("Unvollständig: Preis fehlt bei Kennenlernen.", gap.Text);
        Assert.Equal("Stammdaten › Behandlungen › Grunddaten", gap.Where);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "treatments", spa.Location.Id, TargetId: free.Id, Field: "treatment.price"), gap.Link);
        Assert.Null(Assert.Single(reception.Incomplete!).Link);
        Assert.True(withPrice.Incomplete is null or { Count: 0 });
    }

    [Fact]
    public async Task Ohne_jede_Aktion_nennt_die_Antwort_die_Luecke_fuer_wen_den_Verweis_nicht_hat()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var owner = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?");
        var reception = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?", role: StaffRole.Reception);

        // Der Verweis „Aktionen verwalten“ der Antwort führt schon dorthin.
        Assert.Equal("promotions.any", owner.Link!.Field);
        Assert.True(owner.Incomplete is null or { Count: 0 });
        Assert.Equal(["Unvollständig: Aktionen fehlen beim Standort Wien."], reception.Incomplete!.Select(g => g.Text));
        Assert.Equal("Nur Leitung", reception.Incomplete![0].Hint);
    }

    // ---------- über die API ----------

    [Fact]
    public async Task Der_Abschnitt_kommt_ueber_die_API_beim_Client_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = await AnswerKit.LocationAsync(spa, "Graz", null);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        var block = await AnswerKit.BlockAsync(reception, "Wie lange haben wir heute offen?", location.Id);

        var gap = Assert.Single(block.Incomplete!);
        Assert.Equal("Unvollständig: Bundesland oder Kanton fehlt beim Standort Graz.", gap.Text);
        Assert.Equal("Nur Leitung", gap.Hint);
        Assert.Equal(["location.region"], gap.Fields);
    }
}

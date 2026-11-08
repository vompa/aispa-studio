using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Fragen zu Aktionen und Rabatten (ADR 0072, Szenario 4): laufende Aktionen heute, in einem Monat, mit Paketen, nach Standort; aus dem Fachbereich Aktionen
/// (ADR 0071). „Heute“ ist Mittwoch, der 3. März 2027.
/// </summary>
public sealed class BusinessPromotionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static Task<PromotionDto> CreateAsync(TestSpa spa, string name, string from, string until, decimal? percent = 10m, decimal? amount = null, bool all = true,
        Guid[]? locations = null, PromotionScope scope = PromotionScope.AllTreatments, Guid[]? treatments = null, bool active = true) =>
        spa.PostAsync<PromotionDto>("/api/promotions", new PromotionInput(name, $"Gästetext {name}", DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(until, "yyyy-MM-dd", CultureInfo.InvariantCulture), all, locations ?? [], amount is null ? percent : null, amount, scope, treatments ?? [], active), Ct);

    [Fact]
    public async Task Welche_Aktionen_gerade_laufen_nennt_Rabatt_Gueltigkeit_und_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, "Frühlingszauber", "2027-03-01", "2027-03-31");
        await CreateAsync(spa, "Alt", "2027-01-01", "2027-01-31");
        await CreateAsync(spa, "Pausiert", "2027-03-01", "2027-03-31", active: false);
        await CreateAsync(spa, "Fünfer", "2027-03-02", "2027-03-10", percent: null, amount: 5m, scope: PromotionScope.SelectedTreatments, treatments: [spa.MassageTreatment.Id]);

        var block = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?");

        Assert.Equal("Heute laufen im Standort Wien 2 Aktionen: Fünfer (−5,00 €), Frühlingszauber (−10 %).", block.Text);
        Assert.Equal("Aktionen des Standorts Wien", block.Source);
        Assert.Equal(["Aktion", "Rabatt", "Gilt für", "Zeitraum"], block.Table!.Columns);
        Assert.Equal(["Fünfer", "−5,00 €", "Massage", "02.03. bis 10.03.2027"], block.Table.Rows[0]);
        Assert.Equal(["Frühlingszauber", "−10 %", "alle Behandlungen", "01.03. bis 31.03.2027"], block.Table.Rows[1]);
        Assert.Equal("Aktionen verwalten", block.Link!.Label);
        Assert.Null(block.Period);
    }

    [Fact]
    public async Task Eine_einzelne_Aktion_wird_mit_dem_Gaestetext_genannt_und_ohne_Aktion_steht_es_da()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var empty = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?");
        await CreateAsync(spa, "Frühlingszauber", "2027-03-01", "2027-03-31");

        var one = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?");

        Assert.Equal("Heute läuft im Standort Wien keine Aktion.", empty.Text);
        Assert.Equal("Heute läuft im Standort Wien die Aktion Frühlingszauber: Gästetext Frühlingszauber", one.Text);
    }

    [Fact]
    public async Task Aktionen_in_einem_Monat_sind_die_des_kommenden_Monats_und_nur_die_am_Standort_gueltigen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await AnswerKit.LocationAsync(spa, "Graz", "AT-6");
        await CreateAsync(spa, "Novemberzauber", "2027-11-01", "2027-11-30");
        await CreateAsync(spa, "Nur Graz", "2027-11-10", "2027-11-20", all: false, locations: [graz.Id]);
        await CreateAsync(spa, "Frühling", "2027-03-01", "2027-03-31");

        var wien = await _kit.AnswerAsync(spa, "Gibt es im November ein Angebot?");
        var inGraz = await _kit.AnswerAsync(spa, "Gibt es im November ein Angebot?", locationId: graz.Id);
        var december = await _kit.AnswerAsync(spa, "Gibt es im Dezember ein Angebot?");

        Assert.Equal("Im Zeitraum November 2027 gilt im Standort Wien die Aktion Novemberzauber: Gästetext Novemberzauber", wien.Text);
        Assert.Equal("November 2027", wien.Period);
        Assert.Equal(["Novemberzauber", "Nur Graz"], inGraz.Table!.Rows.Select(r => r[0]).Order(StringComparer.Ordinal));
        Assert.Equal("Im Zeitraum Dezember 2027 ist im Standort Wien noch keine Aktion eingetragen.", december.Text);
    }

    [Fact]
    public async Task Gilt_die_Aktion_auch_fuer_Pakete_antwortet_nach_dem_Geltungsbereich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, "Behandlungsaktion", "2027-03-01", "2027-03-31");
        var none = await _kit.AnswerAsync(spa, "Gilt die Aktion auch für Pakete?");
        await CreateAsync(spa, "Paketwoche", "2027-03-01", "2027-03-31", scope: PromotionScope.SelectedPackages, treatments: [spa.MassageTreatment.Id]);

        var yes = await _kit.AnswerAsync(spa, "Gilt die Aktion auch für Pakete?");

        Assert.Equal("Nein, heute gilt keine Aktion für Pakete. Es laufen 1, aber nur für Behandlungen: Behandlungsaktion.", none.Text);
        Assert.Equal("Ja, heute gilt im Standort Wien für Pakete: Paketwoche.", yes.Text);
        Assert.Equal(["Paketwoche", "−10 %", "Pakete: Massage", "01.03. bis 31.03.2027"], yes.Table!.Rows.Single());
    }

    [Fact]
    public async Task Der_Link_zur_Verwaltung_steht_nur_bei_Recht_Aktionen_zu_pflegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, "Frühlingszauber", "2027-03-01", "2027-03-31");

        var reception = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?", role: StaffRole.Reception);
        var owner = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?");
        var backoffice = await _kit.AnswerAsync(spa, "Welche Rabatte oder Aktionen laufen gerade?", role: StaffRole.Backoffice);

        Assert.Equal(AssistantBlockKind.Answer, reception.Kind);
        Assert.Null(reception.Link);
        Assert.Equal("promotions", owner.Link!.Page);
        Assert.Equal(AssistantBlockKind.Denied, backoffice.Kind);
    }
}

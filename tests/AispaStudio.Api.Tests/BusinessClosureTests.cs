using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Fragen zu Schließzeiten (ADR 0073, Szenario 1 des Assistenten): Öffnungszeiten an einem Tag mit Betriebsurlaub, halbe Tage, „Sind wir im Jänner geschlossen?“ und der nächste
/// Feiertag mit einer Schließzeit. „Heute“ ist Mittwoch, der 3. März 2027.
/// </summary>
public sealed class BusinessClosureTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Am_Tag_mit_Betriebsurlaub_antwortet_der_Assistent_mit_der_Schliessung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-08"), D("2027-03-10"), ClosureKind.Vacation, guestText: "Wir sind im Urlaub."));

        var monday = await _kit.AnswerAsync(spa, "Ist am Montag offen?");
        var tuesday = await _kit.AnswerAsync(spa, "Wie lange haben wir am Dienstag offen?");
        var thursday = await _kit.AnswerAsync(spa, "Haben wir am 11.3. offen?");

        Assert.Equal(AssistantBlockKind.Answer, monday.Kind);
        Assert.Equal("Nein, am Montag, 08.03.2027 ist geschlossen: Betriebsurlaub bis 10.3.", monday.Text);
        Assert.Equal("Öffnungszeiten und Schließzeiten des Standorts Wien", monday.Source);
        Assert.Equal(["Art", "Zeitraum", "Text für Gäste"], monday.Table!.Columns);
        Assert.Equal(["Betriebsurlaub", "8. März bis 10. März", "Wir sind im Urlaub."], monday.Table.Rows[0]);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Setting, "Schließzeiten verwalten", "masterData", "closures", spa.Location.Id), monday.Link);
        Assert.Equal("Am Dienstag, 09.03.2027 ist geschlossen: Betriebsurlaub bis 10.3.", tuesday.Text);
        // Nach dem Urlaub gilt wieder der Wochenplan, ohne Tabelle und Schließzeit-Quelle.
        Assert.Equal("Ja, am Donnerstag, 11.03.2027 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", thursday.Text);
        Assert.Equal("Öffnungszeiten des Standorts Wien", thursday.Source);
        Assert.Null(thursday.Table);
    }

    [Fact]
    public async Task Ein_halber_Tag_nennt_die_Zeiten_die_offen_bleiben_und_dann_die_Schliessung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-04"), kind: ClosureKind.Outage, fromTime: new TimeOnly(14, 0)));

        var block = await _kit.AnswerAsync(spa, "Wie lange haben wir morgen offen?");
        var closing = await _kit.AnswerAsync(spa, "Wann sperren wir morgen zu?");

        Assert.Equal("Am Donnerstag, 04.03.2027 ist von 09:00 bis 14:00 Uhr geöffnet (5 Stunden). Der Rest des Tages ist geschlossen: Ausfall ab 14:00 Uhr.", block.Text);
        Assert.Equal("Am Donnerstag, 04.03.2027 wird um 14:00 Uhr zugesperrt (geöffnet 09:00 bis 14:00 Uhr, 5 Stunden). Der Rest des Tages ist geschlossen: Ausfall ab 14:00 Uhr.", closing.Text);
    }

    [Fact]
    public async Task Sind_wir_im_Zeitraum_geschlossen_nennt_die_Schliesszeiten_oder_dass_keine_eingetragen_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-08"), D("2027-03-10")));
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-22"), kind: ClosureKind.SpecialClosure, guestText: "Brückentag."));
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2028-01-01"), D("2028-01-31"), ClosureKind.Vacation));

        var march = await _kit.AnswerAsync(spa, "Sind wir im März geschlossen?");
        var april = await _kit.AnswerAsync(spa, "Sind wir im April geschlossen?");
        var january = await _kit.AnswerAsync(spa, "Sind wir im Jänner geschlossen?");
        var vacation = await _kit.AnswerAsync(spa, "Haben wir im März Betriebsurlaub?");

        Assert.Equal("Teilweise: im Zeitraum März 2027 gibt es 2 Schließzeiten.", march.Text);
        Assert.Equal("Schließzeiten des Standorts Wien", march.Source);
        Assert.Equal("März 2027", march.Period);
        Assert.Equal([["Betriebsurlaub", "8. März bis 10. März", "Wegen Betriebsurlaub geschlossen."], ["Sonderschließung", "22. März", "Brückentag."]], march.Table!.Rows);
        Assert.Equal("Nein, im Zeitraum April 2027 ist keine Schließzeit eingetragen. Es gelten die Öffnungszeiten laut Wochenplan.", april.Text);
        Assert.Null(april.Table);
        Assert.Equal("Ja, im Zeitraum Jänner 2028 ist ganz geschlossen: Betriebsurlaub.", january.Text);
        Assert.Equal(march.Text, vacation.Text);
    }

    [Fact]
    public async Task Nur_wer_Schliesszeiten_verwalten_darf_bekommt_den_Verweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-08")));

        var owner = await _kit.AnswerAsync(spa, "Ist am Montag offen?");
        var reception = await _kit.AnswerAsync(spa, "Ist am Montag offen?", role: StaffRole.Reception);

        Assert.NotNull(owner.Link);
        Assert.Null(reception.Link);
        Assert.Equal(owner.Text, reception.Text);
    }

    [Fact]
    public async Task Der_naechste_Feiertag_nennt_eine_Schliesszeit_an_diesem_Tag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var without = await _kit.AnswerAsync(spa, "Wann ist der nächste Feiertag?");
        var date = DateOnly.ParseExact(without.Table!.Rows[0][0], "dddd, dd.MM.yyyy", CultureInfo.GetCultureInfo("de-AT"));
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(date, date.AddDays(1), ClosureKind.SpecialClosure));

        var next = await _kit.AnswerAsync(spa, "Wann ist der nächste Feiertag?");
        var onDay = await _kit.AnswerAsync(spa, $"Ist der {date:d.M.yyyy} ein Feiertag?");

        Assert.DoesNotContain("Schließzeit", without.Text, StringComparison.Ordinal);
        Assert.Contains(" An diesem Tag ist geschlossen: Sonderschließung bis ", next.Text, StringComparison.Ordinal);
        Assert.Contains("(Schließzeit des Standorts Wien)", next.Text, StringComparison.Ordinal);
        Assert.Contains(" An diesem Tag ist geschlossen: Sonderschließung bis ", onDay.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_Woche_mit_Schliesszeit_nennt_sie_unter_den_Oeffnungszeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var plain = await _kit.AnswerAsync(spa, "Was sind unsere Öffnungszeiten?");
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(D("2027-03-08"), D("2027-03-10")));

        var week = await _kit.AnswerAsync(spa, "Was sind unsere Öffnungszeiten?");

        Assert.Equal("Öffnungszeiten des Standorts Wien laut Wochenplan.", plain.Text);
        Assert.Equal("Öffnungszeiten des Standorts Wien laut Wochenplan. In den nächsten 14 Tagen gibt es eine Schließzeit: Betriebsurlaub, 8. März bis 10. März.", week.Text);
        Assert.Equal(7, week.Table!.Rows.Count);
    }
}

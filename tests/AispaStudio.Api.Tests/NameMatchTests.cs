using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Unscharfe Namen im Assistenten (Ä315): Anrede, Artikel, Titel und Füllwörter gehören nicht zum Namen, Schreibung zählt nicht, ein Tippfehler ab fünf Buchstaben
/// wird verziehen, genaue Treffer gehen vor, und die Anrede grenzt ein, wenn sie passt. Ohne Datenbank.
/// </summary>
public sealed class NameMatchTests
{
    [Theory]
    [InlineData("herr steiner", "steiner", CustomerSalutation.Male)]
    [InlineData("Hr. Steiner", "Steiner", CustomerSalutation.Male)]
    [InlineData("wann kommt der steiner", "steiner", CustomerSalutation.Male)]
    [InlineData("die moser", "moser", CustomerSalutation.Female)]
    [InlineData("Fr. Moser", "Moser", CustomerSalutation.Female)]
    [InlineData("wann hat herr steiner seinen termin", "steiner", CustomerSalutation.Male)]
    public void Anrede_und_Artikel_stehen_neben_dem_Namen(string text, string name, CustomerSalutation salutation)
    {
        var query = NameMatch.ParseName(text);

        Assert.Equal([name], query.Words);
        Assert.Equal(salutation, query.Salutation);
    }

    [Fact]
    public void Ohne_Anrede_bleibt_die_Anrede_offen()
    {
        var query = NameMatch.ParseName("STEINER");

        Assert.Equal(["STEINER"], query.Words);
        Assert.Null(query.Salutation);
    }

    [Fact]
    public void Titel_und_Fuellwoerter_gehoeren_nicht_zum_Namen()
    {
        var moser = NameMatch.ParseName("Wann war Frau Mag. Dr. Moser zuletzt da?");
        var pronoun = NameMatch.ParseName("wann war sie zuletzt da");

        Assert.Equal(["Moser"], moser.Words);
        Assert.Equal(CustomerSalutation.Female, moser.Salutation);
        Assert.True(pronoun.IsEmpty);
    }

    [Theory]
    [InlineData("steiner", 0)]
    [InlineData("STEINER", 0)]
    [InlineData("steinr", 1)]
    [InlineData("steiener", 1)]
    [InlineData("stiener", 1)]
    [InlineData("steimer", 1)]
    public void Ein_Tippfehler_ab_fuenf_Buchstaben_trifft(string word, int fit) =>
        Assert.Equal(fit, NameMatch.Fit([word], "Christian", "Steiner", null));

    [Theory]
    [InlineData("lina", "Lena")]
    [InlineData("bem", "Ben")]
    [InlineData("stainr", "Steiner")]
    public void Kurze_Woerter_und_zwei_Tippfehler_treffen_nicht(string word, string name) =>
        Assert.Null(NameMatch.Fit([word], name));

    [Theory]
    [InlineData("kathi", "Katharina", "Berger", "Kathi")]
    [InlineData("katharina", "Katharina", "Berger", "Kathi")]
    [InlineData("muller", "Brigitte", "Müller", null)]
    public void Rufname_Vorname_und_Umlaute_treffen_genau(string word, string first, string last, string? preferred) =>
        Assert.Equal(0, NameMatch.Fit([word], first, last, preferred));

    [Fact]
    public void Vor_und_Nachname_muessen_beide_passen()
    {
        Assert.Equal(0, NameMatch.Fit(["christian", "steiner"], "Christian", "Steiner"));
        Assert.Null(NameMatch.Fit(["eva", "steiner"], "Christian", "Steiner"));
    }

    [Fact]
    public void Person_mit_Tippfehler_im_Nachnamen_wird_gefunden() =>
        Assert.Equal(1, NameMatch.Fit(["huberr"], "Lena", "Lena Huber", null));

    [Fact]
    public void Genauer_Treffer_verdraengt_Treffer_mit_Tippfehler()
    {
        string[][] guests = [["Christian", "Steiner"], ["Paul", "Steimer"]];

        var best = NameMatch.Best(guests, g => NameMatch.Fit(["steiner"], g));

        Assert.Equal("Christian", Assert.Single(best)[0]);
    }

    [Fact]
    public void Ohne_genauen_Treffer_zaehlen_die_Tippfehler()
    {
        string[][] guests = [["Christian", "Steiner"], ["Paul", "Huber"]];

        var best = NameMatch.Best(guests, g => NameMatch.Fit(["steinr"], g));

        Assert.Equal("Christian", Assert.Single(best)[0]);
    }

    [Fact]
    public void Anrede_grenzt_auf_passende_Gaeste_ein()
    {
        List<(string Name, CustomerSalutation Salutation)> steiner = [("Christian", CustomerSalutation.Male), ("Eva", CustomerSalutation.Female)];

        var herr = NameMatch.BySalutation(steiner, CustomerSalutation.Male, g => g.Salutation);
        var frau = NameMatch.BySalutation(steiner, CustomerSalutation.Female, g => g.Salutation);

        Assert.Equal("Christian", Assert.Single(herr).Name);
        Assert.Equal("Eva", Assert.Single(frau).Name);
    }

    [Fact]
    public void Ohne_passende_Anrede_bleiben_alle()
    {
        List<(string Name, CustomerSalutation Salutation)> huber = [("Anna", CustomerSalutation.None), ("Berta", CustomerSalutation.None)];

        var herr = NameMatch.BySalutation(huber, CustomerSalutation.Male, g => g.Salutation);

        Assert.Equal(["Anna", "Berta"], herr.Select(g => g.Name));
    }
}

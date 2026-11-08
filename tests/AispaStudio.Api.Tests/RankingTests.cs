using System.Globalization;
using AispaStudio.Api.Assistant.Answers;

namespace AispaStudio.Api.Tests;

/// <summary>Ranglisten des Assistenten (Slice 139): gleiche Werte teilen den Platz und stehen im Kopfsatz als Gruppe statt als Aufzählung.</summary>
public sealed class RankingTests
{
    private static RankedEntry Treatment(string name, int appointments) =>
        new(name, appointments.ToString(CultureInfo.InvariantCulture), appointments.ToString(CultureInfo.InvariantCulture), appointments == 1 ? "1 Termin" : $"{appointments} Terminen");

    private static List<RankedEntry> Treatments(params int[] appointments) =>
        [.. appointments.Select((n, i) => Treatment($"Behandlung {i + 1}", n))];

    [Fact]
    public void Sieben_mit_je_zwei_und_drei_mit_je_einem_Termin_stehen_als_zwei_Gruppen_ohne_Namen()
    {
        var entries = Treatments(2, 2, 2, 2, 2, 2, 2, 1, 1, 1);

        var lead = Ranking.Lead(entries, "Behandlungen");

        Assert.Equal("7 Behandlungen mit je 2 Terminen, danach 3 Behandlungen mit je 1 Termin", lead);
        Assert.DoesNotContain("Behandlung 1", lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Gleiche_Werte_teilen_den_Platz_und_der_naechste_folgt_nach_den_Gleichen()
    {
        var ranks = Ranking.Ranks(Treatments(2, 2, 2, 2, 2, 2, 2, 1, 1, 1));

        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 8, 8, 8], ranks);
        Assert.DoesNotContain(2, ranks);
    }

    [Fact]
    public void Verschiedene_Werte_bekommen_die_Plaetze_eins_bis_drei_und_den_Kopfsatz_mit_Namen()
    {
        var entries = Treatments(5, 3, 1);

        Assert.Equal([1, 2, 3], Ranking.Ranks(entries));
        Assert.Equal("1. Behandlung 1 (5), 2. Behandlung 2 (3), 3. Behandlung 3 (1)", Ranking.Lead(entries, "Behandlungen"));
    }

    [Fact]
    public void Ein_Einzelner_vor_einer_Gruppe_behaelt_seinen_Namen_und_Platz()
    {
        var entries = Treatments(5, 2, 2, 2);

        Assert.Equal([1, 2, 2, 2], Ranking.Ranks(entries));
        Assert.Equal("1. Behandlung 1 (5), danach 3 Behandlungen mit je 2 Terminen", Ranking.Lead(entries, "Behandlungen"));
    }

    [Fact]
    public void Der_Kopfsatz_nennt_hoechstens_drei_Gruppen()
    {
        var lead = Ranking.Lead(Treatments(4, 4, 3, 3, 2, 2, 1, 1), "Behandlungen");

        Assert.Equal("2 Behandlungen mit je 4 Terminen, danach 2 Behandlungen mit je 3 Terminen, danach 2 Behandlungen mit je 2 Terminen", lead);
    }
}

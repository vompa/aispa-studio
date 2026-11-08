using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class BriefingSummaryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task KI_Zusammenfassung_kommt_ueber_die_Queue_und_folgt_den_Fakten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var path = $"/api/reports/locations/{spa.Location.Id}/briefing?date={TestSpa.Monday:yyyy-MM-dd}";

        var first = await WaitAsync(spa, path, b => b.AiSummary is not null);
        await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Anna.Id);
        var changed = await spa.GetAsync<BriefingDto>(path, Ct);
        var second = await WaitAsync(spa, path, b => b.AiSummary is not null);

        Assert.StartsWith("Heute 1 Behandlungen", first.Summary, StringComparison.Ordinal);
        Assert.Equal($"Guten Überblick: {first.Summary}", first.AiSummary);
        Assert.Equal(BriefingSummaries.Notice, first.AiNotice);
        Assert.Null(changed.AiSummary);
        Assert.StartsWith("Heute 2 Behandlungen", second.Summary, StringComparison.Ordinal);
        Assert.Equal($"Guten Überblick: {second.Summary}", second.AiSummary);
    }

    [Fact]
    public async Task KI_Text_mit_erfundener_Zahl_wird_verworfen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        FakeBriefingSummarizer.Answers[spa.Location.Id] = "Ruhiger Tag, Umsatz etwa 12.345,67 €.";
        var path = $"/api/reports/locations/{spa.Location.Id}/briefing?date={TestSpa.Monday:yyyy-MM-dd}";

        var briefing = await WaitAsync(spa, path, b => !b.AiSummaryPending);

        Assert.Null(briefing.AiSummary);
        Assert.Null(briefing.AiNotice);
        Assert.StartsWith("Heute 1 Behandlungen", briefing.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Nur_Zahlen_aus_den_Fakten_sind_erlaubt()
    {
        var facts = new BriefingDto(Guid.NewGuid(), new DateOnly(2027, 3, 1), "Heute 3 Behandlungen, Auslastung 45 %.", 3, 45,
            [new BriefingStaff(Guid.NewGuid(), "Anna", "Dienst", "09:00–17:00")], [], [],
            [], [new BriefingItem(null, null, "Kassa K1: Anfangsbestand laut letztem Abschluss 150,00 €.")]);

        Assert.True(BriefingSummaries.OnlyKnownNumbers("Drei Termine, Anna ab 9 Uhr, Auslastung 45 %, Kassa mit 150,00 € am 1. März.", facts));
        Assert.False(BriefingSummaries.OnlyKnownNumbers("Auslastung 46 %.", facts));
        Assert.False(BriefingSummaries.OnlyKnownNumbers("Umsatz heute etwa 1.200 €.", facts));
    }

    [Fact]
    public void Der_Fingerabdruck_aendert_sich_nicht_durch_das_Feld_Registers()
    {
        var plain = new BriefingDto(Guid.NewGuid(), new DateOnly(2027, 3, 1), "Heute 3 Behandlungen, Auslastung 45 %.", 3, 45,
            [new BriefingStaff(Guid.NewGuid(), "Anna", "Dienst", "09:00–17:00")], [], [],
            [], [new BriefingItem(null, null, "Kassa K1: Anfangsbestand laut letztem Abschluss 150,00 €.")]);
        var withRegisters = plain with { Registers = [new BriefingRegister(Guid.NewGuid(), "K1", "EUR", 150m, ["Signatureinheit nicht erreichbar."])] };

        Assert.Equal(BriefingSummaries.Fingerprint(plain), BriefingSummaries.Fingerprint(withRegisters));
    }

    private static async Task<BriefingDto> WaitAsync(TestSpa spa, string path, Func<BriefingDto, bool> done)
    {
        for (var i = 0; ; i++)
        {
            var briefing = await spa.GetAsync<BriefingDto>(path, Ct);
            if (done(briefing) || i == 100)
            {
                return briefing;
            }

            await Task.Delay(100, Ct);
        }
    }
}

using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 115 (Ä251): „Passt dazu“ an der Kassa. Die API nennt die Vorschläge zur offenen Rechnung; die Kassa fragt sie bei jedem Rechnungswechsel neu ab.
/// Anzeige und Klick auf „Dazugeben“ prüft <c>ViewTests.Recommendations</c> im Fenster.
/// </summary>
public sealed class PosRecommendationTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly string RecommendationsRoute = $"GET /api/pos/bills/{SpaSample.BillId}/recommendations";

    private static readonly RecommendedArticleDto Cream = new(SpaSample.Articles[3].Id, SpaSample.Articles[3].Name, SpaSample.Articles[3].Price, 3, "Stück");

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On("POST /api/pos/bills", SpaSample.Bill(withArticle: false))
        .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill());

    private static async Task WaitForAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<PosViewModel> BillWithAppointment(StubApi stub)
    {
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        return pos;
    }

    [Fact]
    public async Task Given_keine_Vorschlaege_When_die_Rechnung_geladen_ist_Then_gibt_es_keinen_Hinweis()
    {
        var stub = Api().On(RecommendationsRoute, new List<RecommendedArticleDto>());

        var pos = await BillWithAppointment(stub);
        await WaitForAsync(() => stub.Calls.Any(c => c.Route == RecommendationsRoute));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains(stub.Calls, c => c.Route == RecommendationsRoute);
        Assert.Empty(pos.RecommendationHints);
        Assert.False(pos.HasRecommendationHints);
    }

    [Fact]
    public async Task Given_die_Creme_wurde_dazugegeben_When_die_Rechnung_neu_kommt_Then_fragt_die_Kassa_neu_und_der_Hinweis_geht()
    {
        var stub = Api().On(RecommendationsRoute, new List<RecommendedArticleDto> { Cream });
        var pos = await BillWithAppointment(stub);
        await WaitForAsync(() => pos.HasRecommendationHints);
        var hint = Assert.Single(pos.RecommendationHints);
        Assert.Equal(("Passt dazu: Gesichtscreme Hydro 50 ml", "39,00 € · 3 Stück am Regal"), (hint.Title, hint.Detail));

        // Die Creme steht danach auf der Rechnung; die API schlägt sie nicht mehr vor.
        stub.On(RecommendationsRoute, new List<RecommendedArticleDto>());
        var asked = stub.Calls.Count(c => c.Route == RecommendationsRoute);
        await pos.AddRecommendationCommand.ExecuteAsync(hint);
        await WaitForAsync(() => !pos.HasRecommendationHints);

        Assert.True(stub.Calls.Count(c => c.Route == RecommendationsRoute) > asked);
        Assert.False(pos.HasRecommendationHints);
        Assert.Null(pos.Error);
    }
}

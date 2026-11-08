using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Suchfeld „Gast, Code oder Artikel“ über der Liste „Heute abzurechnen“ (Slice 137, Ä344).</summary>
public sealed class PosDayListTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static async Task<PosViewModel> LoadedPos() => (await LoadedPosWithApi()).Pos;

    private static async Task<(PosViewModel Pos, StubApi Api)> LoadedPosWithApi()
    {
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
            .On("GET /api/pos/articles", SpaSample.Articles)
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList(SpaSample.UnbilledVisits()))
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>());
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.LoadDayAsync();
        return (pos, stub);
    }

    [Fact]
    public async Task Enter_im_Suchfeld_ohne_gewaehlte_Zeile_sucht_den_Text_als_Code_und_setzt_keinen_Gast_auf_die_Rechnung()
    {
        var (pos, api) = await LoadedPosWithApi();
        pos.ScanText = "gerhard";
        Assert.Single(pos.Unbilled);

        await pos.ScanCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == "GET /api/pos/articles/by-barcode?code=gerhard");
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
        Assert.Contains("gehört zu keinem Artikel", pos.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suche_nach_Gast_zeigt_nur_passende_Zeilen_und_Artikel_nach_Name()
    {
        var pos = await LoadedPos();
        var all = pos.Unbilled.Count;
        Assert.True(all > 1);

        pos.ScanText = "gerhard";
        Assert.Equal(["Gerhard Lang"], pos.Unbilled.Select(u => u.Customer));
        Assert.Empty(pos.ArticleHits);
        Assert.Equal("Später heute", pos.Unbilled[0].GroupTitle);

        pos.ScanText = "Verleih";
        Assert.Empty(pos.Unbilled);
        Assert.Equal(["Saunatuch-Verleih", "Bademantel-Verleih"], pos.ArticleHits.Select(a => a.Name));
        Assert.False(pos.ShowNoHits);

        pos.ScanText = "xyz";
        Assert.Empty(pos.Unbilled);
        Assert.Empty(pos.ArticleHits);
        Assert.True(pos.ShowNoHits);

        pos.ScanText = "";
        Assert.Equal(all, pos.Unbilled.Count);
        Assert.Empty(pos.ArticleHits);
    }
}

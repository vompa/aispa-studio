using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 146: Der Reiter „Belege“ der Kassa zeigt auf Wunsch auch einen Vortag (Ä313).</summary>
public sealed class PosReceiptDaysTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static string ReceiptsUrl(string date) => $"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date={date}";

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On(ReceiptsUrl("2026-10-19"), new List<ReceiptDto> { SpaSample.Receipt() with { Number = 30 } })
        .On(ReceiptsUrl("2026-10-18"), new List<ReceiptDto> { SpaSample.Receipt() with { Number = 29 } })
        .On(ReceiptsUrl("2026-10-17"), new List<ReceiptDto>());

    private static async Task<PosViewModel> OpenReceiptsAsync()
    {
        var pos = new PosViewModel(Api().Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.ShowReceiptsCommand.ExecuteAsync(null);
        return pos;
    }

    [Fact]
    public async Task Der_Reiter_Belege_zeigt_zuerst_heute()
    {
        var pos = await OpenReceiptsAsync();

        Assert.Equal("Belege heute", pos.ReceiptsTitle);
        Assert.Equal(["Nr. 30"], pos.Receipts.Select(r => r.Number));
        Assert.False(pos.ShowNextReceiptDayCommand.CanExecute(null));
    }

    [Fact]
    public async Task Ein_Vortag_laesst_sich_waehlen_und_der_Weg_zurueck_fuehrt_zu_heute()
    {
        var pos = await OpenReceiptsAsync();

        await pos.ShowPreviousReceiptDayCommand.ExecuteAsync(null);

        Assert.Equal("Belege gestern", pos.ReceiptsTitle);
        Assert.Equal(["Nr. 29"], pos.Receipts.Select(r => r.Number));
        Assert.True(pos.ShowNextReceiptDayCommand.CanExecute(null));

        await pos.ShowNextReceiptDayCommand.ExecuteAsync(null);

        Assert.Equal("Belege heute", pos.ReceiptsTitle);
        Assert.Equal(["Nr. 30"], pos.Receipts.Select(r => r.Number));
    }

    [Fact]
    public async Task Ein_Tag_ohne_Belege_nennt_den_Tag_und_der_Reiter_beginnt_wieder_bei_heute()
    {
        var pos = await OpenReceiptsAsync();

        await pos.ShowPreviousReceiptDayCommand.ExecuteAsync(null);
        await pos.ShowPreviousReceiptDayCommand.ExecuteAsync(null);

        Assert.Equal("Belege Samstag, 17. Oktober 2026", pos.ReceiptsTitle);
        Assert.Equal("An diesem Tag gab es keine Belege an dieser Kasse.", pos.ReceiptsEmptyText);
        Assert.Empty(pos.Receipts);

        await pos.ShowClosingCommand.ExecuteAsync(null);
        await pos.ShowReceiptsCommand.ExecuteAsync(null);

        Assert.Equal("Belege heute", pos.ReceiptsTitle);
        Assert.Equal("Heute noch keine Belege an dieser Kasse.", pos.ReceiptsEmptyText);
    }
}

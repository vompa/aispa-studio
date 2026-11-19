using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Online-Zahlungen …“ in der Kassa: online gekaufte Gutscheine und Erstattungen online bezahlter Belege, nur zum Nachsehen.</summary>
public sealed class PosOnlinePaymentsTests
{
    private static readonly string OrdersRoute = $"GET /api/locations/{SpaSample.LocationId}/voucher-orders";
    private static readonly string RefundsRoute = $"GET /api/locations/{SpaSample.LocationId}/online-refunds";

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static async Task<PosViewModel> PosAsync(StubApi api)
    {
        var pos = new PosViewModel(api.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        return pos;
    }

    private static StubApi PosApi() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>());

    internal static StubApi WithPayments(StubApi api) => api
        .On(OrdersRoute, new List<VoucherOrderStaffEntry>
        {
            new(Guid.NewGuid(), SpaSample.At(9, 12), VoucherOrderStatus.Issued, VoucherKind.MultiPurpose, 50m, "EUR", "gast@example.at", "Maria", Guid.NewGuid(), null,
                "K1 Nr. 12, 19.10.2026"),
            new(Guid.NewGuid(), SpaSample.At(8), VoucherOrderStatus.Failed, VoucherKind.SinglePurpose, 89m, "EUR", "gast@example.at", null, null,
                "Die Kassa war nicht eingerichtet."),
        })
        .On(RefundsRoute, new List<OnlineRefundStaffEntry>
        {
            new(Guid.NewGuid(), "K1 Nr. 9, 18.10.2026", "K1 Nr. 13, 19.10.2026", OnlinePaymentSource.Voucher, 50m, "EUR", OnlineRefundStatus.Refunded,
                SpaSample.At(10), SpaSample.At(10, 1), null),
            new(Guid.NewGuid(), "K1 Nr. 7, 17.10.2026", "K1 Nr. 14, 19.10.2026", OnlinePaymentSource.NoShowFee, 27m, "EUR", OnlineRefundStatus.Failed,
                SpaSample.At(11), null, "Der Anbieter hat abgelehnt."),
        });

    [Fact]
    public async Task Die_Karte_zeigt_Gutscheine_und_Erstattungen_mit_Stand_und_Beleg()
    {
        var api = WithPayments(PosApi());
        var pos = await PosAsync(api);

        await pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        var card = pos.OnlinePayments!;

        Assert.Equal(new OnlinePaymentLine("Wertgutschein 50,00 € · 19.10.2026 09:12", "Ausgestellt · Beleg K1 Nr. 12, 19.10.2026 · für Maria", false), card.Orders[0]);
        Assert.Equal(new OnlinePaymentLine("Behandlungsgutschein 89,00 € · 19.10.2026 08:00", "Bezahlt, aber nicht ausgestellt", true, "Die Kassa war nicht eingerichtet."),
            card.Orders[1]);
        Assert.Equal(new OnlinePaymentLine("Online-Gutschein 50,00 € · 19.10.2026 10:00",
            "Erstattet am 19.10.2026 10:01 · Beleg K1 Nr. 9, 18.10.2026 · Stornobeleg K1 Nr. 13, 19.10.2026", false), card.Refunds[0]);
        Assert.True(card.Refunds[1].IsProblem);
        // Zustand und nächster Schritt stehen getrennt: der Zustand kurz, der Weg samt Fehlertext darunter.
        Assert.Equal("Erstattung fehlgeschlagen", card.Refunds[1].Detail);
        Assert.StartsWith("Im Beleg erneut anstoßen (Kassenaufsicht)", card.Refunds[1].Next, StringComparison.Ordinal);
        Assert.EndsWith("Der Anbieter hat abgelehnt.", card.Refunds[1].Next, StringComparison.Ordinal);
        Assert.False(card.NoOrders);
        Assert.Null(card.Error);
    }

    [Fact]
    public async Task Ohne_Eintraege_steht_Noch_keine_und_ein_Fehler_des_Servers_steht_in_der_Karte()
    {
        var api = PosApi().On(OrdersRoute, new List<VoucherOrderStaffEntry>()).On(RefundsRoute, null, HttpStatusCode.Forbidden);
        var pos = await PosAsync(api);

        await pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        var card = pos.OnlinePayments!;

        Assert.True(card.NoOrders);
        Assert.True(card.NoRefunds);
        Assert.NotNull(card.Error);
    }

    [Fact]
    public async Task Die_Karte_geht_mit_demselben_Knopf_oder_ihrem_Schliessen_wieder_zu()
    {
        var pos = await PosAsync(WithPayments(PosApi()));

        await pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        pos.OnlinePayments!.CloseCommand.Execute(null);
        Assert.Null(pos.OnlinePayments);

        await pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        await pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        Assert.Null(pos.OnlinePayments);
    }
}

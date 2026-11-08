using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Aktionen an der Kassa (ADR 0071): die Kassa zeigt die laufende Aktion als Vorschlag, wendet sie erst auf Klick an (der Server rechnet den
/// Rabatt) und der Beleg nennt die Aktion in einer eigenen Zeile.
/// </summary>
public sealed class PosPromotionTests
{
    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly string OffersRoute = $"GET /api/pos/bills/{SpaSample.BillId}/promotions";

    public static PromotionOfferDto Offer(string name = "Herbst") =>
        new(Guid.NewGuid(), name, "−10 %", [new(SpaSample.TreatmentLineId, $"{SpaSample.Paying.TreatmentName} (Lena Huber)", 8.90m)], 8.90m);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On("POST /api/pos/bills", SpaSample.Bill())
        .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt());

    private static async Task WaitForAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Passende_Aktion_wird_vorgeschlagen_und_erst_mit_Klick_vom_Server_angewendet()
    {
        var offer = Offer();
        var bill = SpaSample.Bill(discount: 8.90m);
        var discounted = bill with { Lines = [bill.Lines[0] with { DiscountNote = "Aktion Herbst 10 %" }, .. bill.Lines.Skip(1)] };
        var applyRoute = $"POST /api/pos/bills/{SpaSample.BillId}/promotions/{offer.PromotionId}/apply";
        var stub = Api().On(OffersRoute, new List<PromotionOfferDto> { offer }).On(applyRoute, discounted);
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        await WaitForAsync(() => pos.HasPromotionHints);

        // Der Vorschlag nennt Aktion, Rabatt und Wirkung; angewendet ist noch nichts.
        var hint = Assert.Single(pos.PromotionHints);
        Assert.Equal(("Aktion „Herbst“ −10 %", "1 Position, zusammen −8,90 €", "Aktion „Herbst“ −10 % anwenden"), (hint.Title, hint.Detail, hint.ApplyName));
        Assert.True(pos.HasPromotionHints);
        Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/apply", StringComparison.Ordinal));
        Assert.Equal(0m, pos.Lines[0].Line.Discount);

        // Klick: der Server setzt den Rabatt, die Zeile nennt die Aktion, der Vorschlag verschwindet.
        stub.On(OffersRoute, new List<PromotionOfferDto>());
        await pos.ApplyPromotionCommand.ExecuteAsync(hint);
        await WaitForAsync(() => !pos.HasPromotionHints);

        Assert.Single(stub.Calls, c => c.Route == applyRoute);
        Assert.Equal("20 % USt · Aktion Herbst 10 % −8,90 €", pos.Lines[0].Tax);
        Assert.Equal(SpaSample.Paying.Price - 8.90m, pos.Bill!.Lines[0].Amount);
        Assert.False(pos.HasPromotionHints);
        Assert.Null(pos.Error);
    }

    [Fact]
    public async Task Ohne_passende_Aktion_oder_ohne_Antwort_gibt_es_keinen_Vorschlag()
    {
        var empty = Api().On(OffersRoute, new List<PromotionOfferDto>());
        var posEmpty = new PosViewModel(empty.Client(), new SampleTime());
        await posEmpty.InitializeAsync(SpaSample.LocationId);
        await posEmpty.AddAppointmentAsync(SpaSample.Paying);
        await WaitForAsync(() => empty.Calls.Any(c => c.Route == OffersRoute));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Der Server kennt die Route nicht oder antwortet mit einem Fehler: kassiert wird trotzdem, es erscheint nur kein Hinweis.
        var down = Api();
        var posDown = new PosViewModel(down.Client(), new SampleTime());
        await posDown.InitializeAsync(SpaSample.LocationId);
        await posDown.AddAppointmentAsync(SpaSample.Paying);
        await WaitForAsync(() => down.Calls.Any(c => c.Route == OffersRoute));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(posEmpty.PromotionHints);
        Assert.False(posEmpty.HasPromotionHints);
        Assert.Empty(posDown.PromotionHints);
        Assert.NotNull(posDown.Bill);
        Assert.Null(posDown.Error);
    }

    [Fact]
    public async Task Eine_bezahlte_Rechnung_bekommt_keinen_Vorschlag_mehr()
    {
        var stub = Api().On(OffersRoute, new List<PromotionOfferDto> { Offer() });
        var pos = new PosViewModel(stub.Client(), new SampleTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);
        await WaitForAsync(() => pos.HasPromotionHints);
        Assert.True(pos.HasPromotionHints);

        pos.Payments.First(p => p.Method == PaymentMethod.Cash).Amount = "138,80";
        await pos.PayCommand.ExecuteAsync(null);
        await WaitForAsync(() => !pos.HasPromotionHints);

        Assert.False(pos.HasPromotionHints);
        Assert.NotNull(pos.Receipt);
    }

    [Fact]
    public void Der_Beleg_nennt_den_Rabatt_der_Aktion_in_einer_eigenen_Zeile_und_die_Spalte_ergibt_die_Summe()
    {
        var sold = SpaSample.Receipt();
        var withPromotion = sold with
        {
            Lines = [sold.Lines[0] with { Amount = SpaSample.Paying.Price - 8.90m, Discount = 8.90m, DiscountNote = "Aktion Herbst 10 %" }, sold.Lines[1]],
            Total = SpaSample.Paying.Price - 8.90m + 49.80m,
        };
        var voided = withPromotion with
        {
            Kind = ReceiptKind.Void,
            Lines = [.. withPromotion.Lines.Select(l => l with { Amount = -l.Amount, Discount = -l.Discount })],
        };

        var rows = new ReceiptViewModel(withPromotion, TimeZoneInfo.Utc, FiscalCountry.AT).Lines;
        var back = new ReceiptViewModel(voided, TimeZoneInfo.Utc, FiscalCountry.AT).Lines;

        // Preis davor, darunter der Rabatt mit dem Namen der Aktion; der zweite Artikel ohne Rabatt bleibt eine Zeile.
        Assert.Equal(3, rows.Count);
        Assert.Equal(($"{SpaSample.Paying.TreatmentName} (Lena Huber)", "89,00 €"), (rows[0].Label, rows[0].Value));
        Assert.Equal(("   Aktion Herbst 10 %", "−8,90 €"), (rows[1].Label, rows[1].Value));
        Assert.Equal("2 × " + SpaSample.Articles[0].Name, rows[2].Label);
        Assert.Equal(3, back.Count);
        Assert.Equal(("   Aktion Herbst 10 %", "8,90 €"), (back[1].Label, back[1].Value));
    }
}

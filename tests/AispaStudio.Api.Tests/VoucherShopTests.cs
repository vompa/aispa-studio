using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class VoucherShopTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static string NewEmail() => $"gutschein-{Guid.NewGuid():N}@example.at";

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa, FiscalCountry country = FiscalCountry.AT) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"G-{Guid.NewGuid():N}"[..14], country, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static VoucherOrderRequest Request(TestSpa spa, string email, VoucherKind kind = VoucherKind.MultiPurpose, decimal? amount = 50m,
        Guid? treatment = null, bool privacy = true) =>
        new(spa.Location.Id, kind, amount, treatment, new OnlineContact("Gerda", "Schenk", email, null), "Mama", "Alles Gute zum Geburtstag!",
            privacy, "de");

    private static async Task<VoucherOrderStarted> OrderAsync(HttpClient client, VoucherOrderRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/online/vouchers/orders", request, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<VoucherOrderStarted>(response, Ct);
    }

    private static async Task<VoucherOrderDto> WaitForAsync(HttpClient client, string token, VoucherOrderStatus status)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var order = (await client.GetFromJsonAsync<VoucherOrderDto>($"/api/online/vouchers/orders/{token}", TestSpa.Json, Ct))!;
            if (order.Status == status)
            {
                return order;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Ohne_Kasse_gibt_es_keinen_Online_Verkauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        var offer = await client.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct);
        var order = await client.PostAsJsonAsync("/api/online/vouchers/orders", Request(spa, NewEmail()), TestSpa.Json, Ct);

        Assert.False(offer!.Available);
        Assert.Contains("keine Kasse", offer.Reason, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, order.StatusCode);
    }

    [Fact]
    public async Task Wertgutschein_wird_nach_der_Zahlung_an_der_Kasse_mit_Beleg_ausgestellt_und_gemailt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var offer = await client.GetFromJsonAsync<OnlineVoucherOffer>($"/api/online/locations/{spa.Location.Id}/vouchers", TestSpa.Json, Ct);
        Assert.True(offer!.Available);
        Assert.True(offer.TestPayment);
        Assert.Contains(offer.Treatments, t => t.Id == spa.MassageTreatment.Id);

        var started = await OrderAsync(client, Request(spa, email));
        Assert.EndsWith($"/buchen/?spa={spa.TenantId}#/gutschein/{started.Token}", started.PaymentUrl, StringComparison.Ordinal);
        var pending = await WaitForAsync(client, started.Token, VoucherOrderStatus.Pending);
        Assert.Null(pending.Code);

        var paid = await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        // Eine zweite Meldung derselben Zahlung stellt nichts doppelt aus.
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);

        // Die Bestellseite findet ihr Spa auch ohne Kopfzeile, über den Code der Bestellung.
        using var plain = fixture.CreateClient();
        var issued = await WaitForAsync(plain, started.Token, VoucherOrderStatus.Issued);
        Assert.NotNull(issued.Code);
        Assert.Equal(50m, issued.Amount);
        Assert.Equal("Mama", issued.RecipientName);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Gutschein", StringComparison.Ordinal), Ct);
        Assert.Contains($"Gutscheincode: {issued.Code}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("im Wert von 50,00 EUR", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Alles Gute zum Geburtstag!", mail.Text, StringComparison.Ordinal);
        Assert.Null(issued.ValidUntil);
        Assert.DoesNotContain("Einzulösen bis", mail.Text, StringComparison.Ordinal);

        var voucher = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{issued.Code}", Ct);
        Assert.Equal(50m, voucher.Balance);
        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        var sale = Assert.Single(receipts, r => r.IssuedVouchers.Count > 0);
        Assert.Equal(PaymentMethod.Card, Assert.Single(sale.Payments).Method);
        Assert.StartsWith("Online Test", sale.Payments[0].Reference, StringComparison.Ordinal);
        Assert.Equal(50m, sale.Total);
        Assert.Contains($"Beleg: {register.Code} Nr. {sale.Number}", mail.Text, StringComparison.Ordinal);

        var staff = await spa.GetAsync<List<VoucherOrderStaffEntry>>($"/api/locations/{spa.Location.Id}/voucher-orders", Ct);
        var listed = Assert.Single(staff);
        Assert.Equal(VoucherOrderStatus.Issued, listed.Status);
        Assert.Equal(sale.Id, listed.ReceiptId);
    }

    [Fact]
    public async Task Online_Gutschein_an_einer_deutschen_Kasse_gilt_bis_Jahresende_plus_drei_Jahre_und_die_Mail_nennt_das_Datum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateRegisterAsync(spa, FiscalCountry.DE);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var started = await OrderAsync(client, Request(spa, email));
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);

        var issued = await WaitForAsync(client, started.Token, VoucherOrderStatus.Issued);
        var expected = new DateOnly(ViennaToday.Year + 3, 12, 31);
        Assert.Equal(expected, issued.ValidUntil);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Gutschein", StringComparison.Ordinal), Ct);
        Assert.Contains($"Einzulösen bis {expected:dd.MM.yyyy}", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Behandlungsgutschein_kostet_den_Preis_der_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateRegisterAsync(spa);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var started = await OrderAsync(client, Request(spa, email, VoucherKind.SinglePurpose, amount: 1m, treatment: spa.MassageTreatment.Id));
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);

        var issued = await WaitForAsync(client, started.Token, VoucherOrderStatus.Issued);
        Assert.Equal(79m, issued.Amount);
        Assert.Equal("Massage", issued.TreatmentName);
        var voucher = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{issued.Code}", Ct);
        Assert.Equal(VoucherKind.SinglePurpose, voucher.Kind);
        Assert.Equal(spa.MassageTreatment.Id, voucher.TreatmentId);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Gutschein", StringComparison.Ordinal), Ct);
        Assert.Contains("für „Massage“", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abgebrochene_Zahlung_stellt_nichts_aus()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        using var client = spa.OnlineClient();
        var started = await OrderAsync(client, Request(spa, NewEmail()));

        var cancelled = await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(false), TestSpa.Json, Ct);
        var paidLater = await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);

        Assert.Equal(VoucherOrderStatus.Cancelled, (await TestSpa.ReadAsync<VoucherOrderDto>(cancelled, Ct)).Status);
        Assert.Equal(VoucherOrderStatus.Cancelled, (await TestSpa.ReadAsync<VoucherOrderDto>(paidLater, Ct)).Status);
        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        Assert.DoesNotContain(receipts, r => r.IssuedVouchers.Count > 0);
        Assert.Empty(await spa.GetAsync<List<VoucherOrderStaffEntry>>($"/api/locations/{spa.Location.Id}/voucher-orders", Ct));
    }

    [Fact]
    public async Task Bestellung_wird_geprueft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateRegisterAsync(spa);
        using var client = spa.OnlineClient();

        async Task<HttpStatusCode> PostAsync(VoucherOrderRequest request) =>
            (await client.PostAsJsonAsync("/api/online/vouchers/orders", request, TestSpa.Json, Ct)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail(), amount: 5m)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail(), amount: 5000m)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail(), amount: 20.555m)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail(), privacy: false)));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail(), VoucherKind.SinglePurpose, treatment: Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, "keine-adresse")));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(Request(spa, NewEmail()) with { Message = new string('x', 301) }));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(Request(spa, NewEmail()) with { LocationId = Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/online/vouchers/orders/{new string('x', 43)}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Ein_anderes_Spa_sieht_die_Bestellungen_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var response = await other.Client.GetAsync($"/api/locations/{spa.Location.Id}/voucher-orders", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Bestellungen_sieht_nur_das_Team_mit_Kassenrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync($"/api/locations/{spa.Location.Id}/voucher-orders", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

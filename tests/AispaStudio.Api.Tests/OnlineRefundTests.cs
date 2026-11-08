using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class OnlineRefundTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static string NewEmail() => $"erstattung-{Guid.NewGuid():N}@example.at";

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"E-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    /// <summary>Kauft online einen Wertgutschein über 50 EUR (Testzahlung) und wartet, bis die Kasse ihn ausgestellt hat.</summary>
    private static async Task<(string Token, VoucherOrderDto Order, ReceiptDto Sale)> BuyVoucherAsync(TestSpa spa, CashRegisterDto register, string email)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/vouchers/orders", new VoucherOrderRequest(spa.Location.Id, VoucherKind.MultiPurpose,
            50m, null, new OnlineContact("Gerda", "Schenk", email, null), null, null, true, "de"), TestSpa.Json, Ct);
        var started = await TestSpa.ReadAsync<VoucherOrderStarted>(response, Ct);
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);
        var order = await WaitForAsync(() => client.GetFromJsonAsync<VoucherOrderDto>($"/api/online/vouchers/orders/{started.Token}", TestSpa.Json, Ct),
            o => o!.Status == VoucherOrderStatus.Issued);
        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        return (started.Token, order!, Assert.Single(receipts, r => r.IssuedVouchers.Count > 0));
    }

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> load, Func<T, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var value = await load();
            if (done(value))
            {
                return value;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private static Task<OnlineRefundDto> RefundOfAsync(TestSpa spa, Guid receiptId) =>
        spa.GetAsync<OnlineRefundDto>($"/api/pos/receipts/{receiptId}/online-refund", Ct);

    [Fact]
    public async Task Storno_eines_online_gekauften_Gutscheins_erstattet_das_Geld_und_schliesst_die_Rechnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var email = NewEmail();
        var (token, order, sale) = await BuyVoucherAsync(spa, register, email);

        var before = await RefundOfAsync(spa, sale.Id);
        Assert.Equal((OnlinePaymentSource.Voucher, 50m, false, null, false), (before.Source, before.Amount, before.Voided, before.Status, before.CanRetry));
        // Erstattet wird nur nach dem Storno an der Kassa.
        var tooEarly = await spa.Client.PostAsync($"/api/pos/receipts/{sale.Id}/online-refund", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);

        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{sale.Id}/void", new VoidRequest("Gast möchte das Geld zurück"), Ct);
        var refunded = await WaitForAsync(() => RefundOfAsync(spa, sale.Id), r => r.Status == OnlineRefundStatus.Refunded);

        Assert.True(refunded.Voided);
        Assert.False(refunded.CanRetry);
        Assert.StartsWith("test-refund-", refunded.Reference, StringComparison.Ordinal);
        Assert.Equal(-50m, voided.Total);
        using var client = spa.OnlineClient();
        var after = await client.GetFromJsonAsync<VoucherOrderDto>($"/api/online/vouchers/orders/{token}", TestSpa.Json, Ct);
        Assert.Equal(VoucherOrderStatus.Refunded, after!.Status);
        Assert.Null(after.Code);
        Assert.Equal(VoucherStatus.Voided, (await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{order.Code}", Ct)).Status);
        // Die durch das Storno wieder geöffnete Rechnung wird geschlossen, sonst ließe sich der Gutschein ein zweites Mal kassieren.
        Assert.Empty(await spa.GetAsync<List<BillDto>>($"/api/pos/registers/{register.Id}/bills", Ct));

        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Rückerstattung", StringComparison.Ordinal), Ct);
        Assert.Contains("Ihren Gutschein storniert und 50,00 EUR", mail.Text, StringComparison.Ordinal);
        Assert.Contains($"Stornobeleg: {register.Code} Nr. {voided.Number}", mail.Text, StringComparison.Ordinal);

        var again = await spa.Client.PostAsync($"/api/pos/receipts/{sale.Id}/online-refund", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Eingeloester_Gutschein_wird_weder_storniert_noch_erstattet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var (_, order, sale) = await BuyVoucherAsync(spa, register, NewEmail());

        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 20m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Voucher, 20m, order.Code, null)]), Ct);

        var voided = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{sale.Id}/void", new VoidRequest("Geld zurück"), TestSpa.Json, Ct);
        var refund = await RefundOfAsync(spa, sale.Id);

        Assert.Equal(HttpStatusCode.BadRequest, voided.StatusCode);
        Assert.Contains("schon eingelöst", await voided.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.False(refund.Voided);
        Assert.Null(refund.Status);
    }

    [Fact]
    public async Task Belastete_Ausfallgebuehr_wird_nach_Storno_erstattet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50), Ct);
        await CreateRegisterAsync(spa);
        using var client = spa.OnlineClient();
        var booking = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id,
            TestSpa.At(10), new OnlineContact("Nora", "Karte", NewEmail(), null), null, true, "de", true), TestSpa.Json, Ct);
        var booked = await TestSpa.ReadAsync<OnlineBookingConfirmation>(booking, Ct);
        await client.PostAsJsonAsync($"/api/online/bookings/{booked.ManageToken}/card/test", new TestCardResult(true), TestSpa.Json, Ct);
        await spa.SetStatusAsync(booked.Appointment.Id, AppointmentStatus.NoShow, Ct);
        await spa.Client.PostAsync($"/api/appointments/{booked.Appointment.Id}/card-guarantee/charge", null, Ct);
        var charged = await WaitForAsync(() => spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct),
            g => g.Status == CardGuaranteeStatus.Charged);

        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{charged.ReceiptId}/void", new VoidRequest("Gast war krank, Attest liegt vor"), Ct);
        var refund = await WaitForAsync(() => RefundOfAsync(spa, charged.ReceiptId!.Value), r => r.Status == OnlineRefundStatus.Refunded);
        var guarantee = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{booked.Appointment.Id}/card-guarantee", Ct);

        Assert.Equal((OnlinePaymentSource.NoShowFee, 39.50m), (refund.Source, refund.Amount));
        Assert.Equal(CardGuaranteeStatus.Refunded, guarantee.Status);
        Assert.False(guarantee.CanCharge);
    }

    [Fact]
    public async Task Belege_von_der_Kassa_sind_keine_Online_Zahlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12m, null, null)]), Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync($"/api/pos/receipts/{receipt.Id}/online-refund", Ct);
        var foreign = await other.Client.GetAsync($"/api/pos/receipts/{receipt.Id}/online-refund", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}

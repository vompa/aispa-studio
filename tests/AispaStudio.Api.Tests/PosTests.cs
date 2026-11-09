using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Api.Sync;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Tests;

public sealed class PosTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    [Fact]
    public async Task Neue_Kasse_hat_einen_signierten_Startbeleg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var register = await CreateRegisterAsync(spa);

        Assert.Equal(1, register.LastReceiptNumber);
        Assert.NotNull(register.CertificateSerial);
        Assert.Contains(register.Warnings, w => w.Contains("Test-Signatureinrichtung", StringComparison.Ordinal));
        var dep = await spa.GetAsync<DepExport>($"/api/pos/registers/{register.Id}/dep", Ct);
        var jws = Assert.Single(Assert.Single(dep.Groups).Receipts);
        var code = VerifyJws(jws, dep.Groups[0].Certificate);
        var fields = code.Split('_');
        Assert.Equal("R1-AT0", fields[1]);
        Assert.Equal(register.Code, fields[2]);
        Assert.Equal("1", fields[3]);
        Assert.Equal(["0,00", "0,00", "0,00", "0,00", "0,00"], fields[5..10]);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(register.Code)), 0, 8), fields[12]);
    }

    [Fact]
    public async Task Termin_bar_bezahlen_gibt_Rueckgeld_und_verschluesselt_den_Umsatzzaehler()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        var receipt = await CheckoutAsync(spa, bill.Id, null, Cash(100m));

        Assert.Equal(spa.Customer.Id, bill.CustomerId);
        Assert.Equal(79m, receipt.Total);
        Assert.Equal(21m, receipt.Change);
        var tax = Assert.Single(receipt.Taxes);
        Assert.Equal((TaxCategory.Normal, 20m, 79m, 65.83m, 13.17m), (tax.TaxCategory, tax.Percent, tax.Gross, tax.Net, tax.Tax));
        Assert.Equal(new PaymentDto(PaymentMethod.Cash, 79m, null, null), Assert.Single(receipt.Payments));
        Assert.Equal(appointment.Id, Assert.Single(receipt.Lines).AppointmentId);
        Assert.False(receipt.SignatureDeviceFailed);
        var fields = receipt.FiscalCode!.Split('_');
        Assert.Equal("2", fields[3]);
        Assert.Equal("79,00", fields[5]);
        Assert.Equal(7900, await DecryptTurnoverAsync(register.Id, fields[10], 2));
        Assert.Equal(BillStatus.Settled, (await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct)).Status);

        var dep = await spa.GetAsync<DepExport>($"/api/pos/registers/{register.Id}/dep", Ct);
        var receipts = dep.Groups[0].Receipts;
        Assert.Equal(2, receipts.Count);
        VerifyJws(receipts[1], dep.Groups[0].Certificate);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(receipts[0])), 0, 8), fields[12]);
    }

    [Fact]
    public async Task Ein_Termin_kann_nur_einmal_abgerechnet_werden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        var second = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), TestSpa.Json, Ct);
        var unbilled = await spa.GetAsync<List<AppointmentDto>>(
            $"/api/pos/locations/{spa.Location.Id}/unbilled-appointments?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Empty(unbilled);
    }

    [Fact]
    public async Task Rechnung_wird_auf_zwei_Belege_mit_Karte_und_bar_geteilt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var oil = await spa.PostAsync<ArticleDto>("/api/pos/articles",
            new ArticleInput("Massageöl", 24.90m, TaxCategory.Normal, PosModel.Products.Id), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", ArticleLine(oil.Id, 2), Ct);
        Assert.Equal(79m + 49.80m, bill.Total);

        // Zwei Personen teilen: die eine zahlt die halbe Massage mit Karte, die andere den Rest bar.
        var massage = bill.Lines[0];
        var first = await CheckoutAsync(spa, bill.Id, [new LineAllocation(massage.Id, 39.50m)],
            new PaymentInput(PaymentMethod.Card, 39.50m, null, "Terminal 4711"));
        var open = await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct);
        var second = await CheckoutAsync(spa, bill.Id, null, Cash(89.30m));

        Assert.Equal(39.50m, first.Total);
        Assert.Contains("(Teilbetrag)", Assert.Single(first.Lines).Description, StringComparison.Ordinal);
        Assert.Equal(PaymentMethod.Card, Assert.Single(first.Payments).Method);
        Assert.Equal(BillStatus.Open, open.Status);
        Assert.Equal(89.30m, open.Open);
        Assert.Equal(89.30m, second.Total);
        Assert.Equal(2, second.Lines.Count);
        var settled = await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct);
        Assert.Equal(BillStatus.Settled, settled.Status);
        Assert.Equal([first.Id, second.Id], settled.ReceiptIds);
    }

    [Fact]
    public async Task Gutschein_beim_Online_Buchen_angeben_vermerkt_ihn_am_Termin_ohne_ihn_einzuloesen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var sale = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);
        sale = await spa.PostAsync<BillDto>($"/api/pos/bills/{sale.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 100m, null), Ct);
        sale = await spa.PostAsync<BillDto>($"/api/pos/bills/{sale.Id}/lines", VoucherLine(VoucherKind.SinglePurpose, null, spa.MassageTreatment.Id), Ct);
        var vouchers = (await CheckoutAsync(spa, sale.Id, null, new PaymentInput(PaymentMethod.Card, 179m, null, null))).IssuedVouchers;
        var value = vouchers.Single(v => v.Kind == VoucherKind.MultiPurpose);
        var other = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Peeling", 30, 0, 0, 45m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        using var client = spa.OnlineClient();
        OnlineBookingRequest Booking(Guid treatment, int hour, string code) => new(spa.Location.Id, treatment, TestSpa.At(hour),
            new OnlineContact("Vera", "Gutschein", $"vera-{Guid.NewGuid():N}@example.at", null), null, true, "de", false, code);

        var booked = await client.PostAsJsonAsync("/api/online/bookings",
            Booking(spa.MassageTreatment.Id, 10, $" {value.Code.ToLowerInvariant()} "), TestSpa.Json, Ct);
        var unknown = await client.PostAsJsonAsync("/api/online/bookings", Booking(spa.MassageTreatment.Id, 12, "XXXX-XXXX-XXXX"), TestSpa.Json, Ct);
        var wrongTreatment = await client.PostAsJsonAsync("/api/online/bookings",
            Booking(other.Id, 13, vouchers.Single(v => v.Kind == VoucherKind.SinglePurpose).Code), TestSpa.Json, Ct);

        var confirmation = await TestSpa.ReadAsync<OnlineBookingConfirmation>(booked, Ct);
        var appointment = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        Assert.Equal($"Gutschein {value.Code} angegeben, an der Kassa einlösen.", appointment.Note);
        Assert.Equal(100m, (await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{value.Code}", Ct)).Balance);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("voucherCode", await unknown.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTreatment.StatusCode);
    }

    [Fact]
    public async Task Wertgutschein_ist_beim_Verkauf_nicht_steuerbar_und_wird_teilweise_eingeloest()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var sale = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);
        sale = await spa.PostAsync<BillDto>($"/api/pos/bills/{sale.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 100m, null), Ct);
        var sold = await CheckoutAsync(spa, sale.Id, null, new PaymentInput(PaymentMethod.Card, 100m, null, null));
        var voucher = Assert.Single(sold.IssuedVouchers);

        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var redeemed = await CheckoutAsync(spa, bill.Id, null,
            new PaymentInput(PaymentMethod.Voucher, 79m, voucher.Code.ToLowerInvariant(), null));
        var after = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{voucher.Code}", Ct);

        Assert.Equal(TaxCategory.Zero, Assert.Single(sold.Taxes).TaxCategory);
        Assert.Equal("100,00", sold.FiscalCode!.Split('_')[8]);
        Assert.Equal(TaxCategory.Normal, Assert.Single(redeemed.Taxes).TaxCategory);
        Assert.Equal(new PaymentDto(PaymentMethod.Voucher, 79m, voucher.Code, null), Assert.Single(redeemed.Payments));
        Assert.Equal(21m, after.Balance);
        Assert.Equal(VoucherStatus.Active, after.Status);
    }

    [Fact]
    public async Task Rabatt_einer_offenen_Position_aendern_aber_nicht_bei_Gutscheinen_und_nicht_ueber_den_Betrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 50m, null), Ct);
        var (treatment, voucher) = (bill.Lines[0], bill.Lines[1]);

        var discounted = await Read<BillDto>(await spa.Client.PutAsJsonAsync($"/api/pos/bills/{bill.Id}/lines/{treatment.Id}/discount",
            new LineDiscountInput(10m), TestSpa.Json, Ct));
        var onVoucher = await spa.Client.PutAsJsonAsync($"/api/pos/bills/{bill.Id}/lines/{voucher.Id}/discount", new LineDiscountInput(5m), TestSpa.Json, Ct);
        var tooMuch = await spa.Client.PutAsJsonAsync($"/api/pos/bills/{bill.Id}/lines/{treatment.Id}/discount",
            new LineDiscountInput(treatment.UnitPrice), TestSpa.Json, Ct);
        var receipt = await CheckoutAsync(spa, bill.Id, null, Cash(discounted.Open));
        var afterPaid = await spa.Client.PutAsJsonAsync($"/api/pos/bills/{bill.Id}/lines/{treatment.Id}/discount", new LineDiscountInput(0m), TestSpa.Json, Ct);

        Assert.Equal(10m, discounted.Lines[0].Discount);
        Assert.Equal(treatment.Amount - 10m, discounted.Lines[0].Amount);
        Assert.Equal(bill.Total - 10m, discounted.Total);
        Assert.Equal(HttpStatusCode.BadRequest, onVoucher.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(treatment.Amount - 10m, receipt.Lines.Single(l => l.AppointmentId == appointment.Id).Amount);
        Assert.False(afterPaid.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Kundenrabatt_aus_der_Kundenkartei_zieht_die_Kassa_ab_und_der_Beleg_weist_ihn_aus()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/discount", new CustomerDiscount(10m), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);

        // Tee ohne Kundin, dann der Termin: mit ihm kommt die Kundin, ihr Rabatt gilt dann auch für den Tee. Gutscheine nie.
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        Assert.Equal(0m, bill.Lines[0].Discount);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, 0, null, null, null, null, null), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 50m, null), Ct);
        var (tea, treatment, voucher) = (bill.Lines[0], bill.Lines[1], bill.Lines[2]);

        Assert.Equal((1.25m, "Kundenrabatt 10 %"), (tea.Discount, tea.DiscountNote));
        Assert.Equal((7.90m, 71.10m, "Kundenrabatt 10 %"), (treatment.Discount, treatment.Amount, treatment.DiscountNote));
        Assert.Equal((0m, (string?)null), (voucher.Discount, voucher.DiscountNote));

        // Von Hand gegebener Rabatt geht vor.
        bill = await Read<BillDto>(await spa.Client.PutAsJsonAsync($"/api/pos/bills/{bill.Id}/lines/{tea.Id}/discount",
            new LineDiscountInput(2m), TestSpa.Json, Ct));
        Assert.Equal((2m, "Rabatt 16 %"), (bill.Lines[0].Discount, bill.Lines[0].DiscountNote));
        Assert.Equal(10.50m + 71.10m + 50m, bill.Total);

        var receipt = await CheckoutAsync(spa, bill.Id, null, Cash(bill.Open));
        var line = receipt.Lines.Single(l => l.AppointmentId == appointment.Id);
        Assert.Equal((71.10m, 7.90m, "Kundenrabatt 10 %"), (line.Amount, line.Discount, line.DiscountNote));
        Assert.Equal(131.60m, receipt.Total);
        Assert.Equal(131.60m, receipt.Taxes.Sum(t => t.Gross));

        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);
        var rows = text.Split('\n');
        var at = Array.FindIndex(rows, r => r.Contains(spa.MassageTreatment.Name, StringComparison.Ordinal));
        Assert.EndsWith("79,00", rows[at], StringComparison.Ordinal);
        Assert.Contains("Kundenrabatt 10 %", rows[at + 1], StringComparison.Ordinal);
        Assert.EndsWith("-7,90", rows[at + 1], StringComparison.Ordinal);

        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        var back = voided.Lines.Single(l => l.AppointmentId == appointment.Id);
        Assert.Equal((-71.10m, -7.90m), (back.Amount, back.Discount));
    }

    [Fact]
    public async Task Rabatt_in_Prozent_bis_zur_Grenze_fuer_alle_darueber_nur_mit_Kassenaufsicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var path = $"/api/pos/bills/{bill.Id}/lines/{bill.Lines[0].Id}/discount";

        var settings = await Read<PosSettingsDto>(await reception.GetAsync("/api/pos/settings", Ct));
        var twenty = await Read<BillDto>(await reception.PutAsJsonAsync(path, new LineDiscountInput(0, 20m), TestSpa.Json, Ct));
        var tooMuch = await reception.PutAsJsonAsync(path, new LineDiscountInput(0, 25m), TestSpa.Json, Ct);
        var tooMuchAsAmount = await reception.PutAsJsonAsync(path, new LineDiscountInput(16m), TestSpa.Json, Ct);
        var tooMuchOnNewLine = await reception.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 10m, 5m, "Tee", TaxCategory.Normal, null, null, null), TestSpa.Json, Ct);
        var byOwner = await Read<BillDto>(await spa.Client.PutAsJsonAsync(path, new LineDiscountInput(0, 25m), TestSpa.Json, Ct));
        var amount = await Read<BillDto>(await spa.Client.PutAsJsonAsync(path, new LineDiscountInput(10m), TestSpa.Json, Ct));
        var raisedByReception = await reception.PutAsJsonAsync("/api/pos/settings", new PosSettingsInput(50m), TestSpa.Json, Ct);
        await spa.PutAsync<PosSettingsDto>("/api/pos/settings", new PosSettingsInput(30m), Ct);
        var underNewLimit = await reception.PutAsJsonAsync(path, new LineDiscountInput(0, 25m), TestSpa.Json, Ct);

        Assert.Equal(new PosSettingsDto(20m, false), settings);
        Assert.Equal((15.80m, 63.20m, "Rabatt 20 %"), (twenty.Lines[0].Discount, twenty.Lines[0].Amount, twenty.Lines[0].DiscountNote));
        Assert.Equal(HttpStatusCode.Forbidden, tooMuch.StatusCode);
        Assert.Contains("über 20 %", await tooMuch.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, tooMuchAsAmount.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooMuchOnNewLine.StatusCode);
        Assert.Equal((19.75m, "Rabatt 25 %"), (byOwner.Lines[0].Discount, byOwner.Lines[0].DiscountNote));
        Assert.Equal((10m, "Rabatt 12,7 %"), (amount.Lines[0].Discount, amount.Lines[0].DiscountNote));
        Assert.Equal(HttpStatusCode.Forbidden, raisedByReception.StatusCode);
        Assert.Equal(HttpStatusCode.OK, underNewLimit.StatusCode);
    }

    [Fact]
    public async Task Kundenrabatt_nur_auf_Produkte_und_ueber_der_Grenze_nur_von_der_Standortleitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var path = $"/api/customers/{spa.Customer.Id}/discount";
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var byReception = await reception.PutAsJsonAsync(path, new CustomerDiscount(50m), TestSpa.Json, Ct);
        var withinLimit = await reception.PutAsJsonAsync(path, new CustomerDiscount(20m), TestSpa.Json, Ct);
        var tooHigh = await spa.Client.PutAsJsonAsync(path, new CustomerDiscount(100m), TestSpa.Json, Ct);
        var saved = await spa.PutAsync<CustomerDto>(path, new CustomerDiscount(15m, CustomerDiscountScope.Products), Ct);
        var oil = await spa.PostAsync<ArticleDto>("/api/pos/articles",
            new ArticleInput("Massageöl", 20m, TaxCategory.Normal, PosModel.Products.Id), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", ArticleLine(oil.Id, 2), Ct);
        var removed = await spa.PutAsync<CustomerDto>(path, new CustomerDiscount(0m), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, byReception.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withinLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Equal(new CustomerDiscount(15m, CustomerDiscountScope.Products), saved.Discount);
        Assert.Equal(0m, bill.Lines[0].Discount);
        Assert.Equal((6m, 34m), (bill.Lines[1].Discount, bill.Lines[1].Amount));
        Assert.Null(removed.Discount);
    }

    [Fact]
    public async Task Barzahlungsrabatt_eigene_Zeile_je_Steuersatz_nur_bei_reiner_Barzahlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var off = await spa.GetAsync<PosSettingsDto>("/api/pos/settings", Ct);
        var tooHigh = await spa.Client.PutAsJsonAsync("/api/pos/settings", new PosSettingsInput(20m, 12m), TestSpa.Json, Ct);
        var on = await spa.PutAsync<PosSettingsDto>("/api/pos/settings", new PosSettingsInput(20m, 3m), Ct);
        var keep = await spa.PutAsync<PosSettingsDto>("/api/pos/settings", new PosSettingsInput(25m), Ct);
        Assert.Equal(0m, off.CashDiscountPercent);
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Equal(3m, on.CashDiscountPercent);
        Assert.Equal((25m, 3m), (keep.DiscountLimitPercent, keep.CashDiscountPercent));

        // Behandlung 20 %, Buch 10 %, Gutschein (bleibt beim vollen Wert).
        async Task<BillDto> FullBillAsync(int hour)
        {
            var appointment = await spa.BookOkAsync(TestSpa.At(hour), Ct);
            await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
            bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Custom, null, null, 1, 30m, 0, "Buch", TaxCategory.Reduced1, null, null, null), Ct);
            return await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 50m, null), Ct);
        }

        var bill = await FullBillAsync(10);
        Assert.Equal((3m, 159m, 3.27m), (bill.CashDiscountPercent, bill.Open, bill.CashDiscount));

        var receipt = await CheckoutAsync(spa, bill.Id, null, Cash(200m));
        var discounts = receipt.Lines.Where(l => l.Description.StartsWith("Barzahlungsrabatt", StringComparison.Ordinal)).ToList();
        Assert.Equal([(-0.90m, 10m), (-2.37m, 20m)], discounts.Select(l => (l.Amount, l.TaxPercent)));
        Assert.All(discounts, l => Assert.Equal("Barzahlungsrabatt 3 %", l.Description));
        Assert.Equal(155.73m, receipt.Total);
        Assert.Equal(44.27m, receipt.Change);
        var normal = receipt.Taxes.Single(t => t.Percent == 20m);
        Assert.Equal((76.63m, 12.77m), (normal.Gross, normal.Tax));
        Assert.Equal((29.10m, 2.65m), (receipt.Taxes.Single(t => t.Percent == 10m).Gross, receipt.Taxes.Single(t => t.Percent == 10m).Tax));

        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);
        Assert.Contains("Barzahlungsrabatt 3 %", text, StringComparison.Ordinal);

        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        Assert.Equal(-155.73m, voided.Total);
        Assert.Contains(voided.Lines, l => l.Amount == 2.37m && l.Description == "Barzahlungsrabatt 3 %");

        // Teilweise mit Karte: kein Barzahlungsrabatt.
        var mixed = await FullBillAsync(13);
        var byCard = await CheckoutAsync(spa, mixed.Id, null, new PaymentInput(PaymentMethod.Card, 100m, null, null), Cash(59m));
        Assert.DoesNotContain(byCard.Lines, l => l.Description.StartsWith("Barzahlungsrabatt", StringComparison.Ordinal));
        Assert.Equal(159m, byCard.Total);
    }

    [Fact]
    public async Task Hoeheren_Rabatt_gibt_die_Leitung_mit_ihrer_PIN_am_Empfang_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var colleague = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var owner = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);
        var other = (await colleague.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!;
        (await spa.Client.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("258014"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await colleague.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("4711"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var path = $"/api/pos/bills/{bill.Id}/lines/{bill.Lines[0].Id}/discount";

        var approvers = (await reception.GetFromJsonAsync<List<QuickSwitchPerson>>($"/api/pos/approvers?locationId={spa.Location.Id}", TestSpa.Json, Ct))!;
        var wrongPin = await reception.PutAsJsonAsync(path, new LineDiscountInput(0, 25m, new SupervisorApproval(owner.Id, "000000")), TestSpa.Json, Ct);
        var byColleague = await reception.PutAsJsonAsync(path, new LineDiscountInput(0, 25m, new SupervisorApproval(other.Id, "4711")), TestSpa.Json, Ct);
        var approved = await Read<BillDto>(await reception.PutAsJsonAsync(path,
            new LineDiscountInput(0, 25m, new SupervisorApproval(owner.Id, "258014")), TestSpa.Json, Ct));

        Assert.Equal([owner.Id], approvers.Select(a => a.UserId));
        Assert.Equal(HttpStatusCode.Forbidden, wrongPin.StatusCode);
        Assert.Contains("PIN der Freigabe", await wrongPin.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, byColleague.StatusCode);
        Assert.Contains("nicht freigeben", await byColleague.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal((19.75m, "Rabatt 25 %"), (approved.Lines[0].Discount, approved.Lines[0].DiscountNote));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        Assert.Equal(owner.Id, await db.BillLines.Where(l => l.Id == bill.Lines[0].Id).Select(l => l.DiscountApprovedByUserId).SingleAsync(Ct));

        // Kundenkartei: dieselbe Freigabe.
        var customerPath = $"/api/customers/{spa.Customer.Id}/discount";
        var withoutApproval = await reception.PutAsJsonAsync(customerPath, new CustomerDiscountInput(30m), TestSpa.Json, Ct);
        var customer = await Read<CustomerDto>(await reception.PutAsJsonAsync(customerPath,
            new CustomerDiscountInput(30m, CustomerDiscountScope.All, new SupervisorApproval(owner.Id, "258014")), TestSpa.Json, Ct));
        Assert.Equal(HttpStatusCode.Forbidden, withoutApproval.StatusCode);
        Assert.Equal(new CustomerDiscount(30m), customer.Discount);
        Assert.Equal(owner.Id, await db.Customers.Where(c => c.Id == spa.Customer.Id).Select(c => c.DiscountApprovedByUserId).SingleAsync(Ct));
    }

    [Fact]
    public async Task Einzweckgutschein_wird_beim_Verkauf_versteuert_und_bei_der_Einloesung_nicht_noch_einmal()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var sale = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        sale = await spa.PostAsync<BillDto>($"/api/pos/bills/{sale.Id}/lines",
            VoucherLine(VoucherKind.SinglePurpose, null, spa.MassageTreatment.Id), Ct);
        var sold = await CheckoutAsync(spa, sale.Id, null, Cash(79m));
        var voucher = Assert.Single(sold.IssuedVouchers);

        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var redeemed = await CheckoutAsync(spa, bill.Id, null, new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null));

        Assert.Equal(13.17m, Assert.Single(sold.Taxes).Tax);
        Assert.Equal(0m, redeemed.Total);
        Assert.Empty(redeemed.Payments);
        Assert.Equal(0m, Assert.Single(redeemed.Taxes).Tax);
        Assert.Equal(-79m, redeemed.Lines[1].Amount);
        Assert.Equal(VoucherStatus.Redeemed, (await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{voucher.Code}", Ct)).Status);
    }

    [Fact]
    public async Task Paket_wird_beim_Kauf_versteuert_je_Termin_eingeloest_und_steht_im_Kalender()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var package = VoucherLine(VoucherKind.SinglePurpose, 200m, spa.MassageTreatment.Id) with { Units = 3, ValidUntil = ViennaToday.AddYears(1) };
        var anonymous = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        var withoutGuest = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{anonymous.Id}/lines", package, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutGuest.StatusCode);

        // Heute bezahlt der Gast seine Massage und kauft dazu einen 3er-Block zum Paketpreis.
        var today = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.SetStatusAsync(today.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [today.Id]), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", package, Ct);
        var sold = await CheckoutAsync(spa, bill.Id, null, Cash(279m));
        var voucher = Assert.Single(sold.IssuedVouchers);

        Assert.Equal("Paket 3× Massage", sold.Lines[1].Description);
        Assert.Equal(279m, sold.Total);
        Assert.Equal((3, 3, spa.Customer.Id), (voucher.Units, voucher.UnitsLeft, voucher.CustomerId));
        var week = TestSpa.Monday.AddDays(7);
        var first = await spa.BookOkAsync(TestSpa.At(10, date: week), Ct, employee: spa.Anna.Id);
        var second = await spa.BookOkAsync(TestSpa.At(10, date: week), Ct, employee: spa.Ben.Id);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={week:yyyy-MM-dd}", Ct);
        Assert.Equal(new GuestPackage(spa.MassageTreatment.Id, "Massage", 3, 3, ViennaToday.AddYears(1)),
            Assert.Single(Assert.Single(day.Guests!).Packages!));
        Assert.Equal(voucher.Code, Assert.Single(await spa.GetAsync<List<VoucherDto>>($"/api/pos/customers/{spa.Customer.Id}/packages", Ct)).Code);

        // Zwei Termine auf einer Rechnung: beide werden abgezogen, zu zahlen bleibt nichts.
        await spa.SetStatusAsync(first.Id, AppointmentStatus.CheckedIn, Ct);
        await spa.SetStatusAsync(second.Id, AppointmentStatus.CheckedIn, Ct);
        var visit = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [first.Id, second.Id]), Ct);
        var redeemed = await CheckoutAsync(spa, visit.Id, null, new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null));
        var after = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{voucher.Code}", Ct);

        Assert.Equal(0m, redeemed.Total);
        Assert.Equal(0m, Assert.Single(redeemed.Taxes).Tax);
        Assert.Equal([$"Paket {voucher.Code} eingelöst (1/3)", $"Paket {voucher.Code} eingelöst (2/3)"],
            redeemed.Lines.Where(l => l.Amount < 0).Select(l => l.Description));
        Assert.Equal((1, VoucherStatus.Active), (after.UnitsLeft, after.Status));
        Assert.Equal(200m - 2 * 66.67m, after.Balance);
        var storno = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{sold.Id}/void", new VoidRequest("Paket zurück"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, storno.StatusCode);

        // Storno der Einlösung gibt beide Behandlungen zurück.
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{redeemed.Id}/void", new VoidRequest("Falsch abgezogen"), Ct);
        var restored = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{voucher.Code}", Ct);
        Assert.Equal((3, 200m), (restored.UnitsLeft, restored.Balance));

        // Wieder offen: die letzte Behandlung leert das Paket.
        var three = await CheckoutAsync(spa, visit.Id, null, new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null));
        Assert.Equal(0m, three.Total);
        var lastDay = TestSpa.Monday.AddDays(14);
        var last = await spa.BookOkAsync(TestSpa.At(10, date: lastDay), Ct, employee: spa.Anna.Id);
        await spa.SetStatusAsync(last.Id, AppointmentStatus.CheckedIn, Ct);
        var lastBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [last.Id]), Ct);
        await CheckoutAsync(spa, lastBill.Id, null, new PaymentInput(PaymentMethod.Voucher, 0, voucher.Code, null));
        var empty = await spa.GetAsync<VoucherDto>($"/api/pos/vouchers/{voucher.Code}", Ct);
        Assert.Equal((0, 0m, VoucherStatus.Redeemed), (empty.UnitsLeft, empty.Balance, empty.Status));
        Assert.Empty(await spa.GetAsync<List<VoucherDto>>($"/api/pos/customers/{spa.Customer.Id}/packages", Ct));
    }

    [Fact]
    public async Task Storno_braucht_einen_Grund_zaehlt_negativ_und_oeffnet_die_Rechnung_wieder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var receipt = await CheckoutAsync(spa, bill.Id, null, new PaymentInput(PaymentMethod.Card, 79m, null, null));

        var withoutReason = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest(" "), TestSpa.Json, Ct);
        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsche Behandlung"), Ct);
        var twice = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Nochmal"), TestSpa.Json, Ct);
        var reopened = await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct);
        var again = await CheckoutAsync(spa, bill.Id, null, Cash(79m));

        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(ReceiptKind.Void, voided.Kind);
        Assert.Equal(-79m, voided.Total);
        Assert.Equal(receipt.Id, voided.VoidsReceiptId);
        Assert.Equal(-79m, Assert.Single(voided.Payments).Amount);
        Assert.Equal(RksvCode.VoidMarker, voided.FiscalCode!.Split('_')[10]);
        Assert.Equal("-79,00", voided.FiscalCode.Split('_')[5]);
        Assert.Equal(voided.Id, (await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}", Ct)).VoidedByReceiptId);
        Assert.Equal(BillStatus.Open, reopened.Status);
        Assert.Equal(79m, reopened.Open);
        // Umsatzzähler: +79 −79 +79.
        Assert.Equal(7900, await DecryptTurnoverAsync(register.Id, again.FiscalCode!.Split('_')[10], again.Number));
    }

    [Fact]
    public async Task Bei_Ausfall_der_Signatureinrichtung_wird_weiter_kassiert_und_danach_ein_Nullbeleg_erstellt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var device = fixture.Factory.Services.GetRequiredService<TestSignatureDeviceSwitch>();

        device.SetFailing(register.Id, true);
        var failed = await SellArticleAsync(spa, register);
        var duringFailure = await spa.GetAsync<CashRegisterDto>($"/api/pos/registers/{register.Id}", Ct);
        device.SetFailing(register.Id, false);
        var recovered = await SellArticleAsync(spa, register);
        var receipts = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);

        Assert.True(failed.SignatureDeviceFailed);
        Assert.EndsWith("_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Sicherheitseinrichtung ausgefallen")), failed.FiscalCode, StringComparison.Ordinal);
        Assert.NotNull(duringFailure.SignatureDeviceFailedSince);
        Assert.Contains(duringFailure.Warnings, w => w.Contains("ausgefallen", StringComparison.Ordinal));
        Assert.False(recovered.SignatureDeviceFailed);
        Assert.Equal(failed.Number + 2, recovered.Number);
        Assert.Contains(receipts, r => r.Kind == ReceiptKind.Null && r.Number == failed.Number + 1 && !r.SignatureDeviceFailed);
        Assert.Null((await spa.GetAsync<CashRegisterDto>($"/api/pos/registers/{register.Id}", Ct)).SignatureDeviceFailedSince);
    }

    [Fact]
    public async Task Tagesabschluss_rechnet_Kassenbestand_und_Differenz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var account = await spa.PostAsync<RevenueAccountDto>("/api/pos/revenue-accounts",
            new RevenueAccountInput($"48{Random.Shared.Next(10000, 99999)}", "Erlöse Shop Hotel", "KST-SHOP"), Ct);
        var shop = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Bademantel", 60m, TaxCategory.Normal, account.Id), Ct);
        await spa.PostAsync<CashMovementDto>($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Deposit, 150m, "Wechselgeld"), Ct);
        await SellArticleAsync(spa, register, shop.Id, Cash(100m));
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", ArticleLine(shop.Id, 1), Ct);
        await CheckoutAsync(spa, bill.Id, null, new PaymentInput(PaymentMethod.InstantTransfer, 60m, null, "SEPA Inst"));
        await spa.PostAsync<CashMovementDto>($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Withdrawal, 50m, "Bank"), Ct);

        var closing = await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(158m, null), Ct);
        var next = await spa.GetAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-report", Ct);

        Assert.Equal(2, closing.ReceiptCount);
        Assert.Equal(120m, closing.Revenue);
        Assert.Equal([new PaymentTotal(PaymentMethod.Cash, 60m), new PaymentTotal(PaymentMethod.InstantTransfer, 60m)], closing.Payments);
        Assert.Equal(160m, closing.ExpectedCash);
        Assert.Equal(-2m, closing.Difference);
        var shopTotal = Assert.Single(closing.Accounts);
        Assert.Equal(("KST-SHOP", 120m, 100m), (shopTotal.CostCenter, shopTotal.Gross, shopTotal.Net));
        Assert.Equal(0, next.ReceiptCount);
        Assert.Equal(158m, next.OpeningCash);
    }

    [Fact]
    public async Task Zu_wenig_bezahlt_wird_abgelehnt_und_es_entsteht_kein_Beleg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [Cash(50m)]), TestSpa.Json, Ct);
        var tooMuchCard = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 100m, null, null)]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuchCard.StatusCode);
        Assert.Equal(1, (await spa.GetAsync<CashRegisterDto>($"/api/pos/registers/{register.Id}", Ct)).LastReceiptNumber);
        Assert.Equal(79m, (await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct)).Open);
    }

    [Fact]
    public async Task Gleichzeitiges_Kassieren_ergibt_lueckenlose_Belegnummern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);

        var receipts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SellArticleAsync(spa, register)));

        Assert.Equal(Enumerable.Range(2, 8).Select(i => (long)i), receipts.Select(r => r.Number).Order());
        var dep = await spa.GetAsync<DepExport>($"/api/pos/registers/{register.Id}/dep", Ct);
        var jws = dep.Groups[0].Receipts;
        for (var i = 1; i < jws.Count; i++)
        {
            var chain = VerifyJws(jws[i], dep.Groups[0].Certificate).Split('_')[12];
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(jws[i - 1])), 0, 8), chain);
        }
    }

    [Fact]
    public async Task Schweizer_Kasse_kassiert_ohne_Signatur()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var swiss = await CreateRegisterAsync(spa, FiscalCountry.CH);
        var receipt = await SellArticleAsync(spa, swiss);

        Assert.Null(receipt.FiscalCode);
        Assert.Equal(8.1m, Assert.Single(receipt.Taxes).Percent);
    }

    [Fact]
    public async Task Sofortueberweisung_liefert_EPC_QR_Code_des_offenen_Betrags()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        var qr = await spa.GetAsync<InstantTransferQr>($"/api/pos/bills/{bill.Id}/instant-transfer", Ct);

        var lines = qr.Payload.Split('\n');
        Assert.Equal(["BCD", "002", "1", "SCT"], lines[..4]);
        Assert.Equal("AT611904300234573201", lines[6]);
        Assert.Equal("EUR79.00", lines[7]);
    }

    [Fact]
    public async Task Monats_und_Jahresbeleg_entstehen_beim_ersten_Beleg_im_neuen_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var clock = new MovableTime(new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        // Ohne HTTP-Anfrage wie ein Hintergrundauftrag: der Mandant muss ausdrücklich gesetzt werden.
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use((await spa.GetAsync<CurrentUser>("/api/auth/me", Ct)).Access!.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var pos = new PosService(db, scope.ServiceProvider.GetRequiredService<FiscalizationResolver>(), clock, scope.ServiceProvider.GetRequiredService<IOptions<PaymentOptions>>(),
            scope.ServiceProvider.GetRequiredService<SiteIdentity>());
        var register = (await pos.CreateRegisterAsync(RegisterInput(spa, FiscalCountry.AT), Ct)).Value!;

        clock.Now = new(2027, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var february = (await pos.NullReceiptAsync(register.Id, Ct)).Value!;
        clock.Now = new(2028, 1, 2, 9, 0, 0, TimeSpan.Zero);
        var nextYear = (await pos.NullReceiptAsync(register.Id, Ct)).Value!;

        var kinds = await db.Receipts.AsNoTracking().Where(r => r.RegisterId == register.Id).OrderBy(r => r.Number).Select(r => r.Kind).ToListAsync(Ct);
        Assert.Equal([ReceiptKind.Start, ReceiptKind.Monthly, ReceiptKind.Null, ReceiptKind.Yearly, ReceiptKind.Null], kinds);
        Assert.Equal(3, february.Number);
        Assert.Equal(5, nextYear.Number);
    }

    [Fact]
    public async Task Kasse_einer_anderen_Stelle_kassiert_hier_nicht_und_eine_Kasse_ohne_Stelle_gehoert_der_ersten()
    {
        // Abgleich (ADR 0041): zwei Stellen dürfen nicht an derselben Kasse Belege erzeugen, sonst entstehen zwei Signaturketten.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var here = scope.ServiceProvider.GetRequiredService<SiteIdentity>().Get(db);
        Assert.NotNull(here);
        Assert.Equal(here, await db.CashRegisters.Where(r => r.Id == register.Id).Select(r => r.SiteId).SingleAsync(Ct));
        Assert.False(register.OtherSite);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);

        // Wie vom Abgleich gebracht: die Kasse gehört einer anderen Stelle.
        await db.CashRegisters.Where(r => r.Id == register.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.SiteId, Guid.NewGuid()), Ct);
        var shown = await spa.GetAsync<CashRegisterDto>($"/api/pos/registers/{register.Id}", Ct);
        Assert.True(shown.OtherSite);
        Assert.Contains(shown.Warnings, w => w.StartsWith("Diese Kasse kassiert an einer anderen Stelle", StringComparison.Ordinal));

        var other = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        var refused = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [Cash(79m)]), Ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var drawer = await spa.Client.PostAsJsonAsync($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Deposit, 150m, "Wechselgeld"), Ct);
        Assert.Equal(HttpStatusCode.Conflict, drawer.StatusCode);
        var closing = await spa.Client.PostAsJsonAsync($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0m, null), Ct);
        Assert.Equal(HttpStatusCode.Conflict, closing.StatusCode);
        Assert.Equal(1, await db.Receipts.CountAsync(r => r.RegisterId == register.Id, Ct));

        // Kasse aus der Zeit vor der Zuordnung: die erste Stelle, die kassiert, übernimmt sie.
        await db.CashRegisters.Where(r => r.Id == register.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.SiteId, (Guid?)null), Ct);
        await CheckoutAsync(spa, bill.Id, null, Cash(79m));
        Assert.Equal(here, await db.CashRegisters.Where(r => r.Id == register.Id).Select(r => r.SiteId).SingleAsync(Ct));
    }

    [Fact]
    public async Task Digitaler_Euro_ist_vorbereitet_und_erst_eingeschaltet_waehlbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        var digital = new PaymentInput(PaymentMethod.DigitalEuro, bill.Open, null, "DE-TX-1");

        // Ausgeschaltet (Standard): nicht angeboten und abgelehnt.
        Assert.DoesNotContain(PaymentMethod.DigitalEuro, register.PaymentMethods);
        var refused = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [digital]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Digitaler Euro ist an dieser Kasse nicht verfügbar", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        // Eingeschaltet: in Euro-Ländern wählbar, in der Schweiz nicht.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var pos = new PosService(db, scope.ServiceProvider.GetRequiredService<FiscalizationResolver>(), TimeProvider.System,
            Options.Create(new PaymentOptions { DigitalEuro = true }), scope.ServiceProvider.GetRequiredService<SiteIdentity>());
        var swiss = (await pos.CreateRegisterAsync(RegisterInput(spa, FiscalCountry.CH), Ct)).Value!;
        Assert.DoesNotContain(PaymentMethod.DigitalEuro, swiss.PaymentMethods);
        var austrian = pos.ToDto(await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == register.Id, Ct));
        Assert.Contains(PaymentMethod.DigitalEuro, austrian.PaymentMethods);

        var receipt = (await pos.CheckoutAsync(bill.Id, new CheckoutRequest(null, [digital]), null, Ct)).Value!;
        var payment = Assert.Single(receipt.Payments);
        Assert.Equal((PaymentMethod.DigitalEuro, bill.Open), (payment.Method, payment.Amount));
    }

    [Theory]
    [InlineData(12.47, -0.02)]
    [InlineData(12.48, 0.02)]
    [InlineData(12.42, -0.02)]
    [InlineData(12.43, 0.02)]
    [InlineData(12.45, 0)]
    [InlineData(0, 0)]
    public void Fuenf_Rappen_Rundung(double due, double rounding) => Assert.Equal((decimal)rounding, PosService.CashRounding((decimal)due));

    [Fact]
    public async Task Schweizer_Kasse_rundet_Barzahlung_auf_5_Rappen_Karte_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.CH);

        async Task<ReceiptDto> SellAsync(PaymentMethod method, decimal amount)
        {
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
            await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Custom, null, null, 1, 12.48m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
            return await CheckoutAsync(spa, bill.Id, null, new PaymentInput(method, amount, null, null));
        }

        var cash = await SellAsync(PaymentMethod.Cash, 20m);
        var card = await SellAsync(PaymentMethod.Card, 12.48m);

        Assert.Equal(12.50m, cash.Total);
        Assert.Equal(7.50m, cash.Change);
        Assert.Contains(cash.Lines, l => l is { Description: "Rundungsdifferenz (5 Rappen)", Amount: 0.02m, TaxPercent: 0 });
        Assert.Equal(12.50m, Assert.Single(cash.Payments).Amount);
        Assert.Equal(12.48m, card.Total);
        Assert.DoesNotContain(card.Lines, l => l.Description.StartsWith("Rundung", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Beleg_fuer_den_Bondrucker_als_Text_und_ESC_POS()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var receipt = await SellArticleAsync(spa, register);

        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);
        var escpos = await spa.Client.GetByteArrayAsync($"/api/pos/receipts/{receipt.Id}/print?format=EscPos", Ct);

        Assert.All(text.TrimEnd('\n').Split('\n'), line => Assert.True(line.Length <= 42, line));
        Assert.Contains("Summe EUR", text, StringComparison.Ordinal);
        Assert.Contains("12,50", text, StringComparison.Ordinal);
        Assert.Equal([0x1B, 0x40], escpos[..2]);
        Assert.NotNull(receipt.FiscalCode);
        Assert.True(escpos.AsSpan().IndexOf(Encoding.UTF8.GetBytes(receipt.FiscalCode!)) > 0);
        Assert.Equal([0x1D, 0x56, 0x42, 0], escpos[^4..]);
    }

    [Fact]
    public async Task Rechnung_mit_Empfaenger_und_Unternehmensangaben_des_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = (await spa.GetAsync<List<LocationDto>>("/api/locations", Ct)).Single(l => l.Id == spa.Location.Id);
        var updated = await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}", new LocationInput(location.Name, location.TimeZone,
            location.Currency, location.OpeningHours, location.Region, null,
            new LocationCompany("Test-Spa GmbH", " ATU12345678 ", "FN 123456a", "Handelsgericht Wien")), Ct);
        var register = await CreateRegisterAsync(spa);
        var receipt = await SellArticleAsync(spa, register);
        var voided = await SellArticleAsync(spa, register);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{voided.Id}/void", new VoidRequest("Irrtum"), Ct);
        var plain = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);

        var missing = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/recipient", new InvoiceRecipient("Firma", " "), TestSpa.Json, Ct);
        var invoiced = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/recipient",
            new InvoiceRecipient("Beispiel Consulting GmbH", "Ringstraße 1\n1010 Wien", "atu87654321"), Ct);
        var twice = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/recipient", new InvoiceRecipient("Andere", "Gasse 2"), TestSpa.Json, Ct);
        var ofVoided = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{voided.Id}/recipient", new InvoiceRecipient("Firma", "Gasse 2"), TestSpa.Json, Ct);
        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);

        Assert.Equal(new LocationCompany("Test-Spa GmbH", "ATU12345678", "FN 123456a", "Handelsgericht Wien"), updated.Company);
        Assert.Equal(400m, receipt.SmallInvoiceLimit);
        Assert.Contains("UID ATU12345678", plain, StringComparison.Ordinal);
        Assert.Contains("FN 123456a, Handelsgericht Wien", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("Rechnung an", plain, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(new InvoiceRecipient("Beispiel Consulting GmbH", "Ringstraße 1\n1010 Wien", "ATU87654321"), invoiced.Recipient);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ofVoided.StatusCode);
        Assert.Contains("Rechnung an:\nBeispiel Consulting GmbH\nRingstraße 1\n1010 Wien\nUID ATU87654321\n", text, StringComparison.Ordinal);
        Assert.Contains($"Rechnung Nr. {register.Code}-{receipt.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Rechnungs- und Leistungsdatum", text, StringComparison.Ordinal);
        Assert.All(text.TrimEnd('\n').Split('\n'), line => Assert.True(line.Length <= 42, line));
    }

    [Fact]
    public async Task UID_Pruefung_ueber_VIES_mit_Nachweis_an_der_Rechnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = (await spa.GetAsync<List<LocationDto>>("/api/locations", Ct)).Single(l => l.Id == spa.Location.Id);
        await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}", new LocationInput(location.Name, location.TimeZone,
            location.Currency, location.OpeningHours, location.Region, null, new LocationCompany("Test-Spa GmbH", "ATU12345678", null, null)), Ct);
        var register = await CreateRegisterAsync(spa);
        var receipt = await SellArticleAsync(spa, register);

        var swiss = await spa.Client.PostAsJsonAsync("/api/pos/vat-checks", new VatIdCheckInput("CHE-123.456.789", register.Id), TestSpa.Json, Ct);
        var started = await spa.PostAsync<VatIdCheckDto>("/api/pos/vat-checks", new VatIdCheckInput("atu 876 543 21", register.Id), Ct);
        var invalid = await spa.PostAsync<VatIdCheckDto>("/api/pos/vat-checks", new VatIdCheckInput("ATU87654000", register.Id), Ct);
        var down = await spa.PostAsync<VatIdCheckDto>("/api/pos/vat-checks", new VatIdCheckInput("DE123456999", register.Id), Ct);
        var valid = await WaitCheckAsync(spa, started.Id);
        var mismatch = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/recipient",
            new InvoiceRecipient("Beispiel GmbH", "Ring 1", "ATU11111111", valid.Id), TestSpa.Json, Ct);
        var invoiced = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/recipient",
            new InvoiceRecipient("Beispiel GmbH", "Ring 1", "ATU87654321", valid.Id), Ct);
        var history = await spa.GetAsync<List<VatIdCheckDto>>("/api/pos/vat-checks?vatId=ATU87654321", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, swiss.StatusCode);
        Assert.Equal(("ATU87654321", "ATU12345678", VatIdCheckStatus.Pending), (started.VatId, started.RequesterVatId, started.Status));
        Assert.Equal((VatIdCheckStatus.Valid, "Testfirma GmbH", "TESTU87654321"), (valid.Status, valid.Name, valid.ConsultationNumber));
        Assert.Equal(VatIdCheckStatus.Invalid, (await WaitCheckAsync(spa, invalid.Id)).Status);
        var failed = await WaitCheckAsync(spa, down.Id);
        Assert.Equal(VatIdCheckStatus.Failed, failed.Status);
        Assert.NotNull(failed.Message);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(valid.Id, invoiced.Recipient!.VatIdCheckId);
        Assert.Equal(valid.Id, Assert.Single(history).Id);
    }

    private static async Task<VatIdCheckDto> WaitCheckAsync(TestSpa spa, Guid id)
    {
        for (var i = 0; ; i++)
        {
            var check = await spa.GetAsync<VatIdCheckDto>($"/api/pos/vat-checks/{id}", Ct);
            if (check.Status != VatIdCheckStatus.Pending || i == 100)
            {
                return check;
            }

            await Task.Delay(100, Ct);
        }
    }

    [Fact]
    public async Task Rezeption_storniert_nur_eigene_frische_Belege_und_entnimmt_kein_Bargeld()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var bill = await Read<BillDto>(await reception.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, []), TestSpa.Json, Ct));
        bill = await Read<BillDto>(await reception.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), TestSpa.Json, Ct));
        var own = await Read<ReceiptDto>(await reception.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [Cash(bill.Open)]), TestSpa.Json, Ct));
        var byOwner = await SellArticleAsync(spa, register);

        var voidOwn = await reception.PostAsJsonAsync($"/api/pos/receipts/{own.Id}/void", new VoidRequest("Fehlgriff"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, voidOwn.StatusCode);
        var voidOther = await reception.PostAsJsonAsync($"/api/pos/receipts/{byOwner.Id}/void", new VoidRequest("Fehlgriff"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, voidOther.StatusCode);
        Assert.Contains("Standortleitung", await voidOther.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{byOwner.Id}/void", new VoidRequest("Fehlgriff"), TestSpa.Json, Ct)).StatusCode);

        var deposit = await reception.PostAsJsonAsync($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Deposit, 50m, "Wechselgeld"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        var withdrawal = await reception.PostAsJsonAsync($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Withdrawal, 50m, "Bank"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, withdrawal.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/pos/registers/{register.Id}/dep", Ct)).StatusCode);

        // Den Tagesabschluss macht abends oft die Rezeption.
        var closing = await reception.PostAsJsonAsync($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(50m, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, closing.StatusCode);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(TestSpa.Json, Ct))!;
    }

    [Fact]
    public async Task Eintritt_in_einen_Bereich_wird_mit_dem_Artikel_des_Bereichs_je_Person_abgerechnet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var dayPass = await spa.PostAsync<ArticleDto>("/api/pos/articles",
            new ArticleInput("Tageseintritt Sauna", 32m, TaxCategory.Reduced2, PosModel.Products.Id), Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms",
            new RoomInput("Sauna", 0, 10, IsArea: true, AdmissionArticleId: dayPass.Id), Ct);
        var visit = await spa.PostAsync<AreaVisitDto>("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(10), TestSpa.At(13), 2), Ct);

        var unbilled = await spa.GetAsync<List<AreaVisitDto>>(
            $"/api/pos/locations/{spa.Location.Id}/unbilled-visits?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [], [visit.Id]), Ct);
        var receipt = await CheckoutAsync(spa, bill.Id, null, Cash(64m));
        var again = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, [], [visit.Id]), TestSpa.Json, Ct);
        var left = await spa.GetAsync<List<AreaVisitDto>>(
            $"/api/pos/locations/{spa.Location.Id}/unbilled-visits?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(visit.Id, Assert.Single(unbilled).Id);
        Assert.Equal(sauna.Id, Assert.Single(unbilled).RoomId);
        Assert.Equal(dayPass.Id, sauna.AdmissionArticleId);
        var line = Assert.Single(bill.Lines);
        Assert.Equal((BillLineKind.AreaVisit, 2, 32m, 64m, TaxCategory.Reduced2, visit.Id),
            (line.Kind, line.Quantity, line.UnitPrice, line.Amount, line.TaxCategory, line.AreaVisitId));
        Assert.StartsWith("Tageseintritt Sauna 01.03.2027 10:00, Sauna", line.Description, StringComparison.Ordinal);
        Assert.Equal(spa.Customer.Id, bill.CustomerId);
        Assert.Equal(13m, Assert.Single(receipt.Taxes).Percent);
        Assert.Equal(64m, receipt.Total);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Empty(left);
    }

    [Fact]
    public async Task Eintritt_ohne_Artikel_am_Bereich_kann_nicht_abgerechnet_werden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var ruheraum = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms",
            new RoomInput("Ruheraum", 0, 10, IsArea: true), Ct);
        var visit = await spa.PostAsync<AreaVisitDto>("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, ruheraum.Id, spa.Customer.Id, TestSpa.At(10), TestSpa.At(12)), Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, [], [visit.Id]), TestSpa.Json, Ct);
        var unknownArticle = await spa.Client.PutAsync($"/api/rooms/{ruheraum.Id}",
            JsonContent.Create(new RoomInput("Ruheraum", 0, 10, IsArea: true, AdmissionArticleId: Guid.NewGuid()), options: TestSpa.Json), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("kein Artikel für den Eintritt", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, unknownArticle.StatusCode);
    }

    [Fact]
    public async Task Belegliste_liefert_dieselben_Belege_wie_die_Einzelabfrage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var tea = await SellArticleAsync(spa, register);
        _ = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{tea.Id}/void", new VoidRequest("Falsch gebucht"), Ct);
        var voucherSale = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        voucherSale = await spa.PostAsync<BillDto>($"/api/pos/bills/{voucherSale.Id}/lines", VoucherLine(VoucherKind.MultiPurpose, 50m, null), Ct);
        _ = await CheckoutAsync(spa, voucherSale.Id, null, Cash(voucherSale.Open));

        var list = await spa.GetAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={ViennaToday:yyyy-MM-dd}", Ct);
        var single = new List<ReceiptDto>();
        foreach (var receipt in list)
        {
            single.Add(await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}", Ct));
        }

        Assert.Equal(list.Select(r => r.Number).OrderDescending(), list.Select(r => r.Number));
        Assert.Contains(list, r => r.VoidedByReceiptId is not null);
        Assert.Contains(list, r => r.IssuedVouchers.Count > 0);
        Assert.Equal(JsonSerializer.Serialize(single, TestSpa.Json), JsonSerializer.Serialize(list, TestSpa.Json));
    }

    [Fact]
    public async Task Belegliste_fragt_die_Datenbank_unabhaengig_von_der_Zahl_der_Belege_gleich_oft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await SellArticleAsync(spa, register)).Id);
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var pos = scope.ServiceProvider.GetRequiredService<PosService>();
        _ = await pos.GetReceiptsAsync(ids, Ct);

        var forOne = await CommandCount.OfAsync(() => pos.GetReceiptsAsync([ids[0]], Ct));
        var forThree = await CommandCount.OfAsync(() => pos.GetReceiptsAsync(ids, Ct));

        Assert.True(forOne > 0);
        Assert.Equal(forOne, forThree);
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa, FiscalCountry country = FiscalCountry.AT) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", RegisterInput(spa, country), Ct);

    private static CashRegisterInput RegisterInput(TestSpa spa, FiscalCountry country) =>
        new(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], country, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
            "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null);

    private static async Task<ReceiptDto> CheckoutAsync(TestSpa spa, Guid billId, IReadOnlyList<LineAllocation>? lines, params PaymentInput[] payments) =>
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{billId}/checkout", new CheckoutRequest(lines, payments), Ct);

    private static async Task<ReceiptDto> SellArticleAsync(TestSpa spa, CashRegisterDto register, Guid? articleId = null, PaymentInput? payment = null)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        var line = articleId is { } id
            ? ArticleLine(id, 1)
            : new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", line, Ct);
        return await CheckoutAsync(spa, bill.Id, null, payment ?? Cash(bill.Open));
    }

    private static BillLineInput ArticleLine(Guid articleId, int quantity) =>
        new(BillLineKind.Article, null, articleId, quantity, null, 0, null, null, null, null, null);

    private static BillLineInput VoucherLine(VoucherKind kind, decimal? value, Guid? treatmentId) =>
        new(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, kind, treatmentId);

    private static PaymentInput Cash(decimal amount) => new(PaymentMethod.Cash, amount, null, null);

    /// <summary>Prüft die ES256-Signatur gegen das Zertifikat aus dem DEP und liefert den maschinenlesbaren Code.</summary>
    private static string VerifyJws(string jws, string certificate)
    {
        var parts = jws.Split('.');
        Assert.Equal(RksvCode.JwsHeader, parts[0]);
        using var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate));
        using var key = cert.GetECDsaPublicKey()!;
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), RksvCode.FromBase64Url(parts[2]), HashAlgorithmName.SHA256),
            "Signatur ungültig");
        return Encoding.UTF8.GetString(RksvCode.FromBase64Url(parts[1]));
    }

    private async Task<long> DecryptTurnoverAsync(Guid registerId, string encrypted, long number)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var register = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().CashRegisters.AsNoTracking().FirstAsync(r => r.Id == registerId, Ct);
        return RksvCode.DecryptTurnover(encrypted, register.Code, number, Convert.FromBase64String(register.TurnoverKey!));
    }

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

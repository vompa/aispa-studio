using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Standard-Ablaufdatum für Gutscheine je Land der Kasse (DE 31.12. des dritten Jahres, CH 5 Jahre, AT ohne), Vorbelegung und Hinweis.</summary>
public sealed class VoucherExpiryTests(ApiFixture fixture)
{
    private const string AustrianWarning = "In Österreich sind Fristen unter 5 Jahren ohne Auszahlungsmöglichkeit unwirksam.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TimeZoneInfo Vienna => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly ViennaToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static DateOnly Day(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(FiscalCountry.DE, "2026-11-09", "2029-12-31")]
    [InlineData(FiscalCountry.DE, "2026-12-31", "2029-12-31")]
    [InlineData(FiscalCountry.DE, "2027-01-01", "2030-12-31")]
    [InlineData(FiscalCountry.CH, "2026-11-09", "2031-11-09")]
    [InlineData(FiscalCountry.CH, "2028-02-29", "2033-02-28")]
    [InlineData(FiscalCountry.AT, "2026-11-09", null)]
    public void Standardfrist_je_Land(FiscalCountry country, string purchase, string? expected)
    {
        var result = VoucherExpiry.Default(country, Day(purchase));

        Assert.Equal(expected is null ? null : Day(expected), result);
    }

    [Theory]
    [InlineData(FiscalCountry.DE, "2029-12-30", "31.12.2029")]
    [InlineData(FiscalCountry.DE, "2029-12-31", null)]
    [InlineData(FiscalCountry.DE, "2035-01-01", null)]
    [InlineData(FiscalCountry.DE, null, null)]
    [InlineData(FiscalCountry.CH, "2031-11-08", "09.11.2031")]
    [InlineData(FiscalCountry.CH, "2031-11-09", null)]
    [InlineData(FiscalCountry.AT, "2040-01-01", AustrianWarning)]
    [InlineData(FiscalCountry.AT, null, null)]
    public void Hinweis_bei_kuerzerer_Frist_und_in_Oesterreich_bei_jedem_Datum(FiscalCountry country, string? until, string? expectedPart)
    {
        var warning = VoucherExpiry.Warning(country, new DateOnly(2026, 11, 9), until is null ? null : Day(until));

        if (expectedPart is null)
        {
            Assert.Null(warning);
        }
        else
        {
            Assert.Contains(expectedPart, warning, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Verkauf_setzt_ohne_Angabe_die_Landesfrist_mit_Angabe_das_Datum_und_auf_Wunsch_keins()
    {
        // Gemeinsame Regel für den Dienst und die Beispieldaten im Speicher; beide rufen sie mit dem Ortsdatum der Kasse.
        var today = new DateOnly(2026, 11, 9);
        var line = Voucher(VoucherKind.MultiPurpose, 50m, null);

        Assert.Equal(new DateOnly(2029, 12, 31), PosService.VoucherValidUntil(FiscalCountry.DE, line, today));
        Assert.Equal(new DateOnly(2031, 11, 9), PosService.VoucherValidUntil(FiscalCountry.CH, line, today));
        Assert.Null(PosService.VoucherValidUntil(FiscalCountry.AT, line, today));
        Assert.Equal(new DateOnly(2027, 6, 30), PosService.VoucherValidUntil(FiscalCountry.DE, line with { ValidUntil = new(2027, 6, 30) }, today));
        Assert.Null(PosService.VoucherValidUntil(FiscalCountry.DE, line with { WithoutExpiry = true }, today));
        Assert.Throws<PosFailure>(() => PosService.VoucherValidUntil(FiscalCountry.DE, line with { ValidUntil = today }, today));
        Assert.Throws<PosFailure>(() =>
            PosService.VoucherValidUntil(FiscalCountry.DE, line with { ValidUntil = new(2030, 1, 1), WithoutExpiry = true }, today));
    }

    [Fact]
    public async Task Deutsche_Kasse_verkauft_Wertgutschein_bis_Jahresende_plus_drei_Jahre_und_druckt_das_Datum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.DE);

        var receipt = await SellAsync(spa, register, Voucher(VoucherKind.MultiPurpose, 50m, null));

        var expected = new DateOnly(ViennaToday.Year + 3, 12, 31);
        Assert.Equal(expected, Assert.Single(receipt.IssuedVouchers).ValidUntil);
        Assert.Contains($"Einzulösen bis {expected:dd.MM.yyyy}", ReceiptPrinter.Text(receipt, Vienna, "EUR", 42), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schweizer_Kasse_verkauft_Behandlungsgutschein_fuer_fuenf_Jahre()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.CH);

        var receipt = await SellAsync(spa, register, Voucher(VoucherKind.SinglePurpose, null, spa.MassageTreatment.Id));

        Assert.Equal(ViennaToday.AddYears(5), Assert.Single(receipt.IssuedVouchers).ValidUntil);
    }

    [Fact]
    public async Task Oesterreichische_Kasse_verkauft_ohne_Ablaufdatum_und_der_Beleg_nennt_keins()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);

        var receipt = await SellAsync(spa, register, Voucher(VoucherKind.MultiPurpose, 50m, null));

        Assert.Null(Assert.Single(receipt.IssuedVouchers).ValidUntil);
        Assert.DoesNotContain("Einzulösen", ReceiptPrinter.Text(receipt, Vienna, "EUR", 42), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eigenes_Datum_und_unbefristet_gelten_ein_Datum_von_heute_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.DE);
        var own = ViennaToday.AddYears(1);

        var dated = await SellAsync(spa, register, Voucher(VoucherKind.MultiPurpose, 30m, null) with { ValidUntil = own });
        var open = await SellAsync(spa, register, Voucher(VoucherKind.MultiPurpose, 30m, null) with { WithoutExpiry = true });
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        var today = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/lines",
            Voucher(VoucherKind.MultiPurpose, 30m, null) with { ValidUntil = ViennaToday }, TestSpa.Json, Ct);

        Assert.Equal(own, Assert.Single(dated.IssuedVouchers).ValidUntil);
        Assert.Null(Assert.Single(open.IssuedVouchers).ValidUntil);
        Assert.Equal(HttpStatusCode.BadRequest, today.StatusCode);
        Assert.Contains("Das Ablaufdatum muss in der Zukunft liegen.", await today.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abgelaufener_Gutschein_laesst_sich_nicht_einloesen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.DE);
        var code = Assert.Single((await SellAsync(spa, register, Voucher(VoucherKind.MultiPurpose, 50m, null))).IssuedVouchers).Code;
        var yesterday = ViennaToday.AddDays(-1);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            await db.Vouchers.Where(v => v.Code == code).ExecuteUpdateAsync(u => u.SetProperty(v => v.ValidUntil, yesterday), Ct);
        }

        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 20m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var refused = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Voucher, 20m, code, null)]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains($"ist am {yesterday:dd.MM.yyyy} abgelaufen", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kasse_liefert_Vorbelegung_Regel_und_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var german = await CreateRegisterAsync(spa, FiscalCountry.DE);
        var austrian = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var safe = new DateOnly(ViennaToday.Year + 3, 12, 31);

        var plain = await spa.GetAsync<VoucherExpiryDto>($"/api/pos/registers/{german.Id}/voucher-expiry", Ct);
        var shorter = await spa.GetAsync<VoucherExpiryDto>($"/api/pos/registers/{german.Id}/voucher-expiry?until={safe.AddDays(-1):yyyy-MM-dd}", Ct);
        var inAustria = await spa.GetAsync<VoucherExpiryDto>($"/api/pos/registers/{austrian.Id}/voucher-expiry?until={safe:yyyy-MM-dd}", Ct);

        Assert.Equal(safe, plain.Default);
        Assert.False(string.IsNullOrWhiteSpace(plain.Rule));
        Assert.Null(plain.Warning);
        Assert.Contains($"{safe:dd.MM.yyyy}", shorter.Warning, StringComparison.Ordinal);
        Assert.Null(inAustria.Default);
        Assert.Equal(AustrianWarning, inAustria.Warning);
    }

    private static BillLineInput Voucher(VoucherKind kind, decimal? value, Guid? treatmentId) =>
        new(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, kind, treatmentId);

    private static async Task<ReceiptDto> SellAsync(TestSpa spa, CashRegisterDto register, BillLineInput line)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", line, Ct);
        return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, bill.Open, null, null)]), Ct);
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa, FiscalCountry country) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(
            spa.Location.Id, $"V-{Guid.NewGuid():N}"[..14], country, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
}

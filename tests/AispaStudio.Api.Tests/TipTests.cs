using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Trinkgeld für das Team an der Kassa (ADR 0095): zusätzlich bezahlt, kein Umsatz, nicht im Umsatzzähler.</summary>
public sealed class TipTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    [Fact]
    public async Task Karte_mit_Trinkgeld_steht_eigens_auf_dem_Beleg_und_zaehlt_nicht_in_den_Umsatzzaehler()
    {
        // Given eine Rechnung über 92,00 an einer österreichischen Kasse
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var bill = await BillAsync(spa, register, 92m);

        // When der Gast 100,00 mit Karte zahlt, davon 8,00 Trinkgeld
        var receipt = await CheckoutAsync(spa, bill.Id, 8m, new PaymentInput(PaymentMethod.Card, 100m, null, "T-1"));

        // Then bleibt der Umsatz 92,00; das Trinkgeld steht daneben, die Zahlung nennt den ganzen Betrag
        Assert.Equal(92m, receipt.Total);
        Assert.Equal(8m, receipt.Tip);
        Assert.Equal(0m, receipt.Change);
        Assert.Equal(new PaymentDto(PaymentMethod.Card, 100m, null, "T-1"), Assert.Single(receipt.Payments));
        Assert.Equal(92m, Assert.Single(receipt.Taxes).Gross);
        Assert.Single(receipt.Lines);

        // And die Signatur kennt nur den Umsatz: Betrag-Satz-Normal 92,00, Betrag-Satz-Null 0,00, Umsatzzähler 92,00
        var fields = receipt.FiscalCode!.Split('_');
        Assert.Equal(["92,00", "0,00", "0,00", "0,00", "0,00"], fields[5..10]);
        Assert.Equal(9200, await DecryptTurnoverAsync(register.Id, fields[10], long.Parse(fields[3], CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(110, 10)]
    public async Task Bar_mit_Trinkgeld_bleibt_in_der_Lade_und_das_Rueckgeld_rechnet_es_ab(int given, int change)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var bill = await BillAsync(spa, register, 92m);

        var receipt = await CheckoutAsync(spa, bill.Id, 8m, new PaymentInput(PaymentMethod.Cash, given, null, null));

        Assert.Equal((92m, 8m, (decimal)change), (receipt.Total, receipt.Tip, receipt.Change));
        Assert.Equal(new PaymentDto(PaymentMethod.Cash, 100m, null, null), Assert.Single(receipt.Payments));
    }

    [Fact]
    public async Task Zu_viel_mit_Karte_ohne_Trinkgeld_und_ungueltiges_Trinkgeld_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var bill = await BillAsync(spa, register, 92m);

        var overpaid = await PostCheckoutAsync(spa, bill.Id, 0m, new PaymentInput(PaymentMethod.Card, 100m, null, null));
        var tooMuch = await PostCheckoutAsync(spa, bill.Id, 5m, new PaymentInput(PaymentMethod.Card, 100m, null, null));
        var negative = await PostCheckoutAsync(spa, bill.Id, -1m, new PaymentInput(PaymentMethod.Card, 91m, null, null));
        var fraction = await PostCheckoutAsync(spa, bill.Id, 1.005m, new PaymentInput(PaymentMethod.Card, 93.005m, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, overpaid.StatusCode);
        Assert.Contains("das Trinkgeld eintragen", await overpaid.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("Trinkgeld", await tooMuch.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fraction.StatusCode);
        Assert.Equal(BillStatus.Open, (await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Tagesabschluss_nennt_das_Trinkgeld_neben_dem_Umsatz_und_ein_Storno_nimmt_es_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var card = await CheckoutAsync(spa, (await BillAsync(spa, register, 92m)).Id, 8m, new PaymentInput(PaymentMethod.Card, 100m, null, null));
        await CheckoutAsync(spa, (await BillAsync(spa, register, 40m)).Id, 5m, new PaymentInput(PaymentMethod.Cash, 50m, null, null));

        var report = await spa.GetAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-report", Ct);

        // Umsatz ohne Trinkgeld; die Zahlarten enthalten es; bares Trinkgeld liegt bis zur Entnahme in der Lade.
        Assert.Equal(132m, report.Revenue);
        Assert.Equal(13m, report.Tips);
        Assert.Equal(100m, report.Payments.Single(p => p.Method == PaymentMethod.Card).Amount);
        Assert.Equal(45m, report.Payments.Single(p => p.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(45m, report.ExpectedCash);

        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{card.Id}/void", new VoidRequest("Falscher Betrag"), Ct);
        var after = await spa.GetAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-report", Ct);

        Assert.Equal((-92m, -8m), (voided.Total, voided.Tip));
        Assert.Equal(-100m, Assert.Single(voided.Payments).Amount);
        Assert.Equal((40m, 5m), (after.Revenue, after.Tips));
        // Der Storno trägt „STO“ statt des Zählers im Code; der Zähler der Kasse steht wieder beim Umsatz des zweiten Belegs.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var counter = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().CashRegisters.AsNoTracking()
            .Where(r => r.Id == register.Id).Select(r => r.TurnoverCounter).FirstAsync(Ct);
        Assert.Equal(4000, counter);
    }

    [Fact]
    public async Task Deutsche_Kasse_signiert_das_Trinkgeld_im_Feld_fuer_null_Prozent_und_meldet_TrinkgeldAN()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.DE);
        var receipt = await CheckoutAsync(spa, (await BillAsync(spa, register, 92m)).Id, 8m, new PaymentInput(PaymentMethod.Card, 100m, null, null));
        await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0, null), Ct);

        // Vorgangsdaten: Brutto je Steuersatz (19, 7, 10,7, 5,5, 0 %) und Zahlung gehen auf.
        Assert.Equal("Beleg^92.00_0.00_0.00_0.00_8.00^100.00:Unbar", receipt.FiscalCode!.Split(';')[3]);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await spa.Client.GetAsync($"/api/pos/registers/{register.Id}/dsfinvk?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(Ct));
        string Read(string name)
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        Assert.Contains("\"Trinkgeld\"", Read("lines.csv"), StringComparison.Ordinal);
        Assert.Contains("\"TrinkgeldAN\"", Read("lines.csv"), StringComparison.Ordinal);
        Assert.Contains("\"TrinkgeldAN\";\"\";0;5;8.00;8.00;0.00", Read("businesscases.csv"), StringComparison.Ordinal);
        Assert.Contains("\"Umsatz\";\"\";0;1;92.00", Read("businesscases.csv"), StringComparison.Ordinal);
        Assert.Contains(";5;8.00;8.00;0.00", Read("transactions_vat.csv"), StringComparison.Ordinal);
        Assert.Contains("\"Unbar\";\"Karte\";\"EUR\";100.00;100.00", Read("datapayment.csv"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Buchhaltung_bucht_das_Trinkgeld_als_Verbindlichkeit_und_gleicht_den_Geldtransit_aus()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa, FiscalCountry.AT);
        var receipt = await CheckoutAsync(spa, (await BillAsync(spa, register, 92m)).Id, 8m, new PaymentInput(PaymentMethod.Card, 100m, null, null));

        var postings = await spa.GetAsync<List<AccountingPosting>>(
            $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}", Ct);

        var sale = postings.Where(p => p.Document == $"{register.Code}-{receipt.Number}").ToList();
        Assert.Contains(sale, p => p is { DebitAccount: "1360", CreditAccount: "4100", Amount: 92m, TaxPercent: 20m });
        Assert.Contains(sale, p => p is { DebitAccount: "1360", CreditAccount: "3790", Amount: 8m, TaxPercent: 0m } && p.Text.StartsWith("Trinkgeld", StringComparison.Ordinal));
        Assert.Contains(sale, p => p is { DebitAccount: "1361", CreditAccount: "1360", Amount: 100m });
        var transit = postings.Where(p => p.DebitAccount == "1360").Sum(p => p.Amount) - postings.Where(p => p.CreditAccount == "1360").Sum(p => p.Amount);
        Assert.Equal(0m, transit);
        Assert.Equal("3790", (await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings", Ct)).TipsAccount);
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa, FiscalCountry country) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], country, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", null, null), Ct);

    private static async Task<BillDto> BillAsync(TestSpa spa, CashRegisterDto register, decimal amount)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        return await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, amount, 0, "Massage", TaxCategory.Normal, null, null, null), Ct);
    }

    private static Task<HttpResponseMessage> PostCheckoutAsync(TestSpa spa, Guid billId, decimal tip, params PaymentInput[] payments) =>
        spa.Client.PostAsJsonAsync($"/api/pos/bills/{billId}/checkout", new CheckoutRequest(null, payments, tip), TestSpa.Json, Ct);

    private static async Task<ReceiptDto> CheckoutAsync(TestSpa spa, Guid billId, decimal tip, params PaymentInput[] payments)
    {
        var response = await PostCheckoutAsync(spa, billId, tip, payments);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ReceiptDto>(TestSpa.Json, Ct))!;
    }

    private async Task<long> DecryptTurnoverAsync(Guid registerId, string encrypted, long number)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var register = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().CashRegisters.AsNoTracking().FirstAsync(r => r.Id == registerId, Ct);
        return RksvCode.DecryptTurnover(encrypted, register.Code, number, Convert.FromBase64String(register.TurnoverKey!));
    }
}

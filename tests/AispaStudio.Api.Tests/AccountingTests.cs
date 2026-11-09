using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class AccountingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    [Fact]
    public async Task Buchungen_gleichen_das_Verrechnungskonto_aus_und_trennen_Erloeskonten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var shop = await ShopAccountAsync(spa);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Bademantel", 60m, TaxCategory.Normal, shop.Id), Ct);
        await spa.PostAsync<CashMovementDto>($"/api/pos/registers/{register.Id}/cash-movements",
            new CashMovementInput(CashMovementKind.Deposit, 150m, "Wechselgeld"), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Article, null, article.Id, 1, null, 0, null, null, null, null, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null,
            [new PaymentInput(PaymentMethod.Card, 100m, null, "T-1"), new PaymentInput(PaymentMethod.Cash, 50m, null, null)]), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsche Kundin"), Ct);

        var postings = await spa.GetAsync<List<AccountingPosting>>(
            $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}", Ct);

        var transit = postings.Where(p => p.DebitAccount == "1360").Sum(p => p.Amount) - postings.Where(p => p.CreditAccount == "1360").Sum(p => p.Amount);
        Assert.Equal(0m, transit);
        var sale = postings.Where(p => p.Document == $"{register.Code}-2").ToList();
        Assert.Contains(sale, p => p is { DebitAccount: "1360", CreditAccount: "4000", Amount: 79m, TaxPercent: 20m, CostCenter: null });
        Assert.Contains(sale, p => p.CreditAccount == shop.Code && p is { Amount: 60m, CostCenter: "KST-SHOP" });
        Assert.Contains(sale, p => p is { DebitAccount: "1361", Amount: 100m });
        Assert.Contains(sale, p => p is { DebitAccount: "1000", Amount: 39m });
        var voided = postings.Where(p => p.Document == $"{register.Code}-3").ToList();
        Assert.Equal(4, voided.Count);
        Assert.All(voided, p => Assert.True(p.Amount < 0));
        Assert.All(voided, p => Assert.StartsWith("Storno", p.Text, StringComparison.Ordinal));
        Assert.Contains(postings, p => p is { DebitAccount: "1000", CreditAccount: "1460", Amount: 150m });
    }

    [Fact]
    public async Task Kassendifferenz_aus_dem_Tagesabschluss_wird_gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 24m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 24m, null, null)]), Ct);

        var closing = await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(21.50m, null), Ct);
        var postings = await spa.GetAsync<List<AccountingPosting>>(
            $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}", Ct);

        Assert.Equal(-2.50m, closing.Difference);
        var shortage = Assert.Single(postings, p => p.Text.StartsWith("Kassenfehlbetrag", StringComparison.Ordinal));
        Assert.Equal(("4900", "1000", 2.50m), (shortage.DebitAccount, shortage.CreditAccount, shortage.Amount));
    }

    [Fact]
    public async Task Datev_und_Bmd_Export_sind_Windows_1252_mit_Kostenstelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var shop = await ShopAccountAsync(spa);
        var austria = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings?country=AT", Ct);
        await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings?country=AT", austria with { DatevTaxKeys = "20=7;10=8;13=9" }, Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Öl für Rücken", 24m, TaxCategory.Normal, shop.Id), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Article, null, article.Id, 1, null, 0, null, null, null, null, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 24m, null, null)]), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Retour"), Ct);
        var query = $"from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(1252);

        var datevResponse = await spa.Client.GetAsync($"/api/accounting/export?format=Datev&{query}", Ct);
        var datev = ansi.GetString(await datevResponse.Content.ReadAsByteArrayAsync(Ct)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var bmd = ansi.GetString(await spa.Client.GetByteArrayAsync($"/api/accounting/export?format=Bmd&{query}", Ct))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(HttpStatusCode.OK, datevResponse.StatusCode);
        Assert.StartsWith("EXTF_Buchungsstapel_", datevResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        Assert.StartsWith("\"EXTF\";700;21;\"Buchungsstapel\"", datev[0], StringComparison.Ordinal);
        Assert.StartsWith("Umsatz (ohne Soll/Haben-Kz);Soll/Haben-Kennzeichen", datev[1], StringComparison.Ordinal);
        Assert.Equal(4, datev.Length - 2);
        var revenue = datev[2].Split(';');
        Assert.Equal(("24,00", "\"S\"", "1360", shop.Code, "\"7\"", "\"KST-SHOP\""), (revenue[0], revenue[1], revenue[6], revenue[7], revenue[8], revenue[36]));
        Assert.Equal($"{ViennaToday:ddMM}", revenue[9]);
        Assert.Contains(datev[4..], line => line.StartsWith("24,00;\"H\";", StringComparison.Ordinal));
        Assert.Equal("satzart;konto;gkonto;belegnr;belegdat;buchsymbol;buchcode;prozent;steuercode;betrag;steuer;text;kost", bmd[0]);
        var bmdRevenue = bmd[1].Split(';');
        Assert.Equal(("1360", shop.Code, "20,00", "1", "24,00", "4,00", "KST-SHOP"),
            (bmdRevenue[1], bmdRevenue[2], bmdRevenue[7], bmdRevenue[8], bmdRevenue[9], bmdRevenue[10], bmdRevenue[12]));
        Assert.Contains(bmd, line => line.Contains(";-24,00;-4,00;Storno", StringComparison.Ordinal));

        var rzlResponse = await spa.Client.GetAsync($"/api/accounting/export?format=Rzl&{query}", Ct);
        var rzl = ansi.GetString(await rzlResponse.Content.ReadAsByteArrayAsync(Ct)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("RZL_Buchungen_", rzlResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        Assert.Equal("Belegdatum;Belegnummer;Konto;Gegenkonto;Betrag;S/H;Steuerprozent;Steuerbetrag;Buchungstext;Kostenstelle", rzl[0]);
        var rzlRevenue = rzl[1].Split(';');
        Assert.Equal(($"{ViennaToday:dd.MM.yyyy}", "1360", shop.Code, "24,00", "S", "20,00", "4,00", "KST-SHOP"),
            (rzlRevenue[0], rzlRevenue[2], rzlRevenue[3], rzlRevenue[4], rzlRevenue[5], rzlRevenue[6], rzlRevenue[7], rzlRevenue[9]));
        Assert.Contains(rzl, line => line.Contains(";24,00;H;20,00;4,00;Storno", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Schweizer_Kassen_gehen_als_CSV_in_CHF_an_Abacus_und_bexio()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var euro = await CreateRegisterAsync(spa);
        var swiss = await CreateRegisterAsync(spa, FiscalCountry.CH);
        var shop = await ShopAccountAsync(spa);
        var defaults = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings", Ct);
        await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings", defaults with { SwissTaxCodes = "8.1=UN81;2.6=UR26", DatevTaxKeys = "19=3;7=2;20=" }, Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Öl; für Rücken", 54m, TaxCategory.Normal, shop.Id), Ct);
        foreach (var register in new[] { euro, swiss })
        {
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
            await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Article, null, article.Id, 1, null, 0, null, null, null, null, null), Ct);
            var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
                new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 54m, null, null)]), Ct);
            if (register == swiss)
            {
                await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Retour"), Ct);
            }
        }

        var period = $"from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}";
        var response = await spa.Client.GetAsync($"/api/accounting/export?format=Swiss&{period}", Ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        var lines = Encoding.UTF8.GetString(bytes.AsSpan(3)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var datev = Encoding.Latin1.GetString(await spa.Client.GetByteArrayAsync($"/api/accounting/export?format=Datev&{period}", Ct));
        var preview = await spa.GetAsync<List<AccountingPosting>>($"/api/accounting/postings?{period}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
        Assert.Equal("Datum;Beleg;Soll;Haben;Betrag;Währung;MWST-Code;MWST-Satz;MWST-Betrag;Text;Kostenstelle", lines[0]);
        Assert.Equal(4, lines.Length - 1);
        Assert.All(lines[1..], l => Assert.StartsWith($"{ViennaToday:dd.MM.yyyy};{swiss.Code}-", l, StringComparison.Ordinal));
        var revenue = lines[1].Split(';');
        Assert.Equal((defaults.TransitAccount, shop.Code, "54.00", "CHF", "UN81", "8.1", "4.05", "KST-SHOP"),
            (revenue[2], revenue[3], revenue[4], revenue[5], revenue[6], revenue[7], revenue[8], revenue[10]));
        var reversal = lines[3].Split(';');
        Assert.Equal((shop.Code, defaults.TransitAccount, "54.00"), (reversal[2], reversal[3], reversal[4]));
        Assert.StartsWith("Storno", reversal[9], StringComparison.Ordinal);
        Assert.DoesNotContain($"{swiss.Code}-", datev, StringComparison.Ordinal);
        Assert.Contains($"{euro.Code}-", datev, StringComparison.Ordinal);
        Assert.Contains(preview, p => p.Currency == "CHF" && p.Document.StartsWith(swiss.Code, StringComparison.Ordinal));
        Assert.Contains(preview, p => p.Currency == "EUR" && p.Document.StartsWith(euro.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Kassen_eines_Landes_buchen_mit_dessen_eigener_Kontenzuordnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var euro = await CreateRegisterAsync(spa);
        var swiss = await CreateRegisterAsync(spa, FiscalCountry.CH);
        var shop = await ShopAccountAsync(spa);
        var defaults = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings", Ct);
        var inherited = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings?country=CH", Ct);
        var invalid = await spa.Client.PutAsJsonAsync("/api/accounting/settings?country=CH",
            inherited with { RevenueAccountCodes = "4000=Erlös" }, TestSpa.Json, Ct);
        var own = await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings?country=CH",
            inherited with { TransitAccount = "1099", CardAccount = "1091", RevenueAccountCodes = $"{shop.Code}=3200" }, Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Peeling", 54m, TaxCategory.Normal, shop.Id), Ct);
        foreach (var register in new[] { euro, swiss })
        {
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
            await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Article, null, article.Id, 1, null, 0, null, null, null, null, null), Ct);
            await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
                new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, 54m, null, null)]), Ct);
        }

        var period = $"from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}";
        var postings = await spa.GetAsync<List<AccountingPosting>>($"/api/accounting/postings?{period}", Ct);
        var germany = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings?country=DE", Ct);
        var reset = await spa.Client.DeleteAsync("/api/accounting/settings?country=CH", Ct);
        var afterReset = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings?country=CH", Ct);

        Assert.True(inherited.Inherited);
        Assert.Equal(defaults.TransitAccount, inherited.TransitAccount);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("revenueAccountCodes", await invalid.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
        Assert.Equal((FiscalCountry.CH, false), (own.Country, own.Inherited));
        var swissPostings = postings.Where(p => p.Document.StartsWith($"{swiss.Code}-", StringComparison.Ordinal)).ToList();
        var euroPostings = postings.Where(p => p.Document.StartsWith($"{euro.Code}-", StringComparison.Ordinal)).ToList();
        Assert.Contains(swissPostings, p => p is { DebitAccount: "1099", CreditAccount: "3200", Amount: 54m, CostCenter: "KST-SHOP" });
        Assert.Contains(swissPostings, p => p is { DebitAccount: "1091", CreditAccount: "1099", Amount: 54m });
        Assert.Contains(euroPostings, p => p.DebitAccount == defaults.TransitAccount && p.CreditAccount == shop.Code);
        Assert.Contains(euroPostings, p => p.DebitAccount == defaults.CardAccount && p.CreditAccount == defaults.TransitAccount);
        Assert.True(germany.Inherited);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(afterReset.Inherited);
    }

    [Fact]
    public async Task Datev_nimmt_den_BU_Schluessel_aus_dem_Land_der_Kassa_und_verweigert_fehlende()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var austrian = await CreateRegisterAsync(spa);
        var german = await CreateRegisterAsync(spa, FiscalCountry.DE);
        foreach (var (register, price) in new[] { (austrian, 24m), (german, 23.80m) })
        {
            var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
            await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
                new BillLineInput(BillLineKind.Custom, null, null, 1, price, 0, "Öl", TaxCategory.Normal, null, null, null), Ct);
            await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
                new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, price, null, null)]), Ct);
        }

        var period = $"from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}";
        var preview = await spa.GetAsync<List<AccountingPosting>>($"/api/accounting/postings?{period}", Ct);
        var refused = await spa.Client.GetAsync($"/api/accounting/export?format=Datev&{period}", Ct);
        var bmd = await spa.Client.GetAsync($"/api/accounting/export?format=Bmd&{period}", Ct);
        var austria = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings?country=AT", Ct);
        await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings?country=AT", austria with { DatevTaxKeys = "20=" }, Ct);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var datev = Encoding.GetEncoding(1252).GetString(await spa.Client.GetByteArrayAsync($"/api/accounting/export?format=Datev&{period}", Ct))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(preview, p => p is { TaxPercent: 20m, TaxKey: null, Country: FiscalCountry.AT });
        Assert.Contains(preview, p => p is { TaxPercent: 19m, TaxKey: "3", Country: FiscalCountry.DE });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadAsStringAsync(Ct);
        Assert.Contains("20 % (Kassen in Österreich)", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("19 %", problem, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, bmd.StatusCode);
        var revenue = datev[2..].Select(l => l.Split(';')).Where(f => f[0] is "24,00" or "23,80" && f[1] == "\"S\"" && f[6] == "1360").ToList();
        Assert.Contains(revenue, f => f[0] == "24,00" && f[8] == "\"\"");
        Assert.Contains(revenue, f => f[0] == "23,80" && f[8] == "\"3\"");
    }

    [Fact]
    public async Task Einstellungen_werden_geprueft_und_gespeichert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var defaults = await spa.GetAsync<AccountingSettingsDto>("/api/accounting/settings", Ct);

        var invalid = await spa.Client.PutAsJsonAsync("/api/accounting/settings",
            defaults with { DatevConsultantNumber = "12AB", CashAccount = "", DatevTaxKeys = "neunzehn", SwissTaxCodes = "8.1" }, TestSpa.Json, Ct);
        var saved = await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings",
            defaults with { DatevConsultantNumber = "1234567", DatevClientNumber = "99999" }, Ct);
        await spa.PutAsync<AccountingSettingsDto>("/api/accounting/settings", defaults, Ct);
        var tooLong = await spa.Client.GetAsync($"/api/accounting/postings?from=2026-01-01&until=2027-06-01", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadAsStringAsync(Ct);
        Assert.Contains("datevConsultantNumber", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cashAccount", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("datevTaxKeys", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("swissTaxCodes", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("1234567", "99999"), (saved.DatevConsultantNumber, saved.DatevClientNumber));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Standortleitung_sieht_nur_eigene_Kassen_und_Rezeption_gar_keine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 10m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 10m, null, null)]), Ct);
        var other = await spa.CreateLocationAsync("Anderer Standort", [DayOfWeek.Monday], Ct);
        using var ownManager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var otherManager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [other.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var query = $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&registerId={register.Id}";

        // Die Buchhaltung braucht das einzeln vergebene Recht „Berichte mit Beträgen“ (ADR 0070).
        var withoutRight = await ownManager.GetAsync(query, Ct);
        foreach (var (client, location) in new[] { (ownManager, spa.Location.Id), (otherManager, other.Id) })
        {
            var id = (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;
            (await spa.Client.PutAsJsonAsync($"/api/staff/{id}",
                new StaffAccessInput(StaffRole.Manager, false, [location], null, true, AmountReports: true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        var own = await ownManager.GetFromJsonAsync<List<AccountingPosting>>(query, TestSpa.Json, Ct);
        var foreignRegister = await otherManager.GetAsync(query, Ct);
        var foreignAll = await otherManager.GetFromJsonAsync<List<AccountingPosting>>(
            $"/api/accounting/postings?from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}", TestSpa.Json, Ct);
        var receptionResponse = await reception.GetAsync(query, Ct);
        var settings = await ownManager.GetFromJsonAsync<AccountingSettingsDto>("/api/accounting/settings", TestSpa.Json, Ct);
        var managerSaves = await ownManager.PutAsJsonAsync("/api/accounting/settings", settings, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, withoutRight.StatusCode);
        Assert.Equal(2, own!.Count);
        Assert.Equal(HttpStatusCode.Forbidden, foreignRegister.StatusCode);
        Assert.DoesNotContain(foreignAll!, p => p.Document.StartsWith(register.Code, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, receptionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerSaves.StatusCode);
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa, FiscalCountry country = FiscalCountry.AT) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"B-{Guid.NewGuid():N}"[..14],
            country, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<RevenueAccountDto> ShopAccountAsync(TestSpa spa) =>
        await spa.PostAsync<RevenueAccountDto>("/api/pos/revenue-accounts",
            new RevenueAccountInput($"48{Random.Shared.Next(10000, 99999)}", "Erlöse Shop", "KST-SHOP"), Ct);
}

using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Deutschland: Belege mit Test-TSE nach KassenSichV, QR-Code nach DSFinV-K und Export für die Kassen-Nachschau.</summary>
public sealed class KassenSichVTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deutscher_Beleg_traegt_TSE_Signatur_und_Zahlungen_im_QR_Code()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateGermanRegisterAsync(spa);
        Assert.Contains(register.Warnings, w => w.Contains("Test-TSE", StringComparison.Ordinal));

        var receipt = await SellAsync(spa, register, 30m, new PaymentInput(PaymentMethod.Cash, 20m, null, null), new PaymentInput(PaymentMethod.Card, 10m, null, null));

        var qr = receipt.FiscalCode!.Split(';');
        Assert.Equal(12, qr.Length);
        Assert.Equal(["V0", register.Code, "Kassenbeleg-V1", "Beleg^30.00_0.00_0.00_0.00_0.00^20.00:Bar_10.00:Unbar"], qr[..4]);
        Assert.Equal(["ecdsa-plain-SHA256", "utcTime"], qr[8..10]);

        // Die Signatur lässt sich mit dem öffentlichen Schlüssel aus dem QR-Code prüfen.
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(qr[11]), out _);
        var signed = TestTseClient.SignedData(long.Parse(qr[4], CultureInfo.InvariantCulture), long.Parse(qr[5], CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(qr[6], CultureInfo.InvariantCulture), DateTimeOffset.Parse(qr[7], CultureInfo.InvariantCulture), qr[2], qr[3]);
        Assert.True(key.VerifyData(signed, Convert.FromBase64String(qr[10]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var second = await SellAsync(spa, register, 5m, new PaymentInput(PaymentMethod.Cash, 5m, null, null));
        var next = second.FiscalCode!.Split(';');
        Assert.Equal(long.Parse(qr[4], CultureInfo.InvariantCulture) + 1, long.Parse(next[4], CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task DSFinV_K_je_Tagesabschluss_als_ZIP()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateGermanRegisterAsync(spa);
        var sale = await SellAsync(spa, register, 30m, new PaymentInput(PaymentMethod.Cash, 30m, null, null));
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{sale.Id}/void", new VoidRequest("Falsch gebucht"), Ct);
        await SellAsync(spa, register, 12.5m, new PaymentInput(PaymentMethod.Card, 12.5m, null, null));
        await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0, null), Ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await spa.Client.GetAsync($"/api/pos/registers/{register.Id}/dsfinvk?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(Ct));
        string Read(string name)
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        Assert.Contains("index.xml", zip.Entries.Select(e => e.Name));
        var transactions = Read("transactions.csv").Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Z_KASSE_ID;Z_ERSTELLUNG;Z_NR;BON_ID", transactions[0], StringComparison.Ordinal);
        Assert.Equal(4, transactions.Length);
        Assert.Contains(transactions, t => t.Contains(";1;", StringComparison.Ordinal) && t.Contains("-30.00", StringComparison.Ordinal));
        Assert.Contains("\"Unbar\";\"Karte\";\"EUR\";12.50;12.50", Read("datapayment.csv"), StringComparison.Ordinal);
        Assert.Contains("\"ecdsa-plain-SHA256\";\"utcTime\"", Read("tse.csv"), StringComparison.Ordinal);
        Assert.Contains(";1;12.50;10.50;2.00", Read("transactions_vat.csv"), StringComparison.Ordinal);
        Assert.Contains("\"Umsatz\";\"\";0;1;12.50;10.50;2.00", Read("businesscases.csv"), StringComparison.Ordinal);
        Assert.Equal(3, Read("transactions_tse.csv").Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length - 1);

        var austrian = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", Input(spa, FiscalCountry.AT), Ct);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await spa.Client.GetAsync($"/api/pos/registers/{austrian.Id}/dsfinvk?from={today:yyyy-MM-dd}&until={today:yyyy-MM-dd}", Ct)).StatusCode);
    }

    [Fact]
    public async Task TSE_Export_als_TAR_ueber_die_Queue()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateGermanRegisterAsync(spa);
        await SellAsync(spa, register, 30m, new PaymentInput(PaymentMethod.Cash, 30m, null, null));
        await SellAsync(spa, register, 12.5m, new PaymentInput(PaymentMethod.Card, 12.5m, null, null));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requested = await spa.PostAsync<TseExportDto>($"/api/pos/registers/{register.Id}/tse-exports",
            new TseExportInput(today.AddDays(-1), today.AddDays(1)), Ct);
        Assert.Equal(TseExportStatus.Pending, requested.Status);

        TseExportDto export;
        for (var i = 0; ; i++)
        {
            export = (await spa.GetAsync<List<TseExportDto>>($"/api/pos/registers/{register.Id}/tse-exports", Ct)).Single();
            if (export.Status != TseExportStatus.Pending || i == 100)
            {
                break;
            }

            await Task.Delay(100, Ct);
        }

        Assert.Equal(TseExportStatus.Done, export.Status);
        var response = await spa.Client.GetAsync($"/api/pos/tse-exports/{export.Id}/file", Ct);
        Assert.Equal("application/x-tar", response.Content.Headers.ContentType?.MediaType);
        var files = new Dictionary<string, string>();
        await using (var tar = new TarReader(await response.Content.ReadAsStreamAsync(Ct)))
        {
            while (await tar.GetNextEntryAsync(cancellationToken: Ct) is { } entry)
            {
                using var reader = new StreamReader(entry.DataStream!, Encoding.UTF8);
                files[entry.Name] = await reader.ReadToEndAsync(Ct);
            }
        }

        Assert.Contains($"Client-ID;{register.Code}", files["info.csv"], StringComparison.Ordinal);
        Assert.Single(files, f => f.Key.EndsWith("_PublicKey.pem", StringComparison.Ordinal) && f.Value.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal));
        var logs = files.Where(f => f.Key.StartsWith("Unixt_", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, logs.Count); // Startbeleg und zwei Verkäufe
        Assert.All(logs, l => Assert.Matches($@"^Unixt_\d+_Sig-\d+_Log-Tra_No-\d+_Finish_Client-{register.Code}\.log$", l.Key));
        Assert.Contains(logs, l => l.Value.Contains("Kassenbeleg-V1;Beleg^12.50_0.00_0.00_0.00_0.00^12.50:Unbar", StringComparison.Ordinal));

        var austrian = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", Input(spa, FiscalCountry.AT), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PostAsJsonAsync($"/api/pos/registers/{austrian.Id}/tse-exports",
            new TseExportInput(today, today), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ohne_TSE_laesst_sich_keine_deutsche_Kasse_anlegen()
    {
        var register = new CashRegister { Code = "D-1", ReceiptHeader = "Test-Spa GmbH" };

        var error = await new KassenSichVFiscalization([]).InitializeAsync(register, Ct);

        Assert.Contains("keine TSE angebunden", error, StringComparison.Ordinal);
        Assert.Null(register.CertificateSerial);
    }

    private static async Task<CashRegisterDto> CreateGermanRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", Input(spa, FiscalCountry.DE), Ct);

    private static CashRegisterInput Input(TestSpa spa, FiscalCountry country) =>
        new(spa.Location.Id, $"D-{Guid.NewGuid():N}"[..14], country, "Test-Spa GmbH\nTeststraße 1, 10115 Berlin", "Test-Spa GmbH", null, null);

    private static async Task<ReceiptDto> SellAsync(TestSpa spa, CashRegisterDto register, decimal amount, params PaymentInput[] payments)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, amount, 0, "Massage", TaxCategory.Normal, null, null, null), Ct);
        var response = await spa.Client.PostAsJsonAsync($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, payments), TestSpa.Json, Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ReceiptDto>(TestSpa.Json, Ct))!;
    }
}

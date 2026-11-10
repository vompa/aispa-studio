using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gutscheinliste als PDF (PDF-Ausgaben, Zusatz): alle Gutscheine eines Standorts mit Code, Art, Ausstellung, Ausgangs- und Restwert, Gültigkeit, Status, Käufer, Empfänger, Verkaufskanal und
/// Versandvermerk, Filter (Zeitraum, Status, „läuft in 30 Tagen ab“, „noch nicht versendet“) und der Summe der offenen Restwerte. Recht: Kassenaufsicht oder Berichte mit Beträgen,
/// fremder Mandant 404. Der Versandweg (Mail, Druck, Übergabe) steht in <see cref="SentDocument"/>.
/// </summary>
public sealed partial class VoucherListPdfTests(ApiFixture fixture)
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Url(Guid locationId, string query = "") => $"/api/pos/locations/{locationId}/vouchers/pdf{query}";

    [GeneratedRegex("^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$")]
    private static partial Regex CodePattern();

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    /// <summary>Die Zeilen der Tabelle: je Code der Text der Zeile, die Zellen durch „ | “ getrennt (jede Zeile des PDFs ein Stück).</summary>
    private static Dictionary<string, string> Rows(byte[] pdf)
    {
        var rows = new Dictionary<string, string>();
        string? code = null;
        foreach (var line in PdfText.Text(pdf).Split('\n'))
        {
            if (CodePattern().IsMatch(line))
            {
                code = line;
                rows[code] = line;
            }
            else if (line.StartsWith("Offene Restwerte", StringComparison.Ordinal) || line.StartsWith("Erstellt am", StringComparison.Ordinal) || line.StartsWith("Seite ", StringComparison.Ordinal))
            {
                code = null;
            }
            else if (code is not null)
            {
                rows[code] += $" | {line}";
            }
        }

        return rows;
    }

    /// <summary>Die Zeile ohne die Zellentrenner: lange Wörter brechen in der Zelle um, so steht der Text wieder zusammen.</summary>
    private static string Flat(string row) => row.Replace(" | ", "", StringComparison.Ordinal);

    private static async Task<CashRegisterDto> RegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"G-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<(ReceiptDto Receipt, VoucherDto Voucher)> SellAsync(
        TestSpa spa, CashRegisterDto register, VoucherKind kind = VoucherKind.MultiPurpose, decimal? value = 50m, int? units = null, bool withBuyer = true)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, withBuyer ? spa.Customer.Id : null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, kind, kind == VoucherKind.SinglePurpose ? spa.MassageTreatment.Id : null, Units: units), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, bill.Open, null, null)]), Ct);
        return (receipt, Assert.Single(receipt.IssuedVouchers));
    }

    private static async Task ChangeAsync(TestSpa spa, Guid voucherId, Func<AispaDbContext, IQueryable<Voucher>, Task> change)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        await change(db, db.Vouchers.Where(v => v.Id == voucherId));
    }

    private static Task ValidUntilAsync(TestSpa spa, Guid voucherId, DateOnly? until) =>
        ChangeAsync(spa, voucherId, (_, v) => v.ExecuteUpdateAsync(s => s.SetProperty(x => x.ValidUntil, until), Ct));

    private static Task IssuedAtAsync(TestSpa spa, Guid voucherId, DateTimeOffset at) =>
        ChangeAsync(spa, voucherId, (_, v) => v.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, at), Ct));

    private static Task RedeemAsync(TestSpa spa, Guid voucherId) =>
        ChangeAsync(spa, voucherId, (_, v) => v.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, VoucherStatus.Redeemed).SetProperty(x => x.Balance, 0m), Ct));

    private static async Task<List<SentDocument>> SentAsync(TestSpa spa, Guid voucherId)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().SentDocuments.AsNoTracking()
            .Where(d => d.Kind == SentDocument.VoucherKind && d.TargetId == voucherId).OrderBy(d => d.SentAt).ToListAsync(Ct);
    }

    /// <summary>
    /// Fünf Gutscheine: A aktiv (50 €, ohne Befristung), B Behandlung Massage aktiv (79 €), C aufgebraucht, D abgelaufen (30 €, gestern), E storniert (Beleg storniert).
    /// </summary>
    private async Task<(TestSpa Spa, CashRegisterDto Register, Dictionary<string, VoucherDto> Vouchers)> FiveAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var vouchers = new Dictionary<string, VoucherDto>();
        vouchers["A"] = (await SellAsync(spa, register)).Voucher;
        vouchers["B"] = (await SellAsync(spa, register, VoucherKind.SinglePurpose, null)).Voucher;
        vouchers["C"] = (await SellAsync(spa, register, value: 20m)).Voucher;
        vouchers["D"] = (await SellAsync(spa, register, value: 30m)).Voucher;
        var (voided, e) = await SellAsync(spa, register, value: 40m);
        vouchers["E"] = e;
        await RedeemAsync(spa, vouchers["C"].Id);
        await ValidUntilAsync(spa, vouchers["D"].Id, Today.AddDays(-1));
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{voided.Id}/void", new VoidRequest("Irrtum"), Ct);
        return (spa, register, vouchers);
    }

    [Fact]
    public async Task Die_Liste_nennt_je_Gutschein_Code_Art_Werte_Status_Kaeufer_Kanal_und_die_Summe_der_offenen_Restwerte()
    {
        var (spa, _, vouchers) = await FiveAsync();

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Gutscheinliste-Wien-", response.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        PdfText.Keep(pdf, "gutscheinliste");
        using (var document = PdfText.Open(pdf))
        {
            Assert.True(document.Pages[0].Width.Point > document.Pages[0].Height.Point, "A4 quer");
        }

        var text = PdfText.Text(pdf);
        Assert.Contains("Gutscheinliste", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "Code", "Art", "Ausgestellt", "Ausgangswert", "Restwert", "Gültig bis", "Status", "Käufer", "Empfänger", "Kanal", "Versand", "EUR" })
        {
            Assert.Contains(heading, text.Split('\n'));
        }

        var rows = Rows(pdf);
        Assert.Equal(5, rows.Count);
        var a = rows[vouchers["A"].Code];
        Assert.Contains("| Wert |", a, StringComparison.Ordinal);
        Assert.Contains($"| {Format(Today)} |", a, StringComparison.Ordinal);
        Assert.Contains("| 50,00 | 50,00 |", a, StringComparison.Ordinal);
        Assert.Contains("| aktiv |", a, StringComparison.Ordinal);
        Assert.Contains(spa.Customer.LastName, Flat(a), StringComparison.Ordinal);
        Assert.Contains("| Kassa |", a, StringComparison.Ordinal);
        Assert.Contains("| – |", a, StringComparison.Ordinal);
        Assert.Contains("| Behandlung | Massage |", rows[vouchers["B"].Code], StringComparison.Ordinal);
        Assert.Contains("| 79,00 | 79,00 |", rows[vouchers["B"].Code], StringComparison.Ordinal);
        Assert.Contains("| aufgebraucht |", rows[vouchers["C"].Code], StringComparison.Ordinal);
        Assert.Contains("| 20,00 | 0,00 |", rows[vouchers["C"].Code], StringComparison.Ordinal);
        Assert.Contains("| abgelaufen |", rows[vouchers["D"].Code], StringComparison.Ordinal);
        Assert.Contains(Format(Today.AddDays(-1)), rows[vouchers["D"].Code], StringComparison.Ordinal);
        Assert.Contains("| storniert |", rows[vouchers["E"].Code], StringComparison.Ordinal);

        // Offen sind nur die aktiven Gutscheine: 50 + 79; der abgelaufene steht gesondert, aufgebrauchte und stornierte zählen nicht.
        Assert.Contains("Offene Restwerte (Verbindlichkeit)", text, StringComparison.Ordinal);
        Assert.Contains("EUR 129,00 aus 2 aktiven Gutscheinen", text, StringComparison.Ordinal);
        Assert.Contains("Abgelaufen, noch nicht ausgebucht", text, StringComparison.Ordinal);
        Assert.Contains("EUR 30,00 aus 1 Gutschein", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Paket_zeigt_die_offenen_Behandlungen_und_den_anteiligen_Restwert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var (_, package) = await SellAsync(spa, register, VoucherKind.SinglePurpose, 450m, units: 10);
        await ValidUntilAsync(spa, package.Id, Today.AddDays(200));

        var (_, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        var row = Rows(pdf)[package.Code];
        Assert.Contains("Paket 10", row, StringComparison.Ordinal);
        Assert.Contains("Behandlungen", row, StringComparison.Ordinal);
        Assert.Contains("Massage", row, StringComparison.Ordinal);
        Assert.Contains("| 450,00 | 450,00 |", row, StringComparison.Ordinal);
        Assert.Contains(Format(Today.AddDays(200)), row, StringComparison.Ordinal);
        Assert.Contains("10 von 10 offen", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Gutscheine_steht_ein_Hinweis_statt_der_Tabelle_und_die_Summe_ist_null()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        response.EnsureSuccessStatusCode();
        var text = PdfText.Text(pdf);
        Assert.Contains("Keine Gutscheine für diese Auswahl.", text, StringComparison.Ordinal);
        Assert.Contains("EUR 0,00 aus 0 aktiven Gutscheinen", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("active", "A,B")]
    [InlineData("redeemed", "C")]
    [InlineData("expired", "D")]
    [InlineData("voided", "E")]
    public async Task Der_Statusfilter_zeigt_nur_Gutscheine_in_diesem_Zustand(string status, string expected)
    {
        var (spa, _, vouchers) = await FiveAsync();

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id, $"?status={status}"));

        response.EnsureSuccessStatusCode();
        var rows = Rows(pdf);
        Assert.Equal([.. expected.Split(',').Select(k => vouchers[k].Code).Order()], [.. rows.Keys.Order()]);
        Assert.Contains($"Status: {status switch { "active" => "aktiv", "redeemed" => "aufgebraucht", "expired" => "abgelaufen", _ => "storniert" }}", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Zeitraum_filtert_nach_dem_Ausstellungsdatum_einschliesslich_beider_Tage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var early = (await SellAsync(spa, register)).Voucher;
        var first = (await SellAsync(spa, register)).Voucher;
        var last = (await SellAsync(spa, register)).Voucher;
        var late = (await SellAsync(spa, register)).Voucher;
        await IssuedAtAsync(spa, early.Id, TestSpa.At(23, 59, new DateOnly(2026, 9, 30)));
        await IssuedAtAsync(spa, first.Id, TestSpa.At(0, 0, new DateOnly(2026, 10, 1)));
        await IssuedAtAsync(spa, last.Id, TestSpa.At(23, 59, new DateOnly(2026, 10, 31)));
        await IssuedAtAsync(spa, late.Id, TestSpa.At(0, 0, new DateOnly(2026, 11, 1)));

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?from=2026-10-01&until=2026-10-31"));
        var (_, fromOnly) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?from=2026-11-01"));
        var (_, untilOnly) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?until=2026-09-30"));

        response.EnsureSuccessStatusCode();
        Assert.Equal([.. new[] { first.Code, last.Code }.Order()], [.. Rows(pdf).Keys.Order()]);
        Assert.Contains("Ausgestellt 01.10.2026 bis 31.10.2026", PdfText.Text(pdf), StringComparison.Ordinal);
        Assert.Equal([late.Code], [.. Rows(fromOnly).Keys]);
        Assert.Equal([early.Code], [.. Rows(untilOnly).Keys]);
    }

    [Fact]
    public async Task Laeuft_in_30_Tagen_ab_zeigt_aktive_Gutscheine_mit_Ablaufdatum_bis_heute_plus_30_Tage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var today = (await SellAsync(spa, register)).Voucher;
        var edge = (await SellAsync(spa, register)).Voucher;
        var later = (await SellAsync(spa, register)).Voucher;
        var gone = (await SellAsync(spa, register)).Voucher;
        _ = await SellAsync(spa, register); // ohne Ablaufdatum
        var used = (await SellAsync(spa, register)).Voucher;
        await ValidUntilAsync(spa, today.Id, Today);
        await ValidUntilAsync(spa, edge.Id, Today.AddDays(30));
        await ValidUntilAsync(spa, later.Id, Today.AddDays(31));
        await ValidUntilAsync(spa, gone.Id, Today.AddDays(-1));
        await ValidUntilAsync(spa, used.Id, Today.AddDays(5));
        await RedeemAsync(spa, used.Id);

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?expiring=true"));

        response.EnsureSuccessStatusCode();
        Assert.Equal([.. new[] { today.Code, edge.Code }.Order()], [.. Rows(pdf).Keys.Order()]);
        Assert.Contains("läuft in 30 Tagen ab", PdfText.Text(pdf), StringComparison.Ordinal);
        Assert.Equal(6, Rows((await PdfAsync(spa.Client, Url(spa.Location.Id, "?expiring=false"))).Pdf).Count);
    }

    [Fact]
    public async Task Der_Versandvermerk_nennt_Mail_Druck_und_Uebergabe_und_der_Filter_zeigt_nur_Gutscheine_ohne_Vermerk()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var mailed = (await SellAsync(spa, register)).Voucher;
        var printed = (await SellAsync(spa, register)).Voucher;
        var handed = (await SellAsync(spa, register)).Voucher;
        var open = (await SellAsync(spa, register)).Voucher;
        var to = $"beschenkte-{Guid.NewGuid():N}@example.at";
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);
        Assert.Equal(HttpStatusCode.Accepted, (await reception.PostAsJsonAsync($"/api/pos/vouchers/{mailed.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reception.PostAsync($"/api/pos/vouchers/{printed.Id}/printed", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reception.PostAsync($"/api/pos/vouchers/{handed.Id}/handed-over", null, Ct)).StatusCode);

        var (_, all) = await PdfAsync(spa.Client, Url(spa.Location.Id));
        PdfText.Keep(all, "gutscheinliste-versand");
        var (_, notSent) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?notSent=true"));

        var rows = Rows(all);
        Assert.Contains($"Mail am {Format(Today)}", rows[mailed.Code], StringComparison.Ordinal);
        Assert.Contains(to, Flat(rows[mailed.Code]), StringComparison.Ordinal);
        Assert.Contains($"Gedruckt am {Format(Today)}", rows[printed.Code], StringComparison.Ordinal);
        Assert.Contains($"Übergeben am {Format(Today)}", rows[handed.Code], StringComparison.Ordinal);
        Assert.EndsWith("| –", rows[open.Code], StringComparison.Ordinal);
        Assert.Equal([open.Code], [.. Rows(notSent).Keys]);
        Assert.Contains("noch nicht versendet", PdfText.Text(notSent), StringComparison.Ordinal);

        var mail = Assert.Single(await SentAsync(spa, mailed.Id));
        Assert.Equal(SentDocument.MailChannel, mail.Channel);
        var print = Assert.Single(await SentAsync(spa, printed.Id));
        Assert.Equal(SentDocument.PrintChannel, print.Channel);
        Assert.Equal("Anna", print.SentBy);
        Assert.Equal(SentDocument.HandoverChannel, Assert.Single(await SentAsync(spa, handed.Id)).Channel);
    }

    [Fact]
    public async Task Mehrfaches_Senden_zaehlt_mit_und_Druck_und_Mail_stehen_nebeneinander()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var voucher = (await SellAsync(spa, register)).Voucher;
        var to = $"beschenkte-{Guid.NewGuid():N}@example.at";
        for (var i = 0; i < 2; i++)
        {
            await spa.Client.PostAsJsonAsync($"/api/pos/vouchers/{voucher.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);
        }

        await spa.Client.PostAsync($"/api/pos/vouchers/{voucher.Id}/printed", null, Ct);

        var (_, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        var row = Rows(pdf)[voucher.Code];
        Assert.Contains($"Mail am {Format(Today)}", row, StringComparison.Ordinal);
        Assert.Contains("(2× gesendet)", row, StringComparison.Ordinal);
        Assert.Contains($"Gedruckt am {Format(Today)}", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_online_gekaufter_Gutschein_nennt_Kanal_Online_Empfaenger_und_den_Mailvermerk_an_den_Kaeufer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RegisterAsync(spa);
        var email = $"gutschein-{Guid.NewGuid():N}@example.at";
        using var client = spa.OnlineClient();
        var request = new VoucherOrderRequest(spa.Location.Id, VoucherKind.MultiPurpose, 50m, null, new OnlineContact("Gerda", "Schenk", email, null), "Mama", "Alles Gute!", true, "de");
        var response = await client.PostAsJsonAsync("/api/online/vouchers/orders", request, TestSpa.Json, Ct);
        var started = await TestSpa.ReadAsync<VoucherOrderStarted>(response, Ct);
        await client.PostAsJsonAsync($"/api/online/vouchers/orders/{started.Token}/test-payment", new TestPaymentResult(true), TestSpa.Json, Ct);
        await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Gutschein", StringComparison.Ordinal), Ct);

        var (_, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        var row = Assert.Single(Rows(pdf).Values);
        Assert.Contains("| Online |", row, StringComparison.Ordinal);
        Assert.Contains("| Mama |", row, StringComparison.Ordinal);
        Assert.Contains("Schenk", Flat(row), StringComparison.Ordinal);
        Assert.Contains($"Mail am {Format(Today)}", row, StringComparison.Ordinal);
        Assert.Contains(email, Flat(row), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lange_Namen_und_Adressen_laufen_nicht_in_Nachbarzellen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await RegisterAsync(spa);
        var buyer = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Marie-Antoinette-Josephine", "Müller-Lüdenscheidt-von-und-zu-Hohenstaufen", "m@example.at", "+43 664 000", null, null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, buyer.Id, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 50m, 0, null, null, null, VoucherKind.MultiPurpose, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, bill.Open, null, null)]), Ct);
        var voucher = Assert.Single(receipt.IssuedVouchers);
        var to = $"eine-sehr-lange-adresse-fuer-die-beschenkte-{Guid.NewGuid():N}@beispiel-unternehmen.example.at";
        await spa.Client.PostAsJsonAsync($"/api/pos/vouchers/{voucher.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        response.EnsureSuccessStatusCode();
        PdfText.Keep(pdf, "gutscheinliste-lange-namen");
        Assert.Contains(voucher.Code, Rows(pdf).Keys);
    }

    [Theory]
    [InlineData("?status=foo")]
    [InlineData("?status=Aktiv")]
    [InlineData("?expiring=yes")]
    [InlineData("?notSent=1")]
    [InlineData("?from=01.10.2026")]
    [InlineData("?until=2026-13-40")]
    [InlineData("?from=2026-11-01&until=2026-10-01")]
    public async Task Unbekannte_Werte_der_Filter_sind_400(string query)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, _) = await PdfAsync(spa.Client, Url(spa.Location.Id, query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Die_Leitung_und_das_Backoffice_duerfen_die_Liste_drucken_Rezeption_und_Therapeut_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.OK, (await PdfAsync(spa.Client, Url(spa.Location.Id))).Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PdfAsync(manager, Url(spa.Location.Id))).Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PdfAsync(backoffice, Url(spa.Location.Id))).Response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PdfAsync(reception, Url(spa.Location.Id))).Response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PdfAsync(therapist, Url(spa.Location.Id))).Response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PdfAsync(other.Client, Url(spa.Location.Id))).Response.StatusCode);
    }

    [Fact]
    public async Task Druck_und_Uebergabe_vermerken_darf_wer_den_Gutschein_sieht_der_Therapeut_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var voucher = (await SellAsync(spa, await RegisterAsync(spa))).Voucher;
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await backoffice.PostAsync($"/api/pos/vouchers/{voucher.Id}/printed", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.PostAsync($"/api/pos/vouchers/{voucher.Id}/printed", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.PostAsync($"/api/pos/vouchers/{voucher.Id}/handed-over", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/api/pos/vouchers/{voucher.Id}/printed", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/api/pos/vouchers/{voucher.Id}/handed-over", null, Ct)).StatusCode);
        Assert.Single(await SentAsync(spa, voucher.Id));
    }
}

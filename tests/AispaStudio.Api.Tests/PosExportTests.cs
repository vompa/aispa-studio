using System.Net;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Listen der Kassa als Excel und CSV (Export/Import, Aufgabe D): Belegliste mit Storno, Umsatz je Tag, Zahlart, Behandlung und Person, Gutscheinbestand und
/// Tagesabschlüsse; nur für die Leitung, ein fremder Mandant sieht nichts. Belege entstehen mit der echten Uhr, daher filtern die Tests auf den Wiener Tag von heute.
/// </summary>
public sealed class PosExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private sealed record Sales(TestSpa Spa, ReceiptDto Massage, ReceiptDto Tea, ReceiptDto Storno);

    /// <summary>Massage (79 €) mit Gutschein (50 €) für den Gast, ein Tee (12,50 €), der storniert wird, und ein Tagesabschluss mit 1 € zu viel in der Lade.</summary>
    private async Task<Sales> SalesAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);

        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, 0, null, null, null, null, null), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, 50m, 0, null, null, null, VoucherKind.MultiPurpose, null), Ct);
        var massage = await CheckoutAsync(spa, bill);

        var teaBill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        teaBill = await spa.PostAsync<BillDto>($"/api/pos/bills/{teaBill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var tea = await CheckoutAsync(spa, teaBill);
        var storno = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{tea.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        _ = await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(130m, null), Ct);
        return new Sales(spa, massage, tea, storno);
    }

    private static Task<ReceiptDto> CheckoutAsync(TestSpa spa, BillDto bill) =>
        spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, bill.Open, null, null)]), Ct);

    private static string Path(TestSpa spa, string list, string format = "xlsx") =>
        $"/api/pos/locations/{spa.Location.Id}/export?list={list}&from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&format={format}";

    [Fact]
    public async Task Belegliste_mit_Zahlart_Storno_und_Gast_und_Eintrag_im_Verlauf()
    {
        var (spa, massage, tea, storno) = await SalesAsync();

        using var response = await spa.Client.GetAsync(Path(spa, "receipts"), Ct);
        var sheet = await ExportResponses.SheetAsync(response, Ct);

        Assert.Equal($"Belege_{ViennaToday:yyyy-MM-dd}.xlsx", ExportResponses.FileName(response));
        Assert.Equal("Belegliste", sheet.Name);
        Assert.Equal(["Kasse", "Nummer", "Tag", "Zeit", "Zahlarten", "Betrag", "Storno", "Gast"], ExportResponses.Headers(sheet));
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
        var rows = Enumerable.Range(2, 3).Select(sheet.Row).ToList();
        Assert.Equal([massage.Number, tea.Number, storno.Number], rows.Select(r => (long)r.Cell(2).GetDouble()));
        Assert.Equal([129d, 12.5d, -12.5d], rows.Select(r => r.Cell(6).GetDouble()));
        Assert.Equal(["", "storniert", $"Storno zu Nr. {tea.Number}"], rows.Select(r => r.Cell(7).GetString()));
        Assert.Equal(("Bar", $"{spa.Customer.FirstName} {spa.Customer.LastName}", ""), (rows[0].Cell(5).GetString(), rows[0].Cell(8).GetString(), rows[1].Cell(8).GetString()));
        Assert.Equal(ViennaToday.ToDateTime(TimeOnly.MinValue), rows[0].Cell(3).GetDateTime());

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal($"Export Belegliste (Excel, 3 Zeilen, {ViennaToday:d.M.yyyy} bis {ViennaToday:d.M.yyyy}) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Umsatz_je_Tag_je_Zahlart_und_je_Behandlung_und_Person()
    {
        var (spa, _, _, _) = await SalesAsync();

        using var days = await spa.Client.GetAsync(Path(spa, "revenue-days", "csv"), Ct);
        var daysBytes = await days.Content.ReadAsByteArrayAsync(Ct);
        SampleFiles.Save(ExportResponses.FileName(days), daysBytes);
        using var payments = await spa.Client.GetAsync(Path(spa, "revenue-payments"), Ct);
        using var treatments = await spa.Client.GetAsync(Path(spa, "revenue-treatments"), Ct);
        var paymentSheet = await ExportResponses.SheetAsync(payments, Ct);
        var treatmentSheet = await ExportResponses.SheetAsync(treatments, Ct);

        Assert.Equal(HttpStatusCode.OK, days.StatusCode);
        Assert.Equal($"Umsatz_je_Tag_{ViennaToday:yyyy-MM-dd}.csv", ExportResponses.FileName(days));
        Assert.Equal($"Tag;Belege;Stornos;Umsatz\r\n{ViennaToday:dd.MM.yyyy};2;1;129,00\r\n", Encoding.UTF8.GetString(daysBytes.AsSpan(3)));
        Assert.Equal(["Zahlart", "Betrag"], ExportResponses.Headers(paymentSheet));
        Assert.Equal(("Bar", 129d), (paymentSheet.Row(2).Cell(1).GetString(), paymentSheet.Row(2).Cell(2).GetDouble()));
        Assert.Equal(2, paymentSheet.LastRowUsed()!.RowNumber());
        Assert.Equal(["Behandlung", "Person", "Termine", "Umsatz"], ExportResponses.Headers(treatmentSheet));
        var row = treatmentSheet.Row(2);
        Assert.Equal(("Massage", "Anna", 1d, 79d), (row.Cell(1).GetString(), row.Cell(2).GetString(), row.Cell(3).GetDouble(), row.Cell(4).GetDouble()));
        Assert.Equal(2, treatmentSheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Gutscheinbestand_ohne_Zeitraum_und_Tagesabschluesse_mit_Differenz()
    {
        var (spa, _, _, _) = await SalesAsync();

        using var vouchers = await spa.Client.GetAsync($"/api/pos/locations/{spa.Location.Id}/export?list=vouchers&format=xlsx", Ct);
        using var closings = await spa.Client.GetAsync(Path(spa, "day-closings"), Ct);
        var voucherSheet = await ExportResponses.SheetAsync(vouchers, Ct);
        var closingSheet = await ExportResponses.SheetAsync(closings, Ct);

        Assert.Equal(["Code", "Art", "Wert", "Rest", "Gültig bis", "Status", "Käufer"], ExportResponses.Headers(voucherSheet));
        var voucher = voucherSheet.Row(2);
        Assert.False(string.IsNullOrWhiteSpace(voucher.Cell(1).GetString()));
        Assert.Equal(("Mehrzweck", 50d, 50d, "Offen", $"{spa.Customer.FirstName} {spa.Customer.LastName}"),
            (voucher.Cell(2).GetString(), voucher.Cell(3).GetDouble(), voucher.Cell(4).GetDouble(), voucher.Cell(6).GetString(), voucher.Cell(7).GetString()));
        Assert.Equal(["Tag", "Kasse", "Belege", "Umsatz", "Soll", "Gezählt", "Differenz"], ExportResponses.Headers(closingSheet));
        var closing = closingSheet.Row(2);
        Assert.Equal((ViennaToday.ToDateTime(TimeOnly.MinValue), 129d, 129d, 130d, 1d),
            (closing.Cell(1).GetDateTime(), closing.Cell(4).GetDouble(), closing.Cell(5).GetDouble(), closing.Cell(6).GetDouble(), closing.Cell(7).GetDouble()));
        Assert.Equal(2, closingSheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Nur_die_Leitung_und_nur_der_eigene_Mandant()
    {
        var (spa, _, _, _) = await SalesAsync();
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var stranger = await TestUsers.ClientAsync(fixture, Ct);

        using var forManager = await manager.GetAsync(Path(spa, "receipts"), Ct);
        using var forStranger = await stranger.GetAsync(Path(spa, "receipts"), Ct);
        using var unknownList = await spa.Client.GetAsync(Path(spa, "bons"), Ct);
        using var noRange = await spa.Client.GetAsync($"/api/pos/locations/{spa.Location.Id}/export?list=receipts&format=xlsx", Ct);
        using var vcf = await spa.Client.GetAsync(Path(spa, "receipts", "vcf"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forManager.StatusCode);
        Assert.Contains("nur die Leitung", await forManager.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains(forStranger.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest], new[] { unknownList.StatusCode, noRange.StatusCode, vcf.StatusCode });
        Assert.DoesNotContain(await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct), h => h.Text.Contains("Belegliste", StringComparison.Ordinal));
    }
}

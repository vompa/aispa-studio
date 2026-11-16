using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Nacherfassung von Ersatzbelegen, die die Windows-App ohne Verbindung ausgegeben hat (ADR 0096).</summary>
public sealed class SubstituteReceiptTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ersatzbeleg_wird_als_signierter_Beleg_nacherfasst_und_nennt_Nummer_und_Zeit_des_Ersatzbelegs()
    {
        // Given ein eingecheckter Termin über 79,00 und ein Artikel über 12,50, ohne Verbindung bar und mit Karte kassiert
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var article = await ArticleAsync(spa);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var input = new SubstituteReceiptInput(Guid.CreateVersion7(), "E-EMPFANG1-20261116-001", issuedAt, spa.Customer.Id, [appointment.Id],
            [new SubstituteArticle(article.Id, 1)],
            [new PaymentInput(PaymentMethod.Card, 50m, null, null), new PaymentInput(PaymentMethod.Cash, 46.50m, null, null)], 91.50m, 5m);

        // When die App den Ersatzbeleg nacherfasst
        var receipt = await ReenterAsync(spa, register, input);

        // Then entsteht ein gewöhnlicher signierter Beleg mit dem Bezug auf den Ersatzbeleg
        Assert.Equal((91.50m, 5m, 0m), (receipt.Total, receipt.Tip, receipt.Change));
        Assert.Equal("E-EMPFANG1-20261116-001", receipt.SubstituteNumber);
        Assert.Equal(issuedAt.ToUnixTimeSeconds(), receipt.SubstituteIssuedAt!.Value.ToUnixTimeSeconds());
        Assert.True(receipt.IssuedAt > issuedAt.AddHours(1));
        Assert.NotNull(receipt.FiscalCode);
        Assert.False(receipt.SignatureDeviceFailed);
        Assert.Equal(2, receipt.Lines.Count);
        Assert.Equal(spa.Customer.Id, receipt.CustomerId);
        Assert.Equal(BillStatus.Settled, (await spa.GetAsync<BillDto>($"/api/pos/bills/{receipt.BillId}", Ct)).Status);

        // And der Bon nennt die Nacherfassung
        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text", Ct);
        Assert.Contains("Nacherfassung zu Ersatzbeleg", text, StringComparison.Ordinal);
        Assert.Contains("E-EMPFANG1-20261116-001", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Derselbe_Ersatzbeleg_noch_einmal_gesendet_liefert_denselben_Beleg_und_bucht_nichts_doppelt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var article = await ArticleAsync(spa);
        var input = new SubstituteReceiptInput(Guid.CreateVersion7(), "E-1", DateTimeOffset.UtcNow.AddMinutes(-30), null, null,
            [new SubstituteArticle(article.Id, 2)], [new PaymentInput(PaymentMethod.Cash, 25m, null, null)], 25m);

        var first = await ReenterAsync(spa, register, input);
        var second = await ReenterAsync(spa, register, input);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Number, second.Number);
        var report = await spa.GetAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-report", Ct);
        Assert.Equal((1, 25m), (report.ReceiptCount, report.Revenue));
    }

    [Fact]
    public async Task Weicht_die_Summe_vom_Stand_der_Kasse_ab_wird_nichts_gebucht_und_die_Meldung_nennt_beide_Betraege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var article = await ArticleAsync(spa);
        var input = new SubstituteReceiptInput(Guid.CreateVersion7(), "E-2", DateTimeOffset.UtcNow.AddMinutes(-30), null, null,
            [new SubstituteArticle(article.Id, 1)], [new PaymentInput(PaymentMethod.Cash, 10m, null, null)], 10m);

        var response = await PostAsync(spa, register, input);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("E-2", body, StringComparison.Ordinal);
        Assert.Contains("10,00", body, StringComparison.Ordinal);
        Assert.Contains("12,50", body, StringComparison.Ordinal);
        var report = await spa.GetAsync<DayClosingDto>($"/api/pos/registers/{register.Id}/day-report", Ct);
        Assert.Equal(0, report.ReceiptCount);
    }

    [Fact]
    public async Task Ein_schon_abgerechneter_Termin_und_ungueltige_Angaben_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        SubstituteReceiptInput Input(string number, DateTimeOffset at, IReadOnlyList<Guid>? appointments) => new(Guid.CreateVersion7(), number, at, null,
            appointments, null, [new PaymentInput(PaymentMethod.Cash, 79m, null, null)], 79m);

        var billed = await PostAsync(spa, register, Input("E-3", DateTimeOffset.UtcNow.AddMinutes(-5), [appointment.Id]));
        var future = await PostAsync(spa, register, Input("E-4", DateTimeOffset.UtcNow.AddHours(2), [appointment.Id]));
        var noNumber = await PostAsync(spa, register, Input(" ", DateTimeOffset.UtcNow.AddMinutes(-5), [appointment.Id]));
        var empty = await PostAsync(spa, register, Input("E-5", DateTimeOffset.UtcNow.AddMinutes(-5), null));

        Assert.Equal(HttpStatusCode.Conflict, billed.StatusCode);
        Assert.Contains("schon auf einer Rechnung", await billed.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noNumber.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", null, null), Ct);

    private static async Task<ArticleDto> ArticleAsync(TestSpa spa)
    {
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        return await spa.PostAsync<ArticleDto>("/api/pos/articles",
            new ArticleInput($"Tee {Guid.NewGuid():N}"[..12], 12.50m, TaxCategory.Normal, accounts.First(a => a.Code == "4100").Id), Ct);
    }

    private static Task<HttpResponseMessage> PostAsync(TestSpa spa, CashRegisterDto register, SubstituteReceiptInput input) =>
        spa.Client.PostAsJsonAsync($"/api/pos/registers/{register.Id}/substitute-receipts", input, TestSpa.Json, Ct);

    private static async Task<ReceiptDto> ReenterAsync(TestSpa spa, CashRegisterDto register, SubstituteReceiptInput input)
    {
        var response = await PostAsync(spa, register, input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ReceiptDto>(TestSpa.Json, Ct))!;
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Kennzahlen des Tages in der Mitarbeiter-App (Slice 109): derselbe Bericht wie am Arbeitsplatz, Rechte der Rolle, nichts Neues dazu.</summary>
public sealed class ManagementOnPhoneTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string TodayReport(TestSpa spa)
    {
        var vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, vienna).DateTime);
        return $"/api/reports/locations/{spa.Location.Id}/management?from={today:yyyy-MM-dd}&until={today:yyyy-MM-dd}";
    }

    /// <summary>Eine Kasse mit einem bezahlten Beleg über 12,50 € in bar.</summary>
    private static async Task SellTeaAsync(TestSpa spa)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"R-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12.50m, null, null)]), Ct);
    }

    private static async Task<HttpClient> AppAsync(TestSpa spa, StaffRole role, string name)
    {
        var person = await spa.CreateEmployeeAsync(name, [spa.Massage.Id], spa.Location.Id, Ct);
        return await ChannelTests.StaffAppAsync(spa, role, person.Id, Ct);
    }

    [Fact]
    public async Task Gegeben_die_Inhaberin_in_der_Mitarbeiter_App_Wenn_sie_den_Tagesbericht_abruft_Dann_steht_der_Umsatz_der_Kassa_darin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SellTeaAsync(spa);
        using var app = await AppAsync(spa, StaffRole.Owner, "Ina");

        var response = await app.GetAsync(TodayReport(spa), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ManagementReportDto>(TestSpa.Json, Ct);
        Assert.False(report!.AmountsHidden);
        Assert.Equal(12.50m, report.Kpis.Single(k => k.Key == "revenue").Value);
    }

    [Fact]
    public async Task Gegeben_die_Rezeption_in_der_Mitarbeiter_App_Wenn_sie_den_Tagesbericht_abruft_Dann_antwortet_die_API_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var app = await AppAsync(spa, StaffRole.Reception, "Rita");

        var response = await app.GetAsync(TodayReport(spa), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gegeben_die_Managerin_ohne_Betragsrecht_Wenn_sie_den_Tagesbericht_abruft_Dann_fehlen_die_Geldbetraege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SellTeaAsync(spa);
        using var app = await AppAsync(spa, StaffRole.Manager, "Mara");

        var report = await app.GetFromJsonAsync<ManagementReportDto>(TodayReport(spa), TestSpa.Json, Ct);

        Assert.True(report!.AmountsHidden);
        Assert.DoesNotContain(report.Kpis, k => k.Unit == "€");
    }

    [Fact]
    public async Task Gegeben_zwei_offene_Anfragen_Wenn_der_Bericht_geladen_wird_Dann_zaehlt_openRequests_zwei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddMailAsync(spa, InboundMailStatus.NeedsReview);
        await AddMailAsync(spa, InboundMailStatus.NeedsReview);
        await AddMailAsync(spa, InboundMailStatus.Booked);

        var report = await spa.GetAsync<ManagementReportDto>(TodayReport(spa), Ct);

        Assert.Equal(2, report.Kpis.Single(k => k.Key == "openRequests").Value);
    }

    private static async Task AddMailAsync(TestSpa spa, InboundMailStatus status)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        db.InboundMails.Add(new InboundMail
        {
            MessageId = $"m-{Guid.NewGuid():N}",
            From = "gast@example.at",
            To = "spa@example.at",
            Subject = "Termin",
            Body = "Ein Termin bitte",
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = status,
            LocationId = spa.Location.Id,
        });
        await db.SaveChangesAsync(Ct);
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>Firmendaten des Spas (ADR 0052): pflegen, Impressum, Rechnung und Sofortüberweisung an Standorten ohne eigene Angaben.</summary>
public sealed class CompanyProfileTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly CompanyProfile Profile = new("Test-Spa GmbH", "Teststraße 1", "1010", "Wien", "+43 1 234 56 78", "office@test-spa.at",
        "https://test-spa.at", " atu12345678 ", "FN 123456a", "Handelsgericht Wien", "Test-Spa GmbH", "at61 1904 3002 3457 3201", "bkauatww",
        "Betrieb eines Day-Spas", "Magistratisches Bezirksamt für den 1. Bezirk", "Mitglied der WKO Wien; Gewerbeordnung");

    [Fact]
    public async Task Firmendaten_speichern_pruefen_und_als_Impressum_zeigen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var page = spa.OnlineClient();
        var tenant = await spa.GetAsync<TenantDto>("/api/tenant", Ct);
        var before = await page.GetFromJsonAsync<OnlineImprint>("/api/online/imprint", TestSpa.Json, Ct);

        var invalid = await spa.Client.PutAsJsonAsync("/api/tenant",
            new TenantInput(tenant.Name, tenant.Country, Profile with { Iban = "AT00 1234", Email = "kein-mail" }), TestSpa.Json, Ct);
        var saved = await spa.PutAsync<TenantDto>("/api/tenant", new TenantInput("Test-Spa am Ring", tenant.Country, Profile), Ct);
        var renamedOnly = await spa.PutAsync<TenantDto>("/api/tenant", new TenantInput("Test-Spa am Ring", tenant.Country), Ct);
        var imprint = await page.GetFromJsonAsync<OnlineImprint>("/api/online/imprint", TestSpa.Json, Ct);

        Assert.False(before!.Complete);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal(["email", "iban"], problem!.Errors.Keys.Order());
        Assert.Equal(Profile with { VatId = "ATU12345678", Iban = "AT611904300234573201", Bic = "BKAUATWW" }, saved.Company);
        Assert.Equal(saved.Company, renamedOnly.Company);
        Assert.Equal(("Test-Spa am Ring", "Test-Spa GmbH", "Teststraße 1", "ATU12345678", "Mitglied der WKO Wien; Gewerbeordnung", true),
            (imprint!.SpaName, imprint.LegalName, imprint.Street, imprint.VatId, imprint.ChamberAndRules, imprint.Complete));
        Assert.DoesNotContain("AT611904300234573201", await page.GetStringAsync("/api/online/imprint", Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rechnung_und_Sofortueberweisung_nehmen_die_Firmendaten_solange_der_Standort_keine_eigenen_hat()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tenant = await spa.GetAsync<TenantDto>("/api/tenant", Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa\nTeststraße 1, 1010 Wien", null, null, null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);

        var withoutAccount = await spa.Client.GetAsync($"/api/pos/bills/{bill.Id}/instant-transfer", Ct);
        await spa.PutAsync<TenantDto>("/api/tenant", new TenantInput(tenant.Name, tenant.Country, Profile), Ct);
        var qr = await spa.GetAsync<InstantTransferQr>($"/api/pos/bills/{bill.Id}/instant-transfer", Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12.50m, null, null)]), Ct);

        // Eigene Gesellschaft am Standort: deren Angaben, und kein Konto einer anderen Gesellschaft für die Sofortüberweisung.
        var location = (await spa.GetAsync<List<LocationDto>>("/api/locations", Ct)).Single(l => l.Id == spa.Location.Id);
        await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}", new LocationInput(location.Name, location.TimeZone, location.Currency,
            location.OpeningHours, location.Region, null, new LocationCompany("Test-Spa Ring GmbH", "ATU87654321", null, null)), Ct);
        var own = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}", Ct);
        var next = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        next = await spa.PostAsync<BillDto>($"/api/pos/bills/{next.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 5m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var ownWithoutAccount = await spa.Client.GetAsync($"/api/pos/bills/{next.Id}/instant-transfer", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, withoutAccount.StatusCode);
        var lines = qr.Payload.Split('\n');
        Assert.Equal(("BKAUATWW", "Test-Spa GmbH", "AT611904300234573201"), (lines[4], lines[5], lines[6]));
        Assert.Equal(new LocationCompany("Test-Spa GmbH", "ATU12345678", "FN 123456a", "Handelsgericht Wien"), receipt.Company);
        Assert.Equal(new LocationCompany("Test-Spa Ring GmbH", "ATU87654321", null, null), own.Company);
        Assert.Equal(HttpStatusCode.BadRequest, ownWithoutAccount.StatusCode);
    }
}

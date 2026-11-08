using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä283 (Slice 121, ADR 0085): Die Windows-App spricht die Team-API unter <c>/api/v1/…</c> an; Kundenbereich und Betreiber bleiben ohne Version.</summary>
public sealed class ApiVersionClientTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Termin_laden_geht_an_api_v1()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(TestData.Appointment(10, TestData.Anna)));
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());

        await vm.InitializeAsync();
        await vm.LoadAsync();

        Assert.Single(vm.Columns[0].Items);
        Assert.Contains($"/api/v1/locations/{TestData.LocationId}/calendar?date=2027-03-01", api.Requests);
        Assert.All(api.Requests, path => Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("api/system/status", "/api/v1/system/status")]
    [InlineData("api/online/locations", "/api/online/locations")]
    [InlineData("api/operator/spas", "/api/operator/spas")]
    [InlineData("api/v1/customers", "/api/v1/customers")]
    public async Task Team_Pfade_bekommen_v1_Kundenbereich_und_Betreiber_nicht(string path, string sent)
    {
        var api = new StubApi();

        _ = await api.Client().GetAsync<object>(path, Ct);

        Assert.Equal([sent], api.Requests);
    }

    [Fact]
    public async Task Statusabfrage_geht_an_api_v1()
    {
        var api = new StubApi();

        _ = await Record.ExceptionAsync(() => api.Client().StatusAsync(Ct));

        Assert.Equal(["/api/v1/system/status"], api.Requests);
    }

    [Fact]
    public async Task Rohdaten_mit_Abfrage_gehen_an_api_v1()
    {
        var api = new StubApi();

        _ = await api.Client().GetBytesAsync("api/pos/receipts/1/escpos?width=42", Ct);

        Assert.Equal(["/api/v1/pos/receipts/1/escpos?width=42"], api.Requests);
    }
}

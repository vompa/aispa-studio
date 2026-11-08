using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): Abschnitt „Kundenbereich einbetten“ der Firmendaten mit „Code kopieren“ (EmbedSection.tsx): der Code zeigt auf die Adresse, die die API nennt.</summary>
public sealed class AdminCompanyEmbedTests
{
    private const string BookingUrl = "https://buchen.day-spa-wien.example/buchen/?spa=0190f5a3-0000-7000-8000-000000000093";

    private static StubApi Api(string? bookingUrl) => new StubApi()
        .On("GET /api/tenant", new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty))
        .On("GET /api/tenant/embedding", new EmbeddingSettings(["https://www.day-spa-wien.at"], bookingUrl))
        .On("GET /api/tenant/history", new List<HistoryEntry>());

    private static async Task<CompanyViewModel> Open(StubApi api)
    {
        var company = new CompanyViewModel(api.Client(), TimeZoneInfo.Utc);
        await company.EnsureLoadedAsync();
        return company;
    }

    [Fact]
    public async Task Given_Firmendaten_When_Code_kopieren_Then_der_iframe_Code_mit_der_Adresse_der_API()
    {
        var company = await Open(Api(BookingUrl));

        Assert.True(company.HasEmbedCode);
        Assert.Equal(
            $"<iframe src=\"{BookingUrl}\" title=\"Online-Buchung\" width=\"100%\" height=\"900\" style=\"border:0\" loading=\"lazy\"></iframe>",
            company.EmbedCode);
    }

    [Fact]
    public async Task Given_die_API_nennt_keine_Adresse_Then_gibt_es_keinen_Code_zum_Kopieren()
    {
        var company = await Open(Api(null));

        Assert.False(company.HasEmbedCode);
        Assert.Null(company.EmbedCode);
    }

    [Fact]
    public async Task Given_Code_kopiert_When_die_Websites_geaendert_werden_Then_verschwindet_die_Meldung()
    {
        var company = await Open(Api(BookingUrl));
        company.EmbedStatus = "Code kopiert.";

        company.EmbedOrigins = "https://www.neu.example";

        Assert.Null(company.EmbedStatus);
    }

    [Fact]
    public async Task Given_Code_kopiert_When_die_Maske_neu_geladen_wird_Then_verschwindet_die_Meldung()
    {
        var company = await Open(Api(BookingUrl));
        company.EmbedStatus = "Code kopiert.";

        await company.LoadAsync();

        Assert.Null(company.EmbedStatus);
    }

    [Fact]
    public async Task Given_gespeicherte_Websites_Then_bleibt_der_Code_stehen_und_aendert_sich_nicht_mit_der_Eingabe()
    {
        var api = Api(BookingUrl)
            .On("PUT /api/tenant", new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty))
            .OnJson("PUT /api/tenant/embedding", body =>
            {
                var input = JsonSerializer.Deserialize<EmbeddingSettings>(body!, ApiClient.Json)!;
                return input with { BookingUrl = BookingUrl };
            });
        var company = await Open(api);
        var before = company.EmbedCode;

        company.EmbedOrigins = "https://www.neu.example";
        Assert.Equal(before, company.EmbedCode);
        Assert.True(await company.SaveChangesAsync());

        Assert.Equal(before, company.EmbedCode);
    }
}

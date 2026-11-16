using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Einwilligung in Erinnerungen an die nächste Behandlung beim Online-Buchen (ADR 0040): freiwilliges Häkchen, danach Double-Opt-in per Mail.</summary>
public sealed partial class OnlineBookingTests
{
    private async Task<MarketingConsent?> ConsentOfAsync(TestSpa spa, string email)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().MarketingConsents.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email, Ct);
    }

    private static Task<RebookSettingsDto> RemindersAsync(TestSpa spa, bool enabled) =>
        spa.PutAsync<RebookSettingsDto>("/api/retention/settings", new RebookSettingsDto(enabled, [new RebookIntervalDto(spa.MassageTreatment.Id, 42)]), Ct);

    private static async Task<bool> OffersConsentAsync(TestSpa spa)
    {
        using var client = spa.OnlineClient();
        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);
        return locations!.Single(l => l.Id == spa.Location.Id).RebookReminders;
    }

    [Fact]
    public async Task Gegeben_Erinnerungen_an_und_Haekchen_gesetzt_Dann_wartet_die_Einwilligung_auf_die_Bestaetigung_per_Mail()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RemindersAsync(spa, true);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var booked = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11), email) with { AcceptMarketing = true }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.True(await OffersConsentAsync(spa));
        var consent = await ConsentOfAsync(spa, email);
        Assert.NotNull(consent);
        Assert.Equal(MarketingConsentState.Pending, consent.State);
        Assert.Equal(MarketingConsents.SourceOnline, consent.Source);
        Assert.Null(consent.ConfirmedAt);
    }

    [Fact]
    public async Task Ohne_Haekchen_entsteht_keine_Einwilligung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RemindersAsync(spa, true);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var booked = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11), email), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.Null(await ConsentOfAsync(spa, email));
    }

    [Fact]
    public async Task Gegeben_Erinnerungen_aus_Dann_bietet_die_Buchung_das_Haekchen_nicht_an_und_fragt_nichts_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RemindersAsync(spa, false);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var booked = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11), email) with { AcceptMarketing = true }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.False(await OffersConsentAsync(spa));
        Assert.Null(await ConsentOfAsync(spa, email));
    }
}

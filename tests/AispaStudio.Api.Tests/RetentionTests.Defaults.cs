using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Tests;

/// <summary>P8 (17.11.2026): ein Standardabstand und Ausnahmen je Kategorie oder Behandlung statt eines Abstands je Behandlung; leer heißt kein Hinweis.</summary>
public sealed partial class RetentionTests
{
    private const string Category = "Massagen";

    private static RebookSettingsDto WithDefault(int? defaultDays, IReadOnlyList<RebookCategoryIntervalDto>? categories = null, IReadOnlyList<Guid>? silent = null) =>
        new(true, []) { DefaultDays = defaultDays, CategoryIntervals = categories ?? [], SilentTreatmentIds = silent ?? [] };

    private async Task<(TestSpa Spa, CustomerDto Guest, AppointmentDto Last)> SpaWithConfirmedGuestAsync(string firstName)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await WithDbAsync(spa, db => db.Treatments.Where(t => t.Id == spa.MassageTreatment.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.Category, Category), Ct));
        var guest = await GuestAsync(spa, firstName);
        var last = await CompletedAsync(spa, guest);
        await spa.PutAsync<MarketingConsentDto>($"/api/customers/{guest.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), Ct);
        return (spa, guest, last);
    }

    /// <summary>Früher gab es ohne Abstand für genau diese Behandlung nie einen Hinweis; jetzt gilt der Standard.</summary>
    [Fact]
    public async Task Der_Standardabstand_gilt_fuer_Behandlungen_ohne_Ausnahme()
    {
        var (spa, _, last) = await SpaWithConfirmedGuestAsync("Dora");
        await spa.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(Days), Ct);

        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days - 1)));
        Assert.Equal(1, await PlanAsync(spa, last.End.AddDays(Days + 1)));
    }

    [Fact]
    public async Task Ohne_Standard_und_ohne_Ausnahme_gibt_es_keinen_Hinweis()
    {
        var (spa, _, last) = await SpaWithConfirmedGuestAsync("Edda");
        await spa.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(null), Ct);

        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days + 1)));
    }

    [Fact]
    public async Task Die_Ausnahme_der_Kategorie_geht_vor_dem_Standard_und_die_der_Behandlung_vor_der_Kategorie()
    {
        var (spa, _, last) = await SpaWithConfirmedGuestAsync("Fina");
        await spa.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(Days, [new RebookCategoryIntervalDto(Category, 2 * Days)]), Ct);

        Assert.Equal(0, await PlanAsync(spa, last.End.AddDays(Days + 1)));
        Assert.Equal(1, await PlanAsync(spa, last.End.AddDays(2 * Days + 1)));

        var (other, _, otherLast) = await SpaWithConfirmedGuestAsync("Gina");
        await other.PutAsync<RebookSettingsDto>("/api/retention/settings",
            new RebookSettingsDto(true, [new RebookIntervalDto(other.MassageTreatment.Id, 10)]) { DefaultDays = Days, CategoryIntervals = [new(Category, 2 * Days)] }, Ct);
        Assert.Equal(1, await PlanAsync(other, otherLast.End.AddDays(11)));
    }

    [Fact]
    public async Task Leere_Tage_bei_Kategorie_und_Behandlung_heissen_kein_Hinweis_trotz_Standard()
    {
        var (byCategory, _, categoryLast) = await SpaWithConfirmedGuestAsync("Hanna");
        await byCategory.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(Days, [new RebookCategoryIntervalDto(Category, null)]), Ct);
        Assert.Equal(0, await PlanAsync(byCategory, categoryLast.End.AddDays(Days + 1)));

        var (byTreatment, _, treatmentLast) = await SpaWithConfirmedGuestAsync("Ida");
        await byTreatment.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(Days, silent: [byTreatment.MassageTreatment.Id]), Ct);
        Assert.Equal(0, await PlanAsync(byTreatment, treatmentLast.End.AddDays(Days + 1)));
    }

    [Fact]
    public async Task Standard_und_Ausnahmen_werden_gespeichert_und_ein_Aufruf_nach_altem_Muster_laesst_sie_stehen()
    {
        var (spa, _, _) = await SpaWithConfirmedGuestAsync("Jana");
        var silent = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Ohne Hinweis", 50, 5, 10, 79m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        var saved = await spa.PutAsync<RebookSettingsDto>("/api/retention/settings",
            WithDefault(Days, [new RebookCategoryIntervalDto(Category.ToUpperInvariant(), 56)], [silent.Id]), Ct);

        Assert.Equal(Days, saved.DefaultDays);
        Assert.Equal([new RebookCategoryIntervalDto(Category, 56)], saved.CategoryIntervals);
        Assert.Equal([silent.Id], saved.SilentTreatmentIds);
        Assert.Empty(saved.Intervals);

        var old = await spa.PutAsync<RebookSettingsDto>("/api/retention/settings",
            new RebookSettingsDto(false, [new RebookIntervalDto(spa.MassageTreatment.Id, 70)]), Ct);
        Assert.False(old.Enabled);
        Assert.Equal(Days, old.DefaultDays);
        Assert.Equal([new RebookCategoryIntervalDto(Category, 56)], old.CategoryIntervals);
        Assert.Equal([silent.Id], old.SilentTreatmentIds);
        Assert.Equal(70, Assert.Single(old.Intervals).Days);

        var cleared = await spa.PutAsync<RebookSettingsDto>("/api/retention/settings", WithDefault(null), Ct);
        Assert.Null(cleared.DefaultDays);
        Assert.Empty(cleared.CategoryIntervals!);
        Assert.Empty(cleared.SilentTreatmentIds!);
    }

    [Fact]
    public async Task Ungueltige_Angaben_zum_Standard_und_zu_Kategorien_werden_abgelehnt()
    {
        var (spa, _, _) = await SpaWithConfirmedGuestAsync("Kira");

        async Task<HttpStatusCode> PutAsync(RebookSettingsDto settings) =>
            (await spa.Client.PutAsJsonAsync("/api/retention/settings", settings, TestSpa.Json, Ct)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(3)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(800)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(Days, [new RebookCategoryIntervalDto("Gibt es nicht", 30)])));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(Days, [new RebookCategoryIntervalDto(Category, 3)])));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(Days, [new(Category, 30), new(Category.ToLowerInvariant(), 40)])));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(WithDefault(Days, silent: [Guid.NewGuid()])));
        Assert.Equal(HttpStatusCode.OK, await PutAsync(WithDefault(Days, [new RebookCategoryIntervalDto(Category, null)])));
    }
}

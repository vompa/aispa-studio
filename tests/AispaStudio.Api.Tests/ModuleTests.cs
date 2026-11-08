using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AispaStudio.Api.Tests;

/// <summary>Zuschaltbare Webmodule und Einzelplatz-Betrieb (ADR 0041).</summary>
public sealed class ModuleTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Server_Betrieb_hat_alle_Module_eingeschaltet()
    {
        using var client = fixture.CreateClient();

        var modules = await client.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);

        Assert.Equal(new SystemModules(false, true, true, true, true, true, true, true), modules);
    }

    [Fact]
    public async Task Standalone_schaltet_alle_Webmodule_aus_und_laesst_den_Kern_laufen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:Standalone", "true"));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);
        client.DefaultRequestHeaders.Add("X-Aispa-Spa", spa.TenantId.ToString());

        var modules = await client.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);
        Assert.Equal(new SystemModules(true, false, false, false, false, false, false, false, SubscriptionPlan.SpaPlus), modules);

        foreach (var path in new[]
                 {
                     "/api/online/locations",
                     "/api/inbox",
                     "/api/reminders/settings",
                     "/api/retention/settings",
                     $"/api/reports/locations/{spa.Location.Id}/benchmark",
                 })
        {
            var response = await client.GetAsync(path, Ct);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path}: {response.StatusCode}");
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
            Assert.Equal("Dieses Modul ist ausgeschaltet.", problem!.Title);
        }

        var assistant = await client.PostAsJsonAsync("/api/assistant", new AssistantQuestion("Wer hat heute frei?", spa.Location.Id), Ct);
        Assert.Equal(HttpStatusCode.NotFound, assistant.StatusCode);

        // Kern: Kalender, Stammdaten und Anamnese auf Papier bleiben.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/locations", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/online/anamnesis/form", Ct)).StatusCode);
    }

    [Fact]
    public async Task Einzelne_Module_lassen_sich_im_Standalone_zuschalten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Modules:Reminders", "true");
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);

        var modules = await client.GetFromJsonAsync<SystemModules>("/api/system/modules", TestSpa.Json, Ct);

        Assert.Equal(new SystemModules(true, false, false, false, true, false, false, false, SubscriptionPlan.SpaPlus), modules);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reminders/settings", Ct)).StatusCode);
    }

    [Fact]
    public async Task Abo_Stufe_schaltet_Module_je_Spa_frei_und_der_Kern_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var planPath = $"/api/operator/spas/{spa.TenantId}/plan";

        // Neue Spas haben alles (ADR 0015: Funktionen nicht wegnehmen).
        Assert.Equal(new SystemModules(false, true, true, true, true, true, true, true, SubscriptionPlan.SpaPlus),
            await spa.GetAsync<SystemModules>("/api/system/modules", Ct));

        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Client.PutAsJsonAsync(planPath, new OperatorPlanInput(SubscriptionPlan.Start), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await operatorClient.PutAsJsonAsync(planPath, new OperatorPlanInput((SubscriptionPlan)7), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await operatorClient.PutAsJsonAsync($"/api/operator/spas/{Guid.NewGuid()}/plan", new OperatorPlanInput(SubscriptionPlan.Start), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await operatorClient.PutAsJsonAsync(planPath, new OperatorPlanInput(SubscriptionPlan.Start), TestSpa.Json, Ct)).StatusCode);

        Assert.Equal(new SystemModules(false, true, false, false, true, false, false, true, SubscriptionPlan.Start),
            await spa.GetAsync<SystemModules>("/api/system/modules", Ct));
        var listed = Assert.Single(await operatorClient.GetFromJsonAsync<List<OperatorSpaDto>>("/api/operator/spas", TestSpa.Json, Ct) ?? [],
            s => s.Id == spa.TenantId);
        Assert.Equal(SubscriptionPlan.Start, listed.Plan);

        // Nicht in der Stufe: 403 mit Hinweis auf die nötige Stufe; in der Stufe und Kern: wie bisher.
        var benchmark = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/benchmark", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, benchmark.StatusCode);
        var problem = await benchmark.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Nicht in der Abo-Stufe.", problem!.Title);
        Assert.Equal("Dieses Modul gehört zur Stufe „Spa Plus“; das Spa hat „Start“.", problem.Detail);
        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Client.GetAsync("/api/retention/settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Client.GetAsync("/api/inbox", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync("/api/reminders/settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync("/api/customers", Ct)).StatusCode);
        using (var online = spa.OnlineClient())
        {
            Assert.Equal(HttpStatusCode.OK, (await online.GetAsync("/api/online/locations", Ct)).StatusCode);
        }

        // Ohne Terminannahme in der Stufe bekommt das Spa keine Nummer.
        var route = await operatorClient.PostAsJsonAsync("/api/operator/routes",
            new IntakeRouteInput(spa.TenantId, BookingChannel.Sms, $"+43660{Random.Shared.Next(1_000_000, 9_999_999)}", null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, route.StatusCode);
        Assert.Contains("Spa Plus", await route.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent,
            (await operatorClient.PutAsJsonAsync(planPath, new OperatorPlanInput(SubscriptionPlan.Spa), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync("/api/retention/settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Client.GetAsync("/api/inbox", Ct)).StatusCode);
    }
}

using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 102 (Ä191): native Masken „Kosten“ und „Betriebsinfo“ nach web/src/costs und web/src/businessinfo. Die Zahlen kommen fest aus dem Stub; ihre Rechnung prüfen die API-Tests.</summary>
public sealed class AdminCostsBusinessInfoTests
{
    private sealed class OctoberTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static async Task<CostsViewModel> Costs(StubApi api, CurrentUser? user = null)
    {
        var costs = new CostsViewModel(api.Client(), user ?? TestData.Owner(), new OctoberTime());
        await costs.EnsureLoadedAsync();
        return costs;
    }

    private static async Task<BusinessInfoViewModel> BusinessInfo(StubApi api, CurrentUser? user = null)
    {
        var info = new BusinessInfoViewModel(api.Client(), user ?? TestData.Owner());
        await info.EnsureLoadedAsync();
        return info;
    }

    [Fact]
    public async Task Kosten_zeigen_die_Vollkosten_der_API_fuer_den_laufenden_Monat()
    {
        var api = AddCosts(new StubApi(), "2026-10");

        var costs = await Costs(api);

        Assert.Contains(api.Calls, c => c.Route == $"GET /api/costs/locations/{LocationId}/analysis?month=2026-10");
        var row = Assert.Single(costs.Rows);
        Assert.Equal("20,80 € (27,73 %)", row.Margin);
        Assert.Equal(65m, costs.PlannedUtilization);
        Assert.False(costs.HasUnsavedChanges);
    }

    [Fact]
    public async Task Kosten_speichern_Saetze_und_nur_die_geaenderten_Raeume_und_Personen()
    {
        var settingsRoute = $"PUT /api/costs/locations/{LocationId}/settings";
        var roomRoute = $"PUT /api/costs/rooms/{RoomId}";
        var personRoute = $"PUT /api/costs/employees/{PersonId}";
        var api = AddCosts(new StubApi(), "2026-10")
            .On(settingsRoute, CostSettings())
            .On(roomRoute, RoomCost())
            .On(personRoute, EmployeeCost());
        var costs = await Costs(api);

        costs.PlannedUtilization = 70m;
        Assert.True(costs.HasUnsavedChanges);
        Assert.True(await costs.SaveChangesAsync());

        Assert.Equal(70, Sent<LocationCostSettingsDto>(api, settingsRoute).PlannedUtilizationPercent);
        Assert.DoesNotContain(api.Calls, c => c.Route == roomRoute || c.Route == personRoute);
    }

    [Fact]
    public async Task Kosten_lehnen_eine_Auslastung_ausserhalb_der_Grenzen_ab_und_senden_nichts()
    {
        var settingsRoute = $"PUT /api/costs/locations/{LocationId}/settings";
        var api = AddCosts(new StubApi(), "2026-10").On(settingsRoute, CostSettings());
        var costs = await Costs(api);

        costs.PlannedUtilization = 0m;

        Assert.False(await costs.SaveChangesAsync());
        Assert.NotNull(costs.UtilizationError);
        Assert.DoesNotContain(api.Calls, c => c.Route == settingsRoute);
    }

    [Fact]
    public async Task Betriebsinfo_legt_einen_Eintrag_an_und_verlangt_Bezeichnung_und_Erreichbarkeit()
    {
        var created = Contact() with { Id = Guid.NewGuid(), Designation = "Installateur" };
        var api = AddBusinessInfo(new StubApi()).On("POST /api/business-contacts", created);
        var info = await BusinessInfo(api);

        await info.NewCommand.ExecuteAsync(null);
        Assert.False(await info.SaveChangesAsync());
        Assert.NotNull(info.DesignationError);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/business-contacts");

        info.Designation = "Installateur";
        info.Phone = "+43 1 234 56 10";
        Assert.True(await info.SaveChangesAsync());

        var sent = Sent<BusinessContactInput>(api, "POST /api/business-contacts");
        Assert.Equal("Installateur", sent.Designation);
        Assert.Equal("+43 1 234 56 10", sent.Phone);
        Assert.True(sent.AllLocations);
    }

    [Fact]
    public async Task Betriebsinfo_Mitarbeitende_sehen_ohne_Recht_kein_Archiv()
    {
        var api = AddBusinessInfo(new StubApi());

        var info = await BusinessInfo(api, TestData.Reception());

        Assert.False(info.CanManage);
        Assert.Contains(api.Calls, c => c.Route == "GET /api/business-contacts?includeArchived=false");
        Assert.DoesNotContain(api.Calls, c => c.Route == "GET /api/business-contacts?includeArchived=true");
    }

    [Fact]
    public async Task Betriebsinfo_archiviert_einen_Eintrag_erst_nach_der_Rueckfrage()
    {
        var route = $"POST /api/business-contacts/{ContactId}/archive";
        var api = AddBusinessInfo(new StubApi()).On(route, Contact(archived: true));
        var info = await BusinessInfo(api);
        var dialogs = new FakeDialogs();
        info.Dialogs = dialogs;
        info.SelectedRow = info.Rows.Single(r => r.Id == ContactId);
        await Task.Yield();
        Assert.False(info.HasUnsavedChanges);

        await info.ArchiveCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Asked);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);

        dialogs.Answer(ConfirmChoice.Confirmed);
        await info.ArchiveCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == route);
        Assert.Equal("Archiviert.", info.Message);
    }

    [Fact]
    public async Task Kosten_speichern_nach_einem_Ladefehler_nichts_auf_den_neuen_Standort()
    {
        var api = AddCosts(new StubApi(), "2026-10");
        var costs = await Costs(api);
        var other = costs.Locations.Single(l => l.Id != LocationId);
        var settingsRoute = $"PUT /api/costs/locations/{other.Id}/settings";
        api.On(settingsRoute, CostSettings())
            .On($"GET /api/costs/locations/{other.Id}/analysis?month=2026-10", new { }, System.Net.HttpStatusCode.InternalServerError)
            .On($"GET /api/costs/locations/{other.Id}/settings", new { }, System.Net.HttpStatusCode.InternalServerError)
            .On($"GET /api/costs/locations/{other.Id}/rooms", new { }, System.Net.HttpStatusCode.InternalServerError)
            .On($"GET /api/costs/locations/{other.Id}/employees", new { }, System.Net.HttpStatusCode.InternalServerError);

        costs.SelectedLocation = other;
        await Task.Yield();

        Assert.NotNull(costs.LoadError);
        Assert.Empty(costs.Rows);
        Assert.False(await costs.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route == settingsRoute);
    }

    [Fact]
    public async Task Kosten_ein_Monatswechsel_waehrend_des_Standortwechsels_laesst_die_Saetze_des_neuen_Standorts_ankommen()
    {
        var api = AddCosts(new StubApi(), "2026-10");
        var costs = await Costs(api);
        var other = costs.Locations.Single(l => l.Id != LocationId);
        var gate = new TaskCompletionSource();
        api.On($"GET /api/costs/locations/{other.Id}/analysis?month=2026-10", CostAnalysis("2026-10"))
            .On($"GET /api/costs/locations/{other.Id}/analysis?month=2026-09", CostAnalysis("2026-09"))
            .On($"GET /api/costs/locations/{other.Id}/settings", CostSettings() with { PlannedUtilizationPercent = 50 })
            .On($"GET /api/costs/locations/{other.Id}/rooms", new List<RoomCostDto>())
            .On($"GET /api/costs/locations/{other.Id}/employees", new List<EmployeeCostDto>())
            .Hold($"GET /api/costs/locations/{other.Id}/settings", gate.Task);

        costs.SelectedLocation = other;
        costs.SelectedMonth = costs.Months[1];
        gate.SetResult();
        for (var i = 0; i < 100 && costs.PlannedUtilization != 50; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(50m, costs.PlannedUtilization);
        Assert.False(costs.HasUnsavedChanges);
    }

    [Fact]
    public async Task Kosten_nach_dem_Verwerfen_gibt_es_sofort_nichts_Ungespeichertes_mehr()
    {
        var costs = await Costs(AddCosts(new StubApi(), "2026-10"));
        costs.PlannedUtilization = 70m;
        Assert.True(costs.HasUnsavedChanges);

        costs.Discard();

        Assert.False(costs.HasUnsavedChanges);
    }

    [Fact]
    public async Task Betriebsinfo_fragt_bei_offenen_Aenderungen_vor_Archivieren_und_Archivierte_zeigen_nach()
    {
        var route = $"POST /api/business-contacts/{ContactId}/archive";
        var api = AddBusinessInfo(new StubApi()).On(route, Contact(archived: true));
        var info = await BusinessInfo(api);
        var dialogs = new FakeDialogs();
        info.Dialogs = dialogs;
        info.SelectedRow = info.Rows.Single(r => r.Id == ContactId);
        await Task.Yield();
        info.Designation = "Geändert";
        Assert.True(info.HasUnsavedChanges);

        await info.ArchiveCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Asked);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);

        info.ShowArchived = true;
        await Task.Yield();
        Assert.Equal(2, dialogs.Asked.Count);
        Assert.Equal("Geändert", info.Designation);
        Assert.DoesNotContain(api.Calls, c => c.Route == "GET /api/business-contacts?includeArchived=true");
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class CostTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vollkosten_aus_Material_Personal_Raum_Raumpflege_und_Gemeinkosten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationCostSettingsDto>($"/api/costs/locations/{spa.Location.Id}/settings", new LocationCostSettingsDto(30m, 30m, 891m, 50, 75), Ct);
        await spa.PutAsync<RoomCostDto>($"/api/costs/rooms/{spa.RoomA.Id}", new RoomCostInput(891m, 1m), Ct);
        await spa.PutAsync<RoomCostDto>($"/api/costs/rooms/{spa.RoomB.Id}", new RoomCostInput(1782m, 0m), Ct);
        await spa.PutAsync<EmployeeCostDto>($"/api/costs/employees/{spa.Anna.Id}", new EmployeeCostInput(36m), Ct);
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("Massageöl", 30m, "ml", 0.04m), new MaterialLine("Handtuchwäsche", 1m, "Stück", 0.80m) }, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct);

        var analysis = await spa.GetAsync<CostAnalysisDto>($"/api/costs/locations/{spa.Location.Id}/analysis?month=2027-03", Ct);

        // März 2027: 27 Öffnungstage zu 11 Stunden.
        Assert.Equal(27 * 660m, analysis.CapacityMinutesPerRoom);
        var massage = Assert.Single(analysis.Treatments);
        Assert.Equal((65, 73), (massage.StaffMinutes, massage.RoomMinutes));
        Assert.Equal(2.00m, massage.Material);
        Assert.Equal(47.67m, massage.Staff);
        Assert.Equal(10.88m, massage.Room);
        Assert.Equal(4.25m, massage.Cleaning);
        Assert.Equal(3.63m, massage.Overhead);
        Assert.Equal(68.43m, massage.Total);
        Assert.Equal(65.83m, massage.NetPrice);
        Assert.Equal(-2.60m, massage.Margin);
        Assert.Equal((1, -2.60m), (massage.Appointments, massage.ContributionTotal));
        Assert.Empty(massage.Missing);
        Assert.Equal(["Material", "Personal", "Raum", "Raumpflege", "Gemeinkosten"], massage.Parts.Select(p => p.Kind));
        Assert.Contains("30 ml Massageöl", massage.Parts[0].Basis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlende_Angaben_werden_genannt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var analysis = await spa.GetAsync<CostAnalysisDto>($"/api/costs/locations/{spa.Location.Id}/analysis?month=2027-03", Ct);

        var massage = Assert.Single(analysis.Treatments);
        Assert.Contains("Kein Material erfasst.", massage.Missing);
        Assert.Contains("Kein Stundensatz für das Personal.", massage.Missing);
        Assert.Contains(massage.Missing, m => m.StartsWith("Mietanteil fehlt für: Raum A, Raum B", StringComparison.Ordinal));
        Assert.Contains("Kein Stundensatz für die Raumpflege.", massage.Missing);
    }

    [Fact]
    public async Task Kostensaetze_sehen_nur_mit_dem_Recht_Berichte_mit_Betraegen_und_Eingaben_werden_geprueft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        var receptionEmployees = await reception.GetAsync($"/api/costs/locations/{spa.Location.Id}/employees", Ct);
        var withoutRight = await manager.GetAsync($"/api/costs/locations/{spa.Location.Id}/employees", Ct);
        var managerId = (await manager.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;
        (await spa.Client.PutAsJsonAsync($"/api/staff/{managerId}",
            new StaffAccessInput(StaffRole.Manager, false, [spa.Location.Id], null, true, AmountReports: true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        var managerSaves = await manager.PutAsJsonAsync($"/api/costs/employees/{spa.Ben.Id}", new EmployeeCostInput(31.5m), TestSpa.Json, Ct);
        var employees = await manager.GetFromJsonAsync<List<EmployeeCostDto>>($"/api/costs/locations/{spa.Location.Id}/employees", TestSpa.Json, Ct);
        var invalid = await spa.Client.PutAsJsonAsync($"/api/costs/locations/{spa.Location.Id}/settings",
            new LocationCostSettingsDto(-1m, 30m, 0m, 0, 75), TestSpa.Json, Ct);
        var badMaterial = await spa.Client.PutAsJsonAsync($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 0m, "ml", 1m) }, TestSpa.Json, Ct);
        var badMonth = await spa.Client.GetAsync($"/api/costs/locations/{spa.Location.Id}/analysis?month=März", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, receptionEmployees.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutRight.StatusCode);
        Assert.Equal(HttpStatusCode.OK, managerSaves.StatusCode);
        Assert.Equal([("Anna", null), ("Ben", 31.5m)], employees!.Select(e => (e.EmployeeName, e.HourlyCost)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMaterial.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMonth.StatusCode);
    }
}

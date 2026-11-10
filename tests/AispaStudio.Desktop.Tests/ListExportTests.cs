using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Der Knopf „Exportieren“ in Kassa, Lager, Zeiterfassung, Dienstplan und Management-Bericht (Export/Import, Aufgabe D): wer ihn sieht, welche Einträge er hat, welchen Pfad ein Klick
/// aufruft und wie die Seite „Gespeichert: …“ meldet. Inhalt, Format und Rechte der Dateien prüfen die API-Tests.
/// </summary>
public sealed class ListExportTests
{
    private static readonly byte[] File = [1, 2, 3];

    private sealed class PosTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static async Task Click(ExportViewModel export, string label) =>
        await export.ExportCommand.ExecuteAsync(Assert.Single(export.Options, o => o.Label == label));

    private static string[] Labels(ExportViewModel? export) => [.. export!.Options.Select(o => o.Label)];

    // ---------- Kassa ----------

    private static StubApi PosApi() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>());

    private static async Task<PosViewModel> Pos(StubApi api, bool ownerLevel)
    {
        var pos = new PosViewModel(api.Client(), new PosTime()) { OwnerLevel = ownerLevel };
        await pos.InitializeAsync(SpaSample.LocationId);
        return pos;
    }

    [Fact]
    public async Task Kassa_Leitung_sieht_sechs_Listen_in_Excel_und_CSV_die_Rezeption_keinen_Knopf()
    {
        var owner = await Pos(PosApi(), ownerLevel: true);
        var reception = await Pos(PosApi(), ownerLevel: false);

        Assert.Equal(
            ["Belegliste (Excel)", "Belegliste (CSV)", "Umsatz je Tag (Excel)", "Umsatz je Tag (CSV)", "Umsatz je Zahlart (Excel)", "Umsatz je Zahlart (CSV)",
                "Umsatz je Behandlung und Person (Excel)", "Umsatz je Behandlung und Person (CSV)", "Tagesabschlüsse (Excel)", "Tagesabschlüsse (CSV)", "Gutscheinbestand (Excel)", "Gutscheinbestand (CSV)"],
            Labels(owner.Export));
        Assert.Null(reception.Export);
    }

    [Fact]
    public async Task Kassa_Beleglisten_nehmen_den_Belegtag_der_Kasse_der_Gutscheinbestand_keinen_Zeitraum()
    {
        var api = PosApi()
            .OnFile($"GET /api/pos/locations/{SpaSample.LocationId}/export?list=receipts&from=2026-10-19&until=2026-10-19&format=xlsx", File, "Belege_2026-10-19.xlsx")
            .OnFile($"GET /api/pos/locations/{SpaSample.LocationId}/export?list=vouchers&format=csv", File, "Gutscheine.csv");
        var files = new FakeFileSaver();
        var pos = await Pos(api, ownerLevel: true);
        pos.Export!.Files = files;

        await Click(pos.Export, "Belegliste (Excel)");
        Assert.Equal("Gespeichert: Belege_2026-10-19.xlsx", pos.ExportSaved);
        Assert.True(pos.HasExportSaved);

        await Click(pos.Export, "Gutscheinbestand (CSV)");
        Assert.Equal("Gespeichert: Gutscheine.csv", pos.ExportSaved);
        Assert.Equal(["Belege_2026-10-19.xlsx", "Gutscheine.csv"], files.Saved.Select(s => s.Name));
    }

    [Fact]
    public async Task Kassa_Fehler_des_Servers_steht_im_Fehlerhinweis_der_Seite()
    {
        var pos = await Pos(PosApi(), ownerLevel: true);
        pos.Export!.Files = new FakeFileSaver();

        await Click(pos.Export, "Umsatz je Tag (CSV)");

        Assert.False(string.IsNullOrEmpty(pos.Error));
        Assert.Null(pos.ExportSaved);
    }

    // ---------- Lager ----------

    private static async Task<StockViewModel> Stock(StubApi api, CurrentUser user)
    {
        var stock = new StockViewModel(api.Client(), user, new StockSample.FixedTime());
        await stock.EnsureLoadedAsync();
        return stock;
    }

    [Fact]
    public async Task Lager_nur_die_Leitung_sieht_fuenf_Listen_in_Excel_und_CSV()
    {
        var owner = await Stock(StockSample.Api(), TestData.Owner());
        var general = await Stock(StockSample.Api(), TestData.Owner(StaffRole.GeneralManager));
        var manager = await Stock(StockSample.Api(), TestData.Manager());

        Assert.Equal(
            ["Bestand je Lagerort (Excel)", "Bestand je Lagerort (CSV)", "Inventur (Excel)", "Inventur (CSV)", "Lagerbewegungen (Excel)", "Lagerbewegungen (CSV)", "Bestellungen (Excel)",
                "Bestellungen (CSV)", "Ablaufliste (Excel)", "Ablaufliste (CSV)"],
            Labels(owner.Export));
        Assert.NotNull(general.Export);
        Assert.Null(manager.Export);
    }

    [Fact]
    public async Task Lager_Bewegungen_nehmen_die_letzten_30_Tage_bis_heute_Bestand_keinen_Zeitraum()
    {
        var api = StockSample.Api()
            .OnFile($"GET {StockSample.Route("/export?list=movements&from=2027-01-30&until=2027-03-01&format=csv")}", File, "Lagerbewegungen_2027-03-01.csv")
            .OnFile($"GET {StockSample.Route("/export?list=levels&format=xlsx")}", File, "Bestand_2027-03-01.xlsx");
        var stock = await Stock(api, TestData.Owner());
        stock.Export!.Files = new FakeFileSaver();

        await Click(stock.Export, "Lagerbewegungen (CSV)");
        Assert.Equal("Gespeichert: Lagerbewegungen_2027-03-01.csv", stock.ExportSaved);

        await Click(stock.Export, "Bestand je Lagerort (Excel)");
        Assert.Equal("Gespeichert: Bestand_2027-03-01.xlsx", stock.ExportSaved);
    }

    // ---------- Zeiterfassung und Dienstplan ----------

    private static TeamContext Team(CurrentUser user) =>
        new(new StubApi().Client(), user, new RosterTests.RosterClock(), false) { Data = new TeamData([], [], [], [RosterSample.Vienna, RosterSample.Graz], [], []) };

    private static string StaffRoute(string query) => $"GET /api/locations/{RosterSample.Vienna.Id}/staff/export?{query}";

    [Fact]
    public void Zeiten_nur_die_Leitung_sieht_Stundenliste_und_Abwesenheiten()
    {
        Assert.Equal(["Stundenliste (Excel)", "Stundenliste (CSV)", "Abwesenheiten (Excel)", "Abwesenheiten (CSV)"], Labels(new TimesheetViewModel(Team(TestData.Owner()), null).Export));
        Assert.Null(new TimesheetViewModel(Team(TestData.Manager()), null).Export);
        Assert.Null(new RosterPlanViewModel(Team(TestData.Reception()), null).Export);
    }

    [Fact]
    public async Task Zeiten_Stundenliste_nimmt_den_Monat_der_Woche_und_auf_der_Personenkarte_die_Person()
    {
        var lena = RosterSample.Lena.Id;
        var api = new StubApi()
            .OnFile(StaffRoute("list=hours&month=2027-03&format=xlsx"), File, "Stunden_2027-03.xlsx")
            .OnFile(StaffRoute($"list=hours&month=2027-03&employeeId={lena}&format=csv"), File, "Stunden_Lena_2027-03.csv")
            .OnFile(StaffRoute("list=absences&from=2027-03-01&until=2027-03-31&format=csv"), File, "Abwesenheiten_2027-03.csv");
        var files = new FakeFileSaver();
        var user = TestData.Owner();
        var team = new TeamContext(api.Client(), user, new RosterTests.RosterClock(), false) { Data = Team(user).Data };
        var all = new TimesheetViewModel(team, null);
        var card = new TimesheetViewModel(team, lena);
        all.Export!.Files = files;
        card.Export!.Files = files;

        await Click(all.Export, "Stundenliste (Excel)");
        Assert.Equal("Gespeichert: Stunden_2027-03.xlsx", all.ExportSaved);

        await Click(all.Export, "Abwesenheiten (CSV)");
        Assert.Equal("Gespeichert: Abwesenheiten_2027-03.csv", all.ExportSaved);

        await Click(card.Export, "Stundenliste (CSV)");
        Assert.Equal("Gespeichert: Stunden_Lena_2027-03.csv", card.ExportSaved);
    }

    [Fact]
    public async Task Dienstplan_nimmt_die_Woche_und_die_Abwesenheiten_der_Woche()
    {
        var api = new StubApi()
            .OnFile(StaffRoute("list=roster&from=2027-03-01&format=xlsx"), File, "Dienstplan_2027-03-01.xlsx")
            .OnFile(StaffRoute("list=absences&from=2027-03-01&until=2027-03-07&format=xlsx"), File, "Abwesenheiten_2027-03-01.xlsx");
        var user = TestData.Owner();
        var plan = new RosterPlanViewModel(new TeamContext(api.Client(), user, new RosterTests.RosterClock(), false) { Data = Team(user).Data }, null);
        plan.Export!.Files = new FakeFileSaver();

        Assert.Equal(["Dienstplan der Woche (Excel)", "Dienstplan der Woche (CSV)", "Abwesenheiten (Excel)", "Abwesenheiten (CSV)"], Labels(plan.Export));
        await Click(plan.Export, "Dienstplan der Woche (Excel)");
        Assert.Equal("Gespeichert: Dienstplan_2027-03-01.xlsx", plan.ExportSaved);

        await Click(plan.Export, "Abwesenheiten (Excel)");
        Assert.Equal("Gespeichert: Abwesenheiten_2027-03-01.xlsx", plan.ExportSaved);
    }

    // ---------- Management-Bericht ----------

    [Fact]
    public async Task Bericht_Kennzahlen_nehmen_Standort_und_Zeitraum_der_Seite()
    {
        var location = StockSample.Salzburg;
        var api = new StubApi()
            .On($"GET /api/reports/locations/{location.Id}/management?from=2026-10-01&until=2026-10-31",
                new ManagementReportDto(location.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), [], [], [], [], [], [], false))
            .OnFile($"GET /api/reports/locations/{location.Id}/management/export?from=2026-10-01&until=2026-10-31&format=xlsx", File, "Kennzahlen_2026-10-01_bis_2026-10-31.xlsx");
        var report = new ManagementReportViewModel(api.Client(), new PosTime());
        report.SetRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        await report.ShowAsync(location, new DateOnly(2026, 10, 19));
        report.Export.Files = new FakeFileSaver();

        Assert.Equal(["Kennzahlen (Excel)", "Kennzahlen (CSV)"], Labels(report.Export));
        await Click(report.Export, "Kennzahlen (Excel)");

        Assert.Equal("Gespeichert: Kennzahlen_2026-10-01_bis_2026-10-31.xlsx", report.ExportSaved);
    }
}

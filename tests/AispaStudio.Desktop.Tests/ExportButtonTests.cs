using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Knopf „Exportieren“ (Export/Import, Aufgabe A): die App holt die Datei vom Server, speichert sie Byte für Byte unter dem Namen aus dem Content-Disposition und meldet
/// „Gespeichert: …“; Fehler des Servers stehen als Hinweis, Abbrechen im Dialog meldet nichts. Inhalt und Format der Datei prüfen die API-Tests.
/// </summary>
public sealed class ExportButtonTests
{
    private const string ExcelRoute = "GET /api/customers/export?format=xlsx&search=huber";
    private static readonly byte[] Excel = [0x50, 0x4B, 0x03, 0x04, 0x42];

    private static ExportViewModel Model(StubApi api, FakeFileSaver? files, List<string> saved, List<string> failed, string search = "huber") =>
        new(api.Client(), [ExportOption.For(ExportFormat.Xlsx, () => $"api/customers/export?format=xlsx&search={search}"), ExportOption.For(ExportFormat.Csv, () => $"api/customers/export?format=csv&search={search}")])
        {
            Files = files,
            Saved = saved.Add,
            Failed = failed.Add,
        };

    [Fact]
    public async Task Speichert_die_Datei_unter_dem_Namen_des_Servers_und_meldet_Gespeichert()
    {
        var api = new StubApi().OnFile(ExcelRoute, Excel, "Kunden_2026-11-09.xlsx");
        var files = new FakeFileSaver();
        List<string> saved = [], failed = [];
        var export = Model(api, files, saved, failed);

        Assert.Equal(["Excel", "CSV"], export.Options.Select(o => o.Label));
        await export.ExportCommand.ExecuteAsync(export.Options[0]);

        Assert.Contains(api.Calls, c => c.Route == ExcelRoute);
        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal("Kunden_2026-11-09.xlsx", name);
        Assert.Equal(Excel, content);
        Assert.Equal(["Gespeichert: Kunden_2026-11-09.xlsx"], saved);
        Assert.Empty(failed);
        Assert.False(export.IsBusy);
    }

    [Fact]
    public async Task Waehrend_des_Ladens_ist_der_Knopf_gesperrt()
    {
        var gate = new TaskCompletionSource();
        var api = new StubApi().OnFile(ExcelRoute, Excel, "Kunden.xlsx").Hold(ExcelRoute, gate.Task);
        var export = Model(api, new FakeFileSaver(), [], []);

        var running = export.ExportCommand.ExecuteAsync(export.Options[0]);
        Assert.True(export.IsBusy);
        Assert.False(export.ExportCommand.CanExecute(export.Options[1]));

        gate.SetResult();
        await running;
        Assert.False(export.IsBusy);
        Assert.True(export.ExportCommand.CanExecute(export.Options[1]));
    }

    [Fact]
    public async Task Fehler_des_Servers_steht_als_Hinweis_und_nichts_wird_gespeichert()
    {
        var api = new StubApi().On(ExcelRoute, new { title = "Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen." }, HttpStatusCode.BadRequest);
        var files = new FakeFileSaver();
        List<string> saved = [], failed = [];
        var export = Model(api, files, saved, failed);

        await export.ExportCommand.ExecuteAsync(export.Options[0]);

        Assert.Empty(files.Saved);
        Assert.Empty(saved);
        Assert.Equal("Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen.", Assert.Single(failed));
    }

    [Fact]
    public async Task Abbrechen_im_Speicherdialog_meldet_nichts()
    {
        var api = new StubApi().OnFile(ExcelRoute, Excel, "Kunden.xlsx");
        List<string> saved = [], failed = [];
        var export = Model(api, new FakeFileSaver { Cancels = true }, saved, failed);

        await export.ExportCommand.ExecuteAsync(export.Options[0]);

        Assert.Empty(saved);
        Assert.Empty(failed);
    }

    [Fact]
    public async Task Der_Pfad_traegt_die_Filter_zum_Zeitpunkt_des_Klicks()
    {
        var search = "huber";
        var api = new StubApi().OnFile("GET /api/customers/export?format=xlsx&search=maier", Excel, "Kunden.xlsx");
        var export = new ExportViewModel(api.Client(), [ExportOption.For(ExportFormat.Xlsx, () => $"api/customers/export?format=xlsx&search={search}")]) { Files = new FakeFileSaver() };

        search = "maier";
        await export.ExportCommand.ExecuteAsync(export.Options[0]);

        Assert.Contains(api.Calls, c => c.Route == "GET /api/customers/export?format=xlsx&search=maier");
    }
}

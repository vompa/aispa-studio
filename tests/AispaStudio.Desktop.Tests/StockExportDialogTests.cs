using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Der Export des Lagers als Dialog (P7): Vorauswahl nach offenem Bereich, gesperrte Listen mit Grund aus der API, Format gemerkt, Zeitraum der Lagerbewegungen, alle fünf Listen in Excel und
/// CSV bis zur gespeicherten Datei, Toast „Gespeichert: …“ mit „Ordner öffnen“ und die Fehlerfälle (Abbruch des Speicherdialogs, Fehler der API, alter Fehler beim neuen Versuch).
/// Inhalt und Format der Dateien prüfen die API-Tests.
/// </summary>
public sealed class StockExportDialogTests
{
    private static readonly byte[] File = [1, 2, 3];

    private static readonly string[] TooLarge = ["Die Datei ist zu groß."];

    private static readonly string[] Lists = ["levels", "count", "movements", "orders", "expiry"];

    internal sealed class PathSaver : IFileSaver, IReportsSavedPath
    {
        public bool Cancels { get; init; }

        public string? LastSavedPath { get; private set; }

        public List<string> Saved { get; } = [];

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            LastSavedPath = null;
            if (Cancels)
            {
                return Task.FromResult(false);
            }

            Saved.Add(suggestedName);
            LastSavedPath = $@"C:\Export\{suggestedName}";
            return Task.FromResult(true);
        }
    }

    internal sealed class FakeFolders : IFolderOpener
    {
        public List<string> Revealed { get; } = [];

        public void Reveal(string path) => Revealed.Add(path);
    }

    internal static StockExportAvailabilityDto All() => new([.. Lists.Select(l => new StockExportListAvailabilityDto(l, true))]);

    internal static StockExportAvailabilityDto WithoutCount() =>
        new([.. Lists.Select(l => l == "count" ? new StockExportListAvailabilityDto(l, false, "Noch keine Zählung an diesem Standort.") : new StockExportListAvailabilityDto(l, true))]);

    internal static string AvailabilityRoute => $"GET {StockSample.Route("/export/availability")}";

    private sealed record Setup(StockViewModel Stock, FakeDialogs Dialogs, PathSaver Files, FakeFolders Folders, ISettingsStore Settings, StubApi Api);

    private static async Task<Setup> OpenAsync(StubApi api, CurrentUser? user = null, ISettingsStore? settings = null, bool cancels = false)
    {
        settings ??= new MemorySettingsStore();
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new StockSample.FixedTime(), settings: settings);
        await stock.EnsureLoadedAsync();
        var dialogs = new FakeDialogs();
        var files = new PathSaver { Cancels = cancels };
        var folders = new FakeFolders();
        stock.Dialogs = dialogs;
        stock.Files = files;
        stock.FolderOpener = folders;
        return new Setup(stock, dialogs, files, folders, settings, api);
    }

    private static StubApi Api(StockExportAvailabilityDto? availability = null) => StockSample.Api().On(AvailabilityRoute, availability ?? All());

    /// <summary>Klickt „Exportieren“ und wartet, bis der Dialog offen ist (die Verfügbarkeit kommt vorher von der API); <c>Running</c> endet mit dem Export.</summary>
    private static async Task<(Task Running, StockExportDialogViewModel Dialog)> StartAsync(Setup setup)
    {
        var running = setup.Stock.OpenExportCommand.ExecuteAsync(null);
        for (var i = 0; i < 200 && setup.Dialogs.Open is null && !running.IsCompleted; i++)
        {
            await Task.Delay(5);
        }

        return (running, Assert.IsType<StockExportDialogViewModel>(setup.Dialogs.Open));
    }

    private static async Task<StockExportDialogViewModel> ShowAsync(Setup setup) => (await StartAsync(setup)).Dialog;

    private static string FileRoute(string list, string format, string range = "") => $"GET {StockSample.Route($"/export?list={list}{range}&format={format}")}";

    [Fact]
    public async Task Nur_die_Leitung_hat_den_Knopf_Exportieren()
    {
        var owner = await OpenAsync(Api());
        var general = await OpenAsync(Api(), TestData.Owner(StaffRole.GeneralManager));
        var manager = await OpenAsync(Api(), TestData.Manager());

        Assert.True(owner.Stock.CanExport);
        Assert.True(general.Stock.CanExport);
        Assert.False(manager.Stock.CanExport);
    }

    [Fact]
    public async Task Dialog_zeigt_fuenf_Inhalte_Excel_als_Standard_und_die_letzten_30_Tage()
    {
        var setup = await OpenAsync(Api());

        var dialog = await ShowAsync(setup);

        Assert.Equal(["Bestand je Lagerort", "Lagerbewegungen", "Bestellungen", "Ablaufliste", "Inventur"], dialog.Choices.Select(c => c.Title));
        Assert.All(dialog.Choices, c => Assert.True(c.IsAvailable && !string.IsNullOrEmpty(c.Description)));
        Assert.Equal((new DateOnly(2027, 1, 30), new DateOnly(2027, 3, 1), true, "levels", false), (dialog.From, dialog.Until, dialog.IsExcel, dialog.Selected, dialog.ShowRange));
        Assert.False(dialog.IsDestructive);
        Assert.Equal(StockExportDialogViewModel.DialogTitle, dialog.Title);
    }

    [Theory]
    [InlineData(StockTab.Overview, "levels")]
    [InlineData(StockTab.Stock, "levels")]
    [InlineData(StockTab.Orders, "orders")]
    [InlineData(StockTab.Count, "count")]
    public async Task Vorauswahl_folgt_dem_offenen_Bereich(StockTab tab, string expected)
    {
        var setup = await OpenAsync(Api());
        setup.Stock.SetTab(tab);

        var dialog = await ShowAsync(setup);

        Assert.Equal(expected, dialog.Selected);
        Assert.Equal(expected, Assert.Single(dialog.Choices, c => c.IsSelected).List);
    }

    [Fact]
    public async Task Gesperrte_Inventur_nennt_den_Grund_und_wird_nicht_vorgewaehlt()
    {
        var setup = await OpenAsync(Api(WithoutCount()));
        setup.Stock.SetTab(StockTab.Count);

        var dialog = await ShowAsync(setup);

        var count = Assert.Single(dialog.Choices, c => c.List == "count");
        Assert.False(count.IsAvailable);
        Assert.True(count.HasReason);
        Assert.Equal("Noch keine Zählung an diesem Standort.", count.Reason);
        Assert.Contains("gesperrt", count.AutomationName, StringComparison.Ordinal);
        Assert.Equal("levels", dialog.Selected);
    }

    [Fact]
    public async Task Ist_nichts_exportierbar_bleibt_Exportieren_gesperrt()
    {
        var none = new StockExportAvailabilityDto([.. Lists.Select(l => new StockExportListAvailabilityDto(l, false, "Noch nichts da."))]);
        var setup = await OpenAsync(Api(none));

        var dialog = await ShowAsync(setup);

        Assert.Null(dialog.Selected);
        Assert.False(dialog.ExportCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("levels", "xlsx")]
    [InlineData("levels", "csv")]
    [InlineData("count", "xlsx")]
    [InlineData("count", "csv")]
    [InlineData("movements", "xlsx")]
    [InlineData("movements", "csv")]
    [InlineData("orders", "xlsx")]
    [InlineData("orders", "csv")]
    [InlineData("expiry", "xlsx")]
    [InlineData("expiry", "csv")]
    public async Task Jede_Liste_in_jedem_Format_holt_die_Datei_und_meldet_Gespeichert_mit_Ordner(string list, string format)
    {
        var range = list == "movements" ? "&from=2027-01-30&until=2027-03-01" : "";
        var name = $"{list}_2027-03-01.{format}";
        var setup = await OpenAsync(Api().OnFile(FileRoute(list, format, range), File, name));

        var (running, dialog) = await StartAsync(setup);
        Assert.Single(dialog.Choices, c => c.List == list).IsSelected = true;
        Assert.Equal(list, dialog.Selected);
        if (format == "csv")
        {
            dialog.ChooseCsvCommand.Execute(null);
        }

        dialog.ExportCommand.Execute(null);
        await running;

        Assert.Equal(name, Assert.Single(setup.Files.Saved));
        Assert.Equal($"Gespeichert: {name}", setup.Stock.Toast);
        Assert.True(setup.Stock.CanOpenSavedFolder);
        Assert.Null(setup.Stock.ExportError);

        setup.Stock.OpenSavedFolderCommand.Execute(null);
        Assert.Equal($@"C:\Export\{name}", Assert.Single(setup.Folders.Revealed));
        Assert.Null(setup.Stock.Toast);
    }

    [Fact]
    public async Task Zeitraum_der_Bewegungen_wird_geprueft_und_nur_dort_gezeigt()
    {
        var setup = await OpenAsync(Api().OnFile(FileRoute("movements", "xlsx", "&from=2027-02-01&until=2027-02-28"), File, "Lagerbewegungen_2027-02-01.xlsx"));

        var (running, dialog) = await StartAsync(setup);
        Assert.False(dialog.ShowRange);
        Assert.Single(dialog.Choices, c => c.List == "movements").IsSelected = true;
        Assert.True(dialog.ShowRange);

        dialog.From = new DateOnly(2027, 3, 5);
        dialog.ExportCommand.Execute(null);
        Assert.Equal(AppointmentListDialogViewModel.EndBeforeStart, dialog.Error);
        Assert.Same(dialog, setup.Dialogs.Open);

        dialog.From = new DateOnly(2027, 2, 1);
        dialog.Until = new DateOnly(2027, 2, 28);
        Assert.Null(dialog.Error);
        dialog.ExportCommand.Execute(null);
        await running;

        Assert.Equal("Lagerbewegungen_2027-02-01.xlsx", Assert.Single(setup.Files.Saved));
    }

    [Fact]
    public async Task Das_zuletzt_gewaehlte_Format_bleibt_gemerkt()
    {
        var settings = new MemorySettingsStore();
        var first = await OpenAsync(Api().OnFile(FileRoute("levels", "csv"), File, "Lagerbestand_2027-03-01.csv"), settings: settings);
        var (running, dialog) = await StartAsync(first);
        Assert.True(dialog.IsExcel);
        dialog.ChooseCsvCommand.Execute(null);
        dialog.ExportCommand.Execute(null);
        await running;

        var second = await OpenAsync(Api(), settings: settings);
        var again = await ShowAsync(second);

        Assert.Equal("csv", settings.Load().StockExportFormat);
        Assert.True(again.IsCsv);
        Assert.False(again.IsExcel);
    }

    [Fact]
    public async Task Abbruch_des_Dialogs_und_des_Speicherdialogs_melden_weder_Gespeichert_noch_Fehler()
    {
        var cancelDialog = await OpenAsync(Api());
        var (running, opened) = await StartAsync(cancelDialog);
        opened.CancelCommand.Execute(null);
        await running;

        var cancelSave = await OpenAsync(Api().OnFile(FileRoute("levels", "xlsx"), File, "Lagerbestand_2027-03-01.xlsx"), cancels: true);
        (running, opened) = await StartAsync(cancelSave);
        opened.ExportCommand.Execute(null);
        await running;

        Assert.DoesNotContain(cancelDialog.Api.Calls, c => c.Route.Contains("/export?", StringComparison.Ordinal));
        Assert.Null(cancelDialog.Stock.Toast);
        Assert.Empty(cancelSave.Files.Saved);
        Assert.Null(cancelSave.Stock.Toast);
        Assert.Null(cancelSave.Stock.ExportError);
        Assert.False(cancelSave.Stock.CanOpenSavedFolder);
    }

    [Fact]
    public async Task Fehler_der_API_steht_als_Exportfehler_nicht_als_Buchungsfehler_und_verschwindet_beim_neuen_Versuch()
    {
        var failing = Api().On(FileRoute("levels", "xlsx"), new { errors = new { list = TooLarge } }, HttpStatusCode.BadRequest);
        var setup = await OpenAsync(failing);

        var (running, dialog) = await StartAsync(setup);
        dialog.ExportCommand.Execute(null);
        await running;

        Assert.False(string.IsNullOrEmpty(setup.Stock.ExportError));
        Assert.True(setup.Stock.HasExportError);
        Assert.Null(setup.Stock.Error);
        Assert.Null(setup.Stock.Toast);
        Assert.Equal("Export nicht erstellt", StockViewModel.ExportErrorTitle);

        // Neuer Versuch: der alte Fehler ist weg, sobald der Dialog aufgeht; ein Abbruch lässt ihn nicht zurückkommen.
        (running, dialog) = await StartAsync(setup);
        Assert.Null(setup.Stock.ExportError);
        dialog.CancelCommand.Execute(null);
        await running;
        Assert.Null(setup.Stock.ExportError);
    }

    [Fact]
    public async Task Fehlt_die_Verfuegbarkeit_steht_ein_Exportfehler_und_kein_Dialog()
    {
        var setup = await OpenAsync(StockSample.Api().On(AvailabilityRoute, null, HttpStatusCode.InternalServerError));

        await setup.Stock.OpenExportCommand.ExecuteAsync(null);

        Assert.Empty(setup.Dialogs.Shown);
        Assert.False(string.IsNullOrEmpty(setup.Stock.ExportError));
    }

    [Fact]
    public async Task Toast_hat_eine_Aktion_und_verschwindet_nach_der_Zeit()
    {
        var setup = await OpenAsync(Api().OnFile(FileRoute("levels", "xlsx"), File, "Lagerbestand_2027-03-01.xlsx"));
        var (running, dialog) = await StartAsync(setup);
        dialog.ExportCommand.Execute(null);
        await running;

        Assert.True(setup.Stock.HasToast);
        Assert.True(setup.Stock.Toast!.Length <= 60, setup.Stock.Toast);
        Assert.False(setup.Stock.Toast.EndsWith('.'));
        setup.Stock.CloseToastCommand.Execute(null);
        Assert.False(setup.Stock.HasToast);
        Assert.False(setup.Stock.CanOpenSavedFolder);
    }
}

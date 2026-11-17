using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Das Lager exportieren (P7): Knopf in der Kopfzeile, Dialog mit fünf Inhalten, Format und Zeitraum in Hell und Dunkel, gesperrte Liste mit Grund, Toast mit „Ordner öffnen“, Handbuchbilder.</summary>
public sealed partial class ViewTests
{
    private static StubApi StockExportApi(StockExportAvailabilityDto availability) =>
        StockApi().On(StockExportDialogTests.AvailabilityRoute, availability)
            .OnFile($"GET {StockSample.Route("/export?list=levels&format=xlsx")}", [1, 2, 3], "Lagerbestand_2027-03-01.xlsx");

    private static async Task<StockExportDialogViewModel> OpenExportDialog(Window window, MainViewModel main, StockViewModel stock, string? shot = null, string theme = "hell")
    {
        stock.Files = new StockExportDialogTests.PathSaver();
        stock.FolderOpener = new StockExportDialogTests.FakeFolders();
        // Per Befehl statt per Klick: nach einem Klick bleibt der Tooltip des Knopfes im Bild.
        Assert.Same(stock.OpenExportCommand, Named<Button>(window, "StockExportButton").Command);
        _ = stock.OpenExportCommand.ExecuteAsync(null);
        await Pump(Settling);
        await Pump(Settling);
        if (shot is not null)
        {
            SaveShot(window, shot, theme);
        }

        return Assert.IsType<StockExportDialogViewModel>(main.DialogHost.Current);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_exportieren_Dialog_mit_fuenf_Inhalten_und_Format(string theme) => Render(theme, "lager-exportieren-seite", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        Assert.True(Named<Button>(window, "StockExportButton").IsEffectivelyVisible);
        var dialog = await OpenExportDialog(window, main, stock, "lager-exportieren-dialog", theme);

        var card = Dialog(window);
        Assert.Equal(420, card.Bounds.Width, 0.5);
        Assert.Equal("Lager exportieren", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle").Text);
        var radios = card.GetVisualDescendants().OfType<RadioButton>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.Equal(5, radios.Count);
        Assert.Single(radios, r => r.IsChecked == true);
        Assert.Equal("Excel", Named<TabButton>(window, "ExportExcelButton").Content);
        Assert.True(Named<TabButton>(window, "ExportExcelButton").IsSelected);
        Assert.False(card.GetVisualDescendants().OfType<DateField>().Any(f => f.IsEffectivelyVisible), "Zeitraum nur bei den Lagerbewegungen");
        Assert.Equal("Exportieren", DialogButton(window, "ConfirmButton").Content);
        dialog.Choices.Single(c => c.List == StockExportDialogViewModel.Movements).IsSelected = true;
        await Pump(Settling);
        Assert.Equal(["ExportFromField", "ExportUntilField"], card.GetVisualDescendants().OfType<DateField>().Where(f => f.IsEffectivelyVisible).Select(f => f.Name));
        dialog.CancelCommand.Execute(null);
        return shell.AdminPage;
    }, StockExportApi(StockExportDialogTests.All()));

    [Fact]
    public Task Lager_exportieren_Gesperrtes_nennt_den_Grund() => Render("hell", "lager-exportieren-gesperrt-seite", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        var dialog = await OpenExportDialog(window, main, stock, "lager-exportieren-gesperrt");

        var card = Dialog(window);
        var texts = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("Noch keine Zählung an diesem Standort.", texts);
        var count = card.GetVisualDescendants().OfType<RadioButton>().Single(r => Avalonia.Automation.AutomationProperties.GetName(r)!.StartsWith("Inventur", StringComparison.Ordinal));
        Assert.False(count.IsEnabled);
        dialog.CancelCommand.Execute(null);
        return shell.AdminPage;
    }, StockExportApi(StockExportDialogTests.WithoutCount()));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_exportieren_meldet_Gespeichert_mit_Ordner_oeffnen(string theme) => Render(theme, "lager-exportiert-seite", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        var dialog = await OpenExportDialog(window, main, stock);
        dialog.ExportCommand.Execute(null);
        await Pump(Settling);
        await Pump(Settling);

        var toast = Named<Border>(window, "StockToast");
        Assert.True(toast.IsEffectivelyVisible);
        Assert.Equal("Gespeichert: Lagerbestand_2027-03-01.xlsx", stock.Toast);
        Assert.Equal("Ordner öffnen", Named<Button>(window, "StockToastFolder").Content);
        Assert.True(Named<Button>(window, "StockToastFolder").IsEffectivelyVisible);
        SaveShot(window, "lager-exportiert", theme);
        return shell.AdminPage;
    }, StockExportApi(StockExportDialogTests.All()));
}

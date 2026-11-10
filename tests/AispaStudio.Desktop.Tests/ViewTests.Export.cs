using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Knopf „Exportieren“ (Export/Import, Aufgabe A): Knopf und Menü in Hell und Dunkel, Tastatur, gesperrt während des Ladens, ohne Modell unsichtbar.</summary>
public sealed partial class ViewTests
{
    private static StackPanel ExportStrip(ExportViewModel? model)
    {
        var button = new ExportButton { Name = "Export", DataContext = model };
        return new StackPanel { Margin = new Thickness(40), Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { new Button { Content = "Davor" }, button } };
    }

    private static ExportViewModel ExportModel(StubApi api) => new(api.Client(),
        [ExportOption.For(ExportFormat.Xlsx, () => "api/customers/export?format=xlsx"), ExportOption.For(ExportFormat.Csv, () => "api/customers/export?format=csv"), ExportOption.For(ExportFormat.Vcf, () => "api/customers/export?format=vcf")]);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Export_Knopf_oeffnet_per_Enter_das_Menue_der_Formate(string theme) => InWindow(theme, () => ExportStrip(ExportModel(Api())), 600, 400, async window =>
    {
        var button = window.GetVisualDescendants().OfType<ExportButton>().Single();
        Assert.True(button.IsVisible);
        Assert.Equal("Exportieren", AutomationProperties.GetName(button));
        Assert.Equal(ExportButton.Hint, ToolTip.GetTip(button));
        Assert.Contains(button.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Exportieren");
        Assert.Single(button.GetVisualDescendants().OfType<Icon>());
        Assert.InRange(button.Bounds.Height, 34, 34.5);

        button.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.True(button.Flyout!.IsOpen);

        var items = MenuPresenter(window).GetVisualDescendants().OfType<MenuItem>().ToList();
        Assert.Equal(["Excel", "CSV", "vCard"], items.Select(i => i.Header?.ToString()));
        Assert.All(items, i => Assert.True(i.Bounds.Height >= 32, $"{i.Header}: {i.Bounds.Height}"));
        var text = items[0].GetVisualDescendants().OfType<TextBlock>().First();
        Assert.True(Contrast(ColorOf(text.Foreground), ColorOf(window, "SurfaceBrush")) >= 4.5);

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(button.Flyout.IsOpen);
        Assert.True(button.IsFocused);
    }, "export-knopf");

    [Fact]
    public Task Export_Knopf_ist_ohne_Modell_unsichtbar_und_waehrend_des_Ladens_gesperrt() => InWindow("hell", () => ExportStrip(null), 600, 400, async window =>
    {
        var button = window.GetVisualDescendants().OfType<ExportButton>().Single();
        Assert.False(button.IsVisible);

        var gate = new TaskCompletionSource();
        var api = Api().OnFile("GET /api/customers/export?format=xlsx", [1, 2, 3], "Kunden.xlsx").Hold("GET /api/customers/export?format=xlsx", gate.Task);
        var model = ExportModel(api);
        model.Files = new FakeFileSaver();
        button.DataContext = model;
        await Pump();
        Assert.True(button.IsVisible);

        var running = model.ExportCommand.ExecuteAsync(model.Options[0]);
        await Pump();
        Assert.False(button.IsEnabled);
        Assert.Contains(button.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == ExportButton.BusyLabel);

        gate.SetResult();
        await running;
        await Pump();
        Assert.True(button.IsEnabled);
        Assert.Contains(button.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == ExportButton.Label);
    });
}

using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Startauswahl auf dem Bildschirm (ADR 0104): Hauptknopf mit Fokus, drei Nebenwege in Quelltextreihenfolge, jeder Knopf mit Automationsnamen. Hell und dunkel; mit <c>AISPA_SCREENSHOTS</c> entstehen die Bilder.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Startauswahl_zeigt_Hauptknopf_mit_Fokus_und_drei_Nebenwege(string theme) => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
        var api = Api();
        api.Down = true;
        var main = new MainViewModel(api.Client(), time: new StoppedTime())
        {
            StartupScreenDelay = TimeSpan.FromMilliseconds(300),
            StartChoice = new StartChoiceViewModel(new MemorySettingsStore(), _ => { }),
        };
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        await main.StartAsync();
        await Pump(Settling);

        var screen = window.GetVisualDescendants().OfType<StartupScreen>().Single();
        Assert.True(screen.GetVisualDescendants().OfType<Border>().First(b => b.Name == "StartChoiceCard").IsEffectivelyVisible);
        Assert.False(screen.GetVisualDescendants().OfType<Border>().First(b => b.Name == "OfflineCard").IsEffectivelyVisible);

        var buttons = screen.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Choice", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(["ChoiceSetUp", "ChoiceDemo", "ChoiceRestore", "ChoiceConnect"], buttons.Select(b => b.Name));
        Assert.All(buttons, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))));
        Assert.True(buttons[0].IsDefault);
        Assert.Same(buttons[0], window.FocusManager?.GetFocusedElement());

        SaveFrame(window, "startauswahl", theme);

        // „Mit einem vorhandenen Spa verbinden“ blendet die Auswahl aus; der Fokus geht auf „Erneut versuchen“.
        main.StartChoice!.ConnectToExistingCommand.Execute(null);
        await Pump(Settling);
        Assert.Same(screen.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Retry"), window.FocusManager?.GetFocusedElement());
        window.Close();
        return true;
    }, CancellationToken.None);
}

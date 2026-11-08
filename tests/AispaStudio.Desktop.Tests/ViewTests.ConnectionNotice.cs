using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Verbindungsfehler (Runde R02, Slice 34: W19): der Zustand „Keine Verbindung zum Server“ wird dem Screenreader sofort angesagt.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Verbindungsfehler_Statustext_wird_dem_Screenreader_sofort_angesagt() => Session.Dispatch(async () =>
    {
        // Ein Stub statt einer geschlossenen Adresse (siehe MainViewModelTests.Meldet_wenn_Server_nicht_erreichbar).
        var main = new MainViewModel(new StubApi { Down = true }.Client()) { ProbeWindow = TimeSpan.Zero };
        await main.CheckStatusCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        await Pump(Settling);

        var status = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible && t.Text == main.StatusText);
        var live = AutomationProperties.GetLiveSetting(status);
        window.Close();

        Assert.False(main.IsConnected);
        Assert.Equal(AutomationLiveSetting.Assertive, live);
        return true;
    }, CancellationToken.None);
}

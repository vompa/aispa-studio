using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Meldungen und Primärknöpfe in Sicherung, Abgleich und Geräten (Runde R01, Slice 04: W10, W11, W13, W16, W17).</summary>
public sealed partial class ViewTests
{
    private static int VisiblePrimaries(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("primary") && b.IsEffectivelyVisible);

    [Fact]
    public Task InfoBar_zeigt_Detail_unter_dem_Text_und_ohne_Detail_nichts() => Session.Dispatch(async () =>
    {
        var code = new TextBlock { Text = "K7QM-2XRT" };
        var withDetail = new InfoBar { Severity = InfoBarSeverity.Warning, Message = "Notieren", Detail = code };
        var without = new InfoBar { Severity = InfoBarSeverity.Warning, Message = "Ohne" };
        var window = new Window { Width = 600, Height = 300, Content = new StackPanel { Children = { withDetail, without } } };
        window.Show();
        await Pump();

        Assert.True(code.IsEffectivelyVisible);
        Assert.Contains(code, withDetail.GetVisualDescendants());
        Assert.All(without.GetVisualDescendants().OfType<ContentControl>(), c => Assert.False(c.IsEffectivelyVisible));
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Sicherung_mit_Schluessel_Warnung_ist_InfoBar_Warnung_ohne_CritBrush_mit_einem_Primaerknopf() => Render("hell", "sicherung-meldung", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var backup = shell.Backup!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == backup);
        backup.NewRecoveryKeyCommand.Execute(null);
        await Pump();

        Assert.True(backup.HasNewCode);
        var bar = VisibleBars(window).Single(b => b.Title == "Wiederherstellungsschlüssel notieren");
        Assert.Equal(InfoBarSeverity.Warning, bar.Severity);
        Assert.Equal("Verstanden", bar.ActionText);
        var crit = ColorOf(bar, "CritBrush");
        Assert.All(bar.GetVisualDescendants().OfType<TextBlock>(), t => Assert.False(t.Foreground is ISolidColorBrush b && b.Color == crit));
        Assert.Equal(1, VisiblePrimaries(window));

        bar.ActionCommand!.Execute(null);
        Assert.False(backup.HasNewCode);
        return backup;
    }, local: new SampleLocal());

    [Fact]
    public Task Statuszeile_der_Sicherung_ist_nach_dem_Sichern_hoeflich_angesagt() => Render("hell", "sicherung-status", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var backup = shell.Backup!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == backup);
        backup.NewRecoveryKeyCommand.Execute(null);
        await Pump();

        var line = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == backup.Message && t.IsEffectivelyVisible);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(line));
        return backup;
    }, local: new SampleLocal());

    [Fact]
    public Task Abgleich_mit_angezeigtem_Schluesselcode_hat_eine_Warnung_und_einen_Primaerknopf() => Render("hell", "abgleich-schluesselcode", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var sync = shell.Sync!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == sync);
        sync.ConnectionInput = "Host=db.spa-wien.at;Database=aispadb";
        await sync.ConnectCommand.ExecuteAsync(null);
        await Pump();

        Assert.True(sync.HasShownCode);
        var bar = VisibleBars(window).Single(b => b.Title == "Schlüsselcode notieren");
        Assert.Equal(InfoBarSeverity.Warning, bar.Severity);
        Assert.Equal("Verstanden", bar.ActionText);
        Assert.Equal(1, VisiblePrimaries(window));
        return sync;
    }, local: new SampleLocal());

    [Fact]
    public Task Geraete_als_Leitung_ohne_Empfangsgeraet_haben_einen_Primaerknopf() => Render("hell", "geraete-primaer", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Devices);
        await shell.Devices.InitializeAsync();
        await Pump();

        Assert.True(shell.Devices.CanManageDevice);
        Assert.False(shell.Devices.IsSharedDevice);
        Assert.Equal(1, VisiblePrimaries(window));
        return shell.Devices;
    });

    private static readonly DisplayDeviceDto Breakroom = new(Guid.NewGuid(), "Pausenraum", SpaSample.LocationId, SpaSample.At(8), null);

    [Fact]
    public Task Geraete_Leitung_tippt_den_Code_der_Anzeige_ein_und_koppelt_sie_per_Klick()
    {
        var stub = Api().On($"GET /api/locations/{SpaSample.LocationId}/displays", new List<DisplayDeviceDto>()).On("POST /api/displays", Breakroom);
        return Render("hell", "geraete-anzeige-koppeln", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Devices);
        await shell.Devices.InitializeAsync();
        await Pump();

        Named<TextBox>(window, "DisplayCode").Text = "k7qm-2xrt";
        await Pump();
        await ClickIntoView(window, Named<Button>(window, "PairDisplay"));

        var body = stub.Calls.Single(c => c.Route == "POST /api/displays").Body!;
        Assert.Contains("\"code\":\"k7qm-2xrt\"", body, StringComparison.Ordinal);
        Assert.Contains($"\"locationId\":\"{SpaSample.LocationId}\"", body, StringComparison.Ordinal);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(),
            b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Anzeige Pausenraum widerrufen");
        Assert.Equal("", Named<TextBox>(window, "DisplayCode").Text);
        return shell.Devices;
    }, stub);
    }
}

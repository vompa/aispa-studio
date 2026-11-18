using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beispiel-Spa in der eigenen Datenbank (ADR 0100): Leiste über allen Seiten, Hinweis auf der Anmeldung, Sicherungsseite; hell und dunkel, ohne Bindungsfehler.</summary>
public sealed partial class ViewTests
{
    private static readonly SystemModules DemoModules = new(true, false, false, false, false, false, false, false, Demo: true);

    private static StubApi DemoApi() => Api().On("GET /api/system/modules", DemoModules);

    private static StubApi DemoLoginApi() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("0.9.0", "Production", DateTimeOffset.UtcNow, new StartupInfo(StartupPhase.Ready, "Bereit")))
        .On("GET /api/system/modules", DemoModules)
        .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false));

    private static InfoBar DemoBarOf(Window window) => Named<InfoBar>(window, "DemoBar");

    [Theory]
    [InlineData("hell", "beispiel-spa-leiste")]
    [InlineData("dunkel", "beispiel-spa-leiste")]
    public Task Leiste_Sie_sehen_Beispieldaten_steht_ueber_dem_Kalender_und_der_Knopf_loescht_nach_Rueckfrage(string theme, string name) =>
        Render(theme, name, async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await Pump();

            var bar = DemoBarOf(window);
            Assert.True(bar.IsEffectivelyVisible);
            Assert.Equal(InfoBarSeverity.Info, bar.Severity);
            Assert.Equal("Sie sehen Beispieldaten.", bar.Message);
            Assert.Equal("Beispieldaten löschen …", bar.ActionText);
            Assert.Same(shell.DeleteDemoCommand, bar.ActionCommand);
            var page = window.GetVisualDescendants().OfType<ContentControl>().First(c => c.Name == "PageHost");
            Assert.True(bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y <= page.TranslatePoint(new Point(0, 0), window)!.Value.Y,
                "Die Leiste steht über der Seite");

            // Über allen Seiten: auch auf der Sicherung und in der Verwaltung bleibt sie.
            shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup);
            await Pump();
            Assert.True(DemoBarOf(window).IsEffectivelyVisible);
            return shell.Backup!;
        }, DemoApi(), local: new SampleLocal());

    [Fact]
    public Task Leiste_fehlt_ohne_das_Recht_den_Standort_zu_verwalten() => Render("hell", "beispiel-spa-leiste-rezeption", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        Assert.False(shell.ShowDemoBar);
        // Ohne Recht steht keine sichtbare Leiste da; ob das Element im Baum liegt, hängt von der Vorlage ab.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<InfoBar>(), b => b.Name == "DemoBar" && b.IsEffectivelyVisible);
        return shell.Calendar;
    }, DemoApi().On("GET /api/auth/me", Reception()), local: new SampleLocal());

    [Fact]
    public Task Sicherung_im_Beispiel_Spa_sagt_dass_nicht_gesichert_wird_und_sperrt_Jetzt_sichern() => Render("hell", "beispiel-spa-sicherung", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup);
        await Pump();

        Assert.False(shell.Backup!.HasReminder);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Im Beispiel-Spa wird nicht gesichert." && t.IsEffectivelyVisible);
        var now = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Jetzt sichern");
        Assert.False(now.IsEffectivelyEnabled);
        Assert.Equal(BackupViewModel.DemoText, Avalonia.Automation.AutomationProperties.GetHelpText(now));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Speichern und sichern" && b.IsEffectivelyVisible);
        return shell.Backup;
    }, DemoApi(), local: new SampleLocal());

    [Theory]
    [InlineData("hell", "beispiel-spa-anmeldung")]
    [InlineData("dunkel", "beispiel-spa-anmeldung")]
    public Task Anmeldung_zeigt_im_Beispiel_Spa_den_Hinweis_mit_Konto_und_Passwort(string theme, string name) => Render(theme, name, async (window, main) =>
    {
        await main.StartAsync();
        var login = Assert.IsType<LoginViewModel>(main.Content);
        await Pump();

        Assert.True(login.HasDemoHint);
        var hint = window.GetVisualDescendants().OfType<InfoBar>().Single(b => b.Message == LoginViewModel.DemoHint);
        Assert.True(hint.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Info, hint.Severity);
        return login;
    }, DemoLoginApi());

    [Fact]
    public Task Beispiel_Spa_Leiste_Anmeldung_und_Sicherung_melden_keine_Bindungsfehler() => Render("hell", "beispiel-spa-bindungen", async (window, main) =>
    {
        var shell = await WithBindingLog(async log =>
        {
            await AssertNoBindingErrors(log, "Start und Kalender", async () => await SignedInCalendar(main));
            var shell = (ShellViewModel)main.Content!;
            await AssertNoBindingErrors(log, "Sicherung", () => { shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Zurück zum Kalender", () => { shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar); return Task.CompletedTask; });
            log.AssertClean();
            return shell;
        });
        return shell.Calendar;
    }, DemoApi(), local: new SampleLocal());

    [Fact]
    public Task Anmeldung_im_Beispiel_Spa_meldet_keine_Bindungsfehler() => Session.Dispatch(async () =>
    {
        await WithBindingLog(async log =>
        {
            var login = new LoginViewModel(DemoLoginApi().Client(), _ => Task.CompletedTask);
            var window = await Show(new Views.LoginView { DataContext = login });
            await AssertNoBindingErrors(log, "Anmeldung", () => login.LoadAsync());
            log.AssertClean();
            window.Close();
            return true;
        });
        return true;
    }, CancellationToken.None);
}

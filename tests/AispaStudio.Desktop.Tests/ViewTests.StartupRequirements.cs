using System.Net;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using InfoBar = AispaStudio.Desktop.Controls.InfoBar;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Startprüfung auf dem Bildschirm: die Karte „AISPA Studio kann auf diesem PC nicht starten“ mit „Beenden“, die Warnungen oben auf der Anmeldung und der Weg
/// auf der Karte „Keine Verbindung zum Server“. Hell und dunkel; mit <c>AISPA_SCREENSHOTS</c> entstehen die Bilder.
/// </summary>
public sealed partial class ViewTests
{
    private const long OneGb = 1024L * 1024 * 1024;

    private static async Task<(MainWindow Window, MainViewModel Main)> OpenWithFactsAsync(string theme, StubApi api, ISystemFacts facts, Func<Window, MainViewModel, Task>? before = null)
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var main = new MainViewModel(api.Client(), time: new StoppedTime(), facts: facts)
        {
            StartupScreenDelay = TimeSpan.FromMilliseconds(300),
            ProbeWindow = TimeSpan.Zero,
        };
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        if (before is not null)
        {
            await before(window, main);
        }

        await main.StartAsync();
        await Pump(Settling);
        return (window, main);
    }

    private static StubApi LoginApi() => Api().On("GET /api/auth/me", null, HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Startpruefung_Karte_nennt_alle_Funde_und_bietet_nur_Beenden(string theme) => Session.Dispatch(async () =>
    {
        var facts = new StartupRequirementsTests.Facts(Build: 19044, WebView2: false);
        var (window, main) = await OpenWithFactsAsync(theme, LoginApi(), facts);

        var card = Named<Border>(window, "BlockedCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(12, card.CornerRadius.TopLeft);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "AISPA Studio kann auf diesem PC nicht starten");
        var texts = card.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Windows ist zu alt", texts);
        Assert.Contains("Hilfe nicht verfügbar", texts);

        // Es gibt kein Weiter: weder „Erneut versuchen“ noch Verbindungskarten, nur „Beenden“ mit dem Fokus.
        var quit = Named<Button>(window, "Quit");
        Assert.True(quit.IsEffectivelyVisible);
        Assert.Equal("Beenden", quit.Content);
        Assert.False(Named<Border>(window, "OfflineCard").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "Retry").IsEffectivelyVisible);
        Assert.Same(quit, window.FocusManager?.GetFocusedElement());
        Assert.False(Named<ItemsControl>(window, "StartupHints").IsEffectivelyVisible);
        Assert.True(main.IsBlocked);

        SaveFrame(window, "start-nicht-moeglich", theme);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Startpruefung_Hinweise_stehen_als_schliessbare_Warnungen_ueber_der_Anmeldung(string theme) => Session.Dispatch(async () =>
    {
        var facts = new StartupRequirementsTests.Facts(Build: 19045, Memory: 2 * OneGb, WebView2: false);
        var (window, main) = await OpenWithFactsAsync(theme, LoginApi(), facts);

        Assert.IsType<LoginViewModel>(main.Content);
        var hints = Named<ItemsControl>(window, "StartupHints");
        Assert.True(hints.IsEffectivelyVisible);
        var bars = hints.GetVisualDescendants().OfType<InfoBar>().ToList();
        Assert.Equal(["Wenig Arbeitsspeicher", "Hilfe nicht verfügbar", "Windows 10"], bars.Select(b => b.Title));
        Assert.All(bars, bar => Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Warning, bar.Severity));
        Assert.All(bars, bar => Assert.Contains("warn", bar.Classes));
        Assert.False(Named<Border>(window, "BlockedCard").IsEffectivelyVisible);
        SaveFrame(window, "start-hinweise", theme);

        main.Hints[0].CloseCommand.Execute(null);
        await Pump();
        Assert.Equal(2, hints.GetVisualDescendants().OfType<InfoBar>().Count());

        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startpruefung_Hinweise_verschwinden_in_der_Arbeitsflaeche() => Session.Dispatch(async () =>
    {
        var facts = new StartupRequirementsTests.Facts(WebView2: false);
        var (window, main) = await OpenWithFactsAsync("hell", Api(), facts);

        Assert.IsType<ShellViewModel>(main.Content);
        Assert.Single(main.Hints);
        Assert.False(Named<ItemsControl>(window, "StartupHints").IsEffectivelyVisible);

        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Startpruefung_Offline_Karte_nennt_den_Weg_zu_einem_Server_auf_diesem_PC(string theme) => Session.Dispatch(async () =>
    {
        var api = Api();
        api.Down = true;
        var (window, _) = await OpenWithFactsAsync(theme, api, new StartupRequirementsTests.Facts(Docker: false));

        var advice = Named<TextBlock>(window, "OfflineAdvice");
        Assert.True(advice.IsEffectivelyVisible);
        Assert.StartsWith("Auf diesem PC läuft kein Server von AISPA Studio.", advice.Text, StringComparison.Ordinal);
        Assert.True(Named<Border>(window, "OfflineCard").IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "BlockedCard").IsEffectivelyVisible);
        SaveFrame(window, "start-keine-verbindung-weg", theme);
        window.Close();

        var withDocker = Api();
        withDocker.Down = true;
        (window, _) = await OpenWithFactsAsync(theme, withDocker, new StartupRequirementsTests.Facts(Docker: true));
        Assert.Contains("docker compose up -d", Named<TextBlock>(window, "OfflineAdvice").Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startpruefung_Offline_Karte_ohne_Satz_bei_fremder_Adresse() => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        var api = Api();
        api.Down = true;
        var main = new MainViewModel(new ApiClient(new Uri("https://spa.example.at/"), new MemorySessionStore(), api), time: new StoppedTime(),
            facts: new StartupRequirementsTests.Facts(Docker: false))
        {
            ProbeWindow = TimeSpan.Zero,
        };
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        await main.StartAsync();
        await Pump(Settling);

        Assert.True(main.IsOffline);
        Assert.False(Named<TextBlock>(window, "OfflineAdvice").IsEffectivelyVisible);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startpruefung_meldet_keine_Bindungsfehler() => Session.Dispatch(async () =>
    {
        await WithBindingLog(async log =>
        {
            await AssertNoBindingErrors(log, "Karte nicht startbar", async () =>
            {
                var (window, _) = await OpenWithFactsAsync("hell", LoginApi(), new StartupRequirementsTests.Facts(Is64Bit: false));
                window.Close();
            });
            await AssertNoBindingErrors(log, "Warnungen über der Anmeldung", async () =>
            {
                var (window, main) = await OpenWithFactsAsync("hell", LoginApi(), new StartupRequirementsTests.Facts(Build: 19045, WebView2: false));
                main.Hints[0].CloseCommand.Execute(null);
                await Pump();
                window.Close();
            });
            await AssertNoBindingErrors(log, "Keine Verbindung mit Weg", async () =>
            {
                var api = Api();
                api.Down = true;
                var (window, _) = await OpenWithFactsAsync("hell", api, new StartupRequirementsTests.Facts(Docker: false));
                window.Close();
            });
            log.AssertClean();
            return 0;
        });
        return true;
    }, CancellationToken.None);
}

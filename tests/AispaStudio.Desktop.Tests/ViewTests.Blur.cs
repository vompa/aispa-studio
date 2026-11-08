using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Weichzeichner nur hinter Overlays (Auftraggeber, 29.10.2026): Abmelde-Warnung am Empfangsgerät (ganzer Inhalt), Tastenhilfe im Kalender (Kalenderbereich),
/// statisch das große Logo der Anmeldung. Arbeitsflächen bleiben scharf.
/// </summary>
public sealed partial class ViewTests
{
    private static StubApi ReceptionDeviceApi() => Api().On("GET /api/auth/quick-switch", new QuickSwitchState(QuickSwitchTests.Device with { LocationId = SpaSample.LocationId }, QuickSwitchTests.People));

    /// <summary>Angemeldet am Empfangsgerät, 20 Sekunden vor dem Abmelden ohne Eingabe.</summary>
    private static async Task<CalendarViewModel> IdleWarning(MainViewModel main, IdleTime time)
    {
        var calendar = await SignedInCalendar(main);
        Assert.NotNull(main.Device);
        time.Now += TimeSpan.FromSeconds(4 * 60 + 40);
        await main.TickAsync();
        await Pump(Settling);
        Assert.True(main.IsIdleWarning);
        return calendar;
    }

    private static double BlurRadius(Visual visual) => visual.Effect is IBlurEffect blur ? blur.Radius : 0;

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Abmelde_Warnung_zeichnet_den_ganzen_Inhalt_weich_mit_Scrim_und_Karte_in_der_Mitte(string theme)
    {
        var time = new IdleTime { Now = SpaSample.At(10, 0) };
        return Render(theme, "abmelde-warnung", async (window, main) =>
        {
            var calendar = await IdleWarning(main, time);
            var content = Named<ContentControl>(window, "Main");
            Assert.Contains("blurred", content.Classes);
            Assert.Equal(14, BlurRadius(content));
            Assert.Contains(content.Transitions!, t => t is EffectTransition);

            var scrim = Named<Border>(window, "IdleScrim");
            Assert.True(scrim.IsEffectivelyVisible);
            Assert.Equal(ColorOf(window, "ScrimBrush"), ColorOf(scrim.Background));
            Assert.Equal((window.Bounds.Width, window.Bounds.Height), (scrim.Bounds.Width, scrim.Bounds.Height));

            // Die Warnung ist eine Karte in der Mitte, selbst scharf, mit „Weiter arbeiten“ und unverändertem Zähler.
            var card = Named<InfoBar>(window, "IdleCard");
            Assert.Equal(0, BlurRadius(card));
            Assert.Equal(window.Bounds.Width / 2, LeftOf(window, card) + (card.Bounds.Width / 2), 1.0);
            Assert.Equal(window.Bounds.Height / 2, TopOf(window, card) + (card.Bounds.Height / 2), 1.0);
            Assert.Equal("Weiter arbeiten", card.ActionText);
            Assert.Equal("Ohne Eingabe wird in 20 Sekunden abgemeldet.", card.Message);
            return calendar;
        }, ReceptionDeviceApi(), time: time);
    }

    [Fact]
    public Task Weiter_arbeiten_und_jede_Taste_heben_den_Weichzeichner_der_Abmelde_Warnung_auf()
    {
        var time = new IdleTime { Now = SpaSample.At(10, 0) };
        return Render("hell", "abmelde-warnung-weiter", async (window, main) =>
        {
            var calendar = await IdleWarning(main, time);
            var content = Named<ContentControl>(window, "Main");

            Named<InfoBar>(window, "IdleCard").ActionCommand!.Execute(null);
            await Pump(Settling);
            Assert.False(main.IsIdleWarning);
            Assert.DoesNotContain("blurred", content.Classes);
            Assert.Equal(0, BlurRadius(content));
            Assert.False(Named<Panel>(window, "IdleOverlay").IsVisible);

            // Wieder 20 Sekunden vor dem Abmelden: eine beliebige Taste genügt.
            time.Now += TimeSpan.FromSeconds(4 * 60 + 40);
            await main.TickAsync();
            await Pump();
            Assert.Contains("blurred", content.Classes);
            Press(window, PhysicalKey.ShiftLeft);
            await Pump(Settling);
            Assert.False(main.IsIdleWarning);
            Assert.DoesNotContain("blurred", content.Classes);
            return calendar;
        }, ReceptionDeviceApi(), time: time);
    }

    [Fact]
    public Task Ohne_Overlay_ist_nichts_weichgezeichnet() => Render("hell", "ohne-weichzeichner", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);
        Assert.DoesNotContain(window.GetVisualDescendants(), v => BlurRadius(v) > 0);
        Assert.False(Named<Border>(window, "IdleScrim").IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "KeyHelpScrim").IsEffectivelyVisible);
        return calendar;
    });

    [Fact]
    public Task Tastenhilfe_zeichnet_nur_den_Kalender_weich_Esc_und_Klick_auf_den_Scrim_machen_ihn_wieder_scharf() => Render("hell", "tastenhilfe-weich", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var body = Named<Grid>(window, "CalendarBody");
        var scrim = Named<Border>(window, "KeyHelpScrim");

        calendar.ShowKeyHelp = true;
        await Pump(Settling);
        Assert.Contains("blurred", body.Classes);
        Assert.Equal(14, BlurRadius(body));
        Assert.True(scrim.IsEffectivelyVisible);
        Assert.Equal(0, BlurRadius(Named<Border>(window, "KeyHelp")));
        // Kopfzeile und Fenster bleiben scharf: nur der Kalenderbereich hinter dem Overlay verschwimmt.
        Assert.Equal(0, BlurRadius(Named<StackPanel>(window, "CalendarHeader")));
        Assert.Equal(0, BlurRadius(Named<ContentControl>(window, "Main")));

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(calendar.ShowKeyHelp);
        Assert.DoesNotContain("blurred", body.Classes);
        Assert.Equal(0, BlurRadius(body));

        calendar.ShowKeyHelp = true;
        await Pump(Settling);
        var corner = scrim.TranslatePoint(new Point(8, 8), window)!.Value;
        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);
        await Pump(Settling);
        Assert.False(calendar.ShowKeyHelp);
        Assert.Equal(0, BlurRadius(body));
        return calendar;
    });

    [Fact]
    public Task Animationen_aus_nimmt_dem_Weichzeichner_den_Uebergang() => Render("hell", "weichzeichner-ohne-bewegung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var body = Named<Grid>(window, "CalendarBody");
        Assert.Contains(body.Transitions!, t => t is EffectTransition);

        window.Styles.AddRange(ReducedMotion.Overrides());
        await Pump();
        Assert.Empty(body.Transitions!);
        calendar.ShowKeyHelp = true;
        await Pump();
        Assert.Equal(14, BlurRadius(body));
        return calendar;
    });

    [Fact]
    public Task Anmeldung_zeichnet_nur_das_grosse_Logo_statisch_weich() => Render("hell", "anmeldung-logo", async (window, main) =>
    {
        var api = new StubApi().On("GET /api/system/status", new SystemStatus("0.9.0", "Production", DateTimeOffset.UtcNow));
        var signedOut = new MainViewModel(api.Client());
        window.DataContext = signedOut;
        await signedOut.StartAsync();
        await Pump(Settling);
        Assert.IsType<LoginViewModel>(signedOut.Content);

        var glow = Named<Icon>(window, "BrandGlow");
        Assert.Equal(40, BlurRadius(glow));
        Assert.Equal(0, BlurRadius(Named<Border>(window, "BrandTile")));
        Assert.DoesNotContain(window.GetVisualDescendants(), v => v != glow && BlurRadius(v) > 0);
        return signedOut;
    });
}

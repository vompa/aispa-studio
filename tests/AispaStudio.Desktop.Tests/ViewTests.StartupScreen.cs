using System.Net;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Shape = Avalonia.Controls.Shapes.Shape;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Startbildschirm (Auftraggeber, 27.10.2026, nach der HTML-Vorschau): Petrolfläche mit Logo, Name und Stand, bis die API bereit ist; danach wird die Fläche zur
/// Navigationsleiste bzw. zur Markenspalte der Anmeldung. Die Uhr steht (<see cref="StoppedTime"/>): 300 ms und 10 s vergehen nur per <c>Advance</c>.
/// </summary>
public sealed partial class ViewTests
{
    private sealed record Startup(MainWindow Window, MainViewModel Main, StubApi Stub, StoppedTime Time, Task Running)
    {
        public StartupScreen Screen => Window.GetVisualDescendants().OfType<StartupScreen>().Single();

        public T Part<T>(string name)
            where T : Control => Screen.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }

    /// <summary>Öffnet das Fenster mit einem Server, der noch nicht antwortet; <see cref="ReleaseAsync"/> lässt ihn antworten.</summary>
    private static async Task<Startup> StartSlowlyAsync(string theme = "hell", StubApi? stub = null, TimeSpan? probeWindow = null)
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
        var api = stub ?? Api();
        api.Down = true;
        var time = new StoppedTime();
        var main = new MainViewModel(api.Client(), time: time)
        {
            StartupScreenDelay = TimeSpan.FromMilliseconds(300),
            ProbeWindow = probeWindow ?? TimeSpan.FromMinutes(5),
        };
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();
        var running = main.StartAsync();
        await Pump();
        return new(window, main, api, time, running);
    }

    /// <summary>Der Server antwortet ab dem nächsten Versuch; wartet, bis der Start durch ist.</summary>
    private static async Task ReleaseAsync(Startup startup)
    {
        startup.Stub.Down = false;
        startup.Time.Advance(startup.Main.ProbeInterval);
        await PumpUntil(() => startup.Running.IsCompleted);
    }

    private static async Task PumpUntil(Func<bool> done, int rounds = 300)
    {
        for (var i = 0; i < rounds && !done(); i++)
        {
            await Pump(1);
        }

        Assert.True(done(), "Der erwartete Zustand trat nicht ein.");
    }

    /// <summary>Lage eines Elements im Fenster, mit seiner Render-Transformation (Kachel und Name werden verschoben und skaliert).</summary>
    private static Rect PlaceOf(Visual visual, Visual window)
    {
        var topLeft = visual.TranslatePoint(default, window)!.Value;
        var bottomRight = visual.TranslatePoint(new Point(visual.Bounds.Width, visual.Bounds.Height), window)!.Value;
        return new Rect(topLeft, bottomRight);
    }

    private static Control Brand(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name && c.FindAncestorOfType<StartupScreen>() is null);

    private static void AssertNear(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 0.5);
        Assert.Equal(expected.Y, actual.Y, 0.5);
        Assert.Equal(expected.Width, actual.Width, 0.5);
        Assert.Equal(expected.Height, actual.Height, 0.5);
    }

    [Fact]
    public Task Startbildschirm_erscheint_nicht_wenn_die_API_unter_300_ms_antwortet() => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var main = new MainViewModel(Api().Client(), time: new StoppedTime()) { StartupScreenDelay = TimeSpan.FromMilliseconds(300) };
        var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
        window.Show();

        await main.StartAsync();
        await Pump();

        Assert.False(main.IsStartupScreenShown);
        Assert.False(window.GetVisualDescendants().OfType<StartupScreen>().Single().IsVisible);
        Assert.Equal(1, window.GetVisualDescendants().OfType<ShellView>().Single().Opacity);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_zeigt_vor_300_ms_nur_die_Petrolflaeche_danach_Logo_Name_und_Stand() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync();

        // Kein heller Blitz: die Fläche steht sofort, Logo und Stand noch nicht.
        Assert.True(startup.Screen.IsVisible);
        Assert.True(startup.Part<Border>("Surface").IsEffectivelyVisible);
        Assert.False(startup.Part<Border>("Tile").IsEffectivelyVisible);

        startup.Time.Advance(TimeSpan.FromMilliseconds(299));
        await Pump();
        Assert.False(startup.Main.IsStartupScreenShown);

        startup.Time.Advance(TimeSpan.FromMilliseconds(1));
        await Pump();
        Assert.True(startup.Main.IsStartupScreenShown);
        Assert.True(startup.Part<Border>("Tile").IsEffectivelyVisible);
        Assert.Equal(96, startup.Part<Border>("Tile").Bounds.Width);
        Assert.Equal("AISPA Studio", startup.Part<TextBlock>("BrandName").Text);
        Assert.Equal("Server wird gestartet …", startup.Part<TextBlock>("StatusLine").Text);
        Assert.True(startup.Part<Border>("Bar").IsEffectivelyVisible);
        Assert.Equal(new Size(160, 4), startup.Part<Border>("Bar").Bounds.Size);

        // Nach dem Zeichnen sind beide Wellen ganz zu sehen.
        await Pump(60);
        Assert.All<Shape>([startup.Part<Shape>("Wave1"), startup.Part<Shape>("Wave2")], wave => Assert.Equal(0, wave.StrokeDashOffset));
        Assert.Equal(1, startup.Part<Shape>("Drop").Opacity);

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_ist_ein_benannter_Bereich_mit_angesagter_Statuszeile() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync();
        startup.Time.Advance(TimeSpan.FromMilliseconds(300));
        await Pump();

        Assert.Equal("AISPA Studio startet", AutomationProperties.GetName(startup.Screen));
        // Die Ansage hängt am TextBlock, dessen Text sich ändert (am Rahmen bliebe sie stumm).
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(startup.Part<TextBlock>("StatusLine")));
        // Fokus erst mit „Erneut versuchen“: beim Warten gibt es nichts zu bedienen.
        Assert.Null(startup.Window.FocusManager?.GetFocusedElement());

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_zeigt_nach_10_s_den_Hinweis_mit_aufklappbaren_technischen_Angaben() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync();
        startup.Time.Advance(TimeSpan.FromMilliseconds(300));
        await Pump();
        for (var i = 0; i < 4; i++)
        {
            // Die Versuche alle 2 s laufen mit derselben Uhr weiter.
            startup.Time.Advance(TimeSpan.FromSeconds(2));
            await Pump(2);
        }

        startup.Time.Advance(TimeSpan.FromMilliseconds(1600));
        await Pump();
        Assert.False(startup.Main.IsSlowStart);

        startup.Time.Advance(TimeSpan.FromMilliseconds(100));
        await Pump();
        Assert.True(startup.Main.IsSlowStart);
        var hint = startup.Screen.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Das dauert länger als üblich.");
        Assert.True(hint.IsEffectivelyVisible);

        var details = startup.Part<ToggleButton>("DetailsToggle");
        Assert.Equal("Technische Angaben", details.Content);
        Assert.False(startup.Part<Grid>("Details").IsEffectivelyVisible);
        details.IsChecked = true;
        await Pump();
        var shown = startup.Part<Grid>("Details").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["Adresse", "http://localhost/", "Phase", "Verbinden"], shown);

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_ohne_Verbindung_zeigt_die_Karte_auf_der_Petrolflaeche_mit_Fokus_auf_Erneut_versuchen() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync(probeWindow: TimeSpan.Zero);
        await PumpUntil(() => startup.Running.IsCompleted);
        await Pump();

        Assert.True(startup.Main.IsOffline);
        var card = startup.Part<Border>("OfflineCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(12, card.CornerRadius.TopLeft);
        Assert.True(startup.Part<Border>("Tile").IsEffectivelyVisible);
        Assert.False(startup.Part<StackPanel>("Status").IsEffectivelyVisible);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Keine Verbindung zum Server");
        var retry = startup.Part<Button>("Retry");
        Assert.Same(retry, startup.Window.FocusManager?.GetFocusedElement());

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_wird_mit_gemerkter_Anmeldung_zur_Navigationsleiste() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync();
        startup.Time.Advance(TimeSpan.FromMilliseconds(300));
        await Pump(60);
        (Rect Surface, Rect Tile)? handover = null;
        startup.Screen.HandedOver += (surface, tile) => handover = (surface, tile);

        await ReleaseAsync(startup);
        await PumpUntil(() => !startup.Screen.IsVisible);

        Assert.IsType<ShellViewModel>(startup.Main.Content);
        var rail = Brand(startup.Window, "BrandSurface");
        Assert.Equal(88, rail.Bounds.Width);
        Assert.NotNull(handover);
        AssertNear(PlaceOf(rail, startup.Window), handover.Value.Surface);
        AssertNear(PlaceOf(Brand(startup.Window, "BrandTile"), startup.Window), handover.Value.Tile);
        Assert.Equal(1, startup.Window.GetVisualDescendants().OfType<ShellView>().Single().Opacity);

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_wird_ohne_Anmeldung_zur_Markenspalte_der_Anmeldung() => Session.Dispatch(async () =>
    {
        var startup = await StartSlowlyAsync(stub: Api().On("GET /api/auth/me", null, HttpStatusCode.Unauthorized));
        startup.Time.Advance(TimeSpan.FromMilliseconds(300));
        await Pump(60);
        (Rect Surface, Rect Tile)? handover = null;
        var name = startup.Part<TextBlock>("BrandName");
        Rect? namePlace = null;
        startup.Screen.HandedOver += (surface, tile) =>
        {
            handover = (surface, tile);
            namePlace = PlaceOf(name, startup.Window);
        };

        await ReleaseAsync(startup);
        await PumpUntil(() => !startup.Screen.IsVisible);

        Assert.IsType<LoginViewModel>(startup.Main.Content);
        var column = Brand(startup.Window, "BrandSurface");
        Assert.Equal(1440 * 5 / 12.0, column.Bounds.Width, 0.5);
        Assert.NotNull(handover);
        AssertNear(PlaceOf(column, startup.Window), handover.Value.Surface);
        var tile = Brand(startup.Window, "BrandTile");
        Assert.Equal(60, tile.Bounds.Width);
        AssertNear(PlaceOf(tile, startup.Window), handover.Value.Tile);
        // Gleiche Schrift, gleiche Lage; der Rahmen des Ziels ist so breit wie die Spalte.
        Assert.Equal(PlaceOf(Brand(startup.Window, "BrandName"), startup.Window).TopLeft, namePlace!.Value.TopLeft);

        startup.Window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Startbildschirm_bei_Animationen_aus_ohne_Zeichnen_ohne_Balken_und_ohne_Uebergang() => Session.Dispatch(async () =>
    {
        var before = ReducedMotion.SystemSource;
        ReducedMotion.SystemSource = () => true;
        try
        {
            var startup = await StartSlowlyAsync();
            startup.Time.Advance(TimeSpan.FromMilliseconds(300));
            await Pump(1);

            // Logo sofort fertig, kein Balken.
            Assert.All<Shape>([startup.Part<Shape>("Wave1"), startup.Part<Shape>("Wave2")], wave => Assert.Null(wave.StrokeDashArray));
            Assert.Equal(1, startup.Part<Shape>("Drop").Opacity);
            Assert.Equal(1, startup.Part<StackPanel>("Status").Opacity);
            Assert.False(startup.Part<Border>("Bar").IsVisible);

            // Kein Warten auf das Logo und kein 400-ms-Übergang: die Leiste steht im nächsten Durchlauf.
            await ReleaseAsync(startup);
            await Pump(2);
            Assert.False(startup.Screen.IsVisible);
            Assert.Equal(1, startup.Window.GetVisualDescendants().OfType<ShellView>().Single().Opacity);

            startup.Window.Close();
        }
        finally
        {
            ReducedMotion.SystemSource = before;
        }

        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Startbildschirm_Bild_beim_Warten_und_ohne_Verbindung(string theme) => Session.Dispatch(async () =>
    {
        var waiting = await StartSlowlyAsync(theme);
        waiting.Time.Advance(TimeSpan.FromSeconds(10));
        await Pump(60);
        waiting.Part<ToggleButton>("DetailsToggle").IsChecked = true;
        await Pump(Settling);
        SaveFrame(waiting.Window, "start-warten", theme);
        waiting.Window.Close();

        var offline = await StartSlowlyAsync(theme, probeWindow: TimeSpan.Zero);
        await PumpUntil(() => offline.Running.IsCompleted);
        await Pump(60);
        SaveFrame(offline.Window, "start-keine-verbindung", theme);
        offline.Window.Close();
        return true;
    }, CancellationToken.None);

    private static void SaveFrame(Window window, string name, string theme)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            frame.Save(Path.Combine(folder, $"{name}{(theme == "hell" ? "" : "-" + theme)}.png"), PngBitmapEncoderOptions.Default);
        }
    }
}

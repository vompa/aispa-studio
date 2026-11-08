using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F14: Kontrollkästchen, Optionsfelder (ein Tab-Stopp, Pfeile wählen) und Schalter nach Design-Brief 5.2 und 5.23.</summary>
public sealed partial class ViewTests
{
    private static Border Part(Control owner, string name) => owner.GetVisualDescendants().OfType<Border>().Single(b => b.Name == name);

    private static Control RadioGroupPanel() => new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 4,
        Children =
        {
            new Button { Content = "Davor" },
            new RadioButton { GroupName = "Beispiel", Content = "Eins" },
            new RadioButton { GroupName = "Beispiel", Content = "Zwei" },
            new RadioButton { GroupName = "Beispiel", Content = "Drei" },
            new Button { Content = "Danach" },
        },
    };

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kontrollkaestchen_und_Optionsfeld_haben_den_Rand_fg_3_mit_3_zu_1_und_Hover_fg_2(string theme) =>
        InWindow(theme, () => new StackPanel
        {
            Margin = new Thickness(24),
            Children = { new CheckBox { Content = "Gäste per Mail informieren" }, new CheckBox { Content = "Aktiv", IsChecked = true }, new RadioButton { Content = "Anzahl", GroupName = "g" } },
        }, 360, 240, async window =>
        {
            var panel = (StackPanel)window.Content!;
            var unchecked1 = (CheckBox)panel.Children[0];
            var checked1 = (CheckBox)panel.Children[1];
            var radio = (RadioButton)panel.Children[2];
            var box = Part(unchecked1, "NormalRectangle");
            var ring = radio.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "OuterEllipse");

            // Rest: Rand fg-3, mindestens 3:1 gegen die Fläche (Brief 2.6).
            Assert.Equal(ColorOf(window, "Fg3Brush"), ColorOf(box.BorderBrush));
            Assert.Equal(ColorOf(window, "Fg3Brush"), ColorOf(ring.Stroke));
            Assert.True(Contrast(ColorOf(window, "Fg3Brush"), ColorOf(window, "SurfaceBrush")) >= 3.0);
            Assert.True(Contrast(ColorOf(window, "Fg3Brush"), ColorOf(window, "BgBrush")) >= 3.0);
            Assert.Equal(1, box.BorderThickness.Left);
            Assert.True(unchecked1.Bounds.Height >= 34 && radio.Bounds.Height >= 34, "Die Zeile ist 34 hoch");

            // Gewählt: Akzentfläche.
            Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(Part(checked1, "NormalRectangle").Background));

            // Hover: Rand fg-2.
            var at = unchecked1.TranslatePoint(new Point(unchecked1.Bounds.Width - 4, unchecked1.Bounds.Height / 2), window)!.Value;
            window.MouseMove(at);
            await Pump(Settling);
            Assert.Equal(ColorOf(window, "Fg2Brush"), ColorOf(box.BorderBrush));
        }, "kaestchen-optionsfeld");

    [Fact]
    public Task Optionsgruppe_ist_ein_Tab_Stopp_Pfeile_bewegen_und_waehlen_Strg_Pfeil_nur_den_Fokus_kein_Umlauf() => InWindow("hell", RadioGroupPanel, 400, 300, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var before = (Button)panel.Children[0];
        var radios = panel.Children.OfType<RadioButton>().ToList();
        var after = (Button)panel.Children[4];

        // Ohne Auswahl ist nur die erste Option ein Tab-Stopp.
        Assert.Equal([true, false, false], radios.Select(r => r.IsTabStop));
        before.Focus(NavigationMethod.Pointer);
        Press(window, PhysicalKey.Tab);
        await Pump();
        Assert.Same(radios[0], Focused(window));
        Assert.All(radios, r => Assert.NotEqual(true, r.IsChecked));

        // Pfeil ab: Fokus und Auswahl.
        Press(window, PhysicalKey.ArrowDown);
        await Pump();
        Assert.Same(radios[1], Focused(window));
        Assert.True(radios[1].IsChecked);
        Assert.Equal([false, true, false], radios.Select(r => r.IsTabStop));

        // Strg+Pfeil bewegt nur den Fokus; die Leertaste wählt.
        Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Control);
        await Pump();
        Assert.Same(radios[2], Focused(window));
        Assert.True(radios[1].IsChecked);
        Assert.NotEqual(true, radios[2].IsChecked);
        Press(window, PhysicalKey.Space);
        await Pump();
        Assert.True(radios[2].IsChecked);

        // Kein Umlauf am Ende: der Fokus bleibt auf der letzten Option.
        Press(window, PhysicalKey.ArrowDown);
        await Pump();
        Assert.Same(radios[2], Focused(window));
        Assert.True(radios[2].IsChecked);
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(radios[2], Focused(window));

        // Pfeile nach oben wählen rückwärts; am Anfang bleibt es stehen.
        Press(window, PhysicalKey.ArrowUp);
        Press(window, PhysicalKey.ArrowUp);
        Press(window, PhysicalKey.ArrowUp);
        await Pump();
        Assert.Same(radios[0], Focused(window));
        Assert.True(radios[0].IsChecked);

        // Tab verlässt die Gruppe sofort (ein Tab-Stopp), Umschalt+Tab landet auf der gewählten Option.
        Press(window, PhysicalKey.Tab);
        await Pump();
        Assert.Same(after, Focused(window));
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump();
        Assert.Same(radios[0], Focused(window));
        _ = before;
    });

    [Fact]
    public Task Optionsgruppe_im_Programm_Serie_endet_nach_Anzahl_oder_bis_ist_ein_Tab_Stopp() => Render("hell", "optionsfeld-serie", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        calendar.Selected!.EditSeriesCommand.Execute(null);
        await Pump();

        var radios = window.GetVisualDescendants().OfType<RadioButton>().Where(r => r.GroupName == "SeriesEnd" && r.IsEffectivelyVisible).ToList();
        Assert.Equal(2, radios.Count);
        Assert.Equal(1, radios.Count(r => r.IsTabStop));
        Assert.True(radios[0].IsChecked == true ? radios[0].IsTabStop : radios[1].IsTabStop);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Schalter_hat_Spur_40_mal_20_Daumen_18_und_wechselt_in_200_ms(string theme) =>
        InWindow(theme, () => new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 8,
            Children = { new ToggleSwitch { Content = "Online-Buchung" }, new ToggleSwitch { Content = "Online-Buchung", IsChecked = true }, new ToggleSwitch { Content = "Gesperrt", IsEnabled = false } },
        }, 360, 240, async window =>
        {
            var panel = (StackPanel)window.Content!;
            var off = (ToggleSwitch)panel.Children[0];
            var on = (ToggleSwitch)panel.Children[1];
            var track = Part(off, "Track");
            var thumb = Part(off, "Thumb");
            Assert.Equal((40, 20), (track.Bounds.Width, track.Bounds.Height));
            Assert.Equal((18, 18), (thumb.Bounds.Width, thumb.Bounds.Height));
            Assert.Equal(new CornerRadius(999), track.CornerRadius);
            Assert.Equal(1, track.BorderThickness.Left);

            // Aus: Fläche surface, Rand und Daumen fg-3; an: Fläche und Rand accent, Daumen on-accent.
            Assert.Equal(ColorOf(window, "SurfaceBrush"), ColorOf(track.Background));
            Assert.Equal(ColorOf(window, "Fg3Brush"), ColorOf(track.BorderBrush));
            Assert.Equal(ColorOf(window, "Fg3Brush"), ColorOf(thumb.Background));
            var onTrack = Part(on, "Track");
            var onThumb = Part(on, "Thumb");
            Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(onTrack.Background));
            Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(onTrack.BorderBrush));
            Assert.Equal(ColorOf(window, "AccentFgBrush"), ColorOf(onThumb.Background));
            // Der Daumen steht an links, an rechts (20 px Weg), die Beschriftung rechts mit 8 px Abstand.
            // (der Rand von 1 px liegt davor)
            Assert.Equal(1, thumb.TranslatePoint(new Point(0, 0), track)!.Value.X, 0.5);
            Assert.Equal(21, onThumb.TranslatePoint(new Point(0, 0), onTrack)!.Value.X, 0.5);
            var label = off.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Single(c => c.Name == "PART_ContentPresenter");
            Assert.Equal(8, label.TranslatePoint(new Point(0, 0), track)!.Value.X - track.Bounds.Width, 0.5);
            Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "SurfaceBrush")) >= 3.0, "Spur an gegen Fläche 3:1");

            // 200 ms mit (0.33, 0, 0.67, 1) für den Daumen und die Spur.
            var move = Assert.Single(thumb.Transitions!.OfType<TransformOperationsTransition>());
            Assert.Equal(TimeSpan.FromMilliseconds(200), move.Duration);
            AssertSpline(move.Easing, 0.33, 0, 0.67, 1);
            Assert.All(track.Transitions!.OfType<BrushTransition>(), t => Assert.Equal(TimeSpan.FromMilliseconds(200), t.Duration));

            // Hover: Rand fg-2 (aus) bzw. Fläche accent-hover (an).
            var at = off.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseMove(at);
            await Pump(Settling);
            Assert.Equal(ColorOf(window, "Fg2Brush"), ColorOf(track.BorderBrush));
            var atOn = on.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseMove(atOn);
            await Pump(Settling);
            Assert.Equal(ColorOf(window, "AccentHoverBrush"), ColorOf(onTrack.Background));

            // Klick und Leertaste schalten; Fokus zeigt den Ring.
            window.MouseDown(atOn, MouseButton.Left);
            window.MouseUp(atOn, MouseButton.Left);
            await Pump();
            Assert.False(on.IsChecked);
            off.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            await Pump(Settling);
            Assert.True(off.IsChecked);
            Assert.Equal(21, Part(off, "Thumb").TranslatePoint(new Point(0, 0), Part(off, "Track"))!.Value.X, 0.5);
            Assert.Equal(0.5, panel.Children[2].Opacity);
        }, "schalter");

    [Fact]
    public Task Animationen_aus_nimmt_dem_Schalter_alle_Uebergaenge() => InWindow("hell", () => new ToggleSwitch { Content = "Online-Buchung" }, 360, 120, async window =>
    {
        var toggle = (ToggleSwitch)window.Content!;
        var track = Part(toggle, "Track");
        var thumb = Part(toggle, "Thumb");
        Assert.NotEmpty(track.Transitions!);
        Assert.NotEmpty(thumb.Transitions!);
        window.Styles.AddRange(ReducedMotion.Overrides());
        await Pump();
        Assert.Empty(track.Transitions!);
        Assert.Empty(thumb.Transitions!);
    });

    [Fact]
    public Task Filter_im_Posteingang_wirkt_sofort_und_ist_ein_Schalter_Formulare_mit_Speichern_behalten_Kaestchen() => Render("hell", "schalter-posteingang", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();

        var toggle = window.GetVisualDescendants().OfType<ToggleSwitch>().Single(t => t.IsEffectivelyVisible);
        Assert.Equal("Nur „Prüfen“", toggle.Content);
        Assert.False(shell.Inbox.OnlyOpen);
        toggle.IsChecked = true;
        await Pump();
        Assert.True(shell.Inbox.OnlyOpen);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible && c.Content as string == "Nur „Prüfen“");
        return shell.Inbox;
    });
}

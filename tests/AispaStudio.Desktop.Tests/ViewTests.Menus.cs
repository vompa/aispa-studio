using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F16: Menü und Kontextmenü nach Design-Brief 5.21 (Maße, Tasten, Esc mit Fokusrückgabe, keine Untermenüs, Rechtsklick am Termin).</summary>
public sealed partial class ViewTests
{
    private static Control MenuTrigger()
    {
        var items = new List<Control>
        {
            new MenuItem { Header = "Freie Zeit suchen", Icon = new Icon { Data = Icons.Search, Width = 16, Height = 16 } },
            new MenuItem { Header = "Wunsch eintippen (KI)", Icon = new Icon { Data = Icons.Pencil, Width = 16, Height = 16 } },
            new MenuItem { Header = "Ein sehr langer Eintrag mit viel zu viel Text, damit die Höchstbreite von dreihundert greift" },
            new Separator(),
            new MenuItem { Header = "Stornieren …", Classes = { "danger" } },
        };
        var button = new Button { Content = "Mehr", Name = "Trigger" };
        button.Flyout = new MenuFlyout { ItemsSource = items };
        return new StackPanel { Margin = new Thickness(40), Children = { new Button { Content = "Davor" }, button } };
    }

    private static MenuFlyoutPresenter MenuPresenter(Window window) => window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().Single();

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Menue_hat_die_Masse_Eintraege_32_hoch_Trenner_und_gefaehrlich_unten(string theme) => InWindow(theme, MenuTrigger, 600, 460, async window =>
    {
        var trigger = ((StackPanel)window.Content!).Children.OfType<Button>().Single(b => b.Name == "Trigger");
        trigger.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.True(trigger.Flyout!.IsOpen);

        var presenter = MenuPresenter(window);
        var root = presenter.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "LayoutRoot");
        Assert.InRange(root.Bounds.Width, 138, 300.5);
        Assert.Equal(300, root.Bounds.Width, 0.5);
        Assert.Equal(new CornerRadius(8), root.CornerRadius);
        Assert.Equal(ColorOf(window, "SurfaceBrush"), ColorOf(root.Background));
        Assert.Equal(ColorOf(window, "LineBrush"), ColorOf(root.BorderBrush));
        Assert.Equal(new Thickness(1), root.BorderThickness);
        Assert.NotEqual(default, root.BoxShadow);

        var items = presenter.GetVisualDescendants().OfType<MenuItem>().ToList();
        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.True(i.Bounds.Height >= 32, $"{i.Header}: {i.Bounds.Height}"));
        var layout = items[0].GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_LayoutRoot");
        Assert.Equal(new CornerRadius(6), layout.CornerRadius);
        Assert.Equal(10, layout.Padding.Left);
        Assert.Equal(6, layout.Padding.Top);
        var icon = items[0].GetVisualDescendants().OfType<Icon>().Single();
        Assert.Equal((16, 16), (icon.Bounds.Width, icon.Bounds.Height));
        Assert.Equal(14, items[0].GetVisualDescendants().OfType<TextBlock>().First().FontSize);

        // Trenner 1 px in line, direkt vor dem gefährlichen Eintrag, der als letzter steht und die Farbe danger trägt.
        var separator = presenter.GetVisualDescendants().OfType<Separator>().Single();
        Assert.Equal(1, separator.Bounds.Height, 0.5);
        Assert.Equal(ColorOf(window, "LineBrush"), ColorOf(separator.Background));
        Assert.True(TopOf(window, separator) < TopOf(window, items[3]));
        Assert.True(TopOf(window, separator) > TopOf(window, items[2]));
        Assert.Equal(ColorOf(window, "CritBrush"), ColorOf(items[3].Foreground));
        Assert.True(Contrast(ColorOf(window, "CritBrush"), ColorOf(window, "SurfaceBrush")) >= 4.5);
        // Abstand zwischen den Einträgen 2.
        Assert.Equal(2, TopOf(window, items[1]) - (TopOf(window, items[0]) + items[0].Bounds.Height), 0.5);

        // Hover: Fläche surface-2.
        var at = items[1].TranslatePoint(new Point(40, items[1].Bounds.Height / 2), window)!.Value;
        window.MouseMove(at);
        await Pump(Settling);
        Assert.Equal(ColorOf(window, "Surface2Brush"), ColorOf(items[1].GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_LayoutRoot").Background));
        SaveShot(window, "menue-offen", theme);
    });

    [Fact]
    public Task Menue_Tastatur_Pfeil_ab_erster_Pfeil_auf_letzter_Umlauf_Esc_schliesst_und_der_Fokus_kehrt_zurueck() => InWindow("hell", MenuTrigger, 600, 460, async window =>
    {
        var trigger = ((StackPanel)window.Content!).Children.OfType<Button>().Single(b => b.Name == "Trigger");
        trigger.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        var items = MenuPresenter(window).GetVisualDescendants().OfType<MenuItem>().ToList();
        int Selected() => items.FindIndex(i => i.IsFocused || i.IsSelected);

        // Mit der Tastatur geöffnet steht der erste Eintrag schon bereit; Pfeil ab geht weiter, Pfeil auf vom ersten springt zum letzten (Umlauf).
        Assert.Equal(0, Selected());
        Press(window, PhysicalKey.ArrowDown);
        await Pump();
        Assert.Equal(1, Selected());
        Press(window, PhysicalKey.ArrowUp);
        await Pump();
        Assert.Equal(0, Selected());
        Press(window, PhysicalKey.ArrowUp);
        await Pump();
        Assert.Equal(3, Selected());
        Press(window, PhysicalKey.ArrowDown);
        await Pump();
        Assert.Equal(0, Selected());

        // Esc schließt das Menü, der Fokus kehrt zum Auslöser zurück.
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(trigger.Flyout!.IsOpen);
        Assert.Same(trigger, Focused(window));
    });

    [Fact]
    public Task Menue_von_oben_Pfeil_auf_als_erste_Taste_waehlt_den_letzten_Eintrag() => InWindow("hell", MenuTrigger, 600, 460, async window =>
    {
        var trigger = ((StackPanel)window.Content!).Children.OfType<Button>().Single(b => b.Name == "Trigger");
        trigger.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        var items = MenuPresenter(window).GetVisualDescendants().OfType<MenuItem>().ToList();
        Press(window, PhysicalKey.ArrowUp);
        await Pump();
        Assert.Equal(3, items.FindIndex(i => i.IsFocused || i.IsSelected));
    });

    [Fact]
    public Task Rechtsklick_am_Termin_oeffnet_ein_flaches_Menue_mit_gefaehrlichem_Eintrag_unten_Esc_gibt_den_Fokus_zurueck() => Render("hell", "kalender-kontextmenue", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);

        var menu = window.GetVisualDescendants().OfType<ContextMenu>().Single();
        var items = menu.GetVisualDescendants().OfType<MenuItem>().ToList();
        Assert.NotEmpty(items);
        // Keine Untermenüs: kein Eintrag hat Kinder oder einen Pfeil.
        Assert.All(items, i => Assert.Equal(0, i.ItemCount));
        Assert.All(items.SelectMany(i => i.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Where(p => p.Name == "PART_ChevronPath")), p => Assert.False(p.IsEffectivelyVisible));
        var border = menu.GetVisualDescendants().OfType<Border>().First();
        Assert.Equal(new CornerRadius(8), border.CornerRadius);
        Assert.InRange(border.Bounds.Width, 138, 300.5);
        Assert.All(items, i => Assert.True(i.Bounds.Height >= 32));

        // Der gefährliche Eintrag steht zuletzt, nach einem Trenner, in danger; die Tastenkürzel stehen rechts.
        var danger = items[^1];
        Assert.Equal("danger", danger.Classes.Single(c => c == "danger"));
        Assert.Equal(ColorOf(window, "CritBrush"), ColorOf(danger.Foreground));
        Assert.Single(items, i => i.Classes.Contains("danger"));
        Assert.Single(menu.GetVisualDescendants().OfType<Separator>());
        AssertMenuTextsFit(menu);
        Assert.Contains(menu.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("menugesture") && t.Text == "Entf");
        SaveShot(window, "kontextmenue-offen", "hell");

        // Esc schließt, der Fokus bleibt im Kalender (nicht verloren).
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.Empty(window.GetVisualDescendants().OfType<ContextMenu>());
        Assert.NotNull(Focused(window));
        return calendar;
    });

    private static readonly string[] MenuEntriesWithIcon = ["Details öffnen", "Einchecken", "Kundenkarte", "Folgetermin", "Verschieben", "Erledigt", "Nicht erschienen", "Stornieren …"];

    private static string HeaderOf(MenuItem item) => item.Header as string ?? ((Grid)item.Header!).Children.OfType<TextBlock>().First().Text!;

    /// <summary>Kein Menütext ist breiter als seine Fläche (Brief 5.21: Höchstbreite 300): ein zu langer Eintrag würde rechts abgeschnitten.</summary>
    private static void AssertMenuTextsFit(Control menu)
    {
        foreach (var item in menu.GetVisualDescendants().OfType<MenuItem>())
        {
            var header = HeaderOf(item);
            var shown = item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == header);
            var natural = new TextBlock { Text = header, FontSize = shown.FontSize, FontFamily = shown.FontFamily, FontWeight = shown.FontWeight };
            natural.Measure(Size.Infinity);
            Assert.True(shown.Bounds.Width + 0.5 >= natural.DesiredSize.Width, $"{header}: Fläche {shown.Bounds.Width}, Text {natural.DesiredSize.Width}");
        }
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kontextmenue_am_Termin_traegt_Symbole_an_den_wichtigsten_Eintraegen_und_kommt_spaeter_hat_drei_Stufen(string theme) => Render(theme, "kontextmenue-symbole", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);

        var menu = window.GetVisualDescendants().OfType<ContextMenu>().Single();
        var items = menu.GetVisualDescendants().OfType<MenuItem>().ToList();
        var headers = items.Select(HeaderOf).ToList();

        // „Kommt später“: nur 5, 10 und 15 Minuten.
        Assert.Equal(["Kommt 5 Min. später", "Kommt 10 Min. später", "Kommt 15 Min. später"], headers.Where(h => h.StartsWith("Kommt", StringComparison.Ordinal)));

        // Die wichtigsten Einträge tragen ein Symbol 16 px links, die übrigen keines (nur eine leere Stelle, damit die Texte in einer Linie beginnen).
        Assert.Equal(MenuEntriesWithIcon, headers.Where(MenuEntriesWithIcon.Contains));
        foreach (var item in items)
        {
            var withIcon = MenuEntriesWithIcon.Contains(HeaderOf(item));
            var icons = item.GetVisualDescendants().OfType<Icon>().ToList();
            Assert.Equal(withIcon ? 1 : 0, icons.Count);
            if (withIcon)
            {
                Assert.Equal((16, 16), (icons[0].Bounds.Width, icons[0].Bounds.Height));
                Assert.True(icons[0].IsEffectivelyVisible);
                Assert.True(LeftOf(window, icons[0]) < LeftOf(window, item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == HeaderOf(item))));
            }
        }

        var left = items.Select(i => LeftOf(window, i.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == HeaderOf(i)))).Distinct().Select(x => Math.Round(x)).ToList();
        Assert.Single(left);
        // Das Symbol des gefährlichen Eintrags hat seine Farbe (danger), die der übrigen die Textfarbe.
        var danger = items.Single(i => HeaderOf(i) == "Stornieren …");
        Assert.Equal(ColorOf(window, "CritBrush"), ColorOf(danger.Foreground));
        SaveShot(window, "kontextmenue-symbole", theme);
        return calendar;
    });
}

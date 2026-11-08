using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F27: F6 und Umschalt+F6 wechseln zwischen den großen Bereichen, jeder Bereich hat einen Namen (Design-Brief 9).</summary>
public sealed partial class ViewTests
{
    /// <summary>Name des Bereichs, in dem der Fokus gerade liegt (ohne: null).</summary>
    private static string? FocusedRegion(Window window)
    {
        var shell = window.GetVisualDescendants().OfType<Views.ShellView>().Single();
        var page = shell.FindControl<ContentControl>("PageHost");
        var regions = RegionNavigation.RegionsOf(shell, page);
        var index = RegionNavigation.CurrentIndex(regions, Focused(window) as Avalonia.Visual);
        return index < 0 ? null : AutomationProperties.GetName(regions[index]);
    }

    private static async Task<List<string?>> PressF6(Window window, int times, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var seen = new List<string?>();
        for (var i = 0; i < times; i++)
        {
            window.KeyPressQwerty(PhysicalKey.F6, modifiers);
            window.KeyReleaseQwerty(PhysicalKey.F6, modifiers);
            await Pump();
            seen.Add(FocusedRegion(window));
        }

        return seen;
    }

    private static async Task FocusNavigation(Window window)
    {
        var navigation = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Classes.Contains("nav"));
        navigation.ContainerFromIndex(navigation.SelectedIndex)!.Focus(NavigationMethod.Tab);
        await Pump();
    }

    [Fact]
    public Task F6_wechselt_zwischen_Navigation_Kopfzeile_Kalenderflaeche_Seitenleiste_und_Reitern() => Render("hell", "f6-bereiche", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 2);
        var panelTitle = calendar.ActivePanel!.PanelTitle;
        await FocusNavigation(window);
        Assert.Equal("Navigation", FocusedRegion(window));

        // Vorwärts: Kopfzeile, Kalenderfläche, Seitenleiste, Reiter unten, dann wieder die Navigation.
        Assert.Equal(["Kopfzeile", "Kalenderfläche", panelTitle, "Offene Seiten", "Navigation"], await PressF6(window, 5));

        // Die Kalenderfläche nimmt die Tasten selbst: der Fokus liegt auf der Ansicht.
        await PressF6(window, 2);
        Assert.Equal("Kalenderfläche", FocusedRegion(window));
        Assert.IsType<Views.CalendarView>(Focused(window));

        // Rückwärts mit Umschalt: zurück zur Kopfzeile, über die Navigation zu den Reitern unten und zur Seitenleiste.
        Assert.Equal(["Kopfzeile", "Navigation", "Offene Seiten", panelTitle], await PressF6(window, 4, RawInputModifiers.Shift));
        return calendar;
    });

    [Fact]
    public Task F6_nimmt_KI_und_Hilfe_im_Kalender_als_Reiter_der_Seitenleiste_mit() => Render("hell", "f6-ki-und-hilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.IsOpen = true;
        await Pump(Settling);

        // Im Kalender ist „KI und Hilfe“ ein Reiter der Seitenleiste (Ä66): dieselben Bereiche wie bei jeder Seite, die Leiste trägt den Namen des Assistenten.
        Assert.True(calendar.HasPanel);
        Assert.Equal("KI und Hilfe", calendar.ActivePanel!.PanelTitle);
        await FocusNavigation(window);
        Assert.Equal(["Kopfzeile", "Kalenderfläche", "KI und Hilfe", "Offene Seiten", "Navigation"], await PressF6(window, 5));
        Assert.Equal(["Offene Seiten", "KI und Hilfe", "Kalenderfläche", "Kopfzeile", "Navigation"], await PressF6(window, 5, RawInputModifiers.Shift));
        return calendar;
    });

    [Fact]
    public Task F6_nimmt_KI_und_Hilfe_auf_Seiten_ohne_Reiterschiene_als_Leiste_der_Shell_mit() => Render("hell", "f6-ki-und-hilfe-shell", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
        await Pump(Settling);
        shell.Companion.IsOpen = true;
        await Pump(Settling);
        Assert.False(calendar.HasPanel);
        await FocusNavigation(window);

        var customers = shell.SelectedPage!.Title;
        Assert.Equal([customers, "KI und Hilfe", "Navigation"], await PressF6(window, 3));
        Assert.Equal(["KI und Hilfe", customers, "Navigation"], await PressF6(window, 3, RawInputModifiers.Shift));
        return calendar;
    });

    [Fact]
    public Task F6_springt_nur_zu_vorhandenen_Bereichen_und_in_Seiten_ohne_Unterteilung_zur_Seite() => Render("hell", "f6-ohne-leiste", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await Pump(Settling);
        Assert.False(calendar.HasPanel);
        Assert.False(shell.Companion.IsOpen);

        // Ohne Seitenleiste und ohne „KI und Hilfe“ bleiben Navigation, Kopfzeile und Kalenderfläche.
        window.GetVisualDescendants().OfType<Views.CalendarView>().Single().Focus();
        await Pump();
        Assert.Equal(["Navigation", "Kopfzeile", "Kalenderfläche", "Navigation"], await PressF6(window, 4));

        // Die Seite „Kunden“ hat keine eigenen Bereiche: sie zählt als einer, mit dem Namen der Seite.
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
        await Pump(Settling);
        var customers = shell.SelectedPage!.Title;
        Assert.Equal([customers, "Navigation", customers], await PressF6(window, 3));
        return calendar;
    });

    [Fact]
    public Task Jeder_Bereich_hat_einen_Namen_fuer_den_Screenreader() => Render("hell", "f6-namen", async (window, main) =>
    {
        var calendar = await WithPages(window, main, 3);
        var shell = window.GetVisualDescendants().OfType<Views.ShellView>().Single();
        var page = shell.FindControl<ContentControl>("PageHost");
        var regions = RegionNavigation.RegionsOf(shell, page);
        Assert.Equal(5, regions.Count);
        Assert.All(regions, r => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(r)), $"{r.GetType().Name} ohne Namen"));

        // Im Kalender kommt der Assistent als Reiter in die Seitenleiste (Ä66); die Zahl der Bereiche ändert sich nicht.
        ((ShellViewModel)main.Content!).Companion.IsOpen = true;
        await Pump(Settling);
        regions = RegionNavigation.RegionsOf(shell, page);
        Assert.Equal(5, regions.Count);
        Assert.All(regions, r => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(r)), $"{r.GetType().Name} ohne Namen"));
        return calendar;
    });
}

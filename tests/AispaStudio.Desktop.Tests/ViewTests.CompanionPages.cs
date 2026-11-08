using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R02 Slice 31 (Ä74): der runde Knopf „KI und Hilfe“ und die Leiste der Shell sind auf jeder Seite da, auch neben dem eingebetteten Browser
/// der Hilfe (die Verwaltung ist seit Slice 103 ganz nativ), und der Knopf verdeckt keinen Text. In Tests gibt es keinen nativen Browser; seine Fläche ist der leere Host der Seite.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Alle Seiten der Navigationsleiste im Server-Betrieb (ohne Abgleich, Sicherung und Im Netz des Einzelplatzes).</summary>
    public static TheoryData<string> CompanionPages => new(["Kalender", "Kunden", "Anfragen", "Kassa", "Geräte", "Verwaltung", "Hilfe"]);

    private static string PageSlug(string title) => title.ToLowerInvariant().Replace("ä", "ae", StringComparison.Ordinal);

    /// <summary>Lage eines Steuerelements in Fensterkoordinaten.</summary>
    private static Rect InWindow(Window window, Visual control) => new(control.TranslatePoint(new Point(0, 0), window)!.Value, control.Bounds.Size);

    /// <summary>Die Fläche, die auf der Hilfe der native Browser einnimmt (Panel „Host“ der Seite).</summary>
    private static List<Rect> BrowserAreas(Window window) => window.GetVisualDescendants().OfType<UserControl>()
        .Where(v => v is HelpView && v.IsEffectivelyVisible)
        .Select(v => InWindow(window, v.FindControl<Panel>("Host")!))
        .ToList();

    private static async Task<ShellViewModel> OpenPage(MainViewModel main, string title)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.Single(p => p.Title == title);
        await Pump(Settling);
        return shell;
    }

    [Theory]
    [MemberData(nameof(CompanionPages))]
    public Task KI_und_Hilfe_Knopf_ist_auf_jeder_Seite_sichtbar_und_ein_Klick_in_seine_Mitte_trifft_ihn(string title) => Render("hell", $"ki-knopf-{PageSlug(title)}", async (window, main) =>
    {
        var shell = await OpenPage(main, title);
        Assert.False(shell.Companion.IsOpen);

        var fab = Named<Button>(window, "CompanionFab");
        Assert.True(fab.IsEffectivelyVisible, "Knopf unsichtbar");
        var knob = InWindow(window, fab);
        Assert.True(new Rect(window.Bounds.Size).Contains(knob), $"Knopf außerhalb des Fensters: {knob}");
        // Ein natives Browserfenster liegt über allem, was Avalonia zeichnet: der Knopf darf seine Fläche nicht berühren.
        Assert.All(BrowserAreas(window), browser => Assert.False(knob.Intersects(browser), $"Knopf {knob} liegt unter dem Browser {browser}"));

        var center = knob.Center;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        await Pump();
        Assert.True(shell.Companion.IsOpen, "Klick in die Mitte des Knopfs öffnet KI und Hilfe nicht");
        return shell;
    });

    [Theory]
    [InlineData("Hilfe")]
    public Task KI_und_Hilfe_Leiste_steht_auf_Seiten_mit_Browser_neben_dem_Browser(string title) => Render("hell", $"ki-leiste-{PageSlug(title)}", async (window, main) =>
    {
        var shell = await OpenPage(main, title);

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.True(shell.Companion.IsOpen);

        var panel = Named<Border>(window, "CompanionPanel");
        Assert.True(panel.IsEffectivelyVisible, "Leiste unsichtbar");
        var panelArea = InWindow(window, panel);
        var browser = Assert.Single(BrowserAreas(window));
        Assert.False(panelArea.Intersects(browser), $"Leiste {panelArea} liegt unter dem Browser {browser}");
        return shell;
    });

    [Fact]
    public Task Rueckfragedialog_ueber_der_Shell_behaelt_Esc_F6_und_Strg_Leertaste_fuer_sich() => Render("hell", "ki-dialog-tasten", async (window, main) =>
    {
        // Der Tasten-Handler der Shell hängt am Fenster; ein Dialog darüber darf davon nichts merken.
        var shell = await OpenPage(main, "Kunden");
        shell.Companion.IsOpen = true;
        await Pump(Settling);
        var asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        var inDialog = Focused(window);
        Assert.NotNull(inDialog);

        window.KeyPressQwerty(PhysicalKey.F6, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F6, RawInputModifiers.None);
        await Pump();
        Assert.Same(inDialog, Focused(window));

        // Die Leertaste gehört dem fokussierten Knopf des Dialogs („Abbrechen“ schließt ihn); die Leiste der Shell bleibt offen.
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        await Pump();
        Assert.True(shell.Companion.IsOpen);
        if (main.DialogHost.HasDialog)
        {
            Press(window, PhysicalKey.Escape);
            await Pump();
        }

        Assert.False((await asking).IsConfirmed);
        Assert.True(shell.Companion.IsOpen);

        // Esc ist „Abbrechen“ des Dialogs, die Leiste bleibt offen.
        asking = main.DialogHost.AskAsync(DeleteRequest);
        await Pump(Settling);
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False((await asking).IsConfirmed);
        Assert.False(main.DialogHost.HasDialog);
        Assert.True(shell.Companion.IsOpen);
        return shell;
    });

    [Theory]
    [MemberData(nameof(CompanionPages))]
    public Task KI_und_Hilfe_Knopf_ueberlappt_auf_keiner_Seite_sichtbaren_Text(string title) => Render("hell", $"ki-knopf-text-{PageSlug(title)}", async (window, main) =>
    {
        // Ein kleines Fenster wie am Laptop: die Inhalte reichen bis an den unteren Rand.
        window.Height = 720;
        var shell = await OpenPage(main, title);
        if (shell.SelectedPage!.Page == shell.Inbox)
        {
            shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        }
        else if (shell.SelectedPage.Page == shell.Customers)
        {
            shell.Customers.Query = "mül";
            await Pump(Settling);
            shell.Customers.Selected = shell.Customers.Results[0];
        }

        await Pump(Settling);
        // Am Ende des Inhalts muss die letzte Zeile über dem Knopf stehen; mitten im Scrollen läuft Text wie bei jedem schwebenden Knopf darunter durch.
        foreach (var scroller in window.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible).ToList())
        {
            scroller.ScrollToEnd();
        }

        await Pump(Settling);
        var fab = Named<Button>(window, "CompanionFab");
        var knob = InWindow(window, fab);
        // Text steht in TextBlocks und, in Eingabefeldern, im TextPresenter.
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => (Control: (Control)t, Text: t.Text ?? t.Inlines?.Text))
            .Concat(window.GetVisualDescendants().OfType<TextPresenter>().Select(p => (Control: (Control)p, Text: p.Text)));
        var covered = texts
            .Where(x => x.Control.IsEffectivelyVisible && !fab.IsVisualAncestorOf(x.Control) && !string.IsNullOrWhiteSpace(x.Text))
            .Where(x => InWindow(window, x.Control).Intersects(knob))
            .Select(x => $"„{x.Text}“ {InWindow(window, x.Control)}")
            .ToList();
        Assert.True(covered.Count == 0, $"Der Knopf {knob} verdeckt Text: {string.Join("; ", covered)}");
        return shell;
    }, Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail() with
    {
        // Eine lange Nachricht wie im Betrieb: der Text reicht bis ans Ende der Karte.
        Body = string.Join("\n", Enumerable.Repeat("Diese Zeile der Nachricht reicht bis an den rechten Rand der Karte und noch ein Stück weiter.", 40)),
    }));
}

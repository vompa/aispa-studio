using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 125 (Ä293): Balken mit sieben Gruppen und Reiter darunter, bedient über echte Klicks und Tasten, als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static OverflowStrip GroupStrip(Window window) => Named<OverflowStrip>(window, "GroupStrip");

    private static TabButton StripEntry(OverflowStrip strip, string title) =>
        strip.Children.OfType<TabButton>().Single(b => AutomationProperties.GetName(b) == title);

    /// <summary>Öffnet eine Maske wie die Person: Klick auf ihre Gruppe im Balken, dann auf ihren Reiter.</summary>
    private static async Task OpenMask(Window window, ShellViewModel shell, string title)
    {
        var group = shell.AdminPage.Groups.Single(g => g.Areas.Any(a => a.Title == title));
        ClickCenter(window, StripEntry(GroupStrip(window), group.Title));
        await Pump(Settling);
        if (group.HasTabs)
        {
            ClickCenter(window, StripEntry(Named<OverflowStrip>(window, "AreaStrip"), title));
            await Pump(Settling);
        }
    }

    [Fact]
    public Task Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_zeigt_der_Balken_sieben_Gruppen_und_keine_Liste() =>
        Render("hell", "verwaltung", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);

            Assert.Equal(["Betrieb", "Angebot", "Team", "Lager", "Kassa & Geld", "Kontakt", "Zahlen"],
                GroupStrip(window).Children.OfType<TabButton>().Select(AutomationProperties.GetName));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ListBox>(), l => l.Name == "AreaList");
            Assert.True(StripEntry(GroupStrip(window), "Betrieb").IsSelected);
            return shell.AdminPage;
        });

    [Fact]
    public Task Balken_im_dunklen_Schema_zeigt_die_gewaehlte_Gruppe_Kassa_und_Geld() =>
        Render("dunkel", "verwaltung-balken", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            ClickCenter(window, StripEntry(GroupStrip(window), "Kassa & Geld"));
            await Pump(Settling);
            Assert.Equal("Kassa", shell.AdminPage.SelectedArea?.Title);
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_Schliesszeiten_gewaehlt_When_per_Klick_zu_Lager_und_zurueck_Then_wieder_Schliesszeiten_und_Reiter_nur_bei_mehreren_Masken() =>
        Render("hell", "verwaltung-gruppen", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var tabs = Named<OverflowStrip>(window, "AreaStrip");
            ClickCenter(window, StripEntry(tabs, "Schließzeiten"));
            await Pump(Settling);
            Assert.Same(shell.AdminPage.Closures, shell.AdminPage.Content);

            ClickCenter(window, StripEntry(GroupStrip(window), "Lager"));
            await Pump(Settling);
            Assert.Same(shell.AdminPage.Stock, shell.AdminPage.Content);
            Assert.False(tabs.IsEffectivelyVisible);

            ClickCenter(window, StripEntry(GroupStrip(window), "Betrieb"));
            await Pump(Settling);
            Assert.Same(shell.AdminPage.Closures, shell.AdminPage.Content);
            Assert.True(tabs.IsEffectivelyVisible);
            Assert.True(StripEntry(tabs, "Schließzeiten").IsSelected);
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_1024_px_When_die_Verwaltung_offen_ist_Then_bricht_der_Balken_nicht_um() =>
        Render("hell", "verwaltung-1024", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            window.MinWidth = 0;
            window.Width = 1024;
            await Pump();
            await Pump();
            Assert.Equal(1024, window.Bounds.Width, 0.5);

            var strip = GroupStrip(window);
            var visible = strip.Children.OfType<TabButton>().Where(b => !OverflowStrip.GetIsOverflowed(b)).ToList();
            Assert.NotEmpty(visible);
            var line = TopOf(window, visible[0]);
            Assert.All(visible, b => Assert.Equal(line, TopOf(window, b), 0.5));
            Assert.Empty(strip.GetVisualDescendants().OfType<ScrollViewer>());
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_zu_schmaler_Balken_When_Mehr_per_Tastatur_geoeffnet_wird_Then_ist_die_uebrige_Gruppe_erreichbar_und_Mehr_zeigt_die_Wahl() =>
        Render("hell", "verwaltung-mehr", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var strip = GroupStrip(window);
            strip.MaxWidth = 400;
            await Pump();
            await Pump();

            var more = strip.OverflowButton;
            Assert.True(more.IsEffectivelyVisible);
            Assert.Equal("Mehr", AutomationProperties.GetName(more));
            Assert.Contains(strip.Overflowed, c => AutomationProperties.GetName(c) == "Zahlen");
            Assert.False(StripEntry(strip, "Zahlen").IsEffectivelyEnabled);

            // Vom gewählten Eintrag mit Ende auf „Mehr“, Enter öffnet das Menü.
            StripEntry(strip, "Betrieb").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.End);
            await Pump();
            Assert.Same(more, Focused(window));
            Press(window, PhysicalKey.Enter);
            await Pump();
            var menu = (MenuFlyout)more.Flyout!;
            Assert.True(menu.IsOpen);
            await Pump(Settling);
            Assert.All(menu.Items.OfType<MenuItem>(), i => Assert.True(i.Bounds.Height > 0, $"{i.Header} ist nicht zu sehen"));
            SaveShot(window, "verwaltung-mehr-menue", "hell");
            var entry = menu.Items.OfType<MenuItem>().Single(i => (string?)i.Header == "Zahlen");
            entry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            await Pump(Settling);

            Assert.Equal("Zahlen", shell.AdminPage.SelectedArea?.Title);
            Assert.Contains("on", more.Classes);
            Assert.True(more.IsTabStop);
            Assert.Equal("Mehr, gewählt: Zahlen", AutomationProperties.GetName(more));

            // Auch per Maus zeigt das Menü seine Einträge; das ausgelöste Klick-Ereignis oben schließt es nicht von selbst.
            menu.Hide();
            await Pump(Settling);
            ClickCenter(window, more);
            await Pump(Settling);
            Assert.True(menu.IsOpen);
            Assert.All(menu.Items.OfType<MenuItem>(), i => Assert.True(i.Bounds.Height > 0, $"{i.Header} ist nicht zu sehen"));
            Assert.Equal(["Zahlen"], menu.Items.OfType<MenuItem>().Where(i => i.IsChecked).Select(i => (string?)i.Header));
            Assert.Equal("Mehr, gewählt: Zahlen", ToolTip.GetTip(more));
            SaveShot(window, "verwaltung-mehr-gewaehlt", "hell");
            menu.Hide();
            await Pump(Settling);
            return shell.AdminPage;
        });

    [Fact]
    public Task Balken_ist_ein_Tab_Stopp_Pfeiltasten_bewegen_den_Fokus_und_Enter_waehlt() =>
        Render("hell", "verwaltung-tastatur", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var strip = GroupStrip(window);
            Assert.Equal(["Betrieb"], strip.Children.OfType<TabButton>().Where(b => b.IsTabStop).Select(AutomationProperties.GetName));

            StripEntry(strip, "Betrieb").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            await Pump();
            Assert.Same(StripEntry(strip, "Angebot"), Focused(window));
            Assert.Equal("Firmendaten", shell.AdminPage.SelectedArea?.Title);

            Press(window, PhysicalKey.Enter);
            await Pump(Settling);
            Assert.Equal("Behandlungen", shell.AdminPage.SelectedArea?.Title);
            return shell.AdminPage;
        });

    [Fact]
    public Task F6_erreicht_vom_Balken_die_Maske_und_zurueck() =>
        Render("hell", "verwaltung-f6", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var strip = GroupStrip(window);
            var host = Named<ContentControl>(window, "AreaHost");
            var tabs = Named<OverflowStrip>(window, "AreaStrip");

            StripEntry(strip, "Betrieb").Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.F6);
            await Pump();
            Assert.Same(StripEntry(tabs, "Firmendaten"), Focused(window));
            SaveShot(window, "verwaltung-f6-reiter", "hell");

            Press(window, PhysicalKey.F6);
            await Pump();
            Assert.True(Focused(window) is Visual inMask && host.IsVisualAncestorOf(inMask));

            Press(window, PhysicalKey.F6, RawInputModifiers.Shift);
            await Pump();
            Assert.Same(StripEntry(tabs, "Firmendaten"), Focused(window));

            Press(window, PhysicalKey.F6, RawInputModifiers.Shift);
            await Pump();
            Assert.Same(StripEntry(strip, "Betrieb"), Focused(window));
            SaveShot(window, "verwaltung-f6-balken", "hell");

            Press(window, PhysicalKey.F6, RawInputModifiers.Shift);
            await Pump();
            Assert.False(Focused(window) is Visual inBar && strip.IsVisualAncestorOf(inBar));
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_ungespeicherte_Aenderung_When_die_Rueckfrage_im_Balken_abgebrochen_wird_Then_springen_Balken_und_Reiter_zurueck() =>
        Render("hell", "verwaltung-abbruch", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var tabs = Named<OverflowStrip>(window, "AreaStrip");
            await TypeInto(window, "NameField", "Neuer Name");

            ClickCenter(window, StripEntry(GroupStrip(window), "Lager"));
            await Pump(Settling);
            Assert.Equal("Änderungen speichern?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Press(window, PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);

            Assert.Equal("Firmendaten", shell.AdminPage.SelectedArea?.Title);
            Assert.True(StripEntry(GroupStrip(window), "Betrieb").IsSelected);
            Assert.False(StripEntry(GroupStrip(window), "Lager").IsSelected);
            Assert.True(tabs.IsEffectivelyVisible);
            Assert.True(StripEntry(tabs, "Firmendaten").IsSelected);
            Assert.Equal("Neuer Name", Named<TextBox>(window, "NameField").Text);
            shell.AdminPage.Company?.Discard();
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_Sprung_in_eine_Maske_When_er_ankommt_Then_markieren_Balken_und_Reiter_sie() =>
        Render("hell", "verwaltung-sprung", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);

            shell.AdminPage.Show("masterData", "closures");
            await Pump(Settling);

            Assert.True(StripEntry(GroupStrip(window), "Betrieb").IsSelected);
            Assert.True(StripEntry(Named<OverflowStrip>(window, "AreaStrip"), "Schließzeiten").IsSelected);

            shell.AdminPage.Show("stock", null);
            await Pump(Settling);
            Assert.True(StripEntry(GroupStrip(window), "Lager").IsSelected);
            Assert.False(Named<OverflowStrip>(window, "AreaStrip").IsEffectivelyVisible);
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_schmale_Reiterzeile_When_die_gewaehlte_Maske_im_Ueberlauf_steht_Then_ist_Mehr_der_Tab_Stopp_und_nennt_sie() =>
        Render("hell", "verwaltung-reiter-mehr", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var tabs = Named<OverflowStrip>(window, "AreaStrip");
            ClickCenter(window, StripEntry(tabs, "Handbuch"));
            await Pump(Settling);

            tabs.MaxWidth = 300;
            await Pump();
            await Pump();

            var more = tabs.OverflowButton;
            Assert.Contains("on", more.Classes);
            Assert.True(more.IsTabStop);
            Assert.Equal("Mehr, gewählt: Handbuch", AutomationProperties.GetName(more));
            Assert.Equal(1, tabs.GetVisualDescendants().OfType<Button>().Count(b => b.IsTabStop && b.IsEffectivelyEnabled && b.IsEffectivelyVisible));
            return shell.AdminPage;
        });

    [Fact]
    public Task Der_gewaehlte_Eintrag_im_Balken_ist_halbfett_die_uebrigen_mittel() =>
        Render("hell", "verwaltung-schriftschnitt", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            FontWeight WeightOf(string title) => StripEntry(GroupStrip(window), title).GetVisualDescendants().OfType<TextBlock>().Single().FontWeight;

            Assert.Equal(FontWeight.SemiBold, WeightOf("Betrieb"));
            Assert.Equal(FontWeight.Medium, WeightOf("Lager"));
            return shell.AdminPage;
        });
}

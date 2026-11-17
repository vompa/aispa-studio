using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Hilfe als Leiste (ADR 0079, Nachtrag 17.11.2026): Das Fragezeichen öffnet „KI und Hilfe“ mit dem Abschnitt, die Maske bleibt stehen und behält ihre Eingaben;
/// das aktive Fragezeichen ist markiert, Esc schließt, F1 zeigt die Hilfe zum Feld mit dem Fokus, und offene Leisten der Maske machen der Hilfe Platz.
/// </summary>
public sealed partial class ViewTests
{
    private static HelpButton HelpAt(Window window, string topic, string section) =>
        window.GetVisualDescendants().OfType<HelpButton>().Single(b => b.Topic == topic && b.Section == section && b.IsEffectivelyVisible);

    private static async Task ClickHelp(Window window, HelpButton button)
    {
        button.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, button);
        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Fragezeichen_oeffnet_die_Hilfe_in_der_Leiste_und_die_Maske_behaelt_die_Eingabe(string theme) => Render(theme, "ki-und-hilfe-fragezeichen", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        await TypeInto(window, "NameField", "Day-Spa am Ring");

        var help = HelpAt(window, "firmendaten", "unternehmen");
        await ClickHelp(window, help);

        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.Equal("Day-Spa am Ring", Named<TextBox>(window, "NameField").Text);
        Assert.True(shell.AdminPage.Company!.HasUnsavedChanges);
        Assert.True(Named<Border>(window, "CompanionPanel").IsEffectivelyVisible);
        Assert.True(Named<DockPanel>(window, "HelpArea").IsEffectivelyVisible);
        Assert.Equal("Hilfe: Unternehmen", Named<TextBlock>(window, "HelpTitle").Text);
        Assert.Contains("ansicht=leiste", shell.Companion.HelpArea!.Address?.Query ?? "", StringComparison.Ordinal);
        Assert.Contains("active", help.Classes);
        Assert.True(Named<Button>(window, "OpenManualButton").IsEffectivelyVisible);
        return shell.AdminPage;
    }, CompanyApi());

    [Fact]
    public Task Ein_weiteres_Fragezeichen_tauscht_den_Inhalt_und_Esc_schliesst_die_Hilfe() => Render("hell", "ki-und-hilfe-fragezeichen-wechsel", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        var first = HelpAt(window, "firmendaten", "unternehmen");
        var second = HelpAt(window, "firmendaten", "bankverbindung");
        await ClickHelp(window, first);
        await ClickHelp(window, second);

        Assert.Equal(new HelpTarget("firmendaten", "bankverbindung"), shell.Companion.Help);
        Assert.Equal("Hilfe: Bankverbindung", Named<TextBlock>(window, "HelpTitle").Text);
        Assert.DoesNotContain("active", first.Classes);
        Assert.Contains("active", second.Classes);

        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        await Pump(Settling);

        Assert.False(shell.Companion.ShowsHelp);
        Assert.False(shell.Companion.IsOpen);
        Assert.DoesNotContain("active", second.Classes);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        return shell.AdminPage;
    }, CompanyApi());

    [Fact]
    public Task Die_Hilfe_blendet_den_Verlauf_aus_und_Schliessen_bringt_ihn_zurueck() => Render("hell", "ki-und-hilfe-verlauf", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        ClickCenter(window, Named<Button>(window, "HistoryButton"));
        await Pump(Settling);
        var history = Named<HistoryPanel>(window, "CompanyHistoryPanel");
        Assert.True(history.IsEffectivelyVisible);

        await ClickHelp(window, HelpAt(window, "firmendaten", "unternehmen"));
        Assert.False(history.IsEffectivelyVisible);
        Assert.True(history.IsOpen);
        Assert.True(shell.AdminPage.Company!.IsHistoryOpen);

        ClickCenter(window, Named<Button>(window, "HelpClose"));
        await Pump(Settling);
        Assert.False(shell.Companion.ShowsHelp);
        Assert.True(history.IsEffectivelyVisible);
        return shell.AdminPage;
    }, CompanyApi());

    [Fact]
    public Task F1_zeigt_die_Hilfe_zum_Abschnitt_des_Feldes_mit_dem_Fokus_sonst_zum_Seitenkopf() => Render("hell", "ki-und-hilfe-f1", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        await TypeInto(window, "LegalNameField", "Day-Spa Wien GmbH");

        Press(window, PhysicalKey.F1, RawInputModifiers.None);
        await Pump(Settling);
        Assert.Equal(new HelpTarget("firmendaten", "unternehmen"), shell.Companion.Help);
        Assert.Contains("active", HelpAt(window, "firmendaten", "unternehmen").Classes);

        // Das Feld „Name“ steht in keinem Abschnitt mit eigenem Fragezeichen: dann gilt das Fragezeichen im Kopf der Maske.
        await TypeInto(window, "NameField", "Day-Spa am Ring");
        Press(window, PhysicalKey.F1, RawInputModifiers.None);
        await Pump(Settling);
        Assert.Equal(new HelpTarget("firmendaten", "firmendaten"), shell.Companion.Help);
        Assert.Equal("Day-Spa am Ring", Named<TextBox>(window, "NameField").Text);
        return shell.AdminPage;
    }, CompanyApi());

    [Fact]
    public Task Im_Kalender_zeigt_der_Reiter_KI_und_Hilfe_die_Hilfe_und_F1_bleibt_die_Tastenhilfe() => Render("hell", "ki-und-hilfe-kalender", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        Assert.True(shell.ShowHelpInPanel("kalender-warteliste", null));
        await Pump(Settling);
        Assert.Same(shell.Companion, calendar.CompanionPanel);
        var panel = Named<Border>(window, "SidePanel");
        Assert.True(Named<DockPanel>(panel, "HelpArea").IsEffectivelyVisible);
        Assert.True(panel.GetVisualDescendants().OfType<CompanionContent>().Single().IsEffectivelyVisible);

        Press(window, PhysicalKey.F1, RawInputModifiers.None);
        await Pump();
        Assert.True(calendar.ShowKeyHelp);
        Assert.Equal(new HelpTarget("kalender-warteliste", null), shell.Companion.Help);
        calendar.ShowKeyHelp = false;
        return calendar;
    });
}

using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Durchsicht der Verwaltung „Betrieb“, „Behandlungen“ und „Aktionen“ (Sichtprüfung vom 19.11.2026): Fuß mit „Speichern“ immer sichtbar (masken.md R5), Beschriftung als
/// Substantiv mit Einheit in der Klammer und Erklärung als Hilfetext (R6, design.md 5.2), Fragezeichen an der Gruppe (R9), bündige Ränder (R4).
/// </summary>
public sealed partial class ViewTests
{
    private static readonly string[] CompanyIntros =
    [
        "Gelten für alle Standorte ohne eigene Gesellschaft.",
        "Für die Sofortüberweisung an der Kassa (QR-Code).",
        "Ergänzt das Impressum der Online-Buchung.",
    ];

    private static async Task SetWindowSize(Window window, double width, double height)
    {
        window.MinWidth = 0;
        window.MinHeight = 0;
        window.Width = width;
        window.Height = height;
        await Pump(Settling);
        await Pump(Settling);
    }

    private static List<string> FieldLabels(Control root) =>
        root.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible && f.Label is not null).Select(f => f.Label!).ToList();

    private static List<string> FieldHelps(Control root) =>
        root.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible && f.Help is not null).Select(f => f.Help!).ToList();

    private static void AssertFootVisible(Window window, Control foot, ScrollViewer scroll)
    {
        var bounds = InWindow(window, foot);
        Assert.True(foot.IsEffectivelyVisible);
        Assert.True(bounds.Top >= 0 && bounds.Bottom <= window.Bounds.Height + 0.5, $"Fuß von {bounds.Top} bis {bounds.Bottom}, Fenster {window.Bounds.Height}");
        Assert.Empty(scroll.GetVisualDescendants().OfType<SaveBar>());
        Assert.Contains(foot.GetVisualDescendants().OfType<Button>(), b => b.Name == "SaveButton" && b.IsEffectivelyVisible);
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1100, 680)]
    public Task Standorte_haben_Speichern_fest_im_Fuss_auch_nach_dem_Rollen(double width, double height) => Render("hell", $"probe-standorte-fuss-{width}", async (window, main) =>
    {
        // Given der Standort ist offen; When das Fenster 1440 × 900 bzw. 1100 × 680 groß ist
        var shell = await OpenLocations(window, main);
        await SetWindowSize(window, width, height);
        var scroll = Named<ScrollViewer>(window, "FieldsScroll");
        var foot = Named<Control>(window, "Footer");

        // Then steht „Speichern“ im Fuß außerhalb der rollenden Felder und im sichtbaren Fenster
        AssertFootVisible(window, foot, scroll);
        var before = InWindow(window, foot);

        // And er bleibt, wenn die Maske bis zu den Räumen gerollt ist
        scroll.ScrollToEnd();
        await Pump(Settling);
        Assert.True(scroll.Offset.Y > 0);
        AssertFootVisible(window, foot, scroll);
        Assert.Equal(before.Top, InWindow(window, foot).Top, 0.5);
        return shell.AdminPage;
    }, LocationsApi());

    [Fact]
    public Task Standorte_Online_Regeln_nennen_Einheit_in_der_Klammer_und_Erklaerung_als_Hilfetext() => Render("hell", "probe-standorte-regeln", async (window, main) =>
    {
        // Given die Online-Regeln des Standorts sind geladen
        var shell = await OpenLocations(window, main);
        var rules = window.GetVisualDescendants().OfType<OnlineRulesSection>().Single();
        var labels = FieldLabels(rules);

        // Then ist jede Beschriftung ein Substantiv mit höchstens der Einheit in der Klammer (R6)
        Assert.Contains("Ausfallgebühr (%)", labels);
        Assert.Contains("Anzahlung (%)", labels);
        Assert.Contains("Freigabe ohne Karte oder Anzahlung (Stunden)", labels);
        Assert.Contains("Hinweis für Gäste", labels);
        Assert.Contains("Stornoregel", labels);
        Assert.DoesNotContain(labels, l => l.Contains("0 =", StringComparison.Ordinal) || l.Contains(", ", StringComparison.Ordinal) || l.Contains("des Preises", StringComparison.Ordinal));

        // And die Erklärung steht als Hilfetext darunter, höchstens eine Zeile
        var helps = FieldHelps(rules);
        Assert.Contains("0 = keine Gebühr.", helps);
        Assert.Contains("0 = keine, 100 = Vorauszahlung.", helps);
        Assert.Contains("0 = nie freigeben.", helps);
        foreach (var field in rules.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible && f.Help is not null))
        {
            var message = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_MessageText");
            Assert.True(message.Bounds.Height < 20, $"Hilfetext „{field.Help}“ bricht um (Höhe {message.Bounds.Height})");
        }

        // And der lange Absatz unter den Feldern ist weg: er steht in der Hilfe der Gruppe
        Assert.DoesNotContain(rules.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Mit Ausfallgebühr stimmt", StringComparison.Ordinal) == true);
        rules.BringIntoView();
        await Pump(Settling);
        return shell.AdminPage;
    }, LocationsApi());

    [Fact]
    public Task Standorte_Bundesland_ist_so_breit_wie_die_Felder_darueber_und_die_Gruppe_traegt_das_Fragezeichen() => Render("hell", "probe-standorte-bundesland", async (window, main) =>
    {
        // Given der Standort ist offen
        var shell = await OpenLocations(window, main);
        var view = window.GetVisualDescendants().OfType<LocationsView>().Single();

        // Then reicht das Feld „Bundesland / Kanton“ von der linken bis zur rechten Kante des Namensfelds (R4)
        var name = InWindow(window, Named<TextBox>(view, "NameField"));
        var region = InWindow(window, Named<ComboBox>(view, "RegionField"));
        Assert.Equal(name.Left, region.Left, 0.5);
        Assert.Equal(name.Right, region.Right, 0.5);

        // And das Fragezeichen steht nur am Gruppentitel (R9), nicht zusätzlich am Feld
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<HelpButton>(), h => h.Section == "bundesland-kanton");
        Assert.Contains(view.GetVisualDescendants().OfType<HelpButton>(), h => h.Section == "standort");

        // And Gruppentitel und Feldbeschriftung heißen nicht gleich („Anreise und Parkplätze“)
        Assert.Contains("Wegbeschreibung", FieldLabels(view));
        Assert.DoesNotContain("Anreise und Parkplätze", FieldLabels(view));
        return shell.AdminPage;
    }, LocationsApi());

    [Fact]
    public Task Standorte_Raumkopf_Raumpflege_ist_nicht_abgeschnitten_und_Vorgaben_wiederherstellen_ist_ein_Knopf() => Render("hell", "probe-standorte-raumkopf", async (window, main) =>
    {
        // Given die Räume sind geladen
        var shell = await OpenLocations(window, main);
        var rooms = window.GetVisualDescendants().OfType<RoomsSection>().Single();

        // Then passt der Spaltenkopf „Raumpflege (min)“ ganz in seine Spalte
        var head = rooms.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Raumpflege (min)");
        Assert.True(head.DesiredSize.Width <= head.Bounds.Width + 0.5, $"Kopf braucht {head.DesiredSize.Width}, hat {head.Bounds.Width}");

        // And „Vorgaben wiederherstellen“ ist ein Knopf mit Rand, kein Textlink (design.md 5.1: Sekundär)
        var reset = window.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Vorgaben wiederherstellen");
        Assert.DoesNotContain("ghost", reset.Classes);
        rooms.BringIntoView();
        await Pump(Settling);
        return shell.AdminPage;
    }, LocationsApi());

    [Fact]
    public Task Firmendaten_nennen_den_Namen_als_Substantiv_und_ihre_Einleitungen_haben_eine_Zeile() => Render("hell", "probe-firmendaten-texte", async (window, main) =>
    {
        // Given die Firmendaten sind geöffnet
        var shell = await OpenAdminPage(window, main);
        var view = window.GetVisualDescendants().OfType<CompanyView>().Single();

        // Then heißt das erste Feld „Name des Spas“ und die Erklärung steht darunter
        Assert.Contains("Name des Spas", FieldLabels(view));
        Assert.Contains("So sehen Gäste und Team den Namen.", FieldHelps(view));

        // And jede Einleitung unter einem Gruppentitel hat eine Zeile
        foreach (var text in CompanyIntros)
        {
            var intro = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
            Assert.True(intro.Bounds.Height < 24, $"„{text}“ bricht um (Höhe {intro.Bounds.Height})");
        }

        return shell.AdminPage;
    });
}

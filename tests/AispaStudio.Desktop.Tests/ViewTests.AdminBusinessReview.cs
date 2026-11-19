using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
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
        Assert.True(name.Width > 740, $"Das Formular ist nur {name.Width} breit");
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

        // Then füllt die Maske ihre Höchstbreite auch ohne lange Einleitungstexte (sonst schrumpft sie auf den breitesten Inhalt)
        Assert.True(Named<TextBox>(view, "EmbedField").Bounds.Width > 650, $"Die Maske ist nur {Named<TextBox>(view, "EmbedField").Bounds.Width} breit");

        // And heißt das erste Feld „Name des Spas“ und die Erklärung steht darunter
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

    [Fact]
    public Task Raumsperre_zeigt_die_Warnung_in_hoechstens_zwei_Zeilen_den_Raum_als_Text_und_denselben_Zustand_wie_die_Liste() => Render("hell", "probe-raumsperre-formular", async (window, main) =>
    {
        // Given die Raumsperre „Raum Zeder, Wartung“ mit einem offenen Termin ist in der Seitenleiste geöffnet
        var shell = await OpenClosures(window, main);
        await ClickRow(window, "UpcomingList", 0);
        var panel = Named<Control>(window, "EditPanel");

        // Then ist die Warnung höchstens zwei Zeilen lang (design.md 5.15)
        var warning = panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("1 Termin im gesperrten Raum", StringComparison.Ordinal) == true);
        Assert.True(warning.TextLayout.TextLines.Count <= 2, $"Warnung hat {warning.TextLayout.TextLines.Count} Zeilen");

        // And der feste Raum steht als Text mit Begründung statt als kaum lesbares gesperrtes Feld, und die Gruppe heißt anders als die Beschriftung
        Assert.Equal("Raum Zeder", Named<TextBlock>(panel, "RoomFixedText").Text);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Der Raum einer Sperre lässt sich nicht ändern.");
        Assert.True(Named<TextBlock>(panel, "RoomFixedText").Opacity > 0.99);

        // And der Zustand heißt wie in der Liste „Läuft“
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Läuft");
        Assert.Equal("Läuft", shell.AdminPage.ClosuresPage!.Upcoming[0].StatusText);
        return shell.AdminPage;
    }, ClosuresApi());

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1100, 680)]
    public Task Aktionen_haben_Speichern_fest_im_Fuss_und_das_Rabattfeld_beginnt_am_selben_Rand(double width, double height) => Render("hell", $"probe-aktionen-fuss-{width}", async (window, main) =>
    {
        // Given die Aktion „Kurzfristig frei“ ist offen; When das Fenster 1440 × 900 bzw. 1100 × 680 groß ist
        var (shell, promotions) = await OpenPromotions(window, main);
        promotions.SelectedRow = promotions.Rows.Single(r => r.Id == PromotionSample.LastMinuteId);
        await Pump(Settling);
        await SetWindowSize(window, width, height);
        var scroll = Named<ScrollViewer>(window, "FormScroll");

        // Then steht „Speichern“ im Fuß außerhalb des rollenden Formulars und im sichtbaren Fenster, auch nach dem Rollen
        AssertFootVisible(window, Named<Control>(window, "Footer"), scroll);
        scroll.ScrollToEnd();
        await Pump(Settling);
        AssertFootVisible(window, Named<Control>(window, "Footer"), scroll);

        // And „Rabatt (%)“ beginnt am selben linken Rand wie „Name“ und „Vorlauf (Stunden)“ (R4)
        var name = InWindow(window, Named<TextBox>(window, "PromotionNameField")).Left;
        Assert.Equal(name, InWindow(window, Named<TextBox>(window, "PromotionValueField")).Left, 0.5);
        Assert.Equal(name, InWindow(window, Named<TextBox>(window, "PromotionLeadHoursField")).Left, 0.5);

        // And die Einheit steht bei beiden Zahlenfeldern in der Klammer
        var labels = FieldLabels(Named<Control>(window, "FormScroll"));
        Assert.Contains("Vorlauf (Stunden)", labels);
        Assert.Contains("Rabatt (%)", labels);
        return shell.AdminPage;
    }, PromotionsApi());

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1100, 680)]
    public Task Kalender_Abos_haben_Speichern_fest_im_Fuss_und_zeigen_den_Widerspruch_als_Pille_mit_Erklaerung(double width, double height) => Render("hell", $"probe-abos-fuss-{width}", async (window, main) =>
    {
        // Given das Abo „Feiertage Wien“ mit einem Widerspruch ist offen
        var shell = await OpenAdminSubscriptions(window, main);
        await SetWindowSize(window, width, height);
        var scroll = Named<ScrollViewer>(window, "FormScroll");

        // Then füllt das Formular seine Höchstbreite (800) und steht „Speichern“ im Fuß außerhalb des rollenden Formulars, auch nach dem Rollen
        if (width > 1300)
        {
            Assert.True(Named<TextBox>(window, "UrlField").Bounds.Width > 740, $"Das Formular ist nur {Named<TextBox>(window, "UrlField").Bounds.Width} breit");
        }

        AssertFootVisible(window, Named<Control>(window, "Footer"), scroll);
        scroll.ScrollToEnd();
        await Pump(Settling);
        AssertFootVisible(window, Named<Control>(window, "Footer"), scroll);
        scroll.ScrollToHome();
        await Pump(Settling);

        // And die Beschriftung „Quelle“ ist ein Substantiv, jede Erklärung unter einem Feld hat eine Zeile
        Assert.Contains("Quelle", FieldLabels(scroll));
        foreach (var field in scroll.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible && f.Help is not null))
        {
            var message = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_MessageText");
            Assert.True(message.Bounds.Height < 20 || width < 1200, $"Hilfetext „{field.Help}“ bricht um (Höhe {message.Bounds.Height})");
        }

        // And der Widerspruch in der Liste ist eine Warnungs-Pille mit Erklärung im Tooltip
        var list = Named<ListBox>(window, "SubscriptionList");
        var pill = list.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("pill") && b.Classes.Contains("warn") && b.IsEffectivelyVisible);
        Assert.Contains("Widerspruch", pill.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Contains("Widersprüche entscheiden", ToolTip.GetTip(pill) as string);
        return shell.AdminPage;
    }, AdminSubscriptionsApi());

    [Fact]
    public Task Handbuch_legt_Eintraege_ueber_den_primaeren_Knopf_an_und_erklaert_Wochentage_und_Gilt_als_Hilfetext() => Render("hell", "probe-handbuch", async (window, main) =>
    {
        // Given ein Eintrag mit Erinnerung ist offen
        var shell = await OpenHandbook(window, main);
        var handbook = shell.AdminPage.Handbook!;
        handbook.SelectedRow = handbook.Entries[0];
        await Pump(Settling);

        // Then ist „Neuen Eintrag anlegen“ wie „Standort anlegen“, „Neue Behandlung“ und „Neue Aktion“ der primäre Knopf (ADR 0099)
        Assert.Contains("primary", Named<Button>(window, "NewEntryButton").Classes);

        // And Beschriftung und Erklärung sind getrennt: „Wochentage“ mit Zeile, „Gilt“ mit Zeile
        Assert.True(Shows(window, "Wochentage"));
        Assert.Equal("Gilt", Named<CheckBox>(window, "ActiveBox").Content);
        Assert.Equal("Ausgeschaltete Einträge erinnern nicht mehr.", Named<TextBlock>(window, "ActiveHelp").Text);
        Assert.Equal("Erinnert nur an diesen Tagen; ohne Auswahl täglich.", Named<TextBlock>(window, "DaysHelp").Text);
        Assert.False(Shows(window, "keiner gewählt heißt täglich"));
        return shell.AdminPage;
    }, HandbookApi());

    [Fact]
    public Task Behandlungsliste_zeigt_jeden_Namen_vollstaendig_mit_Dauer_und_Preis_rechts_und_Verlauf_ist_sekundaer() => Render("hell", "probe-behandlungen-liste", async (window, main) =>
    {
        // Given die Liste der Behandlungen mit Dauer und Preis je Zeile
        var treatments = await OpenTreatments(window, main);

        // Then wird kein Name mit „…“ gekürzt: er bricht bei Bedarf um und Dauer und Preis stehen rechts in derselben Zeile (design.md 575)
        var titles = Named<ListBox>(window, "TreatmentList").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && ToolTip.GetTip(t) is not null).ToList();
        Assert.NotEmpty(titles);
        Assert.All(titles, t =>
        {
            Assert.Equal(Avalonia.Media.TextTrimming.None, t.TextTrimming);
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, t.TextWrapping);
        });

        // And „Dauer · Preis“ steht rechts neben dem Namen, nicht darunter
        var meta = Named<ListBox>(window, "TreatmentList").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("num")).ToList();
        Assert.NotEmpty(meta);
        Assert.All(meta, m => Assert.Equal(2, Grid.GetColumn(m)));

        // And „Verlauf“ ist ein sekundärer Knopf (design.md 575), kein Ghost-Knopf
        Assert.DoesNotContain("ghost", Named<Button>(window, "HistoryButton").Classes);
        return treatments;
    }, TreatmentApi());

    [Fact]
    public Task Behandlung_Durchfuehrung_hat_Spaltenkoepfe_ueber_den_Zeilenlisten_und_klare_Beschriftungen() => Render("hell", "probe-behandlungen-durchfuehrung", async (window, main) =>
    {
        // Given der Bereich „Durchführung“ von Hot Stone 60
        var treatments = await OpenTreatments(window, main);
        var editor = treatments.Editor!;
        await ClickSection(window, "Durchführung");

        // Then steht über jeder Zeilenliste mit Einträgen genau ein Kopf, über leeren Listen keiner (design.md 6, Punkt 4)
        Assert.Equal(editor.PreparationSteps.Count > 0, Named<Control>(window, "StepHeader").IsEffectivelyVisible);
        Assert.Equal(editor.Materials.Count > 0, Named<Control>(window, "MaterialHeader").IsEffectivelyVisible);
        Assert.Equal(editor.Extras.Count > 0, Named<Control>(window, "ExtraHeader").IsEffectivelyVisible);
        Assert.True(editor.PreparationSteps.Count + editor.Materials.Count + editor.Extras.Count > 0, "Die Beispieldaten haben keine Zeilen");

        // And „Menge“ steht über dem Mengenfeld der ersten Zeile
        if (editor.Materials.Count > 0)
        {
            var quantity = window.GetVisualDescendants().OfType<NumberField>().First(f => f.IsEffectivelyVisible && AutomationProperties.GetName(f) == "Menge");
            var header = Named<Control>(window, "MaterialHeader").GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Menge");
            Assert.Equal(InWindow(window, quantity).Left, InWindow(window, header).Left, 1);
        }

        // And die Zeitangabe „Am Gast“ heißt „Arbeit vor der Einwirkzeit (min)“ mit Erklärung
        var labels = FieldLabels(Named<Control>(window, "ExecutionSection"));
        Assert.Contains("Arbeit vor der Einwirkzeit (min)", labels);
        Assert.DoesNotContain(labels, l => l.StartsWith("Am Gast", StringComparison.Ordinal));
        return treatments;
    }, TreatmentApi());

    [Fact]
    public Task Neue_Behandlung_ohne_Qualifikation_zeigt_den_Hinweis_mit_Aktion_und_Standorte_ohne_Raum_einen_Satz() => Render("hell", "probe-behandlungen-hinweis", async (window, main) =>
    {
        // Given eine neue Behandlung mit Name und Preis, aber ohne Qualifikation
        var treatments = await OpenTreatments(window, main);
        ClickCenter(window, Named<Button>(window, "NewButton"));
        await Pump(Settling);
        await TypeInto(window, "NameField", "Kräuterstempel");
        treatments.Editor!.Price = 59m;

        // When Strg+S gedrückt wird
        PressSave(window);
        await Pump(Settling);

        // Then steht der Hinweis als Warnung nach design.md 5.9 über den Spalten, mit der Aktion „Trotzdem speichern“
        var bar = Named<InfoBar>(window, "QualificationBar");
        Assert.True(bar.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Warning, bar.Severity);
        Assert.Equal("Trotzdem speichern", bar.ActionText);
        Assert.Equal("Keine Qualifikation gewählt: Jede Person im Team kann diese Behandlung übernehmen.", bar.Message);

        // And die Beschriftungen der Räume haben keinen Doppelpunkt, ein Standort ohne Raum nennt das
        Assert.False(Shows(window, "Geeignete Räume:"));
        Assert.True(Shows(window, "Geeignete Räume"));
        Assert.True(Shows(window, "Noch kein Raum an diesem Standort."));
        return treatments;
    }, TreatmentApi());
}

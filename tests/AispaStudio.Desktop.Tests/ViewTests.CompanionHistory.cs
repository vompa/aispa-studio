using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Slice 138 (Ä347): „KI und Hilfe“ beginnt nach dem Schließen leer, „Neu beginnen“ und „Zuletzt gefragt“ – als Inhaberin, mit echter Maus und echten Tasten,
/// im Kalender (Reiter der Seitenleiste) und in der Leiste der Shell.
/// </summary>
public sealed partial class ViewTests
{
    private const string AssistantRoute = "POST /api/assistant";

    private static CompanionContent CompanionPanelOf(Window window) => window.GetVisualDescendants().OfType<CompanionContent>().Single();

    private static void OpenWithCtrlSpace(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
    }

    private static Control? FocusedOf(Window window) => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;

    /// <summary>„Neu beginnen“ im Kalender: die Aktion im gemeinsamen Kopf der Seitenleiste (T-018, Ä359).</summary>
    private static Button ResetButton(Window window) => Named<Button>(Named<Border>(window, "SidePanel"), "PanelHeaderAction");

    /// <summary>Der Knopf liegt im Kopf der Leiste, in der Zeile des Kreuzes und links davon.</summary>
    private static void AssertLeftOfCrossInHeader(Window window, Button button)
    {
        var header = button.FindAncestorOfType<SidePanelHeader>();
        Assert.NotNull(header);
        var cross = header.GetVisualDescendants().OfType<Button>().First(b => AutomationPropertiesName(b) == "Schließen");
        var at = button.TranslatePoint(default, window)!.Value;
        var crossAt = cross.TranslatePoint(default, window)!.Value;
        Assert.InRange(at.Y, crossAt.Y - 4, crossAt.Y + 4);
        Assert.True(at.X + button.Bounds.Width <= crossAt.X, $"Knopf endet bei {at.X + button.Bounds.Width}, Kreuz beginnt bei {crossAt.X}");
    }

    private static bool AnswerShown(Control panel) => Named<Border>(panel, "AnswerCard").IsEffectivelyVisible;

    private static Button? RecentEntry(Control panel, string question) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && AutomationPropertiesName(b) == question
            && b.GetVisualAncestors().OfType<StackPanel>().Any(p => p.Name == "RecentQuestionsList"));

    [Fact]
    public Task Esc_schliesst_im_Kalender_und_Strg_Leertaste_zeigt_KI_und_Hilfe_leer_mit_Fokus_und_Zuletzt_gefragt() => Render("hell", "ki-zuletzt-gefragt", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");
        await AskByKeyboard(window, "Wer arbeitet morgen?");
        Assert.True(AnswerShown(panel));

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(shell.Companion.IsOpen);
        OpenWithCtrlSpace(window);
        await Pump(Settling);

        panel = CompanionPanelOf(window);
        Assert.True(shell.Companion.IsOpen);
        Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
        Assert.Equal("", Named<TextBox>(panel, "CompanionQuestion").Text ?? "");
        Assert.False(AnswerShown(panel));
        Assert.NotNull(RecentEntry(panel, "Wer arbeitet morgen?"));
        Assert.NotNull(RecentEntry(panel, "Termine morgen?"));
        Assert.False(ResetButton(window).IsEffectivelyVisible);

        // Für das Bild im Handbuch: keine Hervorhebung oder Tooltip darüber.
        window.MouseMove(new Avalonia.Point(4, 4));
        await Pump(Settling);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Kreuz_der_Leiste_in_der_Shell_schliesst_und_oeffnet_leer() => Render("hell", "ki-leer-shell", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Title == "Kunden");
        await Pump(Settling);
        shell.Companion.ToggleCommand.Execute(null);
        await Pump(Settling);
        Assert.True(shell.Companion.ShellOpen);
        await AskByKeyboard(window, "Termine morgen?");
        var panel = CompanionPanelOf(window);
        Assert.True(AnswerShown(panel));

        var panelBorder = Named<Border>(window, "CompanionPanel");
        var cross = panelBorder.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationPropertiesName(b) == "Schließen");
        await ClickIntoView(window, cross);
        await Pump(Settling);
        Assert.False(shell.Companion.IsOpen);

        OpenWithCtrlSpace(window);
        await Pump(Settling);

        panel = CompanionPanelOf(window);
        Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
        Assert.False(AnswerShown(panel));
        Assert.NotNull(RecentEntry(panel, "Termine morgen?"));
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Kreuz_im_Kalender_Reiter_schliesst_den_Reiter_und_oeffnet_ihn_leer() => Render("hell", "ki-leer-reiter", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        await AskByKeyboard(window, "Termine morgen?");
        Assert.True(AnswerShown(panel));

        var side = Named<Border>(window, "SidePanel");
        var cross = side.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationPropertiesName(b) == "Schließen");
        await ClickIntoView(window, cross);
        await Pump(Settling);
        Assert.False(shell.Companion.IsOpen);

        shell.Companion.ToggleCommand.Execute(null);
        await Pump(Settling);

        panel = CompanionPanelOf(window);
        Assert.False(AnswerShown(panel));
        Assert.Equal("", Named<TextBox>(panel, "CompanionQuestion").Text ?? "");
        Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Klick_auf_Zuletzt_gefragt_stellt_die_Frage_neu_und_Pfeil_nach_oben_holt_sie_ins_leere_Feld()
    {
        var api = ListApi();
        return Render("hell", "ki-zuletzt-klick", async (window, main) =>
        {
            var (shell, panel) = await CompanionOpen(window, main);
            await AskByKeyboard(window, "Termine morgen?");
            Press(window, PhysicalKey.Escape);
            await Pump(Settling);
            OpenWithCtrlSpace(window);
            await Pump(Settling);
            panel = CompanionPanelOf(window);
            Assert.False(AnswerShown(panel));
            Assert.Equal(1, api.Calls.Count(c => c.Route == AssistantRoute));

            await ClickIntoView(window, RecentEntry(panel, "Termine morgen?")!);
            await Pump(Settling);
            Assert.Equal(2, api.Calls.Count(c => c.Route == AssistantRoute));
            Assert.True(AnswerShown(panel));

            // Neu beginnen mit der Maus: leer, Fokus im Feld, dann holt Pfeil nach oben die letzte Frage.
            await ClickIntoView(window, ResetButton(window));
            await Pump(Settling);
            Assert.False(AnswerShown(panel));
            Assert.True(shell.Companion.IsOpen);
            Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
            Press(window, PhysicalKey.ArrowUp);
            await Pump(Settling);
            Assert.Equal("Termine morgen?", Named<TextBox>(panel, "CompanionQuestion").Text);
            return shell.Companion;
        }, api);
    }

    [Fact]
    public Task Neu_beginnen_ist_nur_da_wenn_es_etwas_zurueckzusetzen_gibt() => Render("hell", "ki-neu-beginnen", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        var reset = ResetButton(window);
        Assert.False(reset.IsEffectivelyVisible);
        // Nicht mehr in der Zeile „Assistent“ (Ä359): nur im Kopf neben dem Kreuz.
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => AutomationPropertiesName(b) == "Neu beginnen");

        await TypeInto(window, "CompanionQuestion", "halb getippt");
        Assert.True(reset.IsEffectivelyVisible);
        Assert.Equal("Neu beginnen", AutomationPropertiesName(reset));
        AssertLeftOfCrossInHeader(window, reset);

        await ClickIntoView(window, reset);
        Assert.Equal("", Named<TextBox>(panel, "CompanionQuestion").Text ?? "");
        Assert.False(reset.IsEffectivelyVisible);
        Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Neu_beginnen_steht_in_der_Leiste_der_Shell_im_Kopf_links_vom_Kreuz() => Render("hell", "ki-neu-beginnen-shell", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Title == "Kunden");
        await Pump(Settling);
        shell.Companion.ToggleCommand.Execute(null);
        await Pump(Settling);
        var reset = Named<Button>(Named<Border>(window, "CompanionPanel"), "CompanionReset");
        Assert.False(reset.IsEffectivelyVisible);

        await TypeInto(window, "CompanionQuestion", "halb getippt");
        Assert.True(reset.IsEffectivelyVisible);
        Assert.Equal("Neu beginnen", AutomationPropertiesName(reset));
        AssertLeftOfCrossInHeader(window, reset);

        await ClickIntoView(window, reset);
        Assert.Equal("", Named<TextBox>(CompanionPanelOf(window), "CompanionQuestion").Text ?? "");
        Assert.False(reset.IsEffectivelyVisible);
        Assert.True(shell.Companion.IsOpen);
        Assert.Equal("CompanionQuestion", FocusedOf(window)?.Name);
        return shell.Companion;
    }, ListApi());

    [Fact]
    public Task Andere_Leisten_zeigen_keine_Aktion_im_Kopf_und_der_Titel_behaelt_seine_Breite() => Render("hell", "warteliste-kopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenWaitlistCommand.Execute(null);
        await Pump(Settling);

        var header = Named<SidePanelHeader>(Named<Border>(window, "SidePanel"), "PanelHeader");
        Assert.False(ResetButton(window).IsEffectivelyVisible);
        var cross = header.GetVisualDescendants().OfType<Button>().First(b => AutomationPropertiesName(b) == "Schließen");
        var title = header.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Warteliste");
        var titleEnd = title.TranslatePoint(new Point(title.Bounds.Width, 0), window)!.Value.X;
        // Zwischen Titelspalte und Kreuz liegt genau der Spaltenabstand von 12 px, keine zusätzliche leere Spalte.
        Assert.Equal(cross.TranslatePoint(default, window)!.Value.X - 12, titleEnd, 1);
        return calendar;
    }, ListApi());

    [Fact]
    public Task Auf_der_Seite_Hilfe_zeigt_die_Leiste_nur_den_Assistenten() => Render("hell", "ki-hilfe-seite", async (window, main) =>
    {
        var (shell, panel) = await CompanionOpen(window, main);
        Assert.Contains("Ganze Beschreibung öffnen", TextsAndButtons(panel));

        shell.Companion.ShowPage("hilfe");
        await Pump(Settling);

        var texts = TextsAndButtons(panel);
        Assert.DoesNotContain("Alle Hilfethemen", texts);
        Assert.DoesNotContain("Ganze Beschreibung öffnen", texts);
        Assert.DoesNotContain(texts, t => t.StartsWith("Zu jeder Einstellung", StringComparison.Ordinal));
        Assert.Contains("Assistent", texts);
        return shell.Companion;
    }, ListApi());

    private static List<string> TextsAndButtons(Control panel) =>
        [.. TextsIn(panel), .. panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string ?? "")];
}

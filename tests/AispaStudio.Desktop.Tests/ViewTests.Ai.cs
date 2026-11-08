using System.Net;
using System.Reflection;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Fluent 2 F30: KI-Kennzeichnung nach Design-Brief 5.28. Jede KI-Ausgabe der Windows-App trägt die Pille „KI“, eine Hinweiszeile, „Warum dieser Vorschlag?“ und die Rückmeldung;
/// ein Antwortentwurf wird nie ohne ausdrücklichen Klick gesendet; bei einem Fehler nennt die Oberfläche einen Weg ohne KI.
/// </summary>
public sealed partial class ViewTests
{
    private static List<AiPill> Pills(Visual root) => [.. root.GetVisualDescendants().OfType<AiPill>().Where(p => p.IsEffectivelyVisible)];

    private static List<AiFooter> Footers(Visual root) => [.. root.GetVisualDescendants().OfType<AiFooter>().Where(f => f.IsEffectivelyVisible)];

    private static List<string> TextsIn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!)];

    private static Button VisibleButton(Visual root, string content) =>
        root.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && (b.Content as string == content || AutomationPropertiesName(b) == content));

    private static string? AutomationPropertiesName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control);

    private static void Invoke(Button button)
    {
        Assert.True(button.Command!.CanExecute(button.CommandParameter), $"{button.Content}: Befehl nicht ausführbar");
        button.Command.Execute(button.CommandParameter);
    }

    /// <summary>
    /// Prüft die Kennzeichen unter <paramref name="root"/>: mindestens eine Pille und ein Fuß in einer Zeile aus Hinweis und „Warum?“, ohne Rückmeldeknöpfe
    /// (Auftraggeber, 29.10.2026).
    /// </summary>
    private static AiFooter AssertMarked(Visual root, string why, string? disclaimer = null)
    {
        Assert.NotEmpty(Pills(root));
        var footer = Footers(root).First(f => f.Why == why);
        Assert.Equal(disclaimer ?? AiLabels.Disclaimer, footer.Disclaimer);
        var text = footer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DisclaimerText");
        var toggle = footer.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "WhyToggle");
        Assert.True(text.IsEffectivelyVisible && toggle.IsEffectivelyVisible);
        Assert.Equal(footer.Disclaimer, text.Text);
        double Middle(Control c) => c.TranslatePoint(new Point(0, c.Bounds.Height / 2), footer)!.Value.Y;
        Assert.Equal(Middle(text), Middle(toggle), 3.0);
        Assert.DoesNotContain(footer.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content is "Hilfreich" or "Nicht hilfreich");
        return footer;
    }

    // ---------- KI und Hilfe ----------

    private static readonly AssistantAnswer AssistantSample = new(AssistantAudience.Reception, "So geht es in der Kassa:", [], [],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.",
        [new AssistantHelpLink("seite-kassa", "kassieren", "Kassieren", "Termin wählen und kassieren.")]);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_kennzeichnet_die_Antwort_mit_Pille_und_einer_Hinweiszeile(string theme) => Render(theme, "ki-kennzeichnung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Der Knopf trägt dasselbe Zeichen wie die Ausgaben, solange die Leiste zu ist.
        await Pump();
        var fab = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CompanionFab");
        Assert.Contains(fab.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == AiLabels.Pill);
        Assert.Empty(Pills(window));
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?");
        await Pump(Settling);

        var panel = window.GetVisualDescendants().OfType<CompanionContent>().Single();
        // Pille am Kopf „Assistent“ und an der Antwort; der Hinweis des Servers ersetzt den zentralen Wortlaut, die Begründung ist zu.
        Assert.Equal(2, Pills(panel).Count);
        var footer = AssertMarked(panel, AiLabels.WhyAssistant, AssistantSample.Disclosure);
        var why = footer.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "WhyToggle");
        Assert.DoesNotContain(AiLabels.WhyAssistant, TextsIn(footer));
        why.IsChecked = true;
        await Pump();
        Assert.Contains(AiLabels.WhyAssistant, TextsIn(footer));
        Assert.True(why.IsChecked);

        panel.GetVisualAncestors().OfType<ScrollViewer>().First().ScrollToEnd();
        await Pump(Settling);
        SaveShot(window, "ki-kennzeichnung-antwort", theme);
        return calendar;
    }, Api().On("POST /api/assistant", AssistantSample));

    [Fact]
    public Task KI_und_Hilfe_Fehler_nennt_den_Weg_ohne_KI_mit_einem_Knopf_zum_Handbuch() => Render("hell", "ki-fehler-assistent", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?");
        await Pump(Settling);

        var panel = window.GetVisualDescendants().OfType<CompanionContent>().Single();
        Assert.NotNull(shell.Companion.Error);
        Assert.Contains(shell.Companion.Error!, TextsIn(panel));
        Assert.Contains(AiLabels.WayOutHelp, TextsIn(panel));
        Assert.Empty(Footers(panel));
        SaveShot(window, "ki-fehler-assistent", "hell");

        // Der Weg ohne KI führt ins Handbuch.
        Invoke(VisibleButton(panel, "Handbuch öffnen"));
        await Pump();
        Assert.Same(shell.Help, shell.SelectedPage!.Page);
        return calendar;
    }, Api().On("POST /api/assistant", null, HttpStatusCode.ServiceUnavailable));

    // ---------- Wunsch eintippen ----------

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Wunsch_eintippen_kennzeichnet_die_Vorschlaege_mit_Pille_und_einer_Hinweiszeile(string theme) => Render(theme, "ki-kennzeichnung-wunsch", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine kräftige Aromaöl-Massage, nicht bei Marco";
        await calendar.Request.SubmitAsync();
        await Pump(Settling);

        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        AssertMarked(panel, AiLabels.WhyRequest, SpaSample.CalendarRequest().Disclosure);
        SaveShot(window, "ki-kennzeichnung-wunsch", theme);
        return calendar;
    }, Api().On($"POST /api/locations/{SpaSample.LocationId}/calendar-request", SpaSample.CalendarRequest()));

    [Fact]
    public Task Wunsch_eintippen_Fehler_nennt_die_Suche_nach_freier_Zeit_als_Weg_ohne_KI() => Render("hell", "ki-fehler-wunsch", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine Massage";
        await calendar.Request.SubmitAsync();
        await Pump(Settling);

        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        Assert.True(calendar.Request.HasError);
        Assert.Contains(AiLabels.WayOutFreeTime, TextsIn(panel));
        SaveShot(window, "ki-fehler-wunsch", "hell");
        Invoke(VisibleButton(panel, "Freie Zeit suchen"));
        await Pump(Settling);
        Assert.NotNull(calendar.FreeTime);
        Assert.Null(calendar.Request);
        return calendar;
    }, Api().On($"POST /api/locations/{SpaSample.LocationId}/calendar-request", null, HttpStatusCode.ServiceUnavailable));

    // ---------- Posteingang ----------

    private static async Task<InboxDetailViewModel> OpenInboxDetail(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump(Settling);
        _ = window;
        return shell.Inbox.Detail!;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Posteingang_kennzeichnet_Erkanntes_und_Entwurf_und_sendet_nie_von_selbst(string theme)
    {
        var stub = Api()
            .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail())
            .On($"POST /api/inbox/{SpaSample.InboxMailId}/send", SpaSample.InboxMail() with { ReplyStatus = ReplyStatus.Sent });
        return Render(theme, "ki-kennzeichnung-posteingang", async (window, main) =>
        {
            var detail = await OpenInboxDetail(window, main);
            var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);

            // Erkanntes (Pille vor der Zusammenfassung) und Entwurf (Pille und „KI-Entwurf, noch nicht gesendet“), je eine Hinweiszeile.
            Assert.True(Pills(view).Count >= 2, $"Pillen: {Pills(view).Count}");
            Assert.Contains(AiLabels.DraftNotice, TextsIn(view));
            var read = AssertMarked(view, AiLabels.WhyInboxRead);
            var draft = AssertMarked(view, AiLabels.WhyInboxDraft, detail.DraftHint);
            Assert.NotSame(read, draft);
            Assert.StartsWith("Beim Senden angehängt: „", draft.Disclaimer, StringComparison.Ordinal);
            SaveShot(window, "ki-kennzeichnung-posteingang", theme);

            // Prüfschritt: der Entwurf steht da, nichts wurde gesendet; ein geänderter Entwurf lässt sich erst nach dem Speichern senden; gesendet wird nur auf den Klick.
            Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/send", StringComparison.Ordinal));
            Assert.True(detail.IsDraft);
            Assert.True(detail.SendCommand.CanExecute(null));
            detail.ReplyBody += " Bis bald.";
            Assert.False(detail.SendCommand.CanExecute(null), "Ein geänderter Entwurf wird erst gespeichert, dann gesendet.");
            Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/send", StringComparison.Ordinal));
            return detail;
        }, stub);
    }

    [Fact]
    public Task Posteingang_Entwurf_ohne_KI_Zugang_zeigt_keine_Ueberarbeiten_Knoepfe_und_keine_weiteren_Hinweise() => Render("hell", "ki-fehler-entwurf", async (window, main) =>
    {
        var detail = await OpenInboxDetail(window, main);
        var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);
        Assert.False(detail.CanRevise);
        Assert.DoesNotContain("Mit KI überarbeiten", TextsIn(view));
        Assert.DoesNotContain(TextsIn(view), t => t.Contains("API-Schlüssel", StringComparison.Ordinal) || t.StartsWith("Ohne KI weiter", StringComparison.Ordinal));
        Assert.True(detail.IsDraft);
        SaveShot(window, "ki-fehler-entwurf", "hell");
        return detail;
    }, Api()
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail() with { CanRevise = false }));

    [Fact]
    public Task Posteingang_gesendete_Antwort_zeigt_keinen_Entwurfskopf_mehr_aber_die_Pille()
    {
        var inbox = SpaSample.Inbox();
        var stub = Api().On("GET /api/inbox", inbox).On($"GET /api/inbox/{inbox[1].Id}", SpaSample.InboxMail() with { Id = inbox[1].Id, ReplyStatus = ReplyStatus.Sent });
        return Render("hell", "ki-gesendet", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
            await Pump();
            shell.Inbox.SelectedRow = shell.Inbox.Mails[1];
            await Pump(Settling);
            var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);
            var detail = shell.Inbox.Detail!;
            Assert.False(detail.IsDraft);
            Assert.Equal("Gesendet.", detail.ReplyState);
            Assert.DoesNotContain(AiLabels.DraftNotice, TextsIn(view));
            Assert.Contains("Gesendet.", TextsIn(view));
            Assert.NotEmpty(Pills(view));
            return detail;
        }, stub);
    }

    // ---------- Meldungen › Was tun? ----------

    private static async Task OpenMessageDetail(Window window, CalendarViewModel calendar)
    {
        calendar.ByRoom = true;
        await Pump();
        calendar.OpenMessagesCommand.Execute(null);
        await Pump();
        var card = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("messagecard") && b.DataContext is MessageItem { Kind: MessageKind.DayStart });
        card.BringIntoView();
        await Pump();
        ClickCenter(window, card);
        await Pump(Settling);
        await Pump(Settling);
    }

    [Fact]
    public Task Was_tun_kennzeichnet_die_Antwort_mit_Pille_und_einer_Hinweiszeile() => Render("hell", "ki-kennzeichnung-was-tun", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await OpenMessageDetail(window, calendar);
        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        Assert.True(calendar.MessageDetail!.HasAnswer);
        AssertMarked(panel, AiLabels.WhyWhatToDo, "Antwort der KI.");
        SaveShot(window, "ki-kennzeichnung-was-tun", "hell");
        return calendar;
    }, OperationsApi().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Reception, "Raum und Checkliste prüfen.", [], [], "Antwort der KI.", [])));

    [Fact]
    public Task Was_tun_Fehler_nennt_das_Handbuch_zur_Meldung_als_Weg_ohne_KI() => Render("hell", "ki-fehler-was-tun", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await OpenMessageDetail(window, calendar);
        var panel = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SidePanel");
        Assert.True(calendar.MessageDetail!.AskFailed);
        Assert.Contains(AiLabels.WayOutHelpTopic, TextsIn(panel));
        Assert.Empty(Footers(panel));
        Assert.True(calendar.MessageDetail.HasHelp);
        SaveShot(window, "ki-fehler-was-tun", "hell");
        return calendar;
    }, OperationsApi().On("POST /api/assistant", null, HttpStatusCode.ServiceUnavailable));

    // ---------- Scan über die KI-Modelle ----------

    private static IEnumerable<Type> ViewModelTypes() => typeof(CompanionViewModel).Assembly.GetTypes().Where(t => t.Namespace == typeof(CompanionViewModel).Namespace && t.IsClass);

    [Fact]
    public void Jede_KI_Funktion_gehoert_zu_genau_einem_Modell_und_jedes_Modell_mit_Hinweis_auf_eine_KI_Ausgabe_ist_gekennzeichnet()
    {
        var marked = ViewModelTypes().Where(t => t.GetCustomAttribute<AiOutputAttribute>() is not null).ToList();
        var functions = marked.SelectMany(t => t.GetCustomAttribute<AiOutputAttribute>()!.Functions).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.Equal(AiFunctions.All.Select(f => f.Id).OrderBy(f => f, StringComparer.Ordinal), functions);

        // Jedes Modell mit einem Hinweis auf die KI (Eigenschaft „Disclosure“) muss ausgezeichnet sein, sonst fehlt ihm die Pille.
        var unmarked = ViewModelTypes()
            .Where(t => t.GetCustomAttribute<AiOutputAttribute>() is null && t.GetProperties().Any(p => p.Name.Contains("Disclosure", StringComparison.Ordinal)))
            .Select(t => t.Name).ToList();
        Assert.True(unmarked.Count == 0, "KI-Ausgabe ohne [AiOutput]: " + string.Join(", ", unmarked));
    }

    [Fact]
    public void Die_Texte_der_KI_Kennzeichnung_sind_sachlich_ohne_Du_Ich_und_Gefuehl()
    {
        var texts = typeof(AiLabels).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)!).ToList();
        string[] forbidden = ["ich ", "Ich ", " du ", " dein", "leider", "gerne", "freut", "Danke", "Super", "Schade", "!"];
        Assert.All(texts, text => Assert.DoesNotContain(forbidden, word => text.Contains(word, StringComparison.Ordinal)));
        Assert.All(texts.Where(t => t.StartsWith("Ohne KI weiter", StringComparison.Ordinal)), t => Assert.EndsWith(".", t, StringComparison.Ordinal));
    }
}

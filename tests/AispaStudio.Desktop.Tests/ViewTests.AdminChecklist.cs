using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Verwaltungsmasken nach ADR 0098 und der Kurz-Checkliste in masken.md für Betrieb, Kassa &amp; Geld und Kontakt (T-064): je Seite höchstens eine Schiene mit höchstens fünf
/// Einträgen, die nicht umbricht; Haupt- und Nebeninhalt 3 : 2; Warnungen im oberen Drittel. Geprüft wird als Inhaberin.
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("Betrieb", "betrieb", 5)]
    [InlineData("Kassa & Geld", "kassa-geld", 5)]
    [InlineData("Kontakt", "kontakt", 3)]
    public Task Die_Reiterzeile_hat_hoechstens_fuenf_Eintraege_und_bricht_bei_Mindestfenster_nicht_um(string group, string slug, int count) =>
        Render("hell", $"probe-verwaltung-reiter-{slug}", async (window, main) =>
        {
            // Given das Fenster am Minimum 1100 × 680; When die Gruppe gewählt ist
            var shell = await OpenAdminPage(window, main);
            window.MinWidth = window.MinHeight = 0;
            (window.Width, window.Height) = (1100, 680);
            ClickCenter(window, StripEntry(GroupStrip(window), group));
            await Pump(Settling);
            await Pump(Settling);

            // Then hat die Schiene so viele Einträge wie die Gruppe Masken, höchstens fünf, alle sichtbar in einer Zeile und innerhalb der Seite.
            var tabs = Named<OverflowStrip>(window, "AreaStrip");
            var entries = tabs.Children.OfType<TabButton>().Where(b => b != tabs.OverflowButton).ToList();
            Assert.Equal(count, entries.Count);
            Assert.True(entries.Count <= 5);
            Assert.Empty(tabs.Overflowed);
            Assert.All(entries, e => Assert.True(e.IsEffectivelyVisible));
            Assert.Single(entries.Select(e => Math.Round(TopOf(window, e))).Distinct());
            var host = Named<ContentControl>(window, "AreaHost");
            Assert.True(RightOf(window, entries[^1]) <= RightOf(window, host) + 0.5);
            var height = tabs.Bounds.Height;

            // Und die Seite hat darunter keine zweite Schiene.
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("segmented") && b.IsEffectivelyVisible);

            // When die Seitenleiste „KI und Hilfe“ den Platz verengt, Then bleibt die Schiene einzeilig (was nicht passt, steht unter „Mehr“, design.md Abschnitt 6).
            shell.Companion.ToggleCommand.Execute(null);
            await Pump(Settling);
            await Pump(Settling);
            Assert.Equal(height, tabs.Bounds.Height, 0.5);
            Assert.Single(entries.Where(e => e.IsEffectivelyVisible && !OverflowStrip.GetIsOverflowed(e)).Select(e => Math.Round(TopOf(window, e))).Distinct());
            return shell.AdminPage;
        }, SettingsApi());

    [Theory]
    [InlineData("hell", 1440)]
    [InlineData("dunkel", 1440)]
    [InlineData("hell", 1100)]
    public Task Erinnerungen_zeigen_zwei_Karten_mit_Standard_und_Ausnahmen_und_oeffnen_das_Versandprotokoll_als_Seitenleiste(string theme, int width) =>
        Render(theme, $"verwaltung-erinnerungen-{width}", async (window, main) =>
        {
            // Given die Erinnerungen mit Standard 42, zwei Ausnahmen und Zahlen der letzten 30 Tage
            window.MinWidth = 0;
            window.Width = width;
            var reminders = await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen");
            Assert.Equal("42", Named<TextBox>(window, "DefaultDaysField").Text);
            Assert.Equal(2, reminders.Exceptions.Count);
            Assert.Equal("Letzte 30 Tage: 34 verschickt, 0 fehlgeschlagen", Named<TextBlock>(window, "ReminderSummaryText").Text);
            Assert.True(Named<Border>(window, "ExceptionTable").IsEffectivelyVisible);
            Assert.False(Named<SidePanel>(window, "LogPanel").IsEffectivelyVisible);

            // When „Versandprotokoll“ unter den Wiederbuchungs-Hinweisen angeklickt wird
            await ClickScrolled(window, "RebookLogButton");

            // Then steht die Liste der Wiederbuchungs-Hinweise in der Seitenleiste rechts, ohne dass die Karten verschwinden
            var panel = Named<SidePanel>(window, "LogPanel");
            Assert.True(panel.IsEffectivelyVisible);
            Assert.True(reminders.Log.IsOpen);
            Assert.Equal(DispatchFilter.Rebook, reminders.Log.Current);
            Assert.Equal(2, Named<ItemsControl>(window, "LogList").GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible && b.Classes.Contains("ghost")));
            Assert.True(Named<Border>(window, "RebookCard").IsEffectivelyVisible);
            Assert.True(RightOf(window, Named<Border>(window, "RebookCard")) <= LeftOf(window, panel) + 0.5);
            Assert.True(RightOf(window, panel) <= window.Bounds.Width + 0.5);

            // Und kein Feld der Leiste ragt unter die Bildlaufleiste: die Auswahl „Zeitraum“ endet vor dem rechten Rand der Leiste mit 20 Innenabstand.
            Assert.True(RightOf(window, Named<ComboBox>(window, "LogPeriod")) <= RightOf(window, panel) - 20 + 0.5);
            Assert.True(RightOf(window, Named<ListBox>(window, "LogFilters")) <= RightOf(window, panel) - 20 + 0.5);

            // When die erste Zeile angeklickt wird, Then klappt ihr Text auf
            var first = Named<ItemsControl>(window, "LogList").GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("ghost"));
            ClickCenter(window, first);
            await Pump(Settling);
            Assert.True(Shows(window, "„Ihr letzter Besuch bei uns ist schon eine Weile her.“"));
            return reminders;
        }, SettingsApi());

    [Fact]
    public Task Erinnerungen_nehmen_eine_Ausnahme_aus_dem_Menue_und_zeigen_Tage_leer_als_kein_Hinweis() => Render("hell", "probe-erinnerungen-ausnahme", async (window, main) =>
    {
        // Given die Erinnerungen mit zwei Ausnahmen
        var reminders = await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen");
        var before = reminders.Exceptions.Count;

        // When eine Behandlung aus „+ Ausnahme“ gewählt wird
        var field = Named<ComboBox>(window, "AddExceptionField");
        field.BringIntoView();
        reminders.NewException = reminders.ExceptionChoices.First(c => c.Kind == RebookExceptionKind.Treatment);
        await Pump(Settling);

        // Then steht eine Zeile mit „kein Hinweis“ in der Tabelle, und die Maske hat etwas zu speichern
        Assert.Equal(before + 1, reminders.Exceptions.Count);
        Assert.True(Shows(window, "kein Hinweis"));
        Assert.True(reminders.HasUnsavedChanges);
        Assert.Null(field.SelectedItem);
        return reminders;
    }, SettingsApi());

    [Fact]
    public Task Zahlungen_zeigen_die_Warnung_zu_Testschluesseln_oben_vor_der_Wahl_der_Zahlungsart() => Render("hell", "probe-zahlungen-warnung", async (window, main) =>
    {
        // Given eigene Stripe-Schlüssel im Testmodus
        var payments = await OpenSettingsArea<PaymentsViewModel>(window, main, "Zahlungen");
        Assert.True(payments.ShowTestKeyWarning);

        // Then steht „Testschlüssel hinterlegt“ als Warnung oben, über der Wahl der Zahlungsart, und im oberen Drittel der Maske.
        var warning = Named<InfoBar>(window, "TestKeyWarning");
        Assert.True(warning.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Warning, warning.Severity);
        Assert.True(BottomOf(window, warning) <= TopOf(window, Named<RadioButton>(window, "ConnectOption")));
        var view = window.GetVisualDescendants().OfType<PaymentsView>().Single();
        Assert.True(TopOf(window, warning) - TopOf(window, view) < view.Bounds.Height / 3);
        Assert.Single(window.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible && b.Title == "Testschlüssel hinterlegt");
        return payments;
    }, SettingsApi().On("GET /api/tenant/payments", PaymentsTests.OwnKeys with { LiveMode = false }));

    [Fact]
    public Task Postfach_zeigt_den_Stand_des_letzten_Versands_oben_beim_Stand_des_Abrufs() => Render("hell", "probe-postfach-warnung", async (window, main) =>
    {
        // Given der letzte Versand ist fehlgeschlagen
        var shell = await OpenIntake(window, main);

        // Then steht die Warnung oben unter „Eigenes Postfach“, über „Postfach abrufen“ und nicht mehr in „Antworten und Erinnerungen“.
        var notice = Named<InfoBar>(window, "SendNoticeBar");
        Assert.True(notice.IsEffectivelyVisible);
        Assert.Equal("Letzter Versand fehlgeschlagen: Anmeldung abgelehnt", notice.Message);
        Assert.True(BottomOf(window, notice) <= TopOf(window, Named<CheckBox>(window, "EnabledBox")));
        Assert.Single(window.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible && b.Severity == InfoBarSeverity.Warning);
        return shell.AdminPage;
    }, IntakeApi().On("GET /api/intake/mailbox", IntakeMailbox with { LastSendError = "Anmeldung abgelehnt" }));

    [Fact]
    public Task Kosten_nennen_oben_in_einem_Hinweis_wie_vielen_Behandlungen_Angaben_fehlen() => Render("hell", "probe-kosten-warnung", async (window, main) =>
    {
        // Given einer Behandlung fehlt das Material
        var shell = await OpenArea(window, main, "Kosten");
        var costs = shell.AdminPage.Costs!;

        // Then steht über der Tabelle ein Hinweis „Warnung“ mit der Zahl; was fehlt, nennt weiter die Zeile.
        Assert.Equal("Bei 1 Behandlung fehlen Angaben für die Vollkosten. Die Zeile nennt, was fehlt; Sätze und Räume trägt man weiter unten ein, Material bei der Behandlung.", costs.MissingNotice);
        var notice = Named<InfoBar>(window, "MissingNoticeBar");
        Assert.True(notice.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Warning, notice.Severity);
        var row = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Material ist nicht hinterlegt.");
        Assert.True(BottomOf(window, notice) <= TopOf(window, row));
        return shell.AdminPage;
    }, ReportsApi().On($"GET /api/costs/locations/{LocationId}/analysis?month=2026-10",
        CostAnalysis("2026-10") with { Treatments = [.. CostAnalysis("2026-10").Treatments.Select((t, i) => i == 0 ? t with { Missing = ["Material ist nicht hinterlegt."] } : t)] }));

    [Fact]
    public async Task Kosten_ohne_fehlende_Angaben_zeigen_keinen_Hinweis()
    {
        var complete = CostAnalysis("2026-10") with { Treatments = [.. CostAnalysis("2026-10").Treatments.Select(t => t with { Missing = [] })] };
        var api = AddCosts(new StubApi(), "2026-10").On($"GET /api/costs/locations/{LocationId}/analysis?month=2026-10", complete);
        var costs = new CostsViewModel(api.Client(), TestData.Owner(), new SampleTime());
        await costs.EnsureLoadedAsync();

        Assert.NotEmpty(costs.Rows);
        Assert.Null(costs.MissingNotice);
        Assert.False(costs.HasMissingNotice);
    }

    [Theory]
    [InlineData("Standorte & Räume", "LocationList", "standorte")]
    [InlineData("Kalender-Abos", "SubscriptionList", "abos")]
    [InlineData("Handbuch", "EntryList", "handbuch")]
    public Task Die_Liste_links_ist_in_jeder_Maske_von_Betrieb_260_breit(string area, string list, string slug) => Render("hell", $"probe-listenbreite-{slug}", async (window, main) =>
    {
        // design.md 5.6: Liste 260 in der App, daneben mit 16 Abstand die Karte; die Listenkarte selbst ist also 244 breit.
        var shell = await OpenAdminPage(window, main);
        await OpenMask(window, shell, area);
        await Pump(Settling);

        var card = Named<ListBox>(window, list).GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card"));
        Assert.Equal(244, card.Bounds.Width, 0.5);
        return shell.AdminPage;
    }, AdminSubscriptionsApi());
}

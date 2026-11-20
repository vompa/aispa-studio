using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Durchsicht des Kalenders, Teil 2: Termin-Panel (Mehr, Chips, Treatwell), Kundenübersicht, Meldungen, Warteliste, Lückenvorschläge, KI und Hilfe, Tastenhilfe,
/// Pausen, Paar oder Spa-Tag und Raumcheck. Jede Zusicherung misst Grenzen, Sichtbarkeit oder abgeschnittenen Text.
/// </summary>
public sealed partial class ViewTests
{
    // ---------- M1 und 11: „Mehr“ im Termin-Panel ----------

    [Fact]
    public Task Mehr_ist_eine_dichte_Liste_mit_Symbol_in_jeder_Zeile_und_Stornieren_gehoert_dazu() => Render("hell", "durchsicht2-11-mehr", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var more = window.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Name == "MoreToggle");
        more.IsChecked = true;
        await Pump(Settling);

        var list = Named<StackPanel>(window, "MoreList");
        var rows = list.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("menurow")).ToList();
        Assert.True(rows.Count >= 6, $"Nur {rows.Count} Zeilen: {string.Join(", ", rows.Select(r => r.Name))}");
        Assert.Contains(rows, r => r.Name == "CancelButton");
        foreach (var row in rows)
        {
            // Alle Zeilen tragen ein Symbol, keine ist ein großer umrandeter Knopf (M1, 11).
            Assert.NotEmpty(row.GetVisualDescendants().OfType<Icon>());
            Assert.Equal(new Thickness(0), row.BorderThickness);
            Assert.InRange(row.Bounds.Height, 28, 40);
        }

        // Dichter als sechs große Knöpfe: Der Kopf der Karte „Kunde“ bleibt im Bild.
        var scroll = Named<ScrollViewer>(window, "PanelScroll");
        var header = Named<AccordionHeader>(window, "CustomerHeader");
        var view = BoundsIn(scroll, window);
        var headerBox = BoundsIn(header, window);
        Assert.True(headerBox.Top >= view.Top && headerBox.Top < view.Bottom - 8, $"Kunde steht bei {headerBox}, sichtbar ist {view}");
        return calendar;
    });

    [Fact]
    public Task Termin_stornieren_ist_ohne_Mehr_nicht_sichtbar_und_Entf_fragt_weiter_nach() => Render("hell", "durchsicht2-m1-entf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "CancelButton" && b.IsEffectivelyVisible);
        Assert.True(calendar.Selected!.AskCancelCommand.CanExecute(null));
        Assert.True(calendar.Selected.HasMore);
        Assert.Contains(calendar.KeyHints, h => h.Key == "Entf");
        return calendar;
    });

    // ---------- E: Chip-Zeile im Termin-Panel ----------

    [Fact]
    public Task Die_Chip_Zeile_im_Termin_Panel_hat_Luft_darunter_und_beim_Rollen_einen_Trenner() => Render("hell", "durchsicht2-e-chips", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var chips = Named<Border>(window, "AppointmentChips");
        var scroll = Named<ScrollViewer>(window, "PanelScroll");
        var pills = chips.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("pill")).ToList();
        Assert.NotEmpty(pills);
        var pillsBottom = pills.Max(p => BoundsIn(p, window).Bottom);
        Assert.True(BoundsIn(chips, window).Bottom - pillsBottom >= 8 - 0.5, "Unter den Chips fehlen 8 px Luft");
        Assert.True(BoundsIn(chips, window).Bottom <= BoundsIn(scroll, window).Top + 0.5, "Die Chips liegen über dem Rollbereich");
        Assert.True(chips.Background is ISolidColorBrush { Color.A: 255 }, "Der Grund der Chip-Zeile ist nicht deckend");
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)chips.BorderBrush!).Color);

        scroll.Offset = new Vector(0, 60);
        await Pump(Settling);
        Assert.NotEqual(Colors.Transparent, ((ISolidColorBrush)chips.BorderBrush!).Color);
        scroll.Offset = default;
        await Pump(Settling);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)chips.BorderBrush!).Color);
        return calendar;
    });

    // ---------- T1: Treatwell im Termin-Panel ----------

    [Fact]
    public Task Treatwell_Knopf_im_Panel_heisst_wie_im_Posteingang() => Render("hell", "durchsicht2-t1-treatwell", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.TreatwellAppointment());
        await Pump(Settling);
        var panel = SidePanelOf(window);
        var button = panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "In Treatwell angenommen");
        Assert.Equal(calendar.Selected!.PlatformAcceptedCommand, button.Command);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Erledigt");
        return calendar;
    });

    // ---------- 21: KI und Hilfe ----------

    [Fact]
    public Task KI_Antwort_zeigt_den_Hinweis_ganz_die_Frage_einmal_und_Ueberschrift_und_Links_in_Karten_Groesse() => Render("hell", "durchsicht2-21-ki", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        OpenWithCtrlSpace(window);
        await Pump(Settling);
        const string Question = "Wie kassiere ich einen Termin?";
        await shell.Companion.AskExampleCommand.ExecuteAsync(Question);
        await Pump(Settling);
        var panel = CompanionPanelOf(window);
        var card = Named<Border>(panel, "AnswerCard");
        Assert.True(card.IsEffectivelyVisible);

        // 21a: Der Pflichthinweis bricht um und steht ganz da, „Warum?“ bleibt daneben.
        var disclaimer = Named<TextBlock>(card, "DisclaimerText");
        Assert.Equal(TextWrapping.Wrap, disclaimer.TextWrapping);
        Assert.Equal(TextTrimming.None, disclaimer.TextTrimming);
        Assert.Equal(shell.Companion.Answer!.Disclosure, disclaimer.Text);
        Assert.True(BoundsIn(disclaimer, window).Right <= BoundsIn(card, window).Right, "Der Hinweis läuft über die Karte");
        Assert.True(disclaimer.Bounds.Height > 16, $"Der Hinweis bricht nicht um (Höhe {disclaimer.Bounds.Height})");
        Assert.True(BoundsIn(disclaimer, window).Bottom <= BoundsIn(card, window).Bottom, "Der Hinweis ragt unten aus der Karte");

        // 21b: Die Überschrift der Hilfe steht in der Größe der Zwischentitel (16), und zwischen Antworttext und Überschrift liegt keine leere Fläche.
        var heading = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Classes.Contains("h3"));
        Assert.Equal(16, heading.FontSize);
        var answerText = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == shell.Companion.Answer.Text);
        var gap = BoundsIn(heading, window).Top - BoundsIn(answerText, window).Bottom;
        Assert.InRange(gap, 0, 16);

        // 21c: Die Frage steht einmal im Panel (in der Karte), nicht zusätzlich unter „Zuletzt gefragt“.
        var shown = panel.GetVisualDescendants().Count(v => v is TextBlock { IsEffectivelyVisible: true } t && t.Text == Question);
        Assert.Equal(1, shown);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == Question);

        // 21d: „Ganze Beschreibung öffnen“ und „Neu beginnen“ sind Knöpfe mit Rand, kein Text ohne Form.
        Assert.DoesNotContain("ghost", Named<Button>(panel, "OpenWholeTopic").Classes);
        var reset = ResetButton(window);
        Assert.True(reset.IsEffectivelyVisible);
        Assert.DoesNotContain("ghost", reset.Classes);
        return calendar;
    }, Api().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Reception,
        "So geht es in der Kassa:", [], [],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.",
        [new AssistantHelpLink("seite-kassa", "kassieren", "Kassieren", "Termin wählen und kassieren."),
            new AssistantHelpLink("seite-kassa", "abschluss", "Kassenlade und Abschluss", "Am Abend abschließen.")])));

    // ---------- 13: Kundenübersicht ----------

    [Fact]
    public Task Die_Kundenuebersicht_traegt_den_vollen_Namen_beschriftete_Notizen_und_endet_vor_der_Leiste() => Render("hell", "durchsicht2-13-kundenuebersicht", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        var avatar = window.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null && b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Kundenübersicht");
        ClickCenter(window, avatar);
        await Pump(Settling);

        // 13a: Titel der Übersicht und Name im Kopf der Leiste sind derselbe volle Name, der Rufname steht darunter.
        var overview = calendar.Selected!.Overview;
        Assert.Equal(calendar.Selected.Title, overview.Name);
        Assert.Equal("Rufname Gitti", overview.PreferredNameText);

        var flyout = Assert.IsType<Flyout>(avatar.Flyout);
        var content = Assert.IsAssignableFrom<Control>(flyout.Content);
        var texts = content.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)).ToList();
        Assert.Contains(texts, t => t.Text == "Brigitte Müller" && t.Classes.Contains("h2"));

        // 13b: Die Kundennotiz trägt eine Bezeichnung und einen zugänglichen Namen.
        Assert.Contains(texts, t => t.Text == "Notiz zum Gast" && t.IsEffectivelyVisible);
        var hint = content.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "GuestNoteHint");
        Assert.Equal("Notiz zum Gast: Stammkundin, mag Lavendelöl", AutomationProperties.GetName(hint));
        Assert.Equal("Notiz zum Gast", ToolTip.GetTip(hint));

        // 13c: Die Karte endet links von der Seitenleiste und deckt ihren Rand nicht zu.
        var panelLeft = SidePanelOf(window).PointToScreen(default).X;
        var cardRight = content.PointToScreen(new Point(content.Bounds.Width, 0)).X;
        Assert.True(cardRight <= panelLeft, $"Die Übersicht endet bei {cardRight}, die Seitenleiste beginnt bei {panelLeft}");

        // 13b: Die Terminnotiz im Panel trägt ebenfalls eine Bezeichnung.
        flyout.Hide();
        await Pump();
        var note = Named<Border>(window, "AppointmentNoteHint");
        Assert.Equal("Notiz zum Termin", Named<TextBlock>(window, "AppointmentNoteLabel").Text);
        Assert.StartsWith("Notiz zum Termin: ", AutomationProperties.GetName(note), StringComparison.Ordinal);
        Assert.Equal("Notiz zum Termin", ToolTip.GetTip(note));
        return calendar;
    }, WithGuestOverview(Api()));

    [Fact]
    public Task Bei_offenen_Kundendaten_im_Panel_stehen_die_Chips_nicht_da() => Render("hell", "durchsicht2-14-kundendaten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        var chips = Named<Border>(window, "AppointmentChips");
        Assert.True(chips.IsEffectivelyVisible);

        calendar.Selected!.EditCustomerCommand.Execute(null);
        await Pump(Settling);

        Assert.False(chips.IsEffectivelyVisible, "Die Chips stehen bei offenen Kundendaten noch da");
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Selected.CustomerId}", SpaSample.Customers[0] with { Id = SpaSample.Selected.CustomerId }));

    // ---------- 16: Anamnese ----------

    [Fact]
    public Task Der_Stand_der_Anamnese_steht_als_Chip_oben_und_der_Text_der_Karte_buendig_zur_Ueberschrift() => Render("hell", "durchsicht2-16-anamnese", async (window, main) =>
    {
        var calendar = await OpenSelected(main);
        var chip = Named<Border>(window, "AnamnesisChip");
        Assert.True(chip.IsEffectivelyVisible);
        Assert.Equal("Anamnese liegt vor", chip.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        // Nur der Stand, keine Antworten (Gesundheitsdaten).
        Assert.DoesNotContain("Nüsse", string.Join(" ", chip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)), StringComparison.Ordinal);
        Assert.Contains(Named<Border>(window, "AppointmentChips"), chip.GetVisualAncestors());

        var card = Named<Border>(window, "AnamnesisCard");
        var texts = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        var title = texts.First(t => t.Text == "Anamnese");
        var state = texts.First(t => t.Text?.StartsWith("Liegt vor", StringComparison.Ordinal) == true);
        Assert.Equal(BoundsIn(title, window).Left, BoundsIn(state, window).Left, 0.5);
        return calendar;
    }, Api().On($"GET {AnamnesisRoute}/overview", SheetOnFile()));

    // ---------- 17: Meldungen ----------

    [Fact]
    public Task Meldungen_zeigen_den_Chip_nur_wenn_er_mehr_sagt_als_der_Titel_und_heissen_Ausblenden() => Render("hell", "durchsicht2-17-meldungen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);

        var cards = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("panelcard") && b.DataContext is MessageItem && b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(cards);
        foreach (var card in cards)
        {
            var item = (MessageItem)card.DataContext!;
            var kind = card.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("pill") && b.IsEffectivelyVisible);
            Assert.Equal(item.ShowKind, kind is not null);
            if (kind is not null)
            {
                Assert.NotEqual(item.Title, kind.GetVisualDescendants().OfType<TextBlock>().Single().Text);
            }

            Assert.DoesNotContain(card.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Wegklicken");
        }

        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Ausblenden");
        Assert.Contains(calendar.Messages, m => !m.ShowKind);
        Assert.Contains(calendar.Messages, m => m.ShowKind);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Die_Glocke_im_Kopf_erklaert_Zahl_und_Notizsymbol_im_Hinweis_und_im_Namen() => Render("hell", "durchsicht2-17c-glocke", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var bell = Named<Button>(window, "HeadMessages");
        Assert.True(calendar.HasDayNotes && calendar.MessageCount > 0);
        var tip = Assert.IsType<string>(ToolTip.GetTip(bell));
        Assert.Contains("Die Zahl zählt die offenen Hinweise", tip, StringComparison.Ordinal);
        Assert.Contains("Das Notizsymbol steht für", tip, StringComparison.Ordinal);
        var name = AutomationProperties.GetName(bell);
        Assert.Contains($"Meldungen {calendar.MessageCount}", name, StringComparison.Ordinal);
        Assert.Contains("Notiz", name, StringComparison.Ordinal);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Die_Detailansicht_einer_Meldung_traegt_den_Titel_der_Meldung_und_der_erledigte_Punkt_bleibt_lesbar() => Render("hell", "durchsicht2-18-detail", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump();
        var card = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("messagecard") && b.DataContext is MessageItem { Kind: MessageKind.DayStart });
        card.BringIntoView();
        await Pump();
        ClickCenter(window, card);
        await Pump(Settling);

        var title = calendar.MessageDetail!.Item.Title;
        var header = Named<SidePanelHeader>(window, "PanelHeader");
        Assert.True(header.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == title && t.IsEffectivelyVisible), $"Titel der Leiste: {title}");
        // 18a: Die Reiter heißen verschieden.
        Assert.Equal([title, "Meldungen"], calendar.OpenPanels.Select(t => t.Text));

        // 18c: Eine erledigte Zeile steht in fg-2 und ohne Deckkraft 0,6 (Stil „done“).
        var stack = SidePanelOf(window).GetVisualDescendants().OfType<StackPanel>().First(s => s.IsEffectivelyVisible && s.Children.Count > 0);
        var probe = new TextBlock { Text = "erledigt", Classes = { "done" } };
        stack.Children.Add(probe);
        await Pump();
        Assert.Equal(1.0, probe.Opacity);
        Assert.True(window.TryFindResource("Fg2Brush", window.ActualThemeVariant, out var fg2));
        Assert.Equal(((ISolidColorBrush)fg2!).Color, ((ISolidColorBrush)probe.Foreground!).Color);
        stack.Children.Remove(probe);
        return calendar;
    }, OperationsApi());

    // ---------- 19: Warteliste ----------

    [Fact]
    public Task Die_Warteliste_beginnt_mit_einem_Satz_mit_Fragezeichen_und_zeigt_die_Karten_dichter() => Render("hell", "durchsicht2-19-warteliste", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenWaitlistCommand.Execute(null);
        await Pump(Settling);

        var intro = Named<TextBlock>(window, "WaitlistIntro");
        Assert.Equal("Wird etwas frei, schreibt AISPA Studio den Wartenden selbst.", intro.Text);
        var help = SidePanelOf(window).GetVisualDescendants().OfType<HelpButton>().Single(b => b.Topic == "kalender-warteliste");
        Assert.Equal("uebersicht", help.Section);

        var cards = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("panelcard") && b.DataContext is WaitlistRow && b.IsEffectivelyVisible).ToList();
        Assert.Equal(4, cards.Count);
        var scroll = Named<ScrollViewer>(window, "PanelScroll");
        var view = BoundsIn(scroll, window);
        // Vier Wartende ohne Rollen sichtbar (vorher drei) und jede Aktion bleibt da.
        var visible = cards.Count(c => BoundsIn(c, window).Bottom <= view.Bottom);
        Assert.True(visible >= 4, $"Nur {visible} Karten sichtbar; Karten: {string.Join(", ", cards.Select(c => c.Bounds.Height))}");
        foreach (var card in cards)
        {
            var buttons = card.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).ToList();
            Assert.Contains("Freie Zeiten zeigen", buttons);
            Assert.Contains("Anfrage abschließen …", buttons);
        }

        return calendar;
    });

    // ---------- 20: Lücken schließen ----------

    [Fact]
    public Task Die_Wahl_eines_Lueckenvorschlags_hebt_den_Termin_hervor_und_zeigt_das_Ziel_als_Vorschau() => Render("hell", "durchsicht2-20-luecken", async (window, main) =>
    {
        var calendar = await OpenGaps(main);
        var item = calendar.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == SpaSample.Selected.Id);
        Assert.False(item.IsGapHighlighted);
        Assert.Null(calendar.Ghost);

        await ClickVisible(window, "SelectGapButton");

        Assert.True(item.IsGapHighlighted);
        var ghost = Assert.IsType<CalendarGhost>(calendar.Ghost);
        Assert.StartsWith("10:10", ghost.Text, StringComparison.Ordinal);
        var expectedTop = calendar.Hours.Single(h => h.Label == "10:00").Top + 10 * calendar.Scale.PixelsPerMinute;
        Assert.Equal(expectedTop, ghost.Top, 1.5);
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "GapCard" && b.Classes.Contains("gapselected"));
        Assert.Contains(window.GetVisualDescendants().OfType<AppointmentCard>(), c => c.DataContext is ViewModels.CalendarItem { IsGapHighlighted: true } && c.Classes.Contains("gapmark"));

        // Ein zweiter Klick hebt die Wahl auf.
        await ClickVisible(window, "SelectGapButton");
        Assert.False(item.IsGapHighlighted);
        Assert.Null(calendar.Ghost);

        await ClickVisible(window, "SelectGapButton");
        Assert.True(item.IsGapHighlighted);
        return calendar;
    }, Api().On($"GET {GapsRoute}", new List<GapSuggestion> { SampleGap() }));

    [Fact]
    public Task Bei_einer_Kette_leuchten_alle_Termine_und_jeder_hat_sein_Ziel() => Render("hell", "durchsicht2-20-kette", async (window, main) =>
    {
        var calendar = await OpenGaps(main);
        await ClickVisible(window, "SelectGapButton");

        var marked = calendar.Columns.SelectMany(c => c.Items).Where(i => i.IsGapHighlighted).Select(i => i.Id).ToList();
        Assert.Contains(SpaSample.Selected.Id, marked);
        Assert.Contains(BauerId, marked);
        Assert.NotNull(calendar.Ghost);
        Assert.Single(calendar.GroupGhosts);

        // Wer die Seite schließt, nimmt Hervorhebung und Vorschau mit.
        calendar.GapsPanel!.CloseCommand.Execute(null);
        await Pump();
        Assert.DoesNotContain(calendar.Columns.SelectMany(c => c.Items), i => i.IsGapHighlighted);
        Assert.Null(calendar.Ghost);
        Assert.Empty(calendar.GroupGhosts);
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", ChainDay).On($"GET {GapsRoute}", new List<GapSuggestion> { SampleChain() with { Chain = [new(BauerId, "Dr. Wolfgang Bauer", "Ayurveda Abhyanga", SpaSample.At(12), SpaSample.At(11, 40))] } }));

    /// <summary>Der Termin von Dr. Bauer im Beispieltag: Er rückt in der Kette mit.</summary>
    private static readonly CalendarDay ChainDay = SpaSample.CalendarDay();

    private static readonly Guid BauerId = ChainDay.Appointments.First(a => a.CustomerName.Contains("Bauer", StringComparison.Ordinal)).Id;

    // ---------- 22: Tastenhilfe ----------

    [Fact]
    public Task Die_Tastenhilfe_steht_ganz_im_Kalender_mit_Luft_unten_und_Komma_und_Punkt_ausgeschrieben() => Render("hell", "durchsicht2-22-tastenhilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ShowKeyHelp = true;
        await Pump(Settling);

        var card = Named<Border>(window, "KeyHelp");
        var link = Named<Button>(window, "KeyManualLink");
        var cardBox = BoundsIn(card, window);
        var linkBox = BoundsIn(link, window);
        var host = BoundsIn(card.GetVisualAncestors().OfType<Panel>().First(), window);
        Assert.True(cardBox.Bottom <= host.Bottom - 4, $"Die Karte endet bei {cardBox.Bottom}, der Kalender bei {host.Bottom}");
        Assert.True(cardBox.Top >= host.Top + 4, $"Die Karte beginnt bei {cardBox.Top}, der Kalender bei {host.Top}");
        // Innenabstand unten: der Link sitzt nicht auf dem Rand der Karte.
        Assert.True(linkBox.Bottom <= cardBox.Bottom - card.Padding.Bottom + 1, $"Link endet bei {linkBox.Bottom}, Karte bei {cardBox.Bottom}");
        Assert.True(TextFits(link.GetVisualDescendants().OfType<TextBlock>().First()), "Der Link zum Handbuch ist abgeschnitten");

        // 22b: Komma und Punkt stehen ausgeschrieben auf den Kappen.
        var caps = card.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("kbd")).Select(b => b.GetVisualDescendants().OfType<TextBlock>().Single().Text).ToList();
        Assert.Contains("Komma", caps);
        Assert.Contains("Punkt", caps);
        Assert.DoesNotContain(",", caps);
        Assert.DoesNotContain(".", caps);
        return calendar;
    });

    [Fact]
    public Task Die_Tastenhilfe_rollt_in_einem_niedrigen_Fenster_und_der_Fuss_bleibt_ganz() => Render("hell", "durchsicht2-22-niedrig", async (window, main) =>
    {
        window.Height = 560;
        var calendar = await SignedInCalendar(main);
        calendar.ShowKeyHelp = true;
        await Pump(Settling);

        var card = Named<Border>(window, "KeyHelp");
        var link = Named<Button>(window, "KeyManualLink");
        var host = BoundsIn(card.GetVisualAncestors().OfType<Panel>().First(), window);
        Assert.True(BoundsIn(card, window).Bottom <= host.Bottom + 0.5, "Die Karte ragt unten aus dem Kalender");
        Assert.True(BoundsIn(link, window).Bottom <= BoundsIn(card, window).Bottom - card.Padding.Bottom + 1, "Der Fuß der Tastenhilfe ist angeschnitten");
        return calendar;
    });

    // ---------- 24: Notizen, Pausen ----------

    [Fact]
    public Task Notiz_und_Pause_nennen_Beginn_und_Ende_gleich_und_der_Chip_Alle_zeigen_hat_Namen_und_Hinweis() => Render("hell", "durchsicht2-24-notiz", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenNoteAt(DayLayout.ColumnLeft(2, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "16:00").Top + 4);
        await Pump(Settling);
        Assert.NotNull(calendar.Entry);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Beginn" && t.IsEffectivelyVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Ende" && t.IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text is "Uhrzeit" or "Bis" && t.IsEffectivelyVisible);

        // 24b: Der Chip mit dem Personensymbol und der Zahl hat einen Namen mit Wort und einen Hinweis.
        var chip = Named<Button>(window, "HeadStaffFilter");
        Assert.True(chip.IsEffectivelyVisible);
        Assert.Matches("Alle zeigen", AutomationProperties.GetName(chip));
        Assert.Contains("ohne Dienst", Assert.IsType<string>(ToolTip.GetTip(chip)), StringComparison.Ordinal);
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay()));

    // ---------- 25: Paar oder Spa-Tag ----------

    [Fact]
    public Task Paar_oder_Spa_Tag_Vorschau_folgt_der_Zeit_Felder_zeigen_ihren_Text_ganz_und_die_Pause_sagt_wozwischen() => Render("hell", "durchsicht2-25-gruppe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        group.Steps[0].Entries[0].Choice = group.Steps[0].Entries[0].Choices[2];
        group.Steps[0].Entries[1].Choice = group.Steps[0].Entries[1].Choices[2];
        group.AddStepCommand.Execute(null);
        group.Steps[1].Entries[0].Choice = group.Steps[1].Entries[0].Choices[6];
        group.Steps[1].Entries[1].Choice = group.Steps[1].Entries[1].Choices.First(c => c.AreaId == SpaSample.Relax.Id && c.Minutes == 60);
        group.Gap = 10;
        await group.SearchSlotsAsync();
        group.Guests[0].Search.Query = "br";
        group.Guests[1].Search.Query = "be";
        await Pump();
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];
        await Pump(Settling);

        // 25a: Die Vorschau steht an der gewählten Zeit, bei jeder Wahl.
        foreach (var slot in group.Slots.Take(3))
        {
            group.Slot = slot;
            await Pump(Settling);
            var first = group.PreviewParts.First(p => p.EmployeeId is not null);
            var local = DayLayout.Local(first.Start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"));
            var expected = calendar.Hours.Single(h => h.Label == $"{local:HH}:00").Top + local.Minute * calendar.Scale.PixelsPerMinute;
            var shown = new[] { calendar.Ghost }.Concat(calendar.GroupGhosts).OfType<CalendarGhost>().Select(g => g.Top);
            Assert.Contains(shown, top => Math.Abs(top - expected) < 1.5);
            Assert.Contains($"{local:HH:mm}", slot.Text, StringComparison.Ordinal);
        }

        // 25b: Die Auswahlfelder des zweiten Abschnitts zeigen ihren Text ganz; der Name steht darüber.
        var panel = SidePanelOf(window);
        var boxes = panel.GetVisualDescendants().OfType<ComboBox>().Where(b => b.Name == "StepTreatmentBox" && b.IsEffectivelyVisible).ToList();
        Assert.Equal(2, boxes.Count);
        foreach (var box in boxes)
        {
            box.BringIntoView();
            await Pump();
            var shown = box.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text));
            Assert.True(TextFits(shown), $"„{shown.Text}“ ist abgeschnitten ({shown.Bounds.Width} px)");
            var label = box.GetVisualAncestors().OfType<FormField>().First().GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text));
            Assert.True(BoundsIn(label, window).Bottom <= BoundsIn(box, window).Top + 1, "Der Name steht nicht über dem Feld");
            Assert.True(TextFits(label), $"Name „{label.Text}“ abgeschnitten");
        }

        // 25c: Die Pause nennt, wozwischen sie liegt.
        var gap = Named<TextBlock>(window, "GroupGapLabel");
        Assert.Contains("zwischen den Abschnitten", gap.Text, StringComparison.Ordinal);
        Assert.True(TextFits(gap), "Beschriftung der Pause abgeschnitten");
        return calendar;
    });

    // ---------- 26: Raumcheck ----------

    [Fact]
    public Task Der_Raumcheck_nennt_den_Grund_fuer_den_gesperrten_Knopf_und_Aufgefuellt_ist_neutral() => Render("hell", "durchsicht2-26-raumcheck", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();
        var stripe = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible && b.DataContext is CalendarStripe { Care.State: RoomCareState.Overdue });
        var at = stripe.TranslatePoint(new Point(stripe.Bounds.Width / 2, stripe.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump(Settling);

        // 26a: Der Grund steht sichtbar unter dem gesperrten Knopf.
        var finish = Named<Button>(window, "FinishRoomCareButton");
        Assert.False(finish.IsEffectivelyEnabled);
        var hint = Named<TextBlock>(window, "FinishRoomCareHint");
        Assert.True(hint.IsEffectivelyVisible);
        Assert.Equal("Erst jede Zeile mit „Aufgefüllt“ oder „Reicht noch“ beantworten.", hint.Text);
        Assert.True(BoundsIn(hint, window).Top >= BoundsIn(finish, window).Bottom - 8);

        // 26b: „Aufgefüllt“ ist neutral wie „Reicht noch“, ungewählt nicht grün getönt.
        var refill = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "RefillButton" && b.IsEffectivelyVisible);
        Assert.DoesNotContain("ok", refill.Classes);
        var enough = window.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Reicht noch" && b.IsEffectivelyVisible);
        Assert.Equal(enough.Classes.Where(c => c is not (":pointerover" or ":focus")).OrderBy(c => c), refill.Classes.Where(c => c is not (":pointerover" or ":focus")).OrderBy(c => c));
        return calendar;
    }, OperationsApi().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", BusyDayWithRoomCheck()));
}

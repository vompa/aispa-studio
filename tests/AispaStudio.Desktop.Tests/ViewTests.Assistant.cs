using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „KI und Hilfe“ zeigt die Antworten des Assistenten aus den Daten des Spas (ADR 0072): Satz, Tabelle, Quelle, Verweis, Kopieren und die Kennzeichnung der KI mit
/// „Warum diese Antwort?“. Bilder für das Handbuch und die PRs, wenn AISPA_SCREENSHOTS gesetzt ist.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid AssistantLocation = SpaSample.LocationId;

    private static AssistantAnswer BlockReply(AssistantBlock block) =>
        new(AssistantAudience.Management, block.Text, [], [],
            "Antwort eines KI-Assistenten. Die Angaben stammen aus den Daten des Spas (Quelle unten), nicht von der KI; bei Zweifeln bitte dort nachsehen.", [], block);

    private static readonly AssistantBlock HoursBlock = new(AssistantBlockKind.Answer, "OpeningHours",
        "Am Samstag, 24.10.2026 ist von 09:00 bis 20:00 Uhr geöffnet (11 Stunden).", null, "Öffnungszeiten des Standorts Wien Innere Stadt", null, null, null, []);

    private static readonly AssistantBlock ContactBlock = new(AssistantBlockKind.Answer, "Contact",
        "Kontaktdaten des Standorts Wien Innere Stadt: Telefon +43 1 5120000; E-Mail wien@spa.example; Adresse Graben 1, 1010 Wien.",
        new AssistantTable(["Angabe", "Wert"], [["Telefon", "+43 1 5120000"], ["E-Mail", "wien@spa.example"], ["Adresse", "Graben 1, 1010 Wien"]]),
        "Kontaktdaten des Standorts Wien Innere Stadt", null, "Wien Innere Stadt\nTelefon: +43 1 5120000\nE-Mail: wien@spa.example\nAdresse: Graben 1, 1010 Wien", null, []);

    private static readonly AssistantBlock BusinessContactBlock = new(AssistantBlockKind.Answer, "BusinessContacts",
        "Installateur: Haustechnik Sommer GmbH, Ansprechperson Franz Sommer. Telefon +43 1 234 56 10; Mobil +43 664 234 56 10; E-Mail office@haustechnik-sommer.example. "
        + "Zuständig für: Wasserschaden, Heizung, Warmwasser.",
        new AssistantTable(["Angabe", "Wert"],
        [
            ["Bezeichnung", "Installateur"], ["Firma", "Haustechnik Sommer GmbH"], ["Ansprechperson", "Franz Sommer"], ["Telefon", "+43 1 234 56 10"],
            ["Mobil", "+43 664 234 56 10"], ["E-Mail", "office@haustechnik-sommer.example"], ["Zuständig für", "Wasserschaden, Heizung, Warmwasser"],
        ]),
        "Betriebsinformationen",
        new AssistantLink(AssistantLinkKind.Setting, "Betriebsinformationen öffnen", "businessInfo", null, AssistantLocation),
        "Bezeichnung: Installateur\nFirma: Haustechnik Sommer GmbH\nAnsprechperson: Franz Sommer\nTelefon: +43 1 234 56 10\nMobil: +43 664 234 56 10\nE-Mail: office@haustechnik-sommer.example\nZuständig für: Wasserschaden, Heizung, Warmwasser",
        null, []);

    private static readonly AssistantBlock ReportBlock = new(AssistantBlockKind.Answer, "TopTreatments",
        "Am häufigsten gebucht, Oktober 2026: 1. Klassische Massage (42), 2. Hot Stone Massage (27), 3. Gesichtsbehandlung (19).",
        new AssistantTable(["Platz", "Behandlung", "Termine", "Umsatz netto"],
        [
            ["1", "Klassische Massage", "42", "2.756,25 EUR"], ["2", "Hot Stone Massage", "27", "2.700,00 EUR"], ["3", "Gesichtsbehandlung", "19", "1.140,00 EUR"],
        ]),
        "Management-Bericht des Standorts Wien Innere Stadt, Oktober 2026",
        new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "reports", "management", AssistantLocation, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22)), null,
        "Oktober 2026", []);

    private static readonly AssistantBlock DeniedBlock = new(AssistantBlockKind.Denied, "RevenueByTreatment",
        "Dafür fehlt die Berechtigung. Umsätze sehen Inhaber, Manager und Backoffice sowie Personen mit dem Recht „Berichte mit Beträgen“. Die Auswertung ohne Beträge nach Anzahl der Termine ist möglich.",
        null, "Berechtigungen dieses Zugangs", null, null, null, [new AssistantFollowUp("Behandlungen nach Anzahl zeigen", "Top-Behandlungen von 01.09.2026 bis 30.09.2026")]);

    /// <summary>Öffnet die Leiste, stellt die Frage und prüft Pille, Quelle und Fuß „Warum diese Antwort?“ mit der Quelle.</summary>
    private static async Task<CompanionContent> AskInPanel(Window window, MainViewModel main, string question, string name, string theme, AssistantBlock block)
    {
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await shell.Companion.AskExampleCommand.ExecuteAsync(question);
        await Pump(Settling);

        var panel = window.GetVisualDescendants().OfType<CompanionContent>().Single();
        Assert.Equal(2, Pills(panel).Count);
        Assert.Contains(block.Text, TextsIn(panel));
        if (block.Source.Length > 0)
        {
            Assert.Contains($"Quelle: {block.Source}", TextsIn(panel));
        }

        var footer = Footers(panel).Single();
        Assert.Equal(AiLabels.WhyAnswerTitle, footer.WhyTitle);
        Assert.Contains(footer.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == AiLabels.WhyAnswerTitle);
        Assert.Equal(block.Kind == AssistantBlockKind.Answer ? AiLabels.WhyBlock(block.Source) : AiLabels.WhyBlock(null), footer.Why);
        var why = footer.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "WhyToggle");
        why.IsChecked = true;
        await Pump();
        Assert.Contains(footer.Why!, TextsIn(footer));
        panel.GetVisualAncestors().OfType<ScrollViewer>().First().ScrollToEnd();
        await Pump(Settling);
        SaveShot(window, name, theme);
        return panel;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_beantwortet_die_Oeffnungszeiten_mit_Quelle(string theme) => Render(theme, "ki-betrieb-oeffnungszeiten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var panel = await AskInPanel(window, main, "Wie lange haben wir heute offen?", "ki-betrieb-oeffnungszeiten", theme, HoursBlock);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<AnswerTable>(), t => t.IsEffectivelyVisible);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Kopieren");
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(HoursBlock)));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_zeigt_Kontaktdaten_als_Tabelle_mit_Kopieren(string theme) => Render(theme, "ki-betrieb-kontakt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var panel = await AskInPanel(window, main, "Unsere Kontaktdaten", "ki-betrieb-kontakt", theme, ContactBlock);

        // Zwei Spalten stehen als Beschriftung links und Wert rechts.
        var table = panel.GetVisualDescendants().OfType<AnswerTable>().Single(t => t.IsEffectivelyVisible);
        Assert.Contains("+43 1 5120000", TextsIn(table));
        Assert.Contains("Graben 1, 1010 Wien", TextsIn(table));
        var copy = VisibleButton(panel, "Kopieren");
        Assert.Equal("Wien Innere Stadt\nTelefon: +43 1 5120000\nE-Mail: wien@spa.example\nAdresse: Graben 1, 1010 Wien", copy.Tag);
        Assert.Null(shell.Companion.CopiedText);
        shell.Companion.MarkCopied();
        await Pump();
        Assert.Contains("Kopiert.", TextsIn(panel));
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(ContactBlock)));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_nennt_einen_Kontakt_des_Betriebs_mit_Kopieren_und_Link_zu_den_Betriebsinformationen(string theme) => Render(theme, "ki-betrieb-kontakte-nach-link", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var panel = await AskInPanel(window, main, "Wer ist unser Installateur?", "ki-betrieb-kontakte", theme, BusinessContactBlock);

        // Die Angaben stehen als Beschriftung links und Wert rechts, Telefon und Firma lesbar, mit Quelle „Betriebsinformationen“.
        var table = panel.GetVisualDescendants().OfType<AnswerTable>().Single(t => t.IsEffectivelyVisible);
        Assert.Contains("+43 1 234 56 10", TextsIn(table));
        Assert.Contains("Haustechnik Sommer GmbH", TextsIn(table));
        Assert.Contains("Quelle: Betriebsinformationen", TextsIn(panel));
        var copy = VisibleButton(panel, "Kopieren");
        Assert.StartsWith("Bezeichnung: Installateur\nFirma: Haustechnik Sommer GmbH", (string)copy.Tag!, StringComparison.Ordinal);
        Invoke(VisibleButton(panel, "Betriebsinformationen öffnen"));
        await Pump(Settling);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.Same(shell.AdminPage.Reports, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Reports!.IsBusinessInfoTab);
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(BusinessContactBlock)));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_zeigt_eine_Auswertung_als_Liste_mit_Zeitraum_und_Link_zum_Bericht(string theme) => Render(theme, "ki-betrieb-auswertung-nach-link", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var panel = await AskInPanel(window, main, "Top-Behandlungen diesen Monat", "ki-betrieb-auswertung", theme, ReportBlock);

        // Mehrspaltig steht jede Zeile als Block: die erste Zelle fett, darunter die übrigen mit ihrer Spalte.
        var table = panel.GetVisualDescendants().OfType<AnswerTable>().Single(t => t.IsEffectivelyVisible);
        Assert.Contains("1. Klassische Massage", TextsIn(table));
        Assert.Contains("Umsatz netto: 2.756,25 EUR", TextsIn(table));
        Assert.Contains("Termine: 27", TextsIn(table));
        Invoke(VisibleButton(panel, "Im Bericht öffnen"));
        await Pump(Settling);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.Same(shell.AdminPage.Reports, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Reports!.IsManagementTab);
        Assert.Equal(new DateOnly(2026, 10, 1), shell.AdminPage.Reports.Management.From);
        Assert.Equal(new DateOnly(2026, 10, 22), shell.AdminPage.Reports.Management.Until);
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(ReportBlock)));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_sagt_bei_fehlender_Berechtigung_was_moeglich_ist_und_bietet_die_Auswertung_ohne_Betraege_an(string theme) => Render(theme, "ki-betrieb-rechte-nach-angebot", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var panel = await AskInPanel(window, main, "Umsatz nach Behandlung im September", "ki-betrieb-rechte", theme, DeniedBlock);

        // Quelle sind die Rechte des Zugangs (keine Daten des Spas); das Angebot ist ein Knopf, der die Frage stellt.
        Assert.Contains("Quelle: Berechtigungen dieses Zugangs", TextsIn(panel));
        Assert.DoesNotContain("stammen aus", shell.Companion.WhyText, StringComparison.Ordinal);
        var offer = VisibleButton(panel, "Behandlungen nach Anzahl zeigen");
        Invoke(offer);
        await Pump();
        Assert.Equal("Top-Behandlungen von 01.09.2026 bis 30.09.2026", shell.Companion.Asked);
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(DeniedBlock)));

    private static readonly AssistantBlock GapBlock = new(AssistantBlockKind.Answer, "Contact",
        "Kontaktdaten des Standorts Wien Innere Stadt: Telefon +43 1 5120000; E-Mail wien@spa.example; Adresse Graben 1, 1010 Wien.",
        new AssistantTable(["Angabe", "Wert"], [["Telefon", "+43 1 5120000"], ["E-Mail", "wien@spa.example"], ["Adresse", "Graben 1, 1010 Wien"]]),
        "Kontaktdaten des Standorts Wien Innere Stadt", null, null, null, [],
        [
            new AssistantIncomplete("Unvollständig: Anreise fehlt beim Standort Wien Innere Stadt.", "Stammdaten › Standorte › Anreise und Parkplätze", ["location.directions"],
                new AssistantLink(AssistantLinkKind.Setting, "Jetzt ergänzen", "masterData", "locations", AssistantLocation, Field: "location.directions"), null),
            new AssistantIncomplete("Unvollständig: Website fehlt in den Firmendaten.", "Stammdaten › Firmendaten › Sitz und Kontakt", ["company.website"], null, "Nur Leitung"),
        ]);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_nennt_fehlende_Angaben_mit_Ort_Link_und_Hinweis_Nur_Leitung(string theme) => Render(theme, "ki-betrieb-unvollstaendig-verwaltung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        // Das Handbuchbild entsteht hier; das Bild am Ende (nach dem Sprung in die Verwaltung, die im Test nicht erreichbar ist) hat einen eigenen Namen.
        var panel = await AskInPanel(window, main, "Unsere Kontaktdaten", "ki-betrieb-unvollstaendig", theme, GapBlock);

        // Zwei Hinweisflächen unter der Quelle (warn); der Knopf steht unter dem Text, nicht daneben, weil die Leiste schmal ist.
        var hints = panel.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("hint") && b.Classes.Contains("warn") && b.IsEffectivelyVisible && b.DataContext is CompanionGap).ToList();
        Assert.Equal(2, hints.Count);
        Assert.Contains("Unvollständig: Anreise fehlt beim Standort Wien Innere Stadt. Zu ergänzen unter Stammdaten › Standorte › Anreise und Parkplätze.", TextsIn(panel));
        Assert.Contains("Unvollständig: Website fehlt in den Firmendaten. Zu ergänzen unter Stammdaten › Firmendaten › Sitz und Kontakt. Nur Leitung.", TextsIn(panel));
        // Der Knopf „Jetzt ergänzen“ gehört nur zur Meldung, bei der die Person das Recht hat.
        Assert.Single(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Jetzt ergänzen");
        Invoke(VisibleButton(panel, "Jetzt ergänzen"));
        await Pump(Settling);
        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        // Standorte & Räume sind nativ (R05 Slice 95): die Maske wählt den Standort und setzt den Fokus ins Feld Anreise.
        Assert.Equal("Standorte & Räume", shell.AdminPage.SelectedArea?.Title);
        Assert.Equal("Wien Innere Stadt", shell.AdminPage.Locations!.SelectedChoice?.Title);
        Assert.Same(Named<TextBox>(window, "DirectionsField"), Focused(window));

        // Die Verwaltung hat keine Reiterschiene: der Assistent steht dort in der Leiste der Shell (ein neuer Inhalt, derselbe Stand).
        panel = window.GetVisualDescendants().OfType<CompanionContent>().Single();

        // Rückweg: speichert die Maske etwas, meldet es die Leiste und bietet an, die Frage zu wiederholen.
        Assert.DoesNotContain(TextsIn(panel), t => t.Contains("Daten gespeichert", StringComparison.Ordinal));
        await TypeInto(window, "DirectionsField", "U1 bis Stephansplatz, Garage Am Graben");
        PressSave(window);
        await Pump(Settling);
        Assert.Contains("Daten gespeichert. Frage wiederholen?", TextsIn(panel));
        Invoke(VisibleButton(panel, "Frage wiederholen"));
        await Pump(Settling);
        Assert.Null(shell.Companion.SavedNotice);
        Assert.DoesNotContain(TextsIn(panel), t => t.Contains("Daten gespeichert", StringComparison.Ordinal));
        Assert.Equal("Unsere Kontaktdaten", shell.Companion.Asked);
        return calendar;
    }, Api().On("POST /api/assistant", BlockReply(GapBlock)).OnJson($"PUT /api/locations/{SpaSample.LocationId}", body =>
    {
        var input = System.Text.Json.JsonSerializer.Deserialize<LocationInput>(body!, ApiClient.Json)!;
        return new LocationDto(SpaSample.LocationId, input.Name, input.TimeZone, input.Currency, input.OpeningHours, input.Region, input.Contact, input.Company);
    }));

    [Fact]
    public Task Das_Fragefeld_nennt_Beispiele_im_Platzhalter() => Render("hell", "ki-platzhalter", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await Pump(Settling);

        var question = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "CompanionQuestion");
        Assert.Equal("Frage eingeben", question.PlaceholderText);
        Assert.Equal(AiLabels.QuestionPlaceholder, question.PlaceholderText);
        // Vor der ersten Frage steht, was der Assistent beantwortet und was nicht.
        Assert.Contains(AiLabels.AssistantScope, TextsIn(window));
        return calendar;
    }, Api());

    /// <summary>Handbuchbild zu Ä387: ein Auftrag „buche …“ öffnet erst nach dem Klick „Neuer Termin“ vorausgefüllt, mit dem Hinweis über „Termin buchen“.</summary>
    [Fact]
    public Task KI_und_Hilfe_Auftrag_oeffnet_Neuer_Termin_vorausgefuellt_mit_Hinweis() => Render("hell", "ki-auftrag-buchen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Buche Frau Müller heute 16 Uhr Massage");
        var order = Assert.Single(shell.Companion.Suggestions);
        Assert.StartsWith("Öffnet „Neuer Termin“ vorausgefüllt", order.Action, StringComparison.Ordinal);
        Assert.Null(calendar.Booking);

        await shell.Companion.UseSuggestionCommand.ExecuteAsync(order);
        await Pump(Settling);

        Assert.Null(calendar.Error);
        var booking = Assert.IsType<BookingViewModel>(calendar.Booking);
        Assert.Equal(SpaSample.Customers[0].Id, booking.Customer?.Id);
        Assert.Equal("16:00", booking.StartText);
        Assert.Equal(CalendarViewModel.AssistantBookingNote, booking.AssistantNote);

        // Der Hinweis steht am Ende des scrollenden Inhalts, „Termin buchen“ fest im Fuß darunter (A4); fürs Bild den Hinweis in den sichtbaren Bereich holen.
        var note = window.GetVisualDescendants().OfType<InfoBar>().Single(b => b.Message == CalendarViewModel.AssistantBookingNote);
        Assert.True(note.IsEffectivelyVisible);
        var book = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && Equals(b.Content, "Termin buchen"));
        Assert.DoesNotContain(book.GetVisualAncestors(), a => a is ScrollViewer);
        note.BringIntoView();
        await Pump(Settling);
        var viewer = note.GetVisualAncestors().OfType<ScrollViewer>().First();
        var noteTop = note.TranslatePoint(new Point(0, 0), viewer)!.Value.Y;
        Assert.True(noteTop >= 0 && noteTop + note.Bounds.Height <= viewer.Viewport.Height,
            $"Hinweis liegt bei {noteTop}..{noteTop + note.Bounds.Height}, sichtbar sind 0..{viewer.Viewport.Height}");

        return calendar;
    }, Api().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Reception,
        "Ich öffne „Neuer Termin“ vorausgefüllt; gebucht wird erst mit „Termin buchen“.", [],
        [new AssistantSuggestion(AssistantSuggestionKind.NewAppointment, "Neuer Termin: Brigitte Müller, heute 16:00 Massage bei Lena Huber", SpaSample.LocationId,
            SpaSample.Treatments[0].Id, SpaSample.Lena.Id, SpaSample.Customers[0].Id, null, SpaSample.At(16))],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.", [])));
}

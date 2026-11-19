using System.Globalization;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Durchsicht des Kalenders, Teil 1: Seitenleisten (KI-Knopf, Kopfzeile, Reiter, Datumszeile, Tastenleiste) und die Masken Termin, Raumansicht,
/// Alle Mitarbeitenden, Neuer Termin, Paar oder Spa-Tag, Freie Zeit und Sauna. Jede Zusicherung misst Grenzen, Sichtbarkeit oder abgeschnittenen Text.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Der Text steht ohne „…“ im Feld: seine natürliche Breite passt in die Breite des Textfelds.</summary>
    private static bool TextFits(TextBlock block)
    {
        var text = block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) : block.Text;
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var weight = block.Inlines is { Count: > 0 } && block.Inlines.OfType<Run>().FirstOrDefault() is { FontWeight: var runWeight } && runWeight != FontWeight.Normal ? runWeight : block.FontWeight;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(block.FontFamily, block.FontStyle, weight, block.FontStretch),
            block.FontSize, Brushes.Black);
        // Ein gekürzter Text fehlt mindestens ein Zeichen (ab etwa 6 px); die Messung des Textsatzes weicht um ein, zwei Pixel ab.
        return formatted.Width <= block.Bounds.Width + 3;
    }

    private static readonly string[] HeaderChipsWithNames = ["HeadTeam", "HeadRooms", "OverviewChip", "HeadSauna", "HeadWaitlist", "HeadMessages", "HeadCancelled"];

    private static IEnumerable<TextBlock> VisibleTextBlocks(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && (!string.IsNullOrEmpty(t.Text) || t.Inlines is { Count: > 0 }));

    // ---------- A: Knopf „KI und Hilfe“ ----------

    /// <summary>Kein bedienbares Element der offenen Seitenleiste liegt am runden Knopf „KI und Hilfe“: 8 px Luft; Elemente im Rollbereich zählen nur mit dem sichtbaren Teil.</summary>
    private static void AssertFabFree(Window window, string panelName)
    {
        var panel = SidePanelOf(window);
        foreach (var scroll in panel.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible))
        {
            scroll.ScrollToEnd();
        }

        var fab = Named<Button>(window, "CompanionFab");
        Assert.True(fab.IsEffectivelyVisible, panelName);
        var fabBox = BoundsIn(fab, window).Inflate(7);
        var rail = Rail(window);
        var controls = panel.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or ListBoxItem or TextBox or ComboBox or CheckBox or ToggleSwitch && c.IsEffectivelyVisible && c.Bounds.Width > 0 && !rail.IsVisualAncestorOf(c) && c != rail);
        foreach (var control in controls)
        {
            var box = BoundsIn(control, window);
            if (control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { } scroll)
            {
                box = box.Intersect(BoundsIn(scroll, window));
            }

            Assert.False(box.Width > 0 && box.Height > 0 && box.Intersects(fabBox), $"{panelName}: {control.GetType().Name} {AutomationProperties.GetName(control)} {box} liegt am Knopf {fabBox}");
        }
    }

    [Fact]
    public Task Der_Knopf_KI_und_Hilfe_laesst_Luft_zu_jedem_Inhalt_der_offenen_Seitenleiste() => Render("hell", "durchsicht-a-ki-knopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        calendar.Booking!.CustomerQuery = "mül";
        await Pump();
        calendar.Booking.Customer = calendar.Booking.Customers.First();
        await Pump(Settling);
        AssertFabFree(window, "Neuer Termin");

        calendar.OpenGroupBookingCommand.Execute(null);
        await Pump(Settling);
        AssertFabFree(window, "Paar oder Spa-Tag");

        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        await Pump(Settling);
        AssertFabFree(window, "Freie Zeit suchen");

        calendar.OpenAreasCommand.Execute(null);
        await Pump(Settling);
        AssertFabFree(window, "Sauna und Ruheraum");

        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        AssertFabFree(window, "Termin");
        return calendar;
    });

    // ---------- B: Kopfzeile bei offener Seitenleiste ----------

    [Fact]
    public Task Kopfzeile_bei_offener_Leiste_hat_einen_Suchplatzhalter_ohne_Abschnitt_und_Chips_mit_Namen_und_Hinweis() => Render("hell", "durchsicht-b-kopfzeile", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        Assert.True(calendar.IsCompactHeader);

        var search = Named<SearchField>(window, "SearchBox");
        var placeholder = search.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == search.PlaceholderText);
        Assert.True(TextFits(placeholder), $"Der Platzhalter „{search.PlaceholderText}“ ist abgeschnitten ({placeholder.Bounds.Width} px, Feld {search.Bounds.Width})");
        Assert.Contains("Strg+F", ToolTip.GetTip(search) as string, StringComparison.Ordinal);

        // Kompakte Chips zeigen Symbol und Zahl; jede Zahl hat einen Namen mit Wort und einen Hinweis (design.md 7).
        foreach (var name in HeaderChipsWithNames)
        {
            var chip = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name);
            if (chip is not { IsEffectivelyVisible: true })
            {
                continue;
            }

            var label = AutomationProperties.GetName(chip);
            Assert.True(label?.Any(char.IsLetter) == true, $"{name}: der Name „{label}“ enthält kein Wort");
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(chip) as string), $"{name}: kein Hinweis");
        }

        Assert.Equal("Warteliste 4", AutomationProperties.GetName(Named<Button>(window, "HeadWaitlist")));
        Assert.Contains("Warteliste", (string)ToolTip.GetTip(Named<Button>(window, "HeadWaitlist"))!, StringComparison.Ordinal);
        Assert.Contains("Sauna", AutomationProperties.GetName(Named<Button>(window, "HeadSauna")), StringComparison.Ordinal);
        return calendar;
    });

    // ---------- C: Reiter unten ----------

    [Fact]
    public Task Ein_einzelner_Reiter_unten_zeigt_seinen_ganzen_Text() => Render("hell", "durchsicht-c-reiter", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        calendar.Booking!.CustomerQuery = "mül";
        await Pump();
        calendar.Booking.Customer = calendar.Booking.Customers.First();
        await Pump(Settling);

        var tab = Assert.Single(RailTabs(window));
        var text = tab.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal("Neuer Termin · Brigitte Müller", text.Text);
        Assert.True(TextFits(text), "Der Reiter ist abgeschnitten: " + text.Text);
        return calendar;
    });

    // ---------- D: Datumszeile und Suchfeld ----------

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(1100, false)]
    [InlineData(1100, true)]
    public Task Datumszeile_und_Suchfeld_ueberlappen_bei_keiner_Leistenbreite(int width, bool wide) => Render("hell", "durchsicht-d-datum", async (window, main) =>
    {
        window.Width = width;
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        if (wide)
        {
            calendar.Selected!.EditCustomerCommand.Execute(null);
            Assert.Equal(560, calendar.PanelWidth);
        }

        await Pump(Settling);
        var search = BoundsIn(Named<SearchField>(window, "SearchBox"), window);
        var date = Named<Button>(window, "DateButton");
        Assert.False(BoundsIn(date, window).Intersects(search), $"Der Datumsknopf {BoundsIn(date, window)} liegt unter dem Suchfeld {search}");
        foreach (var text in VisibleTextBlocks(date))
        {
            var box = BoundsIn(text, window);
            Assert.False(box.Intersects(search), $"„{text.Text}“ {box} liegt unter dem Suchfeld {search}");
        }

        // Bei dem kleinsten Fenster mit breiter Leiste bleibt für das Datum kein Platz; sonst steht es da (gekürzt, aber sichtbar).
        if (width >= 1440)
        {
            Assert.Contains(VisibleTextBlocks(date), t => t.Text == calendar.DateShort);
        }
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Selected.CustomerId}", SpaSample.Customers[0] with { Id = SpaSample.Selected.CustomerId }));

    // ---------- F: Tastenleiste ----------

    [Theory]
    [InlineData(1440, 360)]
    [InlineData(1440, 560)]
    [InlineData(1100, 360)]
    public Task Die_Tastenleiste_steht_in_einer_Zeile_und_Alle_Tasten_bleibt(int width, int panelWidth) => Render("hell", "durchsicht-f-tastenleiste", async (window, main) =>
    {
        window.Width = width;
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        if (panelWidth == 560)
        {
            calendar.Selected!.EditCustomerCommand.Execute(null);
        }

        await Pump(Settling);
        var strip = window.GetVisualDescendants().OfType<HintStrip>().Single();
        var buttons = strip.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is KeyHint).ToList();
        Assert.Equal(calendar.KeyHints.Count, buttons.Count);
        Control SlotOf(Button b) => b.GetVisualAncestors().OfType<Control>().First(c => ReferenceEquals(c.GetVisualParent(), strip));
        var shown = buttons.Where(b => !HintStrip.GetIsDropped(SlotOf(b))).ToList();
        Assert.True(shown.Count >= 1);
        Assert.All(shown, b => Assert.Equal(BoundsIn(shown[0], window).Top, BoundsIn(b, window).Top, 1));
        Assert.All(shown, b => Assert.True(BoundsIn(b, window).Right <= BoundsIn(strip, window).Right + 1, "Hinweis ragt aus der Leiste"));
        Assert.True(strip.Bounds.Height < 40, $"Die Leiste ist {strip.Bounds.Height} hoch, also zweizeilig");
        // „Alle Tasten“ bleibt, auch wenn andere Hinweise wegfallen; die Taste selbst wirkt weiter.
        Assert.Contains(shown, b => ((KeyHint)b.DataContext!).IsHelp);
        foreach (var dropped in buttons.Except(shown))
        {
            Assert.False(((KeyHint)dropped.DataContext!).IsHelp);
            Assert.Equal(0, SlotOf(dropped).Opacity);
        }

        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Selected.CustomerId}", SpaSample.Customers[0] with { Id = SpaSample.Selected.CustomerId }));

    // ---------- 1a: offener Betrag ----------
    // (steht in ViewTests.CustomerSection.cs: Kunde_Abschnitt_ist_zu_mit_vier_Kennzahlen_in_zwei_Spalten_und_einer_Zeile_Offen)

    // ---------- 2: Raumansicht ----------

    [Fact]
    public Task Raumansicht_Streifen_Namen_und_Koepfe_sind_nicht_abgeschnitten_und_nicht_ueberdeckt() => Render("hell", "durchsicht-2-raeume", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump(Settling);

        var cards = window.GetVisualDescendants().OfType<AppointmentCard>().Where(c => c.IsEffectivelyVisible).ToList();
        // 2a: kein Streifen mit Text ist abgeschnitten, und keine Karte eines anderen Termins liegt darüber.
        var stripes = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible && b.DataContext is CalendarStripe).ToList();
        Assert.NotEmpty(stripes);
        foreach (var stripe in stripes)
        {
            var model = (CalendarStripe)stripe.DataContext!;
            var box = BoundsIn(stripe, window);
            foreach (var card in cards.Where(c => c.DataContext is CalendarItem item && item.Appointment?.Id != model.Appointment?.Id))
            {
                var overlap = box.Intersect(BoundsIn(card, window));
                Assert.False(overlap.Width > 1 && overlap.Height > 1, $"Der Streifen „{model.Text}“ {box} liegt unter der Karte {BoundsIn(card, window)}");
            }

            foreach (var text in stripe.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Assert.True(TextFits(text), $"Streifen abgeschnitten: „{text.Text}“ in {text.Bounds.Width} px");
            }
        }

        Assert.Contains(stripes, s => ((CalendarStripe)s.DataContext!).Text.StartsWith("Nachb. 10 · Pflege", StringComparison.Ordinal));

        // 2b: Zwei Termine nebeneinander: der Name steht ganz da.
        var lanes = cards.Where(c => c.DataContext is CalendarItem { LaneCount: > 1, IsCompact: false }).ToList();
        Assert.NotEmpty(lanes);
        foreach (var card in lanes)
        {
            var title = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Inlines is { Count: > 0 } && t.Inlines.OfType<Run>().Any(r => r.Text == ((CalendarItem)card.DataContext!).Title));
            // Passt der Name nicht in eine Zeile, bricht er um (zweite Zeile) und endet nicht mit „…“.
            Assert.True(TextFits(title) || title.TextWrapping == TextWrapping.Wrap && title.Bounds.Height >= 2 * title.FontSize, $"Name abgeschnitten: {((CalendarItem)card.DataContext!).Title} in {title.Bounds.Width} px");
            Assert.DoesNotContain(card.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("who") && b.IsEffectivelyVisible);
        }

        // 2c: Der Kopf kürzt die zweite Zeile, der Hinweis nennt den vollen Text.
        var heads = window.GetVisualDescendants().OfType<Grid>().Where(g => g.DataContext is CalendarColumn { IsRoom: true } && g.Height == 38).ToList();
        Assert.NotEmpty(heads);
        foreach (var head in heads)
        {
            var column = (CalendarColumn)head.DataContext!;
            var line = head.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == column.HeaderSecondLine);
            Assert.True(TextFits(line), $"Raumkopf abgeschnitten: „{line.Text}“ in {line.Bounds.Width} px");
            if (column.Subtitle.Length > 0 && column.ShortSecondLine is { Length: > 0 })
            {
                Assert.Contains(column.HeaderTipLines, l => l.Text.Contains(column.Subtitle.Split(" · ")[^1], StringComparison.Ordinal));
            }
        }

        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithCoupleRoom));

    // ---------- 4: Alle Mitarbeitenden, Rand mit weiteren Spalten ----------

    /// <summary>Weder Verlauf noch Knopf am Rand liegen über dem Text einer Karte; Text und Köpfe bleiben im vollen Kontrast.</summary>
    private static void AssertEdgesDoNotCoverText(Window window)
    {
        var body = BodyScrollOf(window);
        var view = BoundsIn(body, window);
        foreach (var name in new[] { "EdgePrevious", "EdgeFadePrevious" })
        {
            var edge = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
            Assert.True(edge.IsEffectivelyVisible, name);
            var box = BoundsIn(edge, window);
            var host = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "Host");
            foreach (var text in VisibleTextBlocks(host))
            {
                var textBox = BoundsIn(text, window).Intersect(view);
                Assert.False(textBox.Width > 1 && textBox.Height > 1 && textBox.Intersects(box), $"{name} {box} liegt über „{text.Text}“ {textBox}");
            }
        }
    }

    // ---------- 5: Neuer Termin ----------

    [Fact]
    public Task Neuer_Termin_zeigt_nach_der_Wahl_nur_den_Chip_Aendern_und_Ende_und_Knopf_Freie_Zeit() => Render("hell", "durchsicht-5-neuer-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        booking.CustomerQuery = "mül";
        await Pump();
        booking.Customer = booking.Customers.First();
        booking.Treatment = booking.Treatments.First(t => t.CheckAnamnesis);
        await Pump(Settling);

        // 5a: Der Name steht einmal, als Chip; das Suchfeld ist weg.
        var panel = SidePanelOf(window);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<SearchField>(), f => f.IsEffectivelyVisible);
        var name = booking.Customer!.InHouseName;
        Assert.Single(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == name);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible && t.Text == name);
        var change = panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Ändern");

        // 5b: „Freie Zeit suchen“ ist ein Knopf mit Rahmen, kein Text.
        var freeTime = Named<Button>(panel, "BookingFreeTimeButton");
        Assert.True(freeTime.IsEffectivelyVisible);
        Assert.DoesNotContain("ghost", freeTime.Classes);

        // 5c: Neben dem Beginn steht das Ende; es folgt Beginn und Dauer.
        Assert.Equal("14:30", booking.StartText);
        Assert.Equal(80, booking.Treatment!.DurationMinutes);
        Assert.Equal("bis 15:50", booking.EndText);
        var end = Named<TextBlock>(panel, "BookingEndText");
        Assert.True(end.IsEffectivelyVisible);
        Assert.Equal("bis 15:50", end.Text);
        var start = panel.GetVisualDescendants().OfType<TimeField>().First(f => f.IsEffectivelyVisible);
        Assert.True(BoundsIn(end, window).Left >= BoundsIn(start, window).Right, "Das Ende steht rechts neben dem Beginn");
        booking.StartText = "16:00";
        Assert.Equal("bis 17:20", booking.EndText);
        booking.StartText = "14:30";

        // 5d: Liegt die Vorschau über einem Termin der Spalte, steht sie in der Warnfarbe; die Belegung prüft der Server beim Buchen (Konfliktmeldung mit Alternativen).
        Assert.True(calendar.Ghost is { IsWarning: true }, "Die Vorschau über dem Termin von Nina Weiss steht nicht in Warnfarbe");

        // „Ändern“ bringt das Suchfeld zurück, mit Fokus.
        ClickCenter(window, change);
        await Pump(Settling);
        var search = Named<SearchField>(panel, "BookingCustomerSearch");
        Assert.True(search.IsEffectivelyVisible);
        Assert.True(ReferenceEquals(Focused(window), search) || HasFocusInside(window, search), "Nach „Ändern“ hat das Suchfeld den Fokus nicht");
        return calendar;
    });

    // ---------- 6: Paar oder Spa-Tag ----------

    [Fact]
    public Task Paar_oder_Spa_Tag_zeigt_die_Vorschau_in_allen_betroffenen_Spalten_und_erklaert_den_Abschnitt() => Render("hell", "durchsicht-6-gruppe", async (window, main) =>
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
        group.Slot = group.Slots[1];
        await Pump(Settling);

        // 6a: je Person mit Spalte eine Vorschau; der Eintritt in den Ruheraum hat in der Teamansicht keine Spalte.
        var people = group.PreviewParts.Where(p => p.EmployeeId is not null).Select(p => p.EmployeeId!.Value).Distinct().Count();
        Assert.True(people >= 2, "Der Ablauf nennt mindestens zwei Personen");
        var ghosts = new[] { calendar.Ghost }.Concat(calendar.GroupGhosts).OfType<CalendarGhost>().ToList();
        Assert.Equal(group.PreviewParts.Count(p => p.EmployeeId is not null), ghosts.Count);
        Assert.Equal(people, ghosts.Select(g => g.Left).Distinct().Count());
        // Jede weitere Vorschau steht auch im Bild: eine sichtbare Fläche je Teil (die zweite Fläche der Karte gehört zur Warnfarbe).
        Assert.Equal(calendar.GroupGhosts.Count, Named<ItemsControl>(window, "GroupGhostList").GetVisualDescendants().OfType<Rectangle>().Count(r => r.IsEffectivelyVisible));

        // Jede weitere Vorschau steht in der Spalte ihrer Person: links bündig mit der Spalte (Rand 3), nicht oben links im Raster.
        var host = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "Host");
        var list = Named<ItemsControl>(window, "GroupGhostList");
        foreach (var ghost in calendar.GroupGhosts)
        {
            var presenter = list.GetVisualDescendants().OfType<ContentPresenter>().Single(c => ReferenceEquals(c.Content, ghost));
            Assert.InRange(Math.Abs(BoundsIn(host, window).Left + ghost.Left - BoundsIn(presenter, window).Left), 0, 1.5);
            Assert.InRange(Math.Abs(BoundsIn(host, window).Top + ghost.Top - BoundsIn(presenter, window).Top), 0, 1.5);
        }

        // 6b: Der Knopf nennt das Verb, und ein Fragezeichen erklärt die Abschnitte.
        var add = Named<Button>(window, "AddStepButton");
        Assert.Contains(add.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Abschnitt danach hinzufügen");
        Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(add) as string));
        var help = add.GetVisualAncestors().OfType<StackPanel>().First().GetVisualDescendants().OfType<HelpButton>().Single();
        Assert.Equal("abschnitte", help.Section);
        Assert.Equal("kalender-paar-und-spa-tag", help.Topic);
        return calendar;
    });

    // ---------- 7: Freie Zeit suchen ----------

    [Fact]
    public Task Freie_Zeit_nennt_nur_Abweichungen_und_schreibt_die_Namen_aus() => Render("hell", "durchsicht-7-freie-zeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        calendar.FreeTime!.Treatment = calendar.FreeTime.Treatments.First(t => t.Id == SpaSample.Treatments[1].Id);
        await Pump(Settling);

        var panel = SidePanelOf(window);
        Assert.DoesNotContain(VisibleTextBlocks(panel), t => t.Text == "passt genau");
        Assert.Contains(VisibleTextBlocks(panel), t => t.Text?.StartsWith("lässt ", StringComparison.Ordinal) == true);
        var rows = panel.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is FreeSlotRow).ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var model = (FreeSlotRow)row.DataContext!;
            var who = row.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == model.Who);
            Assert.True(TextFits(who), $"Name abgeschnitten: „{model.Who}“ in {who.Bounds.Width} px");
            // Für Screenreader bleibt die Angabe im Namen der Zeile, sichtbar ist sie als Hinweis.
            Assert.Equal(model.IsSnug, model.AccessibleName.Contains("passt genau", StringComparison.Ordinal));
            Assert.Equal(model.IsSnug ? "Schließt ohne Lücke an" : null, ToolTip.GetTip(row.GetVisualDescendants().OfType<Grid>().First(g => ReferenceEquals(g.DataContext, model))) as string);
        }

        return calendar;
    });

    // ---------- 8: Sauna und Ruheraum ----------

    [Fact]
    public Task Sauna_Achse_Namen_und_Belegsymbol_sind_ganz_sichtbar_und_beschriftet() => Render("hell", "durchsicht-8-sauna", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAreasCommand.Execute(null);
        await Pump();
        calendar.Areas!.Search.Query = "br";
        await Pump();
        calendar.Areas.Search.Customer = calendar.Areas.Search.Results[0];
        await Pump(Settling);

        var panel = SidePanelOf(window);
        // 8a: die Achse endet in der Karte, „20:00“ steht ganz da.
        var axes = panel.GetVisualDescendants().OfType<Grid>().Where(g => g.Name == "AreaAxis").ToList();
        Assert.NotEmpty(axes);
        foreach (var axis in axes)
        {
            var card = axis.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("panelcard"));
            Assert.True(BoundsIn(axis, window).Right <= BoundsIn(card, window).Right - 1, "Die Achse ragt aus der Karte");
            var texts = axis.GetVisualDescendants().OfType<TextBlock>().ToList();
            Assert.All(texts, t => Assert.True(TextFits(t), $"Achse abgeschnitten: „{t.Text}“"));
            Assert.Contains(texts, t => t.Text == "20:00");
        }

        // 8b und M2 (Teil 2): Der Name darf umbrechen; „Stornieren …“ steht im Zeilenmenü „⋯“ und lässt dem Namen mehr Platz.
        var menus = panel.GetVisualDescendants().OfType<MoreMenuButton>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(menus);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Stornieren");
        foreach (var row in panel.GetVisualDescendants().OfType<Grid>().Where(g => g.DataContext is AreaVisitRow { CanCancel: true } && g.IsEffectivelyVisible))
        {
            var model = (AreaVisitRow)row.DataContext!;
            var name = row.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == model.Name);
            Assert.Equal(TextWrapping.Wrap, name.TextWrapping);
            var button = row.GetVisualDescendants().OfType<MoreMenuButton>().Single();
            Assert.True(BoundsIn(name, window).Right <= BoundsIn(button, window).Left + 0.5, "Der Name liegt unter dem Menü");
            Assert.True(BoundsIn(button, window).Right <= BoundsIn(SidePanelOf(window), window).Right, "Das Menü ragt aus der Leiste");
            Assert.True(name.Bounds.Width >= 100, $"Der Name hat nur {name.Bounds.Width} px");
            Assert.Equal($"Weitere Aktionen zu {model.Name}", AutomationProperties.GetName(button));
            var item = Assert.IsType<MenuItem>(Assert.Single(Assert.IsType<MenuFlyout>(button.Flyout).Items));
            Assert.Equal("Stornieren …", item.Header);
            Assert.Same(model.CancelCommand, item.Command);
        }

        // 8c: Das Belegsymbol hat Hinweis und Namen.
        var receipts = panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b is not MoreMenuButton && b.DataContext is AreaVisitRow { CanCheckout: true } && b.GetVisualDescendants().OfType<Icon>().Any()).ToList();
        Assert.NotEmpty(receipts);
        Assert.All(receipts, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(b) as string));
            Assert.StartsWith("Eintritt an der Kassa abrechnen:", AutomationProperties.GetName(b), StringComparison.Ordinal);
        });
        return calendar;
    });
}

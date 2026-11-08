using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ansichten nach den Vorgaben für Masken und Meldungen (docs/gestaltung/masken.md, meldungen.md, ADR 0067): Terminkopf mit Kundenübersicht,
/// Kundendaten-Maske, Meldungsbereich des Kalenders, „Alle Mitarbeitenden“. Die Bildschirmfotos landen im Handbuch (ADR 0056).
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Die Kundenübersicht lädt über die vorhandenen Endpunkte; der Gast des Beispieltermins ist Brigitte Müller.</summary>
    private static StubApi WithGuestOverview(StubApi api)
    {
        var id = SpaSample.Selected.CustomerId;
        var customer = SpaSample.Customers[0] with { Id = id, PreferredName = "Gitti", Pronouns = "sie/ihr" };
        return api
            .On($"GET /api/customers/{id}", customer)
            .On($"GET /api/customers/{id}/insights", SpaSample.Insights())
            .On($"GET /api/customers/{id}/flags", SpaSample.CustomerFlags())
            .On($"GET /api/reports/customers/{id}/preferences", SpaSample.Wishes())
            .On($"GET /api/customers/{id}/appointments", SpaSample.CustomerHistory());
    }

    private static void ClickCenter(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    private static Button ButtonWithText(Visual root, string text) =>
        // Die Einträge der Tastenleiste (KeyHint) sind seit Slice 76 Knöpfe mit gleichem Text und zählen hier nicht.
        root.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.DataContext is not KeyHint
            && (b.Content as string == text || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text)));

    [Fact]
    public Task Terminkopf_hat_Initialen_Namen_Stift_Behandlung_und_Pillen_fest_im_Kopf() => Render("hell", "kalender-terminkopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();

        var header = window.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "AppointmentHeader");
        Assert.True(header.IsEffectivelyVisible);
        var name = header.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "GuestName");
        Assert.Equal(22, name.FontSize);
        Assert.Equal(FontWeight.SemiBold, name.FontWeight);
        Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "BM" && t.IsEffectivelyVisible);
        // ♀/♂ und Stift stehen direkt beim Namen, nicht in einer eigenen Zeile.
        var icon = header.GetVisualDescendants().OfType<Icon>().First(i => i.Data == Icons.Female && i.IsEffectivelyVisible);
        var pencil = header.GetVisualDescendants().OfType<Button>().First(b => b.Command == calendar.Selected!.EditCustomerCommand && b.IsEffectivelyVisible);
        var nameBottom = name.TranslatePoint(new Point(0, name.Bounds.Height), header)!.Value.Y;
        Assert.True(icon.TranslatePoint(new Point(0, 0), header)!.Value.Y < nameBottom);
        Assert.True(pencil.TranslatePoint(new Point(0, 0), header)!.Value.Y < nameBottom);
        // Behandlung darunter in normaler Größe, Status und Dauer als Pillen.
        var treatment = header.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Hot-Stone-Massage");
        Assert.True(treatment.FontSize < name.FontSize);
        Assert.True(treatment.TranslatePoint(new Point(0, 0), header)!.Value.Y >= nameBottom - 1);
        // Die Pillen stehen in der Chip-Zeile direkt unter dem Kopf (5.12, Punkt 3).
        var chips = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "AppointmentChips");
        Assert.Equal(2, chips.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("pill") && b.IsEffectivelyVisible));
        Assert.Equal(0, header.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("pill")));
        Assert.True(chips.TranslatePoint(new Point(0, 0), window)!.Value.Y >= header.TranslatePoint(new Point(0, header.Bounds.Height), window)!.Value.Y);
        // ✕ fest rechts oben.
        var close = header.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Schließen (Esc)");
        Assert.True(close.TranslatePoint(new Point(close.Bounds.Width, 0), header)!.Value.X > header.Bounds.Width - 12);
        Assert.True(close.TranslatePoint(new Point(0, 0), header)!.Value.Y < 12);

        // Kein Knopf „Kundenkarte“ mehr im Panel (sie steht in der Kundenübersicht); „Verschieben …“ hat die volle Breite.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Kundenkarte" && t.IsEffectivelyVisible);
        var move = ButtonWithText(window, "Verschieben …");
        var checkIn = ButtonWithText(window, "Einchecken");
        Assert.Equal(checkIn.Bounds.Width, move.Bounds.Width, 1);
        return calendar;
    });

    [Fact]
    public Task Terminkopf_bricht_einen_langen_Namen_in_zwei_Zeilen_um() => Render("hell", "kalender-terminkopf-langer-name", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(LongNameDay().Appointments.First(a => a.Id == SpaSample.Selected.Id));
        await Pump();

        var name = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "GuestName");
        Assert.Equal(LongName, calendar.Selected!.Title);
        Assert.Equal(2, name.TextLayout.TextLines.Count);
        Assert.Contains(window.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Female && i.IsEffectivelyVisible);
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", LongNameDay()));

    [Fact]
    public Task Kundenuebersicht_oeffnet_per_Klick_auf_Initialen_und_Namen_wie_das_Raum_Flyout() => Render("hell", "kalender-kundenuebersicht", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();

        var avatar = window.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null && b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Kundenübersicht");
        ClickCenter(window, avatar);
        await Pump();
        await Pump();
        Assert.True(avatar.Flyout!.IsOpen);

        var overview = calendar.Selected!.Overview;
        Assert.False(overview.IsLoading);
        Assert.Equal("Gitti Müller", overview.Name);
        Assert.Equal("(sie/ihr)", overview.Pronouns);
        Assert.Equal("Frau Mag. Brigitte Müller", overview.AddressLine);
        Assert.Equal("Rufname Gitti", overview.PreferredNameText);
        Assert.Equal("+43 664 123 45 67", overview.Phone);
        Assert.Equal("7 Besuche in zwölf Monaten", overview.VisitsText);
        Assert.Equal("Letzter Besuch 21.9.2026", overview.LastVisitText);
        // Der Beispieltermin liegt 14 Tage später, nach der Zeitumstellung am 25.10.: dieselbe Uhrzeit in UTC ist dann eine Stunde früher in Wien.
        Assert.Equal(["Mo. 2.11. 09:30 · Hot-Stone-Massage"], overview.Next);
        Assert.Equal(["Braucht mehr Zeit", "Nicht bei Marco Berger"], overview.Flags);
        Assert.Equal(["Hot-Stone-Massage: 7 von 10 offen, gültig bis 30.6.2027"], overview.Packages);
        Assert.Contains("Druck: kräftig", overview.WishesText, StringComparison.Ordinal);
        Assert.Equal("Stammkundin, mag Lavendelöl", overview.Note);
        Assert.True(overview.CanOpenCard && overview.CanEdit);
        return calendar;
    }, WithGuestOverview(Api()));

    [Fact]
    public Task Kundenuebersicht_oeffnet_auch_per_Klick_auf_den_Namen_der_Stift_bearbeitet() => Render("hell", "kalender-kundenuebersicht-name", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        var name = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "GuestName");

        ClickCenter(window, name);
        await Pump();
        await Pump();
        Assert.True(FlyoutBase.GetAttachedFlyout(name)!.IsOpen);
        Assert.Equal("Gitti Müller", calendar.Selected!.Overview.Name);
        FlyoutBase.GetAttachedFlyout(name)!.Hide();
        await Pump();

        // Der Stift beim Namen öffnet nicht die Übersicht, sondern die Kundendaten.
        var pencil = window.GetVisualDescendants().OfType<Button>().First(b => b.Command == calendar.Selected.EditCustomerCommand && b.IsEffectivelyVisible);
        ClickCenter(window, pencil);
        await Pump();
        await Pump();
        Assert.False(FlyoutBase.GetAttachedFlyout(name)!.IsOpen);
        Assert.NotNull(calendar.Selected.CustomerForm);
        return calendar;
    }, WithGuestOverview(Api()));

    [Fact]
    public Task Kundenuebersicht_Bearbeiten_oeffnet_die_Kundendaten_im_Panel() => Render("hell", "kalender-kundenuebersicht-bearbeiten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        var avatar = window.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null && b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Kundenübersicht");
        ClickCenter(window, avatar);
        await Pump();
        await Pump();

        var edit = ButtonWithText(window, "Bearbeiten");
        ClickCenter(window, edit);
        await Pump();
        await Pump();

        Assert.False(avatar.Flyout!.IsOpen);
        Assert.NotNull(calendar.Selected!.CustomerForm);
        return calendar;
    }, WithGuestOverview(Api()));

    // ---------- Kundendaten-Maske (masken.md, Abschnitt 7) ----------

    /// <summary>Brigitte Müller ohne Titel und Notiz: alle seltenen Angaben sind leer, „Weitere Angaben“ bleibt zu und die Maske passt ins Bild.</summary>
    private static StubApi WithPlainCustomer() => Api()
        .On("GET /api/customers?search=", new List<CustomerDto> { SpaSample.Customers[0] with { TitlesBefore = null, Note = null }, SpaSample.Customers[1] })
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}", SpaSample.Customers[0] with { TitlesBefore = null, Note = null });

    private static IEnumerable<Button> HelpButtons(Visual root) => root.GetVisualDescendants().OfType<HelpButton>().Where(b => b.IsEffectivelyVisible);

    [Fact]
    public Task Kundendaten_Maske_hat_drei_Gruppen_drei_Fragezeichen_und_keine_Chips() => Render("hell", "kunden-bearbeiten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);

        // Gruppen mit Überschrift, je ein Fragezeichen für Name, Kontakt und Weitere Angaben.
        var headings = form.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("h2") && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["Name", "Kontakt", "Anschrift", "Weitere Angaben"], headings);
        Assert.Equal(["name", "kontakt", "weitere-angaben"], HelpButtons(form).Select(b => ((HelpButton)b).Section));
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("chip"));
        // Pflichtfelder mit Stern, kein Dauerhinweis zum Leeren, keine Karte „VORSCHAU“.
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Leere Felder", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "VORSCHAU");
        Assert.Equal(2, form.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "*" && t.IsEffectivelyVisible));
        // Ein Raster: Anrede 160, PLZ 96, Land 72.
        var salutation = form.GetVisualDescendants().OfType<ComboBox>().First(c => c.IsEffectivelyVisible);
        Assert.Equal(160, salutation.Bounds.Width, 1);
        Assert.Equal(96, form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "PostalCodeBox").Bounds.Width, 1);
        Assert.Equal(72, form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "CountryBox").Bounds.Width, 1);
        // Speichern und Abbrechen stehen links am Ende der Maske und sind im Bild zu sehen.
        Assert.True(ButtonWithText(form, "Speichern").TranslatePoint(new Point(0, 0), window)!.Value.Y < window.Bounds.Height);
        return calendar;
    }, WithPlainCustomer());

    [Fact]
    public Task Kundendaten_Maske_Weitere_Angaben_ist_zu_und_oeffnet_per_Klick() => Render("hell", "kunden-bearbeiten-weitere", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[1].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);

        Assert.False(shell.Customers.Contact!.MoreOpen);
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<TextBox>(), t => t.Text == "" && t.PlaceholderText == "z. B. Steffi" && t.IsEffectivelyVisible);
        var toggle = form.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "MoreToggle");
        ClickCenter(window, toggle);
        await Pump();
        Assert.True(shell.Customers.Contact.MoreOpen);
        Assert.Contains(form.GetVisualDescendants().OfType<TextBox>(), t => t.PlaceholderText == "z. B. Steffi" && t.IsEffectivelyVisible);
        Assert.Contains(form.GetVisualDescendants().OfType<AutoCompleteBox>(), a => a.IsEffectivelyVisible);
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Customers[1].Id}", SpaSample.Customers[1])
        .On($"GET /api/customers/{SpaSample.Customers[1].Id}/appointments", new List<AppointmentDto>()));

    [Fact]
    public Task Kundendaten_Maske_Fehler_am_Feld_und_Fokus_zum_ersten_Fehler() => Render("hell", "kunden-bearbeiten-fehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        var vm = shell.Customers.Contact!;

        vm.Country = "AT";
        vm.PostalCode = "12";
        await vm.SaveCommand.ExecuteAsync(null);
        await Pump();

        Assert.Equal("Die PLZ hat vier Ziffern, z. B. 1010.", vm.PostalCodeError);
        var postal = form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "PostalCodeBox");
        Assert.Contains("invalid", postal.Classes);
        Assert.True(postal.IsFocused, "Der Fokus springt ins erste ungültige Feld.");
        Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == vm.PostalCodeError && t.IsEffectivelyVisible);
        AssertMessageOneLine(form, vm.PostalCodeError);
        Assert.False(vm.HasError);
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible);
        return calendar;
    }, WithPlainCustomer());

    /// <summary>Der Fehlertext eines schmalen Feldes (PLZ, Land) bricht nicht in der Feldbreite um, sondern steht in einer Zeile und bleibt in der Maske (Brief 5.2).</summary>
    private static void AssertMessageOneLine(CustomerContactForm form, string? text)
    {
        var message = form.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text && t.IsEffectivelyVisible);
        Assert.True(message.Bounds.Height < 20, $"„{text}“ steht in {message.Bounds.Height} px Höhe, also über mehrere Zeilen");
        var scroll = form.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "FieldsScroll");
        var right = message.TranslatePoint(new Point(message.Bounds.Width, 0), form)!.Value.X;
        var limit = scroll.TranslatePoint(new Point(scroll.Bounds.Width, 0), form)!.Value.X;
        Assert.True(right <= limit + 0.5, $"„{text}“ endet bei {right}, die Maske bei {limit}");
    }

    [Fact]
    public Task Kundendaten_Maske_Fehlertext_des_schmalen_Feldes_Land_steht_in_einer_Zeile() => Render("hell", "kunden-bearbeiten-fehler-land", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        var vm = shell.Customers.Contact!;
        var width = form.Bounds.Width;

        vm.Country = "A";
        await vm.SaveCommand.ExecuteAsync(null);
        await Pump();

        Assert.NotNull(vm.CountryError);
        AssertMessageOneLine(form, vm.CountryError);
        Assert.Equal(width, form.Bounds.Width, 0.5);
        return calendar;
    }, WithPlainCustomer());

    [Fact]
    public Task Kundendaten_Maske_zeigt_einen_maskenweiten_Fehler_als_InfoBar_ueber_den_Knoepfen() => Render("hell", "kunden-bearbeiten-meldung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);

        // Der Server lehnt ab: die Meldung steht über der Aktionsleiste, nicht als roter Block im Formular.
        await shell.Customers.Contact!.SaveCommand.ExecuteAsync(null);
        await Pump();

        Assert.True(shell.Customers.Contact.HasError);
        var bar = form.GetVisualDescendants().OfType<InfoBar>().First(b => b.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Error, bar.Severity);
        Assert.Equal("Nicht gespeichert", bar.Title);
        Assert.Equal("Erneut versuchen", bar.ActionText);
        var save = ButtonWithText(form, "Speichern");
        Assert.True(bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y <= save.TranslatePoint(new Point(0, 0), window)!.Value.Y);
        return calendar;
    }, WithPlainCustomer());

    [Fact]
    public Task Kundendaten_im_Panel_haben_die_Aktionsleiste_fest_im_Fuss() => Render("hell", "kalender-kundendaten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        calendar.Selected!.EditCustomerCommand.Execute(null);
        await Pump();
        await Pump();

        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        Assert.True(form.Docked);
        var save = ButtonWithText(form, "Speichern");
        var panel = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SidePanel");
        var saveBottom = save.TranslatePoint(new Point(0, save.Bounds.Height), window)!.Value.Y;
        var panelBottom = panel.TranslatePoint(new Point(0, panel.Bounds.Height), window)!.Value.Y;
        // Unter der Aktionsleiste steht nur noch die Reiterschiene (5.12, Punkt 7).
        var rail = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PanelRail");
        Assert.True(panelBottom - rail.Bounds.Height - saveBottom < 24, "Speichern steht am Fuß der Leiste, nicht im scrollenden Inhalt.");
        Assert.True(form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "FirstNameBox").Bounds.Width > 100);
        // Der Kopf mit dem Gast bleibt über dem Formular stehen.
        Assert.True(window.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "AppointmentHeader").IsEffectivelyVisible);
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Selected.CustomerId}", SpaSample.Customers[0] with { Id = SpaSample.Selected.CustomerId }));

    // ---------- Meldungen des Kalenders (meldungen.md, Schritt 16) ----------

    [Fact]
    public Task Kopfzeile_hat_die_Schaltflaeche_Meldungen_und_keinen_Meldungsstapel_oder_Betriebsleiste_mehr() => Render("hell", "kalender-meldungen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        // Keine InfoBars und keine Betriebsleiste über dem Kalender; die Schaltfläche zählt und steht in der Warnfarbe.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("opsbar"));
        Assert.StartsWith("Meldungen ", calendar.MessagesButtonText, StringComparison.Ordinal);
        var button = ButtonWithText(window, calendar.MessagesButtonText);
        Assert.Contains("warn", button.Classes);

        // Kein Band „Heute“ mehr (Runde 5, Ä31): weder Abwesenheitstext noch Notizen über dem Kalender; der Knopf trägt den Notiz-Punkt, der Zähler zählt nur Meldungen.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Abwesend:", StringComparison.Ordinal) == true);
        Assert.True(Named<Icon>(button, "MessagesNoteDot").IsEffectivelyVisible);
        Assert.Equal(calendar.Messages.Count, calendar.MessageCount);

        // Im Spaltenkopf höchstens ein Symbol, kein Warntext.
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Classes.Contains("ghost") && b.DataContext is CalendarColumn { HasAlert: true });
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.Contains("Raumpflege überfällig", StringComparison.Ordinal) == true
            && t.DataContext is CalendarColumn);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Die_Seitenleiste_Meldungen_zeigt_Gruppen_mit_Zaehler_und_Karten_mit_einer_Aktion() => Render("hell", "kalender-betrieb", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();

        ClickCenter(window, ButtonWithText(window, calendar.MessagesButtonText));
        await Pump();
        await Pump();
        Assert.Equal("Meldungen", calendar.ActivePanel!.PanelTitle);
        var cards = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("messagecard") && b.IsEffectivelyVisible).ToList();
        Assert.Equal(calendar.Messages.Count + calendar.DoneMessages.Count, cards.Count);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Warnungen" && t.IsEffectivelyVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Hinweise" && t.IsEffectivelyVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Wegklicken");
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Ein_Klick_auf_die_Karte_oeffnet_die_Detailansicht_mit_Was_tun() => Render("hell", "kalender-meldung-detail", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();
        calendar.OpenMessagesCommand.Execute(null);
        await Pump();

        var card = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("messagecard") && b.DataContext is MessageItem { Kind: MessageKind.DayStart });
        card.BringIntoView();
        await Pump();
        ClickCenter(window, card);
        await Pump();
        await Pump();

        Assert.Equal("Meldung", calendar.ActivePanel!.PanelTitle);
        Assert.Equal(["Meldung", "Meldungen"], calendar.OpenPanels.Select(t => t.Text));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Was tun?" && t.IsEffectivelyVisible);
        // Die Checkliste des Raums gehört zur Tagesstart-Meldung.
        Assert.Contains(window.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Toast_hat_eine_Zeile_und_nur_Rueckgaengig_und_Schliessen() => Render("hell", "kalender-verschoben", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        calendar.BeginDrag(item, 20);
        calendar.DragTo(DayLayout.ColumnLeft(1) + 40, item.Top + 20 - 30 * DayLayout.PixelsPerMinute);
        await calendar.DropAsync();
        await Pump();

        Assert.True(calendar.CanUndo);
        var toast = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ToastBar");
        Assert.True(toast.IsEffectivelyVisible);
        var buttons = toast.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.Equal(2, buttons.Count);
        Assert.Contains(buttons, b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Rückgängig"));
        var text = toast.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("msg"));
        Assert.Single(text.TextLayout.TextLines);
        Assert.True(text.Text!.Length < 80, $"Toast unter 80 Zeichen: {text.Text.Length}");
        return calendar;
    });

    [Fact]
    public Task Verspaetung_zeigt_die_Ankunft_im_Toast_und_am_Termin_ohne_Vorschlaege() => Render("hell", "kalender-verspaetung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        await calendar.ReportLateAsync(SpaSample.Selected, 15);
        await Pump();
        await Pump();

        // Der Termin bleibt an seiner Zeit, die Karte nennt die Ankunft, der Toast bestätigt; nur „Schließen“ steht im Toast.
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        Assert.Equal(SpaSample.Selected.Start, item.Appointment!.Start);
        Assert.Contains("kommt 10:45", item.MetaText, StringComparison.Ordinal);
        Assert.Null(calendar.Problem);
        var toast = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ToastBar");
        Assert.True(toast.IsEffectivelyVisible);
        Assert.Single(toast.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible);
        return calendar;
    }, Api().On($"PUT /api/appointments/{SpaSample.Selected.Id}/late", SpaSample.Selected with { LateMinutes = 15, UpdatedAt = SpaSample.At(9, 50) }));

    // ---------- Alle Mitarbeitenden ----------

    private static readonly EmployeeDto Nora = new(Guid.NewGuid(), "Nora Pichler", "#8a5cc2", [], []);
    private static readonly EmployeeDto Tamara = new(Guid.NewGuid(), "Tamara Wolf", "#2d8a8a", [], []);

    [Fact]
    public Task Alle_Mitarbeitenden_zeigt_Spalten_ohne_Dienst_schraffiert_mit_Grund() => Render("hell", "kalender-alle-mitarbeitende", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        ClickCenter(window, ButtonWithText(window, calendar.AllStaffText));
        window.MouseMove(new Point(700, 880));
        await Pump();
        await Pump();
        // Die zusätzlichen Spalten liegen rechts außerhalb des Fensters: ganz nach rechts scrollen, damit das Bild sie zeigt.
        var body = window.GetVisualDescendants().OfType<ScrollViewer>().First(x => x.Name == "BodyScroll");
        body.Offset = new Vector(body.Extent.Width, 0);
        await Pump();

        Assert.True(calendar.ShowAllStaff);
        var off = calendar.Columns.Where(c => c.OffDuty).ToDictionary(c => c.Title, c => c.Subtitle);
        Assert.Equal("frei", off["Nora Pichler"]);
        Assert.Equal("Salzburg 10:00–18:00", off["Tamara Wolf"]);
        var tamara = calendar.Columns.Single(c => c.Title == "Tamara Wolf");
        Assert.Equal(("Salzburg", "10:00–18:00"), (tamara.Place, tamara.ShiftText));
        Assert.StartsWith("Urlaub bis", off["Johanna Lechner"], StringComparison.Ordinal);
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent with { Employees = [.. SpaSample.ResourcesWithAbsent.Employees, Nora, Tamara] })
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with
        {
            Absences = [new CalendarAbsence(SpaSample.Johanna.Id, RosterKind.Vacation, SpaSample.Day.AddDays(4), null)],
            Elsewhere = [new CalendarElsewhere(Tamara.Id, "Salzburg", SpaSample.At(10), SpaSample.At(18))],
        }));

    // ---------- Rechte: was die Rezeption nicht sieht ----------

    [Fact]
    public Task Rezeption_sieht_im_Panel_weder_Verwaltungswege_noch_Umsatz() => Render("hell", "kalender-rezeption", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();

        // Die Bildtests laufen als Inhaberin; hier zeigt die Rezeption, was Rechte ausblenden.
        Assert.False(calendar.Selected!.RoomInfo!.CanEdit);
        Assert.False(calendar.CanSeeRevenue);
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Einchecken"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Umsatz laut Kalender" && t.IsEffectivelyVisible);
        return calendar;
    }, ReceptionApi());
}

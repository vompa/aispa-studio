using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Mehrzeiliges Feld (F11) und Suchfeld (F12), Design-Brief 5.16.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Mehrzeiliges_Feld_hat_feste_Hoehe_68_waechst_nicht_mit_dem_Text_und_scrollt() => Session.Dispatch(async () =>
    {
        var box = new TextBox { AcceptsReturn = true };
        var single = new TextBox { Text = "einzeilig" };
        var window = await ShowAsync("hell", new FormField { Label = "Notiz", Content = box }, single);

        Assert.Equal(68, box.Bounds.Height);
        Assert.Equal(200, box.MaxHeight);
        Assert.Equal(TextWrapping.Wrap, box.TextWrapping);
        box.Text = string.Join("\n", Enumerable.Range(1, 30).Select(i => FormattableString.Invariant($"Zeile {i}")));
        await Pump();
        Assert.Equal(68, box.Bounds.Height);
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, box.GetValue(ScrollViewer.VerticalScrollBarVisibilityProperty));
        var scroller = box.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "längerer Text scrollt statt zu wachsen");

        // Einzeilige Felder behalten ihre Höhe von 34.
        Assert.Equal(34, single.Bounds.Height);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zaehler_im_mehrzeiligen_Feld_zeigt_noch_n_Zeichen_und_warnt_ab_90_Prozent() => Session.Dispatch(async () =>
    {
        var box = new TextBox { AcceptsReturn = true, MaxLength = 100 };
        var form = new FormField { Label = "Notiz", Help = "Vorlieben; keine Gesundheitsdaten.", Content = box };
        var window = await ShowAsync("hell", form);
        var counter = form.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Counter");
        var help = form.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_MessageText");

        Assert.Equal("noch 100 Zeichen", counter.Text);
        Assert.True(counter.IsEffectivelyVisible);
        Assert.Equal("Vorlieben; keine Gesundheitsdaten.", help.Text);
        Assert.Equal(Resolve(form, "Fg2Brush"), ((ISolidColorBrush)counter.Foreground!).Color);
        // Der Zähler steht rechts in der Hilfezeile.
        Assert.True(counter.TranslatePoint(default, window)!.Value.X > help.TranslatePoint(default, window)!.Value.X + 100);

        box.Focus(NavigationMethod.Tab);
        window.KeyTextInput(new string('a', 50));
        Assert.Equal("noch 50 Zeichen", counter.Text);
        Assert.DoesNotContain("warn", counter.Classes);
        window.KeyTextInput(new string('b', 40));
        Assert.Equal("noch 10 Zeichen", counter.Text);
        Assert.Contains("warn", counter.Classes);
        Assert.Equal(Resolve(form, "WarnBrush"), ((ISolidColorBrush)counter.Foreground!).Color);
        window.KeyTextInput(new string('c', 9));
        Assert.Equal("noch 1 Zeichen", counter.Text);
        window.KeyTextInput("zz");
        Assert.Equal(100, box.Text!.Length);
        SaveFieldShot(window, "mehrzeilig-zaehler");
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kundendaten_Notiz_ist_ein_mehrzeiliges_Feld_mit_Zaehler_und_Limit_500(string theme) => Render(theme, "kunden-bearbeiten-notiz", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        shell.Customers.Contact!.MoreOpen = true;
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        var note = form.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Notiz");
        var box = Assert.IsType<TextBox>(note.Content);
        Assert.Equal((500, 68.0), (box.MaxLength, box.Bounds.Height));
        Assert.Contains(note.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "PART_Counter" && t.IsEffectivelyVisible && t.Text!.StartsWith("noch ", StringComparison.Ordinal));

        // Fehler am Feld (Bild für die Abnahme): E-Mail ohne @ und ein Geburtstag, den es nicht gibt, ersetzen die Hilfe durch den Fehlersatz.
        shell.Customers.Contact.Email = "name";
        shell.Customers.Contact.BirthDate = "31.2.85";
        await shell.Customers.Contact.SaveCommand.ExecuteAsync(null);
        await Pump(Settling);
        Assert.Equal("Den 31.02.1985 gibt es nicht.", form.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Geburtstag").Message);
        Assert.Equal(FieldMessageKind.Error, form.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "E-Mail").MessageKind);
        return calendar;
    }, WithPlainCustomer());

    // F12: Suchfeld

    [Fact]
    public Task Suchfeld_hat_Lupe_links_und_das_Loeschen_Kreuz_nur_mit_Text() => Session.Dispatch(async () =>
    {
        var field = new SearchField { PlaceholderText = "Name, Telefon oder E-Mail" };
        var window = await ShowAsync("hell", field);

        var icons = field.GetVisualDescendants().OfType<Icon>().ToList();
        var lens = Assert.Single(icons, i => i.Data == Icons.Search);
        Assert.Equal((16, 16), (lens.Width, lens.Height));
        Assert.True(lens.TranslatePoint(default, field)!.Value.X < 20, "Lupe links im Feld");
        var clear = field.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("searchclear"));
        Assert.False(clear.IsEffectivelyVisible, "ohne Text kein Kreuz");
        Assert.Equal("Suche leeren", Avalonia.Automation.AutomationProperties.GetName(clear));

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("müller");
        await Pump();
        Assert.True(clear.IsEffectivelyVisible);
        Assert.True(clear.TranslatePoint(default, field)!.Value.X > field.Bounds.Width - 50, "Kreuz rechts im Feld");
        SaveFieldShot(window, "suchfeld-mit-text");

        ClickCenter(window, clear);
        await Pump();
        Assert.Equal("", field.Text);
        Assert.False(clear.IsEffectivelyVisible);
        Assert.True(field.IsFocused);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Suchfeld_Esc_leert_zuerst_das_Feld_erst_das_zweite_Esc_geht_an_die_Maske_und_Tab_markiert_alles() => Session.Dispatch(async () =>
    {
        var before = new Button { Content = "Davor" };
        var field = new SearchField { Text = "mül" };
        var window = await ShowAsync("hell", before, field);
        var unhandled = WatchUnhandledKeys(window);

        before.Focus(NavigationMethod.Pointer);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await Pump();
        Assert.Equal((0, 3), (field.SelectionStart, field.SelectionEnd));

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("", field.Text);
        Assert.DoesNotContain(Key.Escape, unhandled);
        Assert.True(field.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Contains(Key.Escape, unhandled);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Kalendersuche_Esc_in_drei_Stufen_Liste_zu_Feld_leeren_Feld_verlassen() => Render("hell", "kalender-suche-esc", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var search = window.GetVisualDescendants().OfType<SearchField>().First(f => f.Name == "SearchBox");
        search.Focus(NavigationMethod.Pointer);
        window.KeyTextInput("mül");
        await Pump(Settling);
        Assert.True(calendar.HasSearchHits);
        Assert.Equal("mül", calendar.SearchText);

        // 1. Esc: die Trefferliste schließt, der Text bleibt.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(calendar.HasSearchHits);
        Assert.Equal("mül", calendar.SearchText);
        Assert.True(search.IsFocused);

        // 2. Esc: das Feld leert sich und behält den Fokus.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("", calendar.SearchText);
        Assert.True(search.IsFocused);

        // 3. Esc: der Fokus verlässt das Suchfeld.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(search.IsFocused);

        // Tippt man erneut, öffnet sich die Liste wieder.
        search.Focus(NavigationMethod.Pointer);
        window.KeyTextInput("mül");
        await Pump(Settling);
        Assert.True(calendar.HasSearchHits);
        return calendar;
    });

    [Fact]
    public Task Suchfeld_Plus_gibt_es_nur_mit_Befehl_nennt_sich_per_Tooltip_und_Namen_und_laesst_das_Loeschkreuz_in_Ruhe() => Session.Dispatch(async () =>
    {
        var added = new Counter();
        var field = new SearchField { PlaceholderText = "Name, Telefon oder E-Mail", AddText = "Neue Kundin oder neuen Kunden anlegen" };
        var window = await ShowAsync("hell", field);
        var add = field.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("searchadd"));
        var clear = field.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("searchclear"));

        // Ohne Befehl gibt es das Plus nicht.
        Assert.False(add.IsEffectivelyVisible);

        field.AddCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => added.Value++);
        await Pump();
        Assert.True(add.IsEffectivelyVisible);
        Assert.Equal("Neue Kundin oder neuen Kunden anlegen", ToolTip.GetTip(add));
        Assert.Equal("Neue Kundin oder neuen Kunden anlegen", Avalonia.Automation.AutomationProperties.GetName(add));
        ClickCenter(window, add);
        await Pump();
        Assert.Equal(1, added.Value);

        // Das Löschkreuz bleibt, wie es war: nur mit Text, links davon das Plus.
        Assert.False(clear.IsEffectivelyVisible);
        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("anna");
        await Pump();
        Assert.True(clear.IsEffectivelyVisible);
        Assert.True(add.IsEffectivelyVisible);
        Assert.True(add.TranslatePoint(default, field)!.Value.X < clear.TranslatePoint(default, field)!.Value.X, "Plus links vom Kreuz");
        Assert.Equal("Suche leeren", Avalonia.Automation.AutomationProperties.GetName(clear));
        ClickCenter(window, clear);
        await Pump();
        Assert.Equal("", field.Text);
        Assert.Equal(1, added.Value);

        // Mit ShowAdd = false (Treffer schon gewählt) ist das Plus weg, mit true wieder da; ohne Befehl wieder weg.
        field.ShowAdd = false;
        await Pump();
        Assert.False(add.IsEffectivelyVisible);
        field.ShowAdd = true;
        await Pump();
        Assert.True(add.IsEffectivelyVisible);
        field.AddCommand = null;
        await Pump();
        Assert.False(add.IsEffectivelyVisible);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Neuer_Termin_hat_das_Plus_im_Suchfeld_oeffnet_damit_die_Neuanlage_mit_dem_Namen_und_keinen_Umschalter_mehr() => Render("hell", "kalender-termin-plus", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var booking = calendar.Booking!;
        await Pump();
        var panel = SidePanelOf(window);
        var search = panel.GetVisualDescendants().OfType<SearchField>().Single();
        var add = search.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("searchadd"));

        Assert.True(add.IsEffectivelyVisible);
        Assert.Equal("Neue Kundin oder neuen Kunden anlegen", ToolTip.GetTip(add));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Neue Kundin / neuer Kunde");
        Assert.False(booking.ShowNewCustomer);

        booking.CustomerQuery = "Anna Bauer";
        await Pump();
        ClickCenter(window, add);
        await Pump();
        Assert.True(booking.ShowNewCustomer);
        Assert.Equal(("Anna", "Bauer"), (booking.NewFirstName, booking.NewLastName));
        var names = panel.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("Anna", names);
        Assert.Contains("Bauer", names);

        var cancel = panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Abbrechen");
        ClickCenter(window, cancel);
        await Pump();
        Assert.False(booking.ShowNewCustomer);

        // Ist jemand gewählt, braucht es kein Plus.
        booking.Customer = SpaSample.Customers[0];
        await Pump();
        Assert.True(booking.HasChosenCustomer);
        Assert.False(add.IsEffectivelyVisible);
        return calendar;
    });
}

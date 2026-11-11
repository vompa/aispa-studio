using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenkarte und Kundendaten-Maske nach R01 Slice 08 (W37, W39, W43, W-Muster 1): jedes Feld hat einen Namen, der nicht aus dem Platzhalter stammt,
/// Warnzeilen der Kennzahlen stehen in Warnfarbe mit Symbol, und ein gesperrtes „Speichern“ nennt den Grund.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Karte oder Abschnitt (Kundenkarte seit R07 Slice 141) mit dieser Überschrift; der innerste passende Rahmen.</summary>
    private static Border CardWithTitle(Visual root, string title) =>
        root.GetVisualDescendants().OfType<Border>().Last(b => (b.Classes.Contains("card") || b.Classes.Contains("section"))
            && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == title && t.Classes.Contains("group")));

    private static void ScrollToTop(Control card)
    {
        var scroll = card.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(0, card.TranslatePoint(new Point(0, 0), scroll.Content as Visual ?? scroll)!.Value.Y);
    }

    /// <summary>Jedes sichtbare Eingabe- und Auswahlfeld unter <paramref name="root"/> hat für den Screenreader einen Namen, und der ist nicht sein Platzhalter.</summary>
    private static void AssertFieldsNamedNotFromPlaceholder(Visual root, int atLeast)
    {
        var fields = root.GetVisualDescendants().OfType<Control>()
            .Where(c => c is TextBox or ComboBox && c.IsEffectivelyVisible && c.GetVisualAncestors().OfType<ComboBox>().FirstOrDefault() is null)
            .ToList();
        Assert.True(fields.Count >= atLeast, $"Nur {fields.Count} Felder gefunden");
        Assert.All(fields, field =>
        {
            var placeholder = field switch
            {
                TextBox box => box.PlaceholderText,
                ComboBox box => box.PlaceholderText,
                _ => null,
            };
            var name = ControlAutomationPeer.CreatePeerForElement(field).GetName();
            Assert.False(string.IsNullOrWhiteSpace(name), $"{field.GetType().Name} (Platzhalter „{placeholder}“) ohne Namen");
            Assert.NotEqual(placeholder, name);
        });
    }

    [Fact]
    public Task Kundenkarte_Kennzahl_mit_Warnung_zeigt_Warnfarbe_und_Warnsymbol_statt_Rot() => Render("hell", "kunden-kennzahl-warnung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        Assert.Equal("1× nicht erschienen · 1 späte Absage", shell.Customers.StatsWarning);

        // Given „nicht erschienen“ ist eine Warnung, When die Karte sie zeigt, Then Warnfarbe und Warnsymbol, nicht Rot.
        var warning = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "StatsWarning");
        Assert.True(warning.IsEffectivelyVisible);
        var text = warning.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == shell.Customers.StatsWarning);
        var color = ((ISolidColorBrush)text.Foreground!).Color;
        Assert.Equal(Resolve(text, "WarnBrush"), color);
        Assert.NotEqual(Resolve(text, "CritBrush"), color);
        Assert.Contains(AttentionHint(window).GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Warning && i.IsEffectivelyVisible);
        Assert.Equal(shell.Customers.StatsWarningTip, ToolTip.GetTip(warning));

        // Die Kennzahlen selbst bleiben ohne Symbol in der Hauptfarbe.
        var stats = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "StatsLine");
        Assert.Equal(Resolve(stats, "FgBrush"), ((ISolidColorBrush)stats.Foreground!).Color);
        return calendar;
    });

    [Fact]
    public Task Kundendaten_Maske_ohne_Vor_und_Nachname_nennt_am_Speichern_den_Grund() => Render("hell", "kunden-bearbeiten-ohne-name", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[1].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        shell.Customers.Contact!.FirstName = "";
        shell.Customers.Contact.LastName = "";
        await Pump();

        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
        var save = ButtonWithText(form, "Speichern");
        Assert.False(save.IsEffectivelyEnabled);
        Assert.Equal("Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.", ToolTip.GetTip(save));
        Assert.True(ToolTip.GetShowOnDisabled(save));
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Customers[1].Id}", SpaSample.Customers[1])
        .On($"GET /api/customers/{SpaSample.Customers[1].Id}/appointments", new List<AppointmentDto>()));

    [Fact]
    public Task Kundenkarte_Wuensche_und_Rechnungsempfaenger_im_Bearbeiten_haben_Namen_nicht_aus_dem_Platzhalter() => Render("hell", "kunden-wuensche-rechnung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        // Nur eine Karte zugleich im Bearbeiten-Zustand (Slice 15): erst den Rechnungsempfänger, dann die Wünsche prüfen.
        shell.Customers.EditBillingCommand.Execute(null);
        await Pump();
        Assert.True(shell.Customers.IsEditingBilling);
        AssertFieldsNamedNotFromPlaceholder(CardWithTitle(window, "Rechnungsempfänger"), 3);

        shell.Customers.EditWishesCommand.Execute(null);
        await Pump();
        Assert.True(shell.Customers.IsEditingWishes);
        var wishes = CardWithTitle(window, "Wünsche");
        AssertFieldsNamedNotFromPlaceholder(wishes, 7);

        // Für das Bild: die Wünsche oben im Ausschnitt.
        ScrollToTop(wishes);
        await Pump();
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Termine_am_liebsten_und_Kennzeichen_haben_Namen_und_Wuenscht_steht_ueber_der_Auswahl() => Render("hell", "kunden-termine-kennzeichen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        // Nur eine Karte zugleich im Bearbeiten-Zustand (Slice 15): erst das Kennzeichen, dann die Termine prüfen.
        shell.Customers.AddFlagCommand.Execute(null);
        await Pump();
        AssertFieldsNamedNotFromPlaceholder(CardWithTitle(window, "Kennzeichen"), 2);

        await shell.Customers.EditBookingCommand.ExecuteAsync(null);
        await Pump();
        var booking = CardWithTitle(window, "Termine am liebsten");
        AssertFieldsNamedNotFromPlaceholder(booking, 4);

        var label = booking.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Wünscht" && t.IsEffectivelyVisible);
        var gender = booking.GetVisualDescendants().OfType<ComboBox>().First(c => c.ItemsSource == GenderChoice.All);
        var labelBottom = label.TranslatePoint(new Point(0, label.Bounds.Height), window)!.Value.Y;
        var genderTop = gender.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        Assert.True(labelBottom <= genderTop, $"„Wünscht“ endet bei {labelBottom}, die Auswahl beginnt bei {genderTop}");

        // Für das Bild: „Termine am liebsten“ oben im Ausschnitt.
        ScrollToTop(booking);
        await Pump();
        return calendar;
    });

    [Fact]
    public Task Neue_Kundin_hat_an_jedem_Feld_einen_Namen() => Render("hell", "kunden-neu", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.StartNewCommand.Execute(null);
        await Pump();
        Assert.True(shell.Customers.ShowNew);

        var mask = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("card") && b.IsEffectivelyVisible
            && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Neue Kundin / neuer Kunde"));
        // Die Maske beginnt mit vier Feldern; „Weitere Angaben“ bringt Anrede, Titel, Rufname und Notiz dazu.
        AssertFieldsNamedNotFromPlaceholder(mask, 4);
        shell.Customers.NewMoreOpen = true;
        await Pump();
        AssertFieldsNamedNotFromPlaceholder(mask, 8);
        return calendar;
    });
}

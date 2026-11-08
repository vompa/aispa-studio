using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Aufbau der Kundenkarte nach R07 Slice 141 (Ä349): „Gut zu wissen“ zuerst mit Warn-Chip, Leeres nur in „Noch nicht erfasst“, „Termin buchen“ als Hauptknopf,
/// Verwaltung im Menü „…“ nur mit Recht, Werte in der Hauptfarbe mit mindestens 4,5:1.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly CustomerDto EmptyGuest = SpaSample.Customers[1];

    /// <summary>Bernhard Müllner ohne Wünsche, Kennzeichen, Vermerk, Lieblingstermine und ohne Termine.</summary>
    private static StubApi EmptyGuestApi(StubApi? api = null) => (api ?? Api())
        .On($"GET /api/customers/{EmptyGuest.Id}", EmptyGuest)
        .On($"GET /api/customers/{EmptyGuest.Id}/appointments", new List<AppointmentDto>())
        .On($"GET /api/customers/{EmptyGuest.Id}/insights", SpaSample.Insights() with { ReceptionNote = null })
        .On($"GET /api/customers/{EmptyGuest.Id}/flags", new List<CustomerFlagDto>());

    private static Border CustomerCardBorder(Window window) => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CustomerCard");

    private static List<Border> VisibleSections(Window window) =>
        window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "GoodToKnow")
            .GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("section") && b.IsEffectivelyVisible).ToList();

    [Fact]
    public Task Kundenkarte_Nicht_behandeln_steht_als_Warn_Chip_im_ersten_Block() => Render("hell", "kunden-warn-chip", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given Wünsche „Druck sanft“ und „nicht behandeln: Füße (kitzlig)“; When die Karte offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then ist der erste Abschnitt von „Gut zu wissen“ die Wünsche, und ihr erster Chip ist der Warn-Chip mit Dreieck.
        var first = VisibleSections(window)[0];
        Assert.Equal("WishesSection", first.Name);
        var chip = first.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("tag"));
        Assert.Contains("warn", chip.Classes);
        Assert.Contains(chip.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Nicht behandeln: Füße");
        Assert.Contains(chip.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Warning && i.IsEffectivelyVisible);
        Assert.Equal("Nicht behandeln: Füße (kitzlig)", ToolTip.GetTip(chip));
        return calendar;
    }, Api().On($"GET /api/reports/customers/{SpaSample.Customers[0].Id}/preferences", new CustomerPreferences("sanft", null, null, null, null, null, "Füße (kitzlig)", null)));

    [Fact]
    public Task Kundenkarte_ohne_Angaben_zeigt_keine_Abschnitte_nur_noch_nicht_erfasst() => Render("hell", "kunden-leer", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given Kennzeichen, Rezeptionsvermerk und Lieblingstermine sind leer; When man die Karte öffnet
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();

        // Then gibt es dafür keine eigenen Abschnitte, nur die Zeile „Noch nicht erfasst“ mit je einem Link.
        Assert.Empty(VisibleSections(window));
        var missing = window.GetVisualDescendants().OfType<WrapPanel>().Single(p => p.Name == "MissingLine");
        Assert.True(missing.IsEffectivelyVisible);
        var links = missing.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).ToList();
        Assert.Equal(["+ Wünsche", "+ Kennzeichen", "+ Rezeptionsvermerk", "+ Termine am liebsten"], links);
        Assert.All(missing.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible), b => Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(b) as string)));

        // „+ Kennzeichen“ öffnet den Abschnitt zum Setzen, mit dem Fokus im ersten Feld.
        missing.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddFlagLink").Command!.Execute(null);
        await Pump();
        Assert.Equal(["FlagsSection"], VisibleSections(window).Select(s => s.Name));
        return calendar;
    }, EmptyGuestApi());

    [Fact]
    public Task Kundenkarte_ohne_naechsten_Termin_zeigt_keiner_mit_buchen_und_Termin_buchen_als_Hauptknopf() => Render("hell", "kunden-ohne-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given der Gast hat keinen nächsten Termin; When man die Karte öffnet
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();

        // Then steht oben „Nächster Termin: keiner“ mit „buchen“, und „Termin buchen“ ist der einzige Hauptknopf.
        var next = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "NextLine");
        Assert.True(next.IsEffectivelyVisible);
        Assert.Equal("Nächster Termin: keiner", next.Text);
        Assert.True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextBookLink").IsEffectivelyVisible);
        var view = window.GetVisualDescendants().OfType<CustomersView>().First();
        var primary = Assert.Single(view.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary") && b.IsEffectivelyVisible);
        Assert.Equal("BookButton", primary.Name);
        return calendar;
    }, EmptyGuestApi());

    [Theory]
    [InlineData("Rezeption", false)]
    [InlineData("Inhaberin", true)]
    public Task Kundenkarte_Menue_zeigt_Verwaltungsaktionen_nur_mit_Recht(string role, bool manages) => Render("hell", $"kunden-menue-{role}", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // When sie das Menü „…“ öffnet
        var more = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MoreButton");
        Assert.True(more.IsEffectivelyVisible);
        more.Flyout!.ShowAt(more);
        await Pump();

        // Then sieht die Rezeption keine Verwaltungsaktionen, die Inhaberin schon; die Alltagsaktionen sehen beide.
        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
        Assert.Contains("Bearbeiten (Name, Anrede, Kontakt, Notiz)", items);
        Assert.Contains("Weitere E-Mail-Adresse", items);
        Assert.Equal(manages, items.Contains("Leitung informieren …"));
        Assert.Equal(manages, items.Contains("Nicht mehr buchen …"));
        more.Flyout.Hide();
        return calendar;
    }, role == "Rezeption" ? ReceptionApi() : Api());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kundenkarte_Werte_in_der_Hauptfarbe_und_jeder_Text_mit_mindestens_4_5_zu_1(string theme) => Render(theme, $"kunden-kontrast-{theme}", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        var card = CustomerCardBorder(window);

        // Given jede Bezeichnung und jeder Wert der Karte; When man ihre Farben misst
        var texts = card.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Foreground is ISolidColorBrush
                && !t.GetVisualAncestors().OfType<Button>().Any(b => b.Classes.Contains("primary"))
                && !t.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("avatar")))
            .ToList();
        Assert.True(texts.Count >= 20, $"Nur {texts.Count} Texte gefunden");

        // Then haben Werte die Hauptfarbe, und jeder Text hat mindestens 4,5:1 gegen seine Fläche.
        var fg = Resolve(card, "FgBrush");
        Assert.All(texts.Where(t => t.Classes.Contains("value")), t => Assert.Equal(fg, ((ISolidColorBrush)t.Foreground!).Color));
        Assert.All(texts, t =>
        {
            var contrast = Contrast(((ISolidColorBrush)t.Foreground!).Color, SurfaceBehind(t));
            Assert.True(contrast >= 4.5, $"„{t.Text}“: Kontrast {contrast:0.00}");
        });
        return calendar;
    });

    /// <summary>Die erste deckende Fläche hinter dem Text (Chip, Pille, Karte).</summary>
    private static Color SurfaceBehind(Visual visual) => visual.GetVisualAncestors()
        .Select(a => a switch { Border b => b.Background, Panel p => p.Background, _ => null })
        .OfType<ISolidColorBrush>().First(b => b.Color.A == 255 && b.Opacity >= 1).Color;

    [Fact]
    public Task Kundenkarte_Stift_erscheint_erst_bei_Fokus_im_Abschnitt() => Render("hell", "kunden-stift", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        var pencil = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "EditWishesButton");
        Assert.Equal(0, pencil.Opacity);
        Assert.True(pencil.IsTabStop && pencil.IsEffectivelyVisible);
        pencil.Focus(Avalonia.Input.NavigationMethod.Tab);
        await Pump();
        Assert.Equal(1, pencil.Opacity);
        return calendar;
    }, Api().On($"GET /api/reports/customers/{SpaSample.Customers[0].Id}/preferences", SpaSample.Wishes()));

    [Fact]
    public Task Kundenkarte_zeigt_offene_Gutscheine_und_letzte_Kaeufe_im_Verlauf() => Render("hell", "kunden-gutscheine-kaeufe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        List<string?> Lines(string list) => window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == list && c.IsEffectivelyVisible)
            .GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        // Given ein Wertgutschein mit Restwert und zwei Käufe ohne Termin; When die Karte offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then stehen sie im Verlauf unter „Offene Gutscheine“ und „Letzte Käufe“.
        Assert.Equal(["Wertgutschein …4F2K: 35,00 € offen, gültig bis 31.12.2026"], Lines("VoucherList"));
        Assert.Equal(["3.11.2026 · Massageöl 2× · 24,00 €", "20.10.2026 · Kerze · 5,00 €"], Lines("PurchaseList"));

        // Ohne Gutscheine und Käufe gibt es beide Abschnitte nicht.
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<ItemsControl>(), c => c.Name is "VoucherList" or "PurchaseList" && c.IsEffectivelyVisible);
        return calendar;
    }, EmptyGuestApi().On($"GET /api/customers/{SpaSample.Customers[0].Id}/insights", SpaSample.Insights() with
    {
        Vouchers = [new(VoucherKind.MultiPurpose, "4F2K", null, 35m, "EUR", new DateOnly(2026, 12, 31))],
        Purchases = [new(new DateOnly(2026, 11, 3), "Massageöl", 2, 24m, "EUR"), new(new DateOnly(2026, 10, 20), "Kerze", 1, 5m, "EUR")],
    }));
}

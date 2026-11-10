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

    private static Border AttentionHint(Window window) => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "AttentionHint");

    [Fact]
    public Task Kundenkarte_Nicht_behandeln_steht_im_Warnstreifen_ueber_der_oberen_Drittellinie() => Render("hell", "kunden-warn-chip", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given Wünsche „Druck sanft“ und „nicht behandeln: Füße (kitzlig)“; When die Karte offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then steht „Nicht behandeln: Füße“ im Warnstreifen (Hinweis „Warnung“ mit Dreieck), nicht noch einmal bei den Wünschen.
        var hint = AttentionHint(window);
        Assert.True(hint.IsEffectivelyVisible);
        Assert.Contains("warn", hint.Classes);
        Assert.Contains(hint.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Warning && i.IsEffectivelyVisible);
        var avoid = hint.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "AvoidWarning");
        Assert.Equal("Nicht behandeln: Füße", avoid.Text);
        Assert.Equal("Nicht behandeln: Füße (kitzlig)", ToolTip.GetTip(avoid));
        var wishes = VisibleSections(window).Single(s => s.Name == "WishesSection");
        Assert.DoesNotContain(wishes.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Nicht behandeln: Füße");

        // Drittelregel (masken.md R14): der Streifen beginnt im oberen Drittel der Karte.
        var card = CustomerCardBorder(window);
        var top = hint.TranslatePoint(new Point(0, 0), card)!.Value.Y;
        Assert.True(top < card.Bounds.Height / 3, $"Warnstreifen bei {top:0} von {card.Bounds.Height:0} px");
        return calendar;
    }, Api().On($"GET /api/reports/customers/{SpaSample.Customers[0].Id}/preferences", new CustomerPreferences("sanft", null, null, null, null, null, "Füße (kitzlig)", null)));

    [Fact]
    public Task Kundenkarte_Verlauf_mit_Kachel_Stammperson_und_ohne_Umbruch() => Render("hell", "kunden-verlauf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given der Beispielverlauf von Brigitte Müller (meist bei Lena Huber); When die Karte bei 1440 px offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then steht der nächste Termin als Kachel oben im Verlauf, „meist bei Lena Huber“ einmal bei „Zuletzt“.
        var timeline = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "Timeline");
        var tile = timeline.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "NextVisitTile");
        Assert.True(tile.IsEffectivelyVisible);
        Assert.Contains(timeline.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "meist bei Lena Huber" && t.IsEffectivelyVisible);

        // Keine Zeile unter „Zuletzt“ und in der Kachel bricht um.
        var lines = timeline.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "RecentList").GetVisualDescendants().OfType<TextBlock>()
            .Concat(tile.GetVisualDescendants().OfType<TextBlock>()).Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).ToList();
        Assert.NotEmpty(lines);
        Assert.All(lines, t => Assert.True(t.TextLayout.TextLines.Count == 1, $"„{t.Text}“ bricht um"));
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Customers[0].Id}/appointments", SpaSample.CustomerHistory()));

    [Fact]
    public Task Kundenkarte_teilt_Gut_zu_wissen_und_Verlauf_drei_zu_zwei() => Render("hell", "kunden-drei-zu-zwei", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given das Fenster ist 1440 breit; When die Karte offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then ist „Gut zu wissen“ links 1,5-mal so breit wie der Verlauf rechts (±2 px, das Layout rundet auf ganze Pixel), beide auf derselben Höhe.
        var columns = window.GetVisualDescendants().OfType<MainSideColumns>().Single(c => c.Name == "CardColumns");
        Assert.False(columns.IsStacked);
        var good = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "GoodToKnow");
        var timeline = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "Timeline");
        Assert.InRange(timeline.Bounds.Width, (good.Bounds.Width / 1.5) - 2, (good.Bounds.Width / 1.5) + 2);
        Assert.Equal(good.Bounds.Y, timeline.Bounds.Y);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_stellt_den_Verlauf_bei_schmalem_Fenster_unter_Gut_zu_wissen() => Render("hell", "kunden-schmal", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given das Fenster hat die Mindestbreite; When die Karte offen ist
        window.Width = window.MinWidth;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then steht der Verlauf in voller Breite unter „Gut zu wissen“ statt in einer schmalen Spalte daneben.
        var columns = window.GetVisualDescendants().OfType<MainSideColumns>().Single(c => c.Name == "CardColumns");
        Assert.True(columns.Bounds.Width < columns.StackBelow, $"Spalten {columns.Bounds.Width:0} px breit");
        Assert.True(columns.IsStacked);
        var good = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "GoodToKnow");
        var timeline = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "Timeline");
        Assert.Equal(good.Bounds.Width, timeline.Bounds.Width);
        Assert.True(timeline.Bounds.Y >= good.Bounds.Bottom);
        return calendar;
    });

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
    public Task Kundenkarte_ohne_naechsten_Termin_hat_Termin_buchen_als_einzigen_Hauptknopf() => Render("hell", "kunden-ohne-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        // Given der Gast hat keinen nächsten Termin; When man die Karte öffnet
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();

        // Then gibt es keine eigene Zeile „Nächster Termin“ und keinen Link „buchen“ mehr (ADR 0094); „Termin buchen“ ist der einzige Hauptknopf.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "NextLine" or "NextBookLink");
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
        List<string?> Chips() => [.. window.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "SideChipList")
            .GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text)];

        // Given ein Paket, ein Wertgutschein mit Restwert und zwei Käufe ohne Termin; When die Karte offen ist
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Then stehen sie im Verlauf als Chips, die Käufe als ein Chip mit dem letzten Kauf.
        Assert.Equal(["Hot-Stone-Massage: 7 von 10 offen", "Gutschein 35,00 €", "Letzter Kauf: Massageöl 2× · 24,00 €"], Chips());

        // Ohne Gutscheine und Käufe bleibt nur das Paket.
        await shell.ShowCustomerAsync(EmptyGuest.Id);
        await Pump();
        Assert.Equal(["Hot-Stone-Massage: 7 von 10 offen"], Chips());
        return calendar;
    }, EmptyGuestApi().On($"GET /api/customers/{SpaSample.Customers[0].Id}/insights", SpaSample.Insights() with
    {
        Vouchers = [new(VoucherKind.MultiPurpose, "4F2K", null, 35m, "EUR", new DateOnly(2026, 12, 31))],
        Purchases = [new(new DateOnly(2026, 11, 3), "Massageöl", 2, 24m, "EUR"), new(new DateOnly(2026, 10, 20), "Kerze", 1, 5m, "EUR")],
    }));
}

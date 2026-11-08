using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5 Ä35: Tastenübersicht „Tasten im Kalender“ (Design-Brief 5.22 und 7): Gruppen in fester Reihenfolge, zwei gleich breite Spalten, feste Tastenspalte, höchstens zwei Zeilen.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] KeyGroupTitles = ["Tag", "Buchen", "Markierter Termin", "Seitenleiste und Reiter", "Allgemein"];

    [Fact]
    public void Tastenhilfe_hat_die_Gruppen_in_der_Reihenfolge_des_Briefs_mit_den_neuen_Reiter_Tasten()
    {
        Assert.Equal(KeyGroupTitles, CalendarViewModel.AllKeys.Select(g => g.Title));
        Assert.Equal(["Tag", "Buchen"], CalendarViewModel.KeysLeft.Select(g => g.Title));
        Assert.Equal(["Markierter Termin", "Seitenleiste und Reiter", "Allgemein"], CalendarViewModel.KeysRight.Select(g => g.Title));
        var rail = CalendarViewModel.AllKeys.Single(g => g.Title == "Seitenleiste und Reiter");
        Assert.Equal(["Esc", "Strg+S", "Strg+Tab", "Strg+W", "Strg+1 … 9", "F6"], rail.Keys.Select(k => k.Key));
        Assert.Contains(CalendarViewModel.AllKeys.SelectMany(g => g.Keys), k => k.Key == "Rechtsklick Kopf");
        Assert.Equal("Wunsch eintippen (KI): Enter bucht Vorschlag 1, Strg+2/3 die anderen", CalendarViewModel.AllKeys.SelectMany(g => g.Keys).Single(k => k.Key == "Strg+K").Text);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Tastenhilfe_zwei_gleich_breite_Spalten_feste_Tastenspalte_Beschreibungen_buendig_hoechstens_zwei_Zeilen(string theme) => Render(theme, "tastenhilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ShowKeyHelp = true;
        await Pump(Settling);

        var card = Named<Border>(window, "KeyHelp");
        Assert.True(card.IsEffectivelyVisible);
        var lists = card.GetVisualDescendants().OfType<ItemsControl>().Where(l => l.ItemsSource is IReadOnlyList<KeyHintGroup>).ToList();
        Assert.Equal(2, lists.Count);

        // Gruppentitel: Stufe Zwischentitel 16/22 Semibold, in der Reihenfolge Tag, Buchen | Markierter Termin, Seitenleiste und Reiter, Allgemein.
        var titles = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("h3")).OrderBy(t => LeftOf(window, t) >= LeftOf(window, lists[1]) ? 1 : 0).ThenBy(t => TopOf(window, t)).ToList();
        Assert.Equal(KeyGroupTitles, titles.Select(t => t.Text));
        Assert.All(titles, t =>
        {
            Assert.Equal(16, t.FontSize);
            Assert.Equal(22, t.LineHeight);
            Assert.Equal(FontWeight.SemiBold, t.FontWeight);
        });

        // Zwei gleich breite Spalten.
        Assert.Equal(lists[0].Bounds.Width, lists[1].Bounds.Width, 0.5);

        var rows = card.GetVisualDescendants().OfType<Grid>().Where(g => g.Children.OfType<Border>().Any(b => b.Classes.Contains("kbd"))).ToList();
        Assert.Equal(CalendarViewModel.AllKeys.Sum(g => g.Keys.Count), rows.Count);

        // Feste Tastenspalte: in beiden Spalten dieselbe Breite, nach der längsten Taste; kein Chip ist breiter oder abgeschnitten.
        var chips = rows.Select(r => r.Children.OfType<Border>().Single(b => b.Classes.Contains("kbd"))).ToList();
        var keyColumn = rows.Select(r => r.Children.OfType<TextBlock>().Single().Bounds.X).Distinct().Select(x => Math.Round(x, 1)).ToList();
        Assert.Single(keyColumn);
        var widest = chips.Max(c => c.Bounds.Width);
        Assert.Equal(keyColumn[0] - 12, widest, 1.0);
        Assert.Equal("Rechtsklick Kopf", chips.Single(c => c.Bounds.Width == widest).GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.All(chips, c =>
        {
            var text = c.GetVisualDescendants().OfType<TextBlock>().Single();
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(text.DesiredSize.Width <= text.Bounds.Width + 0.5, $"Taste „{text.Text}“ ist abgeschnitten");
        });

        // Tasten-Chips in einheitlicher Höhe.
        Assert.Single(chips.Select(c => Math.Round(c.Bounds.Height, 1)).Distinct());

        // Beschreibungen beginnen in jeder Spalte bündig und brauchen höchstens zwei Zeilen, ohne gekürzt zu sein.
        var descriptions = rows.Select(r => r.Children.OfType<TextBlock>().Single()).ToList();
        foreach (var list in lists)
        {
            var column = descriptions.Where(d => list.IsVisualAncestorOf(d)).Select(d => Math.Round(LeftOf(window, d) - LeftOf(window, list), 1)).Distinct().ToList();
            Assert.Single(column);
        }

        foreach (var text in descriptions)
        {
            Assert.True(text.Bounds.Height <= 32.5, $"„{text.Text}“ braucht {text.Bounds.Height:0} px");
            var (lines, trimming, width) = (text.MaxLines, text.TextTrimming, text.Bounds.Width);
            text.MaxLines = 0;
            text.TextTrimming = TextTrimming.None;
            text.Measure(new Size(width, double.PositiveInfinity));
            Assert.True(text.DesiredSize.Height - text.Margin.Top <= 32.5, $"„{text.Text}“ bricht auf mehr als zwei Zeilen um");
            text.MaxLines = lines;
            text.TextTrimming = trimming;
        }

        // Unten steht der Link zum Handbuch, die Gruppen sind vollständig sichtbar.
        var link = Named<Button>(window, "KeyManualLink");
        Assert.Equal("Alle Tasten im Handbuch", link.Content);
        Assert.True(link.IsEffectivelyVisible);
        Assert.True(TopOf(window, link) > TopOf(window, descriptions.OrderBy(d => TopOf(window, d)).Last()));
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<ScrollViewer>(), s => s.Extent.Height > s.Viewport.Height + 0.5);
        return calendar;
    });

    [Fact]
    public Task Der_Link_unten_oeffnet_das_Handbuchthema_der_Tasten_und_schliesst_die_Tastenhilfe() => Render("hell", "tastenhilfe-link", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        calendar.ShowKeyHelp = true;
        await Pump(Settling);

        Named<Button>(window, "KeyManualLink").Command!.Execute(null);
        await Pump(Settling);

        Assert.False(calendar.ShowKeyHelp);
        Assert.Same(shell.Help, shell.SelectedPage!.Page);
        Assert.Contains("thema=kalender-tastenkuerzel", shell.Help.Address?.Query);
        return calendar;
    });
}

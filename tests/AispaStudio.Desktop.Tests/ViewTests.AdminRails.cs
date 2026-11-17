using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Gesamttest zu ADR 0098, Regel 1: Jede Verwaltungsseite hat unter dem Balken höchstens eine Schiene mit höchstens fünf Einträgen, die nie umbricht; jede Karte
/// (Personenkarte, Behandlung) hat höchstens vier Bereiche. Geprüft wird als Inhaberin (sie sieht am meisten), im Fenster der Bildtests und beim Mindestfenster 1100 × 680.
/// </summary>
public sealed partial class ViewTests
{
    private const int MaxRailEntries = 5;
    private const int MaxCardAreas = 4;

    private static readonly string[] CardRailNames = ["CardTabs", "SectionTabs"];

    /// <summary>Die Schiene der Seite (Bereichsleiste der Gruppe oder eigene Schiene der Maske) und die Schienen der Karten, die gerade sichtbar sind.</summary>
    private static (List<List<TabButton>> Pages, List<List<TabButton>> Cards) VisibleRails(Window window)
    {
        var pages = new List<List<TabButton>>();
        var cards = new List<List<TabButton>>();
        var strip = Named<OverflowStrip>(window, "AreaStrip");
        if (strip.IsEffectivelyVisible)
        {
            Assert.Empty(strip.Overflowed);
            pages.Add([.. strip.Children.OfType<TabButton>().Where(b => b != strip.OverflowButton && b.IsEffectivelyVisible)]);
        }

        var host = Named<ContentControl>(window, "AreaHost");
        foreach (var rail in host.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("segmented") && b.IsEffectivelyVisible && b.FindAncestorOfType<MovementBookingPanel>() is null))
        {
            var entries = rail.GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).ToList();
            (CardRailNames.Contains(rail.Name) ? cards : pages).Add(entries);
        }

        return (pages, cards);
    }

    private static void AssertRailsOnOneLine(Window window, string place, List<TabButton> entries, int max, Control host)
    {
        Assert.True(entries.Count <= max, $"{place}: {entries.Count} Einträge statt höchstens {max}");
        Assert.True(entries.Count > 0, $"{place}: Schiene ohne Einträge");
        Assert.All(entries, e => Assert.True(Math.Round(TopOf(window, e)) == Math.Round(TopOf(window, entries[0])), $"{place}: „{e.Content}“ steht nicht auf der Höhe von „{entries[0].Content}“ (Umbruch)"));
        Assert.True(RightOf(window, entries[^1]) <= RightOf(window, host) + 0.5, $"{place}: Schiene reicht über die Seite hinaus");
    }

    private static void AssertRails(Window window, string place, int maxRail, int maxCard)
    {
        var (pages, cards) = VisibleRails(window);
        var host = Named<ContentControl>(window, "AreaHost");
        Assert.True(pages.Count <= 1, $"{place}: {pages.Count} Schienen unter dem Balken statt einer");
        Assert.True(cards.Count <= 1, $"{place}: {cards.Count} Kartenschienen statt einer");
        foreach (var entries in pages)
        {
            AssertRailsOnOneLine(window, $"{place} (Seite)", entries, maxRail, host);
        }

        foreach (var entries in cards)
        {
            AssertRailsOnOneLine(window, $"{place} (Karte)", entries, maxCard, host);
        }
    }

    /// <summary>Prüft die Seite, wie sie steht, und danach nach jedem Klick auf einen Eintrag ihrer eigenen Schiene oder der Karte (die Bereichsleiste der Gruppe wählt die Seite selbst).</summary>
    private static async Task VisitRails(Window window, string place, int maxRail, int maxCard)
    {
        AssertRails(window, place, maxRail, maxCard);
        var (pages, cards) = VisibleRails(window);
        var skip = Named<OverflowStrip>(window, "AreaStrip").IsEffectivelyVisible ? 1 : 0;
        var labels = pages.Skip(skip).Concat(cards).SelectMany(r => r).Select(e => e.Content as string).OfType<string>().Distinct().ToList();
        foreach (var label in labels)
        {
            var (p, c) = VisibleRails(window);
            var entry = p.Skip(skip).Concat(c).SelectMany(r => r).FirstOrDefault(e => e.Content as string == label);
            if (entry is null)
            {
                continue;
            }

            ClickCenter(window, entry);
            await Pump(Settling);
            AssertRails(window, $"{place} / {label}", maxRail, maxCard);
        }
    }

    private static async Task SetMinimumSize(Window window)
    {
        window.MinWidth = window.MinHeight = 0;
        (window.Width, window.Height) = (1100, 680);
        await Pump(Settling);
        await Pump(Settling);
    }

    private static async Task CheckGroupRails(Window window, MainViewModel main, string groupTitle, int maxRail, int maxCard)
    {
        var shell = await OpenAdminPage(window, main);
        var group = shell.AdminPage.Groups.Single(g => g.Title == groupTitle);
        foreach (var area in group.Areas)
        {
            await OpenMask(window, shell, area.Title);
            if (shell.AdminPage.Content is TeamViewModel)
            {
                await ClickRow(window, "Lena Huber");
            }

            await VisitRails(window, $"{groupTitle} / {area.Title}", maxRail, maxCard);
            await SetMinimumSize(window);
            await VisitRails(window, $"{groupTitle} / {area.Title} bei 1100 × 680", maxRail, maxCard);
        }
    }

    [Theory]
    [InlineData("Betrieb")]
    [InlineData("Kassa & Geld")]
    [InlineData("Kontakt")]
    public Task Jede_Verwaltungsseite_hat_hoechstens_eine_Schiene_mit_fuenf_Eintraegen(string group) =>
        Render("hell", "probe-verwaltung-schienen", async (window, main) =>
        {
            await CheckGroupRails(window, main, group, MaxRailEntries, MaxCardAreas);
            return ((ShellViewModel)main.Content!).AdminPage;
        }, SettingsApi());

    [Fact]
    public Task Jede_Verwaltungsseite_hat_hoechstens_eine_Schiene_Behandlungen() =>
        Render("hell", "probe-verwaltung-schienen-behandlungen", async (window, main) =>
        {
            await CheckGroupRails(window, main, "Behandlungen", MaxRailEntries, MaxCardAreas);
            return ((ShellViewModel)main.Content!).AdminPage;
        }, TreatmentApi());

    [Fact]
    public Task Jede_Verwaltungsseite_hat_hoechstens_eine_Schiene_Team() =>
        Render("hell", "probe-verwaltung-schienen-team", async (window, main) =>
        {
            await CheckGroupRails(window, main, "Team", MaxRailEntries, MaxCardAreas);
            return ((ShellViewModel)main.Content!).AdminPage;
        }, RosterApi().Api);

    [Fact]
    public Task Jede_Verwaltungsseite_hat_hoechstens_eine_Schiene_Lager() =>
        Render("hell", "probe-verwaltung-schienen-lager", async (window, main) =>
        {
            await CheckGroupRails(window, main, "Lager", MaxRailEntries, MaxCardAreas);
            return ((ShellViewModel)main.Content!).AdminPage;
        }, StockApi());

    [Fact]
    public Task Jede_Verwaltungsseite_hat_hoechstens_eine_Schiene_Zahlen() =>
        Render("hell", "probe-verwaltung-schienen-zahlen", async (window, main) =>
        {
            await CheckGroupRails(window, main, "Zahlen", MaxRailEntries, MaxCardAreas);
            return ((ShellViewModel)main.Content!).AdminPage;
        }, ReportsApi());
}

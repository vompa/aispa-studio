using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 103 (Ä191, Ä286): jedes Sprungziel in die Verwaltung öffnet eine native Maske, nie einen Browser. Die Ziele stehen im Masken-Inventar
/// (Tabelle „Sprungziele in die Verwaltung“, Spalte „Ziel heute“); geöffnet wird über den echten Weg (Verweis des Assistenten, Sprung aus dem Kalender).
/// </summary>
public sealed partial class ViewTests
{
    private const string NoRightNotice = "Diese Maske lässt sich mit der eigenen Rolle nicht öffnen. Das Recht vergibt die Leitung im Bereich „Team“.";
    private const string UnknownNotice = "Diese Stelle gibt es in der Windows-App nicht. Die passende Maske oben im Balken der Verwaltung wählen.";
    private const string NoRightToast = "Diese Maske braucht ein Recht, das die Leitung vergibt";

    /// <summary>Ein Sprungziel aus dem Inventar: woher (Kalender oder Assistent), Seite und Reiter.</summary>
    private sealed record JumpTarget(bool FromCalendar, string Page, string? Tab);

    /// <summary>Die Ziele in Backticks aus der Spalte „Ziel heute“ (z. B. <c>masterData/treatments</c>, <c>stock</c>); Zeilen ohne Backticks sind keine Sprünge.</summary>
    private static List<JumpTarget> InventoryJumpTargets()
    {
        var inventory = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "umsetzung", "masken-inventar.md"));
        var section = inventory[inventory.IndexOf("## Sprungziele in die Verwaltung", StringComparison.Ordinal)..];
        var end = section.IndexOf("\n## ", 5, StringComparison.Ordinal);
        section = end < 0 ? section : section[..end];
        var targets = new List<JumpTarget>();
        foreach (var line in section.Split('\n').Where(l => l.StartsWith('|')))
        {
            var cells = line.Split('|');
            if (cells.Length < 4)
            {
                continue;
            }

            foreach (Match key in Regex.Matches(cells[2], @"`([A-Za-z]+(?:/[A-Za-z]+)?)`"))
            {
                var parts = key.Groups[1].Value.Split('/');
                targets.Add(new JumpTarget(cells[1].Trim().StartsWith("Kalender", StringComparison.Ordinal), parts[0], parts.Length > 1 ? parts[1] : null));
            }
        }

        return targets;
    }

    private static bool HasBrowser(Window window) => window.GetVisualDescendants().Any(v => v is NativeWebView);

    private static bool ShowsNotice(Window window, string text) =>
        window.GetVisualDescendants().OfType<InfoBar>().Any(b => b.IsEffectivelyVisible && b.Message == text);

    private static async Task<ShellViewModel> SignedInShell(MainViewModel main)
    {
        await SignedInCalendar(main);
        await Pump();
        return (ShellViewModel)main.Content!;
    }

    private static void AssertNativeMask(Window window, ShellViewModel shell, string where)
    {
        Assert.True(shell.SelectedPage?.Page == shell.AdminPage, $"{where}: die Verwaltung ist nicht offen");
        Assert.True(shell.AdminPage.Content is INativeArea, $"{where}: keine native Maske, sondern {shell.AdminPage.Content?.GetType().Name ?? "nichts"}");
        Assert.False(HasBrowser(window), $"{where}: im Fenster hängt ein Browser");
    }

    [Fact]
    public Task Jedes_Sprungziel_jeder_Seite_oeffnet_als_Inhaberin_eine_native_Maske_und_keinen_Browser() => Render("hell", "sprungziele", async (window, main) =>
    {
        var targets = InventoryJumpTargets();
        // Die Tabelle hat heute 13 Zeilen mit Ziel in Backticks; weniger hieße, das Lesen der Tabelle ist kaputt.
        Assert.True(targets.Count >= 13, $"Nur {targets.Count} Sprungziele im Inventar gefunden");
        var shell = await SignedInShell(main);

        foreach (var target in targets)
        {
            var where = $"{(target.FromCalendar ? "Kalender" : "Assistent")} → {target.Page}/{target.Tab}";
            shell.SelectedPage = shell.Pages[0];
            await Pump();
            if (target.FromCalendar)
            {
                shell.Calendar.AdminRequested!(target.Page, target.Tab, null);
            }
            else
            {
                shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Öffnen", target.Page, target.Tab));
            }

            await Pump(Settling);
            AssertNativeMask(window, shell, where);
            var area = shell.AdminPage.SelectedArea!;

            // R07 Slice 144: Die Betriebsinfo ist ein Reiter in „Zahlen“; das Ziel öffnet diese Maske beim Reiter „Betriebsinfo“.
            var (page, tab) = target.Page == "businessInfo" ? ("reports", null) : (target.Page, target.Tab);
            Assert.True(area.Page == page && (area.Tab == tab || area.Tab is null), $"{where}: offen ist {area.Title}");
            if (target.Page == "businessInfo")
            {
                Assert.True(shell.AdminPage.Reports!.IsBusinessInfoTab, $"{where}: der Reiter Betriebsinfo ist nicht offen");
            }

            Assert.False(ShowsNotice(window, NoRightNotice) || ShowsNotice(window, UnknownNotice), $"{where}: Hinweis statt Maske");
        }

        // Kalender, Erinnerung an ein Folgegespräch: „Zur Personalakte“ öffnet die Personenkarte im Bereich Team.
        shell.SelectedPage = shell.Pages[0];
        await Pump();
        shell.Calendar.HrFileRequested!(SpaSample.Lena.Id);
        await Pump(Settling);
        AssertNativeMask(window, shell, "Kalender → Personalakte");
        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        return shell.AdminPage;
    });

    [Fact]
    public Task Sprungziel_ohne_Recht_zeigt_einen_Hinweis_und_keinen_Browser() => Render("hell", "sprungziel-ohne-recht", async (window, main) =>
    {
        var shell = await SignedInShell(main);

        // Die Rezeption darf die Kosten nicht sehen (ViewAmountReports fehlt).
        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "costs", null));
        await Pump(Settling);

        AssertNativeMask(window, shell, "Rezeption → costs");
        Assert.Same(shell.AdminPage.Areas[0], shell.AdminPage.SelectedArea);
        Assert.True(ShowsNotice(window, NoRightNotice), "Hinweis fehlt");

        // Der Hinweis verschwindet beim nächsten Sprung, der gelingt.
        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Öffnen", "stock", null));
        await Pump(Settling);
        Assert.Equal("Lager", shell.AdminPage.SelectedArea?.Title);
        Assert.False(ShowsNotice(window, NoRightNotice), "Hinweis bleibt nach dem nächsten Sprung stehen");
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Unbekanntes_Sprungziel_zeigt_einen_Hinweis_und_keinen_Browser() => Render("hell", "sprungziel-unbekannt", async (window, main) =>
    {
        var shell = await SignedInShell(main);

        // Lohn liegt seit R05 Slice 100 auf der Personenkarte; eine Seite „payroll“ gibt es in der Windows-App nicht.
        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Öffnen", "payroll", null));
        await Pump(Settling);

        AssertNativeMask(window, shell, "Inhaberin → payroll");
        Assert.Same(shell.AdminPage.Areas[0], shell.AdminPage.SelectedArea);
        Assert.True(ShowsNotice(window, UnknownNotice), "Hinweis fehlt");
        return shell.AdminPage;
    });

    [Fact]
    public Task Sprungziel_ohne_jeden_Verwaltungsbereich_bleibt_auf_der_Seite_und_zeigt_einen_Hinweis() => Render("hell", "sprungziel-ohne-verwaltung", async (window, main) =>
    {
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        Assert.Empty(shell.AdminPage.Areas);
        var help = shell.Pages.Single(p => p.Page == shell.Help);
        shell.SelectedPage = help;
        await Pump(Settling);

        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Öffnen", "costs", null));
        await Pump(Settling);

        Assert.Same(help, shell.SelectedPage);
        Assert.False(HasBrowser(window));
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("toast") && b.Classes.Contains("shown")
            && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == NoRightToast));
        // meldungen.md M1: die Meldung unten ist eine Zeile mit höchstens 60 Zeichen und ohne Punkt am Ende.
        Assert.True(NoRightToast.Length <= 60 && !NoRightToast.EndsWith('.'));
        return shell;
    }, Api().On("GET /api/auth/me", new CurrentUser("u3", "kasse@demo.aispa.test", "Kai Kassa", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Therapist, true, []), null, [AccessRight.ChangeStatus])));
}

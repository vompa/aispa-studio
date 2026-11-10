using System.Text.RegularExpressions;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R01 Slice 19 (W26, W47, W-Muster 5): Reiterschienen melden den gewählten Reiter, Aufklapp-Köpfe melden „aufgeklappt“ statt „aktiviert“.</summary>
public sealed partial class ViewTests
{
    private static bool IsSelectedFor(Control tab) => Assert.IsAssignableFrom<ISelectionItemProvider>(ControlAutomationPeer.CreatePeerForElement(tab)).IsSelected;

    private static string LabelOf(Button tab) => tab.Content as string ?? tab.GetVisualDescendants().OfType<TextBlock>().First().Text!;

    [Fact]
    public Task Gegeben_die_Kassa_mit_gewaehltem_Reiter_Belege_Wenn_die_Peers_abgefragt_werden_Dann_meldet_Belege_ausgewaehlt_und_die_anderen_Reiter_nicht() =>
        Render("hell", "kassa-reiter-peer", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            shell.Pos.ShowReceiptsCommand.Execute(null);
            await Pump();

            var page = window.GetVisualDescendants().OfType<PosView>().First();
            var strip = page.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("segmented"));
            var tabs = strip.GetVisualDescendants().OfType<Button>().ToList();
            Assert.Equal(["Kassieren", "Belege", "Kassenlade & Abschluss"], tabs.Select(LabelOf));
            Assert.All(tabs, t => Assert.Equal(AutomationControlType.TabItem, ControlAutomationPeer.CreatePeerForElement(t).GetAutomationControlType()));
            Assert.Equal([false, true, false], tabs.Select(IsSelectedFor));

            // Der Wechsel wird gemeldet, damit der Screenreader den neuen Zustand erfährt, ohne neu zu fragen.
            var changes = new List<(string, object?)>();
            foreach (var tab in tabs)
            {
                ControlAutomationPeer.CreatePeerForElement(tab).PropertyChanged += (_, e) =>
                {
                    if (e.Property == SelectionItemPatternIdentifiers.IsSelectedProperty)
                    {
                        changes.Add((LabelOf(tab), e.NewValue));
                    }
                };
            }

            shell.Pos.ShowCheckoutCommand.Execute(null);
            await Pump();
            Assert.Equal([true, false, false], tabs.Select(IsSelectedFor));
            Assert.Equal([("Belege", (object?)false), ("Kassieren", true)], changes.OrderBy(c => c.Item1, StringComparer.Ordinal));

            // „Auswählen“ per Screenreader wählt den Reiter wie ein Klick.
            ((ISelectionItemProvider)ControlAutomationPeer.CreatePeerForElement(tabs[2])).Select();
            await Pump();
            Assert.True(shell.Pos.IsClosing);
            return shell.Pos;
        });

    [Fact]
    public Task Gegeben_Warum_ist_aufgeklappt_Wenn_der_Peer_abgefragt_wird_Dann_meldet_er_ExpandCollapseState_Expanded() =>
        Render("hell", "ki-warum-peer", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await Pump();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
            await shell.Companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?");
            await Pump(Settling);

            var footer = Footers(window.GetVisualDescendants().OfType<CompanionContent>().Single()).First();
            var why = footer.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "WhyToggle");
            var expand = Assert.IsAssignableFrom<IExpandCollapseProvider>(ControlAutomationPeer.CreatePeerForElement(why));
            Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);

            ClickCenter(window, why);
            await Pump();
            Assert.True(why.IsChecked);
            Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
            return calendar;
        }, Api().On("POST /api/assistant", AssistantSample));

    [Fact]
    public Task Reiter_unten_in_der_Seitenleiste_melden_den_gewaehlten_Reiter() => Render("hell", "kalender-reiter-peer", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(WithTimesDay().Appointments.First(a => a.Id == SpaSample.Selected.Id));
        calendar.OpenWaitlistCommand.Execute(null);
        await Pump();

        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");
        var tabs = rail.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("paneltab") && b.Name is null && b.IsEffectivelyVisible).ToList();
        Assert.Equal(["Warteliste", "Termin"], tabs.Select(LabelOf));
        Assert.All(tabs, t => Assert.Equal(AutomationControlType.TabItem, ControlAutomationPeer.CreatePeerForElement(t).GetAutomationControlType()));
        Assert.Equal([true, false], tabs.Select(IsSelectedFor));
        return calendar;
    }, WithTimes());

    /// <summary>Zustandschips der Kalender-Kopfzeile, die keine Reiter sind (Schließzeit, alle Mitarbeitenden zeigen, Art-Chips des Verlaufs: mehrere zugleich an).</summary>
    private static readonly string[] ChipsWithoutTabState = ["HeadClosure", "HeadStaffFilter", "HeadRoomFilter", "KindChip"];

    [Fact]
    public void Kein_Aufklappknopf_more_und_kein_Reiter_mit_Klasse_on_ist_ein_einfacher_Knopf()
    {
        var problems = new List<string>();
        var source = Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop");
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        foreach (var file in Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories).Where(f => !f.Contains(obj, StringComparison.Ordinal)))
        {
            foreach (Match tag in ToggleButtonOrButtonTag().Matches(File.ReadAllText(file)))
            {
                var element = tag.Value;
                var isMore = element.StartsWith("<ToggleButton", StringComparison.Ordinal) && MoreClass().IsMatch(element);
                var isTab = element.StartsWith("<Button", StringComparison.Ordinal) && element.Contains("Classes.on=", StringComparison.Ordinal)
                    && !ChipsWithoutTabState.Any(name => element.Contains($"x:Name=\"{name}\"", StringComparison.Ordinal));
                if (isMore || isTab)
                {
                    problems.Add($"{Path.GetFileName(file)}: {element[..Math.Min(element.Length, 90)]}");
                }
            }
        }

        Assert.Empty(problems);
    }

    [GeneratedRegex(@"<(ToggleButton|Button)\s[^>]*>")]
    private static partial Regex ToggleButtonOrButtonTag();

    [GeneratedRegex(@"Classes=""[^""]*\bmore\b")]
    private static partial Regex MoreClass();
}

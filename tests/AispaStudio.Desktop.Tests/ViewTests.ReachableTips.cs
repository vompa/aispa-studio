using AispaStudio.Desktop.Controls;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// W-Muster 12: Information, die nur im Tooltip steht, ist auch per Tastatur erreichbar. <c>ToolTipBehavior.Reachable</c> macht ein Element mit Tooltip zum Tab-Stopp
/// und nennt den Tooltip dem Screenreader als Hilfetext; <c>ToolTipBehavior.ReachableGroup</c> fasst mehrere zu einem Stopp mit Pfeiltasten zusammen.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Erreichbarer_Text_nimmt_den_Tab_Fokus_zeigt_den_Tooltip_und_nennt_ihn_als_Hilfetext() => InWindow("hell", () => new StackPanel
    {
        Children = { new Button { Content = "Vorher" }, new TextBlock { Text = "Frau Mag. Huber" }, new Button { Content = "Nachher" } },
    }, 300, 200, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var before = (Button)panel.Children[0];
        var line = (TextBlock)panel.Children[1];
        ToolTip.SetTip(line, "Adresszeile mit dem gültigen Namen.");
        ToolTipBehavior.SetReachable(line, true);
        before.Focus(NavigationMethod.Pointer);
        await Pump();

        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(line, Focused(window));
        Assert.Contains("tipstop", line.Classes);
        var peer = ControlAutomationPeer.CreatePeerForElement(line);
        Assert.Equal("Frau Mag. Huber", peer.GetName());
        Assert.Equal("Adresszeile mit dem gültigen Namen.", peer.GetHelpText());

        // Der Tooltip erscheint wie bei jedem Tab-Fokus nach der Verzögerung.
        await Task.Delay(400);
        await Pump();
        Assert.True(ToolTip.GetIsOpen(line));

        // Ein gebundener Tooltip (Sterne, Kennzahlen) zieht den Hilfetext nach.
        ToolTip.SetTip(line, "7 Besuche in zwölf Monaten");
        Assert.Equal("7 Besuche in zwölf Monaten", AutomationProperties.GetHelpText(line));

        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(panel.Children[2], Focused(window));
        Assert.False(ToolTip.GetIsOpen(line));
    });

    [Fact]
    public Task Kennzahlen_Gruppe_ist_ein_Tab_Stopp_und_Pfeile_wechseln_ohne_Umlauf() => InWindow("hell", () =>
    {
        var group = new UniformGrid { Columns = 2 };
        foreach (var (label, tip) in new[] { ("Besuche gesamt 14", "Erledigte Besuche insgesamt"), ("Letzter Besuch vor 5 Wochen", "Letzter Besuch: Montag"), ("Nicht erschienen 1", "Termine ohne Gast") })
        {
            var stat = new StackPanel { Children = { new TextBlock { Text = label } } };
            ToolTip.SetTip(stat, tip);
            AutomationProperties.SetName(stat, label);
            ToolTipBehavior.SetReachable(stat, true);
            group.Children.Add(stat);
        }

        ToolTipBehavior.SetReachableGroup(group, true);
        return new StackPanel { Children = { new Button { Content = "Vorher" }, group, new Button { Content = "Nachher" } } };
    }, 400, 300, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var group = (UniformGrid)panel.Children[1];
        ((Button)panel.Children[0]).Focus(NavigationMethod.Pointer);
        await Pump();

        // Ein Stopp: Tab landet auf der ersten Kennzahl, noch ein Tab verlässt die Gruppe.
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(group.Children[0], Focused(window));
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(panel.Children[2], Focused(window));

        // Pfeile wechseln innerhalb der Gruppe, am Ende bleibt der Fokus stehen.
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump(2);
        Assert.Same(group.Children[0], Focused(window));
        Press(window, PhysicalKey.ArrowRight);
        await Pump(2);
        Assert.Same(group.Children[1], Focused(window));
        Press(window, PhysicalKey.ArrowDown);
        await Pump(2);
        Assert.Same(group.Children[2], Focused(window));
        Press(window, PhysicalKey.ArrowRight);
        await Pump(2);
        Assert.Same(group.Children[2], Focused(window));
        Press(window, PhysicalKey.ArrowLeft);
        await Pump(2);
        Assert.Same(group.Children[1], Focused(window));
        Assert.Equal("Letzter Besuch: Montag", ControlAutomationPeer.CreatePeerForElement((Control)group.Children[1]).GetHelpText());

        // Der Tab-Stopp wandert mit dem Fokus: Tab verlässt die Gruppe, Umschalt+Tab kehrt auf dieselbe Kennzahl zurück.
        Press(window, PhysicalKey.Tab);
        await Pump(2);
        Assert.Same(panel.Children[2], Focused(window));
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump(2);
        Assert.Same(group.Children[1], Focused(window));

        // Ohne Tooltip gibt es keinen Hilfetext mehr.
        ToolTip.SetTip(group.Children[1], null);
        Assert.Null(AutomationProperties.GetHelpText((Control)group.Children[1]));
    });
}

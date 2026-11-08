using AispaStudio.Desktop.Controls;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F29: Tastenkürzel stehen im Tooltip und als <c>AcceleratorKey</c> (Design-Brief 5.22 und 9).</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Tooltip_mit_Kuerzel_setzt_den_AcceleratorKey_und_folgt_der_Aenderung() => InWindow("hell", () => new StackPanel
    {
        Children =
        {
            new Button { Name = "Heute", Content = "Heute" },
            new Button { Name = "Eigener", Content = "Eigener" },
        },
    }, 300, 160, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var today = (Button)panel.Children[0];
        var own = (Button)panel.Children[1];
        ToolTip.SetTip(today, "Heute (Pos1)");
        AutomationProperties.SetAcceleratorKey(own, "Alt+E");
        ToolTip.SetTip(own, "Eigener (Strg+E)");
        await Pump();

        Assert.Equal("Pos1", AutomationProperties.GetAcceleratorKey(today));
        // Ein ausdrücklich gesetzter Wert bleibt.
        Assert.Equal("Alt+E", AutomationProperties.GetAcceleratorKey(own));

        // Ändert sich der Text, folgt der Wert; ohne Kürzel im Text entfällt er wieder.
        ToolTip.SetTip(today, "Heute (Strg+H)");
        Assert.Equal("Strg+H", AutomationProperties.GetAcceleratorKey(today));
        ToolTip.SetTip(today, "Zum heutigen Tag");
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetAcceleratorKey(today)));
    });

    [Fact]
    public Task Kontextmenue_am_Termin_nennt_jedes_Kuerzel_auch_als_AcceleratorKey() => Render("hell", "kalender-kontextmenue-kuerzel", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);

        var menu = window.GetVisualDescendants().OfType<ContextMenu>().Single();
        var items = menu.GetVisualDescendants().OfType<MenuItem>().ToList();
        var withKey = items.Where(i => i.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Classes.Contains("menugesture"))).ToList();
        Assert.NotEmpty(withKey);
        foreach (var item in withKey)
        {
            var shown = item.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("menugesture")).Text;
            Assert.Equal(shown, AutomationProperties.GetAcceleratorKey(item));
            Assert.DoesNotContain(", Taste", AutomationProperties.GetName(item), StringComparison.Ordinal);
        }

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        return calendar;
    });

    [Fact]
    public Task Jedes_Tooltip_mit_Kuerzel_hat_in_der_App_den_AcceleratorKey_und_Einchecken_und_Kassieren_nennen_E_und_K() => Session.Dispatch(async () =>
    {
        await WithApp(async (window, shell) =>
        {
            shell.Companion.IsOpen = false;
            await Pump(Settling);
            var calendar = shell.Calendar;
            calendar.OpenAppointment(SpaSample.Selected);
            await Pump(Settling);

            var named = new List<string>();
            foreach (var page in shell.Pages)
            {
                shell.SelectedPage = page;
                await Pump(Settling);
                foreach (var control in window.GetVisualDescendants().OfType<Control>())
                {
                    if (ToolTip.GetTip(control) is string tip && ShortcutHints.Parse(tip) is { } expected)
                    {
                        named.Add($"{page.Title}: {tip}");
                        Assert.True(expected == AutomationProperties.GetAcceleratorKey(control), $"{tip}: AcceleratorKey fehlt oder weicht ab");
                    }
                }
            }

            // Der Kalender nennt mehrere (Pos1, ←, →, G, F5, N, Strg 0, Esc, Entf, E, K ...).
            Assert.True(named.Count(n => n.StartsWith("Kalender", StringComparison.Ordinal)) >= 8, string.Join("; ", named));

            shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
            await Pump(Settling);
            var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
            Assert.Contains(buttons, b => ToolTip.GetTip(b) as string == "Einchecken (E)" && AutomationProperties.GetAcceleratorKey(b) == "E");
            calendar.OpenAppointment(SpaSample.CalendarDay().Appointments.First(a => a.CustomerName == "Laura Novak"));
            await Pump(Settling);
            buttons = [.. window.GetVisualDescendants().OfType<Button>()];
            Assert.Contains(buttons, b => b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Kassieren (K)" && AutomationProperties.GetAcceleratorKey(b) == "K");
        });
        return true;
    }, CancellationToken.None);
}

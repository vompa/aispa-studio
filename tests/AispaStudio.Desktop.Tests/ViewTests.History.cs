using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä184, Slice 90: Abschnitt „Verlauf“ im Termin-Panel, eingeklappt unter „Kunde“, aufgeklappt neueste oben mit Datum und Uhrzeit.</summary>
public sealed partial class ViewTests
{
    private static List<HistoryEntry> SampleHistory() =>
    [
        new(SpaSample.At(9, 5), "Verschoben von Lena Huber: 10:00 → 10:30"),
        new(SpaSample.At(8, 12).AddDays(-6), "Angelegt per SMS"),
    ];

    [Fact]
    public Task Verlauf_steht_eingeklappt_unter_Kunde_und_zeigt_aufgeklappt_die_Eintraege_neueste_oben() => Render("hell", "termin-verlauf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var panel = await OpenCustomerPanel(window, calendar);
        var header = Named<AccordionHeader>(panel, "HistoryHeader");

        Assert.False(header.IsChecked);
        Assert.Equal("Verlauf", AutomationProperties.GetName(header));
        Assert.Equal(ExpandCollapseState.Collapsed, StateOf(header));
        Assert.True(TopOf(window, header) > TopOf(window, Named<AccordionHeader>(panel, "CustomerHeader")));
        Assert.False(calendar.Selected!.HistorySection.HasEntries);

        await ClickIntoView(window, header);
        for (var i = 0; i < 40 && !calendar.Selected!.HistorySection.HasEntries; i++)
        {
            await Pump();
        }

        await Pump(Settling);
        Assert.True(header.IsChecked);
        Assert.Equal(ExpandCollapseState.Expanded, StateOf(header));
        var rows = panel.GetVisualDescendants().OfType<StackPanel>().Where(p => p.DataContext is HistoryRow && p.IsEffectivelyVisible).ToList();
        Assert.Equal(["Verschoben von Lena Huber: 10:00 → 10:30, 19.10.2026, 09:05", "Angelegt per SMS, 13.10.2026, 08:12"],
            rows.Select(r => AutomationProperties.GetName(r)));
        Assert.True(TopOf(window, rows[0]) < TopOf(window, rows[1]));
        return calendar;
    }, Api().On($"GET /api/appointments/{SpaSample.Selected.Id}/history", SampleHistory()));
}

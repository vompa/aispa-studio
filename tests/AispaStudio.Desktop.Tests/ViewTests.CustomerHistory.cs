using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä184, Slice 91, T-036: Abschnitt „Änderungen“ (nicht „Verlauf“, Ä369) als letzte Karte der Kundenkarte, eingeklappt, lädt beim Aufklappen.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kundenkarte_hat_den_Abschnitt_Aenderungen_eingeklappt_als_Tab_Stopp_und_zeigt_aufgeklappt_die_Eintraege() => Render("hell", "kunde-verlauf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump(Settling);
        var customers = shell.Customers;
        var page = window.GetVisualDescendants().OfType<CustomersView>().First();
        var header = Named<AccordionHeader>(page, "HistoryHeader");

        Assert.NotNull(customers.HistorySection);
        Assert.Equal("Wer die Kundendaten wann geändert hat", customers.HistorySection!.Tip);
        Assert.False(header.IsChecked);
        Assert.True(header.Focusable && header.IsTabStop);
        Assert.Equal("Änderungen", AutomationProperties.GetName(header));
        Assert.Equal("Änderungen", header.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Änderungen").Text);
        Assert.Equal(ExpandCollapseState.Collapsed, StateOf(header));
        Assert.False(customers.HistorySection.HasEntries);

        await ClickIntoView(window, header);
        for (var i = 0; i < 40 && !customers.HistorySection.HasEntries; i++)
        {
            await Pump();
        }

        await Pump(Settling);
        Assert.Equal(ExpandCollapseState.Expanded, StateOf(header));
        var rows = page.GetVisualDescendants().OfType<StackPanel>().Where(p => p.DataContext is HistoryRow && p.IsEffectivelyVisible).ToList();
        Assert.Equal(["Geändert von Lena: Telefon 0664 111 → 0664 222, 19.10.2026, 09:05", "Angelegt von Lena, 13.10.2026, 08:12"],
            rows.Select(r => AutomationProperties.GetName(r)));
        return calendar;
    }, Api().On($"GET /api/customers/{SpaSample.Customers[0].Id}/history",
        new List<HistoryEntry>
        {
            new(SpaSample.At(9, 5), "Geändert von Lena: Telefon 0664 111 → 0664 222"),
            new(SpaSample.At(8, 12).AddDays(-6), "Angelegt von Lena"),
        }));
}

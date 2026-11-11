using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Bild für die Hilfe „Gast anonymisieren“: die Kundenkarte als Inhaberin mit offenem Menü „…“.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kundenkarte_Gast_anonymisieren_steht_im_Menue_der_Inhaberin() => Render("hell", "kunden-anonymisieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        Assert.True(shell.Customers.MayAnonymize);

        var more = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MoreButton" && b.IsEffectivelyVisible);
        more.Flyout!.ShowAt(more);
        await Pump(Settling);

        var menu = (MenuFlyout)more.Flyout;
        Assert.True(menu.IsOpen);
        var item = menu.Items.OfType<MenuItem>().Single(i => i.Name == "AnonymizeItem");
        Assert.Equal("Gast anonymisieren …", item.Header);
        Assert.True(item.IsVisible);
        return calendar;
    });
}

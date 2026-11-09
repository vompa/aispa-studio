using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben E (ADR 0091): „Tagesplan drucken …“ im Menü „+ Termin“ des Kalenders (mit der Wahl Team oder Räume), der Knopfblock in der Wartelisten-Leiste, „Stammblatt als PDF“
/// im Menü der Kundenkarte und die Knopfblöcke zu den Gutscheinen unter dem Beleg in der Kassa.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kalender_Menue_nennt_Tagesplan_drucken_und_oeffnet_den_Knopfblock_mit_Team_oder_Raeumen() => Render("hell", "kalender-tagesplan-drucken", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var split = window.GetVisualDescendants().OfType<SplitButton>().First(s => s.Flyout is MenuFlyout);
        Assert.Contains("Tagesplan drucken …", ((MenuFlyout)split.Flyout!).Items.OfType<MenuItem>().Select(i => i.Header as string));
        // Der Knopfblock liegt in einem Popup, das ein eigenes Fenster ist: gesucht wird über das Popup, nicht über den Baum des Fensters.
        var popup = window.GetVisualDescendants().OfType<Popup>().Single(p => p.Child is Border { Name: "DayPlanPrintCard" });
        Assert.False(popup.IsOpen);

        calendar.OpenDayPlanPrintCommand.Execute(null);
        await Pump(Settling);

        Assert.True(popup.IsOpen);
        var card = (Border)popup.Child!;
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal(["Team", "Räume"], card.GetVisualDescendants().OfType<RadioButton>().Where(r => r.IsEffectivelyVisible).Select(r => r.Content as string));
        Assert.True(Named<RadioButton>(card, "PlanByTeam").IsChecked);
        return calendar;
    }, Api());

    [Fact]
    public Task Kalender_Warteliste_traegt_den_Knopfblock_Drucken_und_PDF_speichern() => Render("hell", "kalender-warteliste-pdf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenWaitlistCommand.Execute(null);
        await Pump(Settling);

        var panel = Named<Border>(window, "SidePanel");
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(panel));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("Druckt die offenen Wünsche", StringComparison.Ordinal) == true);
        return calendar;
    }, Api());

    [Fact]
    public Task Kundenkarte_Menue_nennt_Stammblatt_als_PDF_und_oeffnet_den_Knopfblock_ohne_Senden() => Render("hell", "kunde-stammblatt-pdf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        var page = window.GetVisualDescendants().OfType<CustomersView>().First();
        var more = Named<Button>(page, "MoreButton");
        more.Flyout!.ShowAt(more);
        await Pump();

        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
        Assert.Contains("Stammblatt als PDF", items);
        Assert.False(Named<Border>(page, "MasterSheetCard").IsEffectivelyVisible);

        shell.Customers.ToggleMasterSheetCommand.Execute(null);
        await Pump(Settling);

        var card = Named<Border>(page, "MasterSheetCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        return calendar;
    }, Api());

    [Fact]
    public Task Kassa_Given_Gutschein_verkauft_Then_Knopfblock_je_Gutschein_unter_dem_Beleg() => Render("hell", "kassa-gutschein-als-pdf", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89,00";
        await shell.Pos.PayCommand.ExecuteAsync(null);
        await Pump(Settling);

        var page = window.GetVisualDescendants().OfType<PosView>().First();
        var voucherBlocks = page.GetVisualDescendants().OfType<StackPanel>().Where(s => s.IsEffectivelyVisible && s.Name == "VoucherDocument").ToList();
        Assert.Single(voucherBlocks);
        Assert.Equal(["PrintButton", "SaveButton", "SendButton"], VisibleButtonNames(voucherBlocks[0]));
        Assert.Single(voucherBlocks[0].GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Name == "VoucherHandOverButton");
        Assert.Contains(voucherBlocks[0].GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Gutschein K7QM-2XPA-9RDT als Geschenk");
        return shell.Pos;
    }, Api().On($"POST /api/pos/bills/{SpaSample.BillId}/checkout",
        SpaSample.Receipt() with { IssuedVouchers = [new VoucherDto(Guid.NewGuid(), "K7QM-2XPA-9RDT", VoucherKind.MultiPurpose, 50m, 50m, null, VoucherStatus.Active, null)] }));
}

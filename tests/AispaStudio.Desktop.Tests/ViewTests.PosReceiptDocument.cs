using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben B (ADR 0092): Unter dem Beleg steht der Knopfblock „A4 drucken · PDF speichern (Bon-Format im Menü) · Per Mail senden“, nach dem Bezahlen und im Reiter „Belege“;
/// der Bondrucker-Knopf „Drucken“ bleibt der erste Knopf.
/// </summary>
public sealed partial class ViewTests
{
    private static void AssertReceiptDocumentBlock(PosView page)
    {
        var visible = page.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        var print = Assert.Single(visible.OfType<Button>(), b => b.Name == "PrintButton");
        Assert.Contains(print.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "A4 drucken");
        Assert.Single(visible.OfType<SplitButton>(), b => b.Name == "SaveSplitButton");
        Assert.Single(visible.OfType<Button>(), b => b.Name == "SendButton");
        Assert.DoesNotContain(visible.OfType<Button>(), b => b.Name == "SaveButton");
        Assert.Single(visible.OfType<Button>(), b => b.Name != "PrintButton" && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Drucken"));
    }

    [Fact]
    public Task Kassa_Given_bezahlt_When_Beleg_angezeigt_Then_Knopfblock_unter_dem_Beleg() =>
        Render("hell", "kassa-beleg-als-pdf", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            shell.Pos.Payments.First(p => p.Method == AispaStudio.Contracts.PaymentMethod.Card).Amount = "89,00";
            await shell.Pos.PayCommand.ExecuteAsync(null);
            await Pump(Settling);

            AssertReceiptDocumentBlock(window.GetVisualDescendants().OfType<PosView>().First());
            return shell.Pos;
        }, Api());

    [Fact]
    public Task Kassa_Given_Reiter_Belege_When_Beleg_gewaehlt_Then_Knopfblock_unter_dem_Beleg() =>
        Render("hell", "kassa-belege-als-pdf", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var pos = shell.Pos;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            pos.ShowReceiptsCommand.Execute(null);
            await pos.LoadReceiptsAsync();
            pos.SelectedReceiptItem = pos.Receipts.First();
            await Pump(Settling);

            AssertReceiptDocumentBlock(window.GetVisualDescendants().OfType<PosView>().First());
            return pos;
        }, Api());
}

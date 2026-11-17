using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>PDF-Ausgaben, Zusatz (ADR 0092): „Gutscheinliste …“ in der Kopfzeile der Kassa öffnet eine Karte mit Zeitraum, Status, zwei Schaltern und dem Knopfblock „Drucken · PDF speichern“.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kassa_Given_Gutscheinliste_geoeffnet_Then_Karte_mit_Filtern_und_Knopfblock() => Render("hell", "kassa-gutscheinliste", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Gutscheinliste …");
        var popup = page.GetVisualDescendants().OfType<Popup>().Single(p => p.Child is Border { Name: "VoucherListCard" });
        Assert.False(popup.IsOpen);

        shell.Pos.ToggleVoucherListCommand.Execute(null);
        await Pump(Settling);

        Assert.True(popup.IsOpen);
        var card = (Border)popup.Child!;
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal(["läuft in 30 Tagen ab", "noch nicht versendet"], card.GetVisualDescendants().OfType<CheckBox>().Where(c => c.IsEffectivelyVisible).Select(c => c.Content as string));
        Assert.NotNull(Named<DateField>(card, "VoucherListFromField"));
        Assert.NotNull(Named<DateField>(card, "VoucherListUntilField"));
        Assert.Equal(5, Named<ComboBox>(card, "VoucherListStatus").ItemCount);
        return shell.Pos;
    }, Api());

    [Fact]
    public Task Kassa_Given_Online_Zahlungen_geoeffnet_Then_Gutscheine_und_Erstattungen_mit_Stand() => Render("hell", "kassa-online-zahlungen", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        Assert.Equal("Online-Zahlungen", AutomationProperties.GetName(Named<Button>(page, "OnlinePaymentsButton")));
        var popup = page.GetVisualDescendants().OfType<Popup>().Single(p => p.Child is Border { Name: "OnlinePaymentsCard" });

        await shell.Pos.ToggleOnlinePaymentsCommand.ExecuteAsync(null);
        await Pump(Settling);

        Assert.True(popup.IsOpen);
        var texts = ((Border)popup.Child!).GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("Wertgutschein 50,00 € · 19.10.2026 09:12", texts);
        Assert.Contains("Erstattungen", texts);
        return shell.Pos;
    }, PosOnlinePaymentsTests.WithPayments(Api()));
}

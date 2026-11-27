using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>PDF-Ausgaben, Zusatz (ADR 0092): der Eintrag „Drucken oder als PDF speichern …“ im Menü „Ausgabe“ der Kassa (T-125) öffnet eine Karte mit Zeitraum, Status, zwei Schaltern und dem Knopfblock „Drucken · PDF speichern“.</summary>
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
        Assert.Equal("Ausgabe", AutomationProperties.GetName(Named<Button>(page, "PosOutputButton")));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Control>(), c => c.Name is "VoucherListButton" or "PosExportButton" or "PosExportSaved");
        var popup = page.GetVisualDescendants().OfType<Popup>().Single(p => p.Child is Border { Name: "VoucherListCard" });
        Assert.False(popup.IsOpen);

        shell.Pos.ToggleVoucherListCommand.Execute(null);
        await Pump(Settling);

        Assert.True(popup.IsOpen);
        var card = (Border)popup.Child!;
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal(["Läuft in 30 Tagen ab", "Noch nicht versendet"], card.GetVisualDescendants().OfType<CheckBox>().Where(c => c.IsEffectivelyVisible).Select(c => c.Content as string));
        Assert.NotNull(Named<DateField>(card, "VoucherListFromField"));
        Assert.NotNull(Named<DateField>(card, "VoucherListUntilField"));
        Assert.Equal(5, Named<ComboBox>(card, "VoucherListStatus").ItemCount);
        return shell.Pos;
    }, Api());

    [Fact]
    public Task Kassa_Menue_Ausgabe_hat_beide_Gruppen_und_ist_mit_der_Tastatur_erreichbar() => Render("hell", "kassa-menue-ausgabe", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        var output = Named<Button>(page, "PosOutputButton");
        Assert.True(output.IsEffectivelyVisible);
        output.Focus();
        await Pump(Settling);

        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.True(output.Flyout!.IsOpen);
        var headers = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
        Assert.Equal(["Gutscheinliste · alle Gutscheine", "Drucken oder als PDF speichern …", "Listen · Belegtag"], headers[..3]);
        Assert.Equal(15, headers.Count);

        // Gruppenüberschriften sind abgeschaltet: die Pfeile überspringen sie und erreichen jeden Befehl.
        var reached = new List<string?>();
        if (Focused(window) is not MenuItem)
        {
            Press(window, PhysicalKey.ArrowDown);
            await Pump();
        }

        for (var step = 0; step < 3; step++)
        {
            reached.Add((Focused(window) as Control)?.Name);
            Press(window, PhysicalKey.ArrowDown);
            await Pump();
        }

        Assert.Equal(["PosVoucherListItem", "PosReceiptsExcelItem", "PosReceiptsCsvItem"], reached);

        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(output.Flyout.IsOpen);
        Assert.Same(output, Focused(window));
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

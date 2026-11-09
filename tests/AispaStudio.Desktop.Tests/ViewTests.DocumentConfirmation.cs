using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben C (ADR 0091): Im Termin-Panel steht hinter „Mehr“ der Eintrag „Bestätigung …“, der den Knopfblock mit Drucken, Speichern und Senden öffnet; in der Kundenkarte
/// steht im Menü „…“ der Eintrag „Auskunft als PDF“, der den Knopfblock ohne Senden öffnet.
/// </summary>
public sealed partial class ViewTests
{
    private static List<string> VisibleButtonNames(Control area) =>
        [.. area.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Name is "PrintButton" or "SaveButton" or "SendButton").Select(b => b.Name!)];

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Termin_Panel_Mehr_oeffnet_die_Terminbestaetigung_mit_Drucken_Speichern_und_Senden(string theme) => Render(theme, "termin-bestaetigung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var button = Named<Button>(window, "ConfirmationButton");
        Assert.False(button.IsEffectivelyVisible);

        await ClickIntoView(window, window.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Name == "MoreToggle"));
        Assert.True(button.IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "ConfirmationCard").IsEffectivelyVisible);
        await ClickIntoView(window, button);
        await Pump(Settling);

        var card = Named<Border>(window, "ConfirmationCard");
        Assert.True(card.IsEffectivelyVisible);
        card.BringIntoView();
        await Pump();
        Assert.Equal(["PrintButton", "SaveButton", "SendButton"], VisibleButtonNames(card));
        Assert.Equal($"api/appointments/{SpaSample.Selected.Id}/confirmation/pdf", calendar.Selected!.ConfirmationSource().PdfPath);
        return calendar;
    }, Api());

    [Fact]
    public Task Kundenkarte_Menue_nennt_Auskunft_als_PDF_und_oeffnet_den_Knopfblock_ohne_Senden() => Render("hell", "kunde-auskunft-pdf", async (window, main) =>
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
        Assert.Contains("Auskunft als PDF", items);
        Assert.False(Named<Border>(page, "DataAccessCard").IsEffectivelyVisible);

        shell.Customers.ToggleDataAccessCommand.Execute(null);
        await Pump(Settling);

        var card = Named<Border>(page, "DataAccessCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal($"api/customers/{SpaSample.Customers[0].Id}/history/pdf", shell.Customers.DataAccessDocument!.Source.PdfPath);
        return calendar;
    }, Api());
}

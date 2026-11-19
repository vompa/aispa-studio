using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ein geöffnetes Formular der Kundenkarte rollt so ins Bild, dass „Speichern“ sichtbar ist (masken.md R5), bei 1440 × 900 und im Mindestfenster.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("WishesSection", 1440, 900)]
    [InlineData("WishesSection", 1100, 680)]
    [InlineData("FlagsSection", 1440, 900)]
    [InlineData("FlagsSection", 1100, 680)]
    [InlineData("ReceptionNoteSection", 1440, 900)]
    [InlineData("ReceptionNoteSection", 1100, 680)]
    [InlineData("BookingSection", 1440, 900)]
    [InlineData("BookingSection", 1100, 680)]
    [InlineData("ConsentSection", 1100, 680)]
    [InlineData("BillingSection", 1100, 680)]
    [InlineData("DiscountSection", 1100, 680)]
    public Task Kundenkarte_Formular_rollt_mit_Speichern_ins_Bild(string sectionName, int width, int height) =>
        Render("hell", $"kunden-formular-{sectionName}-{width}", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            window.Width = width;
            window.Height = height;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();

            var customers = shell.Customers;
            switch (sectionName)
            {
                case "WishesSection": customers.EditWishesCommand.Execute(null); break;
                case "FlagsSection": customers.AddFlagCommand.Execute(null); break;
                case "ReceptionNoteSection": customers.EditReceptionNoteCommand.Execute(null); break;
                case "BookingSection": customers.EditBookingCommand.Execute(null); break;
                case "ConsentSection": customers.EditConsentCommand.Execute(null); break;
                case "BillingSection": customers.EditBillingCommand.Execute(null); break;
                default: customers.EditDiscountCommand.Execute(null); break;
            }

            await Pump(Settling);

            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "CardScroll");
            var section = VisibleSections(window).Single(s => s.Name == sectionName);
            var save = section.GetVisualDescendants().OfType<Button>().Last(b => b.IsEffectivelyVisible && b.Classes.Contains("primary"));
            var top = save.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
            var bottom = top + save.Bounds.Height;
            Assert.True(top >= 0 && bottom <= scroll.Bounds.Height, $"„Speichern“ in {sectionName} bei {top:0}–{bottom:0} von {scroll.Bounds.Height:0} px ({width} × {height})");
            return calendar;
        });

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1100, 680)]
    public Task Kundenkarte_Bearbeiten_rollt_mit_Speichern_ins_Bild(int width, int height) =>
        Render("hell", $"kunden-bearbeiten-sichtbar-{width}", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            window.Width = width;
            window.Height = height;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();

            shell.Customers.EditContactCommand.Execute(null);
            await Pump(Settling);

            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "CardScroll");
            var form = window.GetVisualDescendants().OfType<AispaStudio.Desktop.Views.CustomerContactForm>().Single(f => f.IsEffectivelyVisible);
            var save = form.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Classes.Contains("primary"));
            var top = save.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
            Assert.True(top >= 0 && top + save.Bounds.Height <= scroll.Bounds.Height, $"„Speichern“ bei {top:0} von {scroll.Bounds.Height:0} px ({width} × {height}); Offset {scroll.Offset.Y:0}, Extent {scroll.Extent.Height:0}, Viewport {scroll.Viewport.Height:0}, Form {form.Bounds.Height:0}");
            return calendar;
        });
}

using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenkarten nach R01 Slice 15 (W38 Teil 1, W-Muster 4): höchstens eine Karte ist im Bearbeiten-Zustand,
/// und die Kundenseite zeigt dabei genau einen Primärknopf.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Die sieben Bearbeitungen der Kundenseite: sechs Karten und die Stammdaten im Kopf.</summary>
    public static TheoryData<string> CustomerEditors => ["Kennzeichen", "Wünsche", "Vermerk", "Termine", "Rechnung", "Rabatt", "Stammdaten"];

    private static Task OpenEditor(CustomersViewModel customers, string editor) => editor switch
    {
        "Kennzeichen" => Run(customers.AddFlagCommand),
        "Wünsche" => Run(customers.EditWishesCommand),
        "Vermerk" => Run(customers.EditReceptionNoteCommand),
        "Termine" => customers.EditBookingCommand.ExecuteAsync(null),
        "Rechnung" => Run(customers.EditBillingCommand),
        "Rabatt" => customers.EditDiscountCommand.ExecuteAsync(null),
        "Stammdaten" => Run(customers.EditContactCommand),
        _ => throw new ArgumentOutOfRangeException(nameof(editor), editor, null),
    };

    private static Task Run(System.Windows.Input.ICommand command)
    {
        command.Execute(null);
        return Task.CompletedTask;
    }

    /// <summary>Welche Bearbeitungen gerade offen sind, in der Reihenfolge von <see cref="CustomerEditors"/>.</summary>
    private static List<string> OpenEditors(CustomersViewModel c) =>
        new (string Name, bool Open)[]
        {
            ("Kennzeichen", c.IsAddingFlag), ("Wünsche", c.IsEditingWishes), ("Vermerk", c.IsEditingReceptionNote),
            ("Termine", c.IsEditingBooking), ("Rechnung", c.IsEditingBilling), ("Rabatt", c.IsEditingDiscount),
            ("Stammdaten", c.IsEditingContact),
        }.Where(e => e.Open).Select(e => e.Name).ToList();

    private static int VisibleCustomerPrimaries(Window window) =>
        window.GetVisualDescendants().OfType<CustomersView>().First()
            .GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("primary") && b.IsEffectivelyVisible);

    [Fact]
    public Task Kundenkarte_Kennzeichen_offen_dann_Wuensche_bearbeiten_laesst_nur_Wuensche_offen() => Render("hell", "kunden-nur-eine-karte", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Given die Karte „Kennzeichen“ im Bearbeiten-Zustand
        shell.Customers.AddFlagCommand.Execute(null);
        await Pump();
        Assert.Equal(["Kennzeichen"], OpenEditors(shell.Customers));

        // When „Wünsche bearbeiten“ geöffnet wird
        shell.Customers.EditWishesCommand.Execute(null);
        await Pump();

        // Then ist nur die Karte „Wünsche“ im Bearbeiten-Zustand, auch in der Ansicht.
        Assert.Equal(["Wünsche"], OpenEditors(shell.Customers));
        var flags = CardWithTitle(window, "Kennzeichen");
        Assert.DoesNotContain(flags.GetVisualDescendants().OfType<ComboBox>(), c => c.IsEffectivelyVisible);
        Assert.Contains(CardWithTitle(window, "Wünsche").GetVisualDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible);
        return calendar;
    });

    [Theory]
    [MemberData(nameof(CustomerEditors))]
    public Task Kundenkarte_im_Bearbeiten_zeigt_auf_der_Kundenseite_genau_einen_Primaerknopf(string editor) => Render("hell", $"kunden-ein-primaerknopf-{editor}", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        // Given eine Karte im Bearbeiten-Zustand (nach einer anderen, damit auch der Wechsel zählt)
        await OpenEditor(shell.Customers, editor == "Wünsche" ? "Rechnung" : "Wünsche");
        await OpenEditor(shell.Customers, editor);
        await Pump();
        Assert.Equal([editor], OpenEditors(shell.Customers));

        // When die sichtbaren Primärknöpfe der Kundenseite gezählt werden, Then ist es genau einer.
        Assert.Equal(1, VisibleCustomerPrimaries(window));
        return calendar;
    });

    [Fact]
    public Task Jede_Karte_schliesst_beim_Oeffnen_die_anderen() => Render("hell", "kunden-karten-wechsel", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        foreach (var editor in CustomerEditors)
        {
            await OpenEditor(shell.Customers, editor);
            await Pump();
            Assert.Equal([editor], OpenEditors(shell.Customers));
        }

        return calendar;
    });

    [Fact]
    public Task Kundenseite_ohne_Bearbeiten_zeigt_Termin_buchen_als_Primaerknopf() => Render("hell", "kunden-ohne-bearbeiten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditWishesCommand.Execute(null);
        await Pump();
        shell.Customers.CancelWishesCommand.Execute(null);
        await Pump();

        // Mit offener Karte ist „Termin buchen“ der Hauptknopf (R07 Slice 141), „Neue Kundin“ nicht mehr.
        Assert.Empty(OpenEditors(shell.Customers));
        var view = window.GetVisualDescendants().OfType<CustomersView>().First();
        var primary = Assert.Single(view.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary") && b.IsEffectivelyVisible);
        Assert.Contains(primary.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Termin buchen");
        return calendar;
    });

    [Fact]
    public Task Kundenseite_ohne_Karte_zeigt_Neue_Kundin_als_Primaerknopf() => Render("hell", "kunden-ohne-karte", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.Selected = null;
        await Pump();

        var view = window.GetVisualDescendants().OfType<CustomersView>().First();
        var primary = Assert.Single(view.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary") && b.IsEffectivelyVisible);
        Assert.Contains(primary.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Neue Kundin / neuer Kunde");
        return calendar;
    });
}

using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenkarten nach R01 Slice 16 (W38 Teil 2, masken.md R13): Enter speichert die offene Karte, Esc schließt sie ohne zu speichern
/// (nach den Stufen der Felder), und beim Öffnen liegt der Fokus im ersten Feld.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Die sechs Karten mit dem Titel ihrer Karte und der Beschriftung ihres ersten Feldes.</summary>
    public static TheoryData<string, string, string> CustomerCardFirstFields => new()
    {
        { "Kennzeichen", "Kennzeichen", "Kennzeichen" },
        { "Wünsche", "Wünsche", "Druck" },
        { "Vermerk", "Rezeptionsvermerk", "Rezeptionsvermerk" },
        { "Termine", "Termine am liebsten", "Person" },
        { "Rechnung", "Rechnungsempfänger", "Empfänger" },
        { "Rabatt", "Rabatt", "Rabatt (%)" },
    };

    private static string DiscountRoute => $"PUT /api/customers/{SpaSample.Customers[0].Id}/discount";

    private static IInputElement? FocusedIn(Window window) => window.FocusManager!.GetFocusedElement();

    /// <summary>Beschriftung des Feldes: das umgebende <see cref="FormField"/>, sonst der Name für den Screenreader.</summary>
    private static string? FieldLabel(IInputElement? element) => element is Control control
        ? control.GetSelfAndVisualAncestors().OfType<FormField>().FirstOrDefault()?.Label is { Length: > 0 } label ? label : AutomationProperties.GetName(control)
        : null;

    private static async Task<(CustomersViewModel Customers, NumberField Percent)> OpenDiscountCard(Window window, ShellViewModel shell)
    {
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        await shell.Customers.EditDiscountCommand.ExecuteAsync(null);
        await Pump();
        var percent = CardWithTitle(window, "Rabatt").GetVisualDescendants().OfType<NumberField>().First(f => f.IsEffectivelyVisible);
        percent.Focus();
        await Pump();
        return (shell.Customers, percent);
    }

    [Fact]
    public Task Kundenkarte_Rabatt_mit_geaendertem_Wert_Esc_schliesst_und_alter_Wert_steht()
    {
        var stub = Api();
        return Render("hell", "kunden-rabatt-esc", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var (customers, percent) = await OpenDiscountCard(window, (ShellViewModel)main.Content!);
            var before = customers.DiscountText;

            // Given die Karte „Rabatt“ im Bearbeiten-Zustand mit geändertem, mit Tab bestätigtem Wert
            window.KeyTextInput("25");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            await Pump();
            Assert.Equal("25", customers.DiscountPercent);

            // When Esc gedrückt wird
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            await Pump();

            // Then ist der Abschnitt geschlossen, der alte Wert steht, nichts ist gespeichert, und der Fokus liegt auf dem Öffner: ohne Rabatt verschwindet
            // der Abschnitt (Slice 141), also auf dem Menü „…“, aus dem „Stammrabatt“ kam.
            Assert.False(customers.IsEditingDiscount);
            Assert.Equal(before, customers.DiscountText);
            Assert.DoesNotContain(stub.Calls, c => c.Route == DiscountRoute);
            Assert.False(customers.DiscountSectionVisible);
            var focused = Assert.IsType<Button>(FocusedIn(window));
            Assert.Equal("MoreButton", focused.Name);
            Assert.True(focused.IsEffectivelyVisible);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kundenkarte_Rabatt_Esc_geht_in_Stufen_erst_Feld_dann_Karte() => Render("hell", "kunden-rabatt-esc-stufen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var (customers, percent) = await OpenDiscountCard(window, (ShellViewModel)main.Content!);

        // Given getippt, nicht bestätigt; When Esc Then steht der alte Wert im Feld, und die Karte ist offen.
        window.KeyTextInput("25");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("", percent.Text);
        Assert.True(customers.IsEditingDiscount);

        // When noch einmal Esc Then ist die Karte geschlossen.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(customers.IsEditingDiscount);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Rabatt_Enter_im_Feld_speichert_und_schliesst()
    {
        var stub = Api().On(DiscountRoute, SpaSample.Customers[0] with { Discount = new CustomerDiscount(15m, CustomerDiscountScope.All) });
        return Render("hell", "kunden-rabatt-enter", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var (customers, _) = await OpenDiscountCard(window, (ShellViewModel)main.Content!);
            var putsBefore = stub.Calls.Count(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));

            // Given die Karte „Rabatt“ im Bearbeiten-Zustand; When im Feld „15“ getippt und Enter gedrückt wird
            window.KeyTextInput("15");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Pump();

            // Then ist genau einmal gespeichert (15 %), nichts anderes, und die Karte ist geschlossen.
            var puts = stub.Calls.Where(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Skip(putsBefore).ToList();
            var put = Assert.Single(puts);
            Assert.Equal(DiscountRoute, put.Route);
            Assert.Equal(15m, System.Text.Json.JsonSerializer.Deserialize<CustomerDiscountInput>(put.Body!, Desktop.Services.ApiClient.Json)!.Percent);
            Assert.False(customers.IsEditingDiscount);
            Assert.StartsWith("15 %", customers.DiscountText, StringComparison.Ordinal);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kundenkarte_Wuensche_Enter_im_Textfeld_speichert_und_schliesst()
    {
        var route = $"PUT /api/reports/customers/{SpaSample.Customers[0].Id}/preferences";
        var stub = Api().On(route, SpaSample.Wishes());
        return Render("hell", "kunden-wuensche-enter", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();

            // Given die Karte „Wünsche“ im Bearbeiten-Zustand, der Fokus im Feld „Druck“; When Enter gedrückt wird
            shell.Customers.EditWishesCommand.Execute(null);
            await Pump();
            Assert.Equal("Druck", FieldLabel(FocusedIn(window)));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Pump();

            // Then ist gespeichert und die Karte geschlossen.
            Assert.Single(stub.Calls, c => c.Route == route);
            Assert.False(shell.Customers.IsEditingWishes);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kundenkarte_Vermerk_Enter_bricht_um_und_Strg_Enter_speichert()
    {
        var route = $"PUT /api/customers/{SpaSample.Customers[0].Id}/reception-note";
        var stub = Api().On(route, new { });
        return Render("hell", "kunden-vermerk-strg-enter", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();
            shell.Customers.EditReceptionNoteCommand.Execute(null);
            await Pump();

            // Given das mehrzeilige Feld „Rezeptionsvermerk“; When Enter Then neue Zeile, nichts gespeichert, die Karte offen.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("Parkt in der Garage.");
            await Pump();
            Assert.Contains("\nParkt in der Garage.", shell.Customers.ReceptionNoteDraft.ReplaceLineEndings("\n"), StringComparison.Ordinal);
            Assert.DoesNotContain(stub.Calls, c => c.Route == route);
            Assert.True(shell.Customers.IsEditingReceptionNote);

            // When Strg+Enter Then ist gespeichert und die Karte geschlossen.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            await Pump();
            Assert.Single(stub.Calls, c => c.Route == route);
            Assert.False(shell.Customers.IsEditingReceptionNote);
            return calendar;
        }, stub);
    }

    [Theory]
    [MemberData(nameof(CustomerCardFirstFields))]
    public Task Kundenkarte_beim_Oeffnen_liegt_der_Fokus_im_ersten_Feld(string card, string title, string firstField) =>
        Render("hell", $"kunden-startfokus-{card}", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
            await Pump();

            // Given keine Karte offen; When die Karte geöffnet wird
            await OpenEditor(shell.Customers, card);
            await Pump();

            // Then liegt der Fokus im ersten Feld der Karte …
            var focused = FocusedIn(window);
            Assert.Equal(firstField, FieldLabel(focused));
            var cardBorder = CardWithTitle(window, title);
            Assert.Contains(cardBorder, ((Visual)focused!).GetVisualAncestors());

            // … und im Fenster ist genau ein sichtbarer Standardknopf: der Primärknopf dieser Karte.
            var defaultButton = Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b.IsDefault && b.IsEffectivelyVisible);
            Assert.Contains("primary", defaultButton.Classes);
            Assert.Contains(cardBorder, defaultButton.GetVisualAncestors());
            return calendar;
        });
}

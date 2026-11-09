using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Neue Kundin“ (R07 Slice 140): der Fokus steht beim Öffnen im Vornamen, die weiteren Angaben sind zu, Enter legt an, Esc bricht ab.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] CollapsedFieldNames = ["Anrede", "Titel vorangestellt", "Rufname im Haus", "Notiz"];

    private static Border NewCustomerMask(Window window) => window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "NewMask");

    [Fact]
    public Task Neue_Kundin_beginnt_mit_dem_Fokus_im_Vornamen_und_zugeklappten_weiteren_Angaben() => Render("hell", "kunden-neu-start", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();

        shell.Customers.StartNewCommand.Execute(null);
        await Pump();

        var mask = NewCustomerMask(window);
        Assert.True(mask.IsEffectivelyVisible);
        var focused = TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;
        Assert.Equal("Vorname", AutomationProperties.GetName(focused!));
        var visibleNames = mask.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).Select(AutomationProperties.GetName).ToList();
        Assert.All(CollapsedFieldNames, name => Assert.DoesNotContain(name, visibleNames));
        Assert.Contains("Anlegen und Termin buchen", mask.GetVisualDescendants().OfType<Button>().Select(b => b.Content));
        return calendar;
    });

    [Fact]
    public Task Neue_Kundin_zeigt_das_Telefon_nach_Tab_im_Format_des_Spa_Landes() => Render("hell", "kunden-neu-telefon", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.StartNewCommand.Execute(null);
        await Pump();
        var phone = NewCustomerMask(window).GetVisualDescendants().OfType<TextBox>().First(b => AutomationProperties.GetName(b) == "Telefon");
        phone.Focus();
        await Pump();

        window.KeyTextInput("0664 1234567");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await Pump(Settling);

        Assert.Equal("+43 664 1234567", phone.Text);
        Assert.Equal("+43 664 1234567", shell.Customers.NewPhone);
        return calendar;
    });

    [Fact]
    public Task Neue_Kundin_mit_falscher_E_Mail_zeigt_den_Fehler_am_Feld_und_setzt_den_Fokus_dorthin() => Render("hell", "kunden-neu-email-fehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.StartNewCommand.Execute(null);
        await Pump();
        shell.Customers.NewLastName = "Gruber";
        shell.Customers.NewEmail = "keine-adresse";

        await shell.Customers.CreateCommand.ExecuteAsync(null);
        await Pump(Settling);

        var mask = NewCustomerMask(window);
        var email = mask.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == CustomersViewModel.NewEmailField);
        Assert.Contains(mask.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == shell.Customers.NewEmailError);
        Assert.Same(email, TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());
        Assert.Null(shell.Customers.Error);
        return calendar;
    });

    [Fact]
    public Task Neue_Kundin_Esc_bricht_ab() => Render("hell", "kunden-neu-esc", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.StartNewCommand.Execute(null);
        await Pump();
        var firstName = NewCustomerMask(window).GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "NewFirstNameBox");

        firstName.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
        await Pump();

        Assert.False(shell.Customers.ShowNew);
        return calendar;
    });
}

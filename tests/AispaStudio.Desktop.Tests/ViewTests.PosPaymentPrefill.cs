using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ein leeres Betragsfeld einer Zahlart bekommt beim Fokus den offenen Rest, markiert (Slice 131, Ä312).</summary>
public sealed partial class ViewTests
{
    private static MoneyField CashField(Window window)
    {
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        var field = page.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Bar gegeben" && f.IsEffectivelyVisible);
        return (MoneyField)field.Content!;
    }

    private static Task<bool> PosCash(string name, Func<Window, MoneyField, PosViewModel, Task> act) =>
        Render("hell", name, async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            var field = CashField(window);
            field.BringIntoView();
            await Pump();
            await act(window, field, shell.Pos);
            return shell.Pos;
        }, Api());

    [Fact]
    public Task Kassa_Given_Rechnung_ohne_Zahlung_When_in_Bar_gegeben_geklickt_Then_steht_dort_89_00_ganz_markiert() =>
        PosCash("kassa-bar-klick", async (window, field, _) =>
        {
            ClickCenter(window, field);
            await Pump();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("89,00", field.Text);
            Assert.Equal(0, Math.Min(field.SelectionStart, field.SelectionEnd));
            Assert.Equal(5, Math.Max(field.SelectionStart, field.SelectionEnd));
        });

    [Fact]
    public Task Kassa_Given_Bar_vorbelegt_und_markiert_When_100_getippt_Then_zeigt_die_Kassa_11_00_Rueckgeld() =>
        PosCash("kassa-bar-tippen", async (window, field, pos) =>
        {
            ClickCenter(window, field);
            await Pump();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("100");
            await Pump();

            Assert.Equal("100", field.Text);
            Assert.Equal("Rückgeld: 11,00 €", pos.ChangeText);
        });

    [Fact]
    public Task Kassa_Given_Rechnung_ohne_Zahlung_When_per_Tab_in_Bar_gegeben_Then_steht_dort_89_00() =>
        PosCash("kassa-bar-tab", async (_, field, _) =>
        {
            field.Focus(NavigationMethod.Tab);
            await Pump();

            Assert.Equal("89,00", field.Text);
        });
}

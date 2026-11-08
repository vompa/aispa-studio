using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Der Beleg lässt sich in der Ansicht „Bezahlt“ drucken; ohne Bondrucker sagt die Ansicht, warum nicht (Slice 132, Ä314).</summary>
public sealed partial class ViewTests
{
    private const string NoPrinterText = "Kein Bondrucker eingerichtet (unter „Geräte“).";

    private static Task<bool> PaidView(string name, bool withPrinter, Action<Button, bool> check) =>
        Render("hell", name, async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            if (withPrinter)
            {
                shell.Devices.PrinterAddress = "192.168.1.60";
                await shell.Devices.SaveCommand.ExecuteAsync(null);
                shell.Pos.DevicesChanged();
            }

            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            shell.Pos.Payments.First(p => p.Method == AispaStudio.Contracts.PaymentMethod.Card).Amount = "89,00";
            await shell.Pos.PayCommand.ExecuteAsync(null);
            await Pump(Settling);

            var page = window.GetVisualDescendants().OfType<PosView>().First();
            var button = page.GetVisualDescendants().OfType<Button>()
                .Single(b => b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Drucken"));
            var hint = page.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == NoPrinterText);
            check(button, hint);
            return shell.Pos;
        }, Api());

    [Fact]
    public Task Kassa_Given_bezahlt_ohne_Bondrucker_When_Ansicht_Bezahlt_Then_Drucken_sichtbar_gesperrt_mit_Hinweis_auf_Geraete() =>
        PaidView("kassa-bezahlt-ohne-drucker", false, (button, hint) =>
        {
            Assert.False(button.IsEnabled);
            Assert.True(hint);
        });

    [Fact]
    public Task Kassa_Given_bezahlt_mit_Bondrucker_When_Ansicht_Bezahlt_Then_Drucken_aktiv_und_ohne_Hinweis() =>
        PaidView("kassa-bezahlt-mit-drucker", true, (button, hint) =>
        {
            Assert.True(button.IsEnabled);
            Assert.False(hint);
        });
}

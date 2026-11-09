using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>PDF-Ausgaben D (ADR 0091): In der Abschlussmaske der Kassa steht nach dem Tagesabschluss der Knopfblock „Drucken · PDF speichern“, ohne „Per Mail senden“.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kassa_Given_Tagesabschluss_gespeichert_Then_Knopfblock_in_der_Abschlussmaske() =>
        Render("hell", "kassa-abschluss-als-pdf", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var pos = shell.Pos;
            await shell.CheckoutAsync(SpaSample.Paying);
            pos.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
            pos.ShowClosingCommand.Execute(null);
            await pos.LoadReportAsync();
            pos.CountedCash = "356,30";
            await pos.CloseDayCommand.ExecuteAsync(null);
            await Pump(Settling);

            Assert.True(pos.IsClosed);
            var visible = window.GetVisualDescendants().OfType<PosView>().First().GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
            var print = Assert.Single(visible.OfType<Button>(), b => b.Name == "PrintButton");
            Assert.Contains(print.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Drucken");
            Assert.Single(visible.OfType<Button>(), b => b.Name == "SaveButton");
            Assert.DoesNotContain(visible.OfType<Button>(), b => b.Name == "SendButton");
            return pos;
        }, Api().On($"POST /api/pos/registers/{SpaSample.Register.Id}/day-closings", DayReport() with { Id = Guid.NewGuid(), CountedCash = 356.30m, Difference = -1.50m }));
}

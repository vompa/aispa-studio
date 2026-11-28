using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    private static CashRegisterDto PracticeRegister => SpaSample.Register with
    {
        IsPractice = true,
        Readiness = new(FiscalReadinessState.Practice, ["Für den Echtbetrieb eine eigene Kassa einrichten."]),
    };

    [Theory]
    [InlineData(1440, "verwaltung-kassen")]
    [InlineData(1100, "verwaltung-kassen-schmal")]
    public Task Registers_owner_can_navigate_by_keyboard_and_decommission_a_register(int width, string screenshot)
    {
        var practice = PracticeRegister;
        var real = SpaSample.Register with
        {
            Id = Guid.NewGuid(),
            Code = "WIEN-2",
            Country = FiscalCountry.CH,
            Readiness = new(FiscalReadinessState.Ready, []),
        };
        var path = $"POST /api/pos/registers/{real.Id}/decommission";
        var stub = Api().On("GET /api/pos/registers", new[] { practice, real }).On(path, SpaSample.Receipt());
        return Render("hell", screenshot, async (window, main) =>
        {
            window.Width = width;
            var shell = await OpenAdminPage(window, main);
            await shell.Pos.InitializeAsync(SpaSample.LocationId);
            var openRegister = shell.Pos.Register;
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);

            var panel = shell.AdminPage.Registers!;
            Assert.Same(panel, shell.AdminPage.Content);
            var list = Named<ListBox>(window, "RegisterList");
            var create = Named<Button>(window, "CreateRegisterButton");
            var decommission = Named<Button>(window, "DecommissionRegisterButton");
            Assert.True(create.Bounds.Width >= 96 && create.Bounds.Height >= 24);
            Assert.True(decommission.Bounds.Width >= 96 && decommission.Bounds.Height >= 24);
            Assert.True(RightOf(window, decommission) <= RightOf(window, Named<ContentControl>(window, "AreaHost")));
            var row = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(row)));
            Assert.True(row.Focus());
            Press(window, PhysicalKey.ArrowDown);
            await Pump(Settling);
            Assert.Equal(real.Id, panel.SelectedRow!.Register.Id);

            panel.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
            await panel.DecommissionCommand.ExecuteAsync(null);
            await Pump(Settling);
            Assert.Single(stub.Calls, call => call.Route == path);
            Assert.DoesNotContain(shell.Pos.Registers, register => register.Id == real.Id);
            Assert.Same(openRegister, shell.Pos.Register);
            Assert.Equal(real.Id, panel.SelectedRow!.Register.Id);
            Assert.False(decommission.IsEffectivelyEnabled);
            panel.SelectedRow = panel.Rows[0];
            Assert.True(panel.SelectedRow.IsPractice);
            return panel;
        }, stub);
    }

    [Fact]
    public Task Practice_register_band_and_headings_are_visible_on_the_original_and_duplicate()
    {
        var receipt = SpaSample.Receipt() with { IsPractice = true };
        var stub = Api().On("GET /api/pos/registers", new[] { PracticeRegister })
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new[] { receipt });
        return Render("hell", "kassa-uebungsbeleg", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var pos = shell.Pos;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            var band = Named<InfoBar>(window, "PracticeRegisterBand");
            Assert.True(band.IsEffectivelyVisible);
            Assert.Null(band.CloseCommand);
            var bill = pos.Bill;
            var card = pos.Payments.First(payment => payment.Method == PaymentMethod.Card);
            pos.FillRestCommand.Execute(card);
            var amount = card.Amount;
            var updated = PracticeRegister with { Warnings = ["Ein neuer Jahresbeleg fehlt."] };
            await pos.ApplyRegisterChangeAsync(updated);
            await Pump(Settling);
            Assert.Same(updated, pos.Register);
            Assert.Same(bill, pos.Bill);
            Assert.Same(card, pos.Payments.First(payment => payment.Method == PaymentMethod.Card));
            Assert.Equal(amount, card.Amount);
            await pos.PayCommand.ExecuteAsync(null);
            await Pump(Settling);
            Assert.True(pos.Receipt!.IsPractice);
            Assert.True(PracticeHeading().IsEffectivelyVisible);

            pos.CloseReceiptCommand.Execute(null);
            pos.ShowReceiptsCommand.Execute(null);
            await pos.LoadReceiptsAsync();
            pos.SelectedReceiptItem = Assert.Single(pos.Receipts);
            await Pump(Settling);
            Assert.True(pos.SelectedReceipt!.IsPractice);
            Assert.True(PracticeHeading().IsEffectivelyVisible);
            Assert.True(band.IsEffectivelyVisible);
            return pos;

            TextBlock PracticeHeading() => Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Name == "PracticeReceiptHeading" && block.IsEffectivelyVisible);
        }, stub);
    }
}

using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    private const string RegistersRoute = "GET /api/pos/registers";
    private const string RegisterSetupRoute = "GET /api/pos/setup";
    private const string CreateRegisterRoute = "POST /api/pos/registers";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Register_creation_reloads_after_inflight_register_list(bool newLocation)
    {
        var location = newLocation ? SpaSample.Vienna with { Id = Guid.NewGuid(), Name = "Neuer Standort" } : SpaSample.Vienna;
        var register = SpaSample.Register with { LocationId = location.Id };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        const string LocationsRoute = "GET /api/locations";
        var stub = RegisterRefreshApi()
            .OnJson(RegistersRoute, _ => Interlocked.Increment(ref reads) == 1 ? Array.Empty<CashRegisterDto>() : new[] { register })
            .On(CreateRegisterRoute, register with { Country = FiscalCountry.CH })
            .Hold(RegistersRoute, gate.Task);
        return Render("hell", "probe-kassenliste-nach-anlage", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);
            Assert.Single(stub.Calls, call => call.Route == RegistersRoute);
            ClickCenter(window, Named<Button>(window, "CreateRegisterButton"));
            await Pump(Settling);
            var wizard = shell.RegisterWizard!;
            var posting = CreateRegisterAsync(wizard);
            await Pump(Settling);
            Assert.NotNull(wizard.Created);
            Assert.Single(stub.Calls, call => call.Route == CreateRegisterRoute);

            var previousLocationReads = stub.Calls.Count(call => call.Route == LocationsRoute);
            stub.On(LocationsRoute, new[] { location });
            gate.SetResult();
            await posting;
            await Pump(Settling);

            Assert.Equal(2, Volatile.Read(ref reads));
            var row = Assert.Single(shell.AdminPage.Registers!.Rows);
            Assert.Equal(register.Id, row.Register.Id);
            Assert.Equal(location.Name, row.Location);
            Assert.Equal(previousLocationReads + (newLocation ? 1 : 0), stub.Calls.Count(call => call.Route == LocationsRoute));
            return shell;
        }, stub);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Register_setup_card_recovers_on_reopening_after_register_changes(bool decommission)
    {
        var stub = RegisterRefreshApi()
            .On(RegisterSetupRoute, new PosSetupDto(decommission ? FiscalReadinessState.Ready : FiscalReadinessState.NotReady, "Stand vor der Änderung."));
        var route = $"POST /api/pos/registers/{SpaSample.Register.Id}/decommission";
        stub.On(route, SpaSample.Receipt());
        return Render("hell", "probe-kassenkarte-nach-aenderung", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var company = shell.AdminPage.Company!;
            Assert.Equal(decommission ? FiscalReadinessState.Ready : FiscalReadinessState.NotReady, company.RegisterSetup!.State);
            shell.AdminPage.Show("masterData", "registers");
            await Pump(Settling);
            var registers = shell.AdminPage.Registers!;
            if (decommission)
            {
                stub.On(RegisterSetupRoute, new PosSetupDto(FiscalReadinessState.NotReady, "Eine Kassa einrichten."));
                registers.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
                await registers.DecommissionCommand.ExecuteAsync(null);
                await Pump(Settling);
                Assert.Equal(FiscalReadinessState.NotReady, company.RegisterSetup?.State);
            }
            else
            {
                stub.On(RegisterSetupRoute, null, HttpStatusCode.ServiceUnavailable);
                ClickCenter(window, Named<Button>(window, "CreateRegisterButton"));
                await Pump(Settling);
                var wizard = shell.RegisterWizard!;
                await CreateRegisterAsync(wizard);
                Assert.True(wizard.ShowFinish);
                wizard.Close();
                Assert.Single(stub.Calls, call => call.Route == CreateRegisterRoute);
                Assert.Equal(FiscalReadinessState.NotReady, company.RegisterSetup?.State);
                shell.AdminPage.Show("masterData", "company");
                await Pump(Settling);
                var errorBar = Named<InfoBar>(window, "RegisterSetupErrorBar");
                Assert.True(errorBar.IsEffectivelyVisible);
                Assert.True(errorBar.ActionCommand!.CanExecute(null));
                shell.AdminPage.Show("masterData", "registers");
                stub.On(RegisterSetupRoute, new PosSetupDto(FiscalReadinessState.Ready, "Die Kassa ist bereit."));
            }

            shell.AdminPage.Show("masterData", "company");
            await Pump(Settling);
            Assert.Equal(decommission ? FiscalReadinessState.NotReady : FiscalReadinessState.Ready, company.RegisterSetup?.State);
            Assert.Equal(decommission, Named<Button>(window, "RegisterSetupButton").IsEffectivelyVisible);
            return shell;
        }, stub);
    }

    private static StubApi RegisterRefreshApi() => Api()
        .On("GET /api/pos/fiscal/providers?country=CH", new[]
        {
            new FiscalProviderDto("none", "Ohne Signatureinrichtung", FiscalProviderKind.NoSignature, false, [], "kassa"),
        })
        .On(CreateRegisterRoute, SpaSample.Register with { Country = FiscalCountry.CH });

    private static async Task CreateRegisterAsync(RegisterWizardViewModel wizard)
    {
        wizard.Country = RegisterWizardViewModel.Countries[2];
        await wizard.NextCommand.ExecuteAsync(null);
        await wizard.NextCommand.ExecuteAsync(null);
        wizard.Code = "WIEN-1";
        wizard.ReceiptHeader = "Day-Spa Wien";
        await wizard.NextCommand.ExecuteAsync(null);
        await wizard.CreateCommand.ExecuteAsync(null);
    }
}

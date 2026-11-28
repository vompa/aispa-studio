using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class RegisterWizardTests
{
    [Fact]
    public async Task Busy_create_blocks_close_and_confirmation_again_and_keeps_completion_after_read_failure()
    {
        var location = SpaSample.Vienna;
        var access = TestData.Owner().Access! with { AllLocations = false, LocationIds = [location.Id] };
        var register = SpaSample.Register with { Country = FiscalCountry.CH, IsPractice = true };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stub = new StubApi()
            .On("GET /api/locations", new[] { SpaSample.Salzburg, location })
            .On("GET /api/pos/fiscal/providers?country=CH", new[]
            {
                new FiscalProviderDto("none", "Ohne Signatureinrichtung", FiscalProviderKind.NoSignature, false, [], "kassa-einrichten"),
            })
            .On("POST /api/pos/registers", register)
            .Hold("POST /api/pos/registers", gate.Task)
            .On("GET /api/pos/setup", null, HttpStatusCode.ServiceUnavailable);
        var api = stub.Client();
        var wizard = new RegisterWizardViewModel(api, access, SpaSample.Salzburg.Id);
        var closed = false;
        wizard.CloseRequested += () => closed = true;
        wizard.RegisterCreated += async _ => await api.GetAsync<PosSetupDto>("api/pos/setup", TestContext.Current.CancellationToken);
        await wizard.LoadAsync();
        Assert.Equal(location.Id, Assert.Single(wizard.Locations).Id);
        wizard.Country = RegisterWizardViewModel.Countries[2];
        await wizard.NextCommand.ExecuteAsync(null);
        wizard.PracticeMode = true;
        Assert.False(wizard.NextCommand.CanExecute(null));
        wizard.PracticeConfirmed = true;
        await wizard.NextCommand.ExecuteAsync(null);
        wizard.Code = "WIEN-1";
        wizard.ReceiptHeader = "Day-Spa Wien";
        await wizard.NextCommand.ExecuteAsync(null);

        var create = wizard.CreateCommand.ExecuteAsync(null);
        Assert.True(wizard.IsBusy);
        Assert.False(wizard.CreateCommand.CanExecute(null));
        Assert.False(wizard.BackCommand.CanExecute(null));
        wizard.Close();
        Assert.False(closed);
        await wizard.CreateCommand.ExecuteAsync(null);
        gate.SetResult();
        await create;

        Assert.True(wizard.ShowFinish);
        Assert.Equal(register.Id, wizard.Created!.Id);
        await wizard.CreateCommand.ExecuteAsync(null);
        var request = Assert.Single(stub.Calls, call => call.Route == "POST /api/pos/registers");
        var input = JsonSerializer.Deserialize<CashRegisterInput>(request.Body!, ApiClient.Json)!;
        Assert.Equal((location.Id, FiscalCountry.CH, true), (input.LocationId, input.Country, input.Practice!.Value));
        wizard.Close();
        Assert.True(closed);
    }
}

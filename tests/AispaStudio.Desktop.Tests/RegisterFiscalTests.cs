using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class RegisterFiscalTests
{
    [Fact]
    public async Task Initial_provider_credentials_are_required_and_sent_in_the_one_create_request()
    {
        var register = SpaSample.Register with { FiscalProvider = "probe" };
        var provider = new FiscalProviderDto("probe", "Anbieter für den Test", FiscalProviderKind.OnlineService, false,
            [new("partner", "Partnerkennung", false), new("key", "Zugangsschlüssel", true)], "kassa-einrichten");
        var stub = new StubApi().On("GET /api/locations", new[] { SpaSample.Vienna })
            .On("GET /api/pos/fiscal/providers?country=AT", new[] { provider })
            .On("POST /api/pos/registers", register);
        var wizard = new RegisterWizardViewModel(stub.Client(), TestData.Owner().Access!, SpaSample.LocationId);
        await wizard.LoadAsync();
        await wizard.NextCommand.ExecuteAsync(null);
        Assert.False(wizard.NextCommand.CanExecute(null));
        Assert.Equal('\0', wizard.CredentialFields[0].PasswordChar);
        Assert.NotEqual('\0', wizard.CredentialFields[1].PasswordChar);
        wizard.CredentialFields[1].Value = "attrappe-schluessel";
        Assert.False(wizard.NextCommand.CanExecute(null));
        wizard.CredentialFields[0].Value = "attrappe-partner";
        await wizard.NextCommand.ExecuteAsync(null);
        wizard.Code = "WIEN-1";
        wizard.ReceiptHeader = "Day-Spa Wien";
        await wizard.NextCommand.ExecuteAsync(null);
        await wizard.CreateCommand.ExecuteAsync(null);
        var request = Assert.Single(stub.Calls, call => call.Route == "POST /api/pos/registers");
        var input = JsonSerializer.Deserialize<CashRegisterInput>(request.Body!, ApiClient.Json)!;
        Assert.Equal("attrappe-schluessel", input.FiscalCredentials!["key"]);
        Assert.Equal("attrappe-partner", input.FiscalCredentials["partner"]);
        Assert.All(wizard.CredentialFields, field => Assert.Empty(field.Value));
        Assert.DoesNotContain(stub.Calls, call => call.Route.Contains("fiscal-credentials", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Late_registration_response_and_credential_input_are_discarded_after_register_switch()
    {
        var first = SpaSample.Register with { FiscalProvider = "probe" };
        var receiptId = Guid.NewGuid();
        var second = first with
        {
            Id = Guid.NewGuid(),
            Code = "ANDERE",
            YearlyReceipt = new(2026, receiptId, true, true),
            Confirmations = [new(RegisterConfirmationKind.YearlyReceiptCheck, 2026, receiptId, SpaSample.At(11, 40), "u1")],
        };
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRegistration = $"GET /api/pos/registers/{first.Id}/fiscal-registration";
        var provider = new FiscalProviderDto("probe", "Anbieter für den Test", FiscalProviderKind.OnlineService, false,
            [new("key", "Zugangsschlüssel", true)], "kassa-einrichten");
        var stub = new StubApi().On("GET /api/pos/registers", new[] { first, second })
            .On("GET /api/locations", new[] { SpaSample.Vienna })
            .On("GET /api/pos/fiscal/providers?country=AT", new[] { provider })
            .On($"GET /api/pos/registers/{first.Id}/fiscal-credentials", new FiscalCredentialsDto(["key"], null))
            .On($"GET /api/pos/registers/{second.Id}/fiscal-credentials", new FiscalCredentialsDto([], null))
            .On(firstRegistration, new FiscalRegistrationDto(first.Code, "attrappe-a", "attrappe-pruefwert", null, "probe", false, null))
            .Hold(firstRegistration, blocked.Task)
            .On($"GET /api/pos/registers/{second.Id}/fiscal-registration",
                new FiscalRegistrationDto(second.Code, "attrappe-b", "attrappe-pruefwert", null, "probe", false, null));
        var registers = new RegistersViewModel(stub.Client(new OfflineCache(new MemoryOfflineStore())), new ToastState(TimeProvider.System));
        await registers.LoadAsync();
        var old = registers.Fiscal!;
        var oldField = Assert.Single(old.Fields);
        Assert.True(oldField.IsConfigured);
        Assert.Empty(oldField.Value);
        oldField.Value = "attrappe-entwurf";
        var waiting = old.LoadRegistrationCommand.ExecuteAsync(null);
        Assert.Empty(old.Registration);

        registers.SelectedRow = registers.Rows[1];
        var now = registers.Fiscal!;
        await now.LoadRegistrationCommand.ExecuteAsync(null);
        blocked.SetResult();
        await waiting;
        Assert.Empty(oldField.Value);
        Assert.Empty(old.Registration);
        Assert.Contains(now.Registration, item => item.Value == "attrappe-b");
        Assert.DoesNotContain(now.Registration, item => item.Value == "attrappe-a");
        Assert.True(now.MayCopy("attrappe-b"));

        Assert.True(now.Confirmations.Single(item => item.Year == 2026).IsConfirmed);
        var unconfirmed = second with { YearlyReceipt = new(2026, null, false, true), Confirmations = [] };
        stub.On("GET /api/pos/registers", new[] { first, unconfirmed });
        await registers.EnsureLoadedAsync();
        var fresh = registers.Fiscal!.Confirmations.Single(item => item.Year == 2026);
        var retained = registers.SelectedRow;
        Assert.False(fresh.IsConfirmed);
        Assert.Null(fresh.ReceiptId);
        stub.On("GET /api/pos/registers", null, HttpStatusCode.ServiceUnavailable);
        await registers.EnsureLoadedAsync();
        Assert.Equal(second.Id, registers.SelectedRow!.Register.Id);
        Assert.NotNull(registers.LoadError);
        Assert.False(registers.Fiscal!.Confirmations.Single(item => item.Year == 2026).IsConfirmed);
        stub.Down = true;
        await registers.EnsureLoadedAsync();
        Assert.Same(retained, registers.SelectedRow);
        Assert.NotNull(registers.LoadError);
    }
}

using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class RegistersTests
{
    [Fact]
    public async Task Decommission_targets_the_confirmed_register_and_updates_the_selected_row()
    {
        var register = SpaSample.Register with { IsPractice = true };
        var foreign = register with { Id = Guid.NewGuid(), Code = "ANDERE", OtherSite = true };
        var path = $"POST /api/pos/registers/{register.Id}/decommission";
        var stub = new StubApi()
            .On("GET /api/pos/registers", new[] { register, foreign })
            .On("GET /api/locations", new[] { SpaSample.Vienna })
            .On(path, SpaSample.Receipt());
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var model = new RegistersViewModel(stub.Client(), new ToastState(TimeProvider.System)) { Dialogs = dialogs };
        await model.EnsureLoadedAsync();

        model.Open(foreign.Id, null, null);
        Assert.Equal(foreign.Id, model.SelectedRow!.Register.Id);
        Assert.False(model.DecommissionCommand.CanExecute(null));
        model.Open(register.Id, null, null);
        Assert.True(model.DecommissionCommand.CanExecute(null));
        await model.DecommissionCommand.ExecuteAsync(null);
        Assert.DoesNotContain(stub.Calls, call => call.Route == path);

        await model.DecommissionCommand.ExecuteAsync(null);
        Assert.Single(stub.Calls, call => call.Route == path);
        Assert.Equal(register.Id, model.SelectedRow!.Register.Id);
        Assert.False(model.SelectedRow.Register.IsActive);
        Assert.True(model.SelectedRow.IsPractice);
        Assert.False(model.DecommissionCommand.CanExecute(null));
        Assert.True(model.Rows.Single(row => row.Register.Id == foreign.Id).Register.IsActive);
        Assert.All(dialogs.Asked, request => Assert.True(request.IsDestructive));
        dialogs.AssertRules();
    }

    [Fact]
    public async Task Decommission_persists_the_same_register_in_list_and_detail_for_an_offline_restart()
    {
        var register = SpaSample.Register;
        var other = register with { Id = Guid.NewGuid(), Code = "ANDERE" };
        var detail = $"api/pos/registers/{register.Id}";
        var list = "api/pos/registers";
        var filtered = $"{list}?locationId={register.LocationId}";
        var stub = new StubApi()
            .On($"GET /{list}", new[] { register, other })
            .On($"GET /{filtered}", new[] { register, other })
            .On($"GET /{detail}", register)
            .On("GET /api/locations", new[] { SpaSample.Vienna })
            .On($"POST /{detail}/decommission", SpaSample.Receipt());
        var store = new MemoryOfflineStore();
        var api = stub.Client(new OfflineCache(store));
        var model = new RegistersViewModel(api, new ToastState(TimeProvider.System))
        {
            Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed),
        };
        await model.EnsureLoadedAsync();
        await api.GetAsync<CashRegisterDto>(detail, TestContext.Current.CancellationToken);
        await api.GetAsync<List<CashRegisterDto>>(filtered, TestContext.Current.CancellationToken);
        await model.DecommissionCommand.ExecuteAsync(null);
        var saved = JsonSerializer.Deserialize<CashRegisterDto>(store.Load().Responses[detail].Json, ApiClient.Json);
        Assert.False(saved!.IsActive);

        // A GET already in flight at decommission time may still return the old DTO.
        await api.GetAsync<List<CashRegisterDto>>(filtered, TestContext.Current.CancellationToken);
        api.Offline!.Flush();
        stub.Down = true;
        var restarted = stub.Client(new OfflineCache(store));
        foreach (var path in new[] { list, filtered })
        {
            var cached = await restarted.GetAsync<List<CashRegisterDto>>(path, TestContext.Current.CancellationToken);
            Assert.NotNull(cached.CachedAt);
            Assert.False(cached.Value!.Single(row => row.Id == register.Id).IsActive);
            Assert.True(cached.Value!.Single(row => row.Id == other.Id).IsActive);
        }

        var cachedDetail = await restarted.GetAsync<CashRegisterDto>(detail, TestContext.Current.CancellationToken);
        Assert.NotNull(cachedDetail.CachedAt);
        Assert.False(cachedDetail.Value!.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decommission_checks_the_local_pending_and_failed_substitutes_before_posting(bool rejected)
    {
        var register = SpaSample.Register;
        var route = $"POST /api/pos/registers/{register.Id}/decommission";
        var stub = new StubApi()
            .On("GET /api/pos/registers", new[] { register })
            .On("GET /api/locations", new[] { SpaSample.Vienna })
            .On(route, SpaSample.Receipt());
        var offline = new OfflineCache(new MemoryOfflineStore());
        var dialogs = new ConfirmDialogHost();
        var model = new RegistersViewModel(stub.Client(offline), new ToastState(TimeProvider.System)) { Dialogs = dialogs };
        await model.EnsureLoadedAsync();
        AddSubstitute(Guid.NewGuid());

        var pending = model.DecommissionCommand.ExecuteAsync(null);
        Assert.NotNull(dialogs.Current);
        var unresolved = AddSubstitute(register.Id);
        if (rejected)
        {
            offline.Complete(unresolved.Id, new FailedChange(unresolved.Id, unresolved.Description, "Abgelehnt", unresolved.CreatedAt,
                Substitute: unresolved.Substitute, Path: unresolved.Path, Body: unresolved.Body));
        }

        dialogs.Current!.ConfirmCommand.Execute(null);
        await pending;
        Assert.DoesNotContain(stub.Calls, call => call.Route == route);
        Assert.True(model.SelectedRow!.Register.IsActive);
        Assert.NotNull(model.Error);
        Assert.Contains(unresolved.Id, rejected ? offline.Failed.Select(change => change.Id) : offline.Pending.Select(change => change.Id));

        await model.DecommissionCommand.ExecuteAsync(null);
        Assert.Null(dialogs.Current);
        Assert.DoesNotContain(stub.Calls, call => call.Route == route);

        PendingChange AddSubstitute(Guid registerId)
        {
            var receipt = SpaSample.Receipt() with { Id = Guid.NewGuid(), RegisterId = registerId };
            var change = new PendingChange(receipt.Id, $"api/pos/registers/{registerId}/substitute-receipts", "{}", "Ersatzbeleg", receipt.IssuedAt, null, Substitute: receipt);
            offline.Enqueue(change);
            return change;
        }
    }
}

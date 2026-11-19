using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Gast anonymisieren: nur Inhaber und Geschäftsführung, mit Rückfrage; die Karte eines anonymisierten Gastes ist nur noch lesbar.</summary>
public sealed class CustomerAnonymizeTests
{
    private const string Message = "Der Gast lässt sich noch nicht anonymisieren. Offen: 1 künftiger Termin. Bitte zuerst stornieren oder abschließen.";

    private static readonly CustomerDto Guest = SpaSample.Customers[0];

    private static string Route => $"POST /api/customers/{Guest.Id}/anonymize";

    private static StubApi Api(CustomerDto? guest = null) => new StubApi()
        .On("GET /api/customers?search=", new List<CustomerDto> { guest ?? Guest })
        .On($"GET /api/customers/{Guest.Id}/appointments", new List<AppointmentDto>());

    private static async Task<CustomersViewModel> OpenAsync(StubApi stub, CurrentUser user, FakeDialogs? dialogs = null)
    {
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System) { User = user, Dialogs = dialogs };
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        return customers;
    }

    [Fact]
    public async Task Der_Menueeintrag_gilt_nur_fuer_Inhaber_und_Geschaeftsfuehrung()
    {
        var owner = await OpenAsync(Api(), TestData.Owner());
        var general = await OpenAsync(Api(), TestData.Owner(StaffRole.GeneralManager));
        var manager = await OpenAsync(Api(), TestData.Manager());
        var reception = await OpenAsync(Api(), TestData.Reception());

        Assert.True(owner.MayAnonymize);
        Assert.True(general.MayAnonymize);
        Assert.False(manager.MayAnonymize);
        Assert.False(reception.MayAnonymize);
    }

    [Fact]
    public async Task Abbrechen_sendet_nichts()
    {
        var stub = Api().On(Route, null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var customers = await OpenAsync(stub, TestData.Owner(), dialogs);

        await customers.AskAnonymizeCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Gast anonymisieren?", asked.Title);
        Assert.Equal("Endgültig anonymisieren", asked.ConfirmText);
        Assert.True(asked.IsDestructive);
        Assert.Contains($"von {Guest.InHouseName} werden endgültig gelöscht", asked.Message, StringComparison.Ordinal);
        Assert.Contains("„Gelöschter Gast“", asked.Message, StringComparison.Ordinal);
        Assert.Contains("nicht rückgängig", asked.Message, StringComparison.Ordinal);
        dialogs.AssertRules();
        Assert.DoesNotContain(stub.Calls, c => c.Route == Route);
        Assert.Same(customers.Results[0], customers.Selected);
    }

    [Fact]
    public async Task Bestaetigen_sendet_den_POST_schliesst_die_Karte_und_laedt_die_Liste_neu()
    {
        var stub = Api().On(Route, null, HttpStatusCode.NoContent);
        var customers = await OpenAsync(stub, TestData.Owner(), new FakeDialogs().Answer(ConfirmChoice.Confirmed));
        var searchesBefore = stub.Calls.Count(c => c.Route.StartsWith("GET /api/customers?search=", StringComparison.Ordinal));

        await customers.AskAnonymizeCommand.ExecuteAsync(null);

        Assert.Single(stub.Calls, c => c.Route == Route);
        Assert.Null(customers.Selected);
        Assert.Null(customers.Error);
        Assert.Equal(searchesBefore + 1, stub.Calls.Count(c => c.Route.StartsWith("GET /api/customers?search=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Bestaetigen_entfernt_den_Gast_aus_dem_Stand_fuer_die_Arbeit_ohne_Verbindung()
    {
        // Given die Karte und eine Suchliste mit dem Gast liegen im Stand
        var stub = Api().On(Route, null, HttpStatusCode.NoContent).On($"GET /api/customers/{Guest.Id}", Guest);
        var store = new MemoryOfflineStore();
        var api = stub.Client(new OfflineCache(store));
        await api.GetAsync<CustomerDto>($"api/customers/{Guest.Id}", TestContext.Current.CancellationToken);
        var customers = new CustomersViewModel(api, TimeProvider.System) { User = TestData.Owner(), Dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed) };
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        stub.On("GET /api/customers?search=", new List<CustomerDto>());

        // When der Gast anonymisiert wird
        await customers.AskAnonymizeCommand.ExecuteAsync(null);

        // Then steht seine Id in keiner Antwort des Stands mehr
        api.Offline!.Flush();
        Assert.DoesNotContain(store.Load().Responses, r => r.Key.Contains(Guest.Id.ToString(), StringComparison.Ordinal)
            || r.Value.Json.Contains(Guest.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_409_zeigt_die_Meldung_und_laesst_die_Karte_offen()
    {
        var stub = Api().On(Route, new CustomerAnonymizeRejected(Message, ["1 künftiger Termin"]), HttpStatusCode.Conflict);
        var customers = await OpenAsync(stub, TestData.Owner(), new FakeDialogs().Answer(ConfirmChoice.Confirmed));

        await customers.AskAnonymizeCommand.ExecuteAsync(null);

        Assert.Equal(Message, customers.Error);
        Assert.Same(customers.Results[0], customers.Selected);
    }

    [Fact]
    public async Task Ohne_Inhaber_Ebene_fragt_der_Befehl_nicht_und_sendet_nichts()
    {
        var stub = Api().On(Route, null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var customers = await OpenAsync(stub, TestData.Manager(), dialogs);

        await customers.AskAnonymizeCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Asked);
        Assert.DoesNotContain(stub.Calls, c => c.Route == Route);
    }

    [Fact]
    public async Task Die_Karte_eines_anonymisierten_Gastes_zeigt_den_Hinweis_und_sperrt_Bearbeiten_Buchen_und_Kassieren()
    {
        var deleted = Guest with { FirstName = "Gelöschter", LastName = "Gast", Email = null, Phone = null, Note = null, DeletedAt = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero) };
        var customers = await OpenAsync(Api(deleted), TestData.Owner());

        Assert.True(customers.IsAnonymized);
        Assert.Equal("Anonymisiert am 05.11.2026", customers.AnonymizedNotice);
        Assert.False(customers.CanBook);
        Assert.False(customers.CanCheckout);
        Assert.False(customers.MayAnonymize);
        Assert.False(customers.HasMissing);
    }

    [Fact]
    public async Task Die_Karte_eines_normalen_Gastes_hat_keinen_Hinweis_und_bleibt_buchbar()
    {
        var customers = await OpenAsync(Api(), TestData.Owner());

        Assert.False(customers.IsAnonymized);
        Assert.Null(customers.AnonymizedNotice);
        Assert.True(customers.CanBook);
    }
}

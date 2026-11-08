using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenkarten nach R01 Slice 15: auch bei langsamer Antwort des Servers bleibt höchstens eine Karte im Bearbeiten-Zustand,
/// der spätere Klick gewinnt.
/// </summary>
public sealed class CustomerCardsEditTests
{
    /// <summary>Hält die Antwort auf eine Route zurück, bis der Test sie freigibt.</summary>
    private sealed class HeldRoute(StubApi inner, string route) : DelegatingHandler(inner)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if ($"{request.Method} {request.RequestUri!.PathAndQuery}" == route)
            {
                await _release.Task.WaitAsync(cancellationToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    private static readonly CustomerDto Customer = SpaSample.Customers[0];

    private static StubApi Api() => new StubApi()
        .On("GET /api/customers?search=", SpaSample.Customers.ToList())
        .On($"GET /api/customers/{Customer.Id}/appointments", new List<AppointmentDto>())
        .On("GET /api/employees", new List<EmployeeDto> { SpaSample.Marco, SpaSample.Lena })
        .On("GET /api/pos/settings", new PosSettingsDto(10m, false))
        .On("GET /api/pos/approvers", new List<QuickSwitchPerson>())
        .On($"GET /api/customers/{Customer.Id}/booking-preferences/suggestion", new CustomerPreferenceSuggestion(
            5, SpaSample.Lena.Id, SpaSample.Lena.DisplayName, 4, DayOfWeek.Tuesday, 4, new TimeOnly(12, 0), new TimeOnly(17, 0),
            "4 von 5 Besuchen bei Lena Huber · meist Dienstag · meist nachmittags"));

    private static async Task<CustomersViewModel> SelectedCustomer(HttpMessageHandler handler)
    {
        var customers = new CustomersViewModel(new ApiClient(new Uri("http://localhost/"), new MemorySessionStore(), handler), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        return customers;
    }

    private static bool[] States(CustomersViewModel c) =>
        [c.IsAddingFlag, c.IsEditingWishes, c.IsEditingReceptionNote, c.IsEditingBooking, c.IsEditingBilling, c.IsEditingDiscount, c.IsEditingContact];

    [Fact]
    public async Task Rabatt_mit_langsamer_Antwort_dann_Wuensche_bearbeiten_laesst_nur_Wuensche_offen()
    {
        var held = new HeldRoute(Api(), "GET /api/v1/pos/settings");
        var customers = await SelectedCustomer(held);

        // Given „Rabatt ändern“ wartet noch auf den Server
        var discount = customers.EditDiscountCommand.ExecuteAsync(null);
        // When währenddessen „Wünsche“ geöffnet wird und danach die Antwort kommt
        customers.EditWishesCommand.Execute(null);
        held.Release();
        await discount;

        // Then ist nur die Karte „Wünsche“ im Bearbeiten-Zustand.
        Assert.Equal([false, true, false, false, false, false, false], States(customers));
        Assert.False(customers.IsEditingDiscount);
    }

    [Fact]
    public async Task Termine_mit_langsamer_Antwort_dann_Stammdaten_bearbeiten_laesst_nur_Stammdaten_offen()
    {
        var held = new HeldRoute(Api(), "GET /api/v1/employees");
        var customers = await SelectedCustomer(held);

        var booking = customers.EditBookingCommand.ExecuteAsync(null);
        customers.EditContactCommand.Execute(null);
        held.Release();
        await booking;

        Assert.Equal([false, false, false, false, false, false, true], States(customers));
    }

    [Fact]
    public async Task Kennzeichen_offen_dann_Vorschlag_uebernehmen_laesst_nur_Termine_offen()
    {
        var customers = await SelectedCustomer(Api());
        await customers.LoadVisitsAsync(customers.Selected);
        customers.AddFlagCommand.Execute(null);

        await customers.TakeSuggestionCommand.ExecuteAsync(null);

        Assert.Equal([false, false, false, true, false, false, false], States(customers));
    }

    [Fact]
    public async Task Neue_Kundin_offen_dann_eine_Kundin_waehlen_schliesst_Neue_Kundin()
    {
        var customers = await SelectedCustomer(Api());
        customers.StartNewCommand.Execute(null);
        Assert.True(customers.IsEditingAny);

        customers.Selected = customers.Results[0];

        Assert.False(customers.ShowNew);
        Assert.False(customers.IsEditingAny);
    }
}

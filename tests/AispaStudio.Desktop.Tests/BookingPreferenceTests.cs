using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Buchungsvorlieben: Kundenkarte, FEST im Kalender, Warnung beim Verschieben zu jemand anderem.</summary>
public sealed class BookingPreferenceTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    [Fact]
    public async Task Vorschlag_aus_den_Besuchen_uebernehmen_und_als_feste_Therapeutin_speichern()
    {
        var customer = SpaSample.Customers[0];
        var lena = SpaSample.Lena;
        var saved = new CustomerBookingPreferences(lena.Id, true, [DayOfWeek.Tuesday], new TimeOnly(12, 0), new TimeOnly(17, 0));
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On("GET /api/employees", new List<EmployeeDto> { SpaSample.Marco, lena })
            .On($"GET /api/customers/{customer.Id}/booking-preferences/suggestion", new CustomerPreferenceSuggestion(
                5, lena.Id, lena.DisplayName, 4, DayOfWeek.Tuesday, 4, new TimeOnly(12, 0), new TimeOnly(17, 0),
                "4 von 5 Besuchen bei Lena Huber · meist Dienstag · meist nachmittags"))
            .On($"PUT /api/customers/{customer.Id}/booking-preferences", customer with { Booking = saved });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        await customers.LoadVisitsAsync(customers.Selected);
        var before = customers.BookingText;

        await customers.TakeSuggestionCommand.ExecuteAsync(null);
        var day = customers.Weekdays.Where(d => d.IsChecked).Select(d => d.Day).ToList();
        customers.BookingFixed = true;
        customers.BookingUntil = "9";
        var cannotSaveBackwards = customers.SaveBookingCommand.CanExecute(null);
        customers.BookingUntil = "17:00";
        await customers.SaveBookingCommand.ExecuteAsync(null);
        var sent = JsonSerializer.Deserialize<CustomerBookingPreferences>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json)!;

        Assert.Empty(before); // ohne Wunsch kein Abschnitt, nur „+ Termine am liebsten“ (Slice 141)
        Assert.True(customers.HasSuggestion);
        Assert.Equal([DayOfWeek.Tuesday], day);
        Assert.False(cannotSaveBackwards);
        Assert.Equal(lena.Id, sent.EmployeeId);
        Assert.True(sent.EmployeeFixed);
        Assert.Equal([DayOfWeek.Tuesday], sent.Weekdays);
        Assert.Equal(new TimeOnly(12, 0), sent.From);
        Assert.Equal(new TimeOnly(17, 0), sent.Until);
        Assert.False(customers.IsEditingBooking);
        Assert.Equal("Feste Therapeut:in: Lena Huber · lieber Di. · 12:00–17:00", customers.BookingText);
    }

    [Fact]
    public async Task Keine_Vorliebe_nimmt_auch_fest_zurueck()
    {
        var customer = SpaSample.Customers[0] with { Booking = new CustomerBookingPreferences(SpaSample.Lena.Id, true) };
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On("GET /api/employees", new List<EmployeeDto> { SpaSample.Lena })
            .On($"PUT /api/customers/{customer.Id}/booking-preferences", customer with { Booking = null });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];

        await customers.EditBookingCommand.ExecuteAsync(null);
        var wasFixed = customers.BookingFixed;
        customers.BookingEmployee = customers.EmployeeChoices[0];
        await customers.SaveBookingCommand.ExecuteAsync(null);
        var sent = JsonSerializer.Deserialize<CustomerBookingPreferences>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json)!;

        Assert.True(wasFixed);
        Assert.Null(sent.EmployeeId);
        Assert.False(sent.EmployeeFixed);
        Assert.Null(customers.Selected!.Booking);
    }

    [Fact]
    public void Feste_Therapeutin_steht_als_FEST_am_Termin_und_im_Hover()
    {
        var own = TestData.Appointment(10, TestData.Anna);
        var elsewhere = TestData.Appointment(13, TestData.Ben) with { CustomerId = Guid.NewGuid(), CustomerName = "Olga Ort" };
        var day = TestData.Day(own, elsewhere) with
        {
            Guests =
            [
                new CalendarGuest(own.CustomerId, 7, 4, 0, TestData.Monday.AddDays(-30), null,
                    new CustomerBookingPreferences(TestData.Anna.Id, true, [DayOfWeek.Monday]), "Druck: fest · wenig Gespräch"),
                new CalendarGuest(elsewhere.CustomerId, 3, 3, 0, TestData.Monday.AddDays(-30), null,
                    new CustomerBookingPreferences(TestData.Anna.Id, true)),
            ],
        };

        var layout = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna);
        var first = layout.Columns[0].Items.Single(i => i.Id == own.Id);
        var second = layout.Columns[1].Items.Single(i => i.Id == elsewhere.Id);

        var fest = first.Flags.Single(f => f.Symbol == "FEST");
        Assert.Equal(GuestFlagKind.Fixed, fest.Kind);
        Assert.Contains("Feste Therapeut:in: Anna", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("lieber Mo.", first.HoverText, StringComparison.Ordinal);
        Assert.Contains("Vorlieben: Druck: fest · wenig Gespräch", first.HoverText, StringComparison.Ordinal);
        Assert.Equal(GuestFlagKind.FixedElsewhere, second.Flags.Single(f => f.Symbol == "FEST").Kind);
    }

    [Fact]
    public async Task Verschieben_weg_von_der_festen_Therapeutin_warnt_mit_Rueckgaengig()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var day = TestData.Day(appointment) with
        {
            Guests = [new CalendarGuest(appointment.CustomerId, 7, 4, 0, null, null, new CustomerBookingPreferences(TestData.Anna.Id, true))],
        };
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day)
            .On($"POST /api/appointments/{appointment.Id}/move", appointment with { EmployeeId = TestData.Ben.Id, UpdatedAt = TestData.At(9) });
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        var item = vm.Columns[0].Items[0];
        vm.BeginDrag(item, grabOffset: 0);
        vm.DragTo(DayLayout.ColumnLeft(1) + 20, item.Top);
        await vm.DropAsync();

        // Der Toast bestätigt die Verschiebung; die Warnung steht als InfoBar mit „Rückgängig“ daneben.
        Assert.True(vm.CanUndo);
        Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Warning, vm.Problem!.Severity);
        Assert.Contains("ist Anna", vm.Problem.Message, StringComparison.Ordinal);
        Assert.Equal("Rückgängig", vm.Problem.ActionText);

        await vm.UndoMoveCommand.ExecuteAsync(null);
        Assert.Null(vm.Problem);
    }
}

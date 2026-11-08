using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Wunsch „Therapeutin“ oder „Therapeut“: Kundenkarte, Zeichen im Kalender, „Freie Zeit suchen“.</summary>
public sealed class TherapistGenderTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly EmployeeDto Anna = TestData.Anna with { Gender = TherapistGender.Female };
    private static readonly EmployeeDto Ben = TestData.Ben with { Gender = TherapistGender.Male };

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Wunsch_in_der_Kundenkarte_speichern()
    {
        var customer = SpaSample.Customers[0];
        var saved = new CustomerBookingPreferences(Gender: TherapistGender.Female);
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On("GET /api/employees", new List<EmployeeDto> { SpaSample.Lena })
            .On($"PUT /api/customers/{customer.Id}/booking-preferences", customer with { Booking = saved });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];

        await customers.EditBookingCommand.ExecuteAsync(null);
        Assert.Equal("egal", customers.BookingGender.Label);
        customers.BookingGender = GenderChoice.All.Single(g => g.Value == TherapistGender.Female);
        await customers.SaveBookingCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<CustomerBookingPreferences>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json)!;
        Assert.Equal(TherapistGender.Female, sent.Gender);
        Assert.Equal("wünscht eine Therapeutin", customers.BookingText);
    }

    [Fact]
    public void Zeichen_am_Termin_wird_gelb_wenn_die_Person_anders_eingetragen_ist()
    {
        var guest = new CalendarGuest(Guid.NewGuid(), 6, 1, 0, null, null, new CustomerBookingPreferences(Gender: TherapistGender.Female));
        var employees = new Dictionary<Guid, EmployeeDto> { [Anna.Id] = Anna, [Ben.Id] = Ben, [TestData.Anna.Id] = Anna };

        var atAnna = DayLayout.Flags(guest, Anna.Id, employees).Single(f => f.Symbol == "♀");
        var atBen = DayLayout.Flags(guest, Ben.Id, employees).Single(f => f.Symbol == "♀");

        Assert.Equal(("Wünscht eine Therapeutin", false), (atAnna.Tip, atAnna.IsNote));
        Assert.Equal(("Wünscht eine Therapeutin; Ben ist als Therapeut eingetragen", true), (atBen.Tip, atBen.IsNote));
    }

    [Fact]
    public async Task Freie_Zeit_laesst_Personen_mit_anderer_Angabe_weg()
    {
        var resources = TestData.Resources with { Employees = [Anna, Ben] };
        var api = new StubApi().On($"GET /api/locations/{TestData.LocationId}/availability?treatmentId={TestData.MassageTreatment.Id}&date=2027-03-01&minSlots=3&days=14",
            new List<AvailableSlot>
            {
                new(TestData.At(10), TestData.At(10, 50), Ben.Id, TestData.Room.Id, null),
                new(TestData.At(14), TestData.At(14, 50), Anna.Id, TestData.Room.Id, null),
            });
        var vm = new FreeTimeViewModel(api.Client(), resources, TestData.Monday, Vienna, TestData.At(8), _ => Task.CompletedTask, () => { },
            TestData.MassageTreatment);
        await Until(() => vm.HasSlots);
        Assert.True(vm.HasGenders);
        Assert.Equal(["Ben", "Anna"], vm.Slots.Select(s => s.Who));

        vm.Gender = GenderChoice.All.Single(g => g.Value == TherapistGender.Female);
        await Until(() => vm.Slots.Count == 1);

        Assert.Equal(["Anna"], vm.Slots.Select(s => s.Who));
        Assert.False(new FreeTimeViewModel(api.Client(), TestData.Resources, TestData.Monday, Vienna, TestData.At(8), _ => Task.CompletedTask, () => { }).HasGenders);
    }
}

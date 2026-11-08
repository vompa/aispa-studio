using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Wunsch „Therapeutin“ oder „Therapeut“: Angabe im Team, Wunsch in der Kundenkarte, Wirkung im Planer.</summary>
public sealed class TherapistGenderTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<EmployeeDto> SetGenderAsync(TestSpa spa, EmployeeDto e, TherapistGender? gender) =>
        spa.PutAsync<EmployeeDto>($"/api/employees/{e.Id}", new EmployeeInput(e.DisplayName, e.Color, e.QualificationIds, e.WorkingHours, e.Languages, gender), Ct);

    [Fact]
    public async Task Wunsch_Therapeutin_laesst_den_Planer_nur_passende_Personen_vorschlagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Equal(TherapistGender.Female, (await SetGenderAsync(spa, spa.Anna, TherapistGender.Female)).Gender);
        await SetGenderAsync(spa, spa.Ben, TherapistGender.Male);
        var customer = await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/booking-preferences",
            new CustomerBookingPreferences(Gender: TherapistGender.Female), Ct);
        Assert.Equal(TherapistGender.Female, customer.Booking!.Gender);

        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}&customerId={spa.Customer.Id}", Ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(spa.Anna.Id, s.EmployeeId));

        // Ohne Personenwahl bucht der Planer die Therapeutin; ausdrücklich gewählt geht auch der Therapeut.
        Assert.Equal(spa.Anna.Id, (await spa.BookOkAsync(TestSpa.At(10), Ct)).EmployeeId);
        Assert.Equal(spa.Ben.Id, (await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id)).EmployeeId);

        // Ohne Angabe passt Ben wieder.
        await SetGenderAsync(spa, spa.Ben, null);
        var all = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}&customerId={spa.Customer.Id}", Ct);
        Assert.Contains(all, s => s.EmployeeId == spa.Ben.Id);

        var cleared = await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/booking-preferences", new CustomerBookingPreferences(), Ct);
        Assert.Null(cleared.Booking);
    }
}

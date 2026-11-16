using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Wunsch „Therapeutin“ oder „Therapeut“ bei der Online-Buchung: gilt für diesen Termin und legt die Person fest.</summary>
public sealed partial class OnlineBookingTests
{
    /// <summary>Anna ist Therapeutin, Ben Therapeut; beide arbeiten montags 9–17 Uhr.</summary>
    private static async Task<TestSpa> SpaWithGendersAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        WorkingHours[] monday = [new(spa.Location.Id, DayOfWeek.Monday, new(9, 0), new(17, 0))];
        await spa.PutAsync<EmployeeDto>($"/api/employees/{spa.Anna.Id}", new EmployeeInput("Anna", "#2f6f5e", [spa.Massage.Id], monday, Gender: TherapistGender.Female), Ct);
        await spa.PutAsync<EmployeeDto>($"/api/employees/{spa.Ben.Id}", new EmployeeInput("Ben", "#2f6f5e", [spa.Massage.Id], monday, Gender: TherapistGender.Male), Ct);
        return spa;
    }

    private static async Task<IReadOnlyList<DateTimeOffset>> TimesAsync(HttpClient client, TestSpa spa, string wish)
    {
        var availability = await client.GetFromJsonAsync<OnlineAvailability>(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={TestSpa.Monday:yyyy-MM-dd}&days=1{wish}", TestSpa.Json, Ct);
        return availability!.Days.Single(d => d.Date == TestSpa.Monday).Times;
    }

    [Fact]
    public async Task Mit_Wunsch_bucht_die_passende_Person_und_der_Wunsch_steht_in_der_Notiz()
    {
        var spa = await SpaWithGendersAsync(fixture);
        using var client = spa.OnlineClient();

        var booked = await client.PostAsJsonAsync("/api/online/bookings",
            Request(spa, TestSpa.At(10), NewEmail()) with { TherapistWish = TherapistGender.Male }, TestSpa.Json, Ct);
        var confirmation = await TestSpa.ReadAsync<OnlineBookingConfirmation>(booked, Ct);
        var appointment = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);

        Assert.Equal(spa.Ben.Id, appointment.EmployeeId);
        Assert.Contains("Wunsch bei der Online-Buchung: Therapeut.", appointment.Note, StringComparison.Ordinal);
        Assert.True(locations!.Single(l => l.Id == spa.Location.Id).TherapistWish);
    }

    [Fact]
    public async Task Freie_Zeiten_und_Buchung_richten_sich_nach_dem_Wunsch()
    {
        var spa = await SpaWithGendersAsync(fixture);
        // Anna ist um 10 Uhr schon gebucht: Für „Therapeutin“ ist 10 Uhr nicht frei, für „Therapeut“ und ohne Wunsch schon.
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        using var client = spa.OnlineClient();

        var any = await TimesAsync(client, spa, "");
        var female = await TimesAsync(client, spa, "&wish=Female");
        var male = await TimesAsync(client, spa, "&wish=Male");
        var refused = await client.PostAsJsonAsync("/api/online/bookings",
            Request(spa, TestSpa.At(10), NewEmail()) with { TherapistWish = TherapistGender.Female }, TestSpa.Json, Ct);

        Assert.Contains(TestSpa.At(10), any);
        Assert.Contains(TestSpa.At(10), male);
        Assert.DoesNotContain(TestSpa.At(10), female);
        Assert.Contains(TestSpa.At(14), female);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("gewünschte Person", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Angabe_im_Team_bietet_die_Buchung_den_Wunsch_nicht_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);

        Assert.False(locations!.Single(l => l.Id == spa.Location.Id).TherapistWish);
    }
}

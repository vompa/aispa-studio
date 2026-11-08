using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Buchungsvorlieben einer Kundin: Therapeut:in (bevorzugt oder fest), Tage und Zeiten, Vorschlag aus der Historie.</summary>
public sealed class CustomerBookingPreferencesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vorlieben_speichern_pruefen_und_loeschen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/booking-preferences";
        Assert.Null((await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).Booking);

        var saved = await spa.PutAsync<CustomerDto>(path,
            new CustomerBookingPreferences(spa.Anna.Id, true, [DayOfWeek.Thursday, DayOfWeek.Tuesday, DayOfWeek.Thursday], new(14, 0), new(18, 0)), Ct);
        Assert.Equal(spa.Anna.Id, saved.Booking!.EmployeeId);
        Assert.True(saved.Booking.EmployeeFixed);
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Thursday], saved.Booking.Weekdays);
        Assert.Equal(new TimeOnly(14, 0), saved.Booking.From);

        foreach (var wrong in new[]
        {
            new CustomerBookingPreferences(null, true),
            new CustomerBookingPreferences(Guid.NewGuid()),
            new CustomerBookingPreferences(From: new(18, 0), Until: new(14, 0)),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PutAsJsonAsync(path, wrong, TestSpa.Json, Ct)).StatusCode);
        }

        Assert.Null((await spa.PutAsync<CustomerDto>(path, new CustomerBookingPreferences(), Ct)).Booking);
    }

    [Fact]
    public async Task Vorschlag_aus_der_Historie_und_Kalender_zeigt_Vorlieben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suggestionPath = $"/api/customers/{spa.Customer.Id}/booking-preferences/suggestion";
        Assert.Null((await spa.GetAsync<CustomerPreferenceSuggestion>(suggestionPath, Ct)).Text);

        foreach (var (weeksAgo, employee) in new[] { (1, spa.Anna), (2, spa.Anna), (3, spa.Ben), (4, spa.Anna) })
        {
            var past = await spa.BookOkAsync(TestSpa.At(14, date: TestSpa.Monday.AddDays(-7 * weeksAgo)), Ct, employee.Id);
            await spa.PostAsync<AppointmentDto>($"/api/appointments/{past.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        }

        var suggestion = await spa.GetAsync<CustomerPreferenceSuggestion>(suggestionPath, Ct);
        Assert.Equal(4, suggestion.Visits);
        Assert.Equal(spa.Anna.Id, suggestion.EmployeeId);
        Assert.Equal(3, suggestion.WithEmployee);
        Assert.Equal(DayOfWeek.Monday, suggestion.Weekday);
        Assert.Equal(new TimeOnly(12, 0), suggestion.From);
        Assert.Equal("3 von 4 Besuchen bei Anna · meist Montag · meist nachmittags", suggestion.Text);

        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/booking-preferences", new CustomerBookingPreferences(spa.Anna.Id, true), Ct);
        await spa.PutAsync<CustomerPreferences>($"/api/reports/customers/{spa.Customer.Id}/preferences",
            new CustomerPreferences("kräftig", "Lavendel", null, null, true, null, null, null), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var guest = Assert.Single(day.Guests!);
        Assert.Equal(spa.Anna.Id, guest.Booking!.EmployeeId);
        Assert.True(guest.Booking.EmployeeFixed);
        Assert.Equal("Druck: kräftig · Duft: Lavendel · wenig Gespräch", guest.Likes);
    }
}

using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „Trotzdem buchen“ in „Neuer Termin“ (Slice 85, Ä151): Der Hinweis „Der Standort hat zu dieser Zeit geschlossen.“ steht als Warnung mit der Aktion „Trotzdem buchen“;
/// erst die Aktion schickt die Buchung mit <see cref="AppointmentRequest.OutsideHoursConfirmed"/>. Lehnt der Server wegen Dienstende ab, steht dieselbe Warnung mit dem Grund.
/// Testtag: Öffnungszeit 9 bis 20 Uhr, Anna im Dienst 9 bis 17 Uhr.
/// </summary>
public sealed class BookingOutsideHoursTests
{
    private const string Closed = "Der Standort hat zu dieser Zeit geschlossen.";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) })
        .On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));

    private static async Task<BookingViewModel> OpenAsync(StubApi api)
    {
        var calendar = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        return booking;
    }

    private static List<string?> Posts(StubApi api) => [.. api.Calls.Where(c => c.Route == "POST /api/appointments").Select(c => c.Body)];

    private static bool Confirmed(string? body) =>
        JsonDocument.Parse(body!).RootElement.EnumerateObject()
            .Any(p => p.Name.Equals(nameof(AppointmentRequest.OutsideHoursConfirmed), StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.True);

    [Fact]
    public async Task Given_19_30_ausserhalb_der_Oeffnungszeit_When_die_Zeit_getippt_wird_Then_steht_die_Warnung_mit_Trotzdem_buchen_und_kein_Fehler()
    {
        var api = Api();
        var booking = await OpenAsync(api);

        booking.StartText = "19:30";
        await booking.BookCommand.ExecuteAsync(null);

        Assert.Equal(Closed, booking.OutsideHours);
        Assert.Null(booking.Error);
        Assert.True(booking.BookAnywayCommand.CanExecute(null));
        Assert.Empty(Posts(api));
    }

    [Fact]
    public async Task Given_die_Warnung_When_Trotzdem_buchen_gewaehlt_wird_Then_geht_die_Buchung_mit_Bestaetigung_an_den_Server()
    {
        var api = Api();
        var booking = await OpenAsync(api);
        booking.StartText = "19:30";

        await booking.BookAnywayCommand.ExecuteAsync(null);

        var body = Assert.Single(Posts(api));
        Assert.True(Confirmed(body));
        Assert.Null(booking.Error);
    }

    [Fact]
    public async Task Ohne_Warnung_geht_die_Buchung_ohne_Bestaetigung_an_den_Server()
    {
        var api = Api();
        var booking = await OpenAsync(api);
        booking.StartText = "10:00";

        await booking.BookCommand.ExecuteAsync(null);

        Assert.Null(booking.OutsideHours);
        Assert.False(Confirmed(Assert.Single(Posts(api))));
    }

    [Fact]
    public async Task Given_der_Server_lehnt_wegen_Dienstende_ab_When_die_Antwort_kommt_Then_steht_die_Warnung_mit_dem_Grund()
    {
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei, was die Behandlung braucht.",
            [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit nicht im Dienst")], OutsideHours: true);
        var api = Api().On("POST /api/appointments", rejected, HttpStatusCode.Conflict);
        var booking = await OpenAsync(api);
        booking.StartText = "17:30";

        await booking.BookCommand.ExecuteAsync(null);

        Assert.Equal("Anna ist zu dieser Zeit nicht im Dienst.", booking.OutsideHours);
        Assert.Null(booking.Error);
        Assert.Null(booking.Conflict);

        api.On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));
        await booking.BookAnywayCommand.ExecuteAsync(null);
        Assert.True(Confirmed(Posts(api)[^1]));
    }

    [Fact]
    public async Task Eine_andere_Uhrzeit_nimmt_die_Warnung_des_Servers_weg()
    {
        var rejected = new BookingRejected("Zu dieser Zeit ist nicht alles frei, was die Behandlung braucht.",
            [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit nicht im Dienst")], OutsideHours: true);
        var booking = await OpenAsync(Api().On("POST /api/appointments", rejected, HttpStatusCode.Conflict));
        booking.StartText = "17:30";
        await booking.BookCommand.ExecuteAsync(null);
        Assert.NotNull(booking.OutsideHours);

        booking.StartText = "11:00";

        Assert.Null(booking.OutsideHours);
    }

    [Fact]
    public async Task Eine_offene_Zeit_nimmt_die_Warnung_wieder_weg()
    {
        var booking = await OpenAsync(Api());

        booking.StartText = "19:30";
        Assert.Equal(Closed, booking.OutsideHours);
        booking.StartText = "10:00";

        Assert.Null(booking.OutsideHours);
    }
}

using System.Diagnostics;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Vorabprüfung in „Neuer Termin“ (Slice 78, Ä84): Liegt die Zeit außerhalb der Öffnungszeit oder in einer Schließzeit, steht die Meldung des Servers sofort als Warnung mit „Trotzdem buchen“ über dem Knopf (seit Slice 85),
/// ohne Aufruf. Die App rechnet mit derselben Regel wie der Server (<see cref="OpeningRules"/>) und mit dem geladenen Tag; der Schließtag sperrt weiter nichts.
/// </summary>
public sealed class BookingPreCheckTests
{
    private const string ClosedForOutage = "Der Standort ist in dieser Zeit geschlossen (Technische Störung).";

    private static readonly ClosureBand Afternoon = new(Guid.NewGuid(), TestData.At(13), TestData.At(23, 59), ClosureKind.Outage, "Technische Störung", "", null, 0);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day)
        .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) })
        .On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));

    private static async Task<BookingViewModel> StartBookingAsync(CalendarViewModel calendar)
    {
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        return booking;
    }

    private static async Task<(CalendarViewModel Calendar, BookingViewModel Booking)> OpenAsync(StubApi api)
    {
        var calendar = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        return (calendar, await StartBookingAsync(calendar));
    }

    private static bool Posted(StubApi api) => api.Calls.Any(c => c.Route == "POST /api/appointments");

    [Fact]
    public async Task Given_ein_Schliesstag_When_der_Termin_geprueft_wird_Then_liegt_die_Antwort_in_unter_300_Millisekunden_vor()
    {
        var api = Api(TestData.Day() with { Closures = [Afternoon] });
        var (_, booking) = await OpenAsync(api);

        var stopwatch = Stopwatch.StartNew();
        booking.StartText = "14:00";
        var shownWhileTyping = booking.OutsideHours;
        await booking.BookCommand.ExecuteAsync(null);
        stopwatch.Stop();

        Assert.Equal(ClosedForOutage, shownWhileTyping);
        Assert.Equal(ClosedForOutage, booking.OutsideHours);
        Assert.False(Posted(api));
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 299);
    }

    [Fact]
    public async Task Given_geaenderte_Schliesszeiten_When_erneut_geprueft_wird_Then_gilt_der_neue_Stand()
    {
        var api = Api(TestData.Day() with { Closures = [Afternoon] });
        var (calendar, first) = await OpenAsync(api);
        first.StartText = "14:00";
        Assert.Equal(ClosedForOutage, first.OutsideHours);

        // Die Schließzeit beginnt jetzt erst um 18 Uhr; der Kalender lädt den Tag neu.
        api.On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day() with { Closures = [Afternoon with { From = TestData.At(18) }] });
        await calendar.LoadAsync();
        var again = await StartBookingAsync(calendar);
        again.StartText = "14:00";

        Assert.Null(again.OutsideHours);
        await again.BookCommand.ExecuteAsync(null);
        Assert.True(Posted(api));
    }

    [Fact]
    public async Task Ein_anderer_Tag_in_der_Maske_prueft_mit_dem_neu_geladenen_Tag()
    {
        var api = Api(TestData.Day() with { Closures = [Afternoon] })
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day() with { Date = new DateOnly(2027, 3, 2), Opening = [new TimeRange(TestData.At(9).AddDays(1), TestData.At(20).AddDays(1))] });
        var (calendar, booking) = await OpenAsync(api);
        booking.StartText = "14:00";
        Assert.Equal(ClosedForOutage, booking.OutsideHours);

        booking.DateText = "02.03.2027";
        Assert.Null(booking.OutsideHours);
        await calendar.LoadAsync();

        Assert.Null(booking.OutsideHours);
        booking.StartText = "19:30";
        Assert.Equal("Der Standort hat zu dieser Zeit geschlossen.", booking.OutsideHours);
    }

    [Fact]
    public async Task Ausserhalb_der_Oeffnungszeit_steht_die_Meldung_sofort_und_verschwindet_bei_einer_offenen_Zeit()
    {
        var api = Api(TestData.Day());
        var (_, booking) = await OpenAsync(api);

        booking.StartText = "19:30";
        Assert.Equal("Der Standort hat zu dieser Zeit geschlossen.", booking.OutsideHours);

        booking.StartText = "10:00";
        Assert.Null(booking.OutsideHours);
    }

    [Fact]
    public async Task Am_Schliesstag_ohne_Oeffnungszeit_bleibt_Buchen_erlaubt_und_geht_an_den_Server()
    {
        var api = Api(TestData.Day() with { Opening = [] });
        var (_, booking) = await OpenAsync(api);

        booking.StartText = "10:00";
        await booking.BookCommand.ExecuteAsync(null);

        Assert.Equal("Der Standort hat an diesem Tag geschlossen.", booking.DayNotice);
        Assert.True(Posted(api));
    }
}

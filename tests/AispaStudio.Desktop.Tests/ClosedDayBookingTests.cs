using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Neuer Termin an einem Schließtag oder Tag ohne Dienst (ADR 0073, Nachtrag Runde R03): die Maske nennt den Hinweis und sperrt nichts.</summary>
public sealed class ClosedDayBookingTests
{
    private const string Notice = "Der Standort hat an diesem Tag geschlossen.";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day)
        .On("GET /api/customers?search=eva", new List<CustomerDto> { new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null) });

    private static async Task<BookingViewModel> OpenBookingAsync(CalendarDay day)
    {
        var vm = new CalendarViewModel(Api(day).Client(), TestData.Owner(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.StartBooking(vm.Columns[0], 30 * DayLayout.PixelsPerMinute);
        return vm.Booking!;
    }

    [Fact]
    public async Task Given_die_Maske_Neuer_Termin_fuer_einen_Schliesstag_When_sie_geoeffnet_wird_Then_steht_der_Hinweis_in_der_Maske_und_Termin_buchen_bleibt_aktiv()
    {
        var closedByHours = TestData.Day() with { Opening = [] };

        var booking = await OpenBookingAsync(closedByHours);
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];

        Assert.Equal(Notice, booking.DayNotice);
        Assert.True(booking.HasDayNotice);
        Assert.True(booking.BookCommand.CanExecute(null));
    }

    [Fact]
    public async Task Eine_ganztaegige_Schliesszeit_zeigt_den_Hinweis_ein_halber_Tag_nicht()
    {
        var wholeDay = new ClosureBand(Guid.NewGuid(), TestData.At(8), TestData.At(21), ClosureKind.Vacation, "Betriebsurlaub", "", null, 0);
        var halfDay = wholeDay with { From = TestData.At(13) };

        Assert.Equal(Notice, (await OpenBookingAsync(TestData.Day() with { Closures = [wholeDay] })).DayNotice);
        Assert.Null((await OpenBookingAsync(TestData.Day() with { Closures = [halfDay] })).DayNotice);
    }

    [Fact]
    public async Task Ein_Tag_ohne_Dienst_zeigt_den_Hinweis_ein_offener_Tag_mit_Dienst_nicht()
    {
        Assert.Equal(Notice, BookingViewModel.NoticeFor(TestData.Day() with { Shifts = [] }));
        Assert.Null((await OpenBookingAsync(TestData.Day())).DayNotice);
    }
}

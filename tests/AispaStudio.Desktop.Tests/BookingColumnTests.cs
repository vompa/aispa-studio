using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „Neuer Termin“ aus einer Spalte (Auftraggeber, 27.10.2026): Die Person bzw. der Raum des angeklickten Platzes steht fest und lässt sich nicht abwählen,
/// die Auswahl bietet nur Behandlungen an, die dort möglich sind, und eine Ablehnung führt zu „Freie Zeit suchen“ statt in eine Woche, in der niemand kann.
/// Im Testtag kann Anna nur die Massage; Ben kann hier nur „Gesicht“.
/// </summary>
public sealed class BookingColumnTests
{
    private static readonly CustomerDto Eva = new(Guid.NewGuid(), "Eva", "Muster", null, null, null, null);

    private static readonly EmployeeDto FacialBen = TestData.Ben with { QualificationIds = TestData.Facial.RequiredQualificationIds };

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api(string lastTreatment = "Massage") => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources with { Employees = [TestData.Anna, FacialBen] })
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On("GET /api/customers?search=eva", new List<CustomerDto> { Eva })
        .On($"GET /api/customers/{Eva.Id}/summary", new CustomerSummary(1, null, 0, null,
            [new CustomerRecentVisit(Guid.NewGuid(), TestData.At(9).AddDays(-14), lastTreatment, "Ben")], null, null, null, false, null));

    private static async Task<(CalendarViewModel Calendar, BookingViewModel Booking)> OpenAtAnnaAsync(StubApi api)
    {
        var calendar = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        calendar.StartBooking(calendar.Columns[0], 30 * DayLayout.PixelsPerMinute);
        var booking = calendar.Booking!;
        await booking.SearchAsync("eva");
        booking.Customer = booking.Customers[0];
        await booking.LoadSuggestionsAsync();
        return (calendar, booking);
    }

    [Fact]
    public async Task Given_ein_Platz_bei_Anna_When_Neuer_Termin_aufgeht_Then_stehen_nur_Behandlungen_zur_Wahl_die_Anna_kann()
    {
        var (_, booking) = await OpenAtAnnaAsync(Api());

        Assert.Equal(["Massage"], booking.Options.Select(o => o.Treatment.Name));
        Assert.Equal("Nur Behandlungen, die Anna kann.", booking.ColumnScopeText);
    }

    [Fact]
    public async Task Given_Neuer_Termin_bei_Anna_When_gebucht_wird_Then_geht_die_Anfrage_immer_mit_Anna()
    {
        var api = Api().On("POST /api/appointments", TestData.Appointment(10, TestData.Anna));
        var (_, booking) = await OpenAtAnnaAsync(api);

        await booking.BookCommand.ExecuteAsync(null);

        var request = JsonSerializer.Deserialize<AppointmentRequest>(api.Calls.Single(c => c.Route == "POST /api/appointments").Body!, ApiClient.Json)!;
        Assert.Equal(TestData.Anna.Id, request.EmployeeId);
        Assert.Equal("bei Anna", booking.ColumnText);
    }

    [Fact]
    public async Task Given_eine_gewaehlte_Massage_When_ein_Platz_bei_Ben_angeklickt_wird_Then_bietet_die_Maske_Bens_Behandlungen_und_laesst_die_Massage_fallen()
    {
        var (calendar, booking) = await OpenAtAnnaAsync(Api());
        booking.Treatment = TestData.MassageTreatment;

        calendar.StartBookingAt(DayLayout.ColumnLeft(1) + 20, 240 * DayLayout.PixelsPerMinute);

        Assert.Same(booking, calendar.Booking);
        Assert.Equal(["Gesicht"], booking.Options.Select(o => o.Treatment.Name));
        Assert.Null(booking.Treatment);
        Assert.Equal("Nur Behandlungen, die Ben kann.", booking.ColumnScopeText);
    }

    [Fact]
    public async Task Given_zuletzt_gebucht_Gesicht_When_der_Gast_bei_Anna_gewaehlt_ist_Then_nennt_der_Vorschlag_den_Grund_und_bietet_kein_Uebernehmen()
    {
        var (_, booking) = await OpenAtAnnaAsync(Api(lastTreatment: "Gesicht"));

        Assert.Equal(TestData.Facial.Id, booking.SuggestedTreatment?.Id);
        Assert.False(booking.SuggestionIsDoable);
        Assert.Equal("Zuletzt gebucht: Gesicht. Bei Anna nicht möglich.", booking.SuggestedTreatmentText);

        booking.AcceptTreatmentCommand.Execute(null);

        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
    }

    [Fact]
    public async Task Given_der_Vorschlag_Gesicht_bei_Anna_When_Freie_Zeit_suchen_gedrueckt_wird_Then_sucht_der_Kalender_Gesicht_fuer_Eva()
    {
        var (calendar, booking) = await OpenAtAnnaAsync(Api(lastTreatment: "Gesicht"));

        await booking.SearchFreeTimeCommand.ExecuteAsync(booking.SuggestedTreatment);

        Assert.Null(calendar.Booking);
        Assert.Equal(TestData.Facial.Id, calendar.FreeTime?.Treatment?.Id);
        Assert.Equal(Eva.Id, calendar.FreeTime?.Guest.Customer?.Id);
    }

    [Fact]
    public async Task Given_eine_abgelehnte_Buchung_mit_belegtem_Raum_When_die_Meldung_erscheint_Then_ist_ihre_Aktion_Freie_Zeit_suchen_mit_Behandlung_und_Gast()
    {
        var occupying = TestData.Appointment(11, TestData.Anna);
        var rejected = new BookingRejected("Zeitraum ist belegt.",
            [new BookingConflict(ResourceKind.Room, TestData.Room.Id, TestData.Room.Name, "ist zu dieser Zeit schon belegt", occupying.Start, occupying.End, occupying.Id)]);
        var api = Api().On("POST /api/appointments", rejected, HttpStatusCode.Conflict);
        var (calendar, booking) = await OpenAtAnnaAsync(api);

        await booking.BookCommand.ExecuteAsync(null);
        var notice = booking.Conflict!;
        Assert.Equal("Freie Zeit suchen", notice.ActionText);
        notice.ActionCommand!.Execute(null);

        Assert.Null(calendar.Booking);
        Assert.Equal(TestData.MassageTreatment.Id, calendar.FreeTime?.Treatment?.Id);
        Assert.Equal(Eva.Id, calendar.FreeTime?.Guest.Customer?.Id);
    }
}

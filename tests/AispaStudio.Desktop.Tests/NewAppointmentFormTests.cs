using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Maske „Neuer Termin“ (Runde R03, Slice 70): Kunde zuerst, Treffer übernehmen, Tag wählen, beim Slotwechsel nichts verwerfen.</summary>
public sealed class NewAppointmentFormTests
{
    private static readonly CustomerDto Demir = new(Guid.NewGuid(), "Tomasz", "Demir", null, "+43 664 7", null, null);

    /// <summary>Ben kann hier die Massage, damit sie beim Slotwechsel bleibt; was die neue Spalte nicht kann, fällt weg (<see cref="BookingColumnTests"/>).</summary>
    private static readonly EmployeeDto MassageBen = TestData.Ben with { QualificationIds = [TestData.Massage] };

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources with { Employees = [TestData.Anna, MassageBen] })
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day())
        .On("GET /api/customers?search=demi", new List<CustomerDto> { Demir })
        .On($"GET /api/customers/{Demir.Id}/summary", new CustomerSummary(3, null, 0, null,
            [new CustomerRecentVisit(Guid.NewGuid(), TestData.At(9).AddDays(-14), "Massage", "Anna"),
             new CustomerRecentVisit(Guid.NewGuid(), TestData.At(9).AddDays(-60), "Gesicht", "Anna")],
            null, null, null, false, null))
        .On($"GET /api/reports/customers/{Demir.Id}/preferences", new CustomerPreferences("fest", null, null, null, null, "Nacken", null, null));

    private static async Task<(CalendarViewModel Calendar, BookingViewModel Booking)> OpenAsync()
    {
        var vm = new CalendarViewModel(Api().Client(), TestData.Owner(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.StartBooking(vm.Columns[0], 30 * DayLayout.PixelsPerMinute);
        return (vm, vm.Booking!);
    }

    private static async Task ChooseDemirAsync(BookingViewModel booking)
    {
        await booking.SearchAsync("demi");
        booking.Customer = booking.Customers[0];
        await booking.LoadSuggestionsAsync();
    }

    [Fact]
    public async Task Given_die_Suche_demi_When_der_Treffer_Tomasz_Demir_angeklickt_wird_Then_steht_Tomasz_Demir_im_Suchfeld()
    {
        var (_, booking) = await OpenAsync();
        booking.CustomerQuery = "demi";
        await booking.SearchAsync("demi");

        booking.Customer = booking.Customers[0];

        Assert.Equal("Tomasz Demir", booking.CustomerQuery);
        Assert.True(booking.HasChosenCustomer);
        Assert.False(booking.IsPicking);
    }

    [Fact]
    public async Task Given_eine_ausgefuellte_Maske_When_ein_anderer_Slot_im_Kalender_angeklickt_wird_Then_bleiben_alle_Eingaben_und_die_Vorschau_steht_am_neuen_Slot()
    {
        var (vm, booking) = await OpenAsync();
        await ChooseDemirAsync(booking);
        booking.Treatment = TestData.MassageTreatment;
        booking.Note = "Nacken";
        var before = vm.Ghost;

        vm.StartBookingAt(DayLayout.ColumnLeft(1) + 20, 150 * DayLayout.PixelsPerMinute);

        Assert.Same(booking, vm.Booking);
        Assert.Equal(Demir, booking.Customer);
        Assert.Equal("Tomasz Demir", booking.CustomerQuery);
        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
        Assert.Equal("Nacken", booking.Note);
        Assert.Equal(TestData.Ben.Id, booking.ColumnId);
        Assert.NotNull(before);
        Assert.NotNull(vm.Ghost);
        Assert.Equal(DayLayout.ColumnLeft(1), vm.Ghost!.Left);
        Assert.NotEqual(before!.Top, vm.Ghost.Top);
    }

    [Fact]
    public async Task Given_ein_gewaehlter_Kunde_mit_gebuchter_Behandlung_When_die_Maske_den_Kunden_uebernimmt_Then_bietet_sie_diese_Behandlung_als_Vorschlag_an()
    {
        var (_, booking) = await OpenAsync();
        booking.Treatment = TestData.Facial;

        await ChooseDemirAsync(booking);

        Assert.Equal(TestData.MassageTreatment.Id, booking.SuggestedTreatment?.Id);
        Assert.True(booking.HasTreatmentSuggestion);
        Assert.Equal(TestData.Facial.Id, booking.Treatment?.Id);

        booking.AcceptTreatmentCommand.Execute(null);

        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
        Assert.False(booking.HasTreatmentSuggestion);
        Assert.Equal("Meist bei Anna", booking.HabitualPersonText);
    }

    [Fact]
    public async Task Der_Wunsch_aus_der_Kundenkarte_wird_auf_Knopfdruck_in_die_Notiz_uebernommen_und_nur_einmal()
    {
        var (_, booking) = await OpenAsync();
        booking.Note = "Eilig";
        await ChooseDemirAsync(booking);

        Assert.True(booking.HasWishSuggestion);
        var wish = booking.SuggestedWish!;
        booking.AcceptWishCommand.Execute(null);
        booking.AcceptWishCommand.Execute(null);

        Assert.Equal("Eilig" + Environment.NewLine + wish, booking.Note);
        Assert.Contains("Druck: fest", wish);
        Assert.Contains("Fokus: Nacken", wish);
        Assert.False(booking.HasWishSuggestion);
    }

    [Fact]
    public async Task Ein_anderes_Datum_in_der_Maske_springt_im_Kalender_auf_den_Tag_und_die_Vorschau_bleibt()
    {
        var (vm, booking) = await OpenAsync();

        booking.DateText = "02.03.2027";

        Assert.Equal(new DateOnly(2027, 3, 2), booking.Date);
        Assert.Equal(new DateOnly(2027, 3, 2), vm.Date);
        await vm.LoadAsync();
        Assert.Same(booking, vm.Booking);
        Assert.NotNull(vm.Ghost);
    }

    [Fact]
    public async Task Ein_unlesbares_Datum_sperrt_das_Buchen_und_laesst_das_alte_Datum_stehen()
    {
        var (_, booking) = await OpenAsync();
        await ChooseDemirAsync(booking);

        booking.DateText = "abc";

        Assert.Equal(new DateOnly(2027, 3, 1), booking.Date);
        Assert.False(booking.BookCommand.CanExecute(null));
    }

    [Fact]
    public async Task Ein_Datum_vor_heute_sperrt_Termin_buchen_heute_und_spaeter_nicht()
    {
        var (_, booking) = await OpenAsync();
        await ChooseDemirAsync(booking);
        booking.Treatment = TestData.MassageTreatment;

        Assert.Equal(new DateOnly(2027, 3, 1), booking.Today);
        Assert.True(booking.BookCommand.CanExecute(null));

        booking.DateText = "28.02.2027";
        Assert.False(booking.BookCommand.CanExecute(null));

        booking.DateText = "01.03.2027";
        Assert.True(booking.BookCommand.CanExecute(null));

        booking.DateText = "02.03.2027";
        Assert.True(booking.BookCommand.CanExecute(null));

        booking.DateText = "27.03.2027";
        Assert.True(booking.BookCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Anna Bauer", "Anna", "Bauer")]
    [InlineData("Bauer", "", "Bauer")]
    [InlineData("Anna Maria Bauer", "Anna Maria", "Bauer")]
    [InlineData("  Anna   Bauer ", "Anna", "Bauer")]
    [InlineData("0664 123", "", "")]
    [InlineData("Anna 2", "", "")]
    [InlineData("anna@example.at", "", "")]
    [InlineData("", "", "")]
    public async Task Das_Plus_im_Suchfeld_oeffnet_die_Neuanlage_mit_dem_getippten_Namen(string typed, string firstName, string lastName)
    {
        var (_, booking) = await OpenAsync();
        booking.CustomerQuery = typed;

        booking.StartNewCustomerCommand.Execute(null);

        Assert.True(booking.ShowNewCustomer);
        Assert.Equal((firstName, lastName), (booking.NewFirstName, booking.NewLastName));
    }

    [Fact]
    public async Task Das_Plus_ueberschreibt_schon_eingetragene_Namen_der_Neuanlage_nicht()
    {
        var (_, booking) = await OpenAsync();
        booking.NewLastName = "Huber";
        booking.CustomerQuery = "Anna Bauer";

        booking.StartNewCustomerCommand.Execute(null);

        Assert.True(booking.ShowNewCustomer);
        Assert.Equal(("", "Huber"), (booking.NewFirstName, booking.NewLastName));
    }

    [Fact]
    public async Task Abbrechen_schliesst_die_Neuanlage_und_raeumt_die_Treffer_der_Dublettensuche_weg()
    {
        var (_, booking) = await OpenAsync();
        booking.CustomerQuery = "Anna Bauer";
        booking.StartNewCustomerCommand.Execute(null);

        booking.CancelNewCustomerCommand.Execute(null);

        Assert.False(booking.ShowNewCustomer);
        Assert.False(booking.Duplicates.HasOptions);
    }
}

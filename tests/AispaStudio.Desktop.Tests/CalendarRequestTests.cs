using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>KI-Anfrage (Strg+K): Wunsch eintippen, Bausteine entfernen, Vorschläge im Kalender, mit Enter oder Ziffer buchen.</summary>
public sealed class CalendarRequestTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly string RequestRoute = $"POST /api/locations/{TestData.LocationId}/calendar-request";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(8);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly RoomDto Lotus = new(Guid.NewGuid(), TestData.LocationId, "Raum Lotus", 10);

    private static CalendarProposal Proposal(int hour, EmployeeDto who, DateOnly? day = null) =>
        new(Start(hour, day), Start(hour, day).AddMinutes(50), TestData.MassageTreatment.Id, "Massage", who.Id, who.DisplayName,
            TestData.Room.Id, TestData.Room.Name, null, 79m, 0, "zur Wunschzeit");

    private static DateTimeOffset Start(int hour, DateOnly? day) => TestData.At(hour).AddDays((day ?? TestData.Monday).DayNumber - TestData.Monday.DayNumber);

    private static CalendarRequestResult Result(params CalendarProposal[] proposals) => new(
        new CalendarWish(TestData.Monday, new TimeOnly(10, 0), null, TestData.MassageTreatment.Id, null, [TestData.Ben.Id]),
        [
            new(CalendarWishPart.Day, "Mo. 1.3."),
            new(CalendarWishPart.Time, "ab ca. 10:00"),
            new(CalendarWishPart.Treatment, "Massage", TestData.MassageTreatment.Id),
            new(CalendarWishPart.Excluded, "nicht Ben", TestData.Ben.Id),
        ],
        proposals, "Vorschläge", "Antwort eines KI-Assistenten.");

    private static CalendarDay Tuesday() => TestData.Day() with
    {
        Date = TestData.Monday.AddDays(1),
        Opening = [new TimeRange(TestData.At(9).AddDays(1), TestData.At(20).AddDays(1))],
        Shifts = [new EmployeeShift(TestData.Anna.Id, TestData.At(9).AddDays(1), TestData.At(17).AddDays(1))],
    };

    private static StubApi Api(CalendarRequestResult result) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", Tuesday())
        .On(RequestRoute, result);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Strg_K_sucht_zeigt_Bausteine_und_Vorschlaege_und_Enter_bucht_den_ersten()
    {
        var api = Api(Result(Proposal(10, TestData.Anna), Proposal(11, TestData.Anna), Proposal(12, TestData.Anna)));
        var vm = await OpenAsync(api);

        Assert.True(vm.HandleKey(Key.K, KeyModifiers.Control));
        var request = Assert.IsType<CalendarRequestViewModel>(vm.Request);
        Assert.True(vm.HasPanel);
        request.Text = "Massage um 10, nicht bei Ben";
        await request.SubmitAsync();

        var call = api.Calls.Single(c => c.Route == RequestRoute);
        Assert.Contains("\"text\":\"Massage um 10, nicht bei Ben\"", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"date\":\"2027-03-01\"", call.Body, StringComparison.Ordinal);
        Assert.Equal("", request.Text);
        Assert.Equal(["Mo. 1.3.", "ab ca. 10:00", "Massage", "nicht Ben"], request.Chips.Select(c => c.Text));
        Assert.False(request.Chips[0].CanRemove);
        Assert.True(request.Chips[^1].IsExcluded);
        Assert.Equal("Antwort eines KI-Assistenten.", request.Disclosure);
        Assert.Equal([1, 2, 3], request.Proposals.Select(p => p.Number));
        Assert.Equal("79,00 €", request.Proposals[0].Price);
        Assert.Equal("10:00–10:50", request.Proposals[0].Time);

        // Die Vorschläge leuchten nummeriert im Kalender, der gewählte kräftig.
        Assert.Equal(3, vm.SlotMarks.Count);
        Assert.Equal("1 · 10:00–10:50", vm.SlotMarks.Single(m => m.IsActive).Text);
        request.Move(1);
        Assert.Equal("2 · 11:00–11:50", vm.SlotMarks.Single(m => m.IsActive).Text);
        request.Move(-1);

        // Enter ohne neuen Text bucht den gewählten: „Neuer Termin“ mit Behandlung, Person und Uhrzeit.
        await request.SubmitAsync();
        var booking = Assert.IsType<BookingViewModel>(vm.Booking);
        Assert.Equal(TestData.MassageTreatment.Id, booking.Treatment?.Id);
        Assert.Equal("10:00", booking.StartText);
        Assert.Null(vm.Request);
        Assert.Empty(vm.SlotMarks);
    }

    [Fact]
    public async Task Baustein_entfernen_und_andere_Zeiten_fragen_neu_ohne_Text()
    {
        var api = Api(Result(Proposal(10, TestData.Anna), Proposal(11, TestData.Anna)));
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        var request = vm.Request!;
        request.Text = "Massage um 10, nicht bei Ben";
        await request.SubmitAsync();

        await request.RemoveAsync(request.Chips.Single(c => c.IsExcluded));
        var removed = api.Calls.Where(c => c.Route == RequestRoute).Last().Body!;
        Assert.Contains("\"text\":null", removed, StringComparison.Ordinal);
        Assert.Contains("\"excludedEmployeeIds\":[]", removed, StringComparison.Ordinal);

        await request.OtherTimesCommand.ExecuteAsync(null);
        var other = api.Calls.Where(c => c.Route == RequestRoute).Last().Body!;
        Assert.Contains(TestData.At(10).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), other, StringComparison.Ordinal);
        Assert.Contains(TestData.At(11).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), other, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vorschlag_an_einem_anderen_Tag_wechselt_den_Kalender_und_Ziffer_bucht()
    {
        var tuesday = TestData.Monday.AddDays(1);
        var api = Api(Result(Proposal(10, TestData.Anna, tuesday), Proposal(14, TestData.Anna, tuesday)));
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        var request = vm.Request!;
        request.Text = "Massage";
        await request.SubmitAsync();

        Assert.Equal(tuesday, vm.Date);
        Assert.Equal("Dienstag, 2. März 2027", request.Proposals[0].OtherDay);
        Assert.True(vm.HandleKey(Key.D2, KeyModifiers.None));
        for (var i = 0; i < 200 && vm.Booking is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("14:00", vm.Booking?.StartText);
    }

    [Fact]
    public async Task Fehlt_die_Behandlung_kommen_die_passendsten_als_Karten_und_alle_nach_Gruppe_und_die_Wahl_fragt_neu()
    {
        var open = new CalendarRequestResult(new CalendarWish(TestData.Monday, new TimeOnly(10, 0), null, null, null, []),
            [new(CalendarWishPart.Day, "Mo. 1.3."), new(CalendarWishPart.Time, "ab ca. 10:00")], [], "Welche Behandlung? Die passendsten stehen unten.", "KI",
            TreatmentChoices: [TestData.MassageTreatment.Id, TestData.Facial.Id]);
        var api = Api(open);
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        var request = vm.Request!;
        request.Text = "morgen um 10";
        await request.SubmitAsync();

        Assert.True(request.NeedsTreatment);
        Assert.Equal(["Massage", "Gesicht"], request.TreatmentCards.Select(c => c.Name));
        Assert.Equal("50 Min. · 79,00 €", request.TreatmentCards[0].Facts);
        Assert.Equal("Einzelraum", request.TreatmentCards[0].RoomKind);
        // Alle Behandlungen nach Gruppe, zunächst zugeklappt.
        Assert.Equal(["Gesicht (1)", "Massagen (1)"], request.TreatmentGroups.Select(g => g.Header));
        Assert.All(request.TreatmentGroups, g => Assert.False(g.IsOpen));
        request.TreatmentGroups[1].ToggleCommand.Execute(null);
        Assert.True(request.TreatmentGroups[1].IsOpen);

        // Eine Karte wählt die Behandlung: der Wunsch geht mit der Behandlung zurück an den Server.
        await request.ChooseTreatmentCommand.ExecuteAsync(request.TreatmentCards[0]);

        var sent = api.Calls.Where(c => c.Route == RequestRoute).Last().Body!;
        Assert.Contains($"\"treatmentId\":\"{TestData.MassageTreatment.Id}\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"text\":null", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rueckfrage_Eine_oder_zwei_Personen_kommt_als_Knoepfe_und_Raum_und_Gaeste_sind_entfernbare_Chips()
    {
        var wish = new CalendarWish(TestData.Monday, new TimeOnly(10, 0), null, TestData.MassageTreatment.Id, null, [], Lotus.Id);
        var asked = new CalendarRequestResult(wish,
            [new(CalendarWishPart.Day, "Mo. 1.3."), new(CalendarWishPart.Treatment, "Massage", TestData.MassageTreatment.Id), new(CalendarWishPart.Room, "Raum Lotus", Lotus.Id)],
            [Proposal(10, TestData.Anna)], "1 Vorschlag", "KI",
            Questions: [new("Für eine oder zwei Personen?", [new("Eine Person", wish with { Guests = 1 }), new("Zwei Personen (Paarraum)", wish with { Guests = 2 })])]);
        var api = Api(asked);
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        var request = vm.Request!;
        request.Text = "Massage im Raum Lotus";
        await request.SubmitAsync();

        var question = Assert.Single(request.Questions);
        Assert.Equal("Für eine oder zwei Personen?", question.Text);
        Assert.Equal(["Eine Person", "Zwei Personen (Paarraum)"], question.Answers.Select(a => a.Label));
        Assert.Contains(request.Chips, c => c.Chip.Part == CalendarWishPart.Room && c.CanRemove);

        await request.AnswerCommand.ExecuteAsync(question.Answers[1]);
        Assert.Contains("\"guests\":2", api.Calls.Where(c => c.Route == RequestRoute).Last().Body, StringComparison.Ordinal);

        await request.RemoveAsync(request.Chips.Single(c => c.Chip.Part == CalendarWishPart.Room));
        Assert.Contains("\"roomId\":null", api.Calls.Where(c => c.Route == RequestRoute).Last().Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mit_Gast_bucht_der_gewaehlte_Vorschlag_gleich_fuer_ihn_und_ohne_Gast_bleibt_es_bei_Neuer_Termin()
    {
        var guest = new CustomerDto(Guid.NewGuid(), "Brigitte", "Müller", null, "+43 1", null, null);
        var booked = TestData.Appointment(10, TestData.Anna) with { CustomerId = guest.Id, CustomerName = "Brigitte Müller" };
        var api = Api(Result(Proposal(10, TestData.Anna), Proposal(11, TestData.Anna), Proposal(12, TestData.Anna))).On("POST /api/appointments", booked);
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        var request = vm.Request!;
        request.Text = "Massage um 10";
        await request.SubmitAsync();
        request.Guest.Customer = guest;

        Assert.Equal("Für Brigitte Müller buchen (Enter)", request.BookText);
        await request.SubmitAsync();

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/appointments");
        Assert.Contains(guest.Id.ToString(), post.Body, StringComparison.Ordinal);
        Assert.Null(vm.NewEntry);
        Assert.StartsWith("Gebucht: Brigitte Müller", vm.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zur_Behandlung_steht_was_bereitgestellt_wird_nur_als_Hinweis()
    {
        var api = Api(Result(Proposal(10, TestData.Anna))).On($"GET /api/treatments/{TestData.MassageTreatment.Id}/extras", new List<TreatmentExtraDto>
        {
            new(Guid.NewGuid(), "Tee", ExtraTiming.OnArrival, null, null, null, null, 0),
            new(Guid.NewGuid(), "Früchteteller", ExtraTiming.BeforeEnd, 10, null, null, null, 0),
        });
        var vm = await OpenAsync(api);
        vm.HandleKey(Key.K, KeyModifiers.Control);
        vm.Request!.Text = "Massage";
        await vm.Request.SubmitAsync();

        Assert.Equal("Bereitgestellt wird: Tee, Früchteteller", vm.Request.ExtrasText);
    }
}

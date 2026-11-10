using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Aufbau der Kundenkarte (R07 Slice 141, Ä349): „Gut zu wissen“ als Chips, Leeres nur in „Noch nicht erfasst“, Kennzahlen als Zeile, ein Hauptknopf.</summary>
public sealed class CustomerCardLayoutTests
{
    private static readonly int[] RelativeSamples = [0, 1, 3, 13, 14, 37];

    private static readonly CustomerPreferences HelgaWishes = new("sanft", null, null, null, null, null, "Füße (kitzlig)", null);

    private static async Task<CustomersViewModel> OpenAsync(
        CustomerDto customer, CustomerInsights insights, CustomerPreferences? wishes, List<CustomerFlagDto>? flags = null, List<AppointmentDto>? appointments = null,
        CurrentUser? user = null)
    {
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", appointments ?? [])
            .On($"GET /api/customers/{customer.Id}/insights", insights)
            .On($"GET /api/customers/{customer.Id}/flags", flags ?? []);
        if (wishes is not null)
        {
            stub.On($"GET /api/reports/customers/{customer.Id}/preferences", wishes);
        }

        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System) { User = user ?? TestData.Owner() };
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        await customers.LoadVisitsAsync(customers.Selected);
        return customers;
    }

    private static CustomerDto Plain() => SpaSample.Customers[0] with { Booking = null, Billing = null, Discount = null, Note = null };

    [Fact]
    public void Nicht_behandeln_steht_nicht_bei_den_Wuenschen_und_kuerzt_den_Klammerzusatz()
    {
        var chips = CustomersViewModel.Chips(HelgaWishes).ToList();

        Assert.Equal([new PreferenceChip("Druck: sanft")], chips);
        Assert.Equal(new PreferenceChip("Nicht behandeln: Füße", true, "Nicht behandeln: Füße (kitzlig)"), CustomersViewModel.AvoidChip("Füße (kitzlig)"));
        Assert.Equal(new PreferenceChip("Nicht behandeln: Füße", true), CustomersViewModel.AvoidChip("Füße"));
    }

    [Fact]
    public async Task Helga_Auer_zeigt_Nicht_behandeln_im_Warnstreifen_und_nur_einmal()
    {
        var helga = Plain() with { FirstName = "Helga", LastName = "Auer" };

        var customers = await OpenAsync(helga, SpaSample.Insights() with { ReceptionNote = null }, HelgaWishes);

        Assert.True(customers.HasAttention);
        Assert.Equal("Nicht behandeln: Füße", customers.AvoidWarning?.Text);
        Assert.True(customers.WishesSectionVisible);
        Assert.DoesNotContain(customers.WishChips, c => c.IsWarning);
        Assert.False(customers.MissingWishes);
    }

    [Fact]
    public async Task Ohne_Nicht_behandeln_und_ohne_Ausfaelle_gibt_es_keinen_Warnstreifen()
    {
        var insights = SpaSample.Insights() with { NoShows = [], LateCancellations = [] };

        var customers = await OpenAsync(Plain(), insights, new CustomerPreferences("sanft", null, null, null, null, null, null, null));

        Assert.Equal((false, null, null), (customers.HasAttention, customers.AvoidWarning, customers.StatsWarning));
    }

    [Fact]
    public async Task Leere_Kennzeichen_Vermerk_und_Lieblingstermine_haben_keine_Abschnitte_nur_noch_nicht_erfasst()
    {
        var customers = await OpenAsync(Plain(), SpaSample.Insights() with { ReceptionNote = null }, null);

        Assert.Equal((false, false, false, false), (customers.FlagsSectionVisible, customers.ReceptionNoteSectionVisible, customers.BookingSectionVisible, customers.WishesSectionVisible));
        Assert.Equal((false, false, false), (customers.BillingSectionVisible, customers.DiscountSectionVisible, customers.NoteSectionVisible));
        Assert.True(customers.HasMissing);
        Assert.Equal((true, true, true, true), (customers.MissingFlags, customers.MissingReceptionNote, customers.MissingBooking, customers.MissingWishes));

        // Öffnen eines leeren Abschnitts zeigt ihn zum Bearbeiten, Abbrechen nimmt ihn wieder weg.
        customers.AddFlagCommand.Execute(null);
        Assert.Equal((true, false), (customers.FlagsSectionVisible, customers.MissingFlags));
        customers.CancelFlagCommand.Execute(null);
        Assert.Equal((false, true), (customers.FlagsSectionVisible, customers.MissingFlags));
    }

    [Fact]
    public async Task Gesetztes_zeigt_Abschnitte_und_fehlt_in_noch_nicht_erfasst()
    {
        var customers = await OpenAsync(SpaSample.Customers[0], SpaSample.Insights(), SpaSample.Wishes(), SpaSample.CustomerFlags());

        Assert.Equal((true, true, true), (customers.FlagsSectionVisible, customers.ReceptionNoteSectionVisible, customers.WishesSectionVisible));
        Assert.Equal((false, false, false), (customers.MissingFlags, customers.MissingReceptionNote, customers.MissingWishes));
        Assert.Equal(SpaSample.Insights().ReceptionNote, customers.ReceptionNoteText);
    }

    [Fact]
    public async Task Ohne_naechsten_Termin_ist_Termin_buchen_der_Hauptknopf()
    {
        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null);

        Assert.False(customers.NoUpcomingPlanned);
        Assert.True(customers.CanBook);
        Assert.True(customers.IsBookPrimary);
        Assert.False(customers.IsNewPrimary);

        // Beim Bearbeiten gehört der Hauptknopf dem Speichern.
        customers.EditWishesCommand.Execute(null);
        Assert.False(customers.IsBookPrimary);
    }

    private static AppointmentDto Visit(int days, EmployeeDto person, AppointmentStatus status, string? treatment = null)
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(days).AddHours(9), TimeSpan.Zero);
        return SpaSample.Selected with
        {
            Id = Guid.NewGuid(),
            Start = start,
            End = start.AddMinutes(80),
            Status = status,
            EmployeeId = person.Id,
            TreatmentName = treatment ?? SpaSample.Selected.TreatmentName,
        };
    }

    [Fact]
    public async Task Naechster_Termin_steht_als_Kachel_mit_Abstand_und_Person_weitere_darunter()
    {
        // Given drei kommende Termine, der nächste in drei Tagen bei Lena Huber
        List<AppointmentDto> upcoming =
        [
            Visit(20, SpaSample.Lena, AppointmentStatus.Booked), Visit(3, SpaSample.Lena, AppointmentStatus.Booked, "Hot-Stone-Massage"),
            Visit(10, SpaSample.Marco, AppointmentStatus.Booked, "Aromaöl-Massage"),
        ];

        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null, appointments: upcoming);

        // Then nennt die Kachel Abstand, Behandlung und Person; die beiden späteren stehen kurz darunter.
        Assert.Equal(("in 3 Tagen", "Hot-Stone-Massage bei Lena Huber"), (customers.NextVisit?.Relative, customers.NextVisit?.Text));
        Assert.Equal(["Aromaöl-Massage", SpaSample.Selected.TreatmentName], customers.LaterEntries.Select(e => e.Text));
        Assert.Empty(customers.RecentEntries);
        Assert.False(customers.NoUpcomingPlanned);
    }

    [Fact]
    public async Task Zuletzt_nennt_die_Stammperson_einmal_und_andere_Personen_in_der_Zeile()
    {
        // Given Lena Huber hat 9 von 14 Besuchen, der mittlere der letzten drei Termine war bei Marco Berger
        List<AppointmentDto> past =
        [
            Visit(-7, SpaSample.Lena, AppointmentStatus.Completed, "Gesichtsbehandlung"), Visit(-14, SpaSample.Marco, AppointmentStatus.Completed, "Rückenmassage"),
            Visit(-21, SpaSample.Lena, AppointmentStatus.NoShow, "Hot-Stone-Massage"),
        ];

        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null, appointments: past);

        // Then steht „meist bei Lena Huber“ einmal oben, ihre Zeilen ohne Namen.
        Assert.Equal("meist bei Lena Huber", customers.MainPersonText);
        Assert.Equal(["Gesichtsbehandlung", "Rückenmassage bei Marco Berger", "Hot-Stone-Massage"], customers.RecentEntries.Select(e => e.Text));
        Assert.Equal("Nicht erschienen", customers.RecentEntries[2].Status);
    }

    [Fact]
    public async Task Ohne_Mehrheit_einer_Person_steht_die_Person_in_jeder_Zeile()
    {
        var insights = SpaSample.Insights() with { Employees = [new(SpaSample.Lena.Id, "Lena Huber", 5, null, null), new(SpaSample.Marco.Id, "Marco Berger", 5, null, null)] };

        var customers = await OpenAsync(Plain(), insights, null, appointments: [Visit(-7, SpaSample.Lena, AppointmentStatus.Completed, "Gesichtsbehandlung")]);

        Assert.Null(customers.MainPersonText);
        Assert.Equal("Gesichtsbehandlung bei Lena Huber", Assert.Single(customers.RecentEntries).Text);
    }

    [Fact]
    public void Datum_und_Abstand_kurz_und_mit_Jahr_nur_wenn_noetig()
    {
        var today = new DateOnly(2026, 11, 10);

        Assert.Equal(["19.10.", "21.9.2025"], [CustomersViewModel.ShortDate(new DateOnly(2026, 10, 19), today), CustomersViewModel.ShortDate(new DateOnly(2025, 9, 21), today)]);
        Assert.Equal(["heute", "morgen", "in 3 Tagen", "in 13 Tagen", "in 2 Wochen", "in 5 Wochen"], RelativeSamples.Select(CustomersViewModel.RelativeDays));
    }

    [Fact]
    public async Task Zuletzt_zeigt_die_drei_juengsten_Termine()
    {
        // Given fünf vergangene Termine im Abstand von je einer Woche
        var past = Enumerable.Range(1, 5).Select(weeks =>
        {
            var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-7 * weeks).AddHours(9), TimeSpan.Zero);
            return SpaSample.Selected with { Id = Guid.NewGuid(), Start = start, End = start.AddMinutes(80), Status = AppointmentStatus.Completed };
        }).ToList();

        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null, appointments: past);

        // Then stehen unter „Zuletzt“ nur drei; alle fünf bleiben unter „Alle Termine“.
        Assert.Equal(3, customers.RecentEntries.Count);
        Assert.Equal(5, customers.Visits.Count);
    }

    [Fact]
    public async Task Nur_vergangene_Termine_ergeben_Kein_Termin_geplant()
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-20).AddHours(9), TimeSpan.Zero);
        var past = SpaSample.Selected with { Id = Guid.NewGuid(), Start = start, End = start.AddMinutes(80), Status = AppointmentStatus.Completed };

        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null, appointments: [past]);

        Assert.True(customers.NoUpcomingPlanned);
        Assert.Null(customers.NextVisit);
    }

    [Fact]
    public async Task Rezeption_ohne_Verwaltungsrecht_bekommt_keine_Verwaltungsaktionen()
    {
        var reception = await OpenAsync(Plain(), SpaSample.Insights(), null, user: TestData.Reception());
        var owner = await OpenAsync(Plain(), SpaSample.Insights(), null, user: TestData.Owner());

        Assert.Equal((true, false), (reception.CanBook, reception.MayManage));
        Assert.Equal((true, true), (owner.CanBook, owner.MayManage));
    }

    [Fact]
    public void Kennzahlen_als_eine_Zeile_und_Warnungen_getrennt()
    {
        var today = new DateOnly(2026, 11, 4);

        Assert.Equal(
            "7 Besuche in 12 Monaten · zuletzt am 21.9. · meist alle 5 Wochen · 845,00 € Umsatz in 12 Monaten (1.602,00 € gesamt)",
            CustomersViewModel.Stats(SpaSample.Insights(finance: true), today));
        Assert.Equal("7 Besuche in 12 Monaten · zuletzt am 21.9.2026 · meist alle 5 Wochen", CustomersViewModel.Stats(SpaSample.Insights(), new DateOnly(2027, 1, 2)));
        Assert.Equal(
            "1 Besuch in 12 Monaten · zuletzt am 21.9. · nie gefehlt",
            CustomersViewModel.Stats(SpaSample.Insights() with { VisitsLastYear = 1, IntervalDays = null, NoShows = [] }, today));
        Assert.Equal("Noch kein Besuch", CustomersViewModel.Stats(SpaSample.Insights() with { Visits = 0, NoShows = [] }, today));
    }
}

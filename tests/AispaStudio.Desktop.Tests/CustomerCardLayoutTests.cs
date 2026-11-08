using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Aufbau der Kundenkarte (R07 Slice 141, Ä349): „Gut zu wissen“ als Chips, Leeres nur in „Noch nicht erfasst“, Kennzahlen als Zeile, ein Hauptknopf.</summary>
public sealed class CustomerCardLayoutTests
{
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
    public void Nicht_behandeln_steht_als_erster_Warn_Chip_und_kuerzt_den_Klammerzusatz()
    {
        var chips = CustomersViewModel.Chips(HelgaWishes).ToList();

        Assert.Equal(new PreferenceChip("Nicht behandeln: Füße", true, "Nicht behandeln: Füße (kitzlig)"), chips[0]);
        Assert.Equal(new PreferenceChip("Druck: sanft"), chips[1]);
        Assert.Equal(2, chips.Count);
        Assert.Equal(new PreferenceChip("Nicht behandeln: Füße", true), CustomersViewModel.Chips(SpaSample.Wishes()).First());
    }

    [Fact]
    public async Task Helga_Auer_zeigt_den_Warn_Chip_im_ersten_Abschnitt()
    {
        var helga = Plain() with { FirstName = "Helga", LastName = "Auer" };

        var customers = await OpenAsync(helga, SpaSample.Insights() with { ReceptionNote = null }, HelgaWishes);

        Assert.True(customers.WishesSectionVisible);
        Assert.Equal(("Nicht behandeln: Füße", true), (customers.WishChips[0].Text, customers.WishChips[0].IsWarning));
        Assert.False(customers.MissingWishes);
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
    public async Task Ohne_naechsten_Termin_steht_keiner_und_Termin_buchen_ist_der_Hauptknopf()
    {
        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null);

        Assert.Equal("Nächster Termin: keiner", customers.NextLine);
        Assert.True(customers.CanBook);
        Assert.True(customers.IsBookPrimary);
        Assert.False(customers.IsNewPrimary);

        // Beim Bearbeiten gehört der Hauptknopf dem Speichern.
        customers.EditWishesCommand.Execute(null);
        Assert.False(customers.IsBookPrimary);
    }

    [Fact]
    public async Task Naechster_Termin_nennt_Tag_Zeit_Behandlung_und_Person()
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(9), TimeSpan.Zero);
        var next = SpaSample.Selected with { Id = Guid.NewGuid(), Start = start, End = start.AddMinutes(80), Status = AppointmentStatus.Booked };

        var customers = await OpenAsync(Plain(), SpaSample.Insights(), null, appointments: [next]);

        Assert.StartsWith("Nächster Termin: ", customers.NextLine, StringComparison.Ordinal);
        Assert.Contains($"{next.TreatmentName} bei ", customers.NextLine, StringComparison.Ordinal);
        Assert.Single(customers.UpcomingEntries);
        Assert.Empty(customers.RecentEntries);
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

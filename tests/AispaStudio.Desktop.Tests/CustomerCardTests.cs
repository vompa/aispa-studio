using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kundenkarte: Rechnungsempfänger und Rabatt ansehen, ändern und entfernen; neue Kundin mit nur einem Namen.</summary>
public sealed class CustomerCardTests
{
    [Fact]
    public async Task Stammdaten_in_der_Kundenkarte_bearbeiten()
    {
        var customer = SpaSample.Customers[0];
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"PUT /api/customers/{customer.Id}", customer with { Phone = "+43 660 999", BirthDate = new DateOnly(1985, 3, 14) });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];

        customers.EditContactCommand.Execute(null);
        Assert.True(customers.IsEditingContact);
        var form = customers.Contact!;
        Assert.Equal(customer.FirstName, form.FirstName);
        form.FirstName = form.LastName = " ";
        Assert.False(form.SaveCommand.CanExecute(null));
        form.FirstName = customer.FirstName;
        form.Phone = "+43 660 999";
        form.BirthDate = "31.02.1985";
        await form.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Den 31.02.1985 gibt es nicht.", form.BirthDateError);
        Assert.Null(form.Error);
        Assert.True(customers.IsEditingContact);

        // Ein Geburtstag braucht das Jahr (Design-Brief 5.17); ein zweistelliges Jahr liegt nie in der Zukunft.
        form.BirthDate = "14.3.";
        await form.SaveCommand.ExecuteAsync(null);
        Assert.Equal(DateInputParser.YearMissingError, form.BirthDateError);

        form.BirthDate = "14.3.85";
        await form.SaveCommand.ExecuteAsync(null);
        var sent = JsonSerializer.Deserialize<CustomerInput>(stub.Calls.Last(c => c.Route == $"PUT /api/customers/{customer.Id}").Body!, ApiClient.Json)!;

        Assert.Equal(("+43 660 999", new DateOnly(1985, 3, 14), ""), (sent.Phone, sent.BirthDate, sent.LastName));
        Assert.Null(form.Error);
        Assert.False(customers.IsEditingContact);
        Assert.Equal("+43 660 999", customers.Results[0].Phone);
        Assert.Equal("14. März 1985", customers.SelectedBirthday);
    }

    [Fact]
    public async Task Rechnungsempfaenger_eintragen_aendern_und_entfernen()
    {
        var customer = SpaSample.Customers[0];
        var billing = new CustomerBilling("Müller Consulting GmbH", "Ringstraße 1\n1010 Wien", "ATU12345678");
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"PUT /api/customers/{customer.Id}/billing", customer with { Billing = billing });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        var before = customers.BillingText;

        customers.EditBillingCommand.Execute(null);
        var canSaveEmpty = customers.SaveBillingCommand.CanExecute(null);
        customers.BillingName = billing.Name;
        customers.BillingAddress = billing.Address;
        customers.BillingVatId = " ATU12345678 ";
        await customers.SaveBillingCommand.ExecuteAsync(null);
        var saved = JsonSerializer.Deserialize<CustomerBilling>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json);

        Assert.Empty(before); // ohne Empfänger kein Abschnitt, nur „…“ → „Rechnungsempfänger“ (Slice 141)
        Assert.False(canSaveEmpty);
        Assert.Equal(billing, saved);
        Assert.False(customers.IsEditingBilling);
        Assert.Equal("Müller Consulting GmbH\nRingstraße 1\n1010 Wien\nUID ATU12345678", customers.BillingText);
        Assert.Equal(billing, customers.Results[0].Billing);

        customers.EditBillingCommand.Execute(null);
        Assert.Equal(billing.Address, customers.BillingAddress);
        stub.On($"PUT /api/customers/{customer.Id}/billing", customer);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        customers.Dialogs = dialogs;
        await customers.AskRemoveBillingCommand.ExecuteAsync(null);
        Assert.DoesNotContain(stub.Calls.Where(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)), c => c.Body!.Contains("\"name\":\"\"", StringComparison.Ordinal));
        Assert.Equal("Rechnungsanschrift entfernen?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await customers.AskRemoveBillingCommand.ExecuteAsync(null);
        Assert.Equal(new CustomerBilling("", ""), JsonSerializer.Deserialize<CustomerBilling>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json));
        Assert.Null(customers.Selected!.Billing);
    }

    [Fact]
    public async Task Rabatt_in_Prozent_mit_Geltungsbereich_eintragen_und_entfernen()
    {
        var customer = SpaSample.Customers[0];
        var discount = new CustomerDiscount(12.5m, CustomerDiscountScope.Products);
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"PUT /api/customers/{customer.Id}/discount", customer with { Discount = discount });
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        var before = customers.DiscountText;

        await customers.EditDiscountCommand.ExecuteAsync(null);
        var canSaveEmpty = customers.SaveDiscountCommand.CanExecute(null);
        customers.DiscountPercent = "100";
        var canSaveHundred = customers.SaveDiscountCommand.CanExecute(null);
        customers.DiscountPercent = "12,5 %";
        customers.DiscountScope = CustomersViewModel.DiscountScopes.Single(o => o.Scope == CustomerDiscountScope.Products);
        await customers.SaveDiscountCommand.ExecuteAsync(null);
        var saved = JsonSerializer.Deserialize<CustomerDiscount>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json);

        Assert.Empty(before); // ohne Rabatt kein Abschnitt, nur „…“ → „Stammrabatt“ (Slice 141)
        Assert.False(canSaveEmpty);
        Assert.False(canSaveHundred);
        Assert.Equal(discount, saved);
        Assert.False(customers.IsEditingDiscount);
        Assert.StartsWith("12,5 % nur auf Produkte", customers.DiscountText, StringComparison.Ordinal);

        await customers.EditDiscountCommand.ExecuteAsync(null);
        Assert.Equal("12,5", customers.DiscountPercent);
        stub.On($"PUT /api/customers/{customer.Id}/discount", customer);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        customers.Dialogs = dialogs;
        await customers.AskRemoveDiscountCommand.ExecuteAsync(null);
        Assert.Equal("Stammrabatt entfernen?", dialogs.Asked.Single().Title);
        dialogs.AssertRules();
        Assert.Equal(0m, JsonSerializer.Deserialize<CustomerDiscount>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json)!.Percent);
        Assert.Null(customers.Selected!.Discount);
    }

    [Fact]
    public void Neue_Kundin_braucht_nur_einen_Namen()
    {
        var customers = new CustomersViewModel(new StubApi().Client(), TimeProvider.System);

        var empty = customers.CreateCommand.CanExecute(null);
        customers.NewLastName = "Huber";
        var lastOnly = customers.CreateCommand.CanExecute(null);
        customers.NewLastName = "";
        customers.NewFirstName = "Mia";

        Assert.False(empty);
        Assert.True(lastOnly);
        Assert.True(customers.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Offene_Gutscheine_und_letzte_Kaeufe_im_Verlauf()
    {
        var customer = SpaSample.Customers[0];
        var insights = SpaSample.Insights() with
        {
            Vouchers = [new(VoucherKind.MultiPurpose, "4F2K", null, 35m, "EUR", new DateOnly(2026, 12, 31)), new(VoucherKind.SinglePurpose, "AB12", "Massage", 21m, "EUR", null)],
            Purchases = [new(new DateOnly(2026, 11, 3), "Massageöl", 2, 24m, "EUR"), new(new DateOnly(2026, 10, 20), "Kerze", 1, 5m, "EUR")],
        };
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"GET /api/customers/{customer.Id}/insights", insights);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        await customers.LoadVisitsAsync(customers.Selected);

        Assert.Equal(["Wertgutschein …4F2K: 35,00 € offen, gültig bis 31.12.2026", "Massage …AB12: 21,00 € offen"], customers.VoucherLines);
        Assert.Equal(["3.11.2026 · Massageöl 2× · 24,00 €", "20.10.2026 · Kerze · 5,00 €"], customers.PurchaseLines);
        Assert.True(customers.HasVouchers && customers.HasPurchases);

        // Ohne das Recht zu buchen liefert die API nichts: beide Abschnitte fehlen.
        stub.On($"GET /api/customers/{customer.Id}/insights", SpaSample.Insights());
        await customers.LoadVisitsAsync(customers.Selected);
        Assert.Empty(customers.VoucherLines);
        Assert.False(customers.HasVouchers || customers.HasPurchases);
    }

    [Fact]
    public async Task Kennzahlen_Sterne_Wuensche_und_Rezeptionsvermerk()
    {
        var customer = SpaSample.Customers[0];
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"GET /api/customers/{customer.Id}/insights", SpaSample.Insights(finance: true))
            .On($"PUT /api/customers/{customer.Id}/reception-note", null, System.Net.HttpStatusCode.NoContent)
            .On($"PUT /api/reports/customers/{customer.Id}/preferences", new CustomerPreferences("sanft", null, null, null, true, null, null, null));
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        await customers.LoadVisitsAsync(customers.Selected);

        Assert.Equal("★★", customers.StarsGlyph);
        Assert.Equal(("★★ Stammgast", "Stammgast, 2 Sterne"), (customers.StarsChipText, customers.StarsName));
        Assert.Equal(CustomersViewModel.Stats(SpaSample.Insights(finance: true), DateOnly.FromDateTime(DateTime.Now)), customers.StatsLine);
        Assert.Contains("845,00 € Umsatz in 12 Monaten (1.602,00 € gesamt)", customers.StatsLine, StringComparison.Ordinal);
        Assert.Equal("1× nicht erschienen · 1 späte Absage", customers.StatsWarning);
        Assert.Equal("Nicht erschienen: 10.7.2026\nSpäte Absagen: 2.3.2026", customers.StatsWarningTip);
        Assert.Equal(["Hot-Stone-Massage: 7 von 10 offen, gültig bis 30.06.2027"], customers.PackageLines);
        // „Je Person“ geht im Verlauf auf: jeder Termin nennt die Person.
        Assert.Empty(customers.WishChips);
        Assert.True(customers.MissingWishes);

        customers.EditReceptionNoteCommand.Execute(null);
        Assert.Equal(SpaSample.Insights().ReceptionNote, customers.ReceptionNoteDraft);
        customers.ReceptionNoteDraft = "Kommt mit dem Rad; schwanger";
        Assert.Contains("„schwanger“", customers.ReceptionNoteWarning, StringComparison.Ordinal);
        customers.ReceptionNoteDraft = " Mann holt ab ";
        Assert.Null(customers.ReceptionNoteWarning);
        await customers.SaveReceptionNoteCommand.ExecuteAsync(null);
        Assert.Equal(new CustomerReceptionNoteInput("Mann holt ab"),
            JsonSerializer.Deserialize<CustomerReceptionNoteInput>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json));
        Assert.Equal(("Mann holt ab", false), (customers.ReceptionNoteText, customers.IsEditingReceptionNote));

        customers.EditWishesCommand.Execute(null);
        customers.WishPressure = "sanft";
        customers.WishQuiet = true;
        customers.WishOther = "Allergie gegen Mandelöl";
        Assert.NotNull(customers.WishesWarning);
        customers.WishOther = "";
        await customers.SaveWishesCommand.ExecuteAsync(null);
        var wishes = JsonSerializer.Deserialize<CustomerPreferences>(stub.Calls.Last(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json);
        Assert.Equal(new CustomerPreferences("sanft", null, null, null, true, null, null, null), wishes);
        Assert.Equal(["Druck: sanft", "wenig Gespräch"], customers.WishChips.Select(c => c.Text));
    }

    [Fact]
    public void Gesundheitswoerter_werden_erkannt_alltaegliches_nicht()
    {
        Assert.Equal("Nussallergie", HealthWords.Find("Nussallergie, sonst alles gut"));
        Assert.Equal("Schwangerschaft", HealthWords.Find("Schwangerschaft 5. Monat"));
        Assert.Null(HealthWords.Find("mag Lavendel, wenig Gespräch, zahlt mit Karte"));
        Assert.Null(HealthWords.Find(null));
    }

    [Fact]
    public async Task Kennzeichen_nach_Rolle_anbieten_setzen_und_aufheben()
    {
        var customer = SpaSample.Customers[0];
        var mine = new CustomerFlagDto(Guid.NewGuid(), CustomerFlagKind.NotWithMe, SpaSample.Lena.Id, SpaSample.Lena.DisplayName, null, new DateOnly(2026, 10, 3), true);
        var stop = new CustomerFlagDto(Guid.NewGuid(), CustomerFlagKind.DoNotBook, null, null, "Zweimal nicht erschienen", new DateOnly(2026, 10, 19), false);
        var stub = new StubApi()
            .On("GET /api/customers?search=", SpaSample.Customers.ToList())
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"GET /api/customers/{customer.Id}/flags", new List<CustomerFlagDto> { stop })
            .On($"POST /api/customers/{customer.Id}/flags", new List<CustomerFlagDto> { mine, stop })
            .On($"DELETE /api/customers/{customer.Id}/flags/{mine.Id}", new List<CustomerFlagDto> { stop });
        var therapist = new CurrentUser("u2", "lena@aispa.test", "Lena Huber", 1, null, SpaSample.Lena.Id, [AccessRight.ViewCalendar, AccessRight.ChangeStatus]);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System) { User = therapist };
        await customers.InitializeAsync();
        customers.Selected = customers.Results[0];
        await customers.LoadVisitsAsync(customers.Selected);

        var row = Assert.Single(customers.Flags);
        Assert.Equal(("Nicht mehr buchen", "Grund: Zweimal nicht erschienen · seit 19.10.2026", false), (row.Title, row.Reason, row.MayRemove));

        customers.AddFlagCommand.Execute(null);
        Assert.Equal([CustomerFlagKind.NotWithMe], customers.FlagChoices.Select(c => c.Kind));
        Assert.Contains("Lena Huber", customers.FlagHint, StringComparison.Ordinal);
        await customers.SaveFlagCommand.ExecuteAsync(null);
        var sent = JsonSerializer.Deserialize<CustomerFlagInput>(stub.Calls.Last(c => c.Route.StartsWith("POST ", StringComparison.Ordinal)).Body!, ApiClient.Json);
        Assert.Equal(new CustomerFlagInput(CustomerFlagKind.NotWithMe), sent);
        Assert.Equal(["Nicht bei Lena Huber", "Nicht mehr buchen"], customers.Flags.Select(f => f.Title));

        await customers.RemoveFlagCommand.ExecuteAsync(customers.Flags[0]);
        Assert.Single(customers.Flags);

        // Die Leitung bekommt alle Kennzeichen; „Nicht mehr buchen“ braucht einen Grund.
        var manager = therapist with { EmployeeId = null, Rights = [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ManageLocation] };
        var lead = new CustomersViewModel(stub.Client(), TimeProvider.System) { User = manager };
        lead.AddFlagCommand.Execute(null);
        Assert.Equal([CustomerFlagKind.MoreTime, CustomerFlagKind.InformManagement, CustomerFlagKind.DoNotBook], lead.FlagChoices.Select(c => c.Kind));
        lead.NewFlag = lead.FlagChoices[^1];
        Assert.False(lead.SaveFlagCommand.CanExecute(null));
        lead.NewFlagReason = "offene Rechnungen";
        Assert.True(lead.SaveFlagCommand.CanExecute(null));
    }

    [Fact]
    public async Task Die_Kundenkarte_nennt_neben_dem_Geburtsdatum_Alter_und_den_naechsten_Geburtstag()
    {
        var customer = SpaSample.Customers[0] with { BirthDate = new DateOnly(1981, 3, 13) };
        var insights = new CustomerInsights(3, 2, null, null, 1, "Stammgast", [], [], [], null, [], false, null, Birthday: new GuestBirthday(new(2027, 3, 13), 12, 46, 45));
        var stub = new StubApi()
            .On("GET /api/customers?search=", new List<CustomerDto> { customer })
            .On($"GET /api/customers/{customer.Id}/appointments", new List<AppointmentDto>())
            .On($"GET /api/customers/{customer.Id}/insights", insights);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();

        Assert.Null(customers.BirthdayInfo);
        customers.Selected = customers.Results[0];
        for (var wait = 0; wait < 100 && customers.BirthdayInfo is null; wait++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }


        Assert.Equal("13. März 1981", customers.SelectedBirthday);
        Assert.Equal("45 Jahre · Nächster Geburtstag in 12 Tagen", customers.BirthdayInfo);
    }

    [Fact]
    public void Kennzeichen_stehen_im_Kalender_und_in_der_Hover_Karte()
    {
        var guest = new CalendarGuest(Guid.NewGuid(), 4, 4, 0, null, null,
            Flags: [CustomerFlagKind.MoreTime, CustomerFlagKind.InformManagement, CustomerFlagKind.DoNotBook]);
        var flags = DayLayout.Flags(guest);

        Assert.Equal(["+10", "L", "STOP", "★"], flags.Select(f => f.Symbol));
        Assert.True(flags.Single(f => f.Symbol == "STOP").IsNote);
        Assert.False(flags.Single(f => f.Symbol == "+10").IsNote);
    }
}

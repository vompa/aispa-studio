using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „Neue Kundin“ an der Theke (R07 Slice 140): Name und Telefon zuerst, „Weitere Angaben“ zu, „Schon vorhanden?“ beim Tippen
/// aus der Kundensuche, Anlegen aus der leeren Suche und „Anlegen und Termin buchen“.
/// </summary>
public sealed class CustomerNewFormTests
{
    private static readonly CustomerDto Huber = SpaSample.Customers[0] with
    {
        FirstName = "Anna",
        LastName = "Huber",
        Phone = "+43 664 1234567",
        PreferredName = null,
    };

    private static CustomerDto Created(string first, string last) => SpaSample.Customers[0] with
    {
        Id = Guid.NewGuid(),
        FirstName = first,
        LastName = last,
        Phone = null,
        PreferredName = null,
    };

    /// <summary>Die Kundensuche kennt „Huber“ und die Ziffern der Nummer (liefern <paramref name="found"/>); alles andere findet niemanden.</summary>
    private static StubApi Api(params CustomerDto[] found) => new StubApi()
        .On("GET /api/customers?search=", new List<CustomerDto>())
        .On("GET /api/customers?search=Gruber", new List<CustomerDto>())
        .On("GET /api/customers?search=Anna%20Gruber", new List<CustomerDto>())
        .On("GET /api/customers?search=Huber", found.ToList())
        .On("GET /api/customers?search=1234567", found.ToList())
        .On("GET /api/customers?search=anna%40huber.at", found.ToList());

    [Fact]
    public async Task Given_Neue_Kundin_When_die_Maske_oeffnet_Then_sind_Anrede_Titel_Rufname_und_Notiz_zugeklappt()
    {
        var customers = new CustomersViewModel(Api().Client(), TimeProvider.System);
        await customers.InitializeAsync();

        customers.StartNewCommand.Execute(null);

        Assert.True(customers.ShowNew);
        Assert.False(customers.NewMoreOpen);
    }

    [Fact]
    public async Task Given_es_gibt_Huber_Anna_When_man_im_Nachnamen_Huber_tippt_Then_erscheint_Schon_vorhanden_mit_Huber_Anna()
    {
        var stub = Api(Huber);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;
        customers.StartNewCommand.Execute(null);

        customers.NewLastName = "Huber";
        await customers.Matches.CheckAsync(customers.NewLastName, customers.NewPhone);

        Assert.True(customers.Matches.HasOptions);
        var option = customers.Matches.Options.Single();
        Assert.Equal(("Huber, Anna", "+43 664 1234567"), (option.Name, option.Phone));
        Assert.Contains(stub.Calls, c => c.Route == "GET /api/customers?search=Huber");
    }

    [Fact]
    public async Task Given_ein_Treffer_When_Diese_Kundin_nehmen_Then_oeffnet_die_vorhandene_Karte_statt_neu_anzulegen()
    {
        var customers = new CustomersViewModel(Api(Huber).Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;
        customers.StartNewCommand.Execute(null);
        customers.NewLastName = "Huber";
        await customers.Matches.CheckAsync("Huber", "");

        await customers.Matches.Options.Single().TakeCommand.ExecuteAsync(null);

        Assert.False(customers.ShowNew);
        Assert.Equal(Huber.Id, customers.Selected?.Id);
        Assert.False(customers.Matches.HasOptions);
    }

    [Fact]
    public async Task Given_ein_Telefon_in_anderer_Schreibweise_When_man_es_tippt_Then_sucht_die_Maske_mit_den_letzten_sieben_Ziffern()
    {
        var stub = Api(Huber);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;

        await customers.Matches.CheckAsync("", "0664 1234567");

        Assert.Equal("Huber, Anna", customers.Matches.Options.Single().Name);
        Assert.Contains(stub.Calls, c => c.Route == "GET /api/customers?search=1234567");
    }

    [Fact]
    public async Task Zu_wenig_Angaben_fragen_nicht_bei_der_Kundensuche_nach()
    {
        var stub = Api(Huber);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;
        var before = stub.Calls.Count;

        await customers.Matches.CheckAsync("H", "0664", "");
        // Halbe Mailadressen beim Tippen: „anna@huber.“ wäre ohne den Punkt am Ende ein Titel-Wort und die Suche ohne Filter.
        foreach (var email in new[] { "anna@", "anna@huber", "anna@huber.", "@huber.at", "anna @huber.at" })
        {
            await customers.Matches.CheckAsync("", "", email);
        }

        Assert.False(customers.Matches.HasOptions);
        Assert.Equal(before, stub.Calls.Count);
    }

    [Fact]
    public async Task Given_es_gibt_Huber_Anna_mit_Mail_When_man_die_Mail_in_anderer_Schreibweise_tippt_Then_erscheint_Schon_vorhanden()
    {
        var stub = Api(Huber);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;

        await customers.Matches.CheckAsync("", "", " Anna@Huber.AT ");

        Assert.Equal("Huber, Anna", customers.Matches.Options.Single().Name);
        Assert.Contains(stub.Calls, c => c.Route == "GET /api/customers?search=anna%40huber.at");
    }

    [Fact]
    public async Task Given_Neue_Kundin_When_man_die_Mail_tippt_Then_fragt_die_Maske_die_Kundensuche()
    {
        var stub = Api(Huber);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Matches.Delay = TimeSpan.Zero;
        customers.StartNewCommand.Execute(null);

        customers.NewEmail = "anna@huber.at";

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!customers.Matches.HasOptions && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Huber, Anna", customers.Matches.Options.Single().Name);
    }

    [Fact]
    public async Task Given_die_Suche_Gruber_findet_nichts_When_man_Neue_Kundin_Gruber_anlegen_waehlt_Then_steht_Gruber_im_Nachnamen()
    {
        var customers = new CustomersViewModel(Api().Client(), TimeProvider.System);
        await customers.InitializeAsync();

        customers.Query = "Gruber";
        await customers.SearchAsync("Gruber");

        Assert.True(customers.ShowCreateFromSearch);
        Assert.Equal("Neue Kundin „Gruber“ anlegen", customers.CreateFromSearchText);
        customers.StartNewCommand.Execute(null);
        Assert.Equal(("", "Gruber"), (customers.NewFirstName, customers.NewLastName));
    }

    [Fact]
    public async Task Zwei_Woerter_aus_der_Suche_werden_Vor_und_Nachname()
    {
        var customers = new CustomersViewModel(Api().Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.Query = "Anna Gruber";
        await customers.SearchAsync("Anna Gruber");

        customers.StartNewCommand.Execute(null);

        Assert.Equal(("Anna", "Gruber"), (customers.NewFirstName, customers.NewLastName));
    }

    [Fact]
    public async Task Die_Suche_mit_Treffern_bietet_kein_Neu_anlegen_an()
    {
        var customers = new CustomersViewModel(Api(Huber).Client(), TimeProvider.System);
        await customers.InitializeAsync();

        customers.Query = "Huber";
        await customers.SearchAsync("Huber");

        Assert.False(customers.ShowCreateFromSearch);
    }

    [Fact]
    public async Task Given_Vor_und_Nachname_When_Anlegen_und_Termin_buchen_Then_ist_der_Gast_angelegt_und_die_Buchung_wird_angefordert()
    {
        var anna = Created("Anna", "Gruber");
        var stub = Api().On("POST /api/customers", anna);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        await customers.InitializeAsync();
        CustomerDto? booked = null;
        customers.BookRequested = guest => booked = guest;
        customers.StartNewCommand.Execute(null);
        customers.NewFirstName = "Anna";
        customers.NewLastName = "Gruber";

        await customers.CreateAndBookCommand.ExecuteAsync(null);

        Assert.Equal(anna.Id, booked?.Id);
        Assert.Contains(stub.Calls, c => c.Route == "POST /api/customers");
        Assert.False(customers.ShowNew);
    }

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly CurrentUser Reception = new("u1", "rita@aispa.test", "Rita Steiner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    [Fact]
    public async Task Given_die_App_laeuft_When_Anlegen_und_Termin_buchen_Then_oeffnet_der_Kalender_die_Buchung_mit_dem_neuen_Gast()
    {
        var anna = Created("Anna", "Gruber");
        var stub = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Development", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", Reception)
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.Resources)
            .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay())
            .On("GET /api/customers?search=", new List<CustomerDto>())
            .On("POST /api/customers", anna);
        var main = new MainViewModel(stub.Client(), time: new SampleTime());
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Calendar.LoadAsync();
        await shell.Customers.InitializeAsync();
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
        shell.Customers.StartNewCommand.Execute(null);
        shell.Customers.NewFirstName = "Anna";
        shell.Customers.NewLastName = "Gruber";

        await shell.Customers.CreateAndBookCommand.ExecuteAsync(null);

        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
        Assert.Equal(anna.Id, shell.Calendar.Booking?.Customer?.Id);
    }

    [Fact]
    public async Task Anlegen_ohne_Buchung_fordert_keine_Buchung_an()
    {
        var anna = Created("Anna", "Gruber");
        var customers = new CustomersViewModel(Api().On("POST /api/customers", anna).Client(), TimeProvider.System);
        await customers.InitializeAsync();
        var requested = false;
        customers.BookRequested = _ => requested = true;
        customers.StartNewCommand.Execute(null);
        customers.NewLastName = "Gruber";

        await customers.CreateCommand.ExecuteAsync(null);

        Assert.False(requested);
        Assert.Equal(anna.Id, customers.Selected?.Id);
    }

    [Fact]
    public void Beide_Anlegen_Knoepfe_brauchen_einen_Namen()
    {
        var customers = new CustomersViewModel(Api().Client(), TimeProvider.System) { BookRequested = _ => { } };

        Assert.False(customers.CreateCommand.CanExecute(null));
        Assert.False(customers.CreateAndBookCommand.CanExecute(null));
        customers.NewLastName = "Huber";
        Assert.True(customers.CreateCommand.CanExecute(null));
        Assert.True(customers.CreateAndBookCommand.CanExecute(null));
    }

    // ---------- Telefon beim Verlassen des Feldes im Format „+43 664 1234567“ (T-019, Ä364) ----------

    private static TenantDto Spa(string country) => new(Guid.NewGuid(), "Day-Spa", country);

    [Theory]
    [InlineData("AT", "0664 1234567", "+43 664 1234567")]
    [InlineData("AT", "0043 664 123 45 67", "+43 664 1234567")]
    [InlineData("DE", "0151 1234567", "+49 151 1234567")]
    [InlineData("AT", "12", "12")]
    public async Task Telefon_steht_nach_dem_Verlassen_im_Format_des_Spa_Landes(string country, string typed, string expected)
    {
        var customers = new CustomersViewModel(Api().On("GET /api/tenant", Spa(country)).Client(), TimeProvider.System);
        customers.StartNewCommand.Execute(null);
        customers.NewPhone = typed;

        await customers.FormatNewPhoneAsync();

        Assert.Equal(expected, customers.NewPhone);
    }

    [Fact]
    public async Task Ohne_Land_des_Spas_bleibt_das_Telefon_wie_getippt_und_der_naechste_Versuch_laedt_neu()
    {
        var stub = Api();
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        customers.StartNewCommand.Execute(null);
        customers.NewPhone = "0664 1234567";

        await customers.FormatNewPhoneAsync();
        Assert.Equal("0664 1234567", customers.NewPhone);

        stub.On("GET /api/tenant", Spa("AT"));
        await customers.FormatNewPhoneAsync();
        Assert.Equal("+43 664 1234567", customers.NewPhone);
    }

    [Fact]
    public async Task Wer_waehrend_des_Ladens_weitertippt_wird_nicht_ueberschrieben()
    {
        var gate = new TaskCompletionSource();
        var stub = Api().On("GET /api/tenant", Spa("AT")).Hold("GET /api/tenant", gate.Task);
        var customers = new CustomersViewModel(stub.Client(), TimeProvider.System);
        customers.StartNewCommand.Execute(null);
        customers.NewPhone = "0664 1234567";

        var formatting = customers.FormatNewPhoneAsync();
        customers.NewPhone = "0664 7654321";
        gate.SetResult();
        await formatting;

        Assert.Equal("0664 7654321", customers.NewPhone);
    }

    [Fact]
    public async Task Abbrechen_klappt_die_weiteren_Angaben_wieder_zu()
    {
        var customers = new CustomersViewModel(Api().Client(), TimeProvider.System);
        await customers.InitializeAsync();
        customers.StartNewCommand.Execute(null);
        customers.NewMoreOpen = true;
        customers.NewNote = "Vorlieben";

        customers.CancelNewCommand.Execute(null);
        customers.StartNewCommand.Execute(null);

        Assert.False(customers.NewMoreOpen);
    }
}

using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä34: der Abschnitt „Kunde“ im Termin-Panel (Kennzahlen, Zuletzt, Kontaktlinks) und die Links aus Telefon und E-Mail.</summary>
public sealed class CustomerSectionTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(8);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private sealed class FakeActions : IContactActions
    {
        public List<Uri> Launched { get; } = [];

        public List<string> Copied { get; } = [];

        public Task LaunchAsync(Uri uri)
        {
            Launched.Add(uri);
            return Task.CompletedTask;
        }

        public Task CopyAsync(string text)
        {
            Copied.Add(text);
            return Task.CompletedTask;
        }
    }

    private static readonly Guid CustomerId = Guid.NewGuid();

    private static string SummaryRoute => $"GET /api/customers/{CustomerId}/summary";

    private static CustomerSummary Summary(bool finance = false, string? phone = "+43 664 123 45 67", string? email = "eva@example.at", string? note = "Kommt gern mit Hund") => new(
        12, new DateOnly(2027, 2, 3), 2, new CustomerNextAppointment(Guid.NewGuid(), TestData.At(10).AddDays(14), "Massage"),
        [
            new(Guid.NewGuid(), TestData.At(10).AddDays(-7), "Massage", "Anna"),
            new(Guid.NewGuid(), TestData.At(10).AddDays(-30), "Gesicht", "Ben"),
        ],
        note, phone, email, true,
        finance ? new CustomerOpenItems(45m, "EUR", [new GuestPackage(Guid.NewGuid(), "Massage", 10, 7, null)]) : null);

    private static CustomerSectionViewModel Section(StubApi api, FakeActions? actions = null, List<string>? toasts = null, List<(Guid, DateTimeOffset)>? opened = null,
        Func<Task>? openCard = null, FiscalCountry country = FiscalCountry.AT, string guest = "Eva Muster") =>
        new(api.Client(), CustomerId, guest, country, Vienna, new FixedTime(), openCard, (id, start) => opened?.Add((id, start)), text => toasts?.Add(text), () => actions);

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    // ---------- Links ----------

    [Theory]
    [InlineData("+43 664 123 45 67", "tel:+436641234567")]
    [InlineData("+43 (0) 664 / 123-45-67", "tel:+436641234567")]
    [InlineData("0664 1234567", "tel:06641234567")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Tel_Link_haelt_nur_Plus_und_Ziffern(string? phone, string? expected) =>
        Assert.Equal(expected, PhoneLinks.Tel(phone)?.OriginalString);

    [Theory]
    [InlineData("+43 664 123 45 67", FiscalCountry.AT, "https://wa.me/436641234567")]
    [InlineData("0043 664 123 45 67", FiscalCountry.DE, "https://wa.me/436641234567")]
    [InlineData("0664 123 45 67", FiscalCountry.AT, "https://wa.me/436641234567")]
    [InlineData("0171 1234567", FiscalCountry.DE, "https://wa.me/491711234567")]
    [InlineData("436641234567", FiscalCountry.AT, "https://wa.me/436641234567")]
    [InlineData("+43 ( 0 ) 664 1234567", FiscalCountry.AT, "https://wa.me/436641234567")]
    [InlineData("whatsapp:+43 664 1234567", FiscalCountry.AT, "https://wa.me/436641234567")]
    [InlineData("079 123 45 67", FiscalCountry.CH, "https://wa.me/41791234567")]
    [InlineData("+49 171 1234567", FiscalCountry.AT, "https://wa.me/491711234567")]
    [InlineData("664 123 45 67", FiscalCountry.AT, null)]
    [InlineData("12 34", FiscalCountry.AT, null)]
    [InlineData("", FiscalCountry.AT, null)]
    public void WhatsApp_Link_braucht_E164_mit_Landesvorwahl_nur_Ziffern(string phone, FiscalCountry country, string? expected) =>
        Assert.Equal(expected, PhoneLinks.WhatsApp(phone, country)?.OriginalString);

    [Fact]
    public void Mail_Link_traegt_den_Gast_im_Betreff()
    {
        Assert.Equal("mailto:eva@example.at?subject=Eva%20Muster", PhoneLinks.Mail("eva@example.at", "Eva Muster")!.OriginalString);
        Assert.Equal("mailto:b@example.at?subject=Brigitte%20M%C3%BCller%20%26%20Co", PhoneLinks.Mail(" b@example.at ", "Brigitte Müller & Co")!.OriginalString);
        Assert.Null(PhoneLinks.Mail(null, "Eva"));
        Assert.Null(PhoneLinks.Mail("  ", "Eva"));
    }

    // ---------- Laden und Kennzahlen ----------

    [Fact]
    public async Task Die_vier_Kennzahlen_stehen_kurz_mit_vollem_Datum_im_Hinweis()
    {
        var api = new StubApi().On(SummaryRoute, Summary());
        var section = Section(api);
        Assert.True(section.IsLoading);
        Assert.False(section.ShowStats);

        await section.LoadAsync();

        Assert.True(section.ShowStats);
        Assert.Equal(("12", "3.2.", "2", "15.3."), (section.VisitsText, section.LastVisitText, section.NoShowsText, section.NextText));
        Assert.True(section.HasNoShows);
        Assert.Equal("Letzter Besuch: Mittwoch, 3. Februar 2027", section.LastVisitTip);
        Assert.Equal("Mo. 15.3. 10:00 · Massage", section.NextTip);
        Assert.False(section.IsOpen);
        Assert.Equal("Anamnese vorhanden", section.AnamnesisText);
        Assert.Single(api.Calls, c => c.Route == SummaryRoute);
    }

    [Fact]
    public async Task Ohne_Besuche_und_Termin_stehen_Striche_und_nichts_Rotes()
    {
        var empty = new CustomerSummary(0, null, 0, null, [], null, null, null, false, null);
        var section = Section(new StubApi().On(SummaryRoute, empty));

        await section.LoadAsync();

        Assert.Equal(("0", "–", "0", "–"), (section.VisitsText, section.LastVisitText, section.NoShowsText, section.NextText));
        Assert.False(section.HasNoShows);
        Assert.Equal("Noch kein Besuch", section.LastVisitTip);
        Assert.Equal("Kein Termin gebucht", section.NextTip);
        Assert.Equal("Anamnese fehlt", section.AnamnesisText);
        Assert.False(section.HasRecent);
        Assert.False(section.HasNote);
        Assert.True(section.HasNoContact);
    }

    [Fact]
    public async Task Ein_anderes_Jahr_bekommt_die_Jahreszahl_zweistellig()
    {
        var old = Summary() with { LastVisit = new DateOnly(2025, 11, 30), Next = new CustomerNextAppointment(Guid.NewGuid(), TestData.At(10).AddYears(1), "Massage") };
        var section = Section(new StubApi().On(SummaryRoute, old));

        await section.LoadAsync();

        Assert.Equal(("30.11.25", "1.3.28"), (section.LastVisitText, section.NextText));
    }

    [Fact]
    public async Task Offener_Betrag_und_Pakete_stehen_in_einer_Zeile_nur_wenn_der_Server_sie_liefert()
    {
        var withRight = Section(new StubApi().On(SummaryRoute, Summary(finance: true)));
        await withRight.LoadAsync();
        Assert.True(withRight.HasOpen);
        Assert.Equal("Offen 45,00 € · Paket Massage: 7 von 10 offen", withRight.OpenLine);

        var without = Section(new StubApi().On(SummaryRoute, Summary()));
        await without.LoadAsync();
        Assert.False(without.HasOpen);
        Assert.Equal("", without.OpenLine);

        // Nur ein Paket, kein offener Betrag.
        var onlyPackage = Section(new StubApi().On(SummaryRoute, Summary() with { Open = new CustomerOpenItems(0, "EUR", [new GuestPackage(Guid.NewGuid(), "Massage", 3, 3, null)]) }));
        await onlyPackage.LoadAsync();
        Assert.Equal("Paket Massage: 3 von 3 offen", onlyPackage.OpenLine);
    }

    [Fact]
    public async Task Die_Kundennotiz_hat_nur_bei_langem_Text_einen_Hinweis_mit_dem_vollen_Wortlaut()
    {
        var shortNote = Section(new StubApi().On(SummaryRoute, Summary(note: "Kommt gern mit Hund")));
        await shortNote.LoadAsync();
        Assert.Equal("Kommt gern mit Hund", shortNote.Note);
        Assert.Null(shortNote.NoteTip);

        var longText = new string('a', 60) + " " + new string('b', 60);
        var longNote = Section(new StubApi().On(SummaryRoute, Summary(note: $"  {longText}  ")));
        await longNote.LoadAsync();
        Assert.Equal(longText, longNote.NoteTip);

        var blank = Section(new StubApi().On(SummaryRoute, Summary(note: "   ")));
        await blank.LoadAsync();
        Assert.False(blank.HasNote);
    }

    [Fact]
    public async Task Ein_Ladefehler_bleibt_im_Abschnitt_und_Erneut_laden_holt_den_Stand_nach()
    {
        var api = new StubApi().On(SummaryRoute, null, HttpStatusCode.InternalServerError);
        var section = Section(api);

        await section.LoadAsync();

        Assert.True(section.HasError);
        Assert.False(section.ShowStats);
        api.On(SummaryRoute, Summary());
        await section.LoadCommand.ExecuteAsync(null);
        Assert.False(section.HasError);
        Assert.True(section.ShowStats);
        Assert.Equal("12", section.VisitsText);
    }

    [Fact]
    public async Task Zuletzt_nennt_Datum_Behandlung_und_Person_und_ein_Klick_oeffnet_den_Termin()
    {
        var summary = Summary();
        var opened = new List<(Guid, DateTimeOffset)>();
        var section = Section(new StubApi().On(SummaryRoute, summary), opened: opened);
        await section.LoadAsync();

        Assert.Equal(["22.2.2027", "30.1.2027"], section.Recent.Select(r => r.DateText));
        Assert.Equal(["Massage", "Gesicht"], section.Recent.Select(r => r.Treatment));
        Assert.Equal(["Anna", "Ben"], section.Recent.Select(r => r.Person));
        Assert.Equal("22.2.2027, Massage bei Anna", section.Recent[0].Name);

        section.Recent[1].OpenCommand.Execute(null);
        Assert.Equal([(summary.Recent[1].AppointmentId, summary.Recent[1].Start)], opened);
    }

    // ---------- Kontaktlinks ----------

    [Fact]
    public async Task Kontaktzeilen_oeffnen_tel_whatsapp_und_mailto_und_kopieren_mit_dem_Toast_Kopiert()
    {
        var actions = new FakeActions();
        var toasts = new List<string>();
        var section = Section(new StubApi().On(SummaryRoute, Summary()), actions, toasts, guest: "Eva Muster");
        await section.LoadAsync();

        Assert.True(section.HasPhone);
        Assert.True(section.HasEmail);
        await section.CallCommand.ExecuteAsync(null);
        await section.WhatsAppCommand.ExecuteAsync(null);
        await section.MailCommand.ExecuteAsync(null);
        Assert.Equal(["tel:+436641234567", "https://wa.me/436641234567", "mailto:eva@example.at?subject=Eva%20Muster"], actions.Launched.Select(u => u.OriginalString));

        await section.CopyPhoneCommand.ExecuteAsync(null);
        await section.CopyEmailCommand.ExecuteAsync(null);
        Assert.Equal(["+43 664 123 45 67", "eva@example.at"], actions.Copied);
        Assert.Equal(["Kopiert", "Kopiert"], toasts);
        Assert.All(toasts, t => Assert.True(t.Length < 60 && !t.EndsWith('.')));
    }

    [Fact]
    public async Task Ohne_Telefonnummer_gibt_es_weder_Anrufen_noch_WhatsApp_und_ohne_Mail_kein_mailto()
    {
        var actions = new FakeActions();
        var section = Section(new StubApi().On(SummaryRoute, Summary(phone: null, email: null)), actions);
        await section.LoadAsync();

        Assert.False(section.HasPhone);
        Assert.False(section.HasEmail);
        Assert.True(section.HasNoContact);
        Assert.Null(section.PhoneUri);
        Assert.Null(section.WhatsAppUri);
        Assert.Null(section.MailUri);
        await section.CallCommand.ExecuteAsync(null);
        await section.WhatsAppCommand.ExecuteAsync(null);
        await section.MailCommand.ExecuteAsync(null);
        await section.CopyPhoneCommand.ExecuteAsync(null);
        Assert.Empty(actions.Launched);
        Assert.Empty(actions.Copied);
    }

    [Fact]
    public async Task Eine_nationale_Nummer_bekommt_die_Vorwahl_des_Standorts_fuer_WhatsApp()
    {
        var section = Section(new StubApi().On(SummaryRoute, Summary(phone: "0171 1234567")), country: FiscalCountry.DE);
        await section.LoadAsync();

        Assert.Equal("https://wa.me/491711234567", section.WhatsAppUri!.OriginalString);
        Assert.Equal("tel:01711234567", section.PhoneUri!.OriginalString);
    }

    [Fact]
    public async Task Kundenkarte_oeffnen_ruft_den_Weg_zur_Kundenkarte_und_fehlt_ohne_ihn()
    {
        var called = 0;
        var section = Section(new StubApi().On(SummaryRoute, Summary()), openCard: () =>
        {
            called++;
            return Task.CompletedTask;
        });
        Assert.True(section.CanOpenCard);
        await section.OpenCardCommand.ExecuteAsync(null);
        Assert.Equal(1, called);

        Assert.False(Section(new StubApi()).CanOpenCard);
    }

    // ---------- Im Termin-Panel ----------

    [Fact]
    public async Task Der_Abschnitt_laedt_erst_beim_ersten_Zeigen_und_ein_Gast_ohne_Namen_hat_keinen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { CustomerId = CustomerId };
        var api = new StubApi().On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment))
            .On(SummaryRoute, Summary());
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.OpenAppointment(appointment);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(api.Calls, c => c.Route == SummaryRoute);

        var section = vm.Selected!.CustomerSection!;
        await Until(() => section.ShowStats);
        Assert.Same(section, vm.Selected.CustomerSection);
        Assert.Single(api.Calls, c => c.Route == SummaryRoute);
        Assert.Equal("12", section.VisitsText);

        var placeholder = appointment with { Id = Guid.NewGuid(), PlaceholderGuest = 2 };
        Assert.Null(new AppointmentViewModel(api.Client(), placeholder, "Anna", "Raum Zeder", Vienna, true, true, _ => Task.CompletedTask, () => { }).CustomerSection);
    }

    [Fact]
    public async Task Zuletzt_oeffnet_einen_Termin_an_einem_anderen_Tag_und_zeigt_sein_Panel()
    {
        var today = TestData.Appointment(10, TestData.Anna) with { CustomerId = CustomerId };
        var past = TestData.Appointment(9, TestData.Ben, AppointmentStatus.Completed) with
        {
            CustomerId = CustomerId,
            Start = TestData.At(9).AddDays(-7),
            End = TestData.At(9, 50).AddDays(-7),
            BlockedFrom = TestData.At(9).AddDays(-7),
            BlockedUntil = TestData.At(10).AddDays(-7),
        };
        var lastWeek = TestData.Monday.AddDays(-7);
        var pastDay = TestData.Day(past) with { Date = lastWeek };
        var api = new StubApi().On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(today))
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date={lastWeek:yyyy-MM-dd}", pastDay);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        vm.OpenAppointmentOn(past.Id, past.Start);

        await Until(() => vm.Selected?.Appointment.Id == past.Id);
        Assert.Equal(lastWeek, vm.Date);
        Assert.Equal(past.Id, vm.MarkedId);

        // Ein Termin des gezeigten Tages öffnet gleich, ohne den Tag zu laden; Anzahlung, Karte und Erinnerungen lädt das Panel selbst.
        int DayLoads() => api.Calls.Count(c => c.Route.Contains("/calendar?", StringComparison.Ordinal));
        var calls = DayLoads();
        vm.OpenAppointmentOn(past.Id, past.Start);
        Assert.Equal(calls, DayLoads());
    }
}

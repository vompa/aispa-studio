using System.Net;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 117 (Ä274, E14): Reiter „Zuständigkeiten“, Ansicht „Zuständigkeiten“ im Bereich Team, Kennzeichen, Erinnerungen im Kalender und die Ersthelfer-Warnung im Dienstplan.
/// Fristen, Empfänger und Rechte rechnet die API (<c>StaffDutyTests</c> dort); hier zählt, was die App von ihr nimmt, schickt und sagt. Klickwege stehen in <c>ViewTests.Duties</c>.
/// </summary>
public sealed class DutiesTests
{
    private const string FirstAidText = "Keine Ersthelferin im Dienst am Dienstag, 2.3.";

    private static readonly string RemindersRoute = $"GET /api/staff-duties/reminders?locationId={TestData.LocationId}";
    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly EmployeeDto Marco = RosterSample.Marco;
    private static readonly DateOnly Tuesday = RosterSample.Monday.AddDays(1);
    private static readonly string DutiesPath = $"/api/employees/{Lena.Id}/duties";
    private static readonly string CertificatesPath = $"/api/employees/{Lena.Id}/certificates";
    private static readonly DutyTypeDto FirstAid = new(Guid.NewGuid(), DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs", 1, true);
    private static readonly DutyTypeDto Hygiene = new(Guid.NewGuid(), DutyTypeKey.Hygiene, "Hygiene", null, null, true);

    private static readonly StaffCertificateDto ExpiredCourse = new(
        Guid.NewGuid(), Lena.Id, "Erste-Hilfe-Kurs", new DateOnly(2024, 10, 3), new DateOnly(2026, 10, 3), "Rotes Kreuz", 30, true, true, true);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api(params StaffCertificateDto[] certificates)
    {
        var (api, _) = RosterTests.Setup();
        return api
            .On("GET /api/staff-duties/types", new List<DutyTypeDto> { FirstAid, Hygiene })
            .On($"GET {DutiesPath}", new List<StaffDutyDto> { new(Guid.NewGuid(), Lena.Id, FirstAid.Id, "Ersthelfer", new DateOnly(2026, 1, 1), null, Marco.Id, "Marco Berger", false) })
            .On($"GET {CertificatesPath}", certificates.ToList());
    }

    private static async Task<PersonDutiesViewModel> Open(StubApi api)
    {
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.DutiesTab, null);
        var tab = team.Card!.Duties!;
        await RosterTests.Until(() => tab.IsLoaded);
        return tab;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route == route).Body!, ApiClient.Json)!;

    // ---------- Kennzeichen ----------

    [Fact]
    public async Task Given_Lenas_Kurs_ist_abgelaufen_When_die_Teamliste_laedt_Then_steht_die_Warnung_vorn()
    {
        var api = Api().On("GET /api/staff-regulations/flags", new List<StaffFlagsDto>
        {
            new(Lena.Id, ["Spricht Italienisch"], [], [], [new("Hygiene", null), new("Ersthelfer", "Erste-Hilfe-Kurs abgelaufen am 3.10.")]),
        });

        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.DetailsTab, null);

        var row = team.Entries.Single(e => e.EmployeeId == Lena.Id);
        Assert.Equal("Warnung: Ersthelfer – Erste-Hilfe-Kurs abgelaufen am 3.10. · Hinweis · Zuständigkeit", row.FlagText);
        Assert.True(row.HasFlagWarning);
        Assert.Contains("Warnung: Ersthelfer", row.Name, StringComparison.Ordinal);
        Assert.Equal(["Warnung: Ersthelfer – Erste-Hilfe-Kurs abgelaufen am 3.10.", "Hinweis: Spricht Italienisch", "Zuständigkeit: Hygiene"], team.Card!.FlagLines);
        Assert.Equal([true, false, false], team.Card.FlagItems.Select(f => f.IsWarning));
    }

    // ---------- Reiter ----------

    [Fact]
    public async Task Given_eine_Rezeptionistin_When_sie_Lenas_Karte_oeffnet_Then_fehlen_Reiter_und_Ansicht()
    {
        var api = Api();
        var team = await RosterTests.Loaded(api, RosterTests.WithRights(StaffRole.Reception, AccessRight.ManageCatalog, AccessRight.ViewStaffRecords));

        team.Open(Lena.Id, PersonCardViewModel.DutiesTab, null);

        Assert.Null(team.Card!.Duties);
        Assert.DoesNotContain(team.Card.Tabs, t => t.Id == PersonCardViewModel.DutiesTab);
        Assert.DoesNotContain(team.Modes, m => m.Id == TeamViewModel.DutiesMode);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("duties", StringComparison.Ordinal) || c.Route.Contains("certificates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_der_Reiter_When_er_laedt_Then_stehen_Zustaendigkeit_mit_Vertretung_und_abgelaufener_Kurs_mit_Warnung()
    {
        var tab = await Open(Api(ExpiredCourse));

        var duty = Assert.Single(tab.Duties);
        Assert.Equal("Ersthelfer", duty.Title);
        Assert.Equal(["Gültig ab 1.1.2026", "Vertretung: Marco Berger"], duty.Lines);
        var certificate = Assert.Single(tab.Certificates);
        Assert.Equal("Erste-Hilfe-Kurs", certificate.Title);
        Assert.Equal("Abgelaufen am 3.10.2026", certificate.Warning);
        Assert.True(certificate.HasWarning);
        Assert.True(certificate.HasScan);
        Assert.Equal("Erste-Hilfe-Kurs, Warnung: Abgelaufen am 3.10.2026", certificate.AutomationName);
    }

    [Fact]
    public async Task Given_ein_neues_Zertifikat_When_gespeichert_wird_Then_geht_es_mit_allen_Angaben_an_die_API()
    {
        var api = Api().On($"POST {CertificatesPath}", ExpiredCourse);
        var tab = await Open(api);

        tab.AddCertificateCommand.Execute(null);
        Assert.Equal(["Erste-Hilfe-Kurs"], tab.CertificateSuggestions);
        Assert.Equal(60, tab.RemindDays);
        tab.CertificateName = " Erste-Hilfe-Kurs ";
        tab.IssuedOn = new DateOnly(2027, 2, 1);
        tab.ValidUntil = new DateOnly(2029, 2, 1);
        tab.IssuedBy = "Rotes Kreuz";
        Assert.True(tab.HasUnsavedChanges);
        await tab.SaveCommand.ExecuteAsync(null);

        var sent = Sent<StaffCertificateInput>(api, $"POST {CertificatesPath}");
        Assert.Equal(new StaffCertificateInput("Erste-Hilfe-Kurs", new DateOnly(2027, 2, 1), new DateOnly(2029, 2, 1), "Rotes Kreuz", 60), sent);
        Assert.False(tab.IsEditing);
        Assert.Equal("Gespeichert.", tab.StatusText);
    }

    [Fact]
    public async Task Given_ein_abgelaufener_Kurs_When_Erneuern_geklickt_wird_Then_uebernimmt_das_neue_Formular_Bezeichnung_Stelle_und_Frist()
    {
        var api = Api(ExpiredCourse).On($"POST {CertificatesPath}", ExpiredCourse);
        var tab = await Open(api);

        tab.Certificates[0].RenewCommand!.Execute(null);

        Assert.Equal("Zertifikat erneuern", tab.FormTitle);
        Assert.Equal("Erste-Hilfe-Kurs", tab.CertificateName);
        Assert.Equal("Rotes Kreuz", tab.IssuedBy);
        Assert.Equal(30, tab.RemindDays);
        Assert.Equal(Tuesday, tab.IssuedOn);
        Assert.Null(tab.ValidUntil);
        tab.ValidUntil = new DateOnly(2029, 3, 2);
        await tab.SaveCommand.ExecuteAsync(null);
        Assert.Contains(api.Calls, c => c.Route == $"POST {CertificatesPath}");
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith($"PUT {CertificatesPath}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eine_neue_Zustaendigkeit_When_gespeichert_wird_Then_gehen_Art_Zeitraum_und_Vertretung_an_die_API()
    {
        var api = Api().On($"POST {DutiesPath}", new StaffDutyDto(Guid.NewGuid(), Lena.Id, Hygiene.Id, "Hygiene", Tuesday, null, null, null, false));
        var tab = await Open(api);

        tab.AddDutyCommand.Execute(null);
        Assert.Equal(["Ersthelfer", "Hygiene"], tab.DutyTypes.Select(t => t.Label));
        Assert.Equal(["Keine Vertretung", "Marco Berger"], tab.Deputies.Select(d => d.Label));
        tab.SelectedDutyType = tab.DutyTypes[1];
        tab.SelectedDeputy = tab.Deputies[1];
        await tab.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new StaffDutyInput(Hygiene.Id, Tuesday, null, Marco.Id), Sent<StaffDutyInput>(api, $"POST {DutiesPath}"));
    }

    [Fact]
    public async Task Given_ein_offenes_Formular_mit_Aenderung_When_die_Karte_fragt_Then_zaehlt_es_als_ungespeichert()
    {
        var api = Api();
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.DutiesTab, null);
        var tab = team.Card!.Duties!;
        await RosterTests.Until(() => tab.IsLoaded);

        tab.AddCertificateCommand.Execute(null);
        tab.CertificateName = "Brandschutz";

        Assert.True(team.Card.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_PDF_When_Scan_hinzufuegen_geklickt_wird_Then_geht_es_als_PDF_in_Base64_an_die_API()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7 Kurs");
        var scanRoute = $"PUT {CertificatesPath}/{ExpiredCourse.Id}/scan";
        var api = Api(ExpiredCourse).On(scanRoute, null, HttpStatusCode.NoContent);
        var tab = await Open(api);
        tab.Picker = new FakeFilePicker { Bytes = pdf };

        await tab.Certificates[0].AddScanCommand!.ExecuteAsync(null);

        var sent = Sent<StaffCertificateScanInput>(api, scanRoute);
        Assert.Equal("application/pdf", sent.ContentType);
        Assert.Equal("Erste-Hilfe-Kurs.pdf", sent.FileName);
        Assert.Equal(pdf, Convert.FromBase64String(sent.Base64));
    }

    [Fact]
    public async Task Given_eine_Textdatei_When_Scan_hinzufuegen_geklickt_wird_Then_sagt_die_Maske_welche_Dateien_gehen()
    {
        var api = Api(ExpiredCourse);
        var tab = await Open(api);
        tab.Picker = new FakeFilePicker { Bytes = Encoding.ASCII.GetBytes("nur Text") };

        await tab.Certificates[0].AddScanCommand!.ExecuteAsync(null);

        Assert.Equal("Der Scan muss ein PDF, JPEG oder PNG sein. Bitte die Datei als PDF speichern oder fotografieren.", tab.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT", StringComparison.Ordinal));
    }

    // ---------- Ansicht „Zuständigkeiten“ ----------

    private static StubApi OverviewApi() => Api()
        .On("GET /api/staff-duties/overview", new List<DutyOverviewRowDto>
        {
            new(FirstAid.Id, DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs",
                [new(Guid.NewGuid(), Lena.Id, "Lena Huber", Marco.Id, "Marco Berger", new DateOnly(2026, 1, 1), null, false),
                 new(Guid.NewGuid(), Marco.Id, "Marco Berger", null, null, new DateOnly(2025, 1, 1), new DateOnly(2026, 6, 30), true)]),
            new(Hygiene.Id, DutyTypeKey.Hygiene, "Hygiene", null, []),
        })
        .On("GET /api/staff-duties/expiring", new List<ExpiringCertificateDto>
        {
            new(ExpiredCourse.Id, Lena.Id, "Lena Huber", "Erste-Hilfe-Kurs", new DateOnly(2026, 10, 3), true),
            new(Guid.NewGuid(), Marco.Id, "Marco Berger", "Brandschutz", new DateOnly(2027, 4, 30), false),
        });

    private static async Task<DutyOverviewViewModel> Overview(StubApi api)
    {
        var team = await RosterTests.Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.DutiesMode));
        var overview = team.Duties!;
        await RosterTests.Until(() => overview.IsLoaded);
        return overview;
    }

    [Fact]
    public async Task Given_die_Uebersicht_When_sie_geoeffnet_wird_Then_steht_je_Zustaendigkeit_Person_Vertretung_und_Gueltigkeit()
    {
        var overview = await Overview(OverviewApi());

        Assert.Equal(["Ersthelfer", "Hygiene"], overview.Rows.Select(r => r.Name));
        Assert.Equal("Verlangt: Erste-Hilfe-Kurs", overview.Rows[0].Certificate);
        Assert.Equal(["Lena Huber, Vertretung Marco Berger, gültig ab 1.1.2026", "Marco Berger, abgelaufen am 30.6.2026"], overview.Rows[0].Holders.Select(h => h.Text));
        Assert.Equal([false, true], overview.Rows[0].Holders.Select(h => h.IsExpired));
        Assert.True(overview.Rows[1].HasNoHolders);
        Assert.Equal(["Lena Huber: Erste-Hilfe-Kurs, abgelaufen am 3.10.2026", "Marco Berger: Brandschutz, läuft am 30.4.2027 ab"], overview.Expiring.Select(e => e.Text));
        Assert.Equal([true, false], overview.Expiring.Select(e => e.IsExpired));
    }

    [Fact]
    public async Task Given_die_Liste_When_die_Mindestzahl_auf_2_gesetzt_wird_Then_geht_sie_mit_dem_Ersthelfer_an_die_API()
    {
        var route = $"PUT /api/staff-duties/types/{FirstAid.Id}";
        var api = OverviewApi().On(route, FirstAid with { MinimumOnDuty = 2 });
        var overview = await Overview(api);

        var firstAid = overview.Types.Single(t => t.Name == "Ersthelfer");
        Assert.True(firstAid.ShowsMinimum);
        Assert.False(overview.Types.Single(t => t.Name == "Hygiene").ShowsMinimum);
        firstAid.MinimumOnDuty = 2;
        Assert.True(overview.HasUnsavedChanges);
        await overview.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DutyTypeInput("Ersthelfer", "Erste-Hilfe-Kurs", 2, true), Sent<DutyTypeInput>(api, route));
        Assert.DoesNotContain(api.Calls, c => c.Route == $"PUT /api/staff-duties/types/{Hygiene.Id}");
        Assert.False(overview.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_neuer_Eintrag_ohne_Namen_When_gespeichert_wird_Then_sagt_die_Zeile_was_fehlt()
    {
        var api = OverviewApi();
        var overview = await Overview(api);

        overview.AddTypeCommand.Execute(null);
        await overview.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Bitte einen Namen eingeben.", overview.Types[^1].NameError);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/staff-duties/types");
    }

    // ---------- Dienstplan (E14) ----------

    [Fact]
    public async Task Given_keine_Ersthelferin_When_die_Managerin_den_Tag_speichert_Then_warnt_die_Maske_und_speichert_erst_nach_Trotzdem()
    {
        var dayRoute = $"PUT /api/locations/{RosterSample.Vienna.Id}/roster/{Lena.Id}/{Tuesday:yyyy-MM-dd}";
        var api = Api().On(dayRoute, new BookingRejected(FirstAidText, [], StaffRule: true), HttpStatusCode.Conflict);
        var team = await RosterTests.Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        var plan = team.Plan!;
        await RosterTests.Until(() => plan.IsLoaded);
        var row = plan.Rows.Single(r => r.Name == "Lena Huber");
        await plan.OpenDayAsync(row, row.Cells[1]);
        var day = plan.Day!;
        day.Shifts[0].End = new TimeOnly(18, 0);

        Assert.False(await day.SaveChangesAsync());
        Assert.Equal(FirstAidText, day.RuleWarning);
        Assert.False(Sent<RosterDayInput>(api, dayRoute).RulesConfirmed);

        var saved = new RosterCell(Tuesday, RosterSource.Plan, [new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new TimeOnly(9, 0), new TimeOnly(18, 0))], null, [], false);
        api.On(dayRoute, new RosterDayResult(saved, []));
        await day.SaveAnywayCommand.ExecuteAsync(null);

        Assert.True(Sent<RosterDayInput>(api, dayRoute).RulesConfirmed);
        Assert.False(plan.HasUnsavedChanges);
    }

    // ---------- Kalender ----------

    private static StubApi CalendarApi(params DutyReminderDto[] reminders) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/appointment-holds", new List<AppointmentHoldDto>())
        .On(RemindersRoute, reminders.ToList());

    private static async Task<CalendarViewModel> Calendar(StubApi api, CurrentUser user, Action<CalendarViewModel>? wire = null)
    {
        var calendar = new CalendarViewModel(api.Client(), user, null, new FixedTime());
        wire?.Invoke(calendar);
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        return calendar;
    }

    [Fact]
    public async Task Given_eine_Erinnerung_der_API_When_der_Kalender_laedt_Then_steht_die_Meldung_mit_Weg_zur_Karte()
    {
        var api = CalendarApi(new DutyReminderDto(DutyReminderKind.CertificateExpiring, "Zertifikat läuft ab", "Lena Huber: Erste-Hilfe-Kurs läuft am 31.12. ab", Lena.Id, null, new DateOnly(2027, 12, 31)));
        Guid? opened = null;

        var calendar = await Calendar(api, TestData.Owner(), c => c.PersonDutiesRequested = id => opened = id);

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.Certificate);
        Assert.Equal("Zertifikat läuft ab", message.Title);
        Assert.Equal("Lena Huber: Erste-Hilfe-Kurs läuft am 31.12. ab", message.Text);
        Assert.Equal("Zertifikat", message.KindText);
        Assert.Equal(("zustaendigkeiten-zertifikate", "erinnerung"), message.Help);
        Assert.Equal("Zur Personenkarte", message.ActionLabel);
        message.ActionCommand!.Execute(null);
        Assert.Equal(Lena.Id, opened);
    }

    [Fact]
    public async Task Given_eine_eigene_Erinnerung_When_eine_Therapeutin_den_Kalender_laedt_Then_fehlt_der_Weg_zur_Karte()
    {
        var api = CalendarApi(new DutyReminderDto(DutyReminderKind.CertificateExpired, "Zertifikat abgelaufen", "Erste-Hilfe-Kurs ist am 3.10. abgelaufen", Lena.Id, null, new DateOnly(2026, 10, 3)));

        var calendar = await Calendar(api, RosterTests.WithRights(StaffRole.Therapist, AccessRight.ViewCalendar, AccessRight.Book), c => c.PersonDutiesRequested = _ => { });

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.Certificate);
        Assert.Equal(MessageSeverity.Warning, message.Severity);
        Assert.Null(message.ActionLabel);
    }

    [Fact]
    public async Task Given_eine_Lageraufgabe_When_Sophie_ohne_Lagerrecht_den_Kalender_oeffnet_Then_fehlt_der_Knopf_Zum_Lager()
    {
        var api = CalendarApi(new DutyReminderDto(DutyReminderKind.StockReorder, "Bestellvorschlag", "2 Materialien nachbestellen", null, TestData.LocationId, new DateOnly(2027, 3, 1)));

        var calendar = await Calendar(api, RosterTests.WithRights(StaffRole.Therapist, AccessRight.ViewCalendar), c => c.AdminRequested = (_, _, _) => { });

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.StockDuty);
        Assert.Equal("2 Materialien nachbestellen", message.Text);
        Assert.Equal("Lager", message.KindText);
        Assert.Null(message.ActionLabel);
    }

    [Fact]
    public async Task Given_eine_Lageraufgabe_When_jemand_mit_Lagerrecht_klickt_Then_oeffnet_sich_das_Lager()
    {
        var api = CalendarApi(new DutyReminderDto(DutyReminderKind.StockExpiry, "Ablaufdaten", "3 Chargen laufen bald ab", null, TestData.LocationId, new DateOnly(2027, 3, 1)));
        string? page = null;

        var calendar = await Calendar(api, TestData.Owner(), c => c.AdminRequested = (p, _, _) => page = p);

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.StockDuty);
        Assert.Equal("Zum Lager", message.ActionLabel);
        message.ActionCommand!.Execute(null);
        Assert.Equal("stock", page);
    }
}

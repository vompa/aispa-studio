using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// R05 Slice 117 (Ä274, E14): Zuständigkeiten und Zertifikate. Erinnerungen und Kennzeichen rechnet die API beim Lesen mit ihrer Uhr; die Tests stellen sie auf den 1.11.2027.
/// Lageraufgaben gehen an die Zuständigkeit Lager, sonst an die Leitung; der Dienstplan warnt bei zu wenigen Ersthelferinnen. Erwartete Texte und Tage stehen von Hand in den Tests.
/// </summary>
public sealed class StaffDutyTests(ApiFixture fixture)
{
    private static readonly DateOnly November1 = new(2027, 11, 1);

    private static readonly DateOnly NewYearsEve = new(2027, 12, 31);

    /// <summary>Samstag nach dem Testmontag; nach der Wochenarbeitszeit arbeitet dort niemand.</summary>
    private static readonly DateOnly Saturday = TestSpa.Monday.AddDays(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Eine API, deren Uhr am 1.11.2027 um 9 Uhr in Wien steht.</summary>
    private WebApplicationFactory<Program> OnNovember1() =>
        fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(TestSpa.At(9, date: November1)))));

    private static HttpClient As(WebApplicationFactory<Program> host, string cookie)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    /// <summary>Anmeldung an der API mit eigener Uhr: Ein Cookie der Test-API von heute wäre dort ein Jahr später abgelaufen.</summary>
    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, TestSpa spa, StaffRole role, Guid? employeeId = null, Guid[]? locations = null)
    {
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await anonymous.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration($"test-{Guid.NewGuid():N}@aispa.test", "Test Person", role, locations ?? [spa.Location.Id], employeeId), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return As(host, Cookies(response));
    }

    private static string Cookies(HttpResponseMessage response) => string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

    /// <summary>Anmeldung an der API mit eigener Uhr, danach der Wechsel in den Kanal der Mitarbeiter-App (wie am Handy).</summary>
    private static async Task<HttpClient> StaffAppAsync(WebApplicationFactory<Program> host, TestSpa spa, StaffRole role, Guid employeeId)
    {
        using var signedIn = await SignInAsync(host, spa, role, employeeId);
        var narrowed = await signedIn.PostAsync("/api/auth/channel/staff-app", null, Ct);
        narrowed.EnsureSuccessStatusCode();
        return As(host, Cookies(narrowed));
    }

    private static Task<LocationDto> SecondLocationAsync(TestSpa spa) =>
        spa.PostAsync<LocationDto>("/api/locations", new LocationInput("Graz", "Europe/Vienna", "EUR", []), Ct);

    private static Task<RosterDayResult> PutDayAsync(TestSpa spa, Guid employeeId, DateOnly date, RosterKind kind, bool draft = false) =>
        spa.PutAsync<RosterDayResult>(RosterPath(spa, employeeId, date), new RosterDayInput(
            [kind == RosterKind.Work ? new RosterEntry(RosterKind.Work, spa.Location.Id, new TimeOnly(9, 0), new TimeOnly(17, 0)) : new RosterEntry(kind, null, null, null)],
            null, Draft: draft, RulesConfirmed: true), Ct);

    private Task<string> CookieAsync(TestSpa spa, StaffRole role, Guid? employeeId = null) =>
        TestUsers.RegisterAsync(fixture, Ct, role, [spa.Location.Id], employeeId);

    private static async Task<Guid> TypeIdAsync(TestSpa spa, DutyTypeKey key) =>
        (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == key).Id;

    private static async Task<StaffDutyDto> AddDutyAsync(TestSpa spa, Guid employeeId, DutyTypeKey key, Guid? deputyId = null) =>
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{employeeId}/duties",
            new StaffDutyInput(await TypeIdAsync(spa, key), new DateOnly(2027, 1, 1), null, deputyId), Ct);

    private static Task<StaffCertificateDto> AddCourseAsync(TestSpa spa, Guid employeeId, DateOnly validUntil) =>
        spa.PostAsync<StaffCertificateDto>($"/api/employees/{employeeId}/certificates",
            new StaffCertificateInput("Erste-Hilfe-Kurs", validUntil.AddYears(-2), validUntil, "Rotes Kreuz"), Ct);

    private static async Task<List<DutyReminderDto>> RemindersAsync(HttpClient client, Guid? locationId = null) =>
        (await client.GetFromJsonAsync<List<DutyReminderDto>>($"/api/staff-duties/reminders{(locationId is { } id ? $"?locationId={id}" : "")}", TestSpa.Json, Ct))!;

    private static string RosterPath(TestSpa spa, Guid employeeId, DateOnly date) => $"/api/locations/{spa.Location.Id}/roster/{employeeId}/{date:yyyy-MM-dd}";

    private static RosterDayInput Work(TestSpa spa, int until = 17, bool confirmed = false) =>
        new([new RosterEntry(RosterKind.Work, spa.Location.Id, new TimeOnly(9, 0), new TimeOnly(until, 0))], null, RulesConfirmed: confirmed);

    private static StaffCertificateScanInput Scan(byte[] content) => new("kurs.pdf", "application/pdf", Convert.ToBase64String(content));

    private static Task<StockItemDto> ItemBelowMinimumAsync(TestSpa spa) =>
        spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput($"Tuchmaske {Guid.NewGuid():N}", "Stück", 2m, 10m, null), Ct);

    [Fact]
    public async Task Given_Kurs_bis_31_12_When_der_1_11_kommt_Then_erinnert_er_Inhaberin_und_Lena()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddCourseAsync(spa, lena.Id, NewYearsEve);
        await using var host = OnNovember1();
        using var ownerClient = await SignInAsync(host, spa, StaffRole.Owner);
        using var lenaClient = await SignInAsync(host, spa, StaffRole.Therapist, lena.Id);
        using var sophieClient = await SignInAsync(host, spa, StaffRole.Therapist, sophie.Id);

        var owner = await RemindersAsync(ownerClient);
        var own = await RemindersAsync(lenaClient);
        var colleague = await RemindersAsync(sophieClient);

        var leadership = Assert.Single(owner);
        Assert.Equal((DutyReminderKind.CertificateExpiring, "Lena: Erste-Hilfe-Kurs läuft am 31.12. ab", lena.Id, NewYearsEve),
            (leadership.Kind, leadership.Text, leadership.EmployeeId, leadership.Date));
        Assert.Equal("Erste-Hilfe-Kurs läuft am 31.12. ab", Assert.Single(own).Text);
        Assert.Empty(colleague);
    }

    [Fact]
    public async Task Given_Lena_ist_Ersthelferin_mit_abgelaufenem_Kurs_When_Kennzeichen_laden_Then_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, new DateOnly(2027, 10, 3));
        await AddDutyAsync(spa, spa.Ben.Id, DutyTypeKey.FirstAid);
        await using var host = OnNovember1();
        using var owner = await SignInAsync(host, spa, StaffRole.Owner);

        var flags = (await owner.GetFromJsonAsync<List<StaffFlagsDto>>("/api/staff-regulations/flags", TestSpa.Json, Ct))!;

        Assert.Equal([new DutyFlagDto("Ersthelfer", "Erste-Hilfe-Kurs abgelaufen am 3.10.")], flags.Single(f => f.EmployeeId == spa.Anna.Id).Duties!);
        Assert.Equal([new DutyFlagDto("Ersthelfer", "Erste-Hilfe-Kurs fehlt")], flags.Single(f => f.EmployeeId == spa.Ben.Id).Duties!);
    }

    [Fact]
    public async Task Given_Kurs_in_der_Erinnerungsfrist_When_Kennzeichen_laden_Then_steht_Kurs_bis_31_12()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);
        await using var host = OnNovember1();
        using var owner = await SignInAsync(host, spa, StaffRole.Owner);

        var flags = (await owner.GetFromJsonAsync<List<StaffFlagsDto>>("/api/staff-regulations/flags", TestSpa.Json, Ct))!;

        Assert.Equal([new DutyFlagDto("Ersthelfer", null), new DutyFlagDto("Erste-Hilfe-Kurs bis 31.12.", null)], Assert.Single(flags).Duties!);
    }

    [Fact]
    public async Task Given_Sophie_hat_Lager_When_Material_unter_Mindestmenge_Then_Aufgabe_an_Sophie_nicht_Leitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, sophie.Id, DutyTypeKey.Stock);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var sophieClient = await SignInAsync(host, spa, StaffRole.Therapist, sophie.Id);
        using var managerClient = await SignInAsync(host, spa, StaffRole.Manager);

        var forSophie = await RemindersAsync(sophieClient, spa.Location.Id);
        var forManager = await RemindersAsync(managerClient, spa.Location.Id);

        var task = Assert.Single(forSophie);
        Assert.Equal((DutyReminderKind.StockReorder, "1 Material nachbestellen", spa.Location.Id, November1), (task.Kind, task.Text, task.LocationId, task.Date));
        Assert.Empty(forManager);
    }

    [Fact]
    public async Task Given_niemand_hat_Lager_When_Material_unter_Mindestmenge_Then_Aufgabe_an_die_Leitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var sophieClient = await SignInAsync(host, spa, StaffRole.Therapist, sophie.Id);
        using var managerClient = await SignInAsync(host, spa, StaffRole.Manager);

        var forSophie = await RemindersAsync(sophieClient, spa.Location.Id);
        var forManager = await RemindersAsync(managerClient, spa.Location.Id);

        Assert.Empty(forSophie);
        Assert.Equal(DutyReminderKind.StockReorder, Assert.Single(forManager).Kind);
    }

    [Fact]
    public async Task Given_Sophie_ist_heute_im_Urlaub_When_die_Lageraufgabe_entsteht_Then_geht_sie_an_die_Vertretung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, sophie.Id, DutyTypeKey.Stock, deputyId: spa.Anna.Id);
        await spa.PutAsync<RosterDayResult>(RosterPath(spa, sophie.Id, November1), new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var sophieClient = await SignInAsync(host, spa, StaffRole.Therapist, sophie.Id);
        using var annaClient = await SignInAsync(host, spa, StaffRole.Therapist, spa.Anna.Id);

        var forSophie = await RemindersAsync(sophieClient, spa.Location.Id);
        var forDeputy = await RemindersAsync(annaClient, spa.Location.Id);

        Assert.Empty(forSophie);
        Assert.Equal(DutyReminderKind.StockReorder, Assert.Single(forDeputy).Kind);
    }

    [Fact]
    public async Task Given_die_Uebersicht_When_sie_geoeffnet_wird_Then_steht_je_Zustaendigkeit_Person_und_Vertretung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{lena.Id}/duties",
            new StaffDutyInput(await TypeIdAsync(spa, DutyTypeKey.FirstAid), new DateOnly(2027, 1, 1), NewYearsEve, spa.Ben.Id), Ct);
        await using var host = OnNovember1();
        using var owner = await SignInAsync(host, spa, StaffRole.Owner);

        var rows = (await owner.GetFromJsonAsync<List<DutyOverviewRowDto>>("/api/staff-duties/overview", TestSpa.Json, Ct))!;

        Assert.Equal(["Ersthelfer", "Sicherheitsvertrauensperson", "Lager", "Brandschutz", "Hygiene", "Kassa-Verantwortung", "Ausbildung"], rows.Select(r => r.Name));
        var holder = Assert.Single(rows[0].Holders);
        Assert.Equal(("Lena", "Ben", new DateOnly(2027, 1, 1), (DateOnly?)NewYearsEve, false),
            (holder.Name, holder.DeputyName, holder.ValidFrom, holder.ValidUntil, holder.IsExpired));
        Assert.Empty(rows.Single(r => r.Key == DutyTypeKey.Hygiene).Holders);
    }

    [Fact]
    public async Task Given_keine_Ersthelferin_am_Samstag_When_die_Managerin_plant_Then_Warnung_und_erst_bestaetigt_gespeichert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);
        using var manager = fixture.CreateClient(await CookieAsync(spa, StaffRole.Manager));

        var warned = await manager.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa), TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var confirmed = await manager.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa, confirmed: true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.Equal((true, "Keine Ersthelferin im Dienst am Samstag, 6.3."), (rejected!.StaffRule, rejected.Message));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Theory]
    [InlineData("2027-03-06", HttpStatusCode.OK)]
    [InlineData("2027-03-05", HttpStatusCode.Conflict)]
    public async Task Given_Ersthelferin_im_Dienst_When_ihr_Kurs_vor_dem_Tag_ablief_Then_zaehlt_sie_nicht(string validUntil, HttpStatusCode expected)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, DateOnly.Parse(validUntil, CultureInfo.InvariantCulture));

        var response = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Anna.Id, Saturday), Work(spa), TestSpa.Json, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Given_Mindestzahl_0_When_ohne_Ersthelferin_geplant_wird_Then_keine_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        var firstAid = (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == DutyTypeKey.FirstAid);
        await spa.PutAsync<DutyTypeDto>($"/api/staff-duties/types/{firstAid.Id}", new DutyTypeInput(firstAid.Name, firstAid.RequiredCertificate, 0), Ct);

        var response = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Given_Sonderregelung_und_keine_Ersthelferin_When_geplant_Then_eine_Warnung_mit_beiden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await spa.PostAsync<StaffRuleDto>($"/api/employees/{spa.Ben.Id}/regulations/rules",
            new StaffRuleInput(StaffRuleKind.NoAppointmentsAfter, TestSpa.Monday.AddDays(-30), NewYearsEve, new TimeOnly(16, 0)), Ct);

        var warned = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa, until: 18), TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var confirmed = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa, until: 18, confirmed: true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.Contains("keine Termine nach 16:00", rejected!.Message, StringComparison.Ordinal);
        Assert.Contains("Keine Ersthelferin im Dienst am Samstag, 6.3.", rejected.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Scan_When_gespeichert_Then_verschluesselt_und_das_Original_kommt_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var course = await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        const string Secret = "Bescheinigung Erste Hilfe fuer Ben";
        var content = Encoding.ASCII.GetBytes($"%PDF-1.4 {Secret}");
        var path = $"/api/employees/{spa.Ben.Id}/certificates/{course.Id}/scan";

        var uploaded = await spa.Client.PutAsJsonAsync(path, Scan(content), TestSpa.Json, Ct);
        var download = await spa.Client.GetAsync(path, Ct);
        var listed = await spa.GetAsync<List<StaffCertificateDto>>($"/api/employees/{spa.Ben.Id}/certificates", Ct);

        Assert.True(uploaded.IsSuccessStatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.True(Assert.Single(listed).HasScan);
        await using var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"""SELECT "Content" FROM hr."StaffCertificateScans" WHERE "CertificateId" = '{course.Id}'""", db);
        var stored = (byte[])(await command.ExecuteScalarAsync(Ct))!;
        Assert.DoesNotContain(Secret, Encoding.ASCII.GetString(stored), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_kein_Schluessel_When_ein_Scan_hochgeladen_wird_Then_503_und_Zertifikate_gehen_trotzdem()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("HealthData:CurrentKeyId", "fehlt"));
        using var client = As(host, spa.Cookie);

        var created = await client.PostAsJsonAsync($"/api/employees/{spa.Ben.Id}/certificates", new StaffCertificateInput("Erste-Hilfe-Kurs", ValidUntil: NewYearsEve), TestSpa.Json, Ct);
        var course = await TestSpa.ReadAsync<StaffCertificateDto>(created, Ct);
        var scan = await client.PutAsJsonAsync($"/api/employees/{spa.Ben.Id}/certificates/{course.Id}/scan", Scan(Encoding.ASCII.GetBytes("%PDF-1.4")), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, scan.StatusCode);
    }

    [Theory]
    [InlineData(5 * 1024 * 1024 + 1, "%PDF")]
    [InlineData(100, "GIF89a")]
    public async Task Given_zu_grosser_Scan_oder_falsches_Format_When_hochgeladen_Then_400(int size, string header)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var course = await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        var content = new byte[size];
        Encoding.ASCII.GetBytes(header).CopyTo(content, 0);

        var response = await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Ben.Id}/certificates/{course.Id}/scan", Scan(content), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(StaffRole.Reception)]
    [InlineData(StaffRole.Manager)]
    public async Task Given_keine_Leitung_When_sie_Zustaendigkeiten_liest_oder_pflegt_Then_403(StaffRole role)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var typeId = await TypeIdAsync(spa, DutyTypeKey.Hygiene);
        using var client = fixture.CreateClient(await CookieAsync(spa, role));

        HttpResponseMessage[] responses =
        [
            await client.PostAsJsonAsync($"/api/employees/{spa.Ben.Id}/duties", new StaffDutyInput(typeId, TestSpa.Monday), TestSpa.Json, Ct),
            await client.GetAsync($"/api/employees/{spa.Ben.Id}/certificates", Ct),
            await client.GetAsync("/api/staff-duties/overview", Ct),
            await client.GetAsync("/api/staff-duties/expiring", Ct),
            await client.GetAsync("/api/staff-duties/types", Ct),
        ];

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Given_Lena_in_der_Mitarbeiter_App_When_sie_Zustaendigkeiten_abruft_Then_nur_Erinnerungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        using var app = await ChannelTests.StaffAppAsync(spa, StaffRole.Owner, lena.Id, Ct);

        var reminders = await app.GetAsync($"/api/staff-duties/reminders?locationId={spa.Location.Id}", Ct);
        var own = await app.GetAsync($"/api/employees/{lena.Id}/certificates", Ct);
        var overview = await app.GetAsync("/api/staff-duties/overview", Ct);

        Assert.Equal(HttpStatusCode.OK, reminders.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, overview.StatusCode);
    }

    [Fact]
    public async Task Given_Zustaendigkeit_Zertifikat_und_Scan_When_Verlauf_geoeffnet_Then_nur_Art_ohne_Inhalt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var duty = await AddDutyAsync(spa, spa.Ben.Id, DutyTypeKey.FirstAid);
        await spa.PutAsync<StaffDutyDto>($"/api/employees/{spa.Ben.Id}/duties/{duty.Id}", new StaffDutyInput(duty.DutyTypeId, duty.ValidFrom, NewYearsEve), Ct);
        var course = await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        (await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Ben.Id}/certificates/{course.Id}/scan", Scan(Encoding.ASCII.GetBytes("%PDF-1.4")), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode();
        (await spa.Client.DeleteAsync(new Uri($"/api/employees/{spa.Ben.Id}/certificates/{course.Id}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/employees/{spa.Ben.Id}/duties/history", Ct);
        var hrFile = await spa.GetAsync<List<HistoryEntry>>($"/api/employees/{spa.Ben.Id}/hr-file/history", Ct);
        var regulations = await spa.GetAsync<List<HistoryEntry>>($"/api/employees/{spa.Ben.Id}/regulations/history", Ct);

        Assert.Contains(history, h => h.Text.StartsWith("Zuständigkeit geändert", StringComparison.Ordinal));
        Assert.Contains(history, h => h.Text.StartsWith("Scan angelegt", StringComparison.Ordinal));
        Assert.Contains(history, h => h.Text.StartsWith("Zertifikat gelöscht", StringComparison.Ordinal));
        Assert.DoesNotContain(history, h => h.Text.Contains("Erste-Hilfe", StringComparison.Ordinal) || h.Text.Contains("Rotes Kreuz", StringComparison.Ordinal));
        Assert.Empty(hrFile);
        Assert.Empty(regulations);
    }

    [Fact]
    public async Task Given_zwei_gleichzeitige_erste_Abrufe_When_die_Liste_geladen_wird_Then_gibt_es_die_Vorgaben_einmal()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var lists = await Task.WhenAll(
            spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct), spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct));
        var again = await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct);

        Assert.All(lists, l => Assert.Equal(7, l.Count));
        Assert.Equal(
            [DutyTypeKey.FirstAid, DutyTypeKey.SafetyRep, DutyTypeKey.Stock, DutyTypeKey.FireSafety, DutyTypeKey.Hygiene, DutyTypeKey.CashResponsibility, DutyTypeKey.Training],
            again.Select(t => t.Key!.Value));
        Assert.Equal(("Erste-Hilfe-Kurs", (int?)1), (again[0].RequiredCertificate, again[0].MinimumOnDuty));
    }

    [Fact]
    public async Task Given_eine_ausgeschiedene_Person_When_ein_Zertifikat_angelegt_wird_Then_409_und_lesen_geht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        (await spa.Client.DeleteAsync(new Uri($"/api/employees/{spa.Ben.Id}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();

        var response = await spa.Client.PostAsJsonAsync($"/api/employees/{spa.Ben.Id}/certificates", new StaffCertificateInput("Hygieneschulung"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await spa.GetAsync<List<StaffCertificateDto>>($"/api/employees/{spa.Ben.Id}/certificates", Ct));
    }

    [Fact]
    public async Task Given_ungueltige_Eingaben_When_gespeichert_Then_antwortet_die_API_400()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var typeId = await TypeIdAsync(spa, DutyTypeKey.Hygiene);
        var certificates = $"/api/employees/{spa.Ben.Id}/certificates";
        var duties = $"/api/employees/{spa.Ben.Id}/duties";

        HttpResponseMessage[] responses =
        [
            await spa.Client.PostAsJsonAsync(certificates, new StaffCertificateInput("  "), TestSpa.Json, Ct),
            await spa.Client.PostAsJsonAsync(certificates, new StaffCertificateInput("Kurs", RemindDays: 0), TestSpa.Json, Ct),
            await spa.Client.PostAsJsonAsync(duties, new StaffDutyInput(typeId, TestSpa.Monday, DeputyId: spa.Ben.Id), TestSpa.Json, Ct),
            await spa.Client.PostAsJsonAsync(duties, new StaffDutyInput(Guid.NewGuid(), TestSpa.Monday), TestSpa.Json, Ct),
            await spa.Client.PostAsJsonAsync(duties, new StaffDutyInput(typeId, TestSpa.Monday, TestSpa.Monday.AddDays(-1)), TestSpa.Json, Ct),
        ];

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    // Durchsicht Slice 117: Kanal Mitarbeiter-App, Ersthelfer im Dienstplan, Lageraufgaben bei mehreren Standorten, Übersicht, Rand der Erinnerung, ausgeblendete Arten.
    [Fact]
    public async Task Given_Inhaberin_in_der_Mitarbeiter_App_When_Erinnerungen_laden_Then_nur_eigene_Zertifikate_ohne_Lageraufgaben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddCourseAsync(spa, lena.Id, NewYearsEve);
        await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var app = await StaffAppAsync(host, spa, StaffRole.Owner, lena.Id);

        var reminders = await RemindersAsync(app, spa.Location.Id);

        var own = Assert.Single(reminders);
        Assert.Equal((DutyReminderKind.CertificateExpiring, "Erste-Hilfe-Kurs läuft am 31.12. ab", (Guid?)lena.Id), (own.Kind, own.Text, own.EmployeeId));
    }

    [Fact]
    public async Task Given_kein_Eintrag_Ersthelfer_When_ohne_Ersthelferin_geplant_wird_Then_keine_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);

        var response = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_eine_andere_Ersthelferin_im_Dienst_When_eine_zweite_Person_geplant_wird_Then_keine_Warnung(bool draft)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);
        await PutDayAsync(spa, spa.Anna.Id, Saturday, RosterKind.Work, draft);

        var response = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Saturday), Work(spa), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Given_die_Ersthelferin_ist_im_Urlaub_When_eine_zweite_Person_geplant_wird_Then_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);
        await PutDayAsync(spa, spa.Anna.Id, TestSpa.Monday, RosterKind.Vacation);

        var warned = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, TestSpa.Monday), Work(spa), TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.Equal("Keine Ersthelferin im Dienst am Montag, 1.3.", rejected!.Message);
    }

    [Fact]
    public async Task Given_die_Ersthelferin_arbeitete_am_Standort_When_sie_krank_gemeldet_wird_Then_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddDutyAsync(spa, spa.Anna.Id, DutyTypeKey.FirstAid);
        await AddCourseAsync(spa, spa.Anna.Id, NewYearsEve);

        var warned = await spa.Client.PutAsJsonAsync(RosterPath(spa, spa.Anna.Id, TestSpa.Monday),
            new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null), TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.Equal("Keine Ersthelferin im Dienst am Montag, 1.3.", rejected!.Message);
    }

    [Fact]
    public async Task Given_Sophie_hat_Lager_und_sieht_Graz_nicht_When_in_Graz_Material_fehlt_Then_Aufgabe_an_die_Leitung_in_Graz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await SecondLocationAsync(spa);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, sophie.Id, DutyTypeKey.Stock);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var sophieClient = await SignInAsync(host, spa, StaffRole.Therapist, sophie.Id);
        using var grazManager = await SignInAsync(host, spa, StaffRole.Manager, locations: [graz.Id]);

        var forSophie = await RemindersAsync(sophieClient, spa.Location.Id);
        var forGraz = await RemindersAsync(grazManager, graz.Id);

        Assert.Equal(DutyReminderKind.StockReorder, Assert.Single(forSophie).Kind);
        var task = Assert.Single(forGraz);
        Assert.Equal((DutyReminderKind.StockReorder, (Guid?)graz.Id), (task.Kind, task.LocationId));
    }

    [Fact]
    public async Task Given_die_Vertretung_ist_ausgeschieden_When_Sophie_im_Urlaub_ist_Then_Aufgabe_an_die_Leitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        var jana = await spa.CreateEmployeeAsync("Jana", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, sophie.Id, DutyTypeKey.Stock, deputyId: jana.Id);
        await spa.PutAsync<RosterDayResult>(RosterPath(spa, sophie.Id, November1), new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);
        await ItemBelowMinimumAsync(spa);
        await using var host = OnNovember1();
        using var janaClient = await SignInAsync(host, spa, StaffRole.Therapist, jana.Id);
        (await spa.Client.DeleteAsync(new Uri($"/api/employees/{jana.Id}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();
        using var managerClient = await SignInAsync(host, spa, StaffRole.Manager);

        var forManager = await RemindersAsync(managerClient, spa.Location.Id);

        Assert.Equal(DutyReminderKind.StockReorder, Assert.Single(forManager).Kind);
    }

    [Fact]
    public async Task Given_ausgeschiedene_Inhaber_und_Vertretung_When_die_Uebersicht_geoeffnet_wird_Then_fehlen_sie()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var jana = await spa.CreateEmployeeAsync("Jana", [spa.Massage.Id], spa.Location.Id, Ct);
        await AddDutyAsync(spa, lena.Id, DutyTypeKey.FirstAid, deputyId: jana.Id);
        await AddDutyAsync(spa, spa.Ben.Id, DutyTypeKey.FirstAid);
        foreach (var gone in new[] { jana.Id, spa.Ben.Id })
        {
            (await spa.Client.DeleteAsync(new Uri($"/api/employees/{gone}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();
        }

        var rows = await spa.GetAsync<List<DutyOverviewRowDto>>("/api/staff-duties/overview", Ct);

        var holder = Assert.Single(rows.Single(r => r.Key == DutyTypeKey.FirstAid).Holders);
        Assert.Equal(("Lena", (Guid?)null, (string?)null), (holder.Name, holder.DeputyId, holder.DeputyName));
    }

    [Theory]
    [InlineData("2027-10-31T22:30:00Z", 0)]
    [InlineData("2027-10-31T23:30:00Z", 1)]
    public async Task Given_Kurs_bis_31_12_When_die_Uhr_am_Rand_des_1_11_in_Wien_steht_Then_erinnert_er_erst_ab_Mitternacht(string utc, int expected)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddCourseAsync(spa, spa.Ben.Id, NewYearsEve);
        var now = DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture);
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(now))));
        using var owner = await SignInAsync(host, spa, StaffRole.Owner);

        var reminders = await RemindersAsync(owner);

        Assert.Equal(expected, reminders.Count);
    }

    [Fact]
    public async Task Given_eine_ausgeblendete_Art_When_eine_Zustaendigkeit_angelegt_oder_umgestellt_wird_Then_400()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var duty = await AddDutyAsync(spa, spa.Ben.Id, DutyTypeKey.FirstAid);
        var hygiene = (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == DutyTypeKey.Hygiene);
        await spa.PutAsync<DutyTypeDto>($"/api/staff-duties/types/{hygiene.Id}", new DutyTypeInput(hygiene.Name, IsActive: false), Ct);

        var created = await spa.Client.PostAsJsonAsync($"/api/employees/{spa.Ben.Id}/duties", new StaffDutyInput(hygiene.Id, TestSpa.Monday), TestSpa.Json, Ct);
        var changed = await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Ben.Id}/duties/{duty.Id}", new StaffDutyInput(hygiene.Id, duty.ValidFrom), TestSpa.Json, Ct);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (created.StatusCode, changed.StatusCode));
    }

    [Fact]
    public async Task Given_eine_ausgeblendete_Art_When_eine_bestehende_Zustaendigkeit_beendet_wird_Then_geht_das()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var duty = await AddDutyAsync(spa, spa.Ben.Id, DutyTypeKey.Hygiene);
        var hygiene = (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == DutyTypeKey.Hygiene);
        await spa.PutAsync<DutyTypeDto>($"/api/staff-duties/types/{hygiene.Id}", new DutyTypeInput(hygiene.Name, IsActive: false), Ct);

        var ended = await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Ben.Id}/duties/{duty.Id}", new StaffDutyInput(hygiene.Id, duty.ValidFrom, NewYearsEve), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
    }
}

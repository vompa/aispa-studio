using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// R05 Slice 111 (Ä252, E10, E11): Hinweise, Sonderregelungen und Vollmachten. Eine Sonderregelung warnt und sperrt nie; Kassa-Storno und Bestellungen auslösen prüft die API
/// zusätzlich zur Rolle, sobald das Spa eine Vollmacht dieser Art erteilt hat. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class StaffRegulationTests(ApiFixture fixture)
{
    /// <summary>Der Dienstag nach dem Testmontag.</summary>
    private static readonly DateOnly Tuesday = TestSpa.Monday.AddDays(1);

    private static readonly DateOnly NewYearsEve = new(2027, 12, 31);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static string Regulations(Guid employeeId) => $"/api/employees/{employeeId}/regulations";

    private static StaffRuleInput NotAfterFour() => new(StaffRuleKind.NoAppointmentsAfter, TestSpa.Monday.AddDays(-30), NewYearsEve, new TimeOnly(16, 0), Reason: "Kinderbetreuung");

    private static RosterDayInput WorkUntil(TestSpa spa, int hour, bool confirmed = false) =>
        new([new RosterEntry(RosterKind.Work, spa.Location.Id, new TimeOnly(9, 0), new TimeOnly(hour, 0))], null, RulesConfirmed: confirmed);

    private static string RosterPath(TestSpa spa, Guid employeeId, DateOnly date) => $"/api/locations/{spa.Location.Id}/roster/{employeeId}/{date:yyyy-MM-dd}";

    private async Task<HttpClient> StaffAsync(TestSpa spa, StaffRole role, Guid? employeeId = null)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@aispa.test";
        using var client = fixture.CreateClient(spa.Cookie);
        var response = await client.PostAsJsonAsync("/api/auth/dev/register", new DevRegistration(email, role.ToString(), role, [spa.Location.Id]), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct);
        var staff = fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        if (employeeId is not null)
        {
            await spa.PutAsync<StaffMemberDto>($"/api/staff/{user!.Id}", new StaffAccessInput(role, false, [spa.Location.Id], employeeId, true), Ct);
        }

        return staff;
    }

    private static async Task<ReceiptDto> SellAsync(TestSpa spa)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"V-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
        return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 100m, null, null)]), Ct);
    }

    [Fact]
    public async Task Given_Lena_hat_keine_Termine_nach_16_Uhr_bis_31_12_When_die_Managerin_Dienst_bis_18_Uhr_eintraegt_Then_warnt_die_API_und_speichert_erst_nach_Bestaetigung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffRuleDto>($"{Regulations(spa.Ben.Id)}/rules", NotAfterFour(), Ct);
        using var manager = await StaffAsync(spa, StaffRole.GeneralManager);

        var warned = await manager.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Tuesday), WorkUntil(spa, 18), TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var confirmed = await manager.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Tuesday), WorkUntil(spa, 18, confirmed: true), TestSpa.Json, Ct);
        var inside = await manager.PutAsJsonAsync(RosterPath(spa, spa.Ben.Id, Tuesday.AddDays(7)), WorkUntil(spa, 16), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.True(rejected!.StaffRule);
        Assert.Contains("keine Termine nach 16:00", rejected.Message, StringComparison.Ordinal);
        Assert.Contains("31.12.2027", rejected.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Kinderbetreuung", rejected.Message, StringComparison.Ordinal);
        Assert.True(confirmed.IsSuccessStatusCode);
        Assert.True(inside.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_Lena_hat_keine_Termine_nach_16_Uhr_When_die_Rezeption_um_17_Uhr_bucht_Then_warnt_die_API_und_bucht_erst_nach_Trotzdem_buchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffRuleDto>($"{Regulations(spa.Ben.Id)}/rules", NotAfterFour(), Ct);
        await spa.PutAsync<RosterDayResult>(RosterPath(spa, spa.Ben.Id, TestSpa.Monday), WorkUntil(spa, 20, confirmed: true), Ct);
        using var reception = await StaffAsync(spa, StaffRole.Reception);
        var request = new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(17), spa.Ben.Id, null, null, null);

        var warned = await reception.PostAsJsonAsync("/api/appointments", request, TestSpa.Json, Ct);
        var rejected = await warned.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var booked = await reception.PostAsJsonAsync("/api/appointments", request with { RulesConfirmed = true }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.True(rejected!.StaffRule);
        Assert.Contains("keine Termine nach 16:00", rejected.Message, StringComparison.Ordinal);
        Assert.True(booked.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_Termin_am_Vormittag_When_er_auf_17_Uhr_verschoben_wird_Then_zieht_er_um_und_die_Antwort_nennt_die_Regelung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffRuleDto>($"{Regulations(spa.Ben.Id)}/rules", NotAfterFour(), Ct);
        await spa.PutAsync<RosterDayResult>(RosterPath(spa, spa.Ben.Id, TestSpa.Monday), WorkUntil(spa, 20, confirmed: true), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id);

        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move", new MoveAppointmentRequest(TestSpa.At(17), spa.Ben.Id, null, null), Ct);

        Assert.Equal(TestSpa.At(17), moved.Start);
        Assert.Contains(moved.Warnings!, w => w.Contains("keine Termine nach 16:00", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eine_Regelung_oder_Vollmacht_ohne_Gueltig_von_When_sie_angelegt_wird_Then_lehnt_die_API_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var rule = await spa.Client.PostAsJsonAsync($"{Regulations(spa.Ben.Id)}/rules", new { kind = "ProtectionPeriod" }, TestSpa.Json, Ct);
        var power = await spa.Client.PostAsJsonAsync($"{Regulations(spa.Ben.Id)}/powers", new { kind = "Keys" }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rule.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, power.StatusCode);
    }

    [Fact]
    public async Task Given_Sophie_hat_Kassa_Storno_bis_gestern_When_sie_heute_storniert_Then_lehnt_die_API_ab_und_nennt_die_abgelaufene_Vollmacht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var yesterday = Today.AddDays(-1);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.CashVoid, Today.AddDays(-30), yesterday), Ct);
        var receipt = await SellAsync(spa);
        using var sophie = await StaffAsync(spa, StaffRole.Manager, spa.Ben.Id);

        var response = await sophie.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Kassa-Storno", text, StringComparison.Ordinal);
        Assert.Contains("abgelaufen", text, StringComparison.Ordinal);
        Assert.Contains(yesterday.ToString("d.M.yyyy", CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Sophie_hat_eine_gueltige_Vollmacht_When_sie_storniert_Then_geht_es_und_eine_Kollegin_ohne_Vollmacht_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.CashVoid, Today, Today), Ct);
        var receipt = await SellAsync(spa);
        using var sophie = await StaffAsync(spa, StaffRole.Manager, spa.Ben.Id);
        using var colleague = await StaffAsync(spa, StaffRole.Manager, spa.Anna.Id);

        var denied = await colleague.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);
        var allowed = await sophie.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Kassa-Storno", await denied.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.True(allowed.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_das_Spa_hat_keine_Vollmacht_erteilt_When_die_Standortleitung_storniert_Then_gilt_nur_die_Rolle_wie_bisher()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellAsync(spa);
        using var manager = await StaffAsync(spa, StaffRole.Manager);

        var response = await manager.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_die_Inhaberin_hat_selbst_keine_Vollmacht_When_sie_storniert_Then_geht_es()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.CashVoid, Today), Ct);
        var receipt = await SellAsync(spa);

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), TestSpa.Json, Ct);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_Vollmacht_Bestellungen_ausloesen_bis_gestern_When_die_Standortleitung_die_Bestellung_ausloest_Then_lehnt_die_API_ab_und_nennt_sie()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.TriggerOrders, Today.AddDays(-30), Today.AddDays(-1)), Ct);
        using var manager = await StaffAsync(spa, StaffRole.Manager, spa.Ben.Id);
        var order = Guid.NewGuid();

        var sent = await manager.PostAsJsonAsync($"/api/stock/orders/{order}/send", new { }, TestSpa.Json, Ct);
        var ordered = await manager.PostAsJsonAsync($"/api/stock/orders/{order}/ordered", new { }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
        Assert.Contains("Bestellungen auslösen", await sent.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, ordered.StatusCode);
    }

    [Fact]
    public async Task Given_die_Uebersicht_Wer_darf_was_When_die_Inhaberin_sie_oeffnet_Then_steht_je_Vollmacht_wer_sie_hat_und_bis_wann()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.Safe, Today, NewYearsEve), Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Anna.Id)}/powers", new StaffPowerInput(StaffPowerKind.Safe, Today.AddDays(-9), Today.AddDays(-1)), Ct);

        var rows = await spa.GetAsync<List<PowerOverviewRowDto>>("/api/staff-regulations/powers", Ct);

        Assert.Equal(
            ["Kassa-Storno", "Kassa-Abschluss", "Tresor", "Bankeinzahlung", "Schlüssel", "Lieferungen annehmen", "Bestellungen auslösen", "Vertretung der Leitung"],
            rows.Select(r => r.Label));
        Assert.Equal([StaffPowerKind.CashVoid, StaffPowerKind.TriggerOrders], rows.Where(r => r.IsChecked).Select(r => r.Kind));
        var safe = rows.Single(r => r.Kind == StaffPowerKind.Safe);
        var ben = safe.Holders.Single(h => h.EmployeeId == spa.Ben.Id);
        var anna = safe.Holders.Single(h => h.EmployeeId == spa.Anna.Id);
        Assert.Equal((NewYearsEve, false), (ben.ValidUntil, ben.IsExpired));
        Assert.True(anna.IsExpired);
        Assert.False(string.IsNullOrWhiteSpace(ben.GrantedBy));
    }

    [Theory]
    [InlineData(StaffRole.Manager)]
    [InlineData(StaffRole.Reception)]
    public async Task Given_keine_Leitung_When_sie_Regelungen_liest_oder_pflegt_Then_antwortet_die_API_403(StaffRole role)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await StaffAsync(spa, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(new Uri(Regulations(spa.Ben.Id), UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(new Uri("/api/staff-regulations/powers", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_Hinweise_mit_Symbol_When_der_Kalender_sie_holt_Then_sieht_das_Team_nur_Hinweise_fuer_das_Team_und_die_Leitung_alle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"{Regulations(spa.Ben.Id)}/notes";
        await spa.PostAsync<StaffNoteDto>(path, new StaffNoteInput("Rücken schonen, keine Hot Stone", StaffNoteVisibility.Team, true), Ct);
        await spa.PostAsync<StaffNoteDto>(path, new StaffNoteInput("Vertraulich", StaffNoteVisibility.Leadership, true), Ct);
        await spa.PostAsync<StaffNoteDto>(path, new StaffNoteInput("Ohne Symbol", StaffNoteVisibility.Team), Ct);
        await spa.PostAsync<StaffNoteDto>(path, new StaffNoteInput("Abgelaufen", StaffNoteVisibility.Team, true, null, TestSpa.Monday.AddDays(-1)), Ct);
        using var reception = await StaffAsync(spa, StaffRole.Reception);
        var url = $"/api/employee-notes/symbols?on={TestSpa.Monday:yyyy-MM-dd}";

        var team = await reception.GetFromJsonAsync<List<StaffNoteSymbolDto>>(url, TestSpa.Json, Ct);
        var leadership = await spa.GetAsync<List<StaffNoteSymbolDto>>(url, Ct);

        Assert.Equal(new StaffNoteSymbolDto(spa.Ben.Id, "Rücken schonen, keine Hot Stone"), Assert.Single(team!));
        Assert.Equal(["Rücken schonen, keine Hot Stone", "Vertraulich"], leadership.Select(s => s.Text).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_Eintraege_einer_Person_When_die_Karte_laedt_Then_stehen_Kennzeichen_nur_fuer_gueltige_und_abgelaufene_sind_gekennzeichnet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = Regulations(spa.Ben.Id);
        await spa.PostAsync<StaffNoteDto>($"{path}/notes", new StaffNoteInput("Rücken schonen", StaffNoteVisibility.Team), Ct);
        await spa.PostAsync<StaffRuleDto>($"{path}/rules", new StaffRuleInput(StaffRuleKind.NoAppointmentsAfter, Today, NewYearsEve, new TimeOnly(16, 0)), Ct);
        await spa.PostAsync<StaffRuleDto>($"{path}/rules", new StaffRuleInput(StaffRuleKind.ProtectionPeriod, Today.AddDays(-9), Today.AddDays(-1)), Ct);
        await spa.PostAsync<StaffPowerDto>($"{path}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today), Ct);

        var flags = await spa.GetAsync<List<StaffFlagsDto>>("/api/staff-regulations/flags", Ct);
        var all = await spa.GetAsync<StaffRegulationsDto>(path, Ct);

        var ben = Assert.Single(flags);
        Assert.Equal(spa.Ben.Id, ben.EmployeeId);
        Assert.Equal(["Rücken schonen"], ben.Notes);
        Assert.Equal(["keine Termine nach 16:00"], ben.Rules);
        Assert.Equal(["Schlüssel"], ben.Powers);
        Assert.Equal([false, true], all.Rules.OrderBy(r => r.Kind).Select(r => r.IsExpired));
    }

    [Fact]
    public async Task Given_ungueltige_Eingaben_When_gespeichert_Then_antwortet_die_API_400()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = Regulations(spa.Ben.Id);

        var noTime = await spa.Client.PostAsJsonAsync($"{path}/rules", new StaffRuleInput(StaffRuleKind.NoAppointmentsAfter, Today), TestSpa.Json, Ct);
        var noHours = await spa.Client.PostAsJsonAsync($"{path}/rules", new StaffRuleInput(StaffRuleKind.MaxHoursPerDay, Today, MaxHours: 0m), TestSpa.Json, Ct);
        var noDays = await spa.Client.PostAsJsonAsync($"{path}/rules", new StaffRuleInput(StaffRuleKind.FixedDaysOff, Today), TestSpa.Json, Ct);
        var backwards = await spa.Client.PostAsJsonAsync($"{path}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today, Today.AddDays(-1)), TestSpa.Json, Ct);
        var noText = await spa.Client.PostAsJsonAsync($"{path}/notes", new StaffNoteInput("  ", StaffNoteVisibility.Team), TestSpa.Json, Ct);

        Assert.All([noTime, noHours, noDays, backwards, noText], r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Fact]
    public async Task Given_eine_Vollmacht_geaendert_und_geloescht_When_der_Verlauf_geoeffnet_wird_Then_steht_die_Art_ohne_Inhalt_und_die_Personalakte_bleibt_aussen_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = Regulations(spa.Ben.Id);
        var power = await spa.PostAsync<StaffPowerDto>($"{path}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today), Ct);
        await spa.PutAsync<StaffPowerDto>($"{path}/powers/{power.Id}", new StaffPowerInput(StaffPowerKind.Keys, Today, NewYearsEve), Ct);
        await spa.PostAsync<StaffNoteDto>($"{path}/notes", new StaffNoteInput("Geheim im Verlauf", StaffNoteVisibility.Leadership), Ct);
        (await spa.Client.DeleteAsync(new Uri($"{path}/powers/{power.Id}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();

        var history = await spa.GetAsync<List<HistoryEntry>>($"{path}/history", Ct);
        var hrFile = await spa.GetAsync<List<HistoryEntry>>($"/api/employees/{spa.Ben.Id}/hr-file/history", Ct);

        Assert.Contains(history, h => h.Text.StartsWith("Vollmacht geändert", StringComparison.Ordinal));
        Assert.Contains(history, h => h.Text.StartsWith("Vollmacht gelöscht", StringComparison.Ordinal));
        Assert.Contains(history, h => h.Text.StartsWith("Hinweis angelegt", StringComparison.Ordinal));
        Assert.DoesNotContain(history, h => h.Text.Contains("Geheim", StringComparison.Ordinal));
        Assert.Empty(hrFile);
    }
}

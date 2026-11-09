using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Auswertungen im Assistenten (ADR 0072, Szenario 5): Top-Behandlungen, Top-Gäste, meiste Termine, Auslastung je Person und Raum, Umsatz je Behandlung,
/// Nicht-erschienen. Alle Zahlen kommen vom Berichtsdienst (dieselben wie im Management-Cockpit), der Zeitraum steht in der Antwort, Beträge gibt es nur mit dem Recht
/// „Berichte mit Beträgen“. Die Daten liegen an zwei Montagen vor „heute“, damit Termine und Belege im selben Zeitraum liegen.
/// </summary>
public sealed class BusinessReportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private readonly AnswerKit _kit = new(fixture);

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    /// <summary>Der letzte Montag vor heute und der davor.</summary>
    private static (DateOnly Older, DateOnly Newer) Mondays()
    {
        var newer = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        newer = newer == Today ? newer.AddDays(-7) : newer;
        return (newer.AddDays(-7), newer);
    }

    private static string Since(DateOnly day) => $"seit {day.AddDays(-1).ToString("d.M.yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Ein Spa mit zwei Behandlungen, zwei Personen und Terminen an zwei Montagen. Älterer Montag: Anna (Massage, Eva), Ben (Massage, Müller), Ben (Massage, Hans) nicht erschienen.
    /// Neuerer Montag: Anna (Massage, Eva), Anna (Gesichtsbehandlung, Hans), Anna (Massage, Hans) storniert. Abgeschlossen sind alle außer den letzten beiden;
    /// berechnet sind die Massagen von Eva und Müller.
    /// </summary>
    private static async Task<(TestSpa Spa, CustomerDto Eva, CustomerDto Mueller, CustomerDto Hans)> DataAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var face = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Gesichtsbehandlung", 45, 0, 0, 60m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);
        var eva = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Eva", "Auer", null, null, null, null), Ct);
        var mueller = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Maria", "Müller", null, null, null, null), Ct);
        var hans = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Hans", "Neu", null, null, null, null), Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var (older, newer) = Mondays();

        async Task<AppointmentDto> VisitAsync(DateOnly day, int hour, Guid employee, Guid room, CustomerDto customer, TreatmentDto? treatment, AppointmentStatus status, bool bill)
        {
            var booked = await spa.BookInThePastAsync(TestSpa.At(hour, date: day), Ct, employee, room, treatment, customer.Id);
            switch (status)
            {
                case AppointmentStatus.Completed:
                    await spa.SetStatusAsync(booked.Id, AppointmentStatus.CheckedIn, Ct);
                    await spa.SetStatusAsync(booked.Id, AppointmentStatus.Completed, Ct);
                    break;
                case AppointmentStatus.NoShow:
                    await spa.SetStatusAsync(booked.Id, AppointmentStatus.NoShow, Ct);
                    break;
                case AppointmentStatus.Cancelled:
                    await spa.PostAsync<AppointmentDto>($"/api/appointments/{booked.Id}/cancel", new CancelAppointmentRequest(null), Ct);
                    break;
            }

            if (bill)
            {
                var open = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, customer.Id, [booked.Id]), Ct);
                await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{open.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, open.Open, null, null)]), Ct);
            }

            return booked;
        }

        await VisitAsync(older, 10, spa.Anna.Id, spa.RoomA.Id, eva, null, AppointmentStatus.Completed, true);
        await VisitAsync(older, 10, spa.Ben.Id, spa.RoomB.Id, mueller, null, AppointmentStatus.Completed, true);
        await VisitAsync(older, 14, spa.Ben.Id, spa.RoomB.Id, hans, null, AppointmentStatus.NoShow, false);
        await VisitAsync(newer, 10, spa.Anna.Id, spa.RoomA.Id, eva, null, AppointmentStatus.Completed, true);
        await VisitAsync(newer, 12, spa.Anna.Id, spa.RoomA.Id, hans, face, AppointmentStatus.Completed, false);
        await VisitAsync(newer, 14, spa.Anna.Id, spa.RoomA.Id, hans, null, AppointmentStatus.Cancelled, false);
        return (spa, eva, mueller, hans);
    }

    /// <summary>Belegte Minuten je Raum laut Terminen in der Datenbank (ohne stornierte und nicht erschienene).</summary>
    private async Task<Dictionary<string, double>> BlockedMinutesAsync(TestSpa spa)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var held = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == spa.Location.Id && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.RoomId, a.BlockedFrom, a.BlockedUntil }).ToListAsync(Ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == spa.Location.Id).ToDictionaryAsync(r => r.Id, r => r.Name, Ct);
        return rooms.ToDictionary(r => r.Value, r => held.Where(a => a.RoomId == r.Key).Sum(a => (a.BlockedUntil - a.BlockedFrom).TotalMinutes));
    }

    private static string Money(decimal amount) => amount.ToString("#,##0.00", CultureInfo.GetCultureInfo("de-AT")) + " EUR";

    private static async Task<ManagementReportDto> ReportAsync(TestSpa spa, DateOnly from, DateOnly until) =>
        await spa.GetAsync<ManagementReportDto>($"/api/reports/locations/{spa.Location.Id}/management?from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}", Ct);

    // ---------- Zahlen und Zeitraum ----------

    [Fact]
    public async Task Top_Behandlungen_zaehlen_die_Termine_des_Zeitraums_und_nennen_ihn()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();

        var block = await _kit.AnswerAsync(spa, $"Top-Behandlungen {Since(older)}", DateTimeOffset.UtcNow);

        var period = $"{older.AddDays(-1):dd.MM.yyyy} bis {Today:dd.MM.yyyy}";
        Assert.Equal(("TopTreatments", period), (block.Topic, block.Period));
        Assert.Equal($"Am häufigsten gebucht, {period}: 1. Massage (3), 2. Gesichtsbehandlung (1).", block.Text);
        Assert.Equal($"Management-Bericht des Standorts Wien, {period}", block.Source);
        Assert.Equal(["Platz", "Behandlung", "Termine", "Umsatz netto"], block.Table!.Columns);
        Assert.Equal(["1", "Massage", "3"], block.Table.Rows[0].Take(3));
        Assert.Equal(["2", "Gesichtsbehandlung", "1"], block.Table.Rows[1].Take(3));
        // Dieselben Zahlen wie im Cockpit.
        var report = await ReportAsync(spa, older.AddDays(-1), Today);
        Assert.Equal(3, report.Treatments.Single(t => t.Name == "Massage").Appointments);
        Assert.Equal(Money(report.Treatments.Single(t => t.Name == "Massage").NetRevenue!.Value), block.Table.Rows[0][3]);
        Assert.Equal(new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "reports", "management", spa.Location.Id, older.AddDays(-1), Today), block.Link);
    }

    [Fact]
    public async Task Umsatz_nach_Behandlung_kommt_vom_Cockpit_und_nur_mit_dem_Betraege_Recht()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var question = $"Umsatz nach Behandlung {Since(older)}";

        var owner = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow);
        var manager = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, StaffRole.Manager);
        var report = await ReportAsync(spa, older.AddDays(-1), Today);

        var massage = report.Treatments.Single(t => t.Name == "Massage");
        Assert.Equal(["Massage", "3", Money(massage.NetRevenue!.Value)], owner.Table!.Rows[0]);
        Assert.Contains("Umsatz netto nach Behandlung", owner.Text, StringComparison.Ordinal);
        Assert.Equal(AssistantBlockKind.Denied, manager.Kind);
        Assert.StartsWith("Dafür fehlt die Berechtigung. Umsätze sehen Inhaber, Manager und Backoffice", manager.Text, StringComparison.Ordinal);
        Assert.Null(manager.Table);
        var offer = Assert.Single(manager.FollowUps);
        Assert.Equal("Behandlungen nach Anzahl zeigen", offer.Label);
        Assert.Equal($"Top-Behandlungen von {older.AddDays(-1):dd.MM.yyyy} bis {Today:dd.MM.yyyy}", offer.Question);

        // Das Angebot ist eine Frage, die der Assistent beantwortet: Anzahl ohne Beträge.
        var withoutAmounts = await _kit.AnswerAsync(spa, offer.Question, DateTimeOffset.UtcNow, StaffRole.Manager);
        Assert.Equal(["Platz", "Behandlung", "Termine"], withoutAmounts.Table!.Columns);
        Assert.Equal(["1", "Massage", "3"], withoutAmounts.Table.Rows[0]);
    }

    [Fact]
    public async Task Welche_Therapeutin_die_meisten_Termine_hat_und_wie_viele_Nicht_erschienen()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var period = $"{older.AddDays(-1):dd.MM.yyyy} bis {Today:dd.MM.yyyy}";

        var therapist = await _kit.AnswerAsync(spa, $"Welche Therapeutin hat die meisten Termine {Since(older)}?", DateTimeOffset.UtcNow);
        var noShows = await _kit.AnswerAsync(spa, $"Wie viele Nicht-erschienen hatten wir {Since(older)}?", DateTimeOffset.UtcNow);
        var report = await ReportAsync(spa, older.AddDays(-1), Today);

        Assert.StartsWith($"Die meisten Termine hatte Anna: 3 (", therapist.Text, StringComparison.Ordinal);
        Assert.Equal(["Anna", "3"], therapist.Table!.Rows[0].Take(2));
        Assert.Equal(["Ben", "1"], therapist.Table.Rows[1].Take(2));
        Assert.Equal(1, report.NoShows);
        Assert.Equal(1, report.Cancellations);
        Assert.StartsWith($"Nicht erschienen: 1 im Zeitraum {period} (Quote ", noShows.Text, StringComparison.Ordinal);
        Assert.Equal(["Nicht erschienen", "1"], noShows.Table!.Rows[0].Take(2));
        Assert.Equal(["Storniert", "1"], noShows.Table.Rows[1].Take(2));
        Assert.Equal($"{report.Kpis.Single(k => k.Key == "noShowRate").Value.ToString("0.#", CultureInfo.GetCultureInfo("de-AT"))} %", noShows.Table.Rows[0][2]);
    }

    [Fact]
    public async Task Die_Auslastung_je_Person_und_Raum_kommt_vom_Berichtsdienst()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var from = older.AddDays(-1);

        var both = await _kit.AnswerAsync(spa, $"Auslastung je Person und Raum {Since(older)}", DateTimeOffset.UtcNow);
        var rooms = await _kit.AnswerAsync(spa, $"Auslastung der Räume {Since(older)}", DateTimeOffset.UtcNow);
        var report = await ReportAsync(spa, from, Today);

        var anna = report.Employees.Single(e => e.Name == "Anna");
        Assert.Contains(["Person", "Anna", $"{anna.Utilization} %"], both.Table!.Rows, new RowComparer());
        // Je Raum: belegte Minuten laut Terminen in der Datenbank (ohne stornierte und nicht erschienene) geteilt durch die Öffnungsminuten (Mo bis Sa, 11 Stunden).
        var open = Enumerable.Range(0, Today.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).Count(d => d.DayOfWeek != DayOfWeek.Sunday) * 660.0;
        var minutes = await BlockedMinutesAsync(spa);
        Assert.Equal(2, minutes.Count);
        foreach (var (name, booked) in minutes)
        {
            Assert.Contains(["Raum", name, $"{(int)Math.Round(booked / open * 100)} %"], both.Table.Rows, new RowComparer());
        }

        // Der Durchschnitt der Räume ist die Kennzahl „Auslastung Räume“ des Cockpits.
        var cockpit = report.Kpis.Single(k => k.Key == "roomUtilization").Value;
        Assert.InRange((decimal)(minutes.Values.Average() / open * 100), cockpit - 0.1m, cockpit + 0.1m);
        Assert.Contains("Räume", both.Text, StringComparison.Ordinal);
        Assert.All(rooms.Table!.Rows, r => Assert.Equal("Raum", r[0]));
        Assert.Contains($"Räume {report.Kpis.Single(k => k.Key == "roomUtilization").Value.ToString("0.#", CultureInfo.GetCultureInfo("de-AT"))} %", rooms.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Top_Kunden_nach_Ausgaben_nur_mit_Recht_sonst_nach_Besuchen()
    {
        var (spa, eva, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var question = $"Top-Kunden {Since(older)}";

        var spending = await _kit.AnswerAsync(spa, $"{question} nach Ausgaben", DateTimeOffset.UtcNow);
        var visits = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, StaffRole.Manager);
        var denied = await _kit.AnswerAsync(spa, $"{question} nach Ausgaben", DateTimeOffset.UtcNow, StaffRole.Manager);

        Assert.Equal(["Platz", "Gast", "Besuche", "Ausgaben"], spending.Table!.Columns);
        Assert.Equal(["1", eva.FirstName + " " + eva.LastName, "2", Money(158m)], spending.Table.Rows[0]);
        Assert.Equal(["2", "Maria Müller", "1", Money(79m)], spending.Table.Rows[1]);
        Assert.Equal(["3", "Hans Neu", "1", Money(0m)], spending.Table.Rows[2]);
        Assert.StartsWith("Beste Gäste nach Ausgaben, ", spending.Text, StringComparison.Ordinal);
        Assert.Equal(["Platz", "Gast", "Besuche"], visits.Table!.Columns);
        Assert.Equal(["1", "Eva Auer", "2"], visits.Table.Rows[0]);
        Assert.Equal(["2", "Hans Neu", "1"], visits.Table.Rows[1]);
        // Gleich viele Besuche teilen sich den Platz (Slice 139).
        Assert.Equal(["2", "Maria Müller", "1"], visits.Table.Rows[2]);
        Assert.StartsWith("Beste Gäste nach Besuchen, ", visits.Text, StringComparison.Ordinal);
        Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
        Assert.StartsWith($"Top-Gäste von {older.AddDays(-1):dd.MM.yyyy} bis", Assert.Single(denied.FollowUps).Question, StringComparison.Ordinal);
    }

    // ---------- Therapeut:in: Auswertungen ohne Beträge abschaltbar (Ä55) ----------

    [Fact]
    public async Task Die_Therapeutin_bekommt_Auswertungen_ohne_Betraege_standardmaessig_und_nicht_mehr_wenn_es_abgeschaltet_ist()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var questions = new[]
        {
            $"Top-Behandlungen {Since(older)}", $"Top-Kunden {Since(older)}", $"Welche Therapeutin hat die meisten Termine {Since(older)}?", $"Auslastung je Person und Raum {Since(older)}",
            $"Wie viele Nicht-erschienen hatten wir {Since(older)}?",
        };

        foreach (var question in questions)
        {
            // Standard an: Antwort ohne Beträge. Abgeschaltet: „Dafür fehlt die Berechtigung.“
            Assert.Equal(AssistantBlockKind.Answer, (await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, StaffRole.Therapist)).Kind);
            var denied = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, StaffRole.Therapist, plainReports: false);
            Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
            Assert.StartsWith("Dafür fehlt die Berechtigung.", denied.Text, StringComparison.Ordinal);
            Assert.Null(denied.Table);

            // Andere Rollen bleiben, wie sie sind, auch wenn der Schalter in ihrem Zugang auf „aus“ steht (er wirkt nur bei der Therapeut:in).
            foreach (var other in new[] { (StaffRole.Reception, false), (StaffRole.Manager, false), (StaffRole.GeneralManager, false), (StaffRole.Backoffice, false) })
            {
                Assert.Equal(AssistantBlockKind.Answer, (await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, other.Item1, other.Item2, plainReports: false)).Kind);
            }
        }
    }

    [Fact]
    public async Task Die_Standortleitung_schaltet_die_Auswertungen_der_Therapeutin_fuer_ihre_Standorte_ab_und_es_wirkt_sofort()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var question = $"Top-Behandlungen {Since(older)}";
        using var therapist = await _kit.JoinAsync(spa, StaffRole.Therapist, allLocations: false, employee: spa.Anna.Id);
        var berta = await spa.CreateEmployeeAsync("Berta", [spa.Massage.Id], spa.Location.Id, Ct);
        using var allLocationsTherapist = await _kit.JoinAsync(spa, StaffRole.Therapist, employee: berta.Id, plainReports: false);
        using var leitung = await _kit.JoinAsync(spa, StaffRole.Manager, allLocations: false);
        var member = Assert.Single(await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct), m => m.Role == StaffRole.Therapist && !m.AllLocations);
        Assert.True(member.PlainReports);
        Assert.Contains(await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct), m => m.Role == StaffRole.Therapist && m.AllLocations && !m.PlainReports);

        Assert.Equal(AssistantBlockKind.Answer, (await AnswerKit.BlockAsync(therapist, question, spa.Location.Id)).Kind);

        // Die Standortleitung ändert den Zugang an ihrem Standort (kein Recht „Berichte mit Beträgen“ nötig).
        var off = await leitung.PutAsJsonAsync($"/api/staff/{member.UserId}",
            new StaffAccessInput(StaffRole.Therapist, false, member.LocationIds, member.EmployeeId, true, null, false, false), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await TestSpa.ReadAsync<StaffMemberDto>(off, Ct)).PlainReports);
        var denied = await AnswerKit.BlockAsync(therapist, question, spa.Location.Id);
        Assert.Equal(AssistantBlockKind.Denied, denied.Kind);
        Assert.StartsWith("Dafür fehlt die Berechtigung.", denied.Text, StringComparison.Ordinal);

        // Wieder an, ebenfalls sofort.
        var on = await leitung.PutAsJsonAsync($"/api/staff/{member.UserId}",
            new StaffAccessInput(StaffRole.Therapist, false, member.LocationIds, member.EmployeeId, true, null, false, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Equal(AssistantBlockKind.Answer, (await AnswerKit.BlockAsync(therapist, question, spa.Location.Id)).Kind);

        // Einen Zugang für alle Standorte verwaltet die Standortleitung nicht.
        var other = (await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct)).First(m => m.Role == StaffRole.Therapist && m.AllLocations);
        var refused = await leitung.PutAsJsonAsync($"/api/staff/{other.UserId}",
            new StaffAccessInput(StaffRole.Therapist, true, [], other.EmployeeId, true, null, false, true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    [Fact]
    public async Task Bei_anderen_Rollen_wird_der_Schalter_nicht_abgeschaltet_gespeichert()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception, plainReports: false);
        var member = Assert.Single(await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct), m => m.Role == StaffRole.Reception);
        Assert.True(member.PlainReports);

        var saved = await spa.Client.PutAsJsonAsync($"/api/staff/{member.UserId}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true, null, false, false), TestSpa.Json, Ct);

        Assert.True((await TestSpa.ReadAsync<StaffMemberDto>(saved, Ct)).PlainReports);
    }

    // ---------- Rechte ----------

    [Fact]
    public async Task Rechte_je_Rolle_Rezeption_bekommt_Auswertungen_ohne_Betraege_Betraege_nur_mit_Recht()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        var withoutAmounts = new[]
        {
            $"Top-Behandlungen {Since(older)}", $"Top-Kunden {Since(older)}", $"Welche Therapeutin hat die meisten Termine {Since(older)}?", $"Auslastung je Person und Raum {Since(older)}",
            $"Wie viele Nicht-erschienen hatten wir {Since(older)}?",
        };
        var amounts = new[] { $"Umsatz nach Behandlung {Since(older)}", $"Top-Kunden {Since(older)} nach Ausgaben" };

        foreach (var question in withoutAmounts)
        {
            foreach (var role in new[] { StaffRole.Reception, StaffRole.Therapist })
            {
                var block = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, role);
                Assert.Equal(AssistantBlockKind.Answer, block.Kind);
                Assert.NotNull(block.Table);
                // Weder Betragsspalten noch der Verweis in den Bericht (den sehen nur Leitung und Backoffice).
                Assert.DoesNotContain(block.Table.Columns, c => c.Contains("Umsatz", StringComparison.Ordinal) || c.Contains("Ausgaben", StringComparison.Ordinal));
                Assert.DoesNotContain(block.Table.Rows.SelectMany(r => r), cell => cell.Contains("EUR", StringComparison.Ordinal));
                Assert.Null(block.Link);
            }

            foreach (var allowed in new[] { (StaffRole.Backoffice, false), (StaffRole.Reception, true), (StaffRole.Manager, false), (StaffRole.GeneralManager, false) })
            {
                var block = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, allowed.Item1, allowed.Item2);
                Assert.Equal(AssistantBlockKind.Answer, block.Kind);
            }
        }

        foreach (var question in amounts)
        {
            foreach (var role in new[] { StaffRole.Reception, StaffRole.Therapist, StaffRole.Manager })
            {
                var block = await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, role);
                Assert.Equal(AssistantBlockKind.Denied, block.Kind);
                Assert.Null(block.Table);
                Assert.Single(block.FollowUps);
            }

            foreach (var allowed in new[] { (StaffRole.Backoffice, false), (StaffRole.Reception, true), (StaffRole.GeneralManager, false) })
            {
                Assert.Equal(AssistantBlockKind.Answer, (await _kit.AnswerAsync(spa, question, DateTimeOffset.UtcNow, allowed.Item1, allowed.Item2)).Kind);
            }
        }
    }

    [Fact]
    public async Task Die_Rezeption_fragt_Auswertungen_ohne_Betraege_ueber_die_API()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        var treatments = await AnswerKit.BlockAsync(reception, $"Top-Behandlungen {Since(older)}", spa.Location.Id);
        var revenue = await AnswerKit.BlockAsync(reception, $"Umsatz nach Behandlung {Since(older)}", spa.Location.Id);
        var offer = await AnswerKit.BlockAsync(reception, Assert.Single(revenue.FollowUps).Question, spa.Location.Id);

        Assert.Equal(AssistantBlockKind.Answer, treatments.Kind);
        Assert.Equal(["Platz", "Behandlung", "Termine"], treatments.Table!.Columns);
        Assert.Equal(["1", "Massage", "3"], treatments.Table.Rows[0]);
        Assert.Equal(AssistantBlockKind.Denied, revenue.Kind);
        Assert.Equal(AssistantBlockKind.Answer, offer.Kind);
        Assert.Equal(["Platz", "Behandlung", "Termine"], offer.Table!.Columns);
    }

    [Fact]
    public async Task Das_Backoffice_fragt_Auswertungen_ueber_die_API_ohne_Kalenderrecht_und_bekommt_Beträge()
    {
        var (spa, _, _, _) = await DataAsync(fixture);
        var (older, _) = Mondays();
        using var backoffice = await _kit.JoinAsync(spa, StaffRole.Backoffice);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);
        using var receptionWithRight = await _kit.JoinAsync(spa, StaffRole.Reception, amountReports: true);

        var report = await AnswerKit.BlockAsync(backoffice, $"Umsatz nach Behandlung {Since(older)}", spa.Location.Id);
        var calendar = await AnswerKit.AskAsync(backoffice, "Was steht heute an?", spa.Location.Id);
        var openingHours = await AnswerKit.BlockAsync(backoffice, "Wie lange haben wir heute offen?", spa.Location.Id);
        var withoutRight = await AnswerKit.BlockAsync(reception, $"Umsatz nach Behandlung {Since(older)}", spa.Location.Id);
        var withRight = await AnswerKit.BlockAsync(receptionWithRight, $"Umsatz nach Behandlung {Since(older)}", spa.Location.Id);

        Assert.Equal(AssistantBlockKind.Answer, report.Kind);
        Assert.Contains("Umsatz netto nach Behandlung", report.Text, StringComparison.Ordinal);
        Assert.Equal(AssistantBlockKind.Denied, calendar.Block!.Kind);
        Assert.Equal(AssistantAudience.Management, calendar.Audience);
        Assert.Equal(AssistantBlockKind.Denied, openingHours.Kind);
        Assert.Equal(AssistantBlockKind.Denied, withoutRight.Kind);
        Assert.Equal(AssistantBlockKind.Answer, withRight.Kind);
    }

    [Fact]
    public async Task Zeitraeume_ueber_ein_Jahr_werden_auf_ein_Jahr_gekuerzt_und_der_Zeitraum_steht_in_der_Antwort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, "Top-Behandlungen seit 1.1.2015", DateTimeOffset.UtcNow);

        Assert.Equal(Today.AddDays(-365), block.Link!.From);
        Assert.Equal(Today, block.Link.Until);
        Assert.Equal($"Im Zeitraum {block.Period} gab es im Standort Wien keine Behandlungen.", block.Text);
    }

    private sealed class RowComparer : IEqualityComparer<IReadOnlyList<string>>
    {
        public bool Equals(IReadOnlyList<string>? x, IReadOnlyList<string>? y) => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(IReadOnlyList<string> obj) => obj.Count;
    }
}

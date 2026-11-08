using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 102 (Ä191): native Maske „Berichte“ nach web/src/reports. Die Tests prüfen Sprung, Adressen, gesendeten Inhalt und die Rechte; alle Zahlen kommen im Stub fest aus der API und ihre
/// Rechnung prüfen die API-Tests der Berichte.
/// </summary>
public sealed partial class AdminReportsTests
{
    private sealed class FixedTime : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(() => callback(state));
            _timers.Add(timer);
            return timer;
        }

        public void FireTimers()
        {
            foreach (var timer in _timers.ToList())
            {
                _timers.Remove(timer);
                timer.Fire();
            }
        }

        private sealed class ManualTimer(Action fire) : ITimer
        {
            public void Fire() => fire();

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static readonly Guid SalzburgId = StockSample.Salzburg.Id;

    private static readonly string OctoberReport = $"GET /api/reports/locations/{SalzburgId}/management?from=2026-10-01&until=2026-10-31";

    private static ManagementReportDto Report(bool hidden = false) => new(SalzburgId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
        [new Kpi("revenue", "Umsatz", 12345.5m, 10000m, "€"), new Kpi("utilization", "Auslastung", 61.5m, null, "%")],
        hidden ? [] : [new RevenueLine("Behandlungen", "SPA", 1200m, 1000m)],
        hidden ? [] : [new RevenueLine("Karte", null, 1200m, 1000m)],
        [new EmployeeKpi(Guid.NewGuid(), "Lisa", 80m, 60m, 75, hidden ? null : 50m)],
        [new TreatmentKpi(Guid.NewGuid(), "Massage", 12, hidden ? null : 900m, hidden ? null : -2.5m)],
        ["Auslastung unter Plan"], hidden);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { StockSample.Vienna, StockSample.Salzburg })
        .On("GET /api/employees", new List<EmployeeDto>())
        .On(OctoberReport, Report());

    private static ShellViewModel Shell(StubApi api) =>
        new(api.Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    private static ReportsViewModel Reports(StubApi api, CurrentUser? user = null, TimeProvider? time = null) => new(api.Client(), user ?? TestData.Owner(), time ?? new FixedTime());

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static CurrentUser Lead(Guid employeeId) => TestData.Reception(employeeId);

    [Fact]
    public async Task Given_der_Assistent_bietet_Im_Bericht_oeffnen_fuer_Oktober_When_die_Inhaberin_klickt_Then_oeffnet_der_native_Umsatzbericht_mit_Standort_und_Oktober()
    {
        var api = Api();
        var shell = Shell(api);
        var link = new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "reports", "management", SalzburgId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        shell.ShowAssistantLink(link);
        await shell.AdminPage.InitializeAsync();
        await shell.AdminPage.Reports!.EnsureLoadedAsync();

        Assert.Same(shell.AdminPage.Reports, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Reports.IsManagementTab);
        Assert.Equal(SalzburgId, shell.AdminPage.Reports.SelectedLocation?.Id);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (shell.AdminPage.Reports.Management.From, shell.AdminPage.Reports.Management.Until));
        Assert.Contains(api.Calls, c => c.Route == OctoberReport);
    }

    [Fact]
    public async Task Ein_Sprung_in_die_geladene_Maske_wechselt_Reiter_Standort_und_Zeitraum()
    {
        var api = AddReports(new StubApi()).On(OctoberReport, Report());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        Assert.True(reports.IsBriefingTab);

        reports.Open(new AdminTarget(null, SalzburgId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, ReportsViewModel.ManagementTab));
        await Task.Yield();

        Assert.True(reports.IsManagementTab);
        Assert.Equal(SalzburgId, reports.SelectedLocation?.Id);
        Assert.Contains(api.Calls, c => c.Route == OctoberReport);
    }

    [Fact]
    public async Task Management_zeigt_die_Zahlen_der_API_und_ohne_Recht_keine_Betraege()
    {
        var api = AddReports(new StubApi());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        await Task.Yield();

        var management = reports.Management;
        Assert.Equal(new DateOnly(2026, 10, 1), management.From);
        Assert.Equal(Today, management.Until);
        Assert.Equal(["12.345,50 €", "61,5 %", "240"], management.Kpis.Select(k => k.Value));
        Assert.Equal("Vorperiode 10.000,00 €", management.Kpis[0].Previous);
        Assert.Equal("Behandlungen (SPA)", management.RevenueByAccount[0].Label);
        Assert.Equal("-2,50 €", management.Treatments[0].Margin);
        Assert.True(management.Treatments[0].IsLoss);
        Assert.True(management.ShowAmounts);

        api.On(ManagementRoute(new DateOnly(2026, 10, 1), Today), Management(hidden: true));
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        await Task.Yield();

        Assert.True(management.AmountsHidden);
        Assert.False(management.ShowAmounts);
        Assert.Empty(management.RevenueByAccount);
        Assert.Equal("–", management.Employees[0].RevenuePerHour);
    }

    [Fact]
    public async Task Der_Vergleich_mit_anderen_Spas_laedt_erst_auf_Wunsch_und_nennt_die_Einordnung()
    {
        var api = AddReports(new StubApi());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        await Task.Yield();
        Assert.DoesNotContain(api.Calls, c => c.Route == BenchmarkRoute(30));

        reports.Management.Compare = true;
        await Task.Yield();

        Assert.Single(api.Calls, c => c.Route == BenchmarkRoute(30));
        Assert.True(reports.Management.HasBenchmark);
        Assert.True(reports.Management.BenchmarkIsSample);
        Assert.Equal("0 Kennzahlen im stärksten Viertel, 1 Hebel unter der Mitte.", reports.Management.BenchmarkSummary);
        Assert.Equal("Über der Mitte", reports.Management.BenchmarkRows[0].Rating);
        Assert.Equal("Mitte 4,0 %, mittlere Hälfte 2,0 % bis 6,0 % (weniger ist besser)", reports.Management.BenchmarkRows[1].Range);
    }

    [Fact]
    public async Task Die_Reiter_folgen_den_Rechten_und_ein_Sprung_ohne_Recht_bleibt_auf_dem_erlaubten_Reiter()
    {
        var api = AddReports(new StubApi());
        var reports = Reports(api, Lead(PersonId));

        reports.Open(new AdminTarget(Section: ReportsViewModel.ManagementTab));
        await reports.EnsureLoadedAsync();

        Assert.False(reports.CanSeeManagement);
        Assert.False(reports.CanSeeCompliance);
        Assert.True(reports.IsBriefingTab);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/management", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Der_Tagesstart_fragt_nach_der_KI_Zusammenfassung_nach_und_endet_beim_Verlassen()
    {
        var time = new FixedTime();
        var api = AddReports(new StubApi()).On(BriefingRoute, Briefing(pending: true));
        var reports = Reports(api, time: time);

        // Der Tagesstart ist der erste Reiter (R07 Slice 144): Das Laden endet erst mit dem Nachfragen, also nicht darauf warten.
        _ = reports.EnsureLoadedAsync();
        await WaitUntilAsync(() => reports.Briefing.IsAiPending);
        Assert.True(reports.IsBriefingTab);
        Assert.True(reports.Briefing.IsAiPending);
        Assert.Equal(1, api.Calls.Count(c => c.Route == BriefingRoute));

        api.On(BriefingRoute, Briefing());
        time.FireTimers();
        await Task.Yield();
        Assert.Equal(2, api.Calls.Count(c => c.Route == BriefingRoute));
        Assert.Equal("Ein ruhiger Tag mit einem Erstbesuch.", reports.Briefing.AiSummary);
        Assert.Equal("Von der KI zusammengefasst.", reports.Briefing.AiNotice);

        api.On(BriefingRoute, Briefing(pending: true));
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        reports.SelectTabCommand.Execute(ReportsViewModel.BriefingTab);
        await Task.Yield();
        var before = api.Calls.Count(c => c.Route == BriefingRoute);
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        time.FireTimers();
        await Task.Yield();
        Assert.Equal(before, api.Calls.Count(c => c.Route == BriefingRoute));
    }

    [Fact]
    public async Task Bereitstellen_abhaken_sendet_den_Stand_und_behaelt_die_Zeile()
    {
        var api = AddReports(new StubApi()).On($"PUT /api/appointments/{AppointmentId}/extras/{ExtraId}/check", new { });
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.BriefingTab);
        await Task.Yield();
        var row = Assert.Single(reports.Briefing.Servings);
        Assert.Equal("11:20 Früchteteller: Anna Berg, Raum 1 · Wunsch: Kräutertee · Anamnese beachten", row.Text);

        api.On(ServingsRoute, Servings(done: true));
        row.Done = true;
        await row.ToggleCommand.ExecuteAsync(null);

        Assert.True(Sent<ServingCheck>(api, $"PUT /api/appointments/{AppointmentId}/extras/{ExtraId}/check").Done);
        Assert.Same(row, Assert.Single(reports.Briefing.Servings));
        Assert.Contains("Lisa", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mein_Tag_laedt_die_eigene_Person_und_ein_Konto_ohne_Person_bekommt_einen_Hinweis()
    {
        var api = AddReports(new StubApi());
        var own = Reports(api, new CurrentUser("u", "m", "L", 1, new CurrentAccess(Guid.NewGuid(), "S", StaffRole.Therapist, true, []), PersonId, [AccessRight.ViewCalendar]));
        await own.EnsureLoadedAsync();
        Assert.False(own.DayPlan.CanChoosePerson);
        Assert.Contains(api.Calls, c => c.Route == DayPlanRoute);
        var card = Assert.Single(own.DayPlan.Cards);
        Assert.Contains("Klassische Massage (60 Min.), Raum 1", card.Title, StringComparison.Ordinal);
        Assert.Equal("Anna Berg · 3. Besuch, zuletzt 19.10.2026 · Geburtstag", card.Guest);
        Assert.True(card.IsAnamnesisWarning);
        Assert.Equal(["Lavendel bereitstellen (Vorliebe)"], card.TodoNotes);

        var none = Reports(new StubApi().On("GET /api/locations", new List<LocationDto> { StockSample.Vienna }).On("GET /api/employees", new List<EmployeeDto>()),
            new CurrentUser("u", "m", "L", 1, new CurrentAccess(Guid.NewGuid(), "S", StaffRole.Therapist, true, []), null, [AccessRight.ViewCalendar]));
        await none.EnsureLoadedAsync();
        Assert.True(none.DayPlan.HasNoPerson);
    }

    [Fact]
    public async Task Vorbereitung_Notiz_und_Vorlieben_werden_gespeichert_und_eine_offene_Notiz_zaehlt_als_ungespeichert()
    {
        var api = AddReports(new StubApi())
            .On($"PUT /api/reports/appointments/{AppointmentId}/preparation/{StepId}", new { })
            .On($"PUT /api/reports/appointments/{AppointmentId}/follow-up", new { })
            .On($"PUT /api/reports/customers/{CustomerId}/preferences", new CustomerPreferences(null, null, null, null, null, null, null, null));
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        await Task.Yield();
        var card = Assert.Single(reports.DayPlan.Cards);

        var step = Assert.Single(card.Todo);
        step.Done = true;
        await step.ToggleCommand.ExecuteAsync(null);
        Assert.True(Sent<PreparationCheck>(api, $"PUT /api/reports/appointments/{AppointmentId}/preparation/{StepId}").Done);

        Assert.False(reports.HasUnsavedChanges);
        card.Note = "Schulter beachten";
        Assert.True(reports.HasUnsavedChanges);
        Assert.True(await reports.SaveChangesAsync());
        Assert.Equal("Schulter beachten", Sent<FollowUpNote>(api, $"PUT /api/reports/appointments/{AppointmentId}/follow-up").Note);
        Assert.False(reports.HasUnsavedChanges);

        card.EditPreferencesCommand.Execute(null);
        card.Music = "Klassik";
        card.Scent = " ";
        await card.SavePreferencesCommand.ExecuteAsync(null);
        var sent = Sent<CustomerPreferences>(api, $"PUT /api/reports/customers/{CustomerId}/preferences");
        Assert.Equal("Klassik", sent.Music);
        Assert.Null(sent.Scent);
        Assert.True(sent.PrefersQuiet);
        Assert.Equal("fest", sent.Pressure);
    }

    [Fact]
    public async Task Eine_verworfene_Notiz_steht_wieder_wie_gespeichert()
    {
        var reports = Reports(AddReports(new StubApi()));
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        await Task.Yield();
        var card = Assert.Single(reports.DayPlan.Cards);

        card.Note = "Entwurf";
        reports.Discard();

        Assert.Equal("Nacken nachfragen", card.Note);
        Assert.False(reports.HasUnsavedChanges);
    }

    [Fact]
    public async Task Hygiene_abhaken_laedt_neu_und_behaelt_die_Zeile_damit_der_Fokus_bleibt()
    {
        var api = AddReports(new StubApi()).On($"PUT /api/rooms/{RoomId}/start-checks/{CheckId}", new { });
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Task.Yield();
        var room = Assert.Single(reports.Hygiene.Rooms);
        var item = Assert.Single(room.Items);
        Assert.Equal("Raum 1: teilweise abgehakt", room.Heading);
        Assert.Equal("Ausstattung vollständig: 1 Material unter Minimum", room.Equipment);
        Assert.Equal(["Massageöl: 200 von 500 ml, 300 ml nachfüllen"], room.EquipmentLines);

        api.On(RoomStartRoute, RoomStart(RoomStartState.Checked, done: true));
        item.Done = true;
        await item.ToggleCommand.ExecuteAsync(null);

        var sent = Sent<RoomStartCheckInput>(api, $"PUT /api/rooms/{RoomId}/start-checks/{CheckId}");
        Assert.True(sent.Done);
        Assert.Equal(Today, sent.Date);
        Assert.Same(item, Assert.Single(room.Items));
        Assert.True(room.CanApprove);
        Assert.Equal("Raum 1: freigegeben", room.Heading);
    }

    [Fact]
    public async Task Die_Leitung_nimmt_ohne_PIN_ab_und_sonst_verlangt_die_Abnahme_Person_und_PIN()
    {
        var route = $"POST /api/rooms/{RoomId}/approvals";
        var approval = new RoomApprovalDto(RoomId, Today, RoomApprovalKind.DayStart, At(8), "Markus");
        var api = AddReports(new StubApi()).On(RoomStartRoute, RoomStart(RoomStartState.Checked, done: true)).On(route, approval);

        var leader = Reports(api);
        await leader.EnsureLoadedAsync();
        leader.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Task.Yield();
        Assert.False(leader.Hygiene.NeedsPin);
        await leader.Hygiene.Rooms[0].ApproveCommand.ExecuteAsync(null);
        var sent = Sent<RoomApprovalInput>(api, route);
        Assert.Null(sent.Approval);
        Assert.Equal(RoomApprovalKind.DayStart, sent.Kind);
        Assert.Equal("Abgenommen.", leader.Hygiene.Message);
        api.Calls.Clear();

        var reception = Reports(api, TestData.Reception(PersonId));
        await reception.EnsureLoadedAsync();
        reception.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Task.Yield();
        await reception.Hygiene.Rooms[0].ApproveCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);
        Assert.Equal("Wählen Sie die Leitung und geben Sie ihre PIN ein.", reception.Hygiene.Error);

        reception.Hygiene.SelectedApprover = Assert.Single(reception.Hygiene.Approvers);
        reception.Hygiene.Pin = "1234";
        await reception.Hygiene.Rooms[0].ApproveCommand.ExecuteAsync(null);
        var withPin = Sent<RoomApprovalInput>(api, route);
        Assert.Equal(new SupervisorApproval("lead", "1234"), withPin.Approval);
        Assert.Equal("", reception.Hygiene.Pin);
    }

    [Fact]
    public async Task Raumpflege_abhaken_bietet_danach_die_Abnahme_an_und_das_Hygieneprotokoll_kommt_unveraendert()
    {
        var csv = new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x3B, 0x0D, 0x0A };
        var logRoute = $"GET /api/locations/{LocationId}/hygiene-log.csv?from=2026-09-19&until=2026-10-19";
        var api = AddReports(new StubApi()).On($"PUT /api/appointments/{AppointmentId}/room-care", new { }).OnBytes(logRoute, csv);
        var files = new FakeFileSaver();
        var reports = Reports(api);
        reports.Files = files;
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Task.Yield();
        var task = Assert.Single(reports.Hygiene.CareTasks);
        Assert.True(task.IsWarning);
        Assert.Equal("überfällig", task.Detail);
        Assert.Empty(reports.Hygiene.CareApprovals);

        api.On(RoomCareRoute, RoomCare(done: true));
        task.Done = true;
        await task.ToggleCommand.ExecuteAsync(null);

        Assert.True(Sent<RoomCareCheckInput>(api, $"PUT /api/appointments/{AppointmentId}/room-care").Done);
        Assert.Equal("Raumpflege in Raum 1 abnehmen", Assert.Single(reports.Hygiene.CareApprovals).Label);

        await reports.Hygiene.SaveLogCommand.ExecuteAsync(null);
        Assert.Equal(csv, Assert.Single(files.Saved).Content);
    }

    [Fact]
    public async Task Pflichtberichte_speichern_die_Exporte_unveraendert_und_ohne_Recht_gibt_es_keine_Exporte()
    {
        var dep = new byte[] { 1, 2, 3 };
        var api = AddReports(new StubApi()).OnBytes("GET /api/pos/registers/at1/dep", dep);
        var files = new FakeFileSaver();
        var reports = Reports(api);
        reports.Files = files;
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.ComplianceTab);
        await Task.Yield();

        var compliance = reports.Compliance;
        Assert.Equal("3 offene Gutscheine über 450,00 € (Verbindlichkeit, aktueller Stand), 1 offene Pakete mit 4 Behandlungen.", compliance.Vouchers);
        Assert.Equal("160,5", compliance.WorkingTime[0].Hours);
        var at = compliance.Registers[0];
        Assert.Equal("Kassa AT1 (AT)", at.Title);
        Assert.False(at.ShowTse);
        await at.ExportCommand.ExecuteAsync(null);
        var saved = Assert.Single(files.Saved);
        Assert.Equal("dep-AT1.json", saved.Name);
        Assert.Equal(dep, saved.Content);

        api.On(ComplianceRoute, Compliance(hidden: true));
        reports.SelectTabCommand.Execute(ReportsViewModel.DayTab);
        reports.SelectTabCommand.Execute(ReportsViewModel.ComplianceTab);
        await Task.Yield();
        Assert.True(compliance.ShowAmountsHint);
        Assert.All(compliance.Registers, r => Assert.False(r.ShowExport || r.ShowTse));
        Assert.Equal("3 offene Gutscheine, 1 offene Pakete mit 4 Behandlungen.", compliance.Vouchers);
    }

    [Fact]
    public async Task Der_TSE_Export_laesst_sich_anfordern_und_sperrt_den_Knopf_solange_er_laeuft()
    {
        var time = new FixedTime();
        var api = AddReports(new StubApi())
            .On(TseRoute, new List<TseExportDto>())
            .On($"POST /api/pos/registers/{RegisterDe}/tse-exports", Tse(TseExportStatus.Pending))
            .OnBytes($"GET /api/pos/tse-exports/{TseId}/file", [9]);
        var files = new FakeFileSaver();
        var reports = Reports(api, time: time);
        reports.Files = files;
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.ComplianceTab);
        await Task.Yield();
        var german = reports.Compliance.Registers[1];
        Assert.True(german.RequestTseCommand.CanExecute(null));

        api.On(TseRoute, new List<TseExportDto> { Tse(TseExportStatus.Pending) });
        await german.RequestTseCommand.ExecuteAsync(null);
        await Task.Yield();

        var input = Sent<TseExportInput>(api, $"POST /api/pos/registers/{RegisterDe}/tse-exports");
        Assert.Equal((new DateOnly(2026, 1, 1), Today), (input.From, input.Until));
        Assert.Equal("TSE-Export angefordert. Er steht gleich in der Liste.", reports.Compliance.Message);
        Assert.False(german.RequestTseCommand.CanExecute(null));

        api.On(TseRoute, new List<TseExportDto> { Tse() });
        time.FireTimers();
        await Task.Yield();
        Assert.True(german.RequestTseCommand.CanExecute(null));
        await Assert.Single(german.TseExports).SaveCommand.ExecuteAsync(null);
        Assert.Equal("tse.tar", Assert.Single(files.Saved).Name);
    }

    [Fact]
    public async Task Nach_einem_Ladefehler_stehen_keine_alten_Zahlen_unter_dem_neuen_Zeitraum()
    {
        var api = AddReports(new StubApi());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        await Task.Yield();
        Assert.NotEmpty(reports.Management.Kpis);
        api.On(ManagementRoute(new DateOnly(2026, 9, 1), Today), new { }, System.Net.HttpStatusCode.InternalServerError);

        reports.Management.From = new DateOnly(2026, 9, 1);
        await Task.Yield();

        Assert.NotNull(reports.Management.LoadError);
        Assert.Empty(reports.Management.Kpis);
    }

    [Fact]
    public async Task Die_Leitung_der_Raumpflege_wird_bei_jedem_Laden_neu_geholt()
    {
        var api = AddReports(new StubApi());
        var reports = Reports(api, TestData.Reception(PersonId));
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Task.Yield();
        Assert.Equal("Markus", Assert.Single(reports.Hygiene.Approvers).DisplayName);
        api.On(ApproversRoute, new List<QuickSwitchPerson> { new("lead2", "Sabine", StaffRole.Manager) });

        await reports.Hygiene.ShowAsync(reports.SelectedLocation!, Today);

        Assert.Equal("Sabine", Assert.Single(reports.Hygiene.Approvers).DisplayName);
    }

    [Fact]
    public async Task Ein_Haken_der_Bereitstellung_gilt_dem_Termin_des_neu_geladenen_Tages()
    {
        var otherAppointment = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var route = $"PUT /api/appointments/{otherAppointment}/extras/{ExtraId}/check";
        var api = AddReports(new StubApi()).On(route, new { });
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.BriefingTab);
        await Task.Yield();
        var day = Servings();
        api.On(ServingsRoute, day with { Items = [day.Items[0] with { AppointmentId = otherAppointment }] });

        await reports.Briefing.ShowAsync(reports.SelectedLocation!, Today);
        await reports.Briefing.Servings[0].ToggleCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == route);
    }
}

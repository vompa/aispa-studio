using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0099, Entscheidung 6 (P10c): der leere Betrieb. Ein frisch eingerichteter Betrieb hat nach der Ersteinrichtung genau einen Standort und die Inhaberin, sonst nichts.
/// Die Shell öffnet mit einem Standort und sonst leeren Antworten der API; jede Verwaltungsseite (alle Gruppen, Reiter und Ansichten) muss sich dann ohne Absturz und ohne
/// Bindungsfehler öffnen lassen und einen sichtbaren Leerzustand oder den Knopf zum Anlegen zeigen.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly LocationDto OnlyLocation = SpaSample.Vienna with { OpeningHours = [] };

    /// <summary>Mit dem Standort entsteht das Hauptlager (ADR 0099), sonst nichts im Lager.</summary>
    private static readonly StoragePlaceDto MainStoragePlace = new(Guid.NewGuid(), OnlyLocation.Id, "Hauptlager", null, StoragePlaceKind.Main, null, true, 0, []);

    /// <summary>Die Ersteinrichtung: der Standort steht, alles andere ist offen.</summary>
    private static readonly SetupProgressDto SetupAfterFirstLocation = new(
    [
        new("location", "Standort anlegen", "Hinweis.", true, "masterData", "locations"),
        new("hours", "Öffnungszeiten eintragen", "Hinweis.", false, "masterData", "locations"),
        new("rooms", "Räume anlegen", "Hinweis.", false, "masterData", "locations"),
        new("treatments", "Behandlungen anlegen", "Hinweis.", false, "masterData", "treatments"),
        new("people", "Team anlegen", "Hinweis.", false, "masterData", "team"),
        new("register", "Kassa einrichten", "Hinweis.", false, "masterData", "kassa"),
    ]);

    /// <summary>Die drei Erlöskonten, die jeder Betrieb von Anfang an hat (Migration „Kassa“); eigene kommen dazu.</summary>
    private static readonly List<RevenueAccountDto> StandardRevenueAccounts =
    [
        new(Guid.NewGuid(), "3600", "Mehrzweckgutscheine (nicht steuerbar)", null),
        new(Guid.NewGuid(), "4000", "Erlöse Behandlungen", null),
        new(Guid.NewGuid(), "4100", "Erlöse Produkte", null),
    ];

    private static readonly EmployeeDto OnlyOwner = new(Guid.NewGuid(), "Katrin Aigner", "#2f6f5e", [], []);

    /// <summary>Ein Standort, die Inhaberin und sonst nichts: keine Räume, Geräte, Behandlungen, Kunden, Termine, Lagerbestände, Belege oder Aktionen.</summary>
    private static StubApi EmptyBusinessApi() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("0.9.0+abc", "Development", DateTimeOffset.UtcNow))
        .On("GET /api/auth/me", Owner())
        .On("GET /api/tenant", SpaTenant with { Company = CompanyProfile.Empty })
        .On("GET /api/tenant/history", new List<HistoryEntry>())
        .On("GET /api/tenant/embedding", new EmbeddingSettings([]))
        .On("GET /api/locations", new List<LocationDto> { OnlyLocation })
        .On($"GET /api/locations/{OnlyLocation.Id}/resources", new LocationResources(OnlyLocation, [], [], [OnlyOwner], [], []))
        .On($"GET /api/locations/{OnlyLocation.Id}/calendar?date=2026-10-19", new CalendarDay(OnlyLocation.Id, SpaSample.Day, "Europe/Vienna", [], [], [], []))
        .OnOverview(OnlyLocation.Id, () => [])
        .On("GET /api/system/modules", new SystemModules(false, true, true, true, true, true, true, true))
        .On("GET /api/auth/quick-switch", new QuickSwitchState(null, []))
        .On("GET /api/employees", new List<EmployeeDto> { OnlyOwner })
        .On("GET /api/setup/progress", SetupAfterFirstLocation)
        .On("GET /api/holidays/regions", new List<HolidayRegionDto>())
        .On("GET /api/pos/articles", new List<ArticleDto>())
        .On("GET /api/pos/registers", new List<CashRegisterDto>())
        .On("GET /api/pos/revenue-accounts", StandardRevenueAccounts)
        .On("GET /api/pos/settings", new PosSettingsDto(10m, false))
        .On("GET /api/treatments", new List<TreatmentDto>())
        .On("GET /api/qualifications", new List<QualificationDto>())
        .On("GET /api/stock/items", new List<StockItemDto>())
        .On("GET /api/handbook", new List<HandbookEntryDto>())
        .On("GET /api/calendar-subscriptions", new List<CalendarSubscriptionDto>())
        .On("GET /api/promotions?includeArchived=true", new List<PromotionDto>())
        .On("GET /api/inbox", new List<InboundMailSummary>())
        .On("GET /api/hr-file/follow-ups", new List<HrFollowUpDto>())
        .On("GET /api/reminders/settings", RemindersTests.Day)
        .On("GET /api/retention/settings", new RebookSettingsDto(false, []))
        .On("GET /api/dispatch-log/summary", new DispatchSummaryDto(30, new(0, 0, 0), new(0, 0, 0)))
        .On("GET /api/intake/mailbox", null, System.Net.HttpStatusCode.NoContent)
        .On("GET /api/intake/sender-rules", new List<SenderRuleDto>())
        .On("GET /api/intake/routes", new List<IntakeRouteDto>())
        .On("GET /api/tenant/payments", PaymentsTests.Nothing)
        .On("GET /api/system/ai", new AiStatus(false, LocalAiState.Missing, null, null, null, null))
        .On("GET /api/system/ai/claude-key", new ClaudeKeyStatus(false, ClaudeKeySource.None, null, false))
        .On("GET /api/accounting/settings", new AccountingSettingsDto(null, null, 4, 1, "1370", "1000", "1360", "1200", "3250", "1460", "1370", "6960", "4960", "19=3;7=2"))
        .On("GET /api/employee-notes/symbols?on=2026-10-19", new List<StaffNoteSymbolDto>())
        .On("GET /api/handbook/reminders?date=2026-10-19", new List<HandbookEntryDto>())
        .On("GET /api/calendar-subscriptions/notices", new List<SubscriptionNoticeDto>())
        .On($"GET /api/locations/{OnlyLocation.Id}/displays", new List<DisplayDeviceDto>())
        .On($"GET /api/pos/terminals?locationId={OnlyLocation.Id}", new List<PaymentTerminalDto>())
        .On($"GET /api/locations/{OnlyLocation.Id}/room-start?date=2026-10-19", new RoomStartDayDto(OnlyLocation.Id, SpaSample.Day, []))
        .On($"GET /api/locations/{OnlyLocation.Id}/servings?date=2026-10-19", new ServingDayDto(OnlyLocation.Id, SpaSample.Day, [], []))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/demand?from=2026-10-19&until=2026-10-19", new StockDemandDto(OnlyLocation.Id, SpaSample.Day, SpaSample.Day, 0, [], []))
        .On($"GET /api/calendar-subscriptions?locationId={OnlyLocation.Id}", new List<CalendarSubscriptionDto>())
        .On($"GET /api/calendar-subscriptions/notices?locationId={OnlyLocation.Id}", new List<SubscriptionNoticeDto>())
        .On($"GET /api/locations/{OnlyLocation.Id}/appointment-holds", new List<AppointmentHoldDto>())
        .On($"GET /api/staff-duties/reminders?locationId={OnlyLocation.Id}", new List<DutyReminderDto>())
        .On($"GET /api/locations/{OnlyLocation.Id}/waitlist", new List<WaitlistStaffEntry>())
        .On($"GET /api/locations/{OnlyLocation.Id}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, "", false, 20, 48, true, 0, ""))
        .On($"GET /api/locations/{OnlyLocation.Id}/guest-message-delay", new GuestMessageDelayDto(10))
        .On($"GET /api/costs/locations/{OnlyLocation.Id}/analysis?month=2026-10", new CostAnalysisDto(OnlyLocation.Id, "2026-10", 0m, 0m, []))
        .On($"GET /api/costs/locations/{OnlyLocation.Id}/settings", new LocationCostSettingsDto(0m, 0m, 0m, 70, 80))
        .On($"GET /api/costs/locations/{OnlyLocation.Id}/rooms", new List<RoomCostDto>())
        .On($"GET /api/costs/locations/{OnlyLocation.Id}/employees", new List<EmployeeCostDto>())
        .On($"GET /api/reports/locations/{OnlyLocation.Id}/briefing?date=2026-10-19", new BriefingDto(OnlyLocation.Id, SpaSample.Day, "", 0, 0, [], [], [], [], []))
        .On("GET /api/calendar-subscriptions/sources", new List<SubscriptionSource>())
        .On("GET /api/employees/former", new List<EmployeeDto>())
        .On("GET /api/staff", new List<StaffMemberDto>())
        .On("GET /api/staff/invitations", new List<InvitationDto>())
        .On("GET /api/stock/suppliers", new List<StockSupplierDto>())
        .On($"GET /api/stock/locations/{OnlyLocation.Id}", new StockOverviewDto(OnlyLocation.Id, 30, 0m, []))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/batches", new StockBatchesDto(OnlyLocation.Id, SpaSample.Day, 30, []))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/storage-places", new List<StoragePlaceDto> { MainStoragePlace })
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/expiry-tasks", new StockExpiryTasksDto(OnlyLocation.Id, SpaSample.Day, [], [], []))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/orders", new List<PurchaseOrderDto>())
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/reorder", new List<ReorderSuggestionDto>())
        .On("GET /api/staff-regulations/flags", new List<StaffFlagsDto>())
        .On($"GET /api/employees/{OnlyOwner.Id}/overview", new EmployeeOverviewDto(OnlyOwner.Id, null, 0m, 0m, null, 0, 0, 0))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/demand?from=2026-10-19&until=2026-10-25", new StockDemandDto(OnlyLocation.Id, SpaSample.Day, SpaSample.Day, 0, [], []))
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/count-sessions?status=Submitted", new List<StockCountSessionDto>())
        .On($"GET /api/reports/locations/{OnlyLocation.Id}/stock-usage?from=2026-09-20&until=2026-10-19", new StockUsageReportDto(OnlyLocation.Id, SpaSample.Day, SpaSample.Day, [], 0m, false))
        .On("POST /api/promotions/preview", new PromotionPreviewDto("", [], ""))
        .On($"GET /api/locations/{OnlyLocation.Id}/roster?from=2026-10-19", new RosterWeek(OnlyLocation.Id, SpaSample.Day, Week, []))
        .On($"GET /api/locations/{OnlyLocation.Id}/timesheet?from=2026-10-19", new Timesheet(OnlyLocation.Id, "AT", SpaSample.Day, Week, []))
        .On("GET /api/absence-requests", new List<AbsenceRequestDto>())
        .On("GET /api/payroll/settings", new PayrollSettingsDto(FiscalCountry.AT, null, null, new TimeOnly(22, 0), new TimeOnly(6, 0), null, null, null, null, null, null, null))
        .On("GET /api/staff-regulations/powers", new List<PowerOverviewRowDto>())
        .On("GET /api/staff-duties/overview", new List<DutyOverviewRowDto>())
        .On("GET /api/staff-duties/expiring", new List<ExpiringCertificateDto>())
        .On("GET /api/staff-duties/types", new List<DutyTypeDto>())
        .On($"GET /api/stock/locations/{OnlyLocation.Id}/movements?from=2026-09-19&until=2026-10-19", new List<StockMovementDto>())
        .On("GET /api/business-contacts?includeArchived=false", new List<BusinessContactDto>())
        .On("GET /api/business-contacts?includeArchived=true", new List<BusinessContactDto>())
        .On($"GET /api/locations/{OnlyLocation.Id}/room-care?date=2026-10-19", new RoomCareDay([], []))
        .On($"GET /api/locations/{OnlyLocation.Id}/room-approvers", new List<QuickSwitchPerson>())
        .On($"GET /api/reports/locations/{OnlyLocation.Id}/day-plan?date=2026-10-19&employeeId={OnlyOwner.Id}", new DayPlanDto(OnlyOwner.Id, OnlyOwner.DisplayName, SpaSample.Day, [], [], []))
        .On($"GET /api/reports/locations/{OnlyLocation.Id}/management?from=2026-10-01&until=2026-10-19", new ManagementReportDto(OnlyLocation.Id, new DateOnly(2026, 10, 1), SpaSample.Day, [], [], [], [], [], []))
        .On("GET /api/reports/mail-subscription", new ReportMailSubscription(Enabled: true, Available: true))
        .On($"GET /api/reports/locations/{OnlyLocation.Id}/compliance?date=2026-10-19", new ComplianceReportDto(OnlyLocation.Id, SpaSample.Day, [], 0m, 0, [], []));

    private static readonly List<DateOnly> Week = [.. Enumerable.Range(0, 7).Select(SpaSample.Day.AddDays)];

    private static async Task<ShellViewModel> OpenEmptyAdmin(Window window, MainViewModel main)
    {
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Calendar.LoadAsync();
        await Pump();
        ClickCenter(window, NavRow(window, "Verwaltung"));
        await Pump(Settling);
        Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
        return shell;
    }

    /// <summary>Alle sichtbaren Fehlerleisten der Maske; eine Maske, die leer nicht lädt, zeigt eine (zum Beispiel „nicht geladen“).</summary>
    private static List<string> VisibleErrors(Window window) =>
        [.. window.GetVisualDescendants().OfType<InfoBar>().Where(b => b.IsEffectivelyVisible && b.Severity == InfoBarSeverity.Error).Select(b => $"{b.Title} {b.Message}".Trim())];

    /// <summary>
    /// Jede Stelle der Verwaltung, die eine eigene Ansicht hat: Name für die Meldung, der Weg dorthin (Sprungziel wie Kalender und Assistent) und was die Ansicht leer zeigen muss.
    /// Ein Eintrag mit „#“ ist ein sichtbares Element dieses Namens (der Knopf zum Anlegen), jeder andere ein sichtbarer Satz (der Leerzustand). Das Team gehört zur Maske Team (P5, P11):
    /// es muss sich leer öffnen lassen, die Erwartung dort bleibt leer.
    /// </summary>
    private static readonly (string Name, Action<AdminPageViewModel> Go, string[] Expect)[] EmptyTour =
    [
        ("Firmendaten", a => a.Show("masterData", "company"), ["#SetupCard"]),
        ("Standorte und Räume", a => a.Show("masterData", "locations"), ["#NewLocationButton", "Noch kein Raum angelegt.", "Noch kein Gerät angelegt."]),
        ("Schließzeiten", a => a.Show("masterData", ClosuresPageViewModel.ClosuresSection), ["#NewButton", "#EmptyNewClosureButton"]),
        ("Kalender-Abos", a => a.Show("masterData", "subscriptions"), ["#NewButton", "Es gibt noch kein Kalender-Abo."]),
        ("Handbuch", a => a.Show("masterData", "handbook"), ["#NewEntryButton", "Noch kein Eintrag im Handbuch."]),
        ("Behandlungen", a => a.Show("masterData", "treatments"), ["#NewButton", "#EmptyNewButton", "Noch keine Behandlung"]),
        ("Aktionen", a => a.Show("promotions"), ["#NewPromotionButton", "Noch keine Aktion angelegt."]),
        ("Team Personen", a => a.Show("masterData", "team", area: TeamViewModel.PeopleMode), []),
        ("Team Wochenplan", a => a.Show("masterData", "team", area: TeamViewModel.RosterMode), []),
        ("Team Zeiten", a => a.Show("masterData", "team", area: TeamViewModel.TimesMode), []),
        ("Team Lohn", a => a.Show("masterData", "team", area: TeamViewModel.PayrollMode), []),
        ("Team Rechte", a => a.Show("masterData", "team", area: TeamViewModel.RightsMode), []),
        ("Lager Übersicht", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Overview); }, []),
        ("Lager Bestand", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Stock); }, ["#NewItemButton", "Noch kein Material angelegt."]),
        ("Lager Chargen", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Tasks); }, []),
        ("Lager Bewegungen", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Movements); }, []),
        ("Lager Bestellen", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Orders); }, []),
        ("Lager Inventur", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Count); }, []),
        ("Lager Lagerorte", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Places); }, ["#NewPlaceButton"]),
        ("Lager Lieferanten", a => { a.Show("stock"); a.Stock!.SetTab(StockTab.Suppliers); }, ["#NewSupplierButton", "Noch kein Lieferant angelegt."]),
        ("Kassa", a => a.Show("masterData", "kassa"), []),
        ("Zahlungen", a => a.Show("masterData", "payments"), []),
        ("Kosten", a => a.Show("costs"), ["Noch kein Raum angelegt. Räume entstehen", "Noch niemand im Team."]),
        ("Buchhaltung", a => a.Show("accounting"), ["#AddAccountButton"]),
        ("Erinnerungen", a => a.Show("masterData", "reminders"), []),
        ("Postfach und Nummern", a => a.Show("masterData", "intake"), ["#AddRuleButton", "Keine Regel."]),
        ("KI", a => a.Show("masterData", "ai"), []),
        ("Zahlen Tagesstart", a => a.Show("reports", ReportsViewModel.BriefingTab), []),
        ("Zahlen Mein Tag", a => a.Show("reports", ReportsViewModel.DayTab), []),
        ("Zahlen Hygiene", a => a.Show("reports", ReportsViewModel.HygieneTab), []),
        ("Zahlen Berichte", a => a.Show("reports", ReportsViewModel.ManagementTab), ["Keine Umsätze im Zeitraum.", "Keine Dienste im Zeitraum.", "Keine Behandlungen im Zeitraum."]),
        ("Zahlen Pflichtberichte", a => a.Show("reports", ReportsViewModel.ComplianceTab), ["Keine Dienste im Zeitraum."]),
        ("Zahlen Betriebsinfo", a => a.Show("reports", ReportsViewModel.BusinessInfoTab), ["#NewBusinessContactButton", "Noch nichts eingetragen."]),
    ];

    /// <summary>Was die Ansicht nicht sichtbar zeigt, obwohl <paramref name="expect"/> es verlangt.</summary>
    private static List<string> MissingOnEmptyPage(Window window, string name, string[] expect)
    {
        var elements = window.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        return [.. expect.Where(e => e.StartsWith('#')
                ? !elements.Any(c => c.Name == e[1..])
                : !elements.OfType<TextBlock>().Any(t => t.Text?.Contains(e, StringComparison.Ordinal) == true))
            .Select(e => $"{name}: fehlt {(e.StartsWith('#') ? "Element" : "Satz")} „{e.TrimStart('#')}“")];
    }

    /// <summary>Hält die Ansicht als Bild fest, wenn der Lauf Bildschirmfotos schreiben soll (<c>AISPA_SCREENSHOTS</c>).</summary>
    private static async Task SaveEmptyFrame(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is not { Length: > 0 } folder)
        {
            return;
        }

        await Pump(Settling);
        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"{name}.png"), PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public Task Leerer_Betrieb_oeffnet_jede_Verwaltungsseite_ohne_Absturz_und_ohne_Bindungsfehler()
    {
        var api = EmptyBusinessApi();
        return Render("hell", "leerer-betrieb", async (window, main) =>
        {
            var problems = new List<string>();
            var visited = new HashSet<string>();
            await WithBindingLog(async log =>
            {
                var shell = await OpenEmptyAdmin(window, main);
                foreach (var (name, go, expect) in EmptyTour)
                {
                    await AssertNoBindingErrors(log, name, async () =>
                    {
                        go(shell.AdminPage);
                        await Pump(Settling);
                    });
                    problems.AddRange(VisibleErrors(window).Select(e => $"{name}: {e}"));
                    problems.AddRange(MissingOnEmptyPage(window, name, expect));
                    await SaveEmptyFrame(window, $"leer-{name.Replace(' ', '-').ToLowerInvariant()}");
                    visited.Add(shell.AdminPage.SelectedArea!.Title);
                }

                problems.AddRange(shell.AdminPage.Areas.Where(a => !visited.Contains(a.Title)).Select(a => $"nicht besucht: {a.Title} (fehlt in EmptyTour)"));
                log.AssertClean();
                return true;
            });
            problems.AddRange(api.Unrouted.Select(r => $"ohne Antwort: {r}"));
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            return main;
        }, api);
    }

    /// <summary>Bild einer leeren Seite im hellen Schema (Bildschirmfoto mit <c>AISPA_SCREENSHOTS</c>).</summary>
    private static Task<bool> RenderEmptyPage(string stop, string picture)
    {
        var (name, go, expect) = EmptyTour.Single(t => t.Name == stop);
        return Render("hell", picture, async (window, main) =>
        {
            var shell = await OpenEmptyAdmin(window, main);
            go(shell.AdminPage);
            await Pump(Settling);
            Assert.Empty(VisibleErrors(window));
            Assert.Empty(MissingOnEmptyPage(window, name, expect));
            return shell.AdminPage;
        }, EmptyBusinessApi());
    }

    [Fact]
    public Task Leerer_Betrieb_Behandlungen_zeigen_Leerzustand_mit_Anlegen() => RenderEmptyPage("Behandlungen", "verwaltung-leer-behandlungen");

    [Fact]
    public Task Leerer_Betrieb_Lager_Bestand_zeigt_Material_anlegen_ueber_der_Liste() => RenderEmptyPage("Lager Bestand", "verwaltung-leer-lager-bestand");

    [Fact]
    public Task Leerer_Betrieb_Buchhaltung_zeigt_das_Anlegen_der_Erloeskonten_ueber_der_Liste() => RenderEmptyPage("Buchhaltung", "verwaltung-leer-buchhaltung");

    [Fact]
    public Task Leerer_Betrieb_Standorte_zeigen_leere_Raeume_und_Geraete() => RenderEmptyPage("Standorte und Räume", "verwaltung-leer-standorte");
}

using System.Globalization;
using AispaStudio.Contracts;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beispieldaten der vier Masken aus R05 Slice 102 für Masken- und Fenstertests; alle Zahlen stehen fest im Stub, gerechnet wird in den API-Tests.</summary>
internal static class ReportSample
{
    public static readonly DateOnly Today = new(2026, 10, 19);
    public static readonly Guid LocationId = Vienna.Id;
    public static readonly Guid PersonId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid AppointmentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid StepId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid CustomerId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid RoomId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid CheckId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid ExtraId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid RegisterAt = Guid.Parse("80000000-0000-0000-0000-000000000001");
    public static readonly Guid RegisterDe = Guid.Parse("80000000-0000-0000-0000-000000000002");
    public static readonly Guid TseId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    public static readonly Guid TreatmentId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid ContactId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private static string Day => Iso(Today);

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ManagementRoute(DateOnly from, DateOnly until) =>
        $"GET /api/reports/locations/{LocationId}/management?from={Iso(from)}&until={Iso(until)}";

    public static string BriefingRoute => $"GET /api/reports/locations/{LocationId}/briefing?date={Day}";

    public static string ServingsRoute => $"GET /api/locations/{LocationId}/servings?date={Day}";

    public static string DayPlanRoute => $"GET /api/reports/locations/{LocationId}/day-plan?date={Day}&employeeId={PersonId}";

    public static string RoomStartRoute => $"GET /api/locations/{LocationId}/room-start?date={Day}";

    public static string RoomCareRoute => $"GET /api/locations/{LocationId}/room-care?date={Day}";

    public static string ApproversRoute => $"GET /api/locations/{LocationId}/room-approvers";

    public static string ComplianceRoute => $"GET /api/reports/locations/{LocationId}/compliance?date={Day}";

    public static string TseRoute => $"GET /api/pos/registers/{RegisterDe}/tse-exports";

    public static string BenchmarkRoute(int days) => $"GET /api/reports/locations/{LocationId}/benchmark?days={days}";

    public static DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 19, hour, minute, 0, TimeSpan.FromHours(2));

    public static ManagementReportDto Management(bool hidden = false) => new(LocationId, new DateOnly(2026, 10, 1), Today,
        [new Kpi("revenue", "Umsatz", 12345.5m, 10000m, "€"), new Kpi("utilization", "Auslastung", 61.5m, 58m, "%"), new Kpi("appointments", "Termine", 240m, null, "")],
        hidden ? [] : [new RevenueLine("Behandlungen", "SPA", 1200m, 1000m)],
        hidden ? [] : [new RevenueLine("Karte", null, 1200m, 1000m)],
        [new EmployeeKpi(PersonId, "Lisa", 80m, 60m, 75, hidden ? null : 50m)],
        [new TreatmentKpi(TreatmentId, "Klassische Massage", 12, hidden ? null : 900m, hidden ? null : -2.5m)],
        ["Auslastung unter Plan"], hidden);

    public static BenchmarkReport Benchmark() => new(LocationId, "Wien", new DateOnly(2026, 7, 21), Today, "EUR", "Österreich, ähnliche Größe", 24, true,
        [
            new BenchmarkMetric("utilization", "Auslastung", "%", 61.5m, 50m, 60m, 70m, true, BenchmarkRating.AboveMedian, null),
            new BenchmarkMetric("noShow", "No-Show-Quote", "%", 8m, 6m, 4m, 2m, false, BenchmarkRating.Bottom, "Anzahlung für Neukunden prüfen."),
        ],
        ["Vergleichswerte sind Beispieldaten."]);

    public static BriefingDto Briefing(bool pending = false) => new(LocationId, Today, "12 Termine, Auslastung 70 %.", 12, 70,
        [new BriefingStaff(PersonId, "Lisa", "im Dienst", "09:00–17:00")],
        [new BriefingItem(AppointmentId, At(9), "Anamnese fehlt bei Anna Berg")],
        [new BriefingItem(AppointmentId, At(9), "Anna Berg: Erstbesuch")],
        [],
        [new BriefingItem(null, null, "Kassa AT1 offen")],
        pending ? null : "Ein ruhiger Tag mit einem Erstbesuch.", pending, pending ? null : "Von der KI zusammengefasst.",
        [new ServingTotalDto("Früchteteller", 2, [At(11, 20), At(12, 40)])],
        [new HandbookEntryDto(Guid.Empty, "Schlüssel", "Saunaschlüssel liegt am Empfang.", "Empfang", true, [], true, At(8))]);

    public static ServingDayDto Servings(bool done = false) => new(LocationId, Today,
        [new ServingItemDto(AppointmentId, ExtraId, "Früchteteller", At(11, 20), "Klassische Massage", RoomId, "Raum 1", CustomerId, "Anna Berg", "Kräutertee", true, done, done ? At(11, 10) : null, done ? "Lisa" : null)],
        [new ServingTotalDto("Früchteteller", 1, [At(11, 20)])]);

    public static DayPlanDto DayPlan() => new(PersonId, "Lisa", Today, [new ShiftDto(At(9), At(17))],
        [
            new TreatmentCard(AppointmentId, At(10), At(11), AppointmentStatus.Booked, "Klassische Massage", 60, "Raum 1", null, "Lisa", CustomerId, "Anna Berg", 3, At(9), "Geburtstag", "Bitte ruhig",
                "Fragebogen fehlt", new CustomerPreferences("fest", "Lavendel", null, null, true, "Nacken", null, null),
                [new PastTreatment(At(9), "Klassische Massage", "Lisa", "Nacken verspannt")],
                [new TodoItem(StepId, "Steine aufheizen", At(9, 30), "Rezept", false), new TodoItem(null, "Lavendel bereitstellen", null, "Vorliebe", false)],
                "Nacken nachfragen"),
        ],
        ["Raumwechsel um 11:00"]);

    public static RoomStartDayDto RoomStart(RoomStartState state = RoomStartState.Partial, bool done = false) => new(LocationId, Today,
        [
            new RoomStartRoomDto(RoomId, "Raum 1", state, [new RoomStartItemStateDto(CheckId, "Liege desinfiziert", done, done ? At(8, 10) : null, done ? "Lisa" : null)], null, null,
                new RoomEquipmentDto(false, [new RestockLine(TreatmentId, "Massageöl", "ml", 200m, 500m, 300m, RestockState.Open)])),
        ]);

    public static RoomCareDay RoomCare(bool done = false) => new(
        [new RoomCareTaskDto(AppointmentId, [AppointmentId], RoomId, "Raum 1", "Raum 1 pflegen bis 11:15", At(11), At(11, 15), PersonId, done ? RoomCareState.Done : RoomCareState.Overdue, done ? At(11, 5) : null, done ? "Lisa" : null)],
        [new RoomDayStateDto(RoomId, "Raum 1", RoomStartState.Partial, 1, 0, null, null, 0, done ? 0 : 1, null, null)]);

    public static ComplianceReportDto Compliance(bool hidden = false) => new(LocationId, Today,
        [
            new RegisterCompliance(RegisterAt, "AT1", FiscalCountry.AT, true, "ABC123", At(8), At(8), null, ["Monatsbeleg bis Monatsende"], "/api/pos/registers/at1/dep"),
            new RegisterCompliance(RegisterDe, "DE1", FiscalCountry.DE, true, null, null, null, null, ["TSE-Export aufbewahren"], "/api/pos/registers/de1/dsfinvk"),
        ],
        hidden ? null : 450m, 3,
        [new WorkingTimeRecord(PersonId, "Lisa", 160.5m, 20, 2, 1, ["Pause fehlt am 3.10."])],
        ["Jahresbeleg bis 31.1."], 1, 4, hidden);

    public static TseExportDto Tse(TseExportStatus status = TseExportStatus.Done) =>
        new(TseId, RegisterDe, new DateOnly(2026, 1, 1), Today, status, At(9), status == TseExportStatus.Done ? At(9, 5) : null, status == TseExportStatus.Done ? "tse.tar" : null, 10, null);

    public static CostAnalysisDto CostAnalysis(string month = "2026-10") => new(LocationId, month, 12000m, 0.25m,
        [
            new TreatmentCostDto(TreatmentId, "Klassische Massage", 50, 60, 90m, 75m, [new CostPart("Material", 1.2m, "30 ml Massageöl")], 1.2m, 30m, 15m, 3m, 5m, 54.2m, 20.8m, 27.73m, 27, 561.6m, ["Stundensatz fehlt"]),
        ]);

    public static LocationCostSettingsDto CostSettings() => new(18m, 30m, 4000m, 65, 80);

    public static RoomCostDto RoomCost() => new(RoomId, "Raum 1", 800m, 1.5m);

    public static EmployeeCostDto EmployeeCost() => new(PersonId, "Lisa", 32m);

    public static StubApi AddCosts(StubApi api, string month) => api
        .On("GET /api/locations", new List<LocationDto> { Vienna, Salzburg })
        .On($"GET /api/costs/locations/{LocationId}/analysis?month={month}", CostAnalysis(month))
        .On($"GET /api/costs/locations/{LocationId}/settings", CostSettings())
        .On($"GET /api/costs/locations/{LocationId}/rooms", new List<RoomCostDto> { RoomCost() })
        .On($"GET /api/costs/locations/{LocationId}/employees", new List<EmployeeCostDto> { EmployeeCost() });

    public static BusinessContactDto Contact(bool archived = false, bool canEdit = true) => new(ContactId, BusinessContactCategory.Insurance, "Haftpflicht", "Versicherung AG", "Frau Maier", "+43 1 234 56 10", null, "service@versicherung.test",
        "Ringstraße 1, Wien", "Schäden", "K-1", "Versicherung AG", "P-77", "Betriebshaftpflicht", "+43 800 111", true, [], null, BusinessContactVisibility.Team, archived, canEdit);

    public static StubApi AddBusinessInfo(StubApi api) => api
        .On("GET /api/locations", new List<LocationDto> { Vienna, Salzburg })
        .On("GET /api/business-contacts?includeArchived=false", new List<BusinessContactDto> { Contact() })
        .On("GET /api/business-contacts?includeArchived=true", new List<BusinessContactDto> { Contact(), Contact(archived: true) with { Id = Guid.NewGuid(), Designation = "Alt" } });

    /// <summary>Alle Antworten der Berichte für die Inhaberin; einzelne Fälle ersetzen Routen danach mit <c>On</c>.</summary>
    public static StubApi AddReports(StubApi api) => api
        .On("GET /api/locations", new List<LocationDto> { Vienna, Salzburg })
        .On("GET /api/employees", new List<EmployeeDto> { new(PersonId, "Lisa", "#2f6f5e", [], []) })
        .On(ManagementRoute(new DateOnly(2026, 10, 1), Today), Management())
        .On(BenchmarkRoute(30), Benchmark())
        .On(BriefingRoute, Briefing())
        .On(ServingsRoute, Servings())
        .On(DayPlanRoute, DayPlan())
        .On(RoomStartRoute, RoomStart())
        .On(RoomCareRoute, RoomCare())
        .On(ApproversRoute, new List<QuickSwitchPerson> { new("lead", "Markus", StaffRole.Manager) })
        .On(ComplianceRoute, Compliance())
        .On(TseRoute, new List<TseExportDto> { Tse() });
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Rollen Manager und Backoffice sowie das Recht „Berichte mit Beträgen“ (ADR 0070): Standardrechte je Rolle, was der Inhaber allein darf,
/// was das Backoffice liest und nicht schreibt, und dass Berichte mit Beträgen im Endpunkt geprüft werden.
/// </summary>
public sealed class RolesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AccessRight[] All = Enum.GetValues<AccessRight>();

    [Fact]
    public void Standardrechte_je_Rolle()
    {
        AccessRight[] reception = [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ViewHealthData];
        AccessRight[] management =
        [
            AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ManageLocation, AccessRight.ManageCatalog,
            AccessRight.ManageStaff, AccessRight.ViewFinance, AccessRight.SuperviseCash, AccessRight.ViewHealthData,
        ];
        AccessRight[] therapist = [AccessRight.ViewCalendar, AccessRight.ChangeStatus, AccessRight.ViewHealthData];

        Assert.Equal(All, Roles.RightsOf(StaffRole.Owner));
        Assert.Equal(All, Roles.RightsOf(StaffRole.GeneralManager));
        Assert.Equivalent(management, Roles.RightsOf(StaffRole.Manager));
        Assert.Equivalent(reception, Roles.RightsOf(StaffRole.Reception));
        Assert.Equivalent(therapist, Roles.RightsOf(StaffRole.Therapist));
        Assert.Equivalent(
            new[] { AccessRight.ViewFinance, AccessRight.ViewAmountReports, AccessRight.ViewReceipts, AccessRight.ViewStaffRecords },
            Roles.RightsOf(StaffRole.Backoffice));

        // Das einzeln vergebene Recht: Rezeption und Standortleitung bekommen die Beträge (und die Berichte ohne Beträge dazu), sonst ändert sich nichts.
        Assert.Equivalent(reception.Concat([AccessRight.ViewFinance, AccessRight.ViewAmountReports]), Roles.RightsOf(StaffRole.Reception, amountReports: true));
        Assert.Equivalent(management.Concat([AccessRight.ViewAmountReports]), Roles.RightsOf(StaffRole.Manager, amountReports: true));
        Assert.Equivalent(therapist, Roles.RightsOf(StaffRole.Therapist, amountReports: true));
        Assert.Equal(All, Roles.RightsOf(StaffRole.Owner, amountReports: true));

        // Das Backoffice öffnet weder Kalender und Kunden noch das Kassieren; das Beträge-Recht der Standortleitung gibt es nicht ohne Vergabe.
        Assert.DoesNotContain(AccessRight.ViewCalendar, Roles.RightsOf(StaffRole.Backoffice));
        Assert.DoesNotContain(AccessRight.Book, Roles.RightsOf(StaffRole.Backoffice));
        Assert.DoesNotContain(AccessRight.ViewAmountReports, Roles.RightsOf(StaffRole.Manager));

        // Anamnese lesen (Ä309): wer Kunden sieht, liest auch die Anamnese; das Backoffice nie, auch nicht mit dem Recht „Berichte mit Beträgen“.
        Assert.DoesNotContain(AccessRight.ViewHealthData, Roles.RightsOf(StaffRole.Backoffice));
        Assert.DoesNotContain(AccessRight.ViewHealthData, Roles.RightsOf(StaffRole.Backoffice, amountReports: true));
    }

    [Fact]
    public void Verwalten_duerfen_Inhaber_alles_Manager_und_Standortleitung_nur_darunter()
    {
        UserAccess Of(StaffRole role, bool all = true) => new("u", role, all, all ? new HashSet<Guid>() : new HashSet<Guid> { Guid.Empty }, null);

        Assert.True(Of(StaffRole.Owner).CanManage(StaffRole.Owner, true, []));
        Assert.True(Of(StaffRole.Owner).CanManage(StaffRole.GeneralManager, true, []));
        Assert.False(Of(StaffRole.GeneralManager).CanManage(StaffRole.Owner, true, []));
        Assert.False(Of(StaffRole.GeneralManager).CanManage(StaffRole.GeneralManager, true, []));
        Assert.True(Of(StaffRole.GeneralManager).CanManage(StaffRole.Backoffice, true, []));
        Assert.True(Of(StaffRole.GeneralManager).CanManage(StaffRole.Manager, true, []));
        Assert.False(Of(StaffRole.Manager, all: false).CanManage(StaffRole.GeneralManager, false, [Guid.Empty]));
        Assert.True(Of(StaffRole.Manager, all: false).CanManage(StaffRole.Reception, false, [Guid.Empty]));
        Assert.False(Of(StaffRole.Backoffice).CanManage(StaffRole.Reception, true, []));
    }

    [Fact]
    public async Task Manager_hat_die_Rechte_des_Inhabers_und_darf_befristet_sein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await JoinAsync(spa, StaffRole.GeneralManager, expires: DateTimeOffset.UtcNow.AddDays(30));

        var me = await manager.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var location = await manager.PostAsJsonAsync("/api/locations", new LocationInput("Graz", "Europe/Vienna", "EUR", []), TestSpa.Json, Ct);
        var staff = await manager.GetFromJsonAsync<List<StaffMemberDto>>("/api/staff", TestSpa.Json, Ct);

        Assert.Equal(StaffRole.GeneralManager, me!.Access?.Role);
        Assert.Equal(All, me.Rights);
        Assert.Equal(HttpStatusCode.Created, location.StatusCode);
        Assert.Contains(staff!, m => m.UserId == me.Id && m.ExpiresAt is not null);
    }

    [Fact]
    public async Task Inhaber_bleibt_Eigentuemer_Manager_aendert_keine_Inhaber_und_ernennt_keine_Manager()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var owner = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);
        using var manager = await JoinAsync(spa, StaffRole.GeneralManager);
        using var other = await JoinAsync(spa, StaffRole.GeneralManager);
        var otherId = (await other.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;

        var visible = await manager.GetFromJsonAsync<List<StaffMemberDto>>("/api/staff", TestSpa.Json, Ct);
        var changeOwner = await manager.PutAsJsonAsync($"/api/staff/{owner.Id}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true), TestSpa.Json, Ct);
        var changeManager = await manager.PutAsJsonAsync($"/api/staff/{otherId}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, false), TestSpa.Json, Ct);
        var inviteManager = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"m-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.GeneralManager, true, [], null), TestSpa.Json, Ct);
        var inviteOwner = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"o-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.Owner, true, [], null), TestSpa.Json, Ct);
        var inviteBackoffice = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"b-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.Backoffice, true, [], null), TestSpa.Json, Ct);

        // Der Inhaber darf alles davon.
        var ownerChangesManager = await spa.Client.PutAsJsonAsync($"/api/staff/{otherId}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true), TestSpa.Json, Ct);

        Assert.DoesNotContain(visible!, m => m.UserId == owner.Id || m.UserId == otherId);
        Assert.Equal(HttpStatusCode.NotFound, changeOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changeManager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inviteManager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inviteOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inviteBackoffice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerChangesManager.StatusCode);
    }

    [Fact]
    public async Task Standortleitung_vergibt_weder_Manager_noch_das_Recht_Berichte_mit_Betraegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var leitung = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await JoinAsync(spa, StaffRole.Reception, allLocations: false);
        var receptionId = (await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;

        var manager = await leitung.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"m-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.GeneralManager, false, [spa.Location.Id], null), TestSpa.Json, Ct);
        var grant = await leitung.PutAsJsonAsync($"/api/staff/{receptionId}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true, AmountReports: true), TestSpa.Json, Ct);
        var inviteWithGrant = await leitung.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"r-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.Reception, false, [spa.Location.Id], null, AmountReports: true),
            TestSpa.Json, Ct);
        var plain = await leitung.PutAsJsonAsync($"/api/staff/{receptionId}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, manager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, grant.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inviteWithGrant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
    }

    [Fact]
    public async Task Backoffice_nur_fuer_alle_Standorte_und_Beträge_Recht_nur_fuer_Rezeption_und_Standortleitung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var limited = await spa.Client.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"b-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.Backoffice, false, [spa.Location.Id], null), TestSpa.Json, Ct);
        using var therapist = await JoinAsync(spa, StaffRole.Therapist, allLocations: false, amountReports: true, employee: spa.Anna.Id);
        var rights = (await therapist.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Rights;
        var staff = await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, limited.StatusCode);
        Assert.DoesNotContain(AccessRight.ViewAmountReports, rights);
        Assert.All(staff.Where(m => m.Role == StaffRole.Therapist), m => Assert.False(m.AmountReports));
    }

    [Fact]
    public async Task Backoffice_liest_Berichte_mit_Betraegen_Belege_Dienstplan_und_Lohn()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, receipt) = await SellTeaAsync(spa);
        using var backoffice = await JoinAsync(spa, StaffRole.Backoffice);
        var locationId = spa.Location.Id;
        var today = $"{DateTime.UtcNow:yyyy-MM-dd}";

        var reads = new[]
        {
            $"/api/reports/locations/{locationId}/management?from={today}&until={today}",
            $"/api/reports/locations/{locationId}/compliance?date={today}",
            $"/api/costs/locations/{locationId}/settings",
            "/api/accounting/settings",
            "/api/pos/revenue-accounts",
            $"/api/pos/registers?locationId={locationId}",
            $"/api/pos/registers/{register.Id}/receipts?date={today}",
            $"/api/pos/receipts/{receipt.Id}",
            $"/api/pos/registers/{register.Id}/day-closings",
            $"/api/pos/registers/{register.Id}/dep",
            $"/api/locations/{locationId}/roster?from={TestSpa.Monday:yyyy-MM-dd}",
            $"/api/locations/{locationId}/timesheet?from={TestSpa.Monday:yyyy-MM-dd}",
            "/api/employees",
            "/api/locations",
            "/api/payroll/employees",
            "/api/payroll/preview?year=2027&month=3",
        };
        var code = await backoffice.PostAsync("/api/auth/device-code", null, Ct);
        Assert.Equal(HttpStatusCode.OK, code.StatusCode);
        foreach (var path in reads)
        {
            Assert.True((await backoffice.GetAsync(path, Ct)).StatusCode == HttpStatusCode.OK, path);
        }

        var management = await backoffice.GetFromJsonAsync<ManagementReportDto>(reads[0], TestSpa.Json, Ct);
        var compliance = await backoffice.GetFromJsonAsync<ComplianceReportDto>(reads[1], TestSpa.Json, Ct);
        Assert.False(management!.AmountsHidden);
        Assert.Contains(management.Kpis, k => k.Key == "revenue");
        Assert.False(compliance!.AmountsHidden);
        Assert.NotNull(compliance.VoucherLiabilities);
    }

    [Fact]
    public async Task Backoffice_bucht_nicht_kassiert_nicht_und_aendert_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, _) = await SellTeaAsync(spa);
        using var backoffice = await JoinAsync(spa, StaffRole.Backoffice);
        var locationId = spa.Location.Id;

        var denied = new Dictionary<string, Task<HttpResponseMessage>>
        {
            ["Termin buchen"] = backoffice.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
                locationId, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(10), null, null, null, null), TestSpa.Json, Ct),
            ["Kalender lesen"] = backoffice.GetAsync($"/api/locations/{locationId}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct),
            ["Kunden lesen"] = backoffice.GetAsync("/api/customers", Ct),
            ["Rechnung eröffnen"] = backoffice.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, []), TestSpa.Json, Ct),
            ["Offene Rechnungen"] = backoffice.GetAsync($"/api/pos/registers/{register.Id}/bills", Ct),
            ["Tagesabschluss"] = backoffice.PostAsJsonAsync($"/api/pos/registers/{register.Id}/day-closings", new DayClosingInput(0, null), TestSpa.Json, Ct),
            ["Zwischenbericht"] = backoffice.GetAsync($"/api/pos/registers/{register.Id}/day-report", Ct),
            ["Kosten ändern"] = backoffice.PutAsJsonAsync($"/api/costs/locations/{locationId}/settings",
                new LocationCostSettingsDto(0, 0, 0, 70, 80), TestSpa.Json, Ct),
            ["Dienstplan ändern"] = backoffice.PutAsJsonAsync($"/api/locations/{locationId}/roster/{spa.Anna.Id}/{TestSpa.Monday:yyyy-MM-dd}",
                new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null), TestSpa.Json, Ct),
            ["Person ändern"] = backoffice.PutAsJsonAsync($"/api/employees/{spa.Anna.Id}", new EmployeeInput("Anna", "#2f6f5e", [spa.Massage.Id], []), TestSpa.Json, Ct),
            ["Personalnummer"] = backoffice.PutAsJsonAsync($"/api/payroll/employees/{spa.Anna.Id}", new PersonnelNumberInput("4711"), TestSpa.Json, Ct),
            ["Zugänge"] = backoffice.GetAsync("/api/staff", Ct),
            ["Standort anlegen"] = backoffice.PostAsJsonAsync("/api/locations", new LocationInput("Graz", "Europe/Vienna", "EUR", []), TestSpa.Json, Ct),
            ["Einstellungen"] = backoffice.PutAsJsonAsync("/api/pos/settings", new PosSettingsInput(10), TestSpa.Json, Ct),
        };
        foreach (var (what, request) in denied)
        {
            Assert.True((await request).StatusCode == HttpStatusCode.Forbidden, what);
        }
    }

    [Fact]
    public async Task Rezeption_bekommt_Berichte_mit_Betraegen_nur_mit_dem_Recht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (register, _) = await SellTeaAsync(spa);
        using var reception = await JoinAsync(spa, StaffRole.Reception);
        var receptionId = (await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;
        var today = $"{DateTime.UtcNow:yyyy-MM-dd}";
        var management = $"/api/reports/locations/{spa.Location.Id}/management?from={today}&until={today}";
        string[] amountReports =
        [
            management,
            $"/api/reports/locations/{spa.Location.Id}/compliance?date={today}",
            $"/api/costs/locations/{spa.Location.Id}/settings",
            "/api/accounting/settings",
            $"/api/pos/registers/{register.Id}/dep",
            $"/api/pos/registers/{register.Id}/day-closings",
        ];

        foreach (var path in amountReports)
        {
            Assert.True((await reception.GetAsync(path, Ct)).StatusCode == HttpStatusCode.Forbidden, $"ohne Recht: {path}");
        }

        var grant = await spa.Client.PutAsJsonAsync($"/api/staff/{receptionId}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true, AmountReports: true), TestSpa.Json, Ct);
        grant.EnsureSuccessStatusCode();
        foreach (var path in amountReports)
        {
            Assert.True((await reception.GetAsync(path, Ct)).StatusCode == HttpStatusCode.OK, $"mit Recht: {path}");
        }

        var report = await reception.GetFromJsonAsync<ManagementReportDto>(management, TestSpa.Json, Ct);
        var me = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var editCosts = await reception.PutAsJsonAsync($"/api/costs/locations/{spa.Location.Id}/settings",
            new LocationCostSettingsDto(0, 0, 0, 70, 80), TestSpa.Json, Ct);
        var bill = await reception.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, []), TestSpa.Json, Ct);
        Assert.False(report!.AmountsHidden);
        Assert.Contains(AccessRight.ViewAmountReports, me!.Rights);
        Assert.Equal(HttpStatusCode.Forbidden, editCosts.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bill.StatusCode);

        // Entzogen wirkt sofort.
        (await spa.Client.PutAsJsonAsync($"/api/staff/{receptionId}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync(management, Ct)).StatusCode);
    }

    [Fact]
    public async Task Standortleitung_sieht_Kennzahlen_ohne_Betraege_bis_ihr_das_Recht_vergeben_wird()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SellTeaAsync(spa);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);
        using var leitung = await JoinAsync(spa, StaffRole.Manager);
        var leitungId = (await leitung.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id;
        var today = $"{DateTime.UtcNow:yyyy-MM-dd}";
        var management = $"/api/reports/locations/{spa.Location.Id}/management?from={TestSpa.Monday:yyyy-MM-dd}&until={TestSpa.Monday.AddDays(6):yyyy-MM-dd}";
        var compliance = $"/api/reports/locations/{spa.Location.Id}/compliance?date={today}";

        var hidden = await leitung.GetFromJsonAsync<ManagementReportDto>(management, TestSpa.Json, Ct);
        var hiddenCompliance = await leitung.GetFromJsonAsync<ComplianceReportDto>(compliance, TestSpa.Json, Ct);
        var costs = await leitung.GetAsync($"/api/costs/locations/{spa.Location.Id}/settings", Ct);
        var accounting = await leitung.GetAsync("/api/accounting/settings", Ct);

        Assert.True(hidden!.AmountsHidden);
        Assert.DoesNotContain(hidden.Kpis, k => k.Unit == "€");
        Assert.Contains(hidden.Kpis, k => k.Key == "staffUtilization");
        Assert.Contains(hidden.Kpis, k => k.Key == "noShowRate");
        Assert.Empty(hidden.RevenueByAccount);
        Assert.Empty(hidden.RevenueByPayment);
        Assert.All(hidden.Treatments, t => Assert.Null(t.NetRevenue));
        Assert.All(hidden.Treatments, t => Assert.Null(t.MarginPerAppointment));
        Assert.All(hidden.Employees, e => Assert.Null(e.RevenuePerHour));
        Assert.DoesNotContain(hidden.Alerts, a => a.Contains('€', StringComparison.Ordinal));
        Assert.True(hiddenCompliance!.AmountsHidden);
        Assert.Null(hiddenCompliance.VoucherLiabilities);
        Assert.NotEmpty(hiddenCompliance.WorkingTime);
        Assert.Equal(HttpStatusCode.Forbidden, costs.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, accounting.StatusCode);

        (await spa.Client.PutAsJsonAsync($"/api/staff/{leitungId}",
            new StaffAccessInput(StaffRole.Manager, true, [], null, true, AmountReports: true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        var shown = await leitung.GetFromJsonAsync<ManagementReportDto>(management, TestSpa.Json, Ct);
        var editCosts = await leitung.PutAsJsonAsync($"/api/costs/locations/{spa.Location.Id}/settings",
            new LocationCostSettingsDto(0, 0, 0, 70, 80), TestSpa.Json, Ct);

        Assert.False(shown!.AmountsHidden);
        Assert.Contains(shown.Kpis, k => k.Key == "revenue");
        Assert.NotEmpty(shown.Treatments);
        Assert.All(shown.Treatments, t => Assert.NotNull(t.NetRevenue));
        Assert.Equal(HttpStatusCode.OK, editCosts.StatusCode);
    }

    [Fact]
    public async Task Ohne_Betraege_sind_Behandlungen_nach_Anzahl_geordnet()
    {
        var shown = new ManagementReportDto(Guid.Empty, TestSpa.Monday, TestSpa.Monday,
            [new("revenue", "Umsatz brutto", 10, 0, "€"), new("appointments", "Behandlungen", 3, 1, "")],
            [new("4000", null, 10, 8)], [new("Bar", null, 10, 10)],
            [new(Guid.Empty, "Anna", 8, 4, 50, 33.5m)],
            [new(Guid.Empty, "Teuer", 1, 500, 100), new(Guid.Empty, "Beliebt", 5, 100, 10)],
            ["No-Show-Quote 12 %: Anzahlung erwägen.", "Kassendifferenzen im Zeitraum zusammen 3.00 €."]);

        var hidden = Reporting.ManagementReports.WithoutAmounts(shown);

        Assert.Equal(["Beliebt", "Teuer"], hidden.Treatments.Select(t => t.Name));
        Assert.Equal(["appointments"], hidden.Kpis.Select(k => k.Key));
        Assert.Equal(["No-Show-Quote 12 %: Anzahlung erwägen."], hidden.Alerts);
        Assert.True(hidden.AmountsHidden);
    }

    /// <summary>Legt in einem neuen Konto eine Person der Rolle an (Einladung des Inhabers, Annahme) und liefert deren Client.</summary>
    private async Task<HttpClient> JoinAsync(TestSpa spa, StaffRole role, bool allLocations = true, bool amountReports = false,
        DateTimeOffset? expires = null, Guid? employee = null)
    {
        var email = $"{role}-{Guid.NewGuid():N}@aispa.test".ToLowerInvariant();
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, $"{role} Person", role, allLocations, allLocations ? [] : [spa.Location.Id], employee, expires, amountReports), Ct);
        using var anonymous = fixture.CreateClient();
        var accepted = await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        accepted.EnsureSuccessStatusCode();
        return fixture.CreateClient(string.Join("; ", accepted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    /// <summary>Eine Kasse mit einem bezahlten Beleg über Tee, damit Berichte und Belegabfragen etwas zeigen.</summary>
    private static async Task<(CashRegisterDto Register, ReceiptDto Receipt)> SellTeaAsync(TestSpa spa)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"R-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 12.50m, null, null)]), Ct);
        return (register, receipt);
    }
}

using System.Globalization;
using System.Text.Json;
using AispaStudio.Api.Costs;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Management-Cockpit und Pflichtberichte. Umsätze kommen aus den signierten Belegen (nie aus Schätzungen),
/// Termine und Auslastung aus Kalender und Dienstplan, Margen aus der Vollkostenrechnung.
/// </summary>
internal sealed partial class ManagementReports(AispaDbContext db, RosterService roster, CostAnalysis costs, TimeProvider time)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private sealed record Period(
        decimal Gross, decimal Net, int Sales, int Appointments, int NoShows, int Cancellations, decimal StaffUtilization,
        decimal RoomUtilization, int Customers, int NewCustomers, int Returning, decimal Rebooking);

    /// <summary>
    /// Kennzahlen eines Zeitraums. Ohne <paramref name="amounts"/> (Recht „Berichte mit Beträgen“) fehlen alle Geldbeträge schon hier,
    /// nicht erst in der Oberfläche.
    /// </summary>
    public async Task<ManagementReportDto?> ManagementAsync(Guid locationId, DateOnly from, DateOnly until, bool amounts, CancellationToken ct) =>
        await ManagementAsync(locationId, from, until, ct) is { } report ? amounts ? report : WithoutAmounts(report) : null;

    /// <summary>Entfernt Geldbeträge: Umsatz-Kennzahlen, Umsatz nach Konto und Zahlart, Umsatz je Person und Behandlung, Margen und Warnungen mit Beträgen.</summary>
    internal static ManagementReportDto WithoutAmounts(ManagementReportDto report) =>
        report with
        {
            Kpis = [.. report.Kpis.Where(k => k.Unit != "€")],
            RevenueByAccount = [],
            RevenueByPayment = [],
            Employees = [.. report.Employees.Select(e => e with { RevenuePerHour = null })],
            Treatments = [.. report.Treatments.OrderByDescending(t => t.Appointments).ThenBy(t => t.Name, StringComparer.CurrentCulture)
                .Select(t => t with { NetRevenue = null, MarginPerAppointment = null })],
            Alerts = [.. report.Alerts.Where(a => !a.Contains('€'))],
            AmountsHidden = true,
        };

    internal async Task<ManagementReportDto?> ManagementAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var length = until.DayNumber - from.DayNumber + 1;
        var current = await PeriodAsync(location, zone, from, until, ct);
        var previous = await PeriodAsync(location, zone, from.AddDays(-length), from.AddDays(-1), ct);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var registerIds = await db.CashRegisters.Where(r => r.LocationId == locationId).Select(r => r.Id).ToListAsync(ct);

        var lines = await db.Receipts.AsNoTracking()
            .Where(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .SelectMany(r => r.Lines)
            .ToListAsync(ct);
        var accounts = await db.RevenueAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var byAccount = lines.GroupBy(l => l.RevenueAccountId)
            .Select(g => new RevenueLine($"{accounts[g.Key].Code} {accounts[g.Key].Name}", accounts[g.Key].CostCenter, g.Sum(l => l.Amount),
                TaxRates.NetSum(g, l => l.Amount, l => l.TaxPercent)))
            .OrderByDescending(l => l.Gross)
            .ToList();
        var payments = await db.Receipts.AsNoTracking()
            .Where(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .SelectMany(r => r.Payments)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        var byPayment = payments.OrderBy(p => p.Method)
            .Select(p => new RevenueLine(PaymentMethods.Label(p.Method), null, p.Amount, p.Amount))
            .ToList();

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= start && a.Start < end && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.TreatmentId, Treatment = a.Treatment.Name, a.EmployeeId, a.Start, a.End, a.Price })
            .ToListAsync(ct);
        var analysis = await costs.AnalyzeAsync(locationId, until.Year, until.Month, ct);
        var unitCosts = analysis?.Treatments.ToDictionary(t => t.TreatmentId) ?? [];
        var country = await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive)
            .OrderBy(r => r.Code).Select(r => (FiscalCountry?)r.Country).FirstOrDefaultAsync(ct) ?? FiscalCountry.AT;
        var categories = await db.TreatmentPosSettings.AsNoTracking().ToDictionaryAsync(t => t.TreatmentId, t => t.TaxCategory, ct);
        decimal Net(Guid treatmentId, decimal price) =>
            TaxRates.TryGet(country, categories.GetValueOrDefault(treatmentId, TaxCategory.Normal), out var percent) ? TaxRates.Net(price, percent) : price;
        var treatments = appointments.GroupBy(a => (a.TreatmentId, a.Treatment))
            .Select(g =>
            {
                var cost = unitCosts.GetValueOrDefault(g.Key.TreatmentId);
                var net = g.Sum(a => Net(a.TreatmentId, a.Price));
                return new TreatmentKpi(g.Key.TreatmentId, g.Key.Treatment, g.Count(), net,
                    cost is null ? null : Math.Round(net / g.Count() - cost.Total, 2));
            })
            .OrderByDescending(t => t.NetRevenue)
            .ToList();

        var employees = await roster.EmployeesAt(locationId, from).AsNoTracking().Include(e => e.WorkingHours).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var employeeKpis = employees.Select(e =>
            {
                var shiftHours = (decimal)Enumerable.Range(0, length).Select(i => plans[(e.Id, from.AddDays(i))])
                    .SelectMany(p => p.Shifts.Where(s => s.LocationId == locationId)).Sum(s => (s.End!.Value - s.Start!.Value).TotalHours);
                var own = appointments.Where(a => a.EmployeeId == e.Id).ToList();
                var treatmentHours = (decimal)own.Sum(a => (a.End - a.Start).TotalHours);
                var revenue = own.Sum(a => Net(a.TreatmentId, a.Price));
                return new EmployeeKpi(e.Id, e.InHouseName, Math.Round(shiftHours, 1), Math.Round(treatmentHours, 1),
                    shiftHours > 0 ? (int)Math.Round(treatmentHours / shiftHours * 100) : 0,
                    treatmentHours > 0 ? Math.Round(revenue / treatmentHours, 2) : 0, own.Count);
            })
            .Where(k => k.ShiftHours > 0 || k.TreatmentHours > 0)
            .ToList();

        var vouchers = await db.Vouchers.AsNoTracking().Where(v => v.Status == VoucherStatus.Active).SumAsync(v => v.Balance, ct);
        var closings = await db.DayClosings.AsNoTracking()
            .Where(c => registerIds.Contains(c.RegisterId) && c.Until >= start && c.Until < end)
            .Select(c => c.Report)
            .ToListAsync(ct);
        var cashDifference = closings.Select(r => JsonSerializer.Deserialize<DayClosingDto>(r, PosJson.Options)!).Sum(c => Math.Abs(c.Difference));

        // Mails der Terminannahme, über die die Rezeption noch entscheiden muss: Stand jetzt, nicht je Zeitraum, deshalb ohne Vergleich.
        var openRequests = await db.InboundMails.AsNoTracking()
            .CountAsync(m => m.Status == InboundMailStatus.NeedsReview && (m.LocationId == locationId || m.LocationId == null), ct);

        var all = current.Appointments + current.NoShows + current.Cancellations;
        var previousAll = previous.Appointments + previous.NoShows + previous.Cancellations;
        decimal Rate(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);
        var kpis = new List<Kpi>
        {
            new("revenue", "Umsatz brutto", current.Gross, previous.Gross, "€"),
            new("revenueNet", "Umsatz netto", current.Net, previous.Net, "€"),
            new("averageTicket", "Durchschnittsbon netto", current.Sales == 0 ? 0 : Math.Round(current.Net / current.Sales, 2),
                previous.Sales == 0 ? 0 : Math.Round(previous.Net / previous.Sales, 2), "€"),
            new("appointments", "Behandlungen", current.Appointments, previous.Appointments, ""),
            new("staffUtilization", "Auslastung Personal", current.StaffUtilization, previous.StaffUtilization, "%"),
            new("roomUtilization", "Auslastung Räume", current.RoomUtilization, previous.RoomUtilization, "%"),
            new("noShowRate", "No-Show-Quote", Rate(current.NoShows, all), Rate(previous.NoShows, previousAll), "%"),
            new("cancellationRate", "Storno-Quote", Rate(current.Cancellations, all), Rate(previous.Cancellations, previousAll), "%"),
            new("customers", "Gäste", current.Customers, previous.Customers, ""),
            new("newCustomers", "Neue Gäste", current.NewCustomers, previous.NewCustomers, ""),
            new("returningRate", "Stammgäste-Anteil", Rate(current.Returning, current.Customers), Rate(previous.Returning, previous.Customers), "%"),
            new("rebookingRate", "Folgetermin gebucht", current.Rebooking, previous.Rebooking, "%"),
            new("voucherLiabilities", "Offene Gutscheine (heute)", vouchers, null, "€"),
            new("openRequests", "Offene Anfragen (heute)", openRequests, null, ""),
        };

        var alerts = new List<string>();
        if (Rate(current.NoShows, all) > 10)
        {
            alerts.Add($"No-Show-Quote {Rate(current.NoShows, all).ToString("0.#", German)} %: Anzahlung oder Ausfallgebühr erwägen.");
        }

        if (current.StaffUtilization is > 0 and < 50)
        {
            alerts.Add($"Personal nur zu {current.StaffUtilization.ToString("0", German)} % ausgelastet: Dienstplan oder Angebote prüfen.");
        }

        alerts.AddRange(treatments.Where(t => t.MarginPerAppointment < 0)
            .Select(t => $"{t.Name} bringt je Termin {(-t.MarginPerAppointment!.Value).ToString("0.00", German)} € Verlust (Vollkosten)."));
        if (cashDifference != 0)
        {
            alerts.Add($"Kassendifferenzen im Zeitraum zusammen {cashDifference.ToString("0.00", German)} €.");
        }

        return new(locationId, from, until, kpis, byAccount, byPayment, employeeKpis, treatments, alerts, NoShows: current.NoShows, Cancellations: current.Cancellations);
    }

    private async Task<Period> PeriodAsync(Location location, TimeZoneInfo zone, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var registerIds = await db.CashRegisters.Where(r => r.LocationId == location.Id).Select(r => r.Id).ToListAsync(ct);
        var receiptLines = await db.Receipts.AsNoTracking()
            .Where(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .SelectMany(r => r.Lines.Select(l => new { l.Amount, l.TaxPercent }))
            .ToListAsync(ct);
        var gross = receiptLines.Sum(l => l.Amount);
        var net = TaxRates.NetSum(receiptLines, l => l.Amount, l => l.TaxPercent);
        var sales = await db.Receipts.CountAsync(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && r.Kind == ReceiptKind.Sale, ct)
            - await db.Receipts.CountAsync(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && r.Kind == ReceiptKind.Void, ct);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == location.Id && a.Start >= start && a.Start < end)
            .Select(a => new { a.Id, a.CustomerId, a.EmployeeId, a.Status, a.Start, a.End, a.BlockedFrom, a.BlockedUntil })
            .ToListAsync(ct);
        var held = appointments.Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow).ToList();

        var employees = await roster.EmployeesAt(location.Id, from).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var shiftMinutes = plans.Values.SelectMany(p => p.Shifts.Where(s => s.LocationId == location.Id)).Sum(s => (s.End!.Value - s.Start!.Value).TotalMinutes);
        var treatmentMinutes = held.Sum(a => (a.End - a.Start).TotalMinutes);

        var rooms = await db.Rooms.CountAsync(r => r.LocationId == location.Id && r.IsActive, ct);
        var openMinutes = OpenMinutes(location, from, until);

        var roomMinutes = held.Sum(a => (a.BlockedUntil - a.BlockedFrom).TotalMinutes);

        var customerIds = held.Select(a => a.CustomerId).Distinct().ToList();
        var firstVisits = await db.Appointments.AsNoTracking()
            .Where(a => customerIds.Contains(a.CustomerId) && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.CustomerId)
            .Select(g => new { CustomerId = g.Key, First = g.Min(a => a.Start) })
            .ToListAsync(ct);
        var newCustomers = firstVisits.Count(f => f.First >= start);
        var completed = held.Where(a => a.Status == AppointmentStatus.Completed).Select(a => a.CustomerId).Distinct().ToList();
        var rebooked = await db.Appointments.AsNoTracking()
            .Where(a => completed.Contains(a.CustomerId) && a.Start >= end && a.Status == AppointmentStatus.Booked)
            .Select(a => a.CustomerId).Distinct().CountAsync(ct);

        return new(
            gross,
            net,
            sales,
            held.Count,
            appointments.Count(a => a.Status == AppointmentStatus.NoShow),
            appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            shiftMinutes > 0 ? Math.Round((decimal)(treatmentMinutes / shiftMinutes * 100), 1) : 0,
            openMinutes > 0 && rooms > 0 ? Math.Round((decimal)(roomMinutes / (openMinutes * rooms) * 100), 1) : 0,
            customerIds.Count,
            newCustomers,
            customerIds.Count - newCustomers,
            completed.Count == 0 ? 0 : Math.Round(rebooked * 100m / completed.Count, 1));
    }

    public async Task<ComplianceReportDto?> ComplianceAsync(Guid locationId, DateOnly date, bool amounts, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var registers = new List<RegisterCompliance>();
        var reminders = new List<string>();
        foreach (var r in await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId).OrderBy(r => r.Code).ToListAsync(ct))
        {
            var special = await db.Receipts.AsNoTracking().Where(x => x.RegisterId == r.Id && (x.Kind == ReceiptKind.Start || x.Kind == ReceiptKind.Monthly || x.Kind == ReceiptKind.Yearly))
                .GroupBy(x => x.Kind).Select(g => new { Kind = g.Key, Last = g.Max(x => x.IssuedAt) }).ToListAsync(ct);
            DateTimeOffset? Last(ReceiptKind kind) => special.FirstOrDefault(s => s.Kind == kind)?.Last;
            var duties = new List<string>();
            string? export = null;
            switch (r.Country)
            {
                case FiscalCountry.AT:
                    duties.Add("Registrierkasse und Signatureinrichtung in FinanzOnline registriert halten; Startbeleg mit der BMF-Belegcheck-App prüfen.");
                    duties.Add("Jahresbeleg (Dezember-Monatsbeleg) ausdrucken, 7 Jahre aufbewahren und bis 15. Februar mit der Belegcheck-App prüfen.");
                    duties.Add("Datenerfassungsprotokoll (DEP) mindestens vierteljährlich extern sichern.");
                    if (r.SignatureDeviceFailedSince is { } failed && now - failed > TimeSpan.FromHours(48))
                    {
                        reminders.Add($"Kassa {r.Code}: Ausfall der Signatureinrichtung seit mehr als 48 Stunden, unverzüglich in FinanzOnline melden.");
                    }

                    export = $"{ApiVersion.Current}/pos/registers/{r.Id}/dep";
                    break;
                case FiscalCountry.DE:
                    duties.Add("Kasse und TSE innerhalb eines Monats nach Anschaffung über ELSTER melden (§ 146a Abs. 4 AO), ebenso die Außerbetriebnahme.");
                    duties.Add("DSFinV-K-Export für Kassen-Nachschau und Außenprüfung bereithalten.");
                    export = FormattableString.Invariant($"{ApiVersion.Current}/pos/registers/{r.Id}/dsfinvk?from={new DateOnly(date.Year, 1, 1):yyyy-MM-dd}&until={date:yyyy-MM-dd}");
                    break;
                case FiscalCountry.CH:
                    duties.Add("Belege und Journal 10 Jahre aufbewahren (GeBüV); Grundlage für die MWST-Abrechnung je Quartal bzw. Semester.");
                    break;
            }

            registers.Add(new(r.Id, r.Code, r.Country, r.IsActive, r.CertificateSerial, Last(ReceiptKind.Start), Last(ReceiptKind.Monthly),
                Last(ReceiptKind.Yearly), duties, export));
        }

        if (registers.Any(r => r.Country == FiscalCountry.AT && r.IsActive) && date.Month <= 2)
        {
            reminders.Add($"Bis 15.02.{date.Year}: Jahresbeleg {date.Year - 1} jeder österreichischen Kasse mit der Belegcheck-App prüfen.");
        }

        var vouchers = await db.Vouchers.AsNoTracking().Where(v => v.Status == VoucherStatus.Active && v.Balance > 0)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Sum = g.Sum(v => v.Balance) }).FirstOrDefaultAsync(ct);
        var packages = await db.Vouchers.AsNoTracking().Where(v => v.Status == VoucherStatus.Active && v.Units != null && v.UnitsLeft > 0)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Units = g.Sum(v => v.UnitsLeft!.Value) }).FirstOrDefaultAsync(ct);

        var first = new DateOnly(date.Year, date.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var employees = await roster.EmployeesAt(locationId, first).AsNoTracking().Include(e => e.WorkingHours).OrderBy(e => e.DisplayName).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, first.AddDays(-1), last, ct);
        var rules = await roster.RulesAsync(ct);
        var records = employees.Select(e =>
            {
                var days = Enumerable.Range(0, last.Day).Select(i => plans[(e.Id, first.AddDays(i))]).ToList();
                var warnings = days.SelectMany(d => WorkingTime.DayWarnings(d, plans.GetValueOrDefault((e.Id, d.Date.AddDays(-1))), rules)
                        .Select(w => $"{d.Date.ToString("dd.MM.", German)} {w}"))
                    .Concat(WorkingTime.WeekWarnings(days.Sum(d => d.Hours) / days.Count * 7, days, rules))
                    .ToList();
                return new WorkingTimeRecord(e.Id, e.DisplayName, Math.Round((decimal)days.Sum(d => d.Hours), 1), days.Count(d => d.Shifts.Any()),
                    days.Count(d => d.Entries.Any(x => x.Kind == RosterKind.Vacation)), days.Count(d => d.Entries.Any(x => x.Kind == RosterKind.Sick)), warnings);
            })
            .ToList();

        return new(locationId, date, registers, amounts ? vouchers?.Sum ?? 0 : null, vouchers?.Count ?? 0, records, reminders,
            packages?.Count ?? 0, packages?.Units ?? 0, !amounts);
    }

}

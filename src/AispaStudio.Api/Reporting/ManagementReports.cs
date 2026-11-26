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
    /// <summary>Ab dieser Summe der Kassendifferenzen (Beträge, Überschuss wie Fehlbetrag) im Zeitraum steht eine Warnung im Bericht.</summary>
    private const decimal CashDifferenceThreshold = 5.00m;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>
    /// Zahlen eines Zeitraums. <c>ServiceNet</c> und <c>ServiceSales</c> lassen den Verkauf von Wertgutscheinen (Mehrzweck, Konto
    /// <see cref="PosModel.MultiPurposeVouchers"/>) weg: Er ist eine Verbindlichkeit, der Umsatz entsteht erst beim Einlösen.
    /// </summary>
    private sealed record Period(
        decimal Gross, decimal Net, int Sales, int Appointments, int NoShows, int Cancellations, decimal StaffUtilization,
        decimal RoomUtilization, int Customers, int NewCustomers, int Returning, decimal Rebooking,
        decimal ServiceNet, int ServiceSales, decimal VouchersSold, decimal VouchersRedeemed);

    /// <summary>
    /// Kennzahlen eines Zeitraums. Ohne <paramref name="amounts"/> (Recht „Berichte mit Beträgen“) fehlen alle Geldbeträge schon hier,
    /// nicht erst in der Oberfläche.
    /// </summary>
    public async Task<ManagementReportDto?> ManagementAsync(Guid locationId, DateOnly from, DateOnly until, bool amounts, CancellationToken ct,
        string compare = CompareModes.Previous) =>
        await ManagementAsync(locationId, from, until, ct, compare) is { } report ? amounts ? report : WithoutAmounts(report) : null;

    /// <summary>Wählbare Vergleiche (Abfrageparameter <c>compare</c>): Zeitraum davor, derselbe Abschnitt im Vormonat, derselbe Zeitraum im Vorjahr.</summary>
    internal static class CompareModes
    {
        public const string Previous = "previous";
        public const string Month = "month";
        public const string Year = "year";

        public static bool IsKnown(string? mode) => mode is Previous or Month or Year;
    }

    /// <summary>
    /// Vergleichszeitraum zu <paramref name="from"/> bis <paramref name="until"/>. „Vormonat“ und „Vorjahr“ verschieben beide Enden um ganze Monate; endet der Zeitraum
    /// am Monatsletzten, endet auch der Vergleich am Monatsletzten (30.11. gegen 31.10., 29.02. gegen 28.02.).
    /// </summary>
    internal static (DateOnly From, DateOnly Until) CompareRange(DateOnly from, DateOnly until, string mode)
    {
        static DateOnly Shift(DateOnly date, int months, bool monthEnd)
        {
            var moved = date.AddMonths(months);
            return monthEnd ? new DateOnly(moved.Year, moved.Month, DateTime.DaysInMonth(moved.Year, moved.Month)) : moved;
        }

        var endsMonth = until.Day == DateTime.DaysInMonth(until.Year, until.Month);
        return mode switch
        {
            CompareModes.Month => (Shift(from, -1, false), Shift(until, -1, endsMonth)),
            CompareModes.Year => (Shift(from, -12, false), Shift(until, -12, endsMonth)),
            _ => (from.AddDays(-(until.DayNumber - from.DayNumber + 1)), from.AddDays(-1)),
        };
    }

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
            Promotions = report.Promotions is null ? null : [.. report.Promotions.Select(p => p with { Discount = null, Revenue = null })],
            AmountsHidden = true,
        };

    internal async Task<ManagementReportDto?> ManagementAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct, string compare = CompareModes.Previous)
    {
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var length = until.DayNumber - from.DayNumber + 1;
        var current = await PeriodAsync(location, zone, from, until, ct);
        var (compareFrom, compareUntil) = CompareRange(from, until, compare);
        var previous = await PeriodAsync(location, zone, compareFrom, compareUntil, ct);
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
            .Select(a => new { a.Id, a.TreatmentId, Treatment = a.Treatment.Name, a.EmployeeId, a.Start, a.End })
            .ToListAsync(ct);
        var cashed = NetByAppointment(lines);
        decimal Net(Guid appointmentId) => cashed.GetValueOrDefault(appointmentId);

        // Vollkosten je Monat des Termins: Ein Zeitraum über zwei Monate rechnet jeden Termin mit den Stückkosten seines Monats.
        static (int Year, int Month) MonthOf(DateOnly date) => (date.Year, date.Month);
        var unitCosts = new Dictionary<(int Year, int Month), Dictionary<Guid, TreatmentCostDto>>();
        foreach (var month in appointments.Select(a => MonthOf(LocalTime.LocalDate(a.Start, zone))).Distinct())
        {
            unitCosts[month] = (await costs.AnalyzeAsync(locationId, month.Year, month.Month, ct))?.Treatments.ToDictionary(t => t.TreatmentId) ?? [];
        }

        var treatments = appointments.GroupBy(a => (a.TreatmentId, a.Treatment))
            .Select(g =>
            {
                var margins = g.Select(a => unitCosts[MonthOf(LocalTime.LocalDate(a.Start, zone))].GetValueOrDefault(a.TreatmentId) is { } cost
                        ? Net(a.Id) - cost.Total
                        : (decimal?)null)
                    .OfType<decimal>().ToList();
                return new TreatmentKpi(g.Key.TreatmentId, g.Key.Treatment, g.Count(), g.Sum(a => Net(a.Id)),
                    margins.Count == 0 ? null : Math.Round(margins.Average(), 2));
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
                var revenue = own.Sum(a => Net(a.Id));
                return new EmployeeKpi(e.Id, e.InHouseName, Math.Round(shiftHours, 1), Math.Round(treatmentHours, 1),
                    shiftHours > 0 ? (int)Math.Round(treatmentHours / shiftHours * 100) : 0,
                    treatmentHours > 0 ? Math.Round(revenue / treatmentHours, 2) : 0, own.Count);
            })
            .Where(k => k.ShiftHours > 0 || k.TreatmentHours > 0)
            .ToList();

        var promotions = await PromotionsAsync(lines, locationId, ct);

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
            new("revenue", "Umsatz brutto", current.Gross, previous.Gross, "€", true),
            new("revenueNet", "Umsatz netto", current.Net, previous.Net, "€", true),
            new("averageTicket", "Durchschnittsbon netto", current.Sales == 0 ? 0 : Math.Round(current.Net / current.Sales, 2),
                previous.Sales == 0 ? 0 : Math.Round(previous.Net / previous.Sales, 2), "€", true),
            new("serviceRevenueNet", "Umsatz ohne Gutscheinverkauf (netto)", current.ServiceNet, previous.ServiceNet, "€", true),
            new("serviceAverageTicket", "Durchschnittsbon ohne Gutscheinverkauf (netto)",
                current.ServiceSales == 0 ? 0 : Math.Round(current.ServiceNet / current.ServiceSales, 2),
                previous.ServiceSales == 0 ? 0 : Math.Round(previous.ServiceNet / previous.ServiceSales, 2), "€", true),
            new("vouchersSold", "Gutscheine verkauft", current.VouchersSold, previous.VouchersSold, "€"),
            new("vouchersRedeemed", "Gutscheine eingelöst", current.VouchersRedeemed, previous.VouchersRedeemed, "€"),
            new("appointments", "Behandlungen", current.Appointments, previous.Appointments, "", true),
            new("staffUtilization", "Auslastung Personal", current.StaffUtilization, previous.StaffUtilization, "%", true),
            new("roomUtilization", "Auslastung Räume", current.RoomUtilization, previous.RoomUtilization, "%", true),
            new("noShowRate", "No-Show-Quote", Rate(current.NoShows, all), Rate(previous.NoShows, previousAll), "%", false),
            new("cancellationRate", "Storno-Quote", Rate(current.Cancellations, all), Rate(previous.Cancellations, previousAll), "%", false),
            new("customers", "Gäste", current.Customers, previous.Customers, "", true),
            new("newCustomers", "Neue Gäste", current.NewCustomers, previous.NewCustomers, "", true),
            new("returningRate", "Stammgäste-Anteil", Rate(current.Returning, current.Customers), Rate(previous.Returning, previous.Customers), "%", true),
            new("rebookingRate", "Folgetermin gebucht", current.Rebooking, previous.Rebooking, "%", true),
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
        var settled = await SettledAppointmentsAsync(lines, ct);
        var now = time.GetUtcNow();
        var uncashed = appointments.Count(a => a.End <= now && !settled.Contains(a.Id));
        if (uncashed > 0)
        {
            alerts.Add(uncashed == 1 ? "1 Termin noch nicht kassiert." : $"{uncashed} Termine noch nicht kassiert.");
        }

        if (cashDifference >= CashDifferenceThreshold)
        {
            alerts.Add($"Kassendifferenzen im Zeitraum zusammen {cashDifference.ToString("0.00", German)} €.");
        }

        // Liegt der Beginn der Vorperiode vor dem ersten Termin des Standorts, fehlt ihr Verlauf: Neue Gäste und Stammgäste sind dann nicht vergleichbar.
        var firstAppointment = await db.Appointments.AsNoTracking().Where(a => a.LocationId == locationId).MinAsync(a => (DateTimeOffset?)a.Start, ct);
        if (firstAppointment is { } first && LocalTime.LocalDate(first, zone) > compareFrom)
        {
            alerts.Add($"Vergleich reicht vor den Beginn der Aufzeichnung am {LocalTime.LocalDate(first, zone).ToString("dd.MM.yyyy", German)}: "
                + "Die Vorperiode ist unvollständig.");
        }

        return new(locationId, from, until, kpis, byAccount, byPayment, employeeKpis, treatments, alerts, NoShows: current.NoShows, Cancellations: current.Cancellations,
            Promotions: promotions, CompareFrom: compareFrom, CompareUntil: compareUntil);
    }

    /// <summary>
    /// Wirkung der Aktionen aus den Belegzeilen des Zeitraums: je Position mit Aktion zählt, was netto kassiert blieb (Stornos heben auf, Teilbeträge zählen
    /// zusammen). Abzüge für Gutscheine und Anzahlungen bleiben außen vor: Die Position war so viel wert, auch wenn das Geld früher kam.
    /// „Neu“ ist ein Gast, dessen erster Termin im Spa der Termin der Position ist.
    /// </summary>
    private async Task<List<PromotionKpi>> PromotionsAsync(List<ReceiptLine> lines, Guid locationId, CancellationToken ct)
    {
        var billLineIds = lines.Where(l => l.BillLineId is not null && l.VoucherId is null && l.DepositId is null).Select(l => l.BillLineId!.Value).Distinct().ToList();
        if (billLineIds.Count == 0)
        {
            return [];
        }

        var promoted = await db.BillLines.AsNoTracking().Where(b => b.PromotionId != null && billLineIds.Contains(b.Id))
            .Join(db.Bills.AsNoTracking(), l => l.BillId, b => b.Id, (l, b) => new { l.Id, PromotionId = l.PromotionId!.Value, l.Discount, l.AppointmentId, b.CustomerId })
            .ToListAsync(ct);
        if (promoted.Count == 0)
        {
            return [];
        }

        var kept = lines.Where(l => l.BillLineId is not null && l.VoucherId is null && l.DepositId is null)
            .GroupBy(l => l.BillLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        var paid = promoted.Where(p => kept.GetValueOrDefault(p.Id) > 0).ToList();
        var customers = paid.Where(p => p.CustomerId is not null).Select(p => p.CustomerId!.Value).Distinct().ToList();
        var firstVisits = await db.Appointments.AsNoTracking()
            .Where(a => customers.Contains(a.CustomerId) && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.CustomerId)
            .Select(g => new { Customer = g.Key, First = g.OrderBy(a => a.Start).Select(a => a.Id).First() })
            .ToDictionaryAsync(f => f.Customer, f => f.First, ct);
        var names = await db.Promotions.AsNoTracking().Where(p => paid.Select(x => x.PromotionId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return [.. paid.GroupBy(p => p.PromotionId)
            .Select(g => new PromotionKpi(g.Key, names.GetValueOrDefault(g.Key, "Aktion"), g.Count(),
                g.Where(p => p.CustomerId is not null).Select(p => p.CustomerId).Distinct().Count(),
                g.Where(p => p.CustomerId is { } c && p.AppointmentId is { } a && firstVisits.GetValueOrDefault(c) == a).Select(p => p.CustomerId).Distinct().Count(),
                g.Sum(p => p.Discount), g.Sum(p => kept[p.Id])))
            .OrderByDescending(p => p.Positions).ThenBy(p => p.Name, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// Netto kassierter Betrag je Termin aus den Belegzeilen des Zeitraums. Zum Termin zählen seine Position und alle Zeilen, die auf
    /// sie verweisen: Rabatt (schon abgezogen), Abzug einer Anzahlung, Einlösung eines Einzweckgutscheins oder Pakets (nur über die
    /// Position verknüpft) und der Anzahlungsbeleg selbst. Zeilen ohne Position (Barzahlungsrabatt, Rundung in der Schweiz) verteilen
    /// sich anteilig nach Betrag auf die Positionen ihres Belegs. Stornos zählen mit umgekehrtem Vorzeichen.
    /// </summary>
    private static Dictionary<Guid, decimal> NetByAppointment(List<ReceiptLine> lines)
    {
        var appointmentOfPosition = lines.Where(l => l.BillLineId is not null && l.AppointmentId is not null)
            .GroupBy(l => l.BillLineId!.Value)
            .ToDictionary(g => g.Key, g => g.First().AppointmentId!.Value);
        var shares = new List<(Guid Appointment, decimal Gross, decimal Percent)>();
        foreach (var receipt in lines.GroupBy(l => l.ReceiptId))
        {
            var positions = receipt.Where(l => l.BillLineId is not null).ToList();
            var basis = positions.Sum(l => l.Amount);
            var withoutPosition = receipt.Where(l => l.BillLineId is null).ToList();
            foreach (var line in positions)
            {
                var linked = appointmentOfPosition.TryGetValue(line.BillLineId!.Value, out var viaPosition) ? viaPosition : (Guid?)null;
                if ((line.AppointmentId ?? linked) is not { } appointment)
                {
                    continue;
                }

                shares.Add((appointment, line.Amount, line.TaxPercent));
                if (basis != 0)
                {
                    shares.AddRange(withoutPosition.Select(w => (appointment, w.Amount * line.Amount / basis, w.TaxPercent)));
                }
            }
        }

        return shares.GroupBy(s => s.Appointment).ToDictionary(g => g.Key, g => TaxRates.NetSum(g, s => s.Gross, s => s.Percent));
    }

    /// <summary>
    /// Termine, deren Position im Zeitraum kassiert und nicht wieder storniert wurde. Ein mit Gutschein oder Paket bezahlter oder zu 100 %
    /// rabattierter Termin ist kassiert (die Position steht auf dem Beleg, auch mit 0 €); eine Anzahlung allein nicht, weil ihre Zeile zu einer
    /// eigenen Position gehört. Gezählt wird nicht der Betrag, sondern die Menge der Positionszeilen: Verkaufsbelege zählen plus, Stornobelege
    /// minus (die Stornozeile behält die Menge des Originals, nur der Betrag kehrt das Vorzeichen um).
    /// </summary>
    private async Task<HashSet<Guid>> SettledAppointmentsAsync(List<ReceiptLine> lines, CancellationToken ct)
    {
        var own = lines.Where(l => l is { BillLineId: not null, AppointmentId: not null, VoucherId: null, DepositId: null }).ToList();
        var positionIds = own.Select(l => l.BillLineId!.Value).Distinct().ToList();
        var treatmentPositions = (await db.BillLines.AsNoTracking()
            .Where(b => positionIds.Contains(b.Id) && b.Kind == BillLineKind.Appointment)
            .Select(b => b.Id).ToListAsync(ct)).ToHashSet();
        var receiptIds = own.Select(l => l.ReceiptId).Distinct().ToList();
        var voided = (await db.Receipts.AsNoTracking()
            .Where(r => receiptIds.Contains(r.Id) && r.Kind == ReceiptKind.Void)
            .Select(r => r.Id).ToListAsync(ct)).ToHashSet();
        return [.. own.Where(l => treatmentPositions.Contains(l.BillLineId!.Value))
            .GroupBy(l => l.AppointmentId!.Value)
            .Where(g => g.Sum(l => voided.Contains(l.ReceiptId) ? -l.Quantity : l.Quantity) > 0)
            .Select(g => g.Key)];
    }

    /// <summary>Eine Aktion in einer Zeile, für den Kommentar der KI und die Mail: „Herbst: 12 Positionen, 10 Gäste, davon 3 neu, Rabatt 84 €, Umsatz 756 €“.</summary>
    internal static string PromotionText(PromotionKpi promotion)
    {
        var text = $"{promotion.Name}: {promotion.Positions} {(promotion.Positions == 1 ? "Position" : "Positionen")}, {promotion.Guests} {(promotion.Guests == 1 ? "Gast" : "Gäste")}, davon {promotion.NewGuests} neu";
        if (promotion.Discount is { } discount)
        {
            text += $", Rabatt {discount.ToString("0.##", German)} €";
        }

        if (promotion.Revenue is { } revenue)
        {
            text += $", Umsatz {revenue.ToString("0.##", German)} €";
        }

        return text;
    }

    private async Task<Period> PeriodAsync(Location location, TimeZoneInfo zone, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var registerIds = await db.CashRegisters.Where(r => r.LocationId == location.Id).Select(r => r.Id).ToListAsync(ct);
        var receipts = db.Receipts.AsNoTracking()
            .Where(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void));
        var receiptLines = await receipts
            .SelectMany(r => r.Lines.Select(l => new
            {
                r.Id,
                r.Kind,
                l.Amount,
                l.TaxPercent,
                l.VoucherId,
                MultiPurposeSale = l.RevenueAccountId == PosModel.MultiPurposeVouchers.Id,
                VoucherSale = db.BillLines.Any(b => b.Id == l.BillLineId && b.Kind == BillLineKind.Voucher),
            }))
            .ToListAsync(ct);
        var gross = receiptLines.Sum(l => l.Amount);
        var net = TaxRates.NetSum(receiptLines, l => l.Amount, l => l.TaxPercent);
        var sales = await db.Receipts.CountAsync(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && r.Kind == ReceiptKind.Sale, ct)
            - await db.Receipts.CountAsync(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && r.Kind == ReceiptKind.Void, ct);

        // Ohne Wertgutscheinverkauf: Einzweckgutscheine und Pakete sind schon beim Verkauf versteuerter Umsatz und bleiben drin.
        // Ein Beleg, der nur Wertgutscheine verkauft, zählt nicht als Bon; ein Storno zieht wie bei „sales“ einen Bon ab.
        var serviceLines = receiptLines.Where(l => !l.MultiPurposeSale).ToList();
        var serviceNet = TaxRates.NetSum(serviceLines, l => l.Amount, l => l.TaxPercent);
        var serviceReceipts = serviceLines.Select(l => (l.Id, l.Kind)).Distinct().ToList();
        var serviceSales = serviceReceipts.Count(r => r.Kind == ReceiptKind.Sale) - serviceReceipts.Count(r => r.Kind == ReceiptKind.Void);

        // Gutscheine brutto: verkauft sind die Zeilen von Gutschein-Positionen; eingelöst sind Wertgutscheine als Zahlung
        // und Einzweckgutscheine und Pakete als negative Zeile mit dem Gutschein. Stornos heben sich über das Vorzeichen auf.
        var vouchersSold = receiptLines.Where(l => l.VoucherSale).Sum(l => l.Amount);
        var voucherPayments = await receipts.SelectMany(r => r.Payments).Where(p => p.Method == PaymentMethod.Voucher).SumAsync(p => p.Amount, ct);
        var vouchersRedeemed = voucherPayments - receiptLines.Where(l => l.VoucherId is not null && !l.VoucherSale).Sum(l => l.Amount);

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
        // Folgetermin ist jeder spätere Termin, der stattfindet oder stattgefunden hat (nicht storniert, kein No-Show).
        var rebooked = await db.Appointments.AsNoTracking()
            .Where(a => completed.Contains(a.CustomerId) && a.Start >= end && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
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
            completed.Count == 0 ? 0 : Math.Round(rebooked * 100m / completed.Count, 1),
            serviceNet,
            serviceSales,
            vouchersSold,
            vouchersRedeemed);
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
            reminders.Add($"Bis 15.02.{date.Year}: Jahresbeleg {date.Year - 1} jeder österreichischen Kassa mit der Belegcheck-App prüfen.");
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

using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Kennzahlen der Kundenkarte: Besuche aus dem Kalender, Umsatz aus den Kassenbelegen, daraus berechnete Sterne.
/// Sterne beschreiben den Wert für das Haus nach festen Regeln, keine Bewertung durch das Personal (siehe docs/produkt/kalender-rezeption.md).
/// </summary>
internal static class CustomerInsightsService
{
    /// <summary>★ Stammgast: so viele erledigte Besuche in zwölf Monaten; wie <c>DayLayout.RegularVisits</c> im Kalender.</summary>
    public const int RegularVisits = 3;

    /// <summary>★★ ab so vielen Besuchen in zwölf Monaten …</summary>
    public const int FrequentVisits = 6;

    /// <summary>… oder ab diesem Umsatz in zwölf Monaten (in der Währung des Standorts).</summary>
    public const decimal ValuableRevenue = 1000m;

    /// <summary>★★★ für die besten 5 % des Standorts nach Umsatz in zwölf Monaten, sobald dort genug Gäste Umsatz haben.</summary>
    public const decimal TopShare = 0.05m;

    public const int TopMinimumCustomers = 20;

    /// <summary>Absage innerhalb dieser Frist vor Beginn gilt als spät (Grundeinstellung der Stornofrist im Kundenbereich).</summary>
    public static readonly TimeSpan LateCancellation = TimeSpan.FromHours(24);

    public static async Task<CustomerInsights> GetAsync(AispaDbContext db, UserAccess access, Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var yearAgo = now.AddYears(-1);
        var visible = access.LocationIds.ToList();
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId && (access.AllLocations || visible.Contains(a.LocationId)))
            .Select(a => new { a.Id, a.LocationId, a.EmployeeId, a.Start, a.Status, a.UpdatedAt })
            .ToListAsync(ct);
        var done = appointments.Where(a => a.Status == AppointmentStatus.Completed).OrderBy(a => a.Start).ToList();
        var lastYear = done.Count(a => a.Start >= yearAgo);
        var noShows = appointments.Where(a => a.Status == AppointmentStatus.NoShow).OrderByDescending(a => a.Start)
            .Select(a => DateOnly.FromDateTime(a.Start.UtcDateTime)).ToList();
        // Ohne eigenen Zeitpunkt der Absage gilt die letzte Änderung; Stornos schreiben sie.
        var late = appointments.Where(a => a.Status == AppointmentStatus.Cancelled && a.UpdatedAt <= a.Start && a.Start - a.UpdatedAt < LateCancellation)
            .OrderByDescending(a => a.Start).Select(a => DateOnly.FromDateTime(a.Start.UtcDateTime)).ToList();

        var finance = access.Has(AccessRight.ViewAmountReports);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Currency, ct);
        var registers = await db.CashRegisters.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.LocationId, ct);
        var receipts = (await db.Receipts.AsNoTracking()
                .Where(r => r.CustomerId == customerId && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
                .Select(r => new { r.RegisterId, r.IssuedAt, r.Total, Lines = r.Lines.Where(l => l.AppointmentId != null).Select(l => new { l.AppointmentId, l.Amount }).ToList() })
                .ToListAsync(ct))
            .Where(r => access.AllLocations || visible.Contains(registers.GetValueOrDefault(r.RegisterId)))
            .ToList();
        string Currency(Guid locationId) => locations.GetValueOrDefault(locationId) ?? "EUR";
        var revenue = receipts.GroupBy(r => Currency(registers.GetValueOrDefault(r.RegisterId)))
            .Select(g => new CustomerRevenue(g.Key, g.Sum(r => r.Total), g.Where(r => r.IssuedAt >= yearAgo).Sum(r => r.Total)))
            .OrderByDescending(r => r.Total).ToList();

        var byAppointment = receipts.SelectMany(r => r.Lines).GroupBy(l => l.AppointmentId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        var employeeIds = done.Select(a => a.EmployeeId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var employees = done.GroupBy(a => a.EmployeeId)
            .Select(g => new CustomerEmployeeVisits(g.Key, names.GetValueOrDefault(g.Key) ?? "Ehemalige Person", g.Count(),
                finance ? g.Sum(a => byAppointment.GetValueOrDefault(a.Id)) : null,
                finance ? Currency(g.Last().LocationId) : null))
            .OrderByDescending(e => e.Visits).ToList();

        // Sterne am Standort des letzten Besuchs, Umsatz in dessen Währung.
        var home = done.LastOrDefault()?.LocationId;
        var homeRevenue = home is { } h ? revenue.FirstOrDefault(r => r.Currency == Currency(h))?.LastYear ?? 0 : 0;
        var top = home is { } location && homeRevenue > 0 && await IsTopAsync(db, location, customerId, yearAgo, ct);
        var (stars, starsText) = Stars(lastYear, homeRevenue, top);

        var packages = await db.Vouchers.AsNoTracking()
            .Where(v => v.CustomerId == customerId && v.Units != null && v.Status == VoucherStatus.Active)
            .Join(db.Treatments, v => v.TreatmentId, t => t.Id, (v, t) => new GuestPackage(t.Id, t.Name, v.Units!.Value, v.UnitsLeft ?? 0, v.ValidUntil))
            .ToListAsync(ct);

        var mayNote = access.Has(AccessRight.Book);
        var note = mayNote ? await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.ReceptionNote).FirstOrDefaultAsync(ct) : null;

        var nameChanges = await db.CustomerNameChanges.AsNoTracking().Where(n => n.CustomerId == customerId).OrderByDescending(n => n.ChangedAt)
            .GroupJoin(db.Users, n => n.ChangedByUserId, u => u.Id, (n, users) => new { n, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new CustomerNameChange(x.n.ChangedAt, u == null ? null : u.DisplayName, x.n.OldName, x.n.NewName))
            .ToListAsync(ct);

        (List<GuestVoucher>? Vouchers, List<GuestPurchase>? Purchases) cash = mayNote ? await CashAsync(db, access, customerId, home, now, registers, ct) : (null, null);

        return new CustomerInsights(done.Count, lastYear, done.Count == 0 ? null : DateOnly.FromDateTime(done[^1].Start.UtcDateTime),
            IntervalDays(done.Select(a => a.Start).ToList()), stars, starsText, noShows, late, employees, finance ? revenue : null, packages,
            mayNote, note, nameChanges, await BirthdayAsync(db, customerId, home, now, ct), cash.Vouchers, cash.Purchases);
    }

    /// <summary>Höchstens so viele letzte Käufe zeigt die Kundenkarte.</summary>
    public const int RecentPurchases = 5;

    /// <summary>
    /// Auskunft der Kassa für die Kundenkarte (T-020), wie im Assistenten nur mit dem Recht zu buchen. Ein Wertgutschein gehört zum Gast, wenn er ihm gehört, auf seinem
    /// Beleg verkauft, mit ihm bezahlt oder (Behandlungsgutschein) auf seinem Beleg eingelöst wurde; Pakete stehen in <c>Packages</c>. Käufe sind bezahlte Positionen
    /// ohne Termin und ohne Anzahlung auf nicht stornierten Verkaufsbelegen der sichtbaren Standorte; Einlösungen, Rabatte und Rundung sind keine eigenen Positionen
    /// der Rechnung und fallen weg. Tage am Kalendertag des Standorts.
    /// </summary>
    private static async Task<(List<GuestVoucher> Vouchers, List<GuestPurchase> Purchases)> CashAsync(AispaDbContext db, UserAccess access, Guid customerId, Guid? home,
        DateTimeOffset now, Dictionary<Guid, Guid> registers, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => new { l.Id, l.Currency, l.TimeZone }).ToListAsync(ct);
        var byId = locations.ToDictionary(l => l.Id);
        string Currency(Guid registerId) => byId.GetValueOrDefault(registers.GetValueOrDefault(registerId))?.Currency ?? "EUR";
        TimeZoneInfo Zone(Guid registerId) => LocalTime.Zone(byId.GetValueOrDefault(registers.GetValueOrDefault(registerId))?.TimeZone ?? "UTC");
        var homeZone = (home is { } h ? byId.GetValueOrDefault(h) : null)?.TimeZone ?? locations.FirstOrDefault()?.TimeZone ?? "UTC";
        var today = LocalTime.LocalDate(now, LocalTime.Zone(homeZone));

        var receipts = db.Receipts.AsNoTracking().Where(r => r.CustomerId == customerId);
        var receiptIds = receipts.Select(r => r.Id);
        var paid = receipts.SelectMany(r => r.Payments).Where(p => p.VoucherId != null).Select(p => p.VoucherId);
        var credited = receipts.SelectMany(r => r.Lines).Where(l => l.VoucherId != null).Select(l => l.VoucherId);
        var open = await db.Vouchers.AsNoTracking()
            .Where(v => v.Status == VoucherStatus.Active && v.Units == null && v.Balance > 0 && (v.ValidUntil == null || v.ValidUntil >= today)
                && (v.CustomerId == customerId || receiptIds.Contains(v.SoldOnReceiptId) || paid.Contains(v.Id) || credited.Contains(v.Id)))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Code)
            .Select(v => new { v.Kind, v.Code, v.TreatmentId, v.Balance, v.ValidUntil, v.SoldOnReceiptId })
            .ToListAsync(ct);
        var treatmentIds = open.Where(v => v.TreatmentId != null).Select(v => v.TreatmentId!.Value).Distinct().ToList();
        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var soldIds = open.Select(v => v.SoldOnReceiptId).Distinct().ToList();
        var soldAt = await db.Receipts.AsNoTracking().Where(r => soldIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.RegisterId, ct);
        var vouchers = open.Select(v => new GuestVoucher(v.Kind, v.Code[^Math.Min(4, v.Code.Length)..], v.TreatmentId is { } id ? treatments.GetValueOrDefault(id) : null,
            v.Balance, Currency(soldAt.GetValueOrDefault(v.SoldOnReceiptId)), v.ValidUntil)).ToList();

        var voided = db.Receipts.AsNoTracking().Where(r => r.VoidsReceiptId != null).Select(r => r.VoidsReceiptId!.Value);
        var lines = await receipts.Where(r => r.Kind == ReceiptKind.Sale && !voided.Contains(r.Id))
            .SelectMany(r => r.Lines.Where(l => l.BillLineId != null && l.AppointmentId == null && l.DepositId == null && l.Amount > 0),
                (r, l) => new { r.RegisterId, r.IssuedAt, r.Number, l.Position, l.Description, l.Quantity, l.Amount })
            .ToListAsync(ct);
        var visible = access.LocationIds.ToHashSet();
        var purchases = lines.Where(l => access.AllLocations || visible.Contains(registers.GetValueOrDefault(l.RegisterId)))
            .OrderByDescending(l => l.IssuedAt).ThenByDescending(l => l.Number).ThenBy(l => l.Position)
            .Take(RecentPurchases)
            .Select(l => new GuestPurchase(LocalTime.LocalDate(l.IssuedAt, Zone(l.RegisterId)), l.Description, l.Quantity, l.Amount, Currency(l.RegisterId)))
            .ToList();
        return (vouchers, purchases);
    }

    /// <summary>
    /// Der nächste Geburtstag für die Kundenkarte (Alter und „in 12 Tagen“), gerechnet am Kalendertag in der Zeitzone des Standorts des letzten Besuchs, sonst des ersten
    /// Standorts. Ohne Geburtsdatum nichts.
    /// </summary>
    private static async Task<GuestBirthday?> BirthdayAsync(AispaDbContext db, Guid customerId, Guid? home, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.BirthDate).FirstOrDefaultAsync(ct) is not { } birth)
        {
            return null;
        }

        var zone = await db.Locations.AsNoTracking().Where(l => l.Id == home).Select(l => l.TimeZone).FirstOrDefaultAsync(ct)
            ?? await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        return Birthdays.Next(birth, LocalTime.LocalDate(now, LocalTime.Zone(zone ?? "UTC")));
    }

    /// <summary>Sterne nach festen Regeln; der Text nennt den Grund, damit niemand rätselt.</summary>
    public static (int Stars, string Text) Stars(int visitsLastYear, decimal revenueLastYear, bool top) =>
        top ? (3, "Unter den besten 5 % des Standorts nach Umsatz in zwölf Monaten")
        : visitsLastYear >= FrequentVisits ? (2, $"{visitsLastYear} Besuche in zwölf Monaten")
        : revenueLastYear >= ValuableRevenue ? (2, "Umsatz in zwölf Monaten über der Schwelle")
        : visitsLastYear >= RegularVisits ? (1, $"Stammgast: {visitsLastYear} Besuche in zwölf Monaten")
        : (0, visitsLastYear == 0 ? "Kein Besuch in zwölf Monaten" : $"{visitsLastYear} {(visitsLastYear == 1 ? "Besuch" : "Besuche")} in zwölf Monaten");

    /// <summary>Üblicher Abstand: Median der Abstände ab drei Besuchen, sonst unbekannt.</summary>
    public static int? IntervalDays(IReadOnlyList<DateTimeOffset> visits)
    {
        if (visits.Count < 3)
        {
            return null;
        }

        var gaps = visits.Order().Zip(visits.Order().Skip(1), (a, b) => (b - a).TotalDays).Order().ToList();
        return (int)Math.Round(gaps.Count % 2 == 1 ? gaps[gaps.Count / 2] : (gaps[(gaps.Count / 2) - 1] + gaps[gaps.Count / 2]) / 2);
    }

    private static async Task<bool> IsTopAsync(AispaDbContext db, Guid locationId, Guid customerId, DateTimeOffset since, CancellationToken ct)
    {
        var registers = db.CashRegisters.Where(r => r.LocationId == locationId).Select(r => r.Id);
        var totals = await db.Receipts.AsNoTracking()
            .Where(r => r.CustomerId != null && registers.Contains(r.RegisterId) && r.IssuedAt >= since && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .GroupBy(r => r.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(r => r.Total) })
            .Where(t => t.Total > 0)
            .OrderByDescending(t => t.Total)
            .ToListAsync(ct);
        if (totals.Count < TopMinimumCustomers)
        {
            return false;
        }

        var count = (int)Math.Ceiling(totals.Count * TopShare);
        return totals.Take(count).Any(t => t.CustomerId == customerId);
    }
}

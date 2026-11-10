using System.Text.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Listen der Kassa als Excel oder CSV (Export/Import, Aufgabe D), nur für die Leitung: Belegliste, Umsatz je Tag, je Zahlart, je Behandlung und Person
/// (jeweils für einen Zeitraum), Gutscheinbestand und Tagesabschlüsse. Gerechnet wird aus den Belegen der Kassen des Standorts; Stornobelege zählen mit
/// negativem Betrag.
/// </summary>
internal static class PosExports
{
    private const string Receipts = "receipts";
    private const string RevenueDays = "revenue-days";
    private const string RevenuePayments = "revenue-payments";
    private const string RevenueTreatments = "revenue-treatments";
    private const string Vouchers = "vouchers";
    private const string DayClosings = "day-closings";

    private static readonly string[] Lists = [Receipts, RevenueDays, RevenuePayments, RevenueTreatments, Vouchers, DayClosings];

    public static IEndpointRouteBuilder MapPosExportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/pos/locations/{locationId:guid}/export", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> (
                Guid locationId, string? list, DateOnly? from, DateOnly? until, string? format, AispaDbContext db, AccessService accessService, ExportLog log,
                CancellationToken ct) =>
            {
                var (request, failure) = await LeadershipListAsync(locationId, list, Lists, format, db, accessService, ct);
                if (request is null)
                {
                    return failure!;
                }

                var registers = await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, r => r.Code, ct);
                if (request.List == Vouchers)
                {
                    var vouchers = await VoucherTableAsync(db, [.. registers.Keys], ct);
                    return await FileAsync(vouchers, request.Format, "Gutscheinbestand", null, "Gutscheine", null, log, ct);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                var zone = ZoneOf(request.Location);
                var begin = LocalTime.Day(from!.Value, zone).From;
                var end = LocalTime.Day(until!.Value, zone).Until;
                var period = $"{Day(from.Value)} bis {Day(until.Value)}";
                if (request.List == DayClosings)
                {
                    var closings = await DayClosingTableAsync(db, registers, begin, end, zone, ct);
                    return await FileAsync(closings, request.Format, "Tagesabschlüsse", period, "Tagesabschluesse", from, log, ct);
                }

                var registerIds = registers.Keys.ToList();
                var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery()
                    .Where(r => registerIds.Contains(r.RegisterId) && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void) && r.IssuedAt >= begin && r.IssuedAt < end)
                    .OrderBy(r => r.IssuedAt).ThenBy(r => r.Number)
                    .ToListAsync(ct);
                var (table, kind, title) = request.List switch
                {
                    Receipts => (await ReceiptTableAsync(db, receipts, registers, zone, ct), "Belegliste", "Belege"),
                    RevenueDays => (RevenueDayTable(receipts, zone), "Umsatz je Tag", "Umsatz_je_Tag"),
                    RevenuePayments => (RevenuePaymentTable(receipts), "Umsatz je Zahlart", "Umsatz_je_Zahlart"),
                    _ => (await RevenueTreatmentTableAsync(db, receipts, ct), "Umsatz je Behandlung und Person", "Umsatz_je_Behandlung"),
                };
                return await FileAsync(table, request.Format, kind, period, title, from, log, ct);
            })
            .WithTags("Kassa")
            .RequirePermission(AccessRight.ManageLocation)
            .WithName("ExportPosList")
            .WithSummary("Listen der Kassa als Excel oder CSV, nur für die Leitung: list=receipts (Belegliste), revenue-days, revenue-payments, revenue-treatments "
                + "(Umsatz je Tag, Zahlart, Behandlung und Person), day-closings (Tagesabschlüsse) für den Zeitraum from bis until; vouchers (Gutscheinbestand) ohne Zeitraum.");

        return routes;
    }

    private static async Task<ExportTable> ReceiptTableAsync(AispaDbContext db, List<Receipt> receipts, Dictionary<Guid, string> registers, TimeZoneInfo zone,
        CancellationToken ct)
    {
        var ids = receipts.Select(r => r.Id).ToList();
        var voided = (await db.Receipts.AsNoTracking().Where(v => v.VoidsReceiptId != null && ids.Contains(v.VoidsReceiptId.Value))
            .Select(v => v.VoidsReceiptId!.Value).ToListAsync(ct)).ToHashSet();
        var numbers = receipts.ToDictionary(r => r.Id, r => r.Number);
        var guests = await GuestNamesAsync(db, receipts.Select(r => r.CustomerId), ct);
        var columns = new ExportColumn[]
        {
            new("Kasse", ColumnKind.Text), new("Nummer", ColumnKind.Number), new("Tag", ColumnKind.Date), new("Zeit", ColumnKind.Time),
            new("Zahlarten", ColumnKind.Text), new("Betrag", ColumnKind.Money), new("Storno", ColumnKind.Text), new("Gast", ColumnKind.Text),
        };
        var rows = receipts.Select(r =>
            {
                var local = TimeZoneInfo.ConvertTime(r.IssuedAt, zone).DateTime;
                return new object?[]
                {
                    registers[r.RegisterId], r.Number, DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local),
                    string.Join(", ", r.Payments.Select(p => p.Method).Distinct().Select(PaymentMethods.Label)), r.Total, StornoText(r, numbers, voided),
                    r.CustomerId is { } c ? guests.GetValueOrDefault(c) : null,
                };
            })
            .ToList();
        return new ExportTable("Belegliste", columns, rows);
    }

    /// <summary>„Storno zu Nr. 12“ beim Stornobeleg, „storniert“ beim stornierten Beleg, sonst leer.</summary>
    private static string? StornoText(Receipt receipt, Dictionary<Guid, long> numbers, HashSet<Guid> voided)
    {
        if (receipt.Kind == ReceiptKind.Void)
        {
            return receipt.VoidsReceiptId is { } original && numbers.TryGetValue(original, out var number) ? $"Storno zu Nr. {number}" : "Storno";
        }

        return voided.Contains(receipt.Id) ? "storniert" : null;
    }

    private static ExportTable RevenueDayTable(List<Receipt> receipts, TimeZoneInfo zone)
    {
        var columns = new ExportColumn[] { new("Tag", ColumnKind.Date), new("Belege", ColumnKind.Number), new("Stornos", ColumnKind.Number), new("Umsatz", ColumnKind.Money) };
        var rows = receipts.GroupBy(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.IssuedAt, zone).DateTime)).OrderBy(g => g.Key)
            .Select(g => new object?[] { g.Key, g.Count(r => r.Kind == ReceiptKind.Sale), g.Count(r => r.Kind == ReceiptKind.Void), g.Sum(r => r.Total) })
            .ToList();
        return new ExportTable("Umsatz je Tag", columns, rows);
    }

    private static ExportTable RevenuePaymentTable(List<Receipt> receipts)
    {
        var columns = new ExportColumn[] { new("Zahlart", ColumnKind.Text), new("Betrag", ColumnKind.Money) };
        var rows = receipts.SelectMany(r => r.Payments).GroupBy(p => p.Method).OrderBy(g => g.Key)
            .Select(g => new object?[] { PaymentMethods.Label(g.Key), g.Sum(p => p.Amount) })
            .ToList();
        return new ExportTable("Umsatz je Zahlart", columns, rows);
    }

    private static async Task<ExportTable> RevenueTreatmentTableAsync(AispaDbContext db, List<Receipt> receipts, CancellationToken ct)
    {
        var lines = receipts.SelectMany(r => r.Lines).Where(l => l.AppointmentId is not null).ToList();
        var appointmentIds = lines.Select(l => l.AppointmentId!.Value).Distinct().ToList();
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
            .Where(a => appointmentIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.InHouseName, ct);
        var columns = new ExportColumn[]
        {
            new("Behandlung", ColumnKind.Text), new("Person", ColumnKind.Text), new("Termine", ColumnKind.Number), new("Umsatz", ColumnKind.Money),
        };
        var rows = lines
            .Select(l => (Line: l, Appointment: appointments.GetValueOrDefault(l.AppointmentId!.Value)))
            .GroupBy(x => (Treatment: x.Appointment?.Treatment.Name ?? x.Line.Description, Person: x.Appointment is { } a ? employees.GetValueOrDefault(a.EmployeeId) : null))
            .OrderBy(g => g.Key.Treatment, StringComparer.CurrentCulture).ThenBy(g => g.Key.Person, StringComparer.CurrentCulture)
            .Select(g => new object?[] { g.Key.Treatment, g.Key.Person, g.Count(x => x.Line.Amount > 0) - g.Count(x => x.Line.Amount < 0), g.Sum(x => x.Line.Amount) })
            .ToList();
        return new ExportTable("Umsatz je Behandlung und Person", columns, rows);
    }

    private static async Task<ExportTable> VoucherTableAsync(AispaDbContext db, List<Guid> registerIds, CancellationToken ct)
    {
        // Gutscheine hängen an keinem Standort; zum Standort gehört, was an seinen Kassen verkauft wurde.
        var vouchers = await db.Vouchers.AsNoTracking()
            .Join(db.Receipts.Where(r => registerIds.Contains(r.RegisterId)), v => v.SoldOnReceiptId, r => r.Id, (v, r) => new { Voucher = v, Buyer = r.CustomerId })
            .OrderBy(x => x.Voucher.CreatedAt).ThenBy(x => x.Voucher.Code)
            .ToListAsync(ct);
        var guests = await GuestNamesAsync(db, vouchers.Select(x => x.Buyer), ct);
        var columns = new ExportColumn[]
        {
            new("Code", ColumnKind.Text), new("Art", ColumnKind.Text), new("Wert", ColumnKind.Money), new("Rest", ColumnKind.Money), new("Gültig bis", ColumnKind.Date),
            new("Status", ColumnKind.Text), new("Käufer", ColumnKind.Text),
        };
        var rows = vouchers.Select(x => new object?[]
            {
                x.Voucher.Code, x.Voucher.Kind == VoucherKind.SinglePurpose ? "Einzweck" : "Mehrzweck", x.Voucher.InitialValue, x.Voucher.Balance, x.Voucher.ValidUntil,
                VoucherStatusText(x.Voucher.Status), x.Buyer is { } c ? guests.GetValueOrDefault(c) : null,
            })
            .ToList();
        return new ExportTable("Gutscheinbestand", columns, rows);
    }

    private static string VoucherStatusText(VoucherStatus status) => status switch
    {
        VoucherStatus.Redeemed => "Eingelöst",
        VoucherStatus.Voided => "Storniert",
        _ => "Offen",
    };

    private static async Task<ExportTable> DayClosingTableAsync(AispaDbContext db, Dictionary<Guid, string> registers, DateTimeOffset begin, DateTimeOffset end,
        TimeZoneInfo zone, CancellationToken ct)
    {
        var registerIds = registers.Keys.ToList();
        var closings = await db.DayClosings.AsNoTracking().Where(d => registerIds.Contains(d.RegisterId) && d.Until >= begin && d.Until < end)
            .OrderBy(d => d.Until).ToListAsync(ct);
        var columns = new ExportColumn[]
        {
            new("Tag", ColumnKind.Date), new("Kasse", ColumnKind.Text), new("Belege", ColumnKind.Number), new("Umsatz", ColumnKind.Money), new("Soll", ColumnKind.Money),
            new("Gezählt", ColumnKind.Money), new("Differenz", ColumnKind.Money),
        };
        var rows = closings.Select(d =>
            {
                var report = JsonSerializer.Deserialize<DayClosingDto>(d.Report, PosJson.Options)!;
                return new object?[]
                {
                    DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(d.Until, zone).DateTime), registers[d.RegisterId], report.ReceiptCount, report.Revenue,
                    report.ExpectedCash, report.CountedCash, report.Difference,
                };
            })
            .ToList();
        return new ExportTable("Tagesabschlüsse", columns, rows);
    }

    private static async Task<Dictionary<Guid, string>> GuestNamesAsync(AispaDbContext db, IEnumerable<Guid?> customerIds, CancellationToken ct)
    {
        var ids = customerIds.OfType<Guid>().Distinct().ToList();
        return await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.FirstName + " " + c.LastName).Trim(), ct);
    }
}

using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Kundenüberblick im Termin-Panel: schlanker als die Kennzahlen der Kundenkarte, ohne Sterne, Umsatz und Rezeptionsvermerk. Termine nur aus den
/// Standorten mit Zugang; Gesundheitsdaten nie, nur der Stand „Anamnese liegt vor“.
/// </summary>
internal static class CustomerSummaryService
{
    /// <summary>So viele letzte Behandlungen nennt der Überblick.</summary>
    public const int RecentCount = 5;

    public static async Task<CustomerSummary?> GetAsync(AispaDbContext db, OnlineDbContext online, UserAccess access, Guid customerId, DateTimeOffset now,
        CancellationToken ct)
    {
        if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
        {
            return null;
        }

        var visible = access.LocationIds.ToList();
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId && (access.AllLocations || visible.Contains(a.LocationId)))
            .Select(a => new { a.Id, a.Start, a.Status, a.EmployeeId, TreatmentName = a.Treatment.Name })
            .ToListAsync(ct);
        var done = appointments.Where(a => a.Status == AppointmentStatus.Completed).OrderByDescending(a => a.Start).ToList();
        var recent = done.Take(RecentCount).ToList();
        var employeeIds = recent.Select(a => a.EmployeeId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var next = appointments.Where(a => a.Start > now && a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
            .OrderBy(a => a.Start).FirstOrDefault();

        return new CustomerSummary(
            done.Count,
            done.Count == 0 ? null : DateOnly.FromDateTime(done[0].Start.UtcDateTime),
            appointments.Count(a => a.Status == AppointmentStatus.NoShow),
            next is null ? null : new CustomerNextAppointment(next.Id, next.Start, next.TreatmentName),
            [.. recent.Select(a => new CustomerRecentVisit(a.Id, a.Start, a.TreatmentName, names.GetValueOrDefault(a.EmployeeId) ?? "Ehemalige Person"))],
            string.IsNullOrWhiteSpace(customer.Note) ? null : customer.Note.Trim(),
            customer.Phone,
            customer.Email,
            await online.AnamnesisRecords.AsNoTracking().AnyAsync(r => r.CustomerId == customerId, ct),
            access.Has(AccessRight.ViewAmountReports) ? await OpenItemsAsync(db, access, customerId, ct) : null);
    }

    /// <summary>Rest der offenen Rechnungen in der Währung des Standorts der Kasse (bei mehreren Währungen die der größten Summe) und Pakete mit Rest.</summary>
    private static async Task<CustomerOpenItems?> OpenItemsAsync(AispaDbContext db, UserAccess access, Guid customerId, CancellationToken ct)
    {
        var visible = access.LocationIds.ToList();
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.CustomerId == customerId && b.Status == BillStatus.Open)
            .Join(db.CashRegisters, b => b.RegisterId, r => r.Id, (b, r) => new { r.LocationId, Open = b.Lines.Sum(l => l.Amount - l.PaidAmount) })
            .Join(db.Locations, x => x.LocationId, l => l.Id, (x, l) => new { x.LocationId, x.Open, l.Currency })
            .ToListAsync(ct);
        var open = bills.Where(b => b.Open > 0 && (access.AllLocations || visible.Contains(b.LocationId)))
            .GroupBy(b => b.Currency).Select(g => new { Currency = g.Key, Amount = g.Sum(b => b.Open) }).OrderByDescending(g => g.Amount).FirstOrDefault();
        var packages = await db.Vouchers.AsNoTracking()
            .Where(v => v.CustomerId == customerId && v.Units != null && v.Status == VoucherStatus.Active && v.UnitsLeft > 0)
            .Join(db.Treatments, v => v.TreatmentId, t => t.Id, (v, t) => new GuestPackage(t.Id, t.Name, v.Units!.Value, v.UnitsLeft ?? 0, v.ValidUntil))
            .ToListAsync(ct);
        return open is null && packages.Count == 0 ? null : new CustomerOpenItems(open?.Amount ?? 0, open?.Currency ?? "EUR", packages);
    }
}

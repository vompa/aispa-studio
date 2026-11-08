using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo.Memory;

/// <summary>
/// Die Stammdaten des Beispiel-Spas, wie sie nach dem Anlegen von Standorten, Katalog, Team, Dienstplan und Kassen in der Datenbank stehen.
/// Der Verlauf im Speicher liest nur hieraus und nie wieder aus der Datenbank. Die Reihenfolge der Personen ist die der Datenbank,
/// denn der Planer wählt unter gleich guten Zeiten nach dieser Reihenfolge.
/// </summary>
internal sealed class DemoSnapshot
{
    public required Tenant Tenant { get; init; }

    public required Dictionary<Guid, Location> Locations { get; init; }

    public required Dictionary<Guid, Treatment> Treatments { get; init; }

    /// <summary>Aktive und inaktive Personen in der Reihenfolge, in der die Datenbank sie liefert.</summary>
    public required List<Employee> Employees { get; init; }

    public required Dictionary<(Guid EmployeeId, DateOnly Date), RosterDay> RosterDays { get; init; }

    /// <summary>Die Dienstplantage je Person, die einen Standort nennen (für „arbeitet dort“).</summary>
    public required Dictionary<Guid, List<RosterDay>> RosterDaysByEmployee { get; init; }

    public required Dictionary<Guid, Customer> Customers { get; init; }

    public required Dictionary<Guid, List<(CustomerFlagKind Kind, Guid? EmployeeId)>> Flags { get; init; }

    public required Dictionary<Guid, Pos.Article> Articles { get; init; }

    public required Dictionary<Guid, RevenueAccount> Accounts { get; init; }

    public required Dictionary<Guid, TreatmentPosSetting> PosSettings { get; init; }

    /// <summary>Die Aktionen des Spas (ADR 0071) mit Standorten und Behandlungen; der Verlauf wendet laufende Aktionen an der Kassa an.</summary>
    public required Dictionary<Guid, Promotion> Promotions { get; init; }

    /// <summary>Die Kassen mit ihrem Stand nach dem Startbeleg; der Verlauf schreibt ihn fort.</summary>
    public required Dictionary<Guid, CashRegister> Registers { get; init; }

    public required Dictionary<Guid, List<Receipt>> RegisterReceipts { get; init; }

    public required List<ResourceBooking> ExistingBookings { get; init; }

    public static async Task<DemoSnapshot> LoadAsync(AispaDbContext db, Guid tenantId, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).ToDictionaryAsync(l => l.Id, ct);
        var treatments = await db.Treatments.AsNoTracking()
            .Include(t => t.RequiredQualifications).Include(t => t.Rooms).Include(t => t.Equipment).Include(t => t.LocationPrices).Include(t => t.PriceRules)
            .AsSplitQuery().ToDictionaryAsync(t => t.Id, ct);
        var employees = await db.Employees.AsNoTracking().Include(e => e.Qualifications).Include(e => e.WorkingHours).AsSplitQuery().ToListAsync(ct);
        var rosterDays = await db.RosterDays.AsNoTracking().Include(d => d.Entries).ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var flags = (await db.CustomerFlags.AsNoTracking().Where(f => f.RemovedAt == null).Select(f => new { f.CustomerId, f.Kind, f.EmployeeId }).ToListAsync(ct))
            .GroupBy(f => f.CustomerId).ToDictionary(g => g.Key, g => g.Select(f => (f.Kind, f.EmployeeId)).ToList());

        // Kassen verfolgt der Kontext: der Verlauf ändert sie, und die Test-Signatureinrichtung sucht ihre Schlüssel im Kontext.
        var registers = await db.CashRegisters.ToDictionaryAsync(r => r.Id, ct);
        _ = await db.Set<TestSignatureKey>().ToListAsync(ct);
        var receipts = (await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery().ToListAsync(ct))
            .GroupBy(r => r.RegisterId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Number).ToList());

        return new DemoSnapshot
        {
            Tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct),
            Locations = locations,
            Treatments = treatments,
            Employees = employees,
            RosterDays = rosterDays.ToDictionary(d => (d.EmployeeId, d.Date)),
            RosterDaysByEmployee = rosterDays.GroupBy(d => d.EmployeeId).ToDictionary(g => g.Key, g => g.ToList()),
            Customers = customers,
            Flags = flags,
            Articles = await db.Articles.AsNoTracking().ToDictionaryAsync(a => a.Id, ct),
            Accounts = await db.RevenueAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct),
            PosSettings = await db.TreatmentPosSettings.AsNoTracking().ToDictionaryAsync(s => s.TreatmentId, ct),
            Promotions = await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery().ToDictionaryAsync(p => p.Id, ct),
            Registers = registers,
            RegisterReceipts = receipts,
            ExistingBookings = await db.ResourceBookings.AsNoTracking().ToListAsync(ct),
        };
    }
}

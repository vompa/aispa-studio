using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Kopf eines Dokuments aus den Daten des Spas (Firmendaten des Mandanten, ADR 0052; je Standort überschreibbar), des Standorts und der Person, die es erzeugt
/// (Vorname des Rufnamens, ADR 0064). Ohne Standort nur die Firmendaten; die Zeitzone kommt dann vom ersten Standort.
/// </summary>
internal sealed class DocumentHeaders(AispaDbContext db, TenantContext tenants, AccessService access, TimeProvider time)
{
    private const string DefaultZone = "Europe/Vienna";

    public async Task<DocumentHeader> ForAsync(Guid? locationId, ClaimsPrincipal? user, CancellationToken ct)
    {
        var tenant = tenants.TenantId is { } tenantId ? await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct) : null;
        var location = locationId is { } id ? await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) : null;
        var zone = location?.TimeZone ?? await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) ?? DefaultZone;
        return new DocumentHeader(tenant?.Name ?? "", CompanyLines(tenant, location), LocationLines(location), time.GetUtcNow(), LocalTime.Zone(zone), await CreatedByAsync(user, ct));
    }

    private async Task<string?> CreatedByAsync(ClaimsPrincipal? user, CancellationToken ct)
    {
        if (user is null || await access.GetAsync(user, ct) is not { EmployeeId: { } employeeId })
        {
            return null;
        }

        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => new { e.DisplayName, e.PreferredName }).FirstOrDefaultAsync(ct);
        return employee is null ? null : EmployeeNames.InHouse(employee.DisplayName, employee.PreferredName).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static List<string> CompanyLines(Tenant? tenant, Location? location)
    {
        var lines = new List<string>();
        Add(lines, location?.LegalName ?? tenant?.LegalName);
        Add(lines, tenant?.Street);
        Add(lines, Join(tenant?.PostalCode, tenant?.City));
        if ((location?.VatId ?? tenant?.VatId) is { } vatId)
        {
            lines.Add($"UID {vatId}");
        }

        var (register, court) = location?.CompanyRegisterNumber is { } own ? (own, location.CompanyRegisterCourt) : (tenant?.CompanyRegisterNumber, tenant?.CompanyRegisterCourt);
        Add(lines, register is null ? null : court is null ? register : $"{register}, {court}");
        return lines;
    }

    private static List<string> LocationLines(Location? location)
    {
        if (location is null)
        {
            return [];
        }

        var lines = new List<string> { location.Name };
        Add(lines, location.Street);
        Add(lines, Join(location.PostalCode, location.City));
        Add(lines, location.Phone);
        Add(lines, location.Email);
        return lines;
    }

    private static void Add(List<string> lines, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(value.Trim());
        }
    }

    private static string? Join(string? postalCode, string? city) =>
        string.Join(' ', new[] { postalCode, city }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } text ? text : null;
}

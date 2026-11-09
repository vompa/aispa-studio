using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Stammblatt einer Kundin oder eines Kunden als PDF (PDF-Ausgaben E, ADR 0091). Recht wie die Kundenkarte; Termine nur an Standorten, die die Person sehen darf.
/// Kein Versand: das Stammblatt ist für das Team.
/// </summary>
internal static class CustomerCardEndpoints
{
    public static IEndpointRouteBuilder MapCustomerCardEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/customers/{customerId:guid}").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar)
            .MapGet("/card/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid customerId, ClaimsPrincipal user, AispaDbContext db, AccessService access, AnamnesisStore anamnesis, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var header = await headers.ForAsync(null, user, ct);
                var data = await LoadAsync(customer, (await access.CurrentAsync(ct))!, header.CreatedAt, db, anamnesis, ct);
                return DocumentFiles.Pdf(CustomerCard.Build(data, header), CustomerCard.FileName(customer));
            })
            .WithName("CustomerCardPdf")
            .WithSummary("Stammblatt einer Kundin oder eines Kunden als PDF: Kontakt, Wünsche und Vermerke, die nächsten Termine und die letzten fünf Besuche; keine Gesundheitsdaten.");
        return routes;
    }

    private static async Task<CustomerCard.Data> LoadAsync(
        Customer customer, UserAccess rights, DateTimeOffset now, AispaDbContext db, AnamnesisStore anamnesis, CancellationToken ct)
    {
        var id = customer.Id;
        var visible = rights.LocationIds.ToList();
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
            .Where(a => a.CustomerId == id && (rights.AllLocations || visible.Contains(a.LocationId))).ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().Select(l => new { l.Id, l.Name }).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var employeeIds = appointments.Select(a => a.EmployeeId).Distinct().ToList();
        var employees = (await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        CustomerCard.Visit Visit(Appointment a) =>
            new(a.Start, a.Treatment.Name, employees.GetValueOrDefault(a.EmployeeId, ""), locations.GetValueOrDefault(a.LocationId, ""), a.Status);

        var upcoming = appointments.Where(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn && a.Start >= now).OrderBy(a => a.Start).Take(CustomerCard.Shown).Select(Visit).ToList();
        var past = appointments.Where(a => a.Status == AppointmentStatus.Completed).OrderByDescending(a => a.Start).Take(CustomerCard.Shown).Select(Visit).ToList();
        var otherEmails = await db.CustomerEmails.AsNoTracking().Where(e => e.CustomerId == id).OrderBy(e => e.Address).Select(e => e.Address).ToListAsync(ct);
        var flags = (await db.CustomerFlags.AsNoTracking().Where(f => f.CustomerId == id && f.RemovedAt == null).Select(f => f.Kind).ToListAsync(ct)).Select(CustomerDisclosure.FlagLabel).ToList();
        var preferences = await db.CustomerPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerId == id, ct);
        var hasHealthData = await anamnesis.LatestSubmittedAtAsync(id, ct) is not null;
        return new CustomerCard.Data(customer, otherEmails, preferences, flags, upcoming, past, hasHealthData);
    }
}

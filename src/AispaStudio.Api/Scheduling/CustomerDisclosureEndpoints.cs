using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Datenschutz-Auskunft zu einem Gast als PDF (PDF-Ausgaben C, ADR 0092). Recht wie die Kundenkarte; die Anamnese steht nur drin, wenn die Person sie in der App sehen darf.
/// Kein Versand: die Leitung übergibt die Auskunft persönlich oder schickt sie selbst.
/// </summary>
internal static class CustomerDisclosureEndpoints
{
    public static IEndpointRouteBuilder MapCustomerDisclosureEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/customers/{customerId:guid}").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar)
            .MapGet("/history/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid customerId, ClaimsPrincipal user, AispaDbContext db, OnlineDbContext online, AccessService access, AnamnesisStore anamnesis, EntityHistory history,
                DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var rights = (await access.CurrentAsync(ct))!;
                var data = await LoadAsync(customer, rights, db, online, anamnesis, history, ct);
                return DocumentFiles.Pdf(CustomerDisclosure.Build(data, await headers.ForAsync(null, user, ct)), CustomerDisclosure.FileName(customer));
            })
            .WithName("CustomerDisclosurePdf")
            .WithSummary("Datenschutz-Auskunft nach Art. 15 DSGVO als PDF: Stammdaten, Kontakt, Wünsche, Termine, Zahlungen, Gutscheine, Online-Konto, Verlauf der Änderungen; "
                + "die Anamnese nur mit dem Recht „Anamnese lesen“, sonst ein fester Satz.");
        return routes;
    }

    private static async Task<CustomerDisclosure.Data> LoadAsync(
        Customer customer, UserAccess rights, AispaDbContext db, OnlineDbContext online, AnamnesisStore anamnesis, EntityHistory history, CancellationToken ct)
    {
        var id = customer.Id;
        var visible = rights.LocationIds.ToList();
        var locations = await db.Locations.AsNoTracking().Select(l => new { l.Id, l.Name, l.Currency }).ToDictionaryAsync(l => l.Id, ct);
        var visits = (await db.Appointments.AsNoTracking().Include(a => a.Treatment)
                .Where(a => a.CustomerId == id && (rights.AllLocations || visible.Contains(a.LocationId))).ToListAsync(ct))
            .Select(a => new CustomerDisclosure.Visit(a.Start, a.Treatment.Name, locations[a.LocationId].Name, a.Status, a.Price, locations[a.LocationId].Currency)).ToList();

        var registers = await db.CashRegisters.AsNoTracking().Select(r => new { r.Id, r.Code, r.LocationId }).ToDictionaryAsync(r => r.Id, ct);
        var payments = (await db.Receipts.AsNoTracking().Include(r => r.Payments).Where(r => r.CustomerId == id).ToListAsync(ct))
            .Select(r => new CustomerDisclosure.Payment(
                r.IssuedAt, $"{registers[r.RegisterId].Code}-{r.Number}", ReceiptPrinter.KindLabel(r.Kind), r.Total, PaymentMethods.Way(r.Payments.Select(p => p.Method)),
                locations[registers[r.RegisterId].LocationId].Currency)).ToList();

        var currency = locations.Values.OrderBy(l => l.Name).Select(l => l.Currency).FirstOrDefault() ?? "EUR";
        var vouchers = (await db.Vouchers.AsNoTracking().Where(v => v.CustomerId == id).OrderBy(v => v.CreatedAt).ToListAsync(ct))
            .Select(v => new CustomerDisclosure.VoucherLine(v.Code, v.Kind.ToString(), v.InitialValue, v.Balance, v.Status, v.UnitsLeft, v.ValidUntil, currency)).ToList();

        var otherEmails = await db.CustomerEmails.AsNoTracking().Where(e => e.CustomerId == id).OrderBy(e => e.Address).Select(e => e.Address).ToListAsync(ct);
        var flags = (await db.CustomerFlags.AsNoTracking().Where(f => f.CustomerId == id && f.RemovedAt == null).Select(f => f.Kind).ToListAsync(ct)).Select(CustomerDisclosure.FlagLabel).ToList();
        var preferences = await db.CustomerPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerId == id, ct);
        var hasAccount = await online.Users.AsNoTracking().AnyAsync(a => a.CustomerId == id, ct);
        var present = await anamnesis.LatestSubmittedAtAsync(id, ct) is not null;
        var health = new CustomerDisclosure.Health(present, present && rights.Has(AccessRight.ViewHealthData) ? await anamnesis.LatestAsync(id, ct) : null);
        return new CustomerDisclosure.Data(customer, otherEmails, preferences, flags, visits, payments, vouchers, hasAccount, health, await history.ForCustomerAsync(id, ct));
    }
}

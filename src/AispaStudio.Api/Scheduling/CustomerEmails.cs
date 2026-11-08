using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Weitere Mailadresse eines Kunden (privat, geschäftlich, Partner). Die Hauptadresse bleibt <see cref="Customer.Email"/>. Der
/// Abgleich eingehender Nachrichten (<c>CustomerMatcher</c>) erkennt den Kunden an jeder dieser Adressen.
/// </summary>
public sealed class CustomerEmail
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }

    /// <summary>Wie eingegeben (ohne Leerzeichen am Rand).</summary>
    public required string Address { get; set; }

    /// <summary>Klein geschrieben, für den Abgleich und die Eindeutigkeit je Kunde.</summary>
    public required string NormalizedAddress { get; set; }

    public static string Normalize(string address) => address.Trim().ToLowerInvariant();
}

internal static class CustomerEmailEndpoints
{
    private const int MaxPerCustomer = 10;

    public static IEndpointRouteBuilder MapCustomerEmailEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers/{customerId:guid}/emails").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/", async Task<Results<Ok<List<CustomerEmailDto>>, NotFound>> (Guid customerId, AispaDbContext db, CancellationToken ct) =>
                await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct)
                    ? TypedResults.Ok(await db.CustomerEmails.AsNoTracking().Where(e => e.CustomerId == customerId)
                        .OrderBy(e => e.Address).Select(e => new CustomerEmailDto(e.Id, e.Address)).ToListAsync(ct))
                    : TypedResults.NotFound())
            .WithName("ListCustomerEmails")
            .WithSummary("Weitere Mailadressen eines Kunden; die Hauptadresse steht im Kunden selbst.");

        group.MapPost("/", async Task<Results<Ok<List<CustomerEmailDto>>, NotFound, ValidationProblem>> (
                Guid customerId, CustomerEmailInput input, AispaDbContext db, CancellationToken ct) =>
            {
                if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var address = (input.Address ?? "").Trim();
                var errors = new Errors();
                errors.Check(address.Length is > 0 and <= 256 && address.Contains('@', StringComparison.Ordinal), nameof(input.Address), "Keine gültige E-Mail-Adresse.");
                var normalized = CustomerEmail.Normalize(address);
                errors.Check(!string.Equals(customer.Email?.Trim(), address, StringComparison.OrdinalIgnoreCase), nameof(input.Address), "Das ist schon die Hauptadresse.");
                errors.Check(!await db.CustomerEmails.AnyAsync(e => e.CustomerId == customerId && e.NormalizedAddress == normalized, ct),
                    nameof(input.Address), "Diese Adresse ist schon eingetragen.");
                errors.Check(await db.CustomerEmails.CountAsync(e => e.CustomerId == customerId, ct) < MaxPerCustomer, nameof(input.Address),
                    $"Höchstens {MaxPerCustomer} weitere Adressen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.CustomerEmails.Add(new CustomerEmail { CustomerId = customerId, Address = address, NormalizedAddress = normalized });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await ListAsync(db, customerId, ct));
            })
            .WithName("AddCustomerEmail")
            .WithSummary("Fügt eine weitere Mailadresse hinzu; Nachrichten von dort werden dem Kunden sicher zugeordnet.")
            .RequirePermission(AccessRight.Book);

        group.MapDelete("/{emailId:guid}", async Task<Results<Ok<List<CustomerEmailDto>>, NotFound>> (
                Guid customerId, Guid emailId, AispaDbContext db, CancellationToken ct) =>
            {
                if (await db.CustomerEmails.FirstOrDefaultAsync(e => e.Id == emailId && e.CustomerId == customerId, ct) is not { } email)
                {
                    return TypedResults.NotFound();
                }

                db.CustomerEmails.Remove(email);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await ListAsync(db, customerId, ct));
            })
            .WithName("RemoveCustomerEmail")
            .WithSummary("Entfernt eine weitere Mailadresse.")
            .RequirePermission(AccessRight.Book);

        return routes;
    }

    private static Task<List<CustomerEmailDto>> ListAsync(AispaDbContext db, Guid customerId, CancellationToken ct) =>
        db.CustomerEmails.AsNoTracking().Where(e => e.CustomerId == customerId).OrderBy(e => e.Address)
            .Select(e => new CustomerEmailDto(e.Id, e.Address)).ToListAsync(ct);
}

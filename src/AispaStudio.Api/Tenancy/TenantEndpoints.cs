using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Tenancy;

/// <summary>Spa-Unternehmen registrieren und Firmendaten pflegen.</summary>
internal static class TenantEndpoints
{
    private static readonly string[] Countries = ["AT", "DE", "CH"];

    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tenant").WithTags("Unternehmen");

        // Ohne Anmeldung: so kommt ein neues Spa ins System. Danach legt die Inhaberin ihren Passkey an und lädt das Team ein.
        group.MapPost("/signup", async Task<Results<Ok<CurrentUser>, ValidationProblem>> (
                TenantSignup input, AispaDbContext db, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                TenantContext tenants, AccessService access, TimeProvider time, CancellationToken ct) =>
            {
                var errors = Validate(input.SpaName, input.Country);
                errors.Require(nameof(input.OwnerName), input.OwnerName);
                var email = input.OwnerEmail?.Trim() ?? "";
                errors.Check(email.Contains('@'), nameof(input.OwnerEmail), "Bitte eine E-Mail-Adresse angeben.");
                errors.Check(await users.FindByEmailAsync(email) is null, nameof(input.OwnerEmail), "Für diese E-Mail gibt es schon ein Konto.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var now = time.GetUtcNow();
                var tenant = new Tenant { Name = input.SpaName.Trim(), Country = input.Country.ToUpperInvariant(), CreatedAt = now };
                var user = new AispaUser { UserName = email, Email = email, DisplayName = input.OwnerName.Trim() };
                var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    db.Tenants.Add(tenant);
                    await db.SaveChangesAsync(ct);
                    var created = await users.CreateAsync(user);
                    if (!created.Succeeded)
                    {
                        return created;
                    }

                    tenants.Use(tenant.Id);
                    await CalendarSubscriptionDefaults.AddMissingAsync(db, tenant.Country, now, ct);
                    db.StaffAccess.Add(new StaffAccess { UserId = user.Id, Role = StaffRole.Owner, AllLocations = true, UpdatedAt = now });
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return created;
                });
                if (!result.Succeeded)
                {
                    return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                await signIn.SignInAsync(user, isPersistent: false);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .RequireHosting(standalone: false)
            .WithName("SignUpTenant")
            .WithSummary("Registriert ein neues Spa-Unternehmen mit Inhaber-Konto und meldet an. Danach einen Passkey hinzufügen.");

        group.MapGet("/", async Task<Results<Ok<TenantDto>, NotFound>> (AispaDbContext db, TenantContext tenants, CancellationToken ct) =>
                await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenants.TenantId, ct) is { } t
                    ? TypedResults.Ok(ToDto(t))
                    : TypedResults.NotFound())
            .WithName("GetTenant")
            .RequirePermission(AccessRight.ViewCalendar)
            .WithSummary("Name, Land und Firmendaten des Spas.");

        group.MapPut("/", async Task<Results<Ok<TenantDto>, ValidationProblem, NotFound>> (
                TenantInput input, AispaDbContext db, TenantContext tenants, CancellationToken ct) =>
            {
                var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenants.TenantId, ct);
                if (tenant is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = Validate(input.Name, input.Country);
                if (input.Company is { } company)
                {
                    CompanyProfiles.Validate(company, errors);
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                tenant.Name = input.Name.Trim();
                tenant.Country = input.Country.ToUpperInvariant();
                if (input.Company is { } profile)
                {
                    CompanyProfiles.Apply(tenant, profile);
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(tenant));
            })
            .WithName("UpdateTenant")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Name, Land und Firmendaten ändern. Firmendaten gelten für alle Standorte ohne eigene Unternehmensangaben.");

        // Verlauf der Firmendaten (R05 Slice 93, ADR 0082): sehen darf ihn, wer sie ändern darf.
        group.MapGet("/history", async Task<Results<Ok<List<HistoryEntry>>, NotFound>> (TenantContext tenants, EntityHistory history, CancellationToken ct) =>
                tenants.TenantId is { } tenant
                    ? TypedResults.Ok(await history.ForCompanyAsync(tenant, ct))
                    : TypedResults.NotFound())
            .WithName("GetCompanyHistory")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Wer Name, Land und Firmendaten wann geändert hat, mit alten und neuen Werten, neueste oben.");

        routes.MapEmbeddingSettingsEndpoints();
        routes.MapEmbeddingPolicyEndpoint();
        return routes;
    }

    private static TenantDto ToDto(Tenant t) => new(t.Id, t.Name, t.Country, CompanyProfiles.Of(t));

    private static Errors Validate(string? name, string? country)
    {
        var errors = new Errors();
        errors.Require("name", name);
        errors.Check(Countries.Contains(country?.ToUpperInvariant()), "country", "Land: AT, DE oder CH.");
        return errors;
    }
}

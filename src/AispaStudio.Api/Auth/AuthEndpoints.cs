using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Auth;

/// <summary>
/// Anmeldung ausschließlich über Passkeys (WebAuthn) mit ASP.NET Core Identity.
/// Die Options-Endpunkte liefern das JSON, das der Browser an navigator.credentials.create()/.get() übergibt.
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes, IHostEnvironment env)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Anmeldung");
        // Passkeys nur im Server-Betrieb; im Einzelplatz gelten Benutzername und Passwort (StandaloneAuth, ADR 0041).
        var passkeys = group.MapGroup("").RequireHosting(standalone: false);

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .AllowChannel(AccessChannel.StaffApp)
            .WithName("GetCurrentUser");

        passkeys.MapPost("/passkey/request-options", async (SignInManager<AispaUser> signIn) =>
                TypedResults.Text(await signIn.MakePasskeyRequestOptionsAsync(user: null), "application/json"))
            .WithName("GetPasskeyRequestOptions")
            .WithSummary("Optionen für navigator.credentials.get() (Anmeldung).");

        passkeys.MapPost("/passkey/login", async Task<Results<NoContent, UnauthorizedHttpResult>> (
                PasskeyCredential credential, SignInManager<AispaUser> signIn) =>
            {
                try
                {
                    var result = await signIn.PasskeySignInAsync(credential.CredentialJson);
                    return result.Succeeded ? TypedResults.NoContent() : TypedResults.Unauthorized();
                }
                catch (InvalidOperationException)
                {
                    // Keine laufende Anmeldung (request-options nicht abgerufen oder abgelaufen).
                    return TypedResults.Unauthorized();
                }
            })
            .WithName("LoginWithPasskey");

        passkeys.MapPost("/passkey/creation-options", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, UserManager<AispaUser> users, SignInManager<AispaUser> signIn) =>
            {
                var user = await users.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var entity = new PasskeyUserEntity
                {
                    Id = user.Id,
                    Name = user.Email ?? user.UserName ?? user.Id,
                    DisplayName = user.DisplayName,
                };
                return TypedResults.Text(await signIn.MakePasskeyCreationOptionsAsync(entity), "application/json");
            })
            .RequireAuthorization()
            .AllowChannel(AccessChannel.StaffApp)
            .WithName("GetPasskeyCreationOptions")
            .WithSummary("Optionen für navigator.credentials.create() (neuen Passkey hinzufügen).");

        passkeys.MapPost("/passkey", async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> (
                PasskeyCredential credential, ClaimsPrincipal principal,
                UserManager<AispaUser> users, SignInManager<AispaUser> signIn) =>
            {
                var user = await users.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var attestation = await signIn.PerformPasskeyAttestationAsync(credential.CredentialJson);
                if (!attestation.Succeeded)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(PasskeyCredential.CredentialJson)] = [attestation.Failure.Message],
                    });
                }

                var stored = await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
                return stored.Succeeded
                    ? TypedResults.NoContent()
                    : TypedResults.ValidationProblem(stored.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            })
            .RequireAuthorization()
            .AllowChannel(AccessChannel.StaffApp)
            .WithName("AddPasskey");

        group.MapPost("/logout", async (SignInManager<AispaUser> signIn) =>
            {
                await signIn.SignOutAsync();
                return TypedResults.NoContent();
            })
            .WithName("Logout");

        if (env.IsDevelopment())
        {
            // Platzhalter bis zur Einladung per Link/QR-Code: legt ein Konto an und meldet es an,
            // damit lokal ein erster Passkey registriert werden kann. Nur in der Entwicklungsumgebung aktiv.
            group.MapPost("/dev/register", async Task<Results<Ok<CurrentUser>, ValidationProblem>> (
                    DevRegistration registration, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                    AispaDbContext db, AccessService access, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
                {
                    tenants.Use(await DevTenantAsync(registration, db, time, ct));
                    await CalendarSubscriptionDefaults.AddMissingAsync(db, await db.Tenants.Where(t => t.Id == tenants.TenantId).Select(t => t.Country).FirstAsync(ct), time.GetUtcNow(), ct);
                    var user = new AispaUser
                    {
                        UserName = registration.Email,
                        Email = registration.Email,
                        DisplayName = registration.DisplayName,
                    };
                    var created = await users.CreateAsync(user);
                    if (!created.Succeeded)
                    {
                        return TypedResults.ValidationProblem(created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                    }

                    db.StaffAccess.Add(new StaffAccess
                    {
                        UserId = user.Id,
                        Role = registration.Role,
                        AllLocations = registration.LocationIds is null,
                        LocationIds = [.. registration.LocationIds ?? []],
                        UpdatedAt = time.GetUtcNow(),
                    });
                    if (registration.EmployeeId is { } employeeId)
                    {
                        await db.Employees.Where(e => e.Id == employeeId).ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, user.Id), ct);
                    }

                    await db.SaveChangesAsync(ct);
                    await signIn.SignInAsync(user, isPersistent: false);
                    return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
                })
                .WithName("DevRegister")
                .ExcludeFromDescription();

            // Testkonten des Beispiel-Spas (Demo.DemoAccounts): Auswahl auf der Anmeldeseite und Anmeldung ohne Passkey.
            group.MapGet("/dev/accounts", async (AispaDbContext db, CancellationToken ct) =>
                {
                    var suffix = "@" + DemoAccounts.Domain;
                    var members = await (
                            from a in db.StaffAccess.IgnoreQueryFilters().AsNoTracking()
                            join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                            where a.IsActive && u.Email != null && u.Email.EndsWith(suffix)
                            select new { Access = a, u.Email, u.DisplayName })
                        .ToListAsync(ct);
                    var locations = await db.Locations.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
                    return members
                        .OrderBy(m => m.Access.Role).ThenBy(m => m.DisplayName, StringComparer.Ordinal)
                        .Select(m => new DevAccount(m.Email!, m.DisplayName, m.Access.Role,
                            m.Access.AllLocations ? ["alle Standorte"] : [.. m.Access.LocationIds.Select(id => locations.GetValueOrDefault(id, "?"))]))
                        .ToList();
                })
                .WithName("DevAccounts")
                .ExcludeFromDescription();

            group.MapPost("/dev/login", async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult>> (
                    DevLogin login, UserManager<AispaUser> users, SignInManager<AispaUser> signIn, AispaDbContext db,
                    AccessService access, TenantContext tenants, CancellationToken ct) =>
                {
                    // Nur die Testkonten: ein echtes Konto bleibt auch in der Entwicklung hinter seinem Passkey.
                    var user = login.Email?.EndsWith("@" + DemoAccounts.Domain, StringComparison.OrdinalIgnoreCase) == true
                        ? await users.FindByEmailAsync(login.Email)
                        : null;
                    var tenantId = user is null ? null : await TenantModel.TenantOfAsync(db.StaffAccess.Where(a => a.UserId == user.Id), ct);
                    if (user is null || tenantId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    tenants.Use(tenantId.Value);
                    await signIn.SignInAsync(user, login.Persistent);
                    return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
                })
                .WithName("DevLogin")
                .ExcludeFromDescription();
        }

        return routes;
    }

    /// <summary>Unternehmen für ein Testkonto: neu, das der angegebenen Standorte oder das erste (mit den Demodaten).</summary>
    private static async Task<Guid> DevTenantAsync(DevRegistration registration, AispaDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!registration.NewTenant)
        {
            var locations = registration.LocationIds ?? [];
            var existing = locations.Count > 0
                ? await db.Locations.IgnoreQueryFilters().Where(l => locations.Contains(l.Id))
                    .Select(l => (Guid?)EF.Property<Guid>(l, TenantModel.Property)).FirstOrDefaultAsync(ct)
                : await db.Tenants.OrderBy(t => t.CreatedAt).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (existing is { } id)
            {
                return id;
            }
        }

        var tenant = new Tenant { Name = $"Test-Spa {time.GetUtcNow():yyyy-MM-dd HH:mm:ss}", Country = "AT", CreatedAt = time.GetUtcNow() };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant.Id;
    }

    private static async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult>> GetCurrentUser(
        ClaimsPrincipal principal, UserManager<AispaUser> users, AccessService access, CancellationToken ct)
    {
        var user = await users.GetUserAsync(principal);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct) with { Channel = Channels.Of(principal) });
    }
}

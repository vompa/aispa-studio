using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Auth;

/// <summary>
/// Klassische Benutzerverwaltung im Einzelplatz-Betrieb (ADR 0041): Einrichtung beim ersten Start, Anmeldung mit Benutzername
/// und Passwort, eigenes Passwort ändern. Zugänge legt die Leitung unter /api/staff/accounts an. Passkeys und Einladungen gibt es hier nicht.
/// </summary>
internal static class StandaloneAuth
{
    private static readonly string[] Countries = ["AT", "DE", "CH"];

    /// <summary>Ohne Adresse (im selben Prozess, z. B. in Tests) oder von 127.0.0.1 bzw. ::1.</summary>
    private static bool IsLocal(IPAddress? remote) =>
        remote is null || IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote);

    public static IEndpointRouteBuilder MapStandaloneAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth/standalone").WithTags("Anmeldung").RequireHosting(standalone: true);

        group.MapGet("/setup", async (AispaDbContext db, IOptions<SyncOptions> sync, CancellationToken ct) =>
            {
                if (await db.Tenants.IgnoreQueryFilters().AnyAsync(ct))
                {
                    return new StandaloneSetupState(false);
                }

                if (string.IsNullOrWhiteSpace(sync.Value.CentralConnectionString))
                {
                    return new StandaloneSetupState(true);
                }

                var error = await db.Database.SqlQueryRaw<string>(
                    "SELECT last_error AS \"Value\" FROM sync.peers WHERE last_error IS NOT NULL ORDER BY last_run DESC LIMIT 1").ToListAsync(ct);
                return new StandaloneSetupState(true, Joining: true, JoinError: error.FirstOrDefault());
            })
            .WithName("GetStandaloneSetup")
            .WithSummary("Einzelplatz-Betrieb: ob die Einrichtung beim ersten Start noch aussteht (noch kein Spa angelegt).");

        group.MapPost("/setup", async Task<Results<Ok<CurrentUser>, ValidationProblem, ProblemHttpResult>> (
                StandaloneSetup input, HttpContext http, AispaDbContext db, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                TenantContext tenants, AccessService access, TimeProvider time, IOptions<SyncOptions> sync, CancellationToken ct) =>
            {
                // Ist der PC im Netz freigegeben, darf ein anderer Arbeitsplatz das Spa nicht vor der Inhaberin anlegen.
                if (!IsLocal(http.Connection.RemoteIpAddress))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Nur am PC mit der Datenbank.",
                        detail: "Die Einrichtung geht nur an dem PC, auf dem AISPA Studio mit der Datenbank läuft.");
                }

                if (await db.Tenants.IgnoreQueryFilters().AnyAsync(ct))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Schon eingerichtet.",
                        detail: "Dieses Spa ist schon angelegt. Bitte anmelden; weitere Zugänge legt die Leitung unter „Zugänge“ an.");
                }

                if (!string.IsNullOrWhiteSpace(sync.Value.CentralConnectionString))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Wird verbunden.",
                        detail: "Dieser PC verbindet sich gerade mit einem bestehenden Spa aus der zentralen Datenbank; ein neues Spa geht dann nicht.");
                }

                var errors = new Errors();
                errors.Require(nameof(input.SpaName), input.SpaName);
                errors.Require(nameof(input.LocationName), input.LocationName);
                errors.Require(nameof(input.OwnerName), input.OwnerName);
                errors.Require(nameof(input.UserName), input.UserName);
                var country = input.Country?.Trim().ToUpperInvariant() ?? "";
                errors.Check(Countries.Contains(country), nameof(input.Country), "Land: AT, DE oder CH.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var now = time.GetUtcNow();
                var tenant = new Tenant { Name = input.SpaName.Trim(), Country = country, CreatedAt = now };
                var user = new AispaUser { UserName = input.UserName.Trim(), DisplayName = input.OwnerName.Trim() };
                var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    db.Tenants.Add(tenant);
                    await db.SaveChangesAsync(ct);
                    var created = await users.CreateAsync(user, input.Password ?? "");
                    if (!created.Succeeded)
                    {
                        return created;
                    }

                    tenants.Use(tenant.Id);
                    await CalendarSubscriptionDefaults.AddMissingAsync(db, country, now, ct);
                    db.StaffAccess.Add(new StaffAccess { UserId = user.Id, Role = StaffRole.Owner, AllLocations = true, UpdatedAt = now });
                    db.Locations.Add(new Location
                    {
                        Name = input.LocationName.Trim(),
                        TimeZone = country switch { "DE" => "Europe/Berlin", "CH" => "Europe/Zurich", _ => "Europe/Vienna" },
                        Currency = country == "CH" ? "CHF" : "EUR",
                    });
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return created;
                });
                if (!result.Succeeded)
                {
                    return Problem(result);
                }

                await signIn.SignInAsync(user, isPersistent: true);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("CompleteStandaloneSetup")
            .WithSummary("Einrichtung beim ersten Start: legt Spa, ersten Standort und das Inhaber-Konto mit Passwort an und meldet an. Nur einmal und nur am PC selbst möglich (nicht über das Netz).");

        group.MapPost("/login", async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult, ProblemHttpResult>> (
                PasswordLogin login, UserManager<AispaUser> users, SignInManager<AispaUser> signIn, AispaDbContext db,
                AccessService access, TenantContext tenants, CancellationToken ct) =>
            {
                var user = await users.FindByNameAsync(login.UserName?.Trim() ?? "");
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var result = await signIn.CheckPasswordSignInAsync(user, login.Password ?? "", lockoutOnFailure: true);
                if (result.IsLockedOut)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status423Locked, title: "Vorübergehend gesperrt.",
                        detail: "Zu viele Fehlversuche. Bitte in fünf Minuten noch einmal, oder die Leitung setzt ein neues Passwort.");
                }

                if (!result.Succeeded)
                {
                    return TypedResults.Unauthorized();
                }

                if (await TenantModel.TenantOfAsync(db.StaffAccess.IgnoreQueryFilters().Where(a => a.UserId == user.Id), ct) is { } tenantId)
                {
                    tenants.Use(tenantId);
                }

                await signIn.SignInAsync(user, login.Persistent);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("LoginWithPassword")
            .WithSummary("Anmeldung mit Benutzername und Passwort. Nach fünf Fehlversuchen fünf Minuten gesperrt (423).");

        group.MapPost("/password", async Task<Results<Ok<CurrentUser>, ValidationProblem, UnauthorizedHttpResult>> (
                PasswordChange change, ClaimsPrincipal principal, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                AccessService access, CancellationToken ct) =>
            {
                var user = await users.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var result = await users.ChangePasswordAsync(user, change.CurrentPassword ?? "", change.NewPassword ?? "");
                if (!result.Succeeded)
                {
                    return Problem(result);
                }

                user.MustChangePassword = false;
                await users.UpdateAsync(user);
                // Neues Cookie, damit die Anmeldung mit dem geänderten Sicherheitsstempel gültig bleibt.
                await signIn.RefreshSignInAsync(user);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Eigenes Passwort ändern; nach einem Startpasswort Pflicht, vorher geht nichts anderes.");

        group.MapPost("/recover", async Task<Results<Ok<CurrentUser>, ValidationProblem, ProblemHttpResult>> (
                PasswordRecovery input, HttpContext http, IConfiguration configuration, IOptions<HealthDataOptions> health,
                UserManager<AispaUser> users, SignInManager<AispaUser> signIn, AispaDbContext db, AccessService access, TenantContext tenants,
                CancellationToken ct) =>
            {
                // Über das Netz nicht: dort könnte jeder im WLAN Schlüssel durchprobieren.
                if (!IsLocal(http.Connection.RemoteIpAddress))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Nur am PC mit der Datenbank.",
                        detail: "Das Passwort mit dem Wiederherstellungsschlüssel neu setzen geht nur am Haupt-PC.");
                }

                if (await RecoveryBlobAsync(configuration, ct) is not { } sealedKey)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Kein Wiederherstellungsschlüssel.",
                        detail: "Auf diesem PC gibt es noch keinen Wiederherstellungsschlüssel; er entsteht beim Einrichten der Sicherung. "
                            + "Ein anderes Konto mit dem Recht „Zugänge verwalten“ kann unter „Zugänge“ ein neues Startpasswort vergeben.");
                }

                if (!Opens(sealedKey, input.RecoveryCode, health.Value))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["recoveryCode"] = ["Der Wiederherstellungsschlüssel passt nicht. Er steht auf dem Ausdruck oder Zettel von der Einrichtung der Sicherung."],
                    });
                }

                var user = await users.FindByNameAsync(input.UserName?.Trim() ?? "");
                if (user is null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["userName"] = ["Diesen Benutzernamen gibt es nicht."] });
                }

                // Erst prüfen, dann tauschen: sonst bliebe das Konto bei einem zu kurzen Passwort ohne Passwort.
                foreach (var validator in users.PasswordValidators)
                {
                    var valid = await validator.ValidateAsync(users, user, input.NewPassword ?? "");
                    if (!valid.Succeeded)
                    {
                        return Problem(valid);
                    }
                }

                var result = await users.RemovePasswordAsync(user);
                if (result.Succeeded)
                {
                    result = await users.AddPasswordAsync(user, input.NewPassword ?? "");
                }

                if (!result.Succeeded)
                {
                    return Problem(result);
                }

                // Selbst gewählt, also kein Pflichtwechsel; Sperre aufheben und alte Anmeldungen beenden.
                user.MustChangePassword = false;
                await users.UpdateAsync(user);
                await users.ResetAccessFailedCountAsync(user);
                await users.SetLockoutEndDateAsync(user, null);
                await users.UpdateSecurityStampAsync(user);

                if (await TenantModel.TenantOfAsync(db.StaffAccess.IgnoreQueryFilters().Where(a => a.UserId == user.Id), ct) is { } tenantId)
                {
                    tenants.Use(tenantId);
                }

                await signIn.SignInAsync(user, isPersistent: true);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("RecoverPassword")
            .WithSummary("Passwort vergessen ohne Leitung: mit dem Wiederherstellungsschlüssel aus der Sicherung das Passwort eines Kontos neu setzen und anmelden. Nur am PC mit der Datenbank.");

        return routes;
    }

    /// <summary>
    /// Schlüssel für Gesundheitsdaten, mit dem Wiederherstellungsschlüssel verschlüsselt (<c>Hosting:RecoveryFile</c>, legt die
    /// Windows-App an). Bei jeder Anfrage neu gelesen, damit ein neuer Schlüssel ohne Neustart gilt.
    /// </summary>
    private static async Task<string?> RecoveryBlobAsync(IConfiguration configuration, CancellationToken ct) =>
        configuration["Hosting:RecoveryFile"] is { Length: > 0 } file && File.Exists(file) ? await File.ReadAllTextAsync(file, ct) : null;

    /// <summary>
    /// Der Code passt, wenn er die Datei öffnet und darin genau der Schlüssel dieses PCs steckt. Wer nur die Datei austauscht, kennt
    /// den Schlüssel nicht und kann so keinen eigenen Code unterschieben.
    /// </summary>
    private static bool Opens(string sealedKey, string? code, HealthDataOptions health) =>
        KeySeal.IsComplete(code)
        && KeySeal.TryOpen(sealedKey, code!, out var keyId, out var key)
        && health.Keys.TryGetValue(keyId, out var mine)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(mine), Encoding.UTF8.GetBytes(key));

    /// <summary>Identity-Fehler als Validierungsproblem; die Texte sind auf Deutsch, siehe <see cref="GermanIdentityErrors"/>.</summary>
    public static ValidationProblem Problem(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors.GroupBy(e => Field(e.Code))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    private static string Field(string code) =>
        code.StartsWith("Password", StringComparison.Ordinal) ? "password"
        : code.StartsWith("UserName", StringComparison.Ordinal) || code.StartsWith("DuplicateUserName", StringComparison.Ordinal) ? "userName"
        : code.StartsWith("Email", StringComparison.Ordinal) || code.StartsWith("DuplicateEmail", StringComparison.Ordinal) ? "email"
        : char.ToLowerInvariant(code[0]) + code[1..];
}

/// <summary>Fehlertexte von Identity auf Deutsch, weil das Team sie liest.</summary>
internal sealed class GermanIdentityErrors : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Das Passwort braucht mindestens {length} Zeichen." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Den Benutzernamen „{userName}“ gibt es schon." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Benutzername: Buchstaben, Ziffern und . _ - @ + sind erlaubt, keine Leerzeichen." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"Für „{email}“ gibt es schon ein Konto." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Bitte eine gültige E-Mail-Adresse angeben." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Das bisherige Passwort stimmt nicht." };
}

using System.Security.Claims;
using System.Security.Cryptography;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>PIN einer Person für den Schnellwechsel am Empfangsgerät. Gespeichert wie ein Passwort (PBKDF2 über Identity).</summary>
public sealed class StaffPin
{
    public required string UserId { get; init; }
    public required string PinHash { get; set; }

    /// <summary>Fehlversuche seit dem letzten Erfolg; nach <see cref="QuickSwitch.MaxAttempts"/> ist die PIN eine Viertelstunde gesperrt.</summary>
    public int FailedAttempts { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Ein Browser oder Gerät, das die Leitung als Empfangsgerät eines Standorts angemeldet hat. Nur dort gilt die PIN;
/// gespeichert ist der Hash des Geräte-Cookies.
/// </summary>
public sealed class SharedDevice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required byte[] TokenHash { get; init; }
    public required string Name { get; set; }
    public Guid LocationId { get; init; }
    public required string RegisteredByUserId { get; init; }

    /// <summary>Nach so vielen Minuten ohne Eingabe meldet das Gerät die per PIN angemeldete Person ab (Server und Oberfläche).</summary>
    public int IdleMinutes { get; set; } = SharedDeviceInput.DefaultIdleMinutes;

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

/// <summary>
/// Schnellwechsel am Empfang: Am gemeinsamen Gerät wechselt das Team mit Name und PIN die angemeldete Person, ohne Passkey.
/// Die PIN allein meldet nirgends an: sie gilt nur auf einem Gerät, das jemand mit Verwaltungsrecht als Empfangsgerät
/// angemeldet hat, nur für Personen mit Zugang an dessen Standort, und nach fünf Fehlversuchen ist sie 15 Minuten gesperrt.
/// Die Anmeldung per PIN ist nicht dauerhaft: sie endet mit dem Browser und nach der am Gerät eingestellten Zeit ohne Eingabe
/// (Cookie mit gleitendem Ablauf, die Oberfläche warnt vorher). Rolle, Standorte, Sperren und Befristung gelten wie sonst.
/// </summary>
internal static class QuickSwitch
{
    public const int MaxAttempts = 5;
    public const string CookieName = "aispa.device";

    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DeviceLifetime = TimeSpan.FromDays(365);

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StaffPin>(e =>
        {
            e.ToTable("StaffPins");
            e.HasKey(p => p.UserId);
            e.Property(p => p.UserId).HasMaxLength(450);
            e.Property(p => p.PinHash).HasMaxLength(200);
            e.HasOne<AispaUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SharedDevice>(e =>
        {
            e.ToTable("SharedDevices");
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Name).HasMaxLength(100);
            e.Property(d => d.RegisteredByUserId).HasMaxLength(450);
            e.HasIndex(d => d.TokenHash).IsUnique();
            e.HasIndex(d => d.LocationId);
        });
    }

    public static IEndpointRouteBuilder MapQuickSwitch(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Anmeldung");

        group.MapPut("/pin", async Task<Results<NoContent, ValidationProblem, ForbidHttpResult>> (
                QuickSwitchPin input, ClaimsPrincipal principal, AccessService accessService, UserManager<AispaUser> users,
                AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var access = await accessService.GetAsync(principal, ct);
                var user = await users.GetUserAsync(principal);
                if (access is null || user is null)
                {
                    return TypedResults.Forbid();
                }

                if (PinError(input.Pin, access.Role) is { } error)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.Pin)] = [error] });
                }

                var now = time.GetUtcNow();
                var pin = await db.Set<StaffPin>().FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
                if (pin is null)
                {
                    db.Add(new StaffPin { UserId = user.Id, PinHash = users.PasswordHasher.HashPassword(user, input.Pin), UpdatedAt = now });
                }
                else
                {
                    pin.PinHash = users.PasswordHasher.HashPassword(user, input.Pin);
                    pin.FailedAttempts = 0;
                    pin.LockedUntil = null;
                    pin.UpdatedAt = now;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .WithName("SetQuickSwitchPin")
            .WithSummary("Eigene PIN für den Schnellwechsel am Empfangsgerät festlegen oder ändern (4 bis 8 Ziffern, Inhaber mindestens 6).");

        group.MapDelete("/pin", async (ClaimsPrincipal principal, AispaDbContext db, CancellationToken ct) =>
            {
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                await db.Set<StaffPin>().Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .WithName("RemoveQuickSwitchPin")
            .WithSummary("Eigene PIN entfernen; danach ist am Empfangsgerät nur noch die Anmeldung mit Passkey möglich.");

        group.MapGet("/quick-switch", async (HttpContext http, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var device = await DeviceAsync(http, db, ct);
                if (device is null)
                {
                    return new QuickSwitchState(null, []);
                }

                tenants.Use(db.TenantOf(device));
                var now = time.GetUtcNow();
                var people = await (
                        from a in db.StaffAccess.AsNoTracking()
                        join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                        join p in db.Set<StaffPin>().AsNoTracking() on a.UserId equals p.UserId
                        where a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now)
                            && (a.AllLocations || a.LocationIds.Contains(device.LocationId))
                        orderby u.DisplayName
                        select new QuickSwitchPerson(u.Id, u.DisplayName, a.Role))
                    .ToListAsync(ct);
                return new QuickSwitchState(await ToDtoAsync(device, db, ct), people);
            })
            .WithName("GetQuickSwitch")
            .WithSummary("Ob dieser Browser ein Empfangsgerät ist und wer dort per PIN wechseln kann. Ohne Anmeldung aufrufbar; ohne Gerät leer.");

        group.MapPost("/quick-switch", async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult, ProblemHttpResult>> (
                QuickSwitchRequest input, HttpContext http, AispaDbContext db, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                AccessService access, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var device = await DeviceAsync(http, db, ct);
                if (device is null)
                {
                    return TypedResults.Unauthorized();
                }

                tenants.Use(db.TenantOf(device));
                var now = time.GetUtcNow();
                var pin = await db.Set<StaffPin>().FirstOrDefaultAsync(p => p.UserId == input.UserId, ct);
                var user = pin is null ? null : await users.FindByIdAsync(input.UserId);
                if (pin is null || user is null)
                {
                    return TypedResults.Unauthorized();
                }

                if (pin.LockedUntil is { } until && until > now)
                {
                    return TypedResults.Problem(
                        $"Zu viele Fehlversuche. Die PIN ist noch {Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes))} Minuten gesperrt; bis dahin mit Passkey anmelden.",
                        statusCode: StatusCodes.Status423Locked);
                }

                var verified = users.PasswordHasher.VerifyHashedPassword(user, pin.PinHash, input.Pin ?? "");
                var covers = await db.StaffAccess.AsNoTracking().AnyAsync(a => a.UserId == user.Id && a.IsActive
                    && (a.ExpiresAt == null || a.ExpiresAt > now) && (a.AllLocations || a.LocationIds.Contains(device.LocationId)), ct);
                if (verified == PasswordVerificationResult.Failed || !covers)
                {
                    pin.FailedAttempts++;
                    if (pin.FailedAttempts >= MaxAttempts)
                    {
                        pin.FailedAttempts = 0;
                        pin.LockedUntil = now + Lockout;
                    }

                    await db.SaveChangesAsync(ct);
                    return TypedResults.Unauthorized();
                }

                pin.FailedAttempts = 0;
                pin.LockedUntil = null;
                device.LastUsedAt = now;
                await db.SaveChangesAsync(ct);
                // Gleitender Ablauf: jede Anfrage verlängert, ohne Anfragen endet die Anmeldung nach der Leerlaufzeit des Geräts.
                await signIn.SignInAsync(user, new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    IssuedUtc = now,
                    ExpiresUtc = now.AddMinutes(device.IdleMinutes),
                });
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("QuickSwitch")
            .WithSummary("Am Empfangsgerät zu einer Person wechseln: Name wählen, PIN eingeben. Nach fünf Fehlversuchen 15 Minuten gesperrt (423).");

        group.MapPost("/shared-device", async Task<Results<Ok<SharedDeviceDto>, ValidationProblem, NotFound>> (
                SharedDeviceInput input, HttpContext http, ClaimsPrincipal principal, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var name = (input.Name ?? "").Trim();
                if (name.Length is 0 or > 100)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.Name)] = ["Bitte einen Namen für das Gerät angeben (höchstens 100 Zeichen)."] });
                }

                if (input.IdleMinutes is < 1 or > SharedDeviceInput.MaxIdleMinutes)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.IdleMinutes)] = [$"Die Zeit bis zum Abmelden liegt zwischen 1 und {SharedDeviceInput.MaxIdleMinutes} Minuten."] });
                }

                if (!await db.Locations.AnyAsync(l => l.Id == input.LocationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var now = time.GetUtcNow();
                if (await DeviceAsync(http, db, ct) is { } previous)
                {
                    // Dasselbe Gerät neu anmelden: das alte Cookie gilt nicht mehr.
                    previous.RevokedAt = now;
                }

                var token = RandomNumberGenerator.GetBytes(32);
                var device = new SharedDevice
                {
                    TokenHash = SHA256.HashData(token),
                    Name = name,
                    LocationId = input.LocationId,
                    IdleMinutes = input.IdleMinutes,
                    RegisteredByUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    CreatedAt = now,
                };
                db.Add(device);
                await db.SaveChangesAsync(ct);
                http.Response.Cookies.Append(CookieName, Convert.ToBase64String(token), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = now + DeviceLifetime,
                    Path = "/",
                });
                return TypedResults.Ok(await ToDtoAsync(device, db, ct));
            })
            .RequirePermission(AccessRight.ManageStaff)
            .WithName("RegisterSharedDevice")
            .WithSummary("Diesen Browser als Empfangsgerät eines Standorts anmelden (Cookie, ein Jahr). Danach wechselt das Team dort per PIN; nach IdleMinutes ohne Eingabe wird abgemeldet.");

        group.MapDelete("/shared-device", async (HttpContext http, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await DeviceAsync(http, db, ct) is { } device)
                {
                    device.RevokedAt = time.GetUtcNow();
                    await db.SaveChangesAsync(ct);
                }

                http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
                return TypedResults.NoContent();
            })
            .RequirePermission(AccessRight.ManageStaff)
            .WithName("RevokeSharedDevice")
            .WithSummary("Dieses Gerät ist kein Empfangsgerät mehr; der Schnellwechsel per PIN geht dort nicht mehr.");

        return routes;
    }

    /// <summary>Das Empfangsgerät zum Cookie dieser Anfrage, ohne Mandantenfilter (die Anfrage kommt ohne Anmeldung).</summary>
    private static async Task<SharedDevice?> DeviceAsync(HttpContext http, AispaDbContext db, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
        {
            return null;
        }

        byte[] token;
        try
        {
            token = Convert.FromBase64String(cookie);
        }
        catch (FormatException)
        {
            return null;
        }

        var hash = SHA256.HashData(token);
        return await db.Set<SharedDevice>().IgnoreQueryFilters().FirstOrDefaultAsync(d => d.TokenHash == hash && d.RevokedAt == null, ct);
    }

    private static async Task<SharedDeviceDto> ToDtoAsync(SharedDevice device, AispaDbContext db, CancellationToken ct) =>
        new(device.Id, device.Name, device.LocationId,
            await db.Locations.IgnoreQueryFilters().Where(l => l.Id == device.LocationId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? "",
            device.CreatedAt, device.IdleMinutes);

    /// <summary>Regeln für die PIN; null, wenn sie passt.</summary>
    public static string? PinError(string? pin, StaffRole role)
    {
        var ownerLevel = Roles.IsOwnerLevel(role);
        var min = ownerLevel ? 6 : 4;
        if (pin is null || pin.Length < min || pin.Length > 8 || !pin.All(char.IsAsciiDigit))
        {
            return ownerLevel
                ? "Die PIN besteht aus 6 bis 8 Ziffern (Inhaber- und Manager-Zugänge brauchen mindestens sechs)."
                : "Die PIN besteht aus 4 bis 8 Ziffern.";
        }

        if (pin.Distinct().Count() == 1)
        {
            return "Bitte nicht nur dieselbe Ziffer verwenden.";
        }

        return null;
    }

    /// <summary>
    /// Freigabe am selben Platz (z. B. Rabatt über der Grenze): Inhaber oder Standortleitung bestätigen mit ihrer PIN, ohne sich
    /// anzumelden. Gleiche Sperre nach Fehlversuchen wie beim Schnellwechsel. Liefert die freigebende Person oder den Grund.
    /// <paramref name="right"/> ist das Recht, das die freigebende Person haben muss (Standard Kassenaufsicht; die Abnahme von Raumpflege und Tagesstart
    /// verlangt „Standort verwalten“), <paramref name="deniedAction"/> der Satzteil der Absage.
    /// </summary>
    public static async Task<(string? UserId, string? Error)> ApproveAsync(SupervisorApproval approval, Guid? locationId, AispaDbContext db,
        UserManager<AispaUser> users, DateTimeOffset now, CancellationToken ct, AccessRight right = AccessRight.SuperviseCash,
        string deniedAction = "diesen Rabatt nicht freigeben")
    {
        const string Wrong = "Name oder PIN der Freigabe stimmt nicht.";
        var pin = await db.Set<StaffPin>().FirstOrDefaultAsync(p => p.UserId == approval.UserId, ct);
        var user = pin is null ? null : await users.FindByIdAsync(approval.UserId);
        if (pin is null || user is null)
        {
            return (null, Wrong);
        }

        if (pin.LockedUntil is { } until && until > now)
        {
            return (null, $"Zu viele Fehlversuche. Die PIN ist noch {Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes))} Minuten gesperrt.");
        }

        var access = await db.StaffAccess.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == user.Id && a.IsActive
            && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
        var covers = access is not null && Roles.RightsOf(access.Role).Contains(right)
            && (locationId is not { } location || access.AllLocations || access.LocationIds.Contains(location));
        if (users.PasswordHasher.VerifyHashedPassword(user, pin.PinHash, approval.Pin ?? "") == PasswordVerificationResult.Failed)
        {
            pin.FailedAttempts++;
            if (pin.FailedAttempts >= MaxAttempts)
            {
                pin.FailedAttempts = 0;
                pin.LockedUntil = now + Lockout;
            }

            await db.SaveChangesAsync(ct);
            return (null, Wrong);
        }

        pin.FailedAttempts = 0;
        pin.LockedUntil = null;
        await db.SaveChangesAsync(ct);
        return covers ? (user.Id, null) : (null, $"{user.DisplayName} darf {deniedAction}; das dürfen Inhaber und Standortleitung.");
    }

    /// <summary>Wer an diesem Standort per PIN freigeben kann: aktive Inhaber und Standortleitungen mit PIN.</summary>
    public static async Task<List<QuickSwitchPerson>> ApproversAsync(Guid? locationId, AispaDbContext db, DateTimeOffset now, CancellationToken ct,
        AccessRight right = AccessRight.SuperviseCash)
    {
        var people = await (
                from a in db.StaffAccess.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                join p in db.Set<StaffPin>().AsNoTracking() on a.UserId equals p.UserId
                where a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now)
                    && (locationId == null || a.AllLocations || a.LocationIds.Contains(locationId.Value))
                orderby u.DisplayName
                select new QuickSwitchPerson(u.Id, u.DisplayName, a.Role))
            .ToListAsync(ct);
        return [.. people.Where(p => Roles.RightsOf(p.Role).Contains(AccessRight.SuperviseCash))];
    }
}

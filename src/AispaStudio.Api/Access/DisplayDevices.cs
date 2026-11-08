using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>Gekoppelte Anzeige (Dashboard im Pausenraum): ohne Person, nur lesend, an einen Standort gebunden, widerrufbar.</summary>
public sealed class DisplayDevice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public Guid LocationId { get; init; }
    public DateTimeOffset PairedAt { get; init; }
    public string? PairedBy { get; init; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

/// <summary>
/// Kopplungswunsch einer neuen Anzeige, noch ohne Mandant (die Anzeige weiß nicht, zu welchem Spa sie gehört). Gespeichert werden nur Hashes;
/// die Leitung tippt den Code in der Windows-App ein, die Anzeige fragt mit dem Geheimnis nach.
/// </summary>
public sealed class DisplayPairing
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required byte[] CodeHash { get; init; }
    public required byte[] SecretHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
}

/// <summary>Anmeldung und Verwaltung der Anzeigen (Slice 104). Eigenes Cookie, damit eine Anzeige nie als Person des Teams gilt.</summary>
internal static class DisplayDevices
{
    public const string Scheme = "Display";
    public const string CookieName = "aispa.anzeige";
    public const string DeviceClaim = "aispa:device";
    public const string LocationClaim = "aispa:location";
    public static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);

    // Wie beim Einmal-Code der Windows-App ohne verwechselbare Zeichen, damit er sich vom Bildschirm sicher abtippen lässt.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DisplayDevice>(e =>
        {
            e.ToTable("DisplayDevices");
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Name).HasMaxLength(100);
            e.Property(d => d.PairedBy).HasMaxLength(450);
            e.HasIndex(d => d.LocationId);
        });
        builder.Entity<DisplayPairing>(e =>
        {
            e.ToTable("DisplayPairings");
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasIndex(p => p.CodeHash).IsUnique();
            e.HasIndex(p => p.SecretHash).IsUnique();
        });
    }

    public static AuthenticationBuilder AddDisplayCookie(this AuthenticationBuilder auth) =>
        auth.AddCookie(Scheme, o =>
        {
            o.Cookie.Name = CookieName;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.HttpOnly = true;
            o.ExpireTimeSpan = TimeSpan.FromDays(365);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            o.Events.OnValidatePrincipal = ValidateAsync;
        });

    /// <summary>
    /// Vor dem Mandanten und der Rechteprüfung: ist niemand des Teams angemeldet, gilt das Cookie der Anzeige. Der Mandant kommt dann wie beim Team
    /// aus dem Claim; ohne Person findet die Rechteprüfung keinen Zugang, und die Kanal-Sperre lässt nur Endpunkte der Anzeige durch.
    /// </summary>
    public static IApplicationBuilder UseDisplaySignIn(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated != true && context.Request.Cookies.ContainsKey(CookieName)
                && await context.AuthenticateAsync(Scheme) is { Succeeded: true } result)
            {
                context.User = result.Principal;
            }

            await next(context);
        });

    public static IEndpointRouteBuilder MapDisplayEndpoints(this IEndpointRouteBuilder routes)
    {
        var device = routes.MapGroup("/api/display").WithTags("Anzeige").RequireModule(AppModule.Dashboard);

        device.MapPost("/pairing", async (AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var code = RandomNumberGenerator.GetString(Alphabet, 8);
                var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var pairing = new DisplayPairing { CodeHash = Hash(code), SecretHash = Hash(secret), ExpiresAt = time.GetUtcNow() + PairingLifetime };
                db.Add(pairing);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new DisplayPairingStart($"{code[..4]}-{code[4..]}", secret, pairing.ExpiresAt));
            })
            .WithName("StartDisplayPairing")
            .WithSummary("Neue Anzeige: Code für den Bildschirm (10 Minuten gültig) und Geheimnis für die Nachfrage.")
            .AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        device.MapPost("/pairing/redeem", async Task<Results<Ok<DisplaySession>, Accepted, UnauthorizedHttpResult>> (
                DisplayPairingRedeem input, HttpContext http, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var now = time.GetUtcNow();
                var hash = Hash(input.Secret ?? "");
                var pairing = await db.Set<DisplayPairing>().FirstOrDefaultAsync(p => p.SecretHash == hash && p.RedeemedAt == null && p.ExpiresAt > now, ct);
                if (pairing is null)
                {
                    return TypedResults.Unauthorized();
                }

                if (pairing.DeviceId is not { } deviceId)
                {
                    return TypedResults.Accepted((string?)null);
                }

                var display = await db.Set<DisplayDevice>().IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == deviceId && d.RevokedAt == null, ct);
                if (display is null)
                {
                    return TypedResults.Unauthorized();
                }

                var tenantId = db.TenantOf(display);
                tenants.Use(tenantId);
                pairing.RedeemedAt = now;
                display.LastSeenAt = now;
                await db.SaveChangesAsync(ct);
                await http.SignInAsync(Scheme, Principal(display, tenantId), new AuthenticationProperties { IsPersistent = true });
                return TypedResults.Ok(await SessionAsync(db, display, ct));
            })
            .WithName("RedeemDisplayPairing")
            .WithSummary("Die Anzeige fragt nach, ob sie gekoppelt ist: 202 solange nicht, dann das dauerhafte Cookie der Anzeige.")
            .AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        device.MapGet("/session", async Task<Results<Ok<DisplaySession>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, AispaDbContext db, CancellationToken ct) =>
            {
                var deviceId = DeviceOf(principal);
                var display = await db.Set<DisplayDevice>().FirstOrDefaultAsync(d => d.Id == deviceId && d.RevokedAt == null, ct);
                return display is null ? TypedResults.Unauthorized() : TypedResults.Ok(await SessionAsync(db, display, ct));
            })
            .WithName("GetDisplaySession")
            .WithSummary("Name und Standort der angemeldeten Anzeige; nach dem Widerruf 401.")
            .RequireAuthorization(p => p.RequireClaim(DeviceClaim))
            .AllowChannel(AccessChannel.Display);

        device.MapGet("/board", async Task<Results<Ok<DisplayBoard>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, DisplayBoards boards, AispaDbContext db, CancellationToken ct) =>
            {
                var deviceId = DeviceOf(principal);
                var display = await db.Set<DisplayDevice>().FirstOrDefaultAsync(d => d.Id == deviceId && d.RevokedAt == null, ct);
                return display is null || await boards.BuildAsync(display.LocationId, ct) is not { } board ? TypedResults.Unauthorized() : TypedResults.Ok(board);
            })
            .WithName("GetDisplayBoard")
            .WithSummary("Die Tafel im Pausenraum: Tagesleiste je Person, freie Räume, nächste Termine mit Initialen; nur lesend.")
            .RequireAuthorization(p => p.RequireClaim(DeviceClaim))
            .AllowChannel(AccessChannel.Display);

        var manage = routes.MapGroup("/api").WithTags("Anzeige").RequireModule(AppModule.Dashboard);

        manage.MapGet("/locations/{locationId:guid}/displays", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                TypedResults.Ok(await db.Set<DisplayDevice>().AsNoTracking().Where(d => d.LocationId == locationId && d.RevokedAt == null)
                    .OrderBy(d => d.Name).Select(d => new DisplayDeviceDto(d.Id, d.Name, d.LocationId, d.PairedAt, d.LastSeenAt)).ToListAsync(ct)))
            .WithName("ListDisplays")
            .WithSummary("Gekoppelte Anzeigen eines Standorts.")
            .RequirePermission(AccessRight.ManageLocation);

        manage.MapPost("/displays", async Task<Results<Ok<DisplayDeviceDto>, ValidationProblem>> (
                DisplayPairInput input, ClaimsPrincipal principal, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var now = time.GetUtcNow();
                var name = (input.Name ?? "").Trim();
                var codeHash = Hash(Normalize(input.Code));
                var pairing = await db.Set<DisplayPairing>().FirstOrDefaultAsync(p => p.CodeHash == codeHash && p.DeviceId == null && p.ExpiresAt > now, ct);
                var errors = new Errors();
                errors.Check(name.Length is > 0 and <= 100, nameof(input.Name), "Einen Namen mit höchstens 100 Zeichen eingeben, z. B. Pausenraum.");
                errors.Check(pairing is not null, nameof(input.Code), "Diesen Code kennt keine Anzeige, oder er ist abgelaufen. Den Code vom Bildschirm der Anzeige abtippen.");
                errors.Check(await db.Locations.AnyAsync(l => l.Id == input.LocationId, ct), nameof(input.LocationId), "Einen Standort wählen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var display = new DisplayDevice
                {
                    Name = name,
                    LocationId = input.LocationId,
                    PairedAt = now,
                    PairedBy = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                };
                db.Add(display);
                pairing!.DeviceId = display.Id;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new DisplayDeviceDto(display.Id, display.Name, display.LocationId, display.PairedAt, null));
            })
            .WithName("PairDisplay")
            .WithSummary("Koppelt eine Anzeige mit dem Code von ihrem Bildschirm an einen Standort.")
            .RequirePermission(AccessRight.ManageLocation);

        manage.MapDelete("/displays/{displayId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid displayId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var display = await db.Set<DisplayDevice>().FirstOrDefaultAsync(d => d.Id == displayId && d.RevokedAt == null, ct);
                if (display is null)
                {
                    return TypedResults.NotFound();
                }

                display.RevokedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("RevokeDisplay")
            .WithSummary("Widerruft eine Anzeige; sie meldet sich bei der nächsten Anfrage ab.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    public static Guid? DeviceOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(DeviceClaim), out var id) ? id : null;

    /// <summary>Bei jeder Anfrage: widerrufen oder gelöscht heißt abmelden (die Anzeige sieht 401 und zeigt wieder den Kopplungscode).</summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AispaDbContext>();
        var deviceId = context.Principal is { } principal ? DeviceOf(principal) : null;
        var active = deviceId is { } id
            && await db.Set<DisplayDevice>().IgnoreQueryFilters().AnyAsync(d => d.Id == id && d.RevokedAt == null, context.HttpContext.RequestAborted);
        if (!active)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }

    private static ClaimsPrincipal Principal(DisplayDevice display, Guid tenantId) =>
        new(new ClaimsIdentity(
        [
            new Claim(DeviceClaim, display.Id.ToString()),
            new Claim(TenantContext.ClaimType, tenantId.ToString()),
            new Claim(LocationClaim, display.LocationId.ToString()),
            new Claim(Channels.ClaimType, nameof(AccessChannel.Display)),
        ], Scheme));

    private static async Task<DisplaySession> SessionAsync(AispaDbContext db, DisplayDevice display, CancellationToken ct)
    {
        var location = await db.Locations.Where(l => l.Id == display.LocationId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? "";
        return new DisplaySession(display.Id, display.Name, display.LocationId, location);
    }

    private static string Normalize(string? code) =>
        new([.. (code ?? "").ToUpperInvariant().Where(c => Alphabet.Contains(c, StringComparison.Ordinal))]);

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}

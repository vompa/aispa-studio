using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>Einmal-Code für die Anmeldung der Windows-App. Gespeichert wird nur der Hash; einmal verwendbar.</summary>
public sealed class DeviceSignInCode
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required byte[] CodeHash { get; init; }
    public required string UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
}

/// <summary>
/// Anmeldung der Windows-App ohne eigenen Passkey-Dialog: im Browser angemeldet einen Code erzeugen, in der App eintippen.
/// Die App bekommt ein dauerhaftes Anmelde-Cookie derselben Person; Rolle, Standorte und Sperren gelten wie im Browser.
/// </summary>
internal static class DeviceSignIn
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // Ohne leicht verwechselbare Zeichen (0/O, 1/I/L), damit der Code sich sicher abtippen lässt.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static void Configure(ModelBuilder builder) =>
        builder.Entity<DeviceSignInCode>(e =>
        {
            e.ToTable("DeviceSignInCodes");
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.UserId).HasMaxLength(450);
            e.HasIndex(c => c.CodeHash).IsUnique();
            e.HasOne<AispaUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

    public static IEndpointRouteBuilder MapDeviceSignIn(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth/device-code").WithTags("Anmeldung");

        group.MapPost("/", async (ClaimsPrincipal principal, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var code = RandomNumberGenerator.GetString(Alphabet, 8);
                var now = time.GetUtcNow();
                var entry = new DeviceSignInCode
                {
                    CodeHash = Hash(code),
                    UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    CreatedAt = now,
                    ExpiresAt = now + Lifetime,
                };
                db.Add(entry);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new DeviceCode($"{code[..4]}-{code[4..]}", entry.ExpiresAt));
            })
            .WithName("CreateDeviceCode")
            .WithSummary("Erzeugt einen Einmal-Code für die Anmeldung der Windows-App (10 Minuten gültig).")
            .RequirePermission(AccessRight.ViewCalendar)
            .AlsoAllow(AccessRight.ViewFinance, insteadOf: AccessRight.ViewCalendar);

        group.MapPost("/redeem", async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult>> (
                DeviceCodeRedeem input, AispaDbContext db, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                AccessService access, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var hash = Hash(Normalize(input.Code));
                var now = time.GetUtcNow();
                var entry = await db.Set<DeviceSignInCode>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.CodeHash == hash && c.UsedAt == null && c.ExpiresAt > now, ct);
                var user = entry is null ? null : await users.FindByIdAsync(entry.UserId);
                if (entry is null || user is null)
                {
                    return TypedResults.Unauthorized();
                }

                tenants.Use(db.TenantOf(entry));
                entry.UsedAt = now;
                await db.SaveChangesAsync(ct);
                await signIn.SignInAsync(user, isPersistent: true);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("RedeemDeviceCode")
            .WithSummary("Meldet die Windows-App mit einem Einmal-Code an (dauerhaftes Cookie).");

        return routes;
    }

    private static string Normalize(string? code) =>
        new([.. (code ?? "").ToUpperInvariant().Where(c => Alphabet.Contains(c, StringComparison.Ordinal))]);

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}

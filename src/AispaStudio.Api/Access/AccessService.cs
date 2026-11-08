using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>Lädt den Zugang der angemeldeten Person einmal pro Aufruf (HTTP-Anfrage oder Hub-Methode).</summary>
public sealed class AccessService(AispaDbContext db, IHttpContextAccessor http, TimeProvider time)
{
    private UserAccess? _cached;
    private string? _cachedFor;

    /// <summary>Zugang der Person dieser HTTP-Anfrage; null ohne Anmeldung, ohne Zugang, wenn gesperrt oder die Befristung abgelaufen ist.</summary>
    public Task<UserAccess?> CurrentAsync(CancellationToken ct) =>
        http.HttpContext?.User is { } user ? GetAsync(user, ct) : Task.FromResult<UserAccess?>(null);

    public async Task<UserAccess?> GetAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return null;
        }

        if (_cachedFor == userId)
        {
            return _cached;
        }

        var now = time.GetUtcNow();
        var access = await db.StaffAccess.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
        var employeeId = await db.Employees.Where(e => e.UserId == userId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        _cachedFor = userId;
        _cached = access is null
            ? null
            : new UserAccess(userId, access.Role, access.AllLocations, access.LocationIds.ToHashSet(), employeeId, access.AmountReports, access.PlainReports);
        return _cached;
    }

    public async Task<CurrentUser> ToCurrentUserAsync(AispaUser user, UserManager<AispaUser> users, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var row = await db.StaffAccess.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
            .Join(db.Tenants, a => EF.Property<Guid>(a, TenantModel.Property), t => t.Id, (a, t) => new { Access = a, Tenant = t })
            .FirstOrDefaultAsync(ct);
        var employeeId = await db.Employees.Where(e => e.UserId == user.Id).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
        var passkeys = (await users.GetPasskeysAsync(user)).Count;
        var hasPin = await db.Set<StaffPin>().AnyAsync(p => p.UserId == user.Id, ct);
        if (row is null)
        {
            return new(user.Id, user.Email ?? "", user.DisplayName, passkeys, null, employeeId, [], hasPin, user.MustChangePassword, user.UserName);
        }

        var access = row.Access;
        return new(user.Id, user.Email ?? "", user.DisplayName, passkeys,
            new(row.Tenant.Id, row.Tenant.Name, access.Role, access.AllLocations, [.. access.LocationIds], row.Tenant.LockedAt, row.Tenant.LockReason),
            employeeId, Roles.RightsOf(access.Role, access.AmountReports), hasPin, user.MustChangePassword, user.UserName);
    }
}

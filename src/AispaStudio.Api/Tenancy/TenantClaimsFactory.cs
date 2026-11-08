using System.Security.Claims;
using AispaStudio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Tenancy;

/// <summary>Schreibt beim Anmelden den Mandanten des Kontos ins Cookie; jede weitere Anfrage filtert danach.</summary>
internal sealed class TenantClaimsFactory(UserManager<AispaUser> users, IOptions<IdentityOptions> options, AispaDbContext db)
    : UserClaimsPrincipalFactory<AispaUser>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AispaUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var tenantId = await db.StaffAccess.IgnoreQueryFilters()
            .Where(a => a.UserId == user.Id)
            .Select(a => EF.Property<Guid>(a, TenantModel.Property))
            .FirstOrDefaultAsync();
        if (tenantId != Guid.Empty)
        {
            identity.AddClaim(new Claim(TenantContext.ClaimType, tenantId.ToString()));
        }

        return identity;
    }
}

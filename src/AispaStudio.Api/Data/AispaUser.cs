using Microsoft.AspNetCore.Identity;

namespace AispaStudio.Api.Data;

public sealed class AispaUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    /// <summary>Einzelplatz-Betrieb: Startpasswort der Leitung gilt noch; vor dem Weiterarbeiten ändern (ADR 0041).</summary>
    public bool MustChangePassword { get; set; }
}

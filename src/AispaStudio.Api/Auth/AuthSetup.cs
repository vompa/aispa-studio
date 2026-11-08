using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace AispaStudio.Api.Auth;

internal static class AuthSetup
{
    public static IServiceCollection AddPasskeyIdentity(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddDisplayCookie()
            .AddIdentityCookies();
        services.Configure<SecurityStampValidatorOptions>(o => o.OnRefreshingPrincipal = Channels.KeepChannelOnRefresh);

        // API statt Login-Seite: 401/403 zurückgeben, nicht auf eine Seite umleiten.
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "aispa.auth";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorization();

        services.AddIdentityCore<AispaUser>(options =>
            {
                // Schema-Version 3 enthält die Tabelle für Passkeys.
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                // Im Einzelplatz-Betrieb melden sich alle mit Benutzername und Passwort an; eine E-Mail ist dort nicht nötig (ADR 0041).
                options.User.RequireUniqueEmail = !modules.Standalone;
                options.User.AllowedUserNameCharacters += "äöüÄÖÜß";
                // Passwörter gibt es nur im Einzelplatz-Betrieb: mindestens acht Zeichen, sonst frei; nach fünf Fehlversuchen fünf Minuten gesperrt.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AispaDbContext>()
            .AddErrorDescriber<GermanIdentityErrors>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<TenantClaimsFactory>();

        services.Configure<IdentityPasskeyOptions>(configuration.GetSection("Passkeys"));

        return services;
    }
}

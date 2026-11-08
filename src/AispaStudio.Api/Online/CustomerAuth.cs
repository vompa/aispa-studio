using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Online;

/// <summary>
/// Anmeldung im Kundenbereich: ohne Passwort per Einmal-Code an die E-Mail-Adresse, per Passkey oder mit Google bzw. Apple.
/// Eigenes Cookie und eigene Kontentabelle: Ein Kundenkonto öffnet keinen Endpunkt der Rezeption, denn deren Endpunkte prüfen
/// nur das Cookie des Teams (Standardschema).
/// </summary>
internal static class CustomerAuth
{
    public const string Scheme = "Customer";
    public const string ExternalScheme = "Customer.External";
    public const string Policy = "Customer";
    public const string GoogleScheme = "Google";
    public const string AppleScheme = "Apple";

    public static IServiceCollection AddCustomerAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(OnlineBookingOptions.Section).Get<OnlineBookingOptions>() ?? new();

        var auth = services.AddAuthentication()
            .AddCookie(Scheme, o =>
            {
                o.Cookie.Name = "aispa.kunde";
                // Lax statt Strict: nach der Rückkehr von Google/Apple muss das Cookie beim ersten Seitenaufruf mitgehen.
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
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
            })
            .AddCookie(ExternalScheme, o =>
            {
                o.Cookie.Name = "aispa.kunde.extern";
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        if (options.Google.IsConfigured)
        {
            auth.AddGoogle(GoogleScheme, o =>
            {
                o.SignInScheme = ExternalScheme;
                o.ClientId = options.Google.ClientId!;
                o.ClientSecret = options.Google.ClientSecret!;
                o.CallbackPath = "/api/online/auth/external/google-callback";
            });
        }

        if (options.Apple.IsConfigured)
        {
            auth.AddApple(AppleScheme, o =>
            {
                o.SignInScheme = ExternalScheme;
                o.ClientId = options.Apple.ClientId!;
                o.TeamId = options.Apple.TeamId!;
                o.KeyId = options.Apple.KeyId!;
                o.CallbackPath = "/api/online/auth/external/apple-callback";
                o.GenerateClientSecret = true;
                o.PrivateKey = (keyId, _) => Task.FromResult(options.Apple.PrivateKey!.AsMemory());
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());

        // Zweite Identity für Kundenkonten, registriert nach der des Teams. Keine Standard-Token-Anbieter, die gehören dem Team.
        services.AddIdentityCore<CustomerAccount>()
            .AddEntityFrameworkStores<OnlineDbContext>()
            .AddSignInManager<CustomerSignInManager>();

        // AddSignInManager registriert die Prüfer des Sicherheitsstempels ohne Typparameter im Dienstnamen und würde damit die
        // des Teams ersetzen. Das Kunden-Cookie nutzt sie nicht, also wieder entfernen.
        foreach (var descriptor in services.Where(d =>
                     d.ServiceType == typeof(ISecurityStampValidator) || d.ServiceType == typeof(ITwoFactorSecurityStampValidator))
                 .Where(d => d.ImplementationType?.GenericTypeArguments.Contains(typeof(CustomerAccount)) == true)
                 .ToList())
        {
            services.Remove(descriptor);
        }

        return services;
    }

    public static Guid? AccountId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Kundenkonto aus dem Cookie lesen, auch auf Endpunkten, die ohne Anmeldung funktionieren (z. B. Buchen).</summary>
    public static async Task<Guid?> TryGetAccountIdAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(Scheme);
        return result.Succeeded ? AccountId(result.Principal) : null;
    }

    public static IReadOnlyList<string> ExternalProviders(OnlineBookingOptions options) =>
        [.. new[] { (GoogleScheme, options.Google.IsConfigured), (AppleScheme, options.Apple.IsConfigured) }
            .Where(p => p.Item2).Select(p => p.Item1)];
}

/// <summary>SignInManager für Kundenkonten: meldet mit dem Kunden-Cookie an statt mit dem des Teams.</summary>
internal sealed class CustomerSignInManager : SignInManager<CustomerAccount>
{
    public CustomerSignInManager(
        UserManager<CustomerAccount> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<CustomerAccount> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<CustomerAccount>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<CustomerAccount> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        AuthenticationScheme = CustomerAuth.Scheme;
    }

    public override async Task SignOutAsync()
    {
        await Context.SignOutAsync(CustomerAuth.Scheme);
        await Context.SignOutAsync(CustomerAuth.ExternalScheme);
    }
}

internal static class CustomerPolicyExtensions
{
    public static TBuilder RequireCustomer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.RequireAuthorization(CustomerAuth.Policy);
}

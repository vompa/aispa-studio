using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Anmeldung im Kundenbereich ohne Passwort: Einmal-Code per Mail, Passkey, Google oder Apple.</summary>
internal static class CustomerAuthEndpoints
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const int MaxCodesPerWindow = 5;
    private static readonly TimeSpan CodeWindow = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;

    public static IEndpointRouteBuilder MapCustomerAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/online/auth").WithTags("Kundenbereich: Anmeldung")
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        group.MapGet("/providers", (IOptions<OnlineBookingOptions> options) =>
                new CustomerSignInProviders(CustomerAuth.ExternalProviders(options.Value)))
            .WithName("ListCustomerSignInProviders")
            .WithSummary("Eingerichtete Anmeldungen über Google oder Apple.");

        group.MapPost("/code", RequestCodeAsync)
            .WithName("RequestCustomerSignInCode")
            .WithSummary("Schickt einen sechsstelligen Anmeldecode per Mail. Antwortet immer gleich, ob es die Adresse gibt oder nicht.");

        group.MapPost("/code/verify", VerifyCodeAsync)
            .WithName("VerifyCustomerSignInCode")
            .WithSummary("Meldet mit dem Code an; beim ersten Mal entsteht dabei das Kundenkonto.");

        group.MapPost("/passkey/request-options", async (CustomerSignInManager signIn) =>
                TypedResults.Text(await signIn.MakePasskeyRequestOptionsAsync(user: null), "application/json"))
            .WithName("GetCustomerPasskeyRequestOptions")
            .WithSummary("Optionen für navigator.credentials.get() im Kundenbereich.");

        group.MapPost("/passkey/login", async Task<Results<Ok<CustomerSession>, UnauthorizedHttpResult>> (
                PasskeyCredential credential, CustomerSignInManager signIn, CustomerAccounts accounts, CancellationToken ct) =>
            {
                try
                {
                    var assertion = await signIn.PerformPasskeyAssertionAsync(credential.CredentialJson);
                    if (!assertion.Succeeded)
                    {
                        return TypedResults.Unauthorized();
                    }

                    await signIn.UserManager.AddOrUpdatePasskeyAsync(assertion.User, assertion.Passkey);
                    await signIn.SignInAsync(assertion.User, isPersistent: true, authenticationMethod: "passkey");
                    return TypedResults.Ok((await accounts.SessionAsync(assertion.User.Id, ct))!);
                }
                catch (InvalidOperationException)
                {
                    // Keine laufende Anmeldung (Optionen nicht abgerufen oder abgelaufen).
                    return TypedResults.Unauthorized();
                }
            })
            .WithName("LoginCustomerWithPasskey");

        group.MapPost("/passkey/creation-options", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, CustomerSignInManager signIn) =>
            {
                var user = await signIn.UserManager.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var entity = new PasskeyUserEntity { Id = user.Id.ToString(), Name = user.Email!, DisplayName = user.Email! };
                return TypedResults.Text(await signIn.MakePasskeyCreationOptionsAsync(entity), "application/json");
            })
            .RequireCustomer()
            .WithName("GetCustomerPasskeyCreationOptions")
            .WithSummary("Optionen für navigator.credentials.create(): Passkey für das Kundenkonto anlegen.");

        group.MapPost("/passkey", async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> (
                PasskeyCredential credential, ClaimsPrincipal principal, CustomerSignInManager signIn) =>
            {
                var user = await signIn.UserManager.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                var attestation = await signIn.PerformPasskeyAttestationAsync(credential.CredentialJson);
                if (!attestation.Succeeded)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["credentialJson"] = [attestation.Failure.Message],
                    });
                }

                var stored = await signIn.UserManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
                return stored.Succeeded
                    ? TypedResults.NoContent()
                    : TypedResults.ValidationProblem(stored.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            })
            .RequireCustomer()
            .WithName("AddCustomerPasskey");

        group.MapGet("/external/{provider}", async Task<Results<ChallengeHttpResult, NotFound>> (
                string provider, string? returnUrl, IAuthenticationSchemeProvider schemes, IOptions<OnlineBookingOptions> options) =>
            {
                var scheme = CustomerAuth.ExternalProviders(options.Value)
                    .FirstOrDefault(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));
                if (scheme is null || await schemes.GetSchemeAsync(scheme) is null)
                {
                    return TypedResults.NotFound();
                }

                var complete = $"/api/online/auth/external/complete?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}";
                return TypedResults.Challenge(new AuthenticationProperties { RedirectUri = complete }, [scheme]);
            })
            .WithName("StartCustomerExternalSignIn")
            .WithSummary("Leitet zu Google oder Apple weiter; danach zurück in den Kundenbereich.");

        group.MapGet("/external/complete", CompleteExternalAsync)
            .ExcludeFromDescription();

        group.MapPost("/logout", async (CustomerSignInManager signIn) =>
            {
                await signIn.SignOutAsync();
                return TypedResults.NoContent();
            })
            .WithName("LogoutCustomer");

        return routes;
    }

    private static async Task<Results<Accepted, ValidationProblem>> RequestCodeAsync(
        SignInCodeRequest request, OnlineDbContext online, CustomerAccounts accounts, IMessageBus bus, TimeProvider time, CancellationToken ct)
    {
        if (!CustomerAccounts.IsValidEmail(request.Email))
        {
            var errors = new Errors();
            errors.Add(nameof(request.Email), "Keine gültige E-Mail-Adresse.");
            return errors.Problem();
        }

        var email = request.Email.Trim();
        var normalized = accounts.Normalize(email);
        var now = time.GetUtcNow();
        var windowStart = now - CodeWindow;
        if (await online.SignInCodes.CountAsync(c => c.NormalizedEmail == normalized && c.CreatedAt > windowStart, ct) >= MaxCodesPerWindow)
        {
            // Gleiche Antwort wie sonst, damit sich nicht erkennen lässt, ob gedrosselt wurde.
            return TypedResults.Accepted((string?)null);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        online.SignInCodes.Add(new SignInCode
        {
            NormalizedEmail = normalized,
            CodeHash = HashCode(normalized, code),
            CreatedAt = now,
            ExpiresAt = now + CodeLifetime,
        });
        await online.SaveChangesAsync(ct);
        await bus.PublishAsync(new SendMail(OnlineMails.SignInCode(email, code, request.Language)));
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Ok<CustomerSession>, ValidationProblem>> VerifyCodeAsync(
        SignInCodeVerification request, OnlineDbContext online, CustomerAccounts accounts, CustomerSignInManager signIn,
        TimeProvider time, CancellationToken ct)
    {
        var invalid = new Errors();
        invalid.Add(nameof(request.Code), "Der Code stimmt nicht oder ist abgelaufen. Bitte einen neuen Code anfordern.");
        if (!CustomerAccounts.IsValidEmail(request.Email) || request.Code is not { Length: 6 } || !request.Code.All(char.IsAsciiDigit))
        {
            return invalid.Problem();
        }

        var normalized = accounts.Normalize(request.Email);
        var now = time.GetUtcNow();
        var entry = await online.SignInCodes
            .Where(c => c.NormalizedEmail == normalized && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (entry is null || entry.FailedAttempts >= MaxFailedAttempts)
        {
            return invalid.Problem();
        }

        if (!CryptographicOperations.FixedTimeEquals(entry.CodeHash, HashCode(normalized, request.Code)))
        {
            entry.FailedAttempts++;
            await online.SaveChangesAsync(ct);
            return invalid.Problem();
        }

        entry.UsedAt = now;
        await online.SaveChangesAsync(ct);

        var account = await accounts.FindOrCreateAsync(request.Email, ct);
        await signIn.SignInAsync(account, isPersistent: true, authenticationMethod: "code");
        return TypedResults.Ok((await accounts.SessionAsync(account.Id, ct))!);
    }

    private static async Task<RedirectHttpResult> CompleteExternalAsync(
        string? returnUrl, HttpContext http, CustomerSignInManager signIn, CustomerAccounts accounts, CancellationToken ct)
    {
        var target = SafeReturnUrl(returnUrl);
        var result = await http.AuthenticateAsync(CustomerAuth.ExternalScheme);
        var provider = result.Properties?.Items.TryGetValue(".AuthScheme", out var scheme) == true ? scheme : null;
        var key = result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = result.Principal?.FindFirstValue(ClaimTypes.Email);
        await http.SignOutAsync(CustomerAuth.ExternalScheme);
        if (!result.Succeeded || provider is null || key is null)
        {
            return TypedResults.Redirect(Append(target, "anmeldung=fehlgeschlagen"));
        }

        var users = signIn.UserManager;
        var account = await users.FindByLoginAsync(provider, key);
        if (account is null)
        {
            if (!CustomerAccounts.IsValidEmail(email))
            {
                return TypedResults.Redirect(Append(target, "anmeldung=ohne-email"));
            }

            // Google und Apple liefern nur bestätigte Adressen; so landen Code-, Passkey- und Google-Anmeldung im selben Konto.
            account = await accounts.FindOrCreateAsync(email!, ct);
            await users.AddLoginAsync(account, new UserLoginInfo(provider, key, provider));
        }

        await signIn.SignInAsync(account, isPersistent: true, authenticationMethod: provider);
        return TypedResults.Redirect(target);
    }

    /// <summary>Nur Ziele im Kundenbereich, damit die Anmeldung nicht als Weiterleitung auf fremde Seiten missbraucht wird.</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is not null && returnUrl.StartsWith("/buchen/", StringComparison.Ordinal) && !returnUrl.Contains("//", StringComparison.Ordinal)
            && !returnUrl.Contains('\\', StringComparison.Ordinal)
            ? returnUrl
            : "/buchen/";

    private static string Append(string url, string query)
    {
        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var path = hash < 0 ? url : url[..hash];
        var fragment = hash < 0 ? "" : url[hash..];
        return $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{query}{fragment}";
    }

    private static byte[] HashCode(string normalizedEmail, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{normalizedEmail}:{code}"));
}

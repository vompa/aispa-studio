using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.System;

/// <summary>
/// Einbetten des Kundenbereichs auf der eigenen Website (ADR 0080, Nachtrag): Prüfung und Pflege der Liste je Spa und die daraus
/// gebildete <c>frame-ancestors</c>-Richtlinie von <c>/buchen/</c>.
/// </summary>
internal static class BookingEmbedding
{
    public const string Field = "allowedOrigins";
    public const int MaxOrigins = 10;
    public const string FrameAncestorsHeader = "X-Frame-Ancestors";

    private const int MaxOriginLength = 200;
    private const string HttpsPrefix = "https://";

    /// <summary>Wert der Richtlinie: <c>'self'</c> und die eingetragenen Websites.</summary>
    public static string FrameAncestors(IEnumerable<string> origins) => string.Join(' ', ["'self'", .. origins]);

    public static string Policy(IEnumerable<string> origins) => $"frame-ancestors {FrameAncestors(origins)}";

    /// <summary>
    /// Die Websites des Spas, das die Seite zeigt: <c>?spa=</c> der Adresse, sonst der einzige Mandant (wie <see cref="OnlineTenant"/>).
    /// Ohne Treffer leer, dann gilt nur <c>'self'</c>.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AllowedOriginsAsync(AispaDbContext db, string? spa, CancellationToken ct)
    {
        List<string>? origins;
        if (string.IsNullOrEmpty(spa))
        {
            var all = await db.Tenants.AsNoTracking().Select(t => t.EmbedAllowedOrigins).Take(2).ToListAsync(ct);
            origins = all.Count == 1 ? all[0] : null;
        }
        else if (Guid.TryParse(spa, out var id))
        {
            origins = await db.Tenants.AsNoTracking().Where(t => t.Id == id).Select(t => t.EmbedAllowedOrigins).FirstOrDefaultAsync(ct);
        }
        else
        {
            origins = null;
        }

        return origins ?? [];
    }

    /// <summary>Prüft und vereinheitlicht die Eingabe (kleingeschrieben, ohne Schrägstrich am Ende, ohne Doppelte); Fehler stehen am Feld.</summary>
    public static List<string> Normalize(IEnumerable<string>? input, Errors errors)
    {
        var result = new List<string>();
        foreach (var raw in input ?? [])
        {
            var text = raw?.Trim() ?? "";
            if (text.Length == 0)
            {
                continue;
            }

            if (Reason(text) is { } reason)
            {
                errors.Add(Field, $"„{text}“: {reason}");
            }
            else if (Format(text) is { } origin && !result.Contains(origin))
            {
                result.Add(origin);
            }
        }

        errors.Check(result.Count <= MaxOrigins, Field, $"Höchstens {MaxOrigins} Websites. Entfernen Sie nicht benötigte Einträge.");
        return result;
    }

    private static string? Reason(string text)
    {
        const string Invalid = "Das ist keine gültige Adresse. Tragen Sie nur die Adresse der Website ein, z. B. https://www.beispiel-spa.at.";
        if (text.Length > MaxOriginLength || text.Any(c => char.IsWhiteSpace(c) || c is ';' or ',' or '\'' or '"' or '\\' or '<' or '>'))
        {
            return Invalid;
        }

        if (text.Any(c => c > 127))
        {
            return "Sonderzeichen wie Umlaute im Namen sind nicht erlaubt. Tragen Sie die Schreibweise mit xn-- (Punycode) ein.";
        }

        if (text.Contains('*'))
        {
            return "Platzhalter wie * sind nicht erlaubt. Tragen Sie jede Website einzeln ein.";
        }

        if (!text.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Nur Adressen mit https:// sind erlaubt, z. B. https://www.beispiel-spa.at.";
        }

        var rest = text[HttpsPrefix.Length..];
        var end = rest.IndexOfAny(['/', '?', '#']);
        var authority = end < 0 ? rest : rest[..end];
        var tail = end < 0 ? "" : rest[end..];
        if (tail is not ("" or "/"))
        {
            return "Die Adresse darf keinen Pfad, keine Abfrage und keinen Anker enthalten. Tragen Sie nur https://www.beispiel-spa.at ein.";
        }

        if (authority.Contains('@'))
        {
            return "Die Adresse darf keine Zugangsdaten enthalten.";
        }

        var (host, port) = Split(authority);
        var portValid = port is null || (port.Length is > 0 and <= 5 && port.All(char.IsAsciiDigit) && int.Parse(port, CultureInfo.InvariantCulture) is >= 1 and <= 65535);
        return host.Length > 0 && Uri.CheckHostName(host) == UriHostNameType.Dns && portValid ? null : Invalid;
    }

    private static (string Host, string? Port) Split(string authority)
    {
        var colon = authority.LastIndexOf(':');
        return colon < 0 ? (authority, null) : (authority[..colon], authority[(colon + 1)..]);
    }

    private static string Format(string text)
    {
        var (host, port) = Split(text[HttpsPrefix.Length..].TrimEnd('/'));
        return HttpsPrefix + host.ToLowerInvariant() + (port is null ? "" : ":" + int.Parse(port, CultureInfo.InvariantCulture));
    }

    /// <summary>Adresse des Kundenbereichs dieses Spas, wie sie auch in Mails steht: <c>{PublicUrl}/buchen/?spa={Kennung}</c>; die Kennung trennt mehrere Spas auf einem Server.</summary>
    private static string BookingUrl(OnlineBookingOptions options, Guid? tenantId) => $"{options.PublicUrl.TrimEnd('/')}/buchen/?{OnlineTenant.Query}={tenantId}";

    public static IEndpointRouteBuilder MapEmbeddingSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tenant/embedding").WithTags("Unternehmen");

        group.MapGet("", async Task<Results<Ok<EmbeddingSettings>, NotFound>> (
                AispaDbContext db, TenantContext tenants, IOptions<OnlineBookingOptions> online, CancellationToken ct) =>
                await db.Tenants.AsNoTracking().Where(t => t.Id == tenants.TenantId).Select(t => t.EmbedAllowedOrigins).FirstOrDefaultAsync(ct) is { } origins
                    ? TypedResults.Ok(new EmbeddingSettings(origins, BookingUrl(online.Value, tenants.TenantId)))
                    : TypedResults.NotFound())
            .WithName("GetEmbeddingSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Websites, die den Kundenbereich einbetten dürfen.");

        group.MapPut("", async Task<Results<Ok<EmbeddingSettings>, ValidationProblem, NotFound>> (
                EmbeddingSettings input, AispaDbContext db, TenantContext tenants, IOptions<OnlineBookingOptions> online, CancellationToken ct) =>
            {
                var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenants.TenantId, ct);
                if (tenant is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                var origins = Normalize(input.AllowedOrigins, errors);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                tenant.EmbedAllowedOrigins = origins;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new EmbeddingSettings(origins, BookingUrl(online.Value, tenants.TenantId)));
            })
            .WithName("SetEmbeddingSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Websites eintragen, die den Kundenbereich einbetten dürfen (nur https://, ohne Pfad). Gilt als frame-ancestors von /buchen/.");

        return routes;
    }

    /// <summary>
    /// Für nginx (<c>auth_request</c>): liefert den Wert von <c>frame-ancestors</c> im Header, denn nginx kennt die Liste nur über die API.
    /// Das Spa steht in <c>X-Original-URI</c> (Adresse der Seite), weil eine Unteranfrage die Abfrage der Seite nicht erbt.
    /// </summary>
    public static IEndpointRouteBuilder MapEmbeddingPolicyEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/online/embedding-policy", async (HttpContext context, AispaDbContext db, CancellationToken ct) =>
            {
                var original = context.Request.Headers["X-Original-URI"].FirstOrDefault();
                var spa = original?.IndexOf('?') is >= 0 and var at
                    ? QueryHelpers.ParseQuery(original[at..])["spa"].FirstOrDefault()
                    : context.Request.Query["spa"].FirstOrDefault();
                var origins = await AllowedOriginsAsync(db, spa, ct);
                context.Response.Headers[FrameAncestorsHeader] = FrameAncestors(origins);
                return Results.NoContent();
            })
            .WithTags("Kundenbereich")
            .AllowAnonymous()
            .ExcludeFromDescription();
        return routes;
    }
}

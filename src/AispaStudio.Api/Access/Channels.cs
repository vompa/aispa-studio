using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>Am Endpunkt bzw. an der Gruppe: dieser eingeschränkte Kanal darf den Endpunkt aufrufen (Slice 104).</summary>
internal sealed record ChannelAllowed(AccessChannel Channel);

/// <summary>Ergebnis des Kanal-Scans über alle Endpunkte: ein Endpunkt je Kanal, als „GET /api/…“.</summary>
internal sealed record ChannelEndpoint(AccessChannel Channel, string Endpoint);

/// <summary>
/// Rechte je Kanal (Slice 104): Der Kanal steht als Claim am Anmelde-Cookie; ohne Claim ist es der Arbeitsplatz mit den Rechten der Rolle.
/// Anzeige und Mitarbeiter-App erreichen nur Endpunkte mit <see cref="ChannelAllowed"/>; die Mitarbeiter-App dazu nur die eigene Person
/// und deren Termine, auch wenn die Rolle mehr darf. Ein Kanal schränkt nur ein, er erweitert nie die Rechte der Rolle.
/// </summary>
internal static class Channels
{
    public const string ClaimType = "aispa:channel";

    public static AccessChannel Of(ClaimsPrincipal principal) =>
        Enum.TryParse<AccessChannel>(principal.FindFirstValue(ClaimType), out var channel) ? channel : AccessChannel.Workstation;

    public static TBuilder AllowChannel<TBuilder>(this TBuilder builder, AccessChannel channel)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ChannelAllowed(channel));

    /// <summary>Nach <c>UseAuthorization</c>: sperrt eingeschränkte Kanäle auf ihre Endpunkte. Öffentliche Endpunkte bleiben für alle offen.</summary>
    public static IApplicationBuilder UseChannelGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var channel = Of(context.User);
            if (channel is AccessChannel.Display or AccessChannel.StaffApp && context.GetEndpoint() is { } endpoint && NeedsSignIn(endpoint))
            {
                if (!endpoint.Metadata.GetOrderedMetadata<ChannelAllowed>().Any(a => a.Channel == channel))
                {
                    await PermissionExtensions.Forbidden("Über diesen Zugang nicht freigegeben.").ExecuteAsync(context);
                    return;
                }

                if (channel == AccessChannel.StaffApp && !await IsOwnDataAsync(context))
                {
                    await PermissionExtensions.Forbidden("Nur eigene Daten.").ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });

    /// <summary>Wechsel der laufenden Team-Sitzung in die Mitarbeiter-App (nach der Anmeldung am Handy). Zurück geht es nur über eine neue Anmeldung.</summary>
    public static IEndpointRouteBuilder MapAccessChannelEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/auth/channel/staff-app", async Task<Results<Ok<CurrentUser>, UnauthorizedHttpResult>> (
                HttpContext http, ClaimsPrincipal principal, UserManager<AispaUser> users, SignInManager<AispaUser> signIn, AccessService access,
                CancellationToken ct) =>
            {
                var user = await users.GetUserAsync(principal);
                if (user is null)
                {
                    return TypedResults.Unauthorized();
                }

                // Dauer und Ablauf der laufenden Sitzung übernehmen (Schnellwechsel am Empfang läuft nach Leerlauf ab).
                var current = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
                await signIn.SignInWithClaimsAsync(user, current.Properties, [new Claim(ClaimType, nameof(AccessChannel.StaffApp))]);
                return TypedResults.Ok(await access.ToCurrentUserAsync(user, users, ct) with { Channel = AccessChannel.StaffApp });
            })
            .WithTags("Anmeldung")
            .WithName("UseStaffAppChannel")
            .WithSummary("Schränkt die laufende Anmeldung auf die Mitarbeiter-App ein: nur freigegebene Endpunkte und nur eigene Daten.")
            .RequireAuthorization();
        return routes;
    }

    /// <summary>Der Kanal-Claim bleibt, wenn Identity das Cookie nach dem Prüfen des Sicherheitsstempels neu ausstellt; sonst wäre die App wieder Arbeitsplatz.</summary>
    public static Task KeepChannelOnRefresh(SecurityStampRefreshingPrincipalContext context)
    {
        if (context.CurrentPrincipal?.FindFirst(ClaimType) is { } claim && context.NewPrincipal?.Identity is ClaimsIdentity identity
            && !identity.HasClaim(c => c.Type == ClaimType))
        {
            identity.AddClaim(new Claim(ClaimType, claim.Value));
        }

        return Task.CompletedTask;
    }

    private static bool NeedsSignIn(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

    /// <summary>Mitarbeiter-App: Person und Termin aus Route oder Abfrage müssen die eigenen sein.</summary>
    private static async Task<bool> IsOwnDataAsync(HttpContext context)
    {
        var ct = context.RequestAborted;
        var access = await context.RequestServices.GetRequiredService<AccessService>().CurrentAsync(ct);
        if (access is null)
        {
            // Ohne Zugang antwortet die Rechteprüfung des Endpunkts.
            return true;
        }

        Guid? Id(string name) =>
            (context.Request.RouteValues.TryGetValue(name, out var v) ? v?.ToString() : context.Request.Query[name].FirstOrDefault()) is { } text
            && Guid.TryParse(text, out var id) ? id : null;

        if (Id("employeeId") is { } employeeId && employeeId != access.EmployeeId)
        {
            return false;
        }

        if (Id("appointmentId") is { } appointmentId)
        {
            var db = context.RequestServices.GetRequiredService<AispaDbContext>();
            var owner = await db.Appointments.Where(a => a.Id == appointmentId).Select(a => new { a.EmployeeId }).FirstOrDefaultAsync(ct);
            return owner is null || owner.EmployeeId == access.EmployeeId;
        }

        return true;
    }
}

/// <summary>Prüft über alle Endpunkte, dass jeder eine Kanal-Entscheidung hat und die eingeschränkten Kanäle genau die erwarteten erreichen.</summary>
internal static class ChannelScan
{
    public static IReadOnlyList<string> Problems(IEnumerable<RouteEndpoint> endpoints, IReadOnlyCollection<ChannelEndpoint> expected,
        IReadOnlyCollection<string> publicEndpoints)
    {
        var all = endpoints.SelectMany(e => Names(e).Select(name => (Endpoint: e, Name: name))).ToList();
        var problems = new List<string>();

        foreach (var (endpoint, name) in all.Where(x => !HasDecision(x.Endpoint) && !publicEndpoints.Contains(x.Name)))
        {
            problems.Add($"{name}: weder Anmeldung noch als öffentlich eingetragen – Kanal entscheiden.");
        }

        // Öffentlich und zugleich mit Anmeldung: die Kanal-Sperre greift nicht, die Rechteprüfung liest trotzdem das Team-Cookie.
        foreach (var (_, name) in all.Where(x => x.Endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null
                     && x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null))
        {
            problems.Add($"{name}: öffentlich und mit Anmeldung zugleich – die Kanal-Sperre greift dort nicht.");
        }

        var names = all.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(publicEndpoints.Where(p => !names.Contains(p)).Select(p => $"{p}: als öffentlich eingetragen, gibt es aber nicht mehr."));

        var reachable = all.SelectMany(x => Reachable(x.Endpoint).Select(channel => new ChannelEndpoint(channel, x.Name))).ToHashSet();
        problems.AddRange(reachable.Except(expected).OrderBy(c => c.Endpoint, StringComparer.Ordinal)
            .Select(c => $"{c.Endpoint}: für {c.Channel} erreichbar, steht aber nicht in der Liste des Kanals."));
        problems.AddRange(expected.Except(reachable).OrderBy(c => c.Endpoint, StringComparer.Ordinal)
            .Select(c => $"{c.Endpoint}: in der Liste von {c.Channel}, aber dort nicht erreichbar."));
        return problems;
    }

    /// <summary>„GET /api/…“ je HTTP-Methode; Endpunkte ohne Methode (Hub, Fallback) als „* /…“.</summary>
    private static IEnumerable<string> Names(RouteEndpoint endpoint)
    {
        var route = "/" + (endpoint.RoutePattern.RawText ?? "").TrimStart('/');
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
        return methods.Count == 0 ? [$"* {route}"] : methods.Select(m => $"{m} {route}");
    }

    private static bool HasDecision(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static IEnumerable<AccessChannel> Reachable(Endpoint endpoint)
    {
        foreach (var allowed in endpoint.Metadata.GetOrderedMetadata<ChannelAllowed>().Select(a => a.Channel).Distinct())
        {
            yield return allowed;
        }

        if (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == CustomerAuth.Policy))
        {
            yield return AccessChannel.CustomerApp;
        }
    }
}

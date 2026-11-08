using System.Reflection;
using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;

namespace AispaStudio.Api.System;

internal static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system").WithTags("System");

        group.MapGet("/status", (IHostEnvironment env, TimeProvider time, StartupState startup) =>
                new SystemStatus(AppVersion, env.EnvironmentName, time.GetUtcNow(), startup.Snapshot()))
            .WithName("GetSystemStatus")
            .WithSummary("Liefert Version, Umgebung, Serverzeit und den Startzustand (Migration, Beispieldaten).");

        group.MapGet("/team-web", (IConfiguration configuration, IHostEnvironment env) => new TeamWebInfo(TeamWebRoot.DevServer(configuration, env)?.ToString()))
            .AllowAnonymous()
            .WithName("GetTeamWeb")
            .WithSummary("Adresse der Web-Apps, wenn sie nicht unter der Adresse des Servers liegen (Entwicklung: Vite). "
                + "Die Windows-App öffnet dort die Hilfe, auch wenn die Weiterleitung von „/“ nicht ankommt.");

        group.MapGet("/modules", async (ModuleSwitches modules, TenantContext tenants, TenantLocks locks, CancellationToken ct) =>
                modules.ToDto(tenants.TenantId is { } tenantId ? await locks.PlanAsync(tenantId, ct) : null))
            .WithName("GetSystemModules")
            .WithSummary("Betriebsart und eingeschaltete Webmodule; angemeldet nur die, die die Abo-Stufe des Spas enthält (ADR 0041, ADR 0015). "
                + "Die Oberflächen blenden die anderen aus.");

        group.MapPost("/ping", async Task<Results<Accepted<PingAccepted>, UnauthorizedHttpResult>> (PingRequest request, ClaimsPrincipal user, IMessageBus bus) =>
            {
                // Rein defensiv: Jede Anmeldung trägt NameIdentifier (Zugriff, Schnellwechsel); die Anzeige-Geräte kommen an der Kanal-Sperre vorher nicht vorbei.
                if (user.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } userId)
                {
                    return TypedResults.Unauthorized();
                }

                var ping = new PingRequested(Guid.CreateVersion7(), request.Message, userId);
                await bus.PublishAsync(ping);
                return TypedResults.Accepted((string?)null, new PingAccepted(ping.PingId));
            })
            .RequireAuthorization()
            .WithName("RequestPing")
            .WithSummary("Stellt einen Ping in die Queue; das Ergebnis kommt per SignalR (Pong) nur an die Verbindungen des Aufrufers.");

        return routes;
    }

    private static string AppVersion { get; } =
        typeof(SystemEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}

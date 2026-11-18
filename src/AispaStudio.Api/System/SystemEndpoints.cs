using System.Reflection;
using System.Security.Claims;
using AispaStudio.Api.Access;
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

        group.MapDelete("/demo", Results<Accepted, ProblemHttpResult> (ModuleSwitches modules, IConfiguration configuration,
                HttpContext http, IHostApplicationLifetime lifetime, ILoggerFactory loggers) =>
            {
                if (!modules.Demo)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Kein Beispiel-Spa.",
                        detail: "Es läuft kein Beispiel-Spa; es gibt keine Beispieldaten zu löschen.");
                }

                // Erst nach der Antwort: Das Löschen beendet alle Verbindungen zur Datenbank, auch die dieser Anfrage.
                var log = loggers.CreateLogger(nameof(SystemEndpoints));
                var connection = configuration.GetConnectionString("aispadb")!;
                http.Response.OnCompleted(() =>
                {
                    _ = Task.Run(() => DropDemoAsync(connection, lifetime, log));
                    return Task.CompletedTask;
                });
                return TypedResults.Accepted((string?)null);
            })
            .RequirePermission(AccessRight.ManageLocation)
            .WithName("DeleteDemoSpa")
            .WithSummary("Löscht das Beispiel-Spa samt seiner Datenbank „aispademo“ und beendet danach die API (202). "
                + "Ohne Beispiel-Spa 404. Die echte Datenbank „aispadb“ wird nie angefasst: der Name ist fest.");

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

    private static async Task DropDemoAsync(string connection, IHostApplicationLifetime lifetime, ILogger log)
    {
        try
        {
            await DemoSpa.DropAsync(connection, CancellationToken.None);
            log.LogInformation("Beispiel-Spa gelöscht; die API wird beendet.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Das Beispiel-Spa konnte nicht gelöscht werden.");
            return;
        }

        lifetime.StopApplication();
    }

    private static string AppVersion { get; } =
        typeof(SystemEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}

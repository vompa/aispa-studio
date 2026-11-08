using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;

namespace AispaStudio.Api.System;

/// <summary>Zuschaltbare Webmodule (ADR 0041). Der Kern (Kalender, Kassa, Stammdaten usw.) ist immer da.</summary>
public enum AppModule
{
    OnlineBooking,
    BookingIntake,
    Assistant,
    Reminders,
    Rebooking,
    Benchmark,
    Dashboard,
}

/// <summary>Rolle der Instanz im Mehrinstanz-Betrieb (ADR 0087).</summary>
public enum HostingRole
{
    /// <summary>Endpunkte und Hintergrunddienste in einer Instanz, wie bisher.</summary>
    All,

    /// <summary>Nur Endpunkte; die Hintergrunddienste laufen im Worker.</summary>
    Api,

    /// <summary>Nur Hintergrunddienste; außer den Health-Endpunkten keine Endpunkte.</summary>
    Worker,
}

/// <summary>
/// Betriebsart und eingeschaltete Module der Installation. <c>Hosting:Standalone</c> schaltet den Einzelplatz-Betrieb ein;
/// dort sind alle Module aus, im Server-Betrieb alle an. <c>Modules:&lt;Name&gt;</c> überschreibt das je Modul.
/// <c>Hosting:Role</c> teilt Endpunkte und Hintergrunddienste auf Instanzen auf (ADR 0087).
/// </summary>
public sealed class ModuleSwitches
{
    private readonly HashSet<AppModule> _enabled;

    private ModuleSwitches(bool standalone, HostingRole role, HashSet<AppModule> enabled)
    {
        Standalone = standalone;
        Role = role;
        _enabled = enabled;
    }

    public bool Standalone { get; }

    public HostingRole Role { get; }

    /// <summary>Hintergrunddienste, die je Installation nur einmal laufen dürfen, laufen in dieser Instanz.</summary>
    public bool RunsBackgroundServices => Role is HostingRole.All or HostingRole.Worker;

    /// <summary>Diese Instanz beantwortet Anfragen (API, Live, Web-Oberfläche).</summary>
    public bool ServesRequests => Role is HostingRole.All or HostingRole.Api;

    public bool IsOn(AppModule module) => _enabled.Contains(module);

    public static ModuleSwitches From(IConfiguration configuration)
    {
        var standalone = configuration.GetValue<bool>("Hosting:Standalone");
        var role = RoleFrom(configuration["Hosting:Role"]);
        if (standalone && role != HostingRole.All)
        {
            throw new InvalidOperationException(
                $"Der Einzelplatz läuft immer mit Hosting:Role=All; Hosting:Standalone und Hosting:Role={role} schließen sich aus. "
                + "Hosting:Role weglassen oder Hosting:Standalone ausschalten.");
        }

        // Den Schlüssel liest MapDefaultEndpoints in ServiceDefaults erst am Ende des Starts; hier wird er in jeder Umgebung zuerst geprüft,
        // damit ein ungültiger Wert früh und deutsch abbricht. Der Einzelplatz hat keinen Orchestrator, der die Proben brauchte.
        if (HealthEndpointsFrom(configuration) && standalone)
        {
            throw new InvalidOperationException(
                "Der Einzelplatz hat keine Health-Endpunkte im Betrieb; Hosting:Standalone und Hosting:HealthEndpoints schließen sich aus, "
                + "denn die Endpunkte antworten ohne Anmeldung. Hosting:HealthEndpoints weglassen oder Hosting:Standalone ausschalten.");
        }

        var enabled = Enum.GetValues<AppModule>()
            .Where(m => configuration.GetValue<bool?>($"Modules:{m}") ?? !standalone)
            .ToHashSet();
        return new(standalone, role, enabled);
    }

    /// <summary>Leer oder fehlend gilt als aus (wie <c>Hosting:ForwardedHeaders:Enabled</c>; Compose setzt unbelegte Werte als leeren Text), sonst nur <c>true</c> oder <c>false</c>.</summary>
    private static bool HealthEndpointsFrom(IConfiguration configuration)
    {
        var value = configuration["Hosting:HealthEndpoints"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return bool.TryParse(value.Trim(), out var enabled)
            ? enabled
            : throw new InvalidOperationException($"Hosting:HealthEndpoints „{value}“ ist ungültig; erlaubt sind true und false.");
    }

    /// <summary>Nur die Namen, ohne Rücksicht auf Groß- und Kleinschreibung; <c>Enum.TryParse</c> nähme auch Zahlen wie „1“ an.</summary>
    private static HostingRole RoleFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return HostingRole.All;
        }

        var name = value.Trim();
        return Enum.GetNames<HostingRole>().Contains(name, StringComparer.OrdinalIgnoreCase)
            ? Enum.Parse<HostingRole>(name, ignoreCase: true)
            : throw new InvalidOperationException($"Hosting:Role „{value}“ ist ungültig; erlaubt sind All, Api und Worker.");
    }

    /// <summary>Eingeschaltet und, mit <paramref name="plan"/>, auch in der Abo-Stufe des Spas enthalten.</summary>
    public bool IsOn(AppModule module, SubscriptionPlan? plan) => IsOn(module) && (plan is not { } p || Plans.Includes(p, module));

    public SystemModules ToDto(SubscriptionPlan? plan = null) => new(
        Standalone,
        IsOn(AppModule.OnlineBooking, plan),
        IsOn(AppModule.BookingIntake, plan),
        IsOn(AppModule.Assistant, plan),
        IsOn(AppModule.Reminders, plan),
        IsOn(AppModule.Rebooking, plan),
        IsOn(AppModule.Benchmark, plan),
        IsOn(AppModule.Dashboard, plan),
        plan);
}

internal static class ModuleExtensions
{
    /// <summary>
    /// Endpunkte eines zuschaltbaren Moduls: ausgeschaltet antworten sie mit 404. Sie bleiben im API-Vertrag,
    /// damit Web, Windows-App und Tests nicht je Betriebsart verzweigen. Steht das Spa der Anfrage fest (Anmeldung oder
    /// Kundenbereich) und enthält seine Abo-Stufe das Modul nicht, antworten sie mit 403 (ADR 0015).
    /// </summary>
    public static TBuilder RequireModule<TBuilder>(this TBuilder builder, AppModule module)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            if (!services.GetRequiredService<ModuleSwitches>().IsOn(module))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Dieses Modul ist ausgeschaltet.",
                    detail: $"Modul {module} ist in dieser Installation nicht eingeschaltet.");
            }

            if (services.GetRequiredService<TenantContext>().TenantId is { } tenantId
                && await services.GetRequiredService<TenantLocks>().PlanAsync(tenantId, context.HttpContext.RequestAborted) is var plan
                && !Plans.Includes(plan, module))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Nicht in der Abo-Stufe.",
                    detail: $"Dieses Modul gehört zur Stufe „{Plans.Name(Plans.Minimum(module))}“; das Spa hat „{Plans.Name(plan)}“.");
            }

            return await next(context);
        });
        return builder;
    }

    /// <summary>
    /// Endpunkte nur einer Betriebsart: Einrichtung und Passwort-Anmeldung gibt es nur im Einzelplatz-Betrieb,
    /// Passkeys und Einladungen per Link nur im Server-Betrieb (ADR 0041). In der anderen Betriebsart 404.
    /// </summary>
    public static TBuilder RequireHosting<TBuilder>(this TBuilder builder, bool standalone)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
            context.HttpContext.RequestServices.GetRequiredService<ModuleSwitches>().Standalone == standalone
                ? await next(context)
                : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: standalone ? "Nur im Einzelplatz-Betrieb." : "Nicht im Einzelplatz-Betrieb.",
                    detail: standalone
                        ? "Diese Funktion gibt es nur mit Hosting:Standalone."
                        : "Im Einzelplatz-Betrieb gelten Benutzername und Passwort; Zugänge legt die Leitung direkt an."));
        return builder;
    }

    /// <summary>Endpunkte eines Moduls in einer eigenen Gruppe ohne Präfix.</summary>
    public static IEndpointRouteBuilder ForModule(this IEndpointRouteBuilder routes, AppModule module) =>
        routes.MapGroup("").RequireModule(module);
}

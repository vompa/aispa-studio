namespace AispaStudio.Api.System;

/// <summary>Hintergrunddienste und Startzeile nach der Rolle der Instanz (<c>Hosting:Role</c>, ADR 0087).</summary>
internal static partial class HostingRoles
{
    /// <summary>
    /// Startet den als Singleton registrierten <typeparamref name="TService"/> als Hintergrunddienst, aber nur in den Rollen
    /// <c>All</c> und <c>Worker</c>. Der Singleton bleibt in jeder Rolle, weil Endpunkte und Wolverine-Handler ihn direkt holen.
    /// </summary>
    public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services, ModuleSwitches modules)
        where TService : class, IHostedService
    {
        if (modules.RunsBackgroundServices)
        {
            services.AddHostedService(sp => sp.GetRequiredService<TService>());
        }

        return services;
    }

    /// <summary>
    /// Eine Zeile im Log: Rolle und die zuständigen eigenen Hintergrunddienste dieser Instanz, ohne die von Wolverine und ASP.NET Core.
    /// Ein Dienst, den sein eigener Schalter abschaltet (etwa <c>Reminders:PollSeconds=0</c>), steht trotzdem in der Liste.
    /// </summary>
    public static void LogHostingRole(this WebApplication app, ModuleSwitches modules)
    {
        var names = app.Services.GetServices<IHostedService>()
            .Select(s => s.GetType())
            .Where(t => t.Assembly == typeof(HostingRoles).Assembly)
            .Select(t => t.Name)
            .ToList();
        RoleStarted(app.Logger, modules.Role, names.Count == 0 ? "keine" : string.Join(", ", names));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rolle {Role}; Hintergrunddienste: {Services}")]
    private static partial void RoleStarted(ILogger logger, HostingRole role, string services);
}

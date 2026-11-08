using AispaStudio.Api.Access;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.LocalAi;

internal static class LocalAiSetup
{
    /// <summary>Dienste des lokalen Modells. Ohne <c>Ai:Local:Url</c> bleibt nur der Status „aus“ registriert.</summary>
    public static LocalModelOptions AddLocalAi(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(LocalModelOptions.Section);
        services.Configure<LocalModelOptions>(section);
        var options = section.Get<LocalModelOptions>() ?? new LocalModelOptions();
        services.AddSingleton<LocalModelStatus>();
        if (!options.Enabled)
        {
            return options;
        }

        var baseAddress = new Uri(options.Url!.TrimEnd('/') + "/");
        services.AddHttpClient(LocalModelJson.HttpClientName, c =>
        {
            c.BaseAddress = baseAddress;
            c.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));
        }).WithoutStandardResilience();
        services.AddHttpClient(LocalModelJson.HttpClientName + "-pull", c =>
        {
            c.BaseAddress = baseAddress;
            c.Timeout = Timeout.InfiniteTimeSpan;
        }).WithoutStandardResilience();
        services.AddSingleton<LocalModelJson>();
        services.AddSingleton<LocalModelProbe>();
        services.AddScoped<LocalModelDownloader>();
        services.AddHostedService<LocalModelMonitor>();
        return options;
    }

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system/ai").WithTags("System");

        group.MapGet("", (LocalModelStatus status, IServiceProvider services) =>
            {
                var (local, percent, error, checkedAt) = status.Current;
                return new AiStatus(services.GetService<ClaudeJson>() is not null, local, status.Model, percent, error, checkedAt);
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("GetAiStatus")
            .WithSummary("Zeigt, ob Claude eingeschaltet ist und wie es um das optionale lokale KI-Modell steht (ADR 0057).");

        group.MapPost("/local/download", async (IOptions<LocalModelOptions> options, LocalModelStatus status, IMessageBus bus) =>
            {
                if (!options.Value.Enabled)
                {
                    return Results.Problem("Das lokale KI-Modell ist auf diesem Server nicht eingerichtet.", statusCode: StatusCodes.Status409Conflict);
                }

                if (!status.Downloading)
                {
                    await bus.PublishAsync(new LocalModelPullRequested(Guid.CreateVersion7()));
                }

                return Results.Accepted();
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("DownloadLocalAiModel")
            .WithSummary("Lädt das lokale KI-Modell im Hintergrund herunter; den Fortschritt zeigt GET /api/system/ai.");

        return routes;
    }
}

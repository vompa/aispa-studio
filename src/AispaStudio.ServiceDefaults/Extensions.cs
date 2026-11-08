using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default; never repeat a POST (a timed out message may already be delivered, the queue decides on a retry)
            http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    /// <summary>For long calls: the standard handler's 10 s per attempt and 30 s in total would override the client's own timeout.</summary>
    public static IHttpClientBuilder WithoutStandardResilience(this IHttpClientBuilder client)
    {
        // The API is marked experimental; HttpResilienceTests notices if its behaviour changes.
#pragma warning disable EXTEXP0001
        return client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // In der Entwicklung immer, im Betrieb nur mit Hosting:HealthEndpoints (ADR 0087): Container Apps und Kubernetes brauchen die Proben für
        // Start, Bereitschaft und Lebendprüfung, auch für den Worker. Die API (ModuleSwitches) prüft den Wert beim Start und bricht bei einem ungültigen ab; ohne den Schalter bleibt alles wie vorher.
        var development = app.Environment.IsDevelopment();
        if (!development && !(bool.TryParse(app.Configuration["Hosting:HealthEndpoints"], out var enabled) && enabled))
        {
            return app;
        }

        // Im Betrieb meldet der Standard-Writer von ASP.NET Core nur Healthy, Degraded oder Unhealthy: die Beschreibungen der Checks enthalten
        // Interna (z. B. „Beispiel-Spa wird eingerichtet (seit 12 s)“), und die Endpunkte antworten ohne Anmeldung. Nur die Entwicklung zeigt den ausführlichen Bericht.
        var options = new HealthCheckOptions();
        if (development)
        {
            // Der Text nennt je Check die Beschreibung, damit man den Stand ohne Dashboard sieht.
            options.ResponseWriter = WriteReportAsync;
        }

        // ShortCircuit: Health-Checks laufen gleich nach dem Routing, ohne Anmeldung und Mandanten-Middleware. Bricht Aspire
        // eine Abfrage ab (z. B. weil der Debugger gerade angehalten hat), endet sie dort still statt als Ausnahme im eigenen Code.
        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks(HealthEndpointPath, options).ShortCircuit();

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        }).ShortCircuit();

        return app;
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        var lines = report.Entries.Select(e => $"{e.Key}: {e.Value.Status}{(string.IsNullOrEmpty(e.Value.Description) ? "" : $" - {e.Value.Description}")}");
        return context.Response.WriteAsync($"{report.Status}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
    }
}

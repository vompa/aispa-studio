using AispaStudio.Api.Data;
using Microsoft.Extensions.FileProviders;

namespace AispaStudio.Api.System;

/// <summary>
/// Liefert die gebauten Web-Apps (Anmelden, Mein Tag, Hilfe, Dashboard, Kundenbereich, Betreiber) aus der API aus, wenn <c>Hosting:WebRoot</c> gesetzt ist
/// (ADR 0041). Im Einzelplatz-Betrieb startet die Windows-App die API mit diesem Ordner, damit die Hilfe ohne nginx unter derselben Adresse läuft.
/// Im Server-Betrieb macht das weiter nginx oder ein anderer Webserver davor. <c>/</c> leitet nach <c>/anmelden/</c>; jeder Pfad ohne App antwortet 404.
/// </summary>
internal static class TeamWebRoot
{
    // Teil-Oberflächen mit eigener index.html, wie in docker/nginx.conf.
    private const string SignInPath = "/anmelden/";

    private static readonly string[] Apps = ["anmelden", "buchen", "mein-tag", "betreiber", "help", "dashboard"];

    public static WebApplication UseTeamWebRoot(this WebApplication app)
    {
        var root = app.Configuration["Hosting:WebRoot"];
        if (string.IsNullOrWhiteSpace(root))
        {
            return app.Environment.IsDevelopment() ? app.RedirectToDevServer() : app;
        }

        var path = Path.GetFullPath(root);
        if (!File.Exists(Path.Combine(path, "help", "index.html")))
        {
            app.Logger.LogWarning("Hosting:WebRoot {Path} enthält keine help/index.html; die Web-Oberfläche wird nicht ausgeliefert.", path);
            return app;
        }

        var files = new PhysicalFileProvider(path);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Response.Redirect(SignInPath + context.Request.QueryString);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/buchen"))
            {
                context.Response.Headers[BookingContentSecurityPolicy.HeaderName] = BookingContentSecurityPolicy.Value;
                // Wer einbetten darf, steht je Spa in der Datenbank; für Seiten (auch /buchen/index.html), nicht für andere Dateien.
                if (Path.GetExtension(context.Request.Path.Value) is "" or ".html")
                {
                    var db = context.RequestServices.GetRequiredService<AispaDbContext>();
                    var origins = await BookingEmbedding.AllowedOriginsAsync(db, context.Request.Query["spa"].FirstOrDefault(), context.RequestAborted);
                    context.Response.Headers.ContentSecurityPolicy = BookingEmbedding.Policy(origins);
                }
            }

            await next(context);
        });
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        app.MapFallback(async context =>
        {
            var request = context.Request.Path;
            if (request.StartsWithSegments("/api") || request.StartsWithSegments("/hubs") || Path.HasExtension(request.Value))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var sub = Apps.FirstOrDefault(a => request.StartsWithSegments($"/{a}"));
            var index = files.GetFileInfo($"{sub}/index.html");
            if (sub is null || !index.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index);
        });
        return app;
    }

    /// <summary>
    /// Entwicklung: Die Web-Apps liefert Vite aus (Aspire oder <c>npm run dev</c>), unter <c>OnlineBooking:PublicUrl</c>. Die
    /// Startseite der API leitet auf dessen Anmeldeseite weiter. Vite reicht <c>/api</c> an die API zurück.
    /// </summary>
    private static WebApplication RedirectToDevServer(this WebApplication app)
    {
        if (DevServer(app.Configuration, app.Environment) is not { } web)
        {
            return app;
        }

        app.MapGet("/", (HttpRequest request) =>
            {
                var own = new Uri($"{request.Scheme}://{request.Host}/");
                return Uri.Compare(own, web, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0
                    ? Results.NotFound()
                    : Results.Redirect(new Uri(web, SignInPath).ToString() + request.QueryString);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }

    /// <summary>Entwicklung ohne eigene Auslieferung: die Adresse von Vite (<c>OnlineBooking:PublicUrl</c>, im AppHost der Endpunkt „web“).</summary>
    public static Uri? DevServer(IConfiguration configuration, IHostEnvironment env) =>
        env.IsDevelopment() && string.IsNullOrWhiteSpace(configuration["Hosting:WebRoot"])
        && Uri.TryCreate(configuration["OnlineBooking:PublicUrl"], UriKind.Absolute, out var web)
            ? new Uri(web, "/")
            : null;
}

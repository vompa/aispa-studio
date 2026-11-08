using Microsoft.OpenApi;

namespace AispaStudio.Api.System;

/// <summary>
/// Pfadversion der Team-API (ADR 0085): Clients sprechen <c>/api/v1/…</c>, der alte Pfad <c>/api/…</c> bleibt als Alias für v1.
/// Die Endpunkte sind im Code weiter unter <c>/api/…</c> registriert; eine Middleware vor dem Routing schreibt <c>/api/v1/…</c> darauf um.
/// So gibt es jeden Endpunkt nur einmal, und Rechte, Kanal-Sperre, Mandantensperre und Verlauf sehen unter beiden Pfaden denselben Pfad.
/// Kundenbereich, Webhooks und Betreiber haben keine Version: ihre Adressen sind beim Anbieter hinterlegt oder kommen mit dem Server.
/// </summary>
internal static class ApiVersion
{
    public const string Current = "/api/v1";

    private static readonly PathString Api = "/api";
    private static readonly PathString CurrentPrefix = Current;
    private static readonly PathString[] Unversioned = ["/online", "/hooks", "/operator"];

    /// <summary>
    /// Schreibt als Startfilter vor der ganzen Pipeline um, auch vor dem Routing, das <see cref="WebApplication"/> still an den Anfang setzt;
    /// so hängt die Reihenfolge nicht an einem ausdrücklichen <c>UseRouting()</c> in <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddApiVersion(this IServiceCollection services) => services.AddTransient<IStartupFilter, RewriteFirst>();

    private sealed class RewriteFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (Unversion(context.Request.Path) is { } path)
                {
                    context.Request.Path = path;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }

    /// <summary>Das OpenAPI-Dokument nennt die Team-Pfade mit Version, Kundenbereich, Webhooks und Betreiber ohne.</summary>
    public static void VersionPaths(OpenApiDocument document)
    {
        var paths = new OpenApiPaths();
        foreach (var (key, item) in document.Paths)
        {
            paths.Add(IsTeamPath(new PathString(key)) ? CurrentPrefix.Add(new PathString(key[Api.Value!.Length..])).Value! : key, item);
        }

        document.Paths = paths;
    }

    /// <summary><c>/api/v1/x</c> → <c>/api/x</c>; <c>null</c> für alle anderen Pfade und für die Bereiche ohne Version.</summary>
    private static PathString? Unversion(PathString path) =>
        path.StartsWithSegments(CurrentPrefix, out var rest) && rest.HasValue && !Unversioned.Any(u => rest.StartsWithSegments(u))
            ? Api.Add(rest)
            // Ausdrücklich PathString?: ein blankes null würde über die Umwandlung aus string zu einem leeren Pfad.
            : (PathString?)null;

    private static bool IsTeamPath(PathString path) =>
        path.StartsWithSegments(Api, out var rest) && rest.HasValue && !Unversioned.Any(u => rest.StartsWithSegments(u));
}

namespace AispaStudio.Api.System;

/// <summary>
/// Beendet Anfragen still, die der Aufrufer abgebrochen hat (Windows-App nach ihrem Zeitlimit, geschlossener Browser). Ohne diesen Fang
/// erreicht die <see cref="OperationCanceledException"/> aus EF erst die Fehlerbehandlung von ASP.NET Core, und Visual Studio hält mit
/// „user-unhandled“ an, obwohl nichts kaputt ist. Andere Ausnahmen laufen unverändert weiter.
/// </summary>
internal static class ClientAbort
{
    /// <summary>Gleich nach der Fehlerbehandlung einhängen, damit der Fang alle Endpunkte und Middlewares danach umschließt.</summary>
    public static IApplicationBuilder UseClientAbort(this IApplicationBuilder app) => app.Use(HandleAsync);

    internal static async Task HandleAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
        }
    }
}

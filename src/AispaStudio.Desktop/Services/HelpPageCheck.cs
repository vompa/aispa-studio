using System.Globalization;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Prüft die Hilfe-Seite eines Servers, bevor der eingebettete Browser sie lädt (Ä155): Antwortet dort etwas anderes als eine Seite
/// (404 der API, JSON, kein Server), steht ein Satz in der Hilfe statt der rohen Antwort.
/// </summary>
public static class HelpPageCheck
{
    /// <summary>Der Satz zum Problem; <c>null</c>, wenn die Adresse eine HTML-Seite liefert.</summary>
    public static async Task<string?> ProblemAsync(Uri page, HttpMessageHandler? handler = null, CancellationToken cancellationToken = default)
    {
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            using var response = await client.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Missing(page, ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            }

            return response.Content.Headers.ContentType?.MediaType is "text/html" ? null : Missing(page, "keine Seite");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"Der Server {page.Authority} ist nicht erreichbar. Die Verbindung prüfen und die Hilfe noch einmal öffnen.";
        }
    }

    private static string Missing(Uri page, string answer) =>
        $"Der Server {page.Authority} liefert die Hilfe nicht aus (Antwort: {answer}). Im Windows-Paket liegt sie neben der App unter „web\\help“; "
        + "beim Entwickeln AISPA_TEAM_WEB_URL auf den Web-Server (Vite) setzen oder die App über den AppHost starten.";
}

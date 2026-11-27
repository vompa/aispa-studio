using System.Net;
using System.Net.Http.Json;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Design;

/// <summary>
/// Antwortet in der Vorschau des Designers je „GET pfad“ mit festen Beispieldaten; der echte <see cref="ApiClient"/> liest sie
/// wie Antworten des Servers. Schreibende Aufrufe und unbekannte Pfade beantwortet er mit 404.
/// </summary>
internal sealed class DesignApi : HttpMessageHandler
{
    private readonly Dictionary<string, object> _routes = [];

    /// <summary>Antwort auf <c>GET /api/…</c> (ohne Version im Pfad).</summary>
    public DesignApi On(string path, object body)
    {
        _routes[path] = body;
        return this;
    }

    public ApiClient Client() => new(new Uri("http://localhost/"), new MemorySessionStore(), this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        if (path.StartsWith("/api/v1/", StringComparison.Ordinal))
        {
            path = "/api/" + path["/api/v1/".Length..];
        }

        var response = request.Method == HttpMethod.Get && _routes.TryGetValue(path, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType(), options: ApiClient.Json) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}

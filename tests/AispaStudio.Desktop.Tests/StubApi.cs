using System.Net;
using System.Net.Http.Json;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Antwortet je „METHODE pfad“ mit festen Daten und merkt sich die Anfragen.</summary>
internal sealed class StubApi : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = [];
    private readonly Dictionary<string, Task> _gates = [];

    public List<(string Route, string? Body)> Calls { get; } = [];

    /// <summary>Die echten Adressen der Anfragen (mit Version); <see cref="Calls"/> und der Abgleich lesen den Pfad ohne <c>/v1</c>.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Netz weg: jeder Aufruf scheitert wie ohne Verbindung.</summary>
    public bool Down { get; set; }

    public StubApi On(string route, object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[route] = _ => new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: ApiClient.Json) };
        return this;
    }

    /// <summary>Antwortet je nach Körper der Anfrage (zum Beispiel ein Schalter, der den gesendeten Wert zurückgibt).</summary>
    public StubApi OnJson(string route, Func<string?, object> respond, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[route] = request =>
        {
            var answer = respond(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return new HttpResponseMessage(status) { Content = JsonContent.Create(answer, answer.GetType(), options: ApiClient.Json) };
        };
        return this;
    }

    /// <summary>Hält die Antwort auf diese Anfrage zurück, bis <paramref name="gate"/> abgeschlossen ist (eine langsame Antwort des Servers).</summary>
    public StubApi Hold(string route, Task gate)
    {
        _gates[route] = gate;
        return this;
    }

    /// <summary>Eine HTML-Seite, z. B. die Seite „Anmelden“ unter „/anmelden/“.</summary>
    public StubApi OnHtml(string route, string html = "<!doctype html><title>AISPA Studio</title>", Uri? redirectedTo = null)
    {
        _routes[route] = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html"),
            RequestMessage = redirectedTo is null ? null : new HttpRequestMessage(HttpMethod.Get, redirectedTo),
        };
        return this;
    }

    /// <summary>Rohdaten, z. B. ESC/POS für den Bondrucker.</summary>
    public StubApi OnBytes(string route, byte[] body)
    {
        _routes[route] = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        return this;
    }

    public ApiClient Client(OfflineCache? offline = null) => new(new Uri("http://localhost/"), new MemorySessionStore(), this, offline);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.PathAndQuery);
        var path = request.RequestUri.PathAndQuery.StartsWith("/api/v1/", StringComparison.Ordinal)
            ? "/api/" + request.RequestUri.PathAndQuery["/api/v1/".Length..]
            : request.RequestUri.PathAndQuery;
        var route = $"{request.Method} {path}";
        if (Down)
        {
            throw new HttpRequestException("Keine Verbindung");
        }

        Calls.Add((route, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        if (_gates.TryGetValue(route, out var gate))
        {
            await gate.ConfigureAwait(false);
        }

        // „GET localhost:5173/“ gilt nur für diesen Rechner und Port, „GET /“ für jede Adresse.
        var hostRoute = $"{request.Method} {request.RequestUri.Authority}{path}";
        return _routes.TryGetValue(hostRoute, out var exact) ? exact(request)
            : request.RequestUri.Authority != "localhost" ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : _routes.TryGetValue(route, out var respond) ? respond(request) : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

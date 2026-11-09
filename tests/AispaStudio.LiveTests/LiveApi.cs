using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AispaStudio.LiveTests;

/// <summary>Antwort des Servers auf eine Probe: Statuscode und Text (bei 400 und 409 die Meldung des Servers).</summary>
internal sealed record ApiAnswer(int Status, string Body)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>Die Antwort als JSON; null, wenn sie keines ist.</summary>
    public JsonNode? Json
    {
        get
        {
            try
            {
                return JsonNode.Parse(Body);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>Kurzform für den Bericht: „409 {Text}“; Meldung aus <c>message</c>, <c>detail</c> oder <c>errors</c>, sonst der Rohtext.</summary>
    public string Describe()
    {
        var text = Json switch
        {
            JsonObject obj when obj["message"] is { } message => message.ToString(),
            JsonObject obj when obj["detail"] is { } detail => detail.ToString(),
            JsonObject obj when obj["errors"] is { } errors => errors.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            _ => Body,
        };
        return $"{Status} {text}";
    }
}

/// <summary>
/// Kleiner Zugang zur API für Standortregeln und Fehlerproben der Themen: meldet sich mit einem Testkonto der Entwicklungsumgebung an
/// (<c>/api/auth/dev/login</c>, ohne Passkey, nur Konten unter demo.aispa.test) und hält das Cookie. Das ist der Weg, den auch die Tests der API nehmen.
/// </summary>
internal sealed class LiveApi : IDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public LiveApi(Uri baseUrl, string mailbox)
    {
        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true }) { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(30) };
        var login = Send(HttpMethod.Post, "api/auth/dev/login", new { email = $"{mailbox}@demo.aispa.test" });
        if (!login.IsSuccess)
        {
            throw new InvalidOperationException($"Anmeldung von {mailbox} über die API gescheitert: {login.Status} {login.Body}");
        }
    }

    public ApiAnswer Send(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json");
        }

        using var response = _http.Send(request);
        using var reader = new StreamReader(response.Content.ReadAsStream(), Encoding.UTF8);
        return new ApiAnswer((int)response.StatusCode, reader.ReadToEnd());
    }

    public JsonNode Get(string path)
    {
        var answer = Send(HttpMethod.Get, path);
        return answer.IsSuccess ? answer.Json ?? throw new InvalidOperationException($"{path}: keine JSON-Antwort.")
            : throw new InvalidOperationException($"GET {path}: {answer.Status} {answer.Body}");
    }

    /// <summary>Der Standort Wien der Demo-Daten (Zeitzone Europe/Vienna).</summary>
    public (Guid Id, string TimeZone) ViennaLocation()
    {
        var location = Get("api/locations").AsArray().First(l => l!["name"]!.ToString().StartsWith("Wien", StringComparison.Ordinal))!;
        return (Guid.Parse(location["id"]!.ToString()), location["timeZone"]!.ToString());
    }

    /// <summary>Alle Termine des Standorts an einem Tag (Kalendertag in Ortszeit).</summary>
    public JsonArray Appointments(Guid locationId, DateOnly day) =>
        Get($"api/locations/{locationId}/calendar?date={day:yyyy-MM-dd}")["appointments"]!.AsArray();

    public void Dispose() => _http.Dispose();
}

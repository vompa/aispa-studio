using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.LocalAi;

/// <summary>
/// Optionales lokales Sprachmodell (ADR 0057): ein Ollama-Dienst im eigenen Netz, meist als Docker-Container neben der API.
/// Leer gelassen ist es aus; dann ändert sich nichts.
/// </summary>
public sealed class LocalModelOptions
{
    public const string Section = "Ai:Local";

    /// <summary>Adresse des Ollama-Dienstes, z. B. <c>http://ollama:11434</c>. Leer heißt aus.</summary>
    public string? Url { get; set; }

    /// <summary>Kleines, deutschfähiges Modell; Gemma 3 mit 4 Mrd. Parametern braucht etwa 3,3 GB Download und läuft ohne Grafikkarte.</summary>
    public string Model { get; set; } = "gemma3:4b";

    /// <summary>So lange darf eine Antwort dauern. Auf einem Prozessor ohne Grafikkarte braucht eine Mail 10 bis 60 Sekunden.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>So lange bleibt das Modell nach der letzten Anfrage im Arbeitsspeicher (Ollama-Schreibweise, z. B. <c>10m</c>).</summary>
    public string KeepAlive { get; set; } = "10m";

    /// <summary>Abstand, in dem der Server prüft, ob der Dienst läuft und das Modell da ist.</summary>
    public int CheckSeconds { get; set; } = 60;

    public bool Enabled => !string.IsNullOrWhiteSpace(Url);
}

/// <summary>
/// Spricht mit Ollama über dessen HTTP-Schnittstelle. Wie bei Claude füllt das Modell nur ein festes JSON-Schema
/// (<c>format</c>); es kann keine Aktionen auslösen.
/// </summary>
internal sealed class LocalModelJson(IHttpClientFactory http, IOptions<LocalModelOptions> options, LocalModelStatus status) : IAiJson
{
    public const string HttpClientName = "local-ai";

    public async Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        if (status.Current.Local is LocalAiState.Missing or LocalAiState.Downloading)
        {
            throw new InvalidOperationException("Das lokale KI-Modell ist noch nicht geladen.");
        }

        using var client = http.CreateClient(HttpClientName);
        var request = new ChatRequest(
            options.Value.Model,
            [new("system", system), new("user", prompt)],
            Stream: false,
            Format: schema,
            Options: new ChatOptions(Temperature: 0),
            KeepAlive: options.Value.KeepAlive);
        using var response = await client.PostAsJsonAsync("api/chat", request, Wire, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            status.Set(LocalAiState.Missing, null);
            throw new InvalidOperationException("Das lokale KI-Modell ist nicht geladen.");
        }

        response.EnsureSuccessStatusCode();
        var chat = await response.Content.ReadFromJsonAsync<ChatResponse>(Wire, ct);
        var text = chat?.Message?.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Das lokale KI-Modell hat keine Antwort geliefert.");
        }

        return JsonSerializer.Deserialize<T>(text, ClaudeJson.Json) ?? throw new InvalidOperationException("Das lokale KI-Modell hat keine Antwort geliefert.");
    }

    internal static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record ChatRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream,
        Dictionary<string, JsonElement> Format,
        ChatOptions Options,
        string KeepAlive);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatOptions(double Temperature);

    private sealed record ChatResponse(ChatMessage? Message);
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Gemeinsamer Zugang zu Claude für Terminannahme und Assistenten: eine Anfrage, Antwort in einem festen JSON-Schema.
/// Das Modell kann so nur Daten liefern und keine Aktionen auslösen.
/// </summary>
internal sealed class ClaudeJson(IOptions<ClaudeOptions> options) : IAiJson, IDisposable
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AnthropicClient _client = new() { ApiKey = options.Value.ApiKey };

    public void Dispose() => _client.Dispose();

    public async Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        var response = await _client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = options.Value.Model,
            MaxTokens = 4000,
            Betas = [FallbackBeta],
            Fallbacks = new Default(),
            System = system,
            OutputConfig = new BetaOutputConfig
            {
                Effort = options.Value.Effort,
                Format = new BetaJsonOutputFormat { Schema = schema },
            },
            Messages = [new() { Role = Role.User, Content = prompt }],
        }, ct);

        if (response.StopReason == BetaStopReason.Refusal)
        {
            throw new InvalidOperationException("Die KI hat die Anfrage abgelehnt.");
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidOperationException("Die KI hat keine Antwort geliefert.");
    }

    public static Dictionary<string, JsonElement> Schema(object schema) =>
        JsonSerializer.SerializeToElement(schema).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
}

using System.Text.Json;
using System.Text.Json.Serialization;
using AispaStudio.Contracts;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Gemeinsamer Zugang zu Claude für Terminannahme und Assistenten: eine Anfrage, Antwort in einem festen JSON-Schema.
/// Das Modell kann so nur Daten liefern und keine Aktionen auslösen. Der Schlüssel gilt je Spa und Aufruf (<see cref="ClaudeAccess"/>, ADR 0103).
/// Jeder Aufruf läuft über <see cref="CreateAsync"/>, das den Verbrauch an <see cref="AiUsageRecorder"/> meldet (T-112a).
/// </summary>
internal sealed class ClaudeJson(IOptions<ClaudeOptions> options, ClaudeAccess access, ClaudeClients clients, AiUsageRecorder usage) : IAiJson
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Für dieses Spa ist ein Schlüssel da.</summary>
    public bool Available => access.Available;

    /// <summary>Client für den Schlüssel des Spas; ohne Schlüssel eine Ausnahme wie bei einem nicht erreichbaren Dienst.</summary>
    private AnthropicClient Client() => clients.For(access.Key ?? throw new InvalidOperationException("Für Claude ist kein API-Schlüssel gesetzt."));

    /// <summary>Ein Aufruf von Claude mit dem Schlüssel des Spas; der Verbrauch wird gemessen, auch wenn Claude ablehnt.</summary>
    public async Task<BetaMessage> CreateAsync(AiUsageTask task, MessageCreateParams request, CancellationToken ct)
    {
        var response = await Client().Beta.Messages.Create(request, ct);
        await usage.RecordAsync(task, response.Model.Raw(), AiTokens.From(response.Usage), ct);
        return response;
    }

    public async Task<T> AskAsync<T>(AiUsageTask task, string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        var response = await CreateAsync(task, new MessageCreateParams
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
            Messages = [new BetaUserMessageParam { Content = prompt }],
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

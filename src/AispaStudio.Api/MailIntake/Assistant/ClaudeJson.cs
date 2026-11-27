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
/// Modell je Aufgabe und Prompt-Caching am gleichbleibenden Anfang der Anfrage: T-112b.
/// </summary>
internal sealed class ClaudeJson(IOptions<ClaudeOptions> options, ClaudeAccess access, ClaudeClients clients, AiUsageRecorder usage) : IAiJson
{
    /// <summary>
    /// Ab dieser Länge in Zeichen bekommt der gleichbleibende Anfang <c>cache_control</c>. Opus 5.5 und Haiku 5.5 legen erst ab
    /// 512 Token in den Cache; ein Token sind bei deutschem Text eher weniger als vier Zeichen, 2048 Zeichen liegen also darüber.
    /// </summary>
    public const int MinCachePrefixChars = 2048;

    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Für dieses Spa ist ein Schlüssel da.</summary>
    public bool Available => access.Available;

    /// <summary>Modell für die Aufgabe: das kleine zum Auslesen, sonst das große (<see cref="ClaudeModels.IsExtraction"/>).</summary>
    public string ModelFor(AiUsageTask task) => options.Value.ModelFor(task);

    /// <summary>Client für den Schlüssel des Spas; ohne Schlüssel eine Ausnahme wie bei einem nicht erreichbaren Dienst.</summary>
    private AnthropicClient Client() => clients.For(access.Key ?? throw new InvalidOperationException("Für Claude ist kein API-Schlüssel gesetzt."));

    /// <summary>Ein Aufruf von Claude mit dem Schlüssel des Spas; der Verbrauch wird gemessen, auch wenn Claude ablehnt.</summary>
    public async Task<BetaMessage> CreateAsync(AiUsageTask task, MessageCreateParams request, CancellationToken ct)
    {
        var response = await Client().Beta.Messages.Create(request, ct);
        await usage.RecordAsync(task, response.Model.Raw(), AiTokens.From(response.Usage), ct);
        return response;
    }

    public Task<T> AskAsync<T>(AiUsageTask task, string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
        AskCoreAsync<T>(task, system, null, prompt, schema, ct);

    public Task<T> AskAsync<T>(AiUsageTask task, string system, string context, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
        AskCoreAsync<T>(task, system, context, prompt, schema, ct);

    private async Task<T> AskCoreAsync<T>(AiUsageTask task, string system, string? context, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        var response = await CreateAsync(task, AskRequest(options.Value, task, system, context, prompt, schema), ct);
        if (response.StopReason == BetaStopReason.Refusal)
        {
            throw new InvalidOperationException("Die KI hat die Anfrage abgelehnt.");
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidOperationException("Die KI hat keine Antwort geliefert.");
    }

    /// <summary>
    /// Die Anfrage für <see cref="AskAsync{T}(AiUsageTask, string, string, Dictionary{string, JsonElement}, CancellationToken)"/>: zuerst der
    /// System-Prompt, dann der gleichbleibende Teil (<paramref name="context"/>, z. B. der Katalog), zuletzt die Mail oder Frage. Was
    /// gleich bleibt, steht vorne und trägt <c>cache_control</c>, sobald es lang genug für den Cache ist.
    /// </summary>
    internal static MessageCreateParams AskRequest(ClaudeOptions options, AiUsageTask task, string system, string? context, string prompt,
        Dictionary<string, JsonElement> schema)
    {
        var content = new List<BetaContentBlockParam>();
        if (context is not null)
        {
            content.Add(Text(context, system.Length + context.Length >= MinCachePrefixChars));
        }

        content.Add(new BetaTextBlockParam { Text = prompt });
        return Request(options.ModelFor(task), options.Effort, [Text(system, system.Length >= MinCachePrefixChars)],
            [new BetaUserMessageParam { Content = content }], schema);
    }

    /// <summary>
    /// Anfrage mit Antwort im Schema für ein Modell: <c>effort</c> nur, wo das Modell es annimmt, und der Rückfall bei einer Ablehnung
    /// (<c>fallbacks</c>) nur, wo es ihn gibt (<see cref="ClaudeModels"/>).
    /// </summary>
    internal static MessageCreateParams Request(string model, string effort, List<BetaTextBlockParam> system, IReadOnlyList<BetaMessageParam> messages,
        Dictionary<string, JsonElement> schema)
    {
        var format = new BetaJsonOutputFormat { Schema = schema };
        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 4000,
            System = system,
            OutputConfig = ClaudeModels.SupportsEffort(model) ? new BetaOutputConfig { Effort = effort, Format = format } : new BetaOutputConfig { Format = format },
            Messages = messages,
        };
        return ClaudeModels.SupportsServerFallback(model) ? request with { Betas = [FallbackBeta], Fallbacks = new Default() } : request;
    }

    private static BetaTextBlockParam Text(string text, bool cache) =>
        cache ? new BetaTextBlockParam { Text = text, CacheControl = new BetaCacheControlEphemeral() } : new BetaTextBlockParam { Text = text };

    public static Dictionary<string, JsonElement> Schema(object schema) =>
        JsonSerializer.SerializeToElement(schema).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
}

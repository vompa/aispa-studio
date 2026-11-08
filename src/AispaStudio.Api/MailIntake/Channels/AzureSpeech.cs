using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>
/// Abschnitt <c>TextChannels:Voice:Azure</c>: Azure AI Speech in einer EU-Region. Der Schlüssel kommt aus User-Secrets oder dem
/// Geheimnisspeicher, nie aus dem Repository.
/// </summary>
public sealed class AzureSpeechOptions
{
    /// <summary>Regionen in der EU (und der Schweiz), in denen Aufnahmen verarbeitet werden dürfen.</summary>
    public static readonly IReadOnlySet<string> EuRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "westeurope", "northeurope", "germanywestcentral", "francecentral", "swedencentral", "italynorth", "polandcentral",
        "spaincentral", "switzerlandnorth", "switzerlandwest",
    };

    public string Region { get; set; } = "westeurope";
    public string? Key { get; set; }

    /// <summary>Sprachen, unter denen der Dienst die passende erkennt.</summary>
    public string[] Locales { get; set; } = ["de-DE", "en-US"];

    /// <summary>Nur für Tests oder einen eigenen Endpunkt; ohne Angabe der Endpunkt der Region.</summary>
    public Uri? Endpoint { get; set; }

    public Uri BaseUri => Endpoint ?? new Uri($"https://{Region}.api.cognitive.microsoft.com/");
}

/// <summary>
/// Abschrift über die „Fast Transcription“-API von Azure AI Speech: Aufnahme hochladen, Text sofort zurück, keine Speicherung beim
/// Dienst. Eine Region außerhalb der EU wird beim Start abgewiesen, damit Gesprächsinhalte die EU nicht verlassen.
/// </summary>
internal sealed class AzureSpeechToText(IHttpClientFactory http, IOptions<AzureSpeechOptions> options) : ISpeechToText
{
    public const string HttpClientName = "azure-speech";
    private const string ApiVersion = "2024-11-15";

    public async Task<Transcript> TranscribeAsync(Stream audio, string contentType, string? languageHint, CancellationToken ct)
    {
        var o = options.Value;
        var locales = Locales(o.Locales, languageHint);
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(audio);
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var type) ? type : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "audio", "aufnahme");
        form.Add(JsonContent.Create(new { locales }), "definition");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(o.BaseUri, $"speechtotext/transcriptions:transcribe?api-version={ApiVersion}"))
        {
            Content = form,
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", o.Key);
        using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FastTranscription>(ct) ?? new FastTranscription(null, null);

        var text = string.Join(" ", (result.CombinedPhrases ?? []).Select(p => p.Text?.Trim()).Where(t => !string.IsNullOrEmpty(t)));
        var locale = (result.Phrases ?? []).Select(p => p.Locale).FirstOrDefault(l => !string.IsNullOrEmpty(l));
        return new Transcript(
            text.Length > 0 ? text : "Sprachnachricht ohne erkennbaren Text. Bitte die Aufnahme anhören.",
            locale?.Split('-')[0].ToLowerInvariant() ?? languageHint);
    }

    /// <summary>Mit Sprachhinweis zuerst diese Sprache, sonst die eingestellten; höchstens vier, wie der Dienst es zulässt.</summary>
    internal static string[] Locales(IReadOnlyList<string> configured, string? hint)
    {
        var first = hint?.ToLowerInvariant() switch
        {
            "de" => "de-DE",
            "en" => "en-US",
            _ => null,
        };
        return [.. new[] { first }.Concat(configured).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(4)];
    }

    /// <summary>Wirft beim Start, wenn die Einstellungen unvollständig sind oder die Region nicht in der EU liegt.</summary>
    internal static void Validate(AzureSpeechOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.Key))
        {
            throw new InvalidOperationException("TextChannels:Voice:Azure:Key fehlt.");
        }

        if (o.Endpoint is null && !AzureSpeechOptions.EuRegions.Contains(o.Region))
        {
            throw new InvalidOperationException($"TextChannels:Voice:Azure:Region „{o.Region}“ liegt nicht in der EU; erlaubt: {string.Join(", ", AzureSpeechOptions.EuRegions)}.");
        }
    }

    private sealed record FastTranscription(
        [property: JsonPropertyName("combinedPhrases")] List<Phrase>? CombinedPhrases,
        [property: JsonPropertyName("phrases")] List<Phrase>? Phrases);

    private sealed record Phrase([property: JsonPropertyName("text")] string? Text, [property: JsonPropertyName("locale")] string? Locale);
}

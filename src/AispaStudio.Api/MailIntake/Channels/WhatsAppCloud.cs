using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.MailIntake.Mailboxes;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>
/// WhatsApp Business über die Cloud API von Meta (Abschnitt <c>TextChannels:Meta</c>). Zugangstoken und App-Geheimnis
/// kommen aus dem Geheimnisspeicher. Als Webhook wird <c>/api/hooks/whatsapp</c> mit <c>VerifyToken</c> eingetragen.
/// </summary>
/// <remarks>
/// Freie Antworten erlaubt WhatsApp nur innerhalb von 24 Stunden nach der letzten Nachricht des Gasts. Für spätere
/// Nachrichten (z. B. Erinnerungen) braucht es freigegebene Vorlagen; das ist hier noch nicht umgesetzt.
/// </remarks>
public sealed class MetaWhatsAppOptions
{
    public string? AccessToken { get; set; }
    public string? PhoneNumberId { get; set; }

    /// <summary>App-Geheimnis für die Signatur <c>X-Hub-Signature-256</c> eingehender Webhooks.</summary>
    public string? AppSecret { get; set; }

    /// <summary>Frei gewählter Schlüssel, den Meta beim Einrichten des Webhooks zurückschickt.</summary>
    public string? VerifyToken { get; set; }

    public Uri ApiBase { get; set; } = new("https://graph.facebook.com/v21.0/");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppSecret) && !string.IsNullOrWhiteSpace(VerifyToken);
}

internal sealed class MetaWhatsAppProvider(IHttpClientFactory http, IOptions<MetaWhatsAppOptions> options) : ITextChannelProvider
{
    public const string HttpClientName = "whatsapp-cloud";

    public string Name => "Meta";

    public async Task SendAsync(OutgoingText message, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(o.ApiBase, $"{Uri.EscapeDataString(message.SenderId ?? o.PhoneNumberId ?? throw new InvalidOperationException("TextChannels:Meta:PhoneNumberId fehlt."))}/messages"))
        {
            Content = message.Template is { } template
                ? JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    to = message.To.TrimStart('+'),
                    type = "template",
                    template = new
                    {
                        name = template.Name,
                        language = new { code = template.Language },
                        components = new[]
                        {
                            new { type = "body", parameters = template.Parameters.Select(p => new { type = "text", text = p }).ToArray() },
                        },
                    },
                })
                : JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    to = message.To.TrimStart('+'),
                    type = "text",
                    text = new { body = message.Text },
                }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
        using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Eingehende WhatsApp-Webhooks von Meta: Signatur prüfen und Textnachrichten herauslesen.</summary>
public static class WhatsAppCloudWebhook
{
    /// <summary>Prüft <c>X-Hub-Signature-256</c> = „sha256=“ + Hex(HMAC-SHA256(App-Geheimnis, Rohdaten)).</summary>
    public static bool IsValid(string appSecret, ReadOnlySpan<byte> body, string? signature)
    {
        const string Prefix = "sha256=";
        if (signature is null || !signature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[Prefix.Length..]));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Alle Textnachrichten aus einem Webhook (ein Aufruf kann mehrere enthalten); Status-Meldungen werden übergangen.</summary>
    public static IReadOnlyList<IncomingMail> ToIncoming(JsonElement payload)
    {
        var result = new List<IncomingMail>();
        foreach (var value in Items(payload, "entry").SelectMany(e => Items(e, "changes")).Select(c => c.TryGetProperty("value", out var v) ? v : default))
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var names = Items(value, "contacts").ToDictionary(c => Text(c, "wa_id") ?? "", c => c.TryGetProperty("profile", out var p) ? Text(p, "name") : null);
            var to = value.TryGetProperty("metadata", out var metadata) ? Text(metadata, "display_phone_number") : null;
            foreach (var message in Items(value, "messages"))
            {
                if (Text(message, "type") != "text" || !message.TryGetProperty("text", out var text) || Text(text, "body") is not { Length: > 0 } body
                    || Text(message, "id") is not { Length: > 0 } id || Text(message, "from") is not { Length: > 0 } from)
                {
                    continue;
                }

                var sent = long.TryParse(Text(message, "timestamp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                    : DateTimeOffset.UtcNow;
                result.Add(new IncomingMail(
                    $"<wa:{id}>",
                    PhoneNumbers.Normalize(from),
                    names.GetValueOrDefault(from) is { Length: > 0 } name ? name : null,
                    PhoneNumbers.Normalize(to ?? ""),
                    "WhatsApp",
                    MimeMail.Trim(body),
                    sent,
                    SenderVerified: true,
                    BookingChannel.WhatsApp));
            }
        }

        return result;
    }

    // Leere Folge, wenn das Feld fehlt; ein ArrayEnumerator ginge dafür nicht.
#pragma warning disable CA1859
    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];
#pragma warning restore CA1859

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

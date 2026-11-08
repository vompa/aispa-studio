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
/// Twilio für SMS und WhatsApp (Abschnitt <c>TextChannels:Twilio</c>). Account-SID und Auth-Token kommen aus User-Secrets
/// oder dem Geheimnisspeicher. In der Twilio-Konsole wird <c>WebhookUrl</c> als Ziel für eingehende Nachrichten eingetragen.
/// </summary>
public sealed class TwilioOptions
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>Absendernummer für SMS, z. B. +43676800123.</summary>
    public string? SmsFrom { get; set; }

    /// <summary>WhatsApp-Absender (freigeschaltete Business-Nummer), z. B. +43676800123.</summary>
    public string? WhatsAppFrom { get; set; }

    /// <summary>
    /// Öffentliche Adresse des Webhooks, genau wie in Twilio eingetragen, z. B. https://app.example.at/api/hooks/twilio/messages.
    /// Die Signatur wird über diese Adresse gebildet; hinter einem Proxy weicht die interne Adresse ab.
    /// </summary>
    public string? WebhookUrl { get; set; }

    public Uri ApiBase { get; set; } = new("https://api.twilio.com/");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken);
}

internal sealed class TwilioProvider(IHttpClientFactory http, IOptions<TwilioOptions> options) : ITextChannelProvider
{
    public const string HttpClientName = "twilio";

    public string Name => "Twilio";

    public async Task SendAsync(OutgoingText message, CancellationToken ct)
    {
        var o = options.Value;
        var whatsApp = message.Channel == BookingChannel.WhatsApp;
        var from = message.From ?? (whatsApp ? o.WhatsAppFrom : o.SmsFrom) ?? throw new InvalidOperationException("TextChannels:Twilio: Absendernummer fehlt.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(o.ApiBase, $"2010-04-01/Accounts/{Uri.EscapeDataString(o.AccountSid!)}/Messages.json"))
        {
            Content = new FormUrlEncodedContent(Fields(message, whatsApp, from)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AccountSid}:{o.AuthToken}")));
        using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Freier Text oder, bei WhatsApp mit Vorlage, die Content-SID mit den Werten der Platzhalter.</summary>
    internal static Dictionary<string, string> Fields(OutgoingText message, bool whatsApp, string from)
    {
        var fields = new Dictionary<string, string>
        {
            ["To"] = whatsApp ? $"whatsapp:{message.To}" : message.To,
            ["From"] = whatsApp ? $"whatsapp:{from}" : from,
        };
        if (whatsApp && message.Template is { } template)
        {
            fields["ContentSid"] = template.Name;
            fields["ContentVariables"] = JsonSerializer.Serialize(
                template.Parameters.Select((value, i) => (Key: (i + 1).ToString(CultureInfo.InvariantCulture), value)).ToDictionary(p => p.Key, p => p.value));
        }
        else
        {
            fields["Body"] = message.Text;
        }

        return fields;
    }
}

/// <summary>Eingehende Nachrichten von Twilio: Signatur prüfen und in eine <see cref="IncomingMail"/> umwandeln.</summary>
public static class TwilioWebhook
{
    /// <summary>
    /// Prüft <c>X-Twilio-Signature</c>: Base64(HMAC-SHA1(Auth-Token, URL + alle Formularfelder nach Namen sortiert,
    /// jeweils Name und Wert aneinander)).
    /// </summary>
    public static bool IsValid(string authToken, string url, IEnumerable<KeyValuePair<string, string>> form, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var data = new StringBuilder(url);
        foreach (var (key, value) in form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            data.Append(key).Append(value);
        }

        // Twilio schreibt HMAC-SHA1 vor; als HMAC (nicht als reiner Hash) ist das für die Signatur weiterhin sicher.
#pragma warning disable CA5350
        var expected = HMACSHA1.HashData(Encoding.UTF8.GetBytes(authToken), Encoding.UTF8.GetBytes(data.ToString()));
#pragma warning restore CA5350
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Twilio-Formular in eine Nachricht; <c>null</c> ohne Text (z. B. nur ein Bild).</summary>
    public static IncomingMail? ToIncoming(IReadOnlyDictionary<string, string> form, DateTimeOffset receivedAt)
    {
        var from = form.GetValueOrDefault("From") ?? "";
        var body = form.GetValueOrDefault("Body")?.Trim();
        if (string.IsNullOrEmpty(body) || form.GetValueOrDefault("MessageSid") is not { Length: > 0 } sid)
        {
            return null;
        }

        var whatsApp = from.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase);
        var name = form.GetValueOrDefault("ProfileName");
        return new IncomingMail(
            $"<twilio:{sid}>",
            PhoneNumbers.Normalize(from),
            string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            PhoneNumbers.Normalize(form.GetValueOrDefault("To") ?? ""),
            whatsApp ? "WhatsApp" : "SMS",
            MimeMail.Trim(body),
            receivedAt,
            SenderVerified: true,
            whatsApp ? BookingChannel.WhatsApp : BookingChannel.Sms);
    }
}

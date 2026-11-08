using System.Text.Json;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Nachrichten der eingebetteten Web-Oberfläche an die App (<c>invokeCSharpAction</c> im eingebetteten Browser): ein JSON-Objekt mit dem Feld <c>type</c>,
/// z. B. <c>{"type":"saved"}</c> aus <c>savedSignal.ts</c> oder <c>{"type":"companion"}</c> aus <c>appShortcuts.ts</c>.
/// </summary>
internal static class WebMessage
{
    /// <summary>Der Wert von <c>type</c>; <c>null</c> für alles, was keine solche Nachricht ist.</summary>
    public static string? TypeOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var message = JsonDocument.Parse(body);
            return message.RootElement.ValueKind == JsonValueKind.Object
                && message.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                ? type.GetString()
                : null;
        }
        catch (JsonException)
        {
            // Keine Nachricht der Web-Oberfläche: nichts zu tun.
            return null;
        }
    }
}

using System.Text;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>Ergebnis der Spracherkennung eines Anrufs.</summary>
/// <param name="Language">Erkannte Sprache (ISO-Code), falls der Dienst sie liefert.</param>
public sealed record Transcript(string Text, string? Language);

/// <summary>
/// Spracherkennung für Anrufe und Mailbox-Nachrichten. Vorbereitet für einen echten Dienst (z. B. Azure Speech oder
/// einen Whisper-Dienst in der EU); bis dahin <see cref="FakeSpeechToText"/>. Der erkannte Text läuft danach wie eine
/// Mail durch die Terminannahme, geantwortet wird per SMS.
/// </summary>
public interface ISpeechToText
{
    Task<Transcript> TranscribeAsync(Stream audio, string contentType, string? languageHint, CancellationToken ct);
}

/// <summary>
/// Ohne echten Dienst: Text-Aufnahmen (<c>text/plain</c>) werden als Abschrift übernommen, damit Tests und Entwicklung
/// den ganzen Weg durchspielen können. Echte Audiodateien ergeben eine Abschrift, die die Rezeption ansehen muss.
/// </summary>
internal sealed class FakeSpeechToText : ISpeechToText
{
    public async Task<Transcript> TranscribeAsync(Stream audio, string contentType, string? languageHint, CancellationToken ct)
    {
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(audio, Encoding.UTF8);
            return new Transcript((await reader.ReadToEndAsync(ct)).Trim(), languageHint);
        }

        return new Transcript("Sprachnachricht ohne Abschrift: Spracherkennung ist noch nicht eingerichtet. Bitte die Aufnahme anhören.", "de");
    }
}

/// <summary>
/// Abschnitt <c>TextChannels:Voice</c>. Die Telefonanlage oder der Telefonie-Anbieter schickt Aufnahmen an
/// <c>POST /api/hooks/voice/recordings</c> und weist sich mit <c>ApiKey</c> im Kopf <c>X-Aispa-Voice-Key</c> aus.
/// Über Twilio nimmt <c>/api/hooks/twilio/voice</c> den Anruf selbst an (<see cref="TwilioVoice"/>).
/// </summary>
public sealed class VoiceOptions
{
    public string? ApiKey { get; set; }

    /// <summary>Größte angenommene Aufnahme; Mailbox-Nachrichten sind meist deutlich kleiner.</summary>
    public long MaxRecordingBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Längste Aufnahme eines Anrufs über Twilio; ein Terminwunsch braucht selten mehr als eine Minute.</summary>
    public int MaxRecordingSeconds { get; set; } = 120;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

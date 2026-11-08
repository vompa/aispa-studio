using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>
/// Queue: Twilio hat einen Anruf aufgenommen. Die Aufnahme wird geholt, abgeschrieben, wie eine Mail in den Posteingang
/// gestellt und danach bei Twilio gelöscht. Läuft nie im Webhook selbst, der antwortet dem Anrufer sofort.
/// </summary>
/// <param name="TenantId">Spa aus <c>?spa=</c> der Webhook-Adresse; ohne Angabe findet <see cref="MailIngestion"/> es über die angerufene Nummer.</param>
public sealed record TwilioRecordingReceived(
    Guid? TenantId,
    string CallSid,
    string From,
    string To,
    string RecordingUrl,
    string? RecordingSid,
    DateTimeOffset ReceivedAt);

/// <summary>Aufnahmen bei Twilio holen und löschen; in Tests eine Attrappe.</summary>
public interface ITwilioRecordings
{
    Task<(Stream Audio, string ContentType)> DownloadAsync(string recordingUrl, CancellationToken ct);

    Task DeleteAsync(string recordingUrl, CancellationToken ct);
}

internal sealed class TwilioRecordings(IHttpClientFactory http, IOptions<TwilioOptions> options) : ITwilioRecordings
{
    public async Task<(Stream Audio, string ContentType)> DownloadAsync(string recordingUrl, CancellationToken ct)
    {
        // Twilio liefert ohne Endung MP3; WAV ist für die Spracherkennung verlustfrei.
        using var request = Request(HttpMethod.Get, $"{recordingUrl}.wav");
        var response = await http.CreateClient(TwilioProvider.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType;
        return (await response.Content.ReadAsStreamAsync(ct), string.IsNullOrEmpty(type) || type == "audio/x-wav" ? "audio/wav" : type);
    }

    public async Task DeleteAsync(string recordingUrl, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Delete, $"{recordingUrl}.json");
        using var response = await http.CreateClient(TwilioProvider.HttpClientName).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Zugangsdaten gehen nur an die Twilio-API, auch wenn das Formular eine andere Adresse nennt.</summary>
    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var o = options.Value;
        var uri = new Uri(url, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, o.ApiBase.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Die Aufnahme liegt nicht bei Twilio.");
        }

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AccountSid}:{o.AuthToken}")));
        return request;
    }
}

/// <summary>
/// Anrufe über Twilio Programmable Voice: Ansage mit Hinweis auf Aufnahme und KI, dann Aufnahme des Anliegens, Abschrift
/// und Antwort per SMS über die normale Terminannahme. Kein Dialog am Telefon: der Gast spricht einmal, die Rezeption
/// prüft die Antwort, bevor sie hinausgeht.
/// </summary>
internal static class TwilioVoice
{
    public const string RecordingPath = "/api/hooks/twilio/voice/recording";

    /// <summary>
    /// Ansage und Aufnahme. Deutsch zuerst, dann kurz Englisch, weil die Sprache des Anrufers erst mit der Aufnahme
    /// feststeht. Der Hinweis auf Aufnahme und KI steht vor dem Ton, damit auflegen kann, wer das nicht möchte.
    /// </summary>
    public static string Greeting(string spaName, string recordingAction, int maxSeconds) => Twiml(
        Say("de-DE", $"Grüß Gott bei {spaName}. Dieser Anruf wird aufgenommen und von unserer KI-Terminannahme ausgewertet. "
            + "Sagen Sie bitte nach dem Ton Ihren Namen, die gewünschte Behandlung, Ihren Wunschtermin und den Standort. "
            + "Wir antworten Ihnen per SMS."),
        Say("en-GB", "This call is recorded and handled by our AI booking assistant. After the tone, please tell us your name, "
            + "the treatment, your preferred time and the location. We will reply by text message."),
        new XElement("Record",
            new XAttribute("action", recordingAction),
            new XAttribute("method", "POST"),
            new XAttribute("maxLength", maxSeconds),
            new XAttribute("playBeep", "true"),
            new XAttribute("finishOnKey", "#")));

    public static string Goodbye() => Twiml(
        Say("de-DE", "Vielen Dank. Sie bekommen in Kürze eine SMS von uns."),
        Say("en-GB", "Thank you. You will receive a text message from us shortly."),
        new XElement("Hangup"));

    public static string HangUp() => Twiml(new XElement("Hangup"));

    /// <summary>
    /// Adresse, über die Twilio die Signatur bildet: Host aus <c>Twilio:WebhookUrl</c> (hinter einem Proxy weicht die interne
    /// Adresse ab), Pfad und Abfrage wie aufgerufen. Anders als beim SMS-Webhook, weil es mehrere Anruf-Adressen gibt.
    /// </summary>
    public static string PublicUrl(TwilioOptions options, HttpRequest request) => $"{Base(options, request)}{request.Path}{request.QueryString}";

    public static string Base(TwilioOptions options, HttpRequest request) =>
        options.WebhookUrl is { Length: > 0 } configured ? new Uri(configured).GetLeftPart(UriPartial.Authority) : $"{request.Scheme}://{request.Host}";

    private static XElement Say(string language, string text) => new("Say", new XAttribute("language", language), text);

    private static string Twiml(params XElement[] verbs) =>
        new XDocument(new XDeclaration("1.0", "UTF-8", null), new XElement("Response", verbs)).ToString(SaveOptions.DisableFormatting);
}

/// <summary>
/// Holt die Aufnahme, schreibt sie ab und stellt sie als Anruf in den Posteingang. Scheitert die Abschrift, kommt der Anruf
/// trotzdem an, mit Link zur Aufnahme für die Rezeption; gelöscht wird bei Twilio nur nach gelungener Abschrift.
/// </summary>
public static class TwilioVoiceHandler
{
    public static async Task Handle(TwilioRecordingReceived message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (message.TenantId is { } tenantId)
        {
            services.GetRequiredService<TenantContext>().Use(tenantId);
        }

        var recordings = services.GetRequiredService<ITwilioRecordings>();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(TwilioVoiceHandler));
        string body;
        var transcribed = false;
        try
        {
            var (audio, contentType) = await recordings.DownloadAsync(message.RecordingUrl, ct);
            await using (audio)
            {
                var transcript = await services.GetRequiredService<ISpeechToText>().TranscribeAsync(audio, contentType, null, ct);
                body = Mailboxes.MimeMail.Trim(transcript.Text);
            }

            transcribed = body.Length > 0;
            if (!transcribed)
            {
                body = "Anruf ohne verständliche Aufnahme. Bitte den Gast zurückrufen.";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Aufnahme des Anrufs {CallSid} konnte nicht abgeschrieben werden.", message.CallSid);
            body = $"Anruf ohne Abschrift: die Aufnahme konnte nicht abgeschrieben werden. Bitte anhören und zurückrufen.\nAufnahme: {message.RecordingUrl}";
        }

        await services.GetRequiredService<MailIngestion>().IngestAsync(new IncomingMail(
            $"<call:{message.CallSid}>",
            message.From,
            null,
            message.To,
            "Anruf",
            body,
            message.ReceivedAt,
            SenderVerified: true,
            BookingChannel.Phone), ct);

        if (transcribed)
        {
            try
            {
                await recordings.DeleteAsync(message.RecordingUrl, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Aufnahme des Anrufs {CallSid} konnte bei Twilio nicht gelöscht werden.", message.CallSid);
            }
        }
    }
}

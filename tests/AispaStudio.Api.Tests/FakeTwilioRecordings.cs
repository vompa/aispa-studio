using System.Collections.Concurrent;
using System.Text;
using AispaStudio.Api.MailIntake.Channels;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Statt Aufnahmen bei Twilio zu holen: der Test hinterlegt je Adresse den gesprochenen Text, den <c>FakeSpeechToText</c> als
/// Abschrift übernimmt. Merkt sich, welche Aufnahmen gelöscht wurden.
/// </summary>
public sealed class FakeTwilioRecordings : ITwilioRecordings
{
    public static ConcurrentDictionary<string, string> Spoken { get; } = new();

    public static ConcurrentQueue<string> Deleted { get; } = new();

    public Task<(Stream Audio, string ContentType)> DownloadAsync(string recordingUrl, CancellationToken ct) =>
        Spoken.TryGetValue(recordingUrl, out var text)
            ? Task.FromResult<(Stream, string)>((new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/plain"))
            : throw new HttpRequestException($"Keine Aufnahme unter {recordingUrl}.");

    public Task DeleteAsync(string recordingUrl, CancellationToken ct)
    {
        Deleted.Enqueue(recordingUrl);
        return Task.CompletedTask;
    }
}

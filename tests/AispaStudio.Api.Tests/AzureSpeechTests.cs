using System.Net;
using System.Text;
using AispaStudio.Api.MailIntake.Channels;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Tests;

public sealed class AzureSpeechTests
{
    [Fact]
    public async Task Sends_the_recording_to_the_eu_endpoint_and_reads_the_transcript()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new RecordingHandler("""
            {"durationMilliseconds":4200,"combinedPhrases":[{"text":"Ich hätte gern morgen um zehn eine Massage."}],
             "phrases":[{"text":"Ich hätte gern morgen um zehn eine Massage.","locale":"de-DE"}]}
            """);
        var speech = new AzureSpeechToText(new Clients(handler),
            Options.Create(new AzureSpeechOptions { Region = "germanywestcentral", Key = "test-key" }));

        using var audio = new MemoryStream([1, 2, 3]);
        var transcript = await speech.TranscribeAsync(audio, "audio/wav", "de", ct);

        Assert.Equal("Ich hätte gern morgen um zehn eine Massage.", transcript.Text);
        Assert.Equal("de", transcript.Language);
        Assert.Equal("https://germanywestcentral.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version=2024-11-15",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Contains("name=definition", handler.Body, StringComparison.Ordinal);
        Assert.Contains("""{"locales":["de-DE","en-US"]}""", handler.Body, StringComparison.Ordinal);
        Assert.Contains("audio/wav", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_result_asks_the_reception_to_listen()
    {
        var speech = new AzureSpeechToText(new Clients(new RecordingHandler("""{"combinedPhrases":[],"phrases":[]}""")),
            Options.Create(new AzureSpeechOptions { Key = "k" }));
        using var audio = new MemoryStream([1]);
        var transcript = await speech.TranscribeAsync(audio, "audio/ogg", null, TestContext.Current.CancellationToken);
        Assert.Contains("Aufnahme anhören", transcript.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Regions_outside_the_eu_and_missing_keys_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => AzureSpeechToText.Validate(new AzureSpeechOptions { Region = "eastus", Key = "k" }));
        Assert.Throws<InvalidOperationException>(() => AzureSpeechToText.Validate(new AzureSpeechOptions { Region = "westeurope" }));
        AzureSpeechToText.Validate(new AzureSpeechOptions { Region = "switzerlandnorth", Key = "k" });
        Assert.Equal(["en-US", "de-DE"], AzureSpeechToText.Locales(["de-DE", "en-US"], "en"));
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = Encoding.UTF8.GetString(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

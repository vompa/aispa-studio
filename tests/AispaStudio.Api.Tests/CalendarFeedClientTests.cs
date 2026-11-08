using System.Net;
using System.Text;
using AispaStudio.Api.CalendarSubscriptions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der echte Abruf der Kalender-Abos (ADR 0075) gegen einen Handler im Speicher, ohne Netz: Anfrage mit <c>Accept: text/calendar</c>, Größenlimit mit und ohne
/// Längenangabe, Weiterleitungen (nur https, nie nach innen, höchstens drei), Zeitlimit und Fehlertexte für die Meldung.
/// </summary>
public sealed class CalendarFeedClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class StubFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) : IHttpClientFactory
    {
        public List<string> Requests { get; } = [];

        public List<string> Accepts { get; } = [];

        public HttpClient CreateClient(string name) => new(new StubHandler(this, respond));
    }

    private sealed class StubHandler(StubFactory owner, Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            owner.Requests.Add(request.RequestUri!.AbsoluteUri);
            owner.Accepts.Add(request.Headers.Accept.ToString());
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>Inhalt ohne Längenangabe: der Abruf muss selbst mitzählen.</summary>
    private sealed class UnknownLength(byte[] data) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) => await stream.WriteAsync(data);

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/calendar") };

    private static HttpResponseMessage Redirect(string to) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(to, UriKind.RelativeOrAbsolute) } };

    [Fact]
    public async Task Der_Abruf_liefert_den_Text_und_bittet_um_text_calendar()
    {
        var factory = new StubFactory(_ => Text("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n"));

        var download = await new HttpCalendarFeedClient(factory).GetAsync("https://feeds.example.org/a.ics", Ct);

        Assert.Equal(("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n", (string?)null), (download.Content, download.Error));
        Assert.StartsWith("text/calendar", Assert.Single(factory.Accepts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlerantworten_werden_zu_einem_Fehlertext()
    {
        var notFound = await new HttpCalendarFeedClient(new StubFactory(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).GetAsync("https://feeds.example.org/a.ics", Ct);
        Assert.Null(notFound.Content);
        Assert.Contains("404", notFound.Error, StringComparison.Ordinal);

        var unreachable = await new HttpCalendarFeedClient(new StubFactory(_ => throw new HttpRequestException(HttpRequestError.NameResolutionError))).GetAsync("https://feeds.example.org/a.ics", Ct);
        Assert.Contains("nicht erreichbar", unreachable.Error, StringComparison.Ordinal);

        var timeout = await new HttpCalendarFeedClient(new StubFactory(_ => throw new TaskCanceledException())).GetAsync("https://feeds.example.org/a.ics", Ct);
        Assert.Equal("Zeitüberschreitung beim Abruf.", timeout.Error);
    }

    [Fact]
    public async Task Ein_zu_grosser_Kalender_wird_abgelehnt_mit_und_ohne_Laengenangabe()
    {
        var big = new byte[FeedReader.MaxBytes + 1];
        Array.Fill(big, (byte)'x');

        var declared = await new HttpCalendarFeedClient(new StubFactory(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(big) }))
            .GetAsync("https://feeds.example.org/a.ics", Ct);
        var undeclared = await new HttpCalendarFeedClient(new StubFactory(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLength(big) }))
            .GetAsync("https://feeds.example.org/a.ics", Ct);
        var exactly = await new HttpCalendarFeedClient(new StubFactory(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLength(big[..FeedReader.MaxBytes]) }))
            .GetAsync("https://feeds.example.org/a.ics", Ct);

        Assert.All(new[] { declared, undeclared }, d => Assert.Equal(("Der Kalender ist größer als 2 MB.", (string?)null), (d.Error, d.Content)));
        Assert.Null(exactly.Error);
        Assert.Equal(FeedReader.MaxBytes, exactly.Content!.Length);
    }

    [Fact]
    public async Task Weiterleitungen_gelten_nur_nach_https_auf_oeffentliche_Ziele_und_hoechstens_dreimal()
    {
        var ok = new StubFactory(r => r.RequestUri!.AbsolutePath == "/a.ics" ? Redirect("https://cdn.example.org/b.ics") : Text("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n"));
        var followed = await new HttpCalendarFeedClient(ok).GetAsync("https://feeds.example.org/a.ics", Ct);
        Assert.Null(followed.Error);
        Assert.Equal(["https://feeds.example.org/a.ics", "https://cdn.example.org/b.ics"], ok.Requests);

        foreach (var target in new[] { "http://cdn.example.org/b.ics", "https://127.0.0.1/b.ics", "https://user:pw@cdn.example.org/b.ics", "https://intranet/b.ics" })
        {
            var factory = new StubFactory(_ => Redirect(target));
            var download = await new HttpCalendarFeedClient(factory).GetAsync("https://feeds.example.org/a.ics", Ct);
            Assert.Equal("Die Weiterleitung der Adresse ist nicht erlaubt.", download.Error);
            Assert.Single(factory.Requests);
        }

        var endless = new StubFactory(_ => Redirect("https://feeds.example.org/again.ics"));
        Assert.Equal("Zu viele Weiterleitungen.", (await new HttpCalendarFeedClient(endless).GetAsync("https://feeds.example.org/a.ics", Ct)).Error);
        Assert.Equal(4, endless.Requests.Count);
    }
}

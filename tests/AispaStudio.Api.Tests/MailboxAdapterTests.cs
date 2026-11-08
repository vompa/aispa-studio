using System.Net;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Mailboxes;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AispaStudio.Api.Tests;

/// <summary>Echte Postfächer ohne Server: Absenderprüfung, MIME-Umwandlung und Graph mit nachgebildeten Antworten.</summary>
public sealed class MailboxAdapterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("mx.spa.at; dmarc=pass (p=reject) header.from=treatwell.com", true)]
    [InlineData("mx.spa.at; dkim=pass header.d=treatwell.com header.s=s1; spf=fail", true)]
    [InlineData("mx.spa.at; dkim=pass header.d=mail.treatwell.com", true)]
    [InlineData("mx.spa.at; dkim=pass header.d=evil.example; spf=pass smtp.mailfrom=bounce@evil.example", false)]
    [InlineData("mx.spa.at; spf=pass smtp.mailfrom=bounce@treatwell.com", true)]
    [InlineData("mx.spa.at; dmarc=fail header.from=treatwell.com; dkim=fail header.d=treatwell.com", false)]
    [InlineData("mx.spa.at; none", false)]
    public void Absender_gilt_nur_bei_bestandener_Pruefung_fuer_die_From_Domain_als_bestaetigt(string header, bool verified) =>
        Assert.Equal(verified, MailAuthentication.IsVerified([header], "buchung@treatwell.com", []));

    [Fact]
    public void Mitgeschickte_Kopfzeilen_fremder_Server_zaehlen_nicht()
    {
        string[] headers =
        [
            "mx.spa.at; dmarc=fail header.from=treatwell.com",
            "mx.spa.at; dmarc=pass header.from=treatwell.com",
        ];
        Assert.False(MailAuthentication.IsVerified(headers, "buchung@treatwell.com", []));

        string[] forged = ["fake.example; dmarc=pass header.from=treatwell.com", "mx.spa.at; dmarc=fail header.from=treatwell.com"];
        Assert.False(MailAuthentication.IsVerified(forged, "buchung@treatwell.com", ["mx.spa.at"]));
        Assert.True(MailAuthentication.IsVerified(forged.Reverse(), "buchung@treatwell.com", ["fake.example"]));
    }

    [Fact]
    public void Mime_Mail_ohne_Textteil_wird_aus_HTML_gelesen_und_From_statt_Reply_To_verwendet()
    {
        var message = new MimeMessage { Subject = "Termin", MessageId = "abc@spa.test" };
        message.From.Add(new MailboxAddress("Eva Sommer", "eva@example.at"));
        message.ReplyTo.Add(new MailboxAddress("", "buchung@treatwell.com"));
        message.To.Add(new MailboxAddress("", "termine@spa.at"));
        message.Headers.Add(HeaderId.AuthenticationResults, "mx.spa.at; dkim=pass header.d=example.at");
        message.Body = new BodyBuilder
        {
            HtmlBody = "<html><head><style>p{}</style></head><body><p>Hallo,</p><p>gerne am 1.3. um 10&nbsp;Uhr.</p><br>Eva</body></html>",
        }.ToMessageBody();
        var received = new DateTimeOffset(2027, 2, 25, 8, 0, 0, TimeSpan.Zero);

        var mail = MimeMail.ToIncoming(message, received, []);

        Assert.Equal("<abc@spa.test>", mail.MessageId);
        Assert.Equal("eva@example.at", mail.From);
        Assert.Equal("Eva Sommer", mail.FromName);
        Assert.Equal("termine@spa.at", mail.To);
        Assert.Equal("Hallo,\ngerne am 1.3. um 10 Uhr.\n\nEva", mail.Body);
        Assert.Equal(received, mail.ReceivedAt);
        Assert.True(mail.SenderVerified);
    }

    [Fact]
    public async Task Graph_blaettert_ueberspringt_verarbeitete_Mails_und_markiert_mit_Kategorie()
    {
        var handler = new FakeGraph();
        handler.Pages.Enqueue(Page(
            "https://graph.test/v1.0/next",
            Message("1", "<eins@x>", ["AISPA verarbeitet"]),
            Message("2", "<zwei@x>", ["Rot"])));
        handler.Pages.Enqueue(Page(null, Message("3", "<drei@x>", null)));
        var mailbox = Create(handler);

        var mails = await mailbox.FetchNewAsync(Ct);

        Assert.Equal(["<zwei@x>", "<drei@x>"], mails.Select(m => m.MessageId));
        var first = mails[0];
        Assert.Equal("gast@example.at", first.From);
        Assert.Equal("Gast", first.FromName);
        Assert.Equal("termine@spa.at", first.To);
        Assert.Equal("Termin bitte", first.Body);
        Assert.True(first.SenderVerified);
        Assert.Contains("receivedDateTime ge ", handler.Requests[1], StringComparison.Ordinal);
        Assert.Equal("https://graph.test/v1.0/next", handler.Requests[2]);

        await mailbox.MarkProcessedAsync("<zwei@x>", Ct);
        var patch = Assert.Single(handler.Patches);
        Assert.EndsWith("/users/termine%40spa.at/messages/2", patch.Url, StringComparison.Ordinal);
        Assert.Equal("""{"categories":["Rot","AISPA verarbeitet"]}""", patch.Body);
        Assert.Equal(1, handler.TokenRequests);
    }

    [Fact]
    public async Task Graph_sendet_aus_dem_Postfach_als_MIME_im_selben_Verlauf()
    {
        var handler = new FakeGraph();
        var mailbox = Create(handler);
        var message = MailMessages.Build(new OutgoingMail("gast@example.at", "Re: Termin", "Gerne am 1.3.", InReplyTo: "<anfrage@gast>"),
            new MailboxAddress("Spa am See", "termine@spa.at"));

        await mailbox.SendAsync(message, Ct);

        var (url, contentType, body) = Assert.Single(handler.Posts);
        Assert.EndsWith("/users/termine%40spa.at/sendMail", url, StringComparison.Ordinal);
        Assert.Equal("text/plain", contentType);
        var mime = Encoding.UTF8.GetString(Convert.FromBase64String(body));
        Assert.Contains("In-Reply-To: <anfrage@gast>", mime, StringComparison.Ordinal);
        Assert.Contains("From: Spa am See <termine@spa.at>", mime, StringComparison.Ordinal);

        handler.RejectSend = true;
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => mailbox.SendAsync(message, Ct));
        Assert.Contains("Mail.Send", refused.Message, StringComparison.Ordinal);
    }

    private static GraphMailbox Create(FakeGraph handler)
    {
        var options = Options.Create(new GraphOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "nur-im-test",
            Mailbox = "termine@spa.at",
            Authority = new Uri("https://login.test/"),
            GraphBase = new Uri("https://graph.test/v1.0/"),
        });
        return new GraphMailbox(new SingleClientFactory(handler), options, TimeProvider.System);
    }

    private static Dictionary<string, object> Page(string? next, params object[] messages) => next is null
        ? new Dictionary<string, object> { ["value"] = messages }
        : new Dictionary<string, object> { ["value"] = messages, ["@odata.nextLink"] = next };

    private static object Message(string id, string internetId, string[]? categories) => new
    {
        id,
        internetMessageId = internetId,
        subject = "Anfrage",
        from = new { emailAddress = new { name = "Gast", address = "gast@example.at" } },
        toRecipients = new[] { new { emailAddress = new { name = "", address = "termine@spa.at" } } },
        receivedDateTime = "2027-02-25T08:00:00Z",
        body = new { contentType = "text", content = "Termin bitte\r\n" },
        categories,
        internetMessageHeaders = new[] { new { name = "Authentication-Results", value = "spf.protection.outlook.com; dmarc=pass header.from=example.at" } },
    };

    private sealed class FakeGraph : HttpMessageHandler
    {
        public Queue<object> Pages { get; } = new();
        public List<string> Requests { get; } = [];
        public List<(string Url, string Body)> Patches { get; } = [];
        public List<(string Url, string? ContentType, string Body)> Posts { get; } = [];
        public bool RejectSend { get; set; }
        public int TokenRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
            Requests.Add(url);
            if (request.RequestUri.Host == "login.test")
            {
                TokenRequests++;
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                Assert.Contains("grant_type=client_credentials", form, StringComparison.Ordinal);
                return Json(new Dictionary<string, object> { ["access_token"] = "token", ["expires_in"] = 3600 });
            }

            Assert.Equal("Bearer token", request.Headers.Authorization?.ToString());
            if (request.Method == HttpMethod.Patch)
            {
                Patches.Add((request.RequestUri.AbsoluteUri, await request.Content!.ReadAsStringAsync(cancellationToken)));
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (request.Method == HttpMethod.Post)
            {
                if (RejectSend)
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                }

                Posts.Add((request.RequestUri.AbsoluteUri, request.Content!.Headers.ContentType?.MediaType,
                    await request.Content.ReadAsStringAsync(cancellationToken)));
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            return Json(Pages.Dequeue());
        }

        private static HttpResponseMessage Json(object value) =>
            new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

using System.Collections.Concurrent;
using System.Net;
using AispaStudio.Api.LocalAi;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.MailIntake.Mailboxes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class HttpResilienceTests(ApiFixture fixture)
{
    private static readonly Uri Target = new("https://anbieter.test/nachricht");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Spielt den Anbieter: zählt die Versuche und antwortet nach der vorgegebenen Regel.</summary>
    private sealed class FakeProvider(Func<int, CancellationToken, Task<HttpStatusCode>> respond) : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, int> _attempts = new();

        public int Attempts(HttpMethod method) => _attempts.GetValueOrDefault(method.Method);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = _attempts.AddOrUpdate(request.Method.Method, 1, (_, count) => count + 1);
            return new HttpResponseMessage(await respond(attempt, cancellationToken));
        }
    }

    private WebApplicationFactory<Program> Host(FakeProvider provider, params string[] clients) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("TextChannels:Voice:SpeechProvider", "Azure");
            b.UseSetting("TextChannels:Voice:Azure:Region", "westeurope");
            b.UseSetting("TextChannels:Voice:Azure:Key", "k");
            b.UseSetting("Ai:Local:Url", "http://lokal.test");
            b.UseSetting("Ai:Local:Model", "testmodell");
            b.UseSetting("Ai:Local:TimeoutSeconds", "60");
            b.ConfigureTestServices(s =>
            {
                foreach (var client in clients)
                {
                    s.AddHttpClient(client).ConfigurePrimaryHttpMessageHandler(() => provider);
                }
            });
        });

    [Theory]
    [InlineData(TwilioProvider.HttpClientName)]
    [InlineData(MetaWhatsAppProvider.HttpClientName)]
    [InlineData(GraphMailbox.HttpClientName)]
    public async Task POST_wird_nicht_wiederholt_damit_keine_Nachricht_doppelt_rausgeht(string clientName)
    {
        var provider = new FakeProvider((_, _) => Task.FromResult(HttpStatusCode.ServiceUnavailable));
        await using var host = Host(provider, clientName);
        var clients = host.Services.GetRequiredService<IHttpClientFactory>();

        using var response = await clients.CreateClient(clientName).PostAsync(Target, null, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, provider.Attempts(HttpMethod.Post));
    }

    [Fact]
    public async Task GET_wird_nach_einem_kurzen_Ausfall_weiter_wiederholt()
    {
        var provider = new FakeProvider((attempt, _) => Task.FromResult(attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        await using var host = Host(provider, TwilioProvider.HttpClientName);
        var clients = host.Services.GetRequiredService<IHttpClientFactory>();

        using var response = await clients.CreateClient(TwilioProvider.HttpClientName).GetAsync(Target, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, provider.Attempts(HttpMethod.Get));
    }

    [Theory]
    [InlineData(AzureSpeechToText.HttpClientName)]
    [InlineData(LocalModelJson.HttpClientName)]
    [InlineData(LocalModelJson.HttpClientName + "-pull")]
    public async Task Lange_Aufrufe_werden_nicht_nach_zehn_Sekunden_abgebrochen(string clientName)
    {
        var provider = new FakeProvider(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(11), cancellationToken);
            return HttpStatusCode.OK;
        });
        await using var host = Host(provider, clientName);
        var clients = host.Services.GetRequiredService<IHttpClientFactory>();

        using var response = await clients.CreateClient(clientName).PostAsync(Target, null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

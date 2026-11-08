using System.Net;
using System.Text;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Die Hilfe vom Server (R04 Slice 87, Ä155): eine Fehlerantwort oder JSON statt der Seite wird vor dem Anzeigen erkannt und als Satz gemeldet.</summary>
public sealed class HelpPageCheckTests
{
    private static readonly Uri Page = new("http://spa.test:5173/help/?eingebettet=1");

    private sealed class Stub(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer());
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string type, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, type) };

    [Fact]
    public async Task Eine_Seite_mit_Status_200_und_HTML_ist_in_Ordnung()
    {
        var problem = await HelpPageCheck.ProblemAsync(Page, new Stub(() => Reply(HttpStatusCode.OK, "text/html", "<html></html>")), TestContext.Current.CancellationToken);

        Assert.Null(problem);
    }

    [Fact]
    public async Task Eine_404_mit_JSON_wird_als_Satz_gemeldet_nicht_als_rohe_Antwort()
    {
        var problem = await HelpPageCheck.ProblemAsync(Page,
            new Stub(() => Reply(HttpStatusCode.NotFound, "application/problem+json", """{"title":"Not Found","status":404}""")), TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Contains("404", problem);
        Assert.DoesNotContain("{", problem);
        Assert.DoesNotContain("title", problem);
    }

    [Fact]
    public async Task Eine_Antwort_mit_Status_200_aber_JSON_ist_keine_Hilfe_Seite()
    {
        var problem = await HelpPageCheck.ProblemAsync(Page, new Stub(() => Reply(HttpStatusCode.OK, "application/json", "{}")), TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
    }

    [Fact]
    public async Task Ein_nicht_erreichbarer_Server_wird_als_Satz_gemeldet()
    {
        var problem = await HelpPageCheck.ProblemAsync(Page, new Stub(() => throw new HttpRequestException("refused")), TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Contains("nicht erreichbar", problem);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.LocalAi;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Tests;

/// <summary>Optionales lokales KI-Modell über Ollama (ADR 0057).</summary>
public sealed class LocalAiTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2027, 3, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lokales_Modell_bekommt_Schema_und_liefert_die_Absicht()
    {
        var ollama = new FakeOllama(_ => Chat("""{"task":"Figures","from":"2027-02-22","until":"2027-02-28","customerQuery":null}"""));
        var model = ollama.Model();

        var intent = await new ModelAssistantClassifier(model).ClassifyAsync("Wie lief letzte Woche?", Now, "Europe/Vienna", Ct);

        Assert.Equal(new AssistantIntent(AssistantTask.Figures, new DateOnly(2027, 2, 22), new DateOnly(2027, 2, 28), null), intent);
        var sent = ollama.Requests.Single();
        Assert.Equal("/api/chat", sent.Path);
        Assert.Equal("gemma3:4b", sent.Body["model"]!.GetValue<string>());
        Assert.False(sent.Body["stream"]!.GetValue<bool>());
        Assert.Equal("object", sent.Body["format"]!["type"]!.GetValue<string>());
        Assert.Equal(0, sent.Body["options"]!["temperature"]!.GetValue<double>());
        Assert.Equal("system", sent.Body["messages"]![0]!["role"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ohne_Antwort_von_Claude_uebernimmt_das_lokale_Modell()
    {
        var ollama = new FakeOllama(_ => Chat("""{"task":"DayOverview","from":"2027-03-04","until":"2027-03-04","customerQuery":null}"""));
        var model = new FallbackAiJson(new Unreachable(), ollama.Model(), NullLogger<FallbackAiJson>.Instance);

        var intent = await new ModelAssistantClassifier(model).ClassifyAsync("Was steht morgen an?", Now, "Europe/Vienna", Ct);

        Assert.Equal(AssistantTask.DayOverview, intent.Task);
        Assert.Single(ollama.Requests);
    }

    [Fact]
    public async Task Ohne_geladenes_Modell_gelten_die_Regeln_und_Antworten_kommen_aus_Textbausteinen()
    {
        var ollama = new FakeOllama(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"model 'gemma3:4b' not found"}""", Encoding.UTF8, "application/json"),
        });
        var assistant = new LocalBookingAssistant(ollama.Model(), new RuleBasedBookingAssistant(), NullLogger<LocalBookingAssistant>.Instance);
        var massage = new NamedOption(Guid.NewGuid(), "Massage");
        var request = new MailAnalysisRequest("gast@example.at", "Eva Gast", "Termin", "Hallo, ich hätte gerne eine Massage am 12.03.2027 um 10 Uhr.",
            Now, "Europe/Vienna", [massage], [], []);

        var analysis = await assistant.AnalyzeAsync(request, Ct);
        var rules = await new RuleBasedBookingAssistant().AnalyzeAsync(request, Ct);

        Assert.Equal(rules.Intent, analysis.Intent);
        Assert.Equal(massage.Id, analysis.TreatmentId);
        Assert.Equal(rules.Wishes, analysis.Wishes);
        Assert.Equal(LocalAiState.Missing, ollama.Status.Current.Local);

        // Danach fragt das System das Modell nicht mehr, bis es geladen ist.
        await assistant.AnalyzeAsync(request, Ct);
        Assert.Single(ollama.Requests);
    }

    [Fact]
    public async Task Lokales_Modell_liest_Terminwunsch_und_Ids_ausserhalb_der_Liste_bleiben_leer()
    {
        var massage = new NamedOption(Guid.NewGuid(), "Massage");
        var ollama = new FakeOllama(_ => Chat($$"""
            {"intent":"BookingRequest","language":"de","firstName":"Eva","lastName":"Gast","phone":null,
             "treatmentId":"{{massage.Id}}","locationId":"keine-id","employeeId":null,
             "wishes":[{"date":"2027-03-04","from":"17:00","until":null}],"summary":"Massage morgen um 17 Uhr."}
            """));
        var assistant = new LocalBookingAssistant(ollama.Model(), new RuleBasedBookingAssistant(), NullLogger<LocalBookingAssistant>.Instance);

        var analysis = await assistant.AnalyzeAsync(new MailAnalysisRequest("gast@example.at", "Eva Gast", "Termin",
            "Morgen 17 Uhr Massage, bitte nicht bei Marco.", Now, "Europe/Vienna", [massage], [], []), Ct);

        Assert.Equal(MailIntent.BookingRequest, analysis.Intent);
        Assert.Equal(massage.Id, analysis.TreatmentId);
        Assert.Null(analysis.LocationId);
        Assert.Equal([new RequestedTime(new DateOnly(2027, 3, 4), new TimeOnly(17, 0), null)], analysis.Wishes);
    }

    [Fact]
    public async Task Pruefung_und_Download_melden_den_Stand()
    {
        var pulled = false;
        var ollama = new FakeOllama(r => r.Path switch
        {
            "/api/tags" => Json(pulled ? """{"models":[{"name":"gemma3:4b"}]}""" : """{"models":[{"name":"llama3.2:latest"}]}"""),
            "/api/pull" => Pull(() => pulled = true),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var probe = new LocalModelProbe(ollama.Factory, ollama.Options, ollama.Status);

        await probe.CheckAsync(Ct);
        Assert.Equal(LocalAiState.Missing, ollama.Status.Current.Local);

        await new LocalModelDownloader(ollama.Factory, ollama.Options, ollama.Status, probe).PullAsync(Ct);
        Assert.Equal(LocalAiState.Ready, ollama.Status.Current.Local);
        Assert.Null(ollama.Status.Current.Error);
    }

    [Fact]
    public async Task Fehler_beim_Download_steht_im_Status()
    {
        var ollama = new FakeOllama(r => r.Path == "/api/pull"
            ? Json("""{"status":"pulling manifest"}""" + "\n" + """{"error":"pull model manifest: no such host"}""")
            : Json("""{"models":[]}"""));
        var probe = new LocalModelProbe(ollama.Factory, ollama.Options, ollama.Status);

        await new LocalModelDownloader(ollama.Factory, ollama.Options, ollama.Status, probe).PullAsync(Ct);

        Assert.Equal(LocalAiState.Missing, ollama.Status.Current.Local);
        Assert.Contains("no such host", ollama.Status.Current.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gemma3:4b", "gemma3:4b", true)]
    [InlineData("gemma3:latest", "gemma3", true)]
    [InlineData("gemma3:4b", "gemma3", false)]
    [InlineData("qwen3:4b", "gemma3:4b", false)]
    public void Modellname_wie_bei_Ollama(string installed, string wanted, bool same) =>
        Assert.Equal(same, LocalModelProbe.IsModel(installed, wanted));

    [Fact]
    public async Task Status_ohne_lokales_Modell_ist_aus_und_Download_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var status = await spa.GetAsync<AiStatus>("/api/system/ai", Ct);
        var download = await spa.Client.PostAsync("/api/system/ai/local/download", null, Ct);

        Assert.False(status.Claude);
        Assert.Equal(LocalAiState.Off, status.Local);
        Assert.Null(status.LocalModel);
        Assert.Equal(HttpStatusCode.Conflict, download.StatusCode);
    }

    [Fact]
    public async Task Status_mit_eingerichtetem_Modell_ohne_Dienst_ist_nicht_erreichbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Ai:Local:Url", "http://127.0.0.1:9");
            b.UseSetting("Ai:Local:Model", "qwen3:4b");
            b.UseSetting("Ai:Local:CheckSeconds", "0");
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);

        var status = await client.GetFromJsonAsync<AiStatus>("/api/system/ai", TestSpa.Json, Ct);

        Assert.Equal(LocalAiState.Unreachable, status!.Local);
        Assert.Equal("qwen3:4b", status.LocalModel);
    }

    [Fact]
    public async Task Status_braucht_das_Recht_fuer_die_Verwaltung()
    {
        using var anonymous = fixture.CreateClient();

        var response = await anonymous.GetAsync("/api/system/ai", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpResponseMessage Chat(string content) =>
        Json(JsonSerializer.Serialize(new { model = "gemma3:4b", message = new { role = "assistant", content }, done = true }));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Pull(Action done)
    {
        done();
        return Json("""
            {"status":"pulling manifest"}
            {"status":"pulling aeda25e63ebd","digest":"sha256:aeda","total":1000,"completed":500}
            {"status":"pulling aeda25e63ebd","digest":"sha256:aeda","total":1000,"completed":1000}
            {"status":"success"}
            """);
    }

    private sealed class Unreachable : IAiJson
    {
        public Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
            throw new HttpRequestException("Keine Verbindung.");
    }

    private sealed record SentRequest(string Path, JsonNode Body);

    /// <summary>Ollama im Speicher: beantwortet Anfragen mit der angegebenen Funktion und merkt sich, was ankam.</summary>
    private sealed class FakeOllama : DelegatingHandler, IHttpClientFactory
    {
        private readonly Func<SentRequest, HttpResponseMessage> _answer;

        public FakeOllama(Func<SentRequest, HttpResponseMessage> answer)
        {
            _answer = answer;
            Options = Microsoft.Extensions.Options.Options.Create(new LocalModelOptions { Url = "http://ollama:11434" });
            Status = new LocalModelStatus(Options, TimeProvider.System);
        }

        public List<SentRequest> Requests { get; } = [];

        public IOptions<LocalModelOptions> Options { get; }

        public LocalModelStatus Status { get; }

        public IHttpClientFactory Factory => this;

        public LocalModelJson Model() => new(this, Options, Status);

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://ollama:11434/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            var sent = new SentRequest(request.RequestUri!.AbsolutePath, JsonNode.Parse(string.IsNullOrEmpty(body) ? "{}" : body)!);
            if (sent.Path == "/api/chat")
            {
                Requests.Add(sent);
            }

            return _answer(sent);
        }
    }
}

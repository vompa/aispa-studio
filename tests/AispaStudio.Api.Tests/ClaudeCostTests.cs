using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Kostenhebel für Claude ohne echten Aufruf (T-112b): kurze Nummern statt Ids im Katalog, kleines Modell für das Auslesen,
/// Prompt-Caching am gleichbleibenden Anfang der Anfrage.
/// </summary>
public sealed partial class ClaudeCostTests
{
    private static readonly NamedOption HotStone = new(Guid.NewGuid(), "Lava-Hot-Stone-Massage");
    private static readonly NamedOption Classic = new(Guid.NewGuid(), "Klassische Massage");
    private static readonly NamedOption Vienna = new(Guid.NewGuid(), "Wien Innere Stadt");
    private static readonly NamedOption Lena = new(Guid.NewGuid(), "Lena Huber");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Der_Katalog_im_Prompt_traegt_Nummern_ab_1_und_keine_Ids()
    {
        var model = new RecordingModel("""{"intent":"Question","language":"de","wishes":[],"summary":"Frage"}""");

        await ClaudeBookingAssistant.AnalyzeWithAsync(model, Request(), Ct);

        Assert.DoesNotMatch(GuidPattern(), model.Prompt);
        Assert.Contains("1: Lava-Hot-Stone-Massage", model.Prompt, StringComparison.Ordinal);
        Assert.Contains("2: Klassische Massage", model.Prompt, StringComparison.Ordinal);
        Assert.Contains("1: Wien Innere Stadt", model.Prompt, StringComparison.Ordinal);
        Assert.Contains("1: Lena Huber", model.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nummer_2_ergibt_die_zweite_Behandlung_und_eine_unbekannte_Nummer_wird_verworfen()
    {
        var model = new RecordingModel("""{"intent":"BookingRequest","language":"de","treatment":2,"location":7,"employee":1,"wishes":[],"summary":"Termin"}""");

        var analysis = await ClaudeBookingAssistant.AnalyzeWithAsync(model, Request(), Ct);

        Assert.Equal(Classic.Id, analysis.TreatmentId);
        Assert.Null(analysis.LocationId);
        Assert.Equal(Lena.Id, analysis.EmployeeId);
    }

    [Fact]
    public void Auslesen_nutzt_das_kleine_Modell_ohne_Fallback_und_Texte_das_grosse()
    {
        var options = new ClaudeOptions();

        var extract = ClaudeJson.AskRequest(options, AiUsageTask.BookingRequest, "System", null, "Mail", Schema);
        var prose = ClaudeJson.AskRequest(options, AiUsageTask.ReplyDraft, "System", null, "Fakten", Schema);

        Assert.Equal(options.ModelExtract, extract.Model.Raw());
        Assert.Null(extract.Fallbacks);
        Assert.Equal(options.Model, prose.Model.Raw());
        Assert.NotNull(prose.Fallbacks);
    }

    [Fact]
    public void Ein_langer_System_Prompt_traegt_cache_control_ein_kurzer_nicht()
    {
        var options = new ClaudeOptions();

        var longer = ClaudeJson.AskRequest(options, AiUsageTask.ReplyDraft, new string('x', ClaudeJson.MinCachePrefixChars), null, "Fakten", Schema);
        var shorter = ClaudeJson.AskRequest(options, AiUsageTask.ReplyDraft, "Kurz.", null, "Fakten", Schema);

        Assert.NotNull(SystemBlock(longer).CacheControl);
        Assert.Null(SystemBlock(shorter).CacheControl);
    }

    private static readonly Dictionary<string, JsonElement> Schema = ClaudeJson.Schema(new { type = "object" });

    private static Anthropic.Models.Beta.Messages.BetaTextBlockParam SystemBlock(Anthropic.Models.Beta.Messages.MessageCreateParams request) =>
        Assert.Single(request.System!.TryPickBetaTextBlockParams(out var blocks) ? blocks : []);

    private static MailAnalysisRequest Request() => new(
        "gast@example.com", "Gast", "Termin", "Ich hätte gerne eine Massage.", new DateTimeOffset(2027, 2, 25, 7, 0, 0, TimeSpan.Zero), "Europe/Vienna",
        [HotStone, Classic], [Vienna], [Lena], null);

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    /// <summary>Modell-Attrappe: merkt sich den Prompt und antwortet mit festem JSON.</summary>
    private sealed class RecordingModel(string answer) : IAiJson
    {
        public string Prompt { get; private set; } = "";

        public Task<T> AskAsync<T>(AiUsageTask task, string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
        {
            Prompt = prompt;
            return Task.FromResult(JsonSerializer.Deserialize<T>(answer, ClaudeJson.Json)!);
        }
    }
}

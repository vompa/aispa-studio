using AispaStudio.Api.Assistant;
using AispaStudio.Api.MailIntake.Assistant;

namespace AispaStudio.Api.LocalAi;

/// <summary>
/// Terminannahme ohne Claude, mit lokalem Modell (ADR 0057): das Modell liest die Mail und zerlegt den Wunsch in Behandlung,
/// Standort, Person und Zeiten; freie Zeiten rechnet der Planer. Antworten an Gäste kommen aus den festen Textbausteinen,
/// weil kleine Modelle auf Französisch und Italienisch zu unsicher formulieren. Antwortet das Modell nicht, gelten die Regeln.
/// </summary>
internal sealed partial class LocalBookingAssistant(LocalModelJson model, RuleBasedBookingAssistant rules, ILogger<LocalBookingAssistant> log)
    : IBookingAssistant
{
    public async Task<MailAnalysis> AnalyzeAsync(MailAnalysisRequest request, CancellationToken ct)
    {
        try
        {
            return await ClaudeBookingAssistant.AnalyzeWithAsync(model, request, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogRules(log, ex);
            return await rules.AnalyzeAsync(request, ct);
        }
    }

    public Task<ReplyDraft> DraftReplyAsync(ReplyRequest request, CancellationToken ct) => rules.DraftReplyAsync(request, ct);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Das lokale KI-Modell hat die Mail nicht gelesen; die Regeln übernehmen.")]
    private static partial void LogRules(ILogger log, Exception ex);
}

/// <summary>Fragen an den Assistenten ohne Claude: das lokale Modell, und wenn es nicht antwortet, die Schlüsselwörter.</summary>
internal sealed partial class LocalAssistantClassifier(LocalModelJson model, RuleBasedAssistantClassifier rules, ILogger<LocalAssistantClassifier> log)
    : IAssistantClassifier
{
    public async Task<AssistantIntent> ClassifyAsync(string question, DateTimeOffset now, string timeZone, CancellationToken ct)
    {
        try
        {
            return await new ModelAssistantClassifier(model).ClassifyAsync(question, now, timeZone, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogRules(log, ex);
            return await rules.ClassifyAsync(question, now, timeZone, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Das lokale KI-Modell hat die Frage nicht verstanden; die Schlüsselwörter übernehmen.")]
    private static partial void LogRules(ILogger log, Exception ex);
}

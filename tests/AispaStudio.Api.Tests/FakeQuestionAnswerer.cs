using System.Collections.Concurrent;
using AispaStudio.Api.MailIntake.Assistant;

namespace AispaStudio.Api.Tests;

/// <summary>
/// KI-Attrappe für Antwortvorschläge: nennt die Öffnungszeiten aus den Angaben des Spas. „Parkplatz“ kann sie nicht beantworten,
/// bei „erfinden“ erfindet sie einen Preis. Merkt sich die Anfrage je Betreff.
/// </summary>
public sealed class FakeQuestionAnswerer : IQuestionAnswerer
{
    public static readonly ConcurrentDictionary<string, QuestionRequest> Requests = new();

    public bool Enabled => true;

    public Task<QuestionAnswer> AnswerAsync(QuestionRequest request, CancellationToken ct)
    {
        Requests[request.Subject] = request;
        if (request.Body.Contains("Parkplatz", StringComparison.Ordinal))
        {
            return Task.FromResult(new QuestionAnswer(false, null));
        }

        if (request.Body.Contains("erfinden", StringComparison.Ordinal))
        {
            return Task.FromResult(new QuestionAnswer(true, "Nur heute kostet jede Massage 12345 Euro weniger."));
        }

        var hours = request.Facts.Split('\n').First(l => l.StartsWith("Öffnungszeiten:", StringComparison.Ordinal)).Trim();
        return Task.FromResult(new QuestionAnswer(true, $"Guten Tag {request.GuestName},\n\nunsere {hours}\n\n{request.SpaName}"));
    }
}

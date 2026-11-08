using System.Collections.Concurrent;
using AispaStudio.Api.Retention;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Attrappe für die Erinnerung an die nächste Behandlung: fester Satz, außer ein Test gibt für einen Vornamen etwas vor.</summary>
public sealed class FakeRebookWriter : IRebookWriter
{
    public static readonly ConcurrentDictionary<string, string> Answers = new();

    public Task<string?> WriteAsync(RebookFacts facts, CancellationToken ct) =>
        Task.FromResult<string?>(Answers.TryGetValue(facts.Guest.FirstName, out var answer)
            ? answer
            : $"Liebe Grüße, {facts.Guest.FirstName}: Ihre {facts.Treatment} tut bestimmt wieder gut.");
}

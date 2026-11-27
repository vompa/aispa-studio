using System.Collections.Concurrent;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// KI-Attrappe für „Mit KI auswerten“: nennt als Kernaussage die erste Zeile der Fakten, außer ein Test gibt eine eigene Antwort vor (Schlüssel: die eigene Frage).
/// Merkt sich jede Anfrage mit Frage und Fakten.
/// </summary>
public sealed class FakeManagementAnalyst : IManagementAnalyst
{
    public static readonly ConcurrentDictionary<string, ManagementAnalysisResult> Answers = new();

    public static readonly ConcurrentBag<(string? Question, string Facts)> Requests = [];

    public bool Enabled => true;

    public Task<ManagementAnalysisResult?> AnalyzeAsync(string facts, ManagementAnalysisFocus focus, string? question, CancellationToken ct)
    {
        Requests.Add((question, facts));
        return Task.FromResult<ManagementAnalysisResult?>(question is not null && Answers.TryGetValue(question, out var answer)
            ? answer
            : new ManagementAnalysisResult([$"Ausgewertet: {facts.Split('\n')[0].Trim()}"], [], [], ["Warum Gäste fernbleiben, zeigen die Zahlen nicht."]));
    }
}

using System.Collections.Concurrent;
using AispaStudio.Api.Reporting;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Attrappe für den Managementbericht: nennt die erste Kennzahl der Fakten, außer ein Test gibt einen eigenen Text vor (Schlüssel: Zeitraum-Zeile).</summary>
public sealed class FakeManagementSummarizer : IManagementSummarizer
{
    public static readonly ConcurrentDictionary<string, string> Answers = new();

    public static readonly ConcurrentBag<string> Requests = [];

    public bool Enabled => true;

    public Task<string?> SummarizeAsync(string facts, CancellationToken ct)
    {
        Requests.Add(facts);
        var period = facts.Split('\n')[0].Trim();
        return Task.FromResult<string?>(Answers.TryGetValue(period, out var answer) ? answer : $"Kommentar zu: {period}");
    }
}

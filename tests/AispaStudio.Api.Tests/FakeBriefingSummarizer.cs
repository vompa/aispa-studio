using System.Collections.Concurrent;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Attrappe für den Tagesstart: wiederholt die regelbasierte Zeile, außer ein Test gibt für seinen Standort einen eigenen Text vor.</summary>
public sealed class FakeBriefingSummarizer : IBriefingSummarizer
{
    public static readonly ConcurrentDictionary<Guid, string> Answers = new();

    public bool Enabled => true;

    public Task<string?> SummarizeAsync(BriefingDto facts, CancellationToken ct) =>
        Task.FromResult<string?>(Answers.TryGetValue(facts.LocationId, out var answer) ? answer : $"Guten Überblick: {facts.Summary}");
}

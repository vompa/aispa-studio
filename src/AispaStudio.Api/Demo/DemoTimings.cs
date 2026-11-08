using System.Collections.Concurrent;
using System.Diagnostics;

namespace AispaStudio.Api.Demo;

/// <summary>Sammelt die Laufzeit der Abschnitte beim Einspielen (Summe und Anzahl je Name) und gibt sie am Ende knapp ins Protokoll.</summary>
internal sealed partial class DemoTimings
{
    private readonly ConcurrentDictionary<string, (long Ticks, int Count)> _sections = new();

    /// <summary>Misst bis zum <c>Dispose</c>: <c>using (timings.Measure("Name")) { … }</c>.</summary>
    public Section Measure(string name) => new(this, name);

    /// <summary>Summe der Sekunden je Name, absteigend sortiert.</summary>
    public IReadOnlyList<(string Name, double Seconds, int Count)> Snapshot() =>
    [
        .. _sections.OrderByDescending(s => s.Value.Ticks)
            .Select(s => (s.Key, Stopwatch.GetElapsedTime(0, s.Value.Ticks).TotalSeconds, s.Value.Count)),
    ];

    public void Log(ILogger logger)
    {
        foreach (var (name, seconds, count) in Snapshot())
        {
            Logged(logger, name, seconds, count);
        }
    }

    private void Add(string name, long ticks) =>
        _sections.AddOrUpdate(name, (ticks, 1), (_, old) => (old.Ticks + ticks, old.Count + 1));

    [LoggerMessage(Level = LogLevel.Information, Message = "Beispieldaten, Zeit: {Section} {Seconds:0.0} s ({Count}x)")]
    private static partial void Logged(ILogger logger, string section, double seconds, int count);

    public readonly struct Section(DemoTimings owner, string name) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();

        public void Dispose() => owner.Add(name, Stopwatch.GetTimestamp() - _start);
    }
}

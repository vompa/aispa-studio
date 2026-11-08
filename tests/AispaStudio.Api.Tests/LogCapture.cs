using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AispaStudio.Api.Tests;

/// <summary>Hält die Protokollzeilen eines Hosts fest, damit Tests prüfen können, was die Instanz meldet (und was nicht).</summary>
internal sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Lines => [.. _lines];

    public ILogger CreateLogger(string categoryName) => new Capture(_lines);

    public void Dispose()
    {
    }

    private sealed class Capture(ConcurrentQueue<(LogLevel Level, string Message)> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue((logLevel, formatter(state, exception)));
    }
}

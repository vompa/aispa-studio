using System.Diagnostics;

namespace AispaStudio.Api.Tests;

/// <summary>Zählt die Befehle an PostgreSQL, die der Request mit der Kopfzeile <see cref="Traceparent"/> auslöst, und sonst keine.</summary>
internal sealed class CommandCounter : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ActivityTraceId _trace = ActivityTraceId.CreateRandom();
    private int _count;

    public CommandCounter()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                // Nur Befehle (Aktivitäten mit SQL-Text) im Trace des Requests; der Verbindungsaufbau („CONNECT“) hat keinen Text.
                var text = activity.GetTagItem("db.query.text") ?? activity.GetTagItem("db.statement");
                if (text is not null && activity.TraceId == _trace)
                {
                    Interlocked.Increment(ref _count);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Kopfzeile <c>traceparent</c> für den Request, dessen Befehle gezählt werden sollen.</summary>
    public string Traceparent => $"00-{_trace}-{ActivitySpanId.CreateRandom()}-01";

    public int Count => Volatile.Read(ref _count);

    public void Dispose() => _listener.Dispose();
}

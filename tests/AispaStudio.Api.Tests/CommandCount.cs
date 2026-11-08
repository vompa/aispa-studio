using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Zählt die Datenbankbefehle, die EF während eines Aufrufs absetzt. Gezählt wird über das Diagnoseereignis von EF; Befehle paralleler Tests
/// zählen nicht mit, weil nur Ereignisse aus dem eigenen asynchronen Ablauf gelten.
/// </summary>
internal sealed class CommandCount : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private static readonly AsyncLocal<CommandCount?> Active = new();
    private readonly List<IDisposable> _subscriptions = [];
    private int _count;

    private CommandCount()
    {
    }

    public static async Task<int> OfAsync(Func<Task> action)
    {
        using var counter = new CommandCount();
        counter._subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(counter));
        Active.Value = counter;
        try
        {
            await action();
        }
        finally
        {
            Active.Value = null;
        }

        return counter._count;
    }

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == DbLoggerCategory.Name)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == RelationalEventId.CommandExecuting.Name && Active.Value == this)
        {
            Interlocked.Increment(ref _count);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        lock (_subscriptions)
        {
            _subscriptions.ForEach(s => s.Dispose());
        }
    }
}

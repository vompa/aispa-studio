using JasperFx;
using JasperFx.CodeGeneration;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace AispaStudio.Api.MailIntake;

/// <summary>
/// Fehlerregel der Mail-Queue in den Rollen <c>Api</c> und <c>Worker</c> (ADR 0087, Entscheidung des Eigentümers vom 07.11.2026): kurz an Ort
/// und Stelle wiederholen, dann Fehlerablage. Kein <c>ScheduleRetry</c> und kein <c>Requeue</c>: Beide stellen die Nachricht hinten an, und
/// die nächste Mail desselben Spas (z. B. Buchung und Storno) käme ihr zuvor. Die Rolle <c>All</c> behält die globale Regel aus <c>Program.cs</c>.
/// </summary>
internal sealed partial class MailIntakeFailurePolicy : IHandlerPolicy
{
    /// <summary>Pausen zwischen den Versuchen: Eine scheiternde Nachricht hält die Queue 21 Sekunden plus die Dauer der vier Versuche auf.</summary>
    public static readonly TimeSpan[] Cooldowns = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(c => MailIntakeSetup.QueuedMessageTypes.Contains(c.MessageType)))
        {
            chain.OnAnyException().RetryWithCooldown(Cooldowns).Then.MoveToErrorQueue().And(GiveUpAsync, "Mail-Queue: Fehlerablage");
        }
    }

    /// <summary>Letzter Versuch gescheitert: im Protokoll melden; eine Antwort, die nicht hinausging, wird wieder Entwurf.</summary>
    private static async ValueTask GiveUpAsync(IWolverineRuntime runtime, IEnvelopeLifecycle lifecycle, Exception exception)
    {
        var log = runtime.LoggerFactory.CreateLogger<MailIntakeFailurePolicy>();
        var envelope = lifecycle.Envelope;
        LogDeadLettered(log, exception, envelope?.MessageType, envelope?.Attempts ?? 0);
        if (envelope?.Message is not ReplySendRequested { MailId: var mailId })
        {
            return;
        }

        try
        {
            await MailIntakeHandler.ReturnUnsentReplyAsync(runtime.Services.GetRequiredService<IServiceScopeFactory>(), mailId, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReturnFailed(log, ex, mailId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail-Queue: {MessageType} nach {Attempts} Versuchen in der Fehlerablage.")]
    private static partial void LogDeadLettered(ILogger logger, Exception exception, string? messageType, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail-Queue: Antwort zu Mail {MailId} ließ sich nicht wieder als Entwurf speichern.")]
    private static partial void LogReturnFailed(ILogger logger, Exception exception, Guid mailId);
}

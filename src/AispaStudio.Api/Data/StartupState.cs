using System.Globalization;
using AispaStudio.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AispaStudio.Api.Data;

/// <summary>
/// Wie weit der Start ist: Migration, Einspielen des Beispiel-Spas, bereit oder fehlgeschlagen. Ohne Migration und Beispieldaten
/// ist er von Anfang an bereit. Migrator und Seeder setzen ihn; <c>/api/system/status</c> und der Health-Check „startup“ lesen ihn.
/// </summary>
internal sealed class StartupState(TimeProvider time)
{
    private readonly Lock _gate = new();
    private StartupPhase _phase = StartupPhase.Ready;
    private DateTimeOffset _since = time.GetUtcNow();
    private string _failure = "";
    private TaskCompletionSource _ready = Completed();

    public void Begin(StartupPhase phase)
    {
        lock (_gate)
        {
            (_phase, _since, _failure) = (phase, time.GetUtcNow(), "");
            if (phase == StartupPhase.Ready)
            {
                _ready.TrySetResult();
            }
            else if (_ready.Task.IsCompleted)
            {
                _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    /// <summary>
    /// Wartet, bis der Start bereit ist (Migration und Einspielen der Beispieldaten fertig). Hintergrundjobs, die auf Fachdaten
    /// arbeiten, rufen es vor jedem Lauf auf; sie stören sonst das Einspielen und lesen unfertige Daten. Bei einem Fehlschlag
    /// wartet es weiter, denn die Daten sind dann unvollständig. Liefert <c>true</c> für <c>&amp;&amp;</c> in Schleifenbedingungen.
    /// </summary>
    public async Task<bool> WaitUntilReadyAsync(CancellationToken ct)
    {
        Task ready;
        lock (_gate)
        {
            ready = _ready.Task;
        }

        await ready.WaitAsync(ct);
        return true;
    }

    private static TaskCompletionSource Completed()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    public void Complete() => Begin(StartupPhase.Ready);

    public void Fail(string message)
    {
        lock (_gate)
        {
            (_phase, _since, _failure) = (StartupPhase.Failed, time.GetUtcNow(), message);
            if (_ready.Task.IsCompleted)
            {
                _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    public StartupInfo Snapshot()
    {
        lock (_gate)
        {
            var seconds = Math.Max(0, (time.GetUtcNow() - _since).TotalSeconds).ToString("0", CultureInfo.InvariantCulture);
            return new StartupInfo(_phase, _phase switch
            {
                StartupPhase.Migrating => $"Datenbank wird migriert (seit {seconds} s)",
                StartupPhase.Seeding => $"Beispiel-Spa wird eingerichtet (seit {seconds} s)",
                StartupPhase.Failed => _failure,
                _ => "Bereit",
            });
        }
    }
}

/// <summary>
/// Health-Check „startup“ (nicht „live“): erst bei bereitem Start gesund. Sonst ungesund mit dem Stand als Beschreibung, damit das
/// Aspire-Dashboard ihn an der Ressource „api“ zeigt und <c>WaitFor</c> (Web, Windows-App) wartet. <c>/alive</c> bleibt davon unberührt.
/// </summary>
internal sealed class StartupHealthCheck(StartupState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var info = state.Snapshot();
        return Task.FromResult(info.Phase == StartupPhase.Ready ? HealthCheckResult.Healthy(info.Text) : HealthCheckResult.Unhealthy(info.Text));
    }
}

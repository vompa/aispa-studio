using AispaStudio.Contracts;

namespace AispaStudio.Api.Data;

/// <summary>
/// Spielt beim Start die EF-Core-Migrationen ein, wenn <c>Database:MigrateOnStartup</c> gesetzt ist (Entwicklung, Tests).
/// Bewusst vor dem Start der API abgewartet: Einzelplatz, Abgleich und Tests brauchen die Tabellen ab der ersten Anfrage.
/// </summary>
internal sealed class DatabaseMigrator(IServiceProvider services, StartupState state) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        state.Begin(StartupPhase.Migrating);
        await using var scope = services.CreateAsyncScope();
        await DatabaseMigrations.ApplyAsync(scope.ServiceProvider, cancellationToken);
        state.Complete();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

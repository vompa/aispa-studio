using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AispaStudio.AppHost;

/// <summary>
/// Befehle im Aspire-Dashboard an der PostgreSQL-Ressource (nur Entwicklung): Sicherung erstellen, einspielen, Beispiel-Spa neu einspielen.
/// </summary>
internal static class DatabaseCommands
{
    private const string DatabaseName = "aispadb";
    private const string RestoreFileInput = "datei";
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(60);

    public static IResourceBuilder<PostgresServerResource> WithDatabaseCommands(
        this IResourceBuilder<PostgresServerResource> postgres, string backupFolder, string apiResourceName)
    {
        var server = postgres.Resource;

        return postgres
            .WithCommand(
                "backup-create",
                "Sicherung erstellen",
                context => CreateBackupAsync(context, server, backupFolder),
                new CommandOptions
                {
                    Description = $"Sichert die Datenbank {DatabaseName} (pg_dump) in den Ordner {DatabaseBackupFiles.FolderName} im Repository.",
                    IconName = "ArrowDownload",
                    UpdateState = IsRunning,
                })
            .WithCommand(
                "backup-restore",
                "Sicherung einspielen …",
                context => RestoreBackupAsync(context, server, apiResourceName, backupFolder),
                new CommandOptions
                {
                    Description = "Spielt eine Sicherung aus dem Sicherungsordner zurück; ohne Auswahl die neueste.",
                    ConfirmationMessage = "Aktuelle Daten in aispadb werden überschrieben. Die API wird dafür kurz angehalten.",
                    IconName = "ArrowUpload",
                    UpdateState = IsRunning,
                    Arguments =
                    [
                        new InteractionInput
                        {
                            Name = RestoreFileInput,
                            Label = "Sicherung",
                            Description = "Leer lassen für die neueste Sicherung.",
                            InputType = InputType.Choice,
                            Required = false,
                            DynamicLoading = new InputLoadOptions
                            {
                                AlwaysLoadOnStart = true,
                                LoadCallback = context =>
                                {
                                    context.Input.Options = DatabaseBackupFiles.ListNewestFirst(backupFolder)
                                        .Select(name => KeyValuePair.Create(name, name))
                                        .ToList();
                                    return Task.CompletedTask;
                                },
                            },
                        },
                    ],
                })
            .WithCommand(
                "demo-reseed",
                "Beispiel-Spa neu einspielen …",
                context => ReseedAsync(context, server, apiResourceName),
                new CommandOptions
                {
                    Description = "Leert die Datenbank; beim Start der API entstehen Schema und Beispiel-Spa neu (Fortschritt: Health-Check startup der API).",
                    ConfirmationMessage = "Alle Daten in aispadb werden gelöscht und das Beispiel-Spa neu eingespielt. Die API wird dafür kurz angehalten.",
                    IconName = "ArrowCounterclockwise",
                    UpdateState = IsRunning,
                });
    }

    private static ResourceCommandState IsRunning(UpdateCommandStateContext context) =>
        context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
            ? ResourceCommandState.Enabled
            : ResourceCommandState.Disabled;

    private static async Task<ExecuteCommandResult> CreateBackupAsync(
        ExecuteCommandContext context, PostgresServerResource server, string backupFolder)
    {
        try
        {
            var postgres = await ResolveContainerAsync(context, server);
            Directory.CreateDirectory(backupFolder);

            var fileName = DatabaseBackupFiles.CreateFileName(DateTime.Now);
            var target = Path.Combine(backupFolder, fileName);
            var partial = target + ".part";
            try
            {
                await using (var stream = File.Create(partial))
                {
                    await postgres.DumpAsync(DatabaseName, stream, context.CancellationToken);
                }

                File.Move(partial, target);
            }
            catch
            {
                File.Delete(partial);
                throw;
            }

            var size = DatabaseBackupFiles.FormatSize(new FileInfo(target).Length);
            var message = $"Sicherung {fileName} erstellt ({size}) in {DatabaseBackupFiles.FolderName}.";
            context.Logger.LogInformation("Befehl abgeschlossen.");
            return CommandResults.Success(message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Logger.LogError(exception, "Sicherung fehlgeschlagen.");
            return CommandResults.Failure($"Sicherung fehlgeschlagen: {exception.Message}");
        }
    }

    private static async Task<ExecuteCommandResult> RestoreBackupAsync(
        ExecuteCommandContext context, PostgresServerResource server, string apiResourceName, string backupFolder)
    {
        string? selected = null;
        if (context.Arguments is not null && context.Arguments.TryGetByName(RestoreFileInput, out var input))
        {
            selected = input.Value;
        }

        var (path, error) = DatabaseBackupFiles.Resolve(backupFolder, selected);
        if (path is null)
        {
            return CommandResults.Failure(error!);
        }

        try
        {
            var postgres = await ResolveContainerAsync(context, server);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            await WithApiStoppedAsync(context, apiResourceName, async () =>
            {
                await postgres.RecreateDatabaseAsync(DatabaseName, context.CancellationToken);
                await postgres.RestoreAsync(DatabaseName, stream, context.CancellationToken);
            });

            var message = $"Sicherung {Path.GetFileName(path)} eingespielt, die API wurde neu gestartet.";
            context.Logger.LogInformation("Befehl abgeschlossen.");
            return CommandResults.Success(message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Logger.LogError(exception, "Einspielen fehlgeschlagen.");
            return CommandResults.Failure($"Einspielen fehlgeschlagen (die API wurde wieder gestartet): {exception.Message}");
        }
    }

    private static async Task<ExecuteCommandResult> ReseedAsync(
        ExecuteCommandContext context, PostgresServerResource server, string apiResourceName)
    {
        try
        {
            var postgres = await ResolveContainerAsync(context, server);
            await WithApiStoppedAsync(context, apiResourceName,
                () => postgres.RecreateDatabaseAsync(DatabaseName, context.CancellationToken));

            return CommandResults.Success(
                "Datenbank geleert, die API startet. Das Beispiel-Spa entsteht in zwei bis drei Minuten (Health-Check startup an der API).");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Logger.LogError(exception, "Neu einspielen fehlgeschlagen.");
            return CommandResults.Failure($"Neu einspielen fehlgeschlagen (die API wurde wieder gestartet): {exception.Message}");
        }
    }

    /// <summary>Hält die API an, führt die Aktion aus und startet die API in jedem Fall wieder.</summary>
    private static async Task WithApiStoppedAsync(ExecuteCommandContext context, string apiResourceName, Func<Task> action)
    {
        var commands = context.Services.GetRequiredService<ResourceCommandService>();
        var notifications = context.Services.GetRequiredService<ResourceNotificationService>();

        var stop = await commands.ExecuteCommandAsync(apiResourceName, KnownResourceCommands.StopCommand, context.CancellationToken);
        if (!stop.Success)
        {
            throw new InvalidOperationException($"Die API ließ sich nicht anhalten: {stop.Message}");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeout.CancelAfter(StopTimeout);
            await notifications.WaitForResourceAsync(
                apiResourceName,
                [KnownResourceStates.Exited, KnownResourceStates.Finished, KnownResourceStates.FailedToStart],
                timeout.Token);

            await action();
        }
        finally
        {
            var start = await commands.ExecuteCommandAsync(apiResourceName, KnownResourceCommands.StartCommand, CancellationToken.None);
            if (!start.Success)
            {
                context.Logger.LogError("Die API ließ sich nicht wieder starten: {Error}", start.Message);
            }
        }
    }

    private static async Task<PostgresContainer> ResolveContainerAsync(ExecuteCommandContext context, PostgresServerResource server)
    {
        var notifications = context.Services.GetRequiredService<ResourceNotificationService>();
        if (!notifications.TryGetCurrentState(server.Name, out var resourceEvent))
        {
            throw new InvalidOperationException("Der Zustand der PostgreSQL-Ressource ist nicht verfügbar.");
        }

        var containerId = resourceEvent.Snapshot.Properties
            .FirstOrDefault(property => property.Name == "container.id")?.Value as string;
        if (!PostgresContainer.IsContainerId(containerId))
        {
            throw new InvalidOperationException("Die Container-Kennung von PostgreSQL ist nicht bekannt; läuft der Container?");
        }

        var userName = await server.UserNameReference.GetValueAsync(context.CancellationToken) ?? "postgres";
        return new PostgresContainer(containerId!, userName);
    }
}

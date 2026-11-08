using System.Data.Common;
using System.Runtime.CompilerServices;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Setzt Person und Kanal (<see cref="ChangeActor"/>) als Sitzungseinstellung, bevor über eine Verbindung zum ersten Mal geschrieben wird, damit der
/// Verlaufs-Trigger sie lesen kann (ADR 0082). Lesende Abfragen kosten nichts; wechselt Person oder Kanal auf derselben Verbindung, wird neu gesetzt.
/// Npgsql setzt die Sitzung bei der Rückgabe in den Pool zurück, deshalb gilt die Einstellung nur bis zum nächsten Öffnen.
/// </summary>
internal sealed class ChangeActorInterceptor(IHttpContextAccessor http) : DbCommandInterceptor, IDbConnectionInterceptor
{
    private static readonly string SetSql =
        $"SELECT set_config('{ChangeHistory.UserSetting}', @user, false), set_config('{ChangeHistory.ChannelSetting}', @channel, false)";

    private readonly ConditionalWeakTable<DbConnection, StrongBox<(ChangeChannel, string?)>> _applied = [];

    public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => _applied.Remove(connection);

    public Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _applied.Remove(connection);
        return Task.CompletedTask;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken);
        return result;
    }

    private void Apply(DbCommand command)
    {
        if (Pending(command) is { } actor)
        {
            using var set = SetCommand(command, actor);
            set.ExecuteNonQuery();
            Remember(command.Connection!, actor);
        }
    }

    private async Task ApplyAsync(DbCommand command, CancellationToken ct)
    {
        if (Pending(command) is { } actor)
        {
            await using var set = SetCommand(command, actor);
            await set.ExecuteNonQueryAsync(ct);
            Remember(command.Connection!, actor);
        }
    }

    /// <summary>Person und Kanal, wenn der Befehl schreibt und die Verbindung sie noch nicht trägt.</summary>
    private (ChangeChannel Channel, string? UserId)? Pending(DbCommand command)
    {
        if (command.Connection is not { } connection || !Writes(command.CommandText))
        {
            return null;
        }

        var actor = ChangeActor.Resolve(http.HttpContext);
        return _applied.TryGetValue(connection, out var applied) && applied.Value == actor ? null : actor;
    }

    private static bool Writes(string sql) =>
        sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase) || sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase)
        || sql.Contains("DELETE ", StringComparison.OrdinalIgnoreCase);

    private static DbCommand SetCommand(DbCommand command, (ChangeChannel Channel, string? UserId) actor)
    {
        var set = command.Connection!.CreateCommand();
        set.Transaction = command.Transaction;
        set.CommandText = SetSql;
        AddParameter(set, "user", actor.UserId ?? "");
        AddParameter(set, "channel", actor.Channel.ToString());
        return set;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private void Remember(DbConnection connection, (ChangeChannel, string?) actor) => _applied.AddOrUpdate(connection, new(actor));
}

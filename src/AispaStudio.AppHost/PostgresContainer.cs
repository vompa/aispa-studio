using System.Diagnostics;
using System.Text;

namespace AispaStudio.AppHost;

/// <summary>
/// Führt pg_dump, pg_restore und psql per <c>docker exec</c> im laufenden PostgreSQL-Container aus.
/// Das Image verlangt auch über den lokalen Socket ein Passwort; es kommt im Container aus dessen Variable <c>POSTGRES_PASSWORD</c>,
/// steht also nie in einer Befehlszeile auf dem Rechner und wird nicht ausgegeben.
/// </summary>
internal sealed class PostgresContainer
{
    private const string AdminDatabase = "postgres";

    private readonly string _containerId;
    private readonly string _userName;

    public PostgresContainer(string containerId, string userName)
    {
        if (!IsContainerId(containerId))
        {
            throw new ArgumentException("Ungültige Container-Kennung.", nameof(containerId));
        }

        _containerId = containerId;
        _userName = userName;
    }

    /// <summary>Docker-Kennungen sind hexadezimal; alles andere wird nicht als Argument weitergereicht.</summary>
    public static bool IsContainerId(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 64 && value.All(Uri.IsHexDigit);

    /// <summary>Setzt im Container das Passwort aus dessen Umgebung und startet dann das Werkzeug mit den übrigen Argumenten unverändert.</summary>
    public const string WithPassword = "PGPASSWORD=\"$POSTGRES_PASSWORD\" exec \"$@\"";

    public IReadOnlyList<string> DumpArguments(string database) =>
        InContainer(interactive: false, "pg_dump", "-U", _userName, "-Fc", database);

    public IReadOnlyList<string> RestoreArguments(string database) =>
        InContainer(interactive: true, "pg_restore", "-U", _userName, "-d", database, "--no-owner", "--no-acl", "--exit-on-error");

    public IReadOnlyList<string> SqlArguments(string database, string sql) =>
        InContainer(interactive: false, "psql", "-U", _userName, "-d", database, "-v", "ON_ERROR_STOP=1", "-tA", "-c", sql);

    private string[] InContainer(bool interactive, params string[] command) =>
        [.. interactive ? ["exec", "-i", _containerId] : new[] { "exec", _containerId }, "sh", "-c", WithPassword, "sh", .. command];

    public async Task DumpAsync(string database, Stream destination, CancellationToken cancellationToken)
    {
        await RunAsync(DumpArguments(database), input: null, output: destination, cancellationToken);
    }

    public async Task RestoreAsync(string database, Stream source, CancellationToken cancellationToken)
    {
        await RunAsync(RestoreArguments(database), source, output: null, cancellationToken);
    }

    /// <summary>Führt eine SQL-Anweisung aus und liefert die Ausgabe (ohne Kopfzeilen).</summary>
    public async Task<string> ExecuteSqlAsync(string database, string sql, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await RunAsync(SqlArguments(database, sql), null, output, cancellationToken);
        return Encoding.UTF8.GetString(output.ToArray()).Trim();
    }

    /// <summary>Trennt alle Verbindungen, löscht die Datenbank und legt sie leer neu an.</summary>
    public async Task RecreateDatabaseAsync(string database, CancellationToken cancellationToken)
    {
        await ExecuteSqlAsync(AdminDatabase, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", cancellationToken);
        await ExecuteSqlAsync(AdminDatabase, $"CREATE DATABASE \"{database}\"", cancellationToken);
    }

    private static async Task RunAsync(IReadOnlyList<string> arguments, Stream? input, Stream? output, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Docker konnte nicht gestartet werden.");

        var error = new StringBuilder();
        var errorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) is not null)
            {
                error.AppendLine(line);
            }
        }, cancellationToken);

        var outputTask = output is null
            ? process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken)
            : process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);

        if (input is not null)
        {
            await input.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
            process.StandardInput.Close();
        }

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker exec endete mit Code {process.ExitCode}: {error.ToString().Trim()}");
        }
    }
}

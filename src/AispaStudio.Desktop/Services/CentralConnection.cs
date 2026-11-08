using System.Data.Common;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Verbindung zur zentralen PostgreSQL für den Abgleich (ADR 0041, Schritt 4), im Format von Npgsql
/// (<c>Host=…;Database=…;Username=…;Password=…;SSL Mode=Require</c>). Die App prüft nur die Form; ob die Gegenstelle antwortet,
/// zeigt der erste Abgleich.
/// </summary>
public static class CentralConnection
{
    private static readonly string[] HostKeys = ["host", "server"];
    private static readonly string[] DatabaseKeys = ["database", "db"];
    private static readonly string[] UserKeys = ["username", "user id", "userid", "user name", "user"];

    /// <summary>Wirft mit verständlichem Grund, wenn Server, Datenbank oder Konto fehlen oder die Angabe auf diesen PC zeigt.</summary>
    public static void Validate(string connectionString, int localDatabasePort)
    {
        var parts = Parse(connectionString);
        var host = Find(parts, HostKeys);
        if (host is null || Find(parts, DatabaseKeys) is null || Find(parts, UserKeys) is null)
        {
            throw new LocalServerException("Bitte Server (Host), Datenbank (Database) und Konto (Username) der zentralen Datenbank angeben.");
        }

        var port = Find(parts, ["port"]) ?? "5432";
        if (host is "127.0.0.1" or "localhost" or "::1" && port == localDatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            throw new LocalServerException("Das ist die Datenbank dieses PCs. Für den Abgleich die zentrale Datenbank angeben.");
        }
    }

    /// <summary>„Server · Datenbank“ für die Anzeige, ohne Konto und Passwort.</summary>
    public static string Describe(string connectionString)
    {
        try
        {
            var parts = Parse(connectionString);
            return $"{Find(parts, HostKeys)} · {Find(parts, DatabaseKeys)}";
        }
        catch (LocalServerException)
        {
            return "eingetragene Datenbank";
        }
    }

    private static DbConnectionStringBuilder Parse(string connectionString)
    {
        try
        {
            return new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            throw new LocalServerException("Die Verbindungsangabe ist nicht lesbar. Format: Host=…;Database=…;Username=…;Password=…");
        }
    }

    private static string? Find(DbConnectionStringBuilder parts, string[] keys) =>
        keys.Select(k => parts.TryGetValue(k, out var value) ? value?.ToString()?.Trim() : null).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}

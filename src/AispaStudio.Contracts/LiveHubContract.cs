namespace AispaStudio.Contracts;

/// <summary>Pfad und Methodennamen des SignalR-Hubs für Echtzeit-Updates.</summary>
public static class LiveHubContract
{
    public const string Path = "/hubs/live";

    /// <summary>Server → Client: Ergebnis eines Pings.</summary>
    public const string Pong = nameof(Pong);

    /// <summary>Server → Client: ein Termin wurde gebucht, verschoben, storniert oder hat seinen Status geändert (<see cref="AppointmentDto"/>).</summary>
    public const string AppointmentChanged = nameof(AppointmentChanged);

    /// <summary>Server → Client: im Betrieb eines Standorts hat sich etwas geändert, das Meldungen auslösen oder beenden kann (Standort-Id, <see cref="OperationsChangeKind"/>).</summary>
    public const string OperationsChanged = nameof(OperationsChanged);

    /// <summary>Server → Client: im Posteingang ist eine Nachricht neu oder hat sich geändert (<see cref="InboxChangedEvent"/>).</summary>
    public const string InboxChanged = nameof(InboxChanged);

    /// <summary>Client → Server: Änderungen im Posteingang des eigenen Spas empfangen (nur mit Buchungsrecht).</summary>
    public const string WatchInbox = nameof(WatchInbox);

    /// <summary>Server → Client: an der Tafel im Pausenraum hat sich etwas geändert, sie lädt neu (ohne Nutzdaten: nie Gastnamen an die Anzeige).</summary>
    public const string DisplayChanged = nameof(DisplayChanged);

    /// <summary>Client → Server: nur die gekoppelte Anzeige; hört auf die Änderungen ihres Standorts (Standort aus ihrer Anmeldung).</summary>
    public const string WatchDisplay = nameof(WatchDisplay);

    /// <summary>Client → Server: Live-Updates eines Standorts abonnieren (Parameter: Standort-Id).</summary>
    public const string WatchLocation = nameof(WatchLocation);

    /// <summary>Client → Server: Abonnement eines Standorts beenden.</summary>
    public const string UnwatchLocation = nameof(UnwatchLocation);

    public static string LocationGroup(Guid locationId) => $"location:{locationId}";

    public static string DisplayGroup(Guid locationId) => $"display:{locationId}";

    public static string InboxGroup(Guid tenantId) => $"inbox:{tenantId}";
}

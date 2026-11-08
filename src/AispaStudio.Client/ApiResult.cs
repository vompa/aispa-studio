using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Client;

/// <summary>Ergebnis eines Aufrufs: Wert oder eine Meldung, die sich der Rezeption so zeigen lässt.</summary>
public sealed record ApiResult<T>(T? Value, string? Error, HttpStatusCode Status)
{
    public bool Ok => Error is null;

    /// <summary>Ohne Verbindung aus dem lokalen Stand: so alt sind die Daten.</summary>
    public DateTimeOffset? CachedAt { get; init; }

    /// <summary>Ohne Verbindung vorgemerkt; wird beim nächsten Kontakt mit dem Server gesendet.</summary>
    public bool Queued { get; init; }

    /// <summary>Abgelehnte Buchung oder Verschiebung mit Begründung.</summary>
    public BookingRejected? Rejection { get; init; }
}

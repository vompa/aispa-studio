namespace AispaStudio.Api.Pos;

internal enum PosErrorKind
{
    NotFound,
    Invalid,
    Conflict,
    Forbidden,
}

internal sealed record PosError(PosErrorKind Kind, string Field, string Message);

/// <summary>Ergebnis einer Kassenaktion; die Endpunkte machen daraus die passende HTTP-Antwort.</summary>
internal sealed record PosResult<T>(T? Value, PosError? Error)
{
    public static implicit operator PosResult<T>(PosError error) => new(default, error);
}

/// <summary>Bricht eine Kassenaktion ab; die laufende Transaktion wird zurückgerollt, es entsteht kein Beleg.</summary>
#pragma warning disable CA1032, CA1064 // Nur innerhalb des Dienstes geworfen und gefangen.
internal sealed class PosFailure(PosError error) : Exception(error.Message)
#pragma warning restore CA1032, CA1064
{
    public PosError Error { get; } = error;

    public static PosFailure NotFound(string what) => new(new(PosErrorKind.NotFound, what, $"{what} nicht gefunden."));

    public static PosFailure Invalid(string field, string message) => new(new(PosErrorKind.Invalid, field, message));

    public static PosFailure Conflict(string message) => new(new(PosErrorKind.Conflict, "", message));

    public static PosFailure Forbidden(string message) => new(new(PosErrorKind.Forbidden, "", message));
}

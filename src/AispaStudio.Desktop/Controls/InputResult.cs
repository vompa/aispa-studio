namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Ergebnis des Lesens einer Eingabe (Design-Brief 5.16): ein Wert, kein Wert (leer, <see cref="IsEmpty"/>) oder ein Fehlertext mit Problem und Weg.
/// Die Leser (<see cref="TimeInputParser"/>, <see cref="DateInputParser"/>, <see cref="MoneyInputParser"/>, <see cref="NumberInputParser"/>)
/// kennen keine Oberfläche; ein leeres Feld ist „keine Angabe“, nie 0 oder Mitternacht.
/// </summary>
public readonly record struct InputResult<T>(T? Value, string? Error, bool IsEmpty = false)
    where T : struct
{
    /// <summary>Die Eingabe ist lesbar oder leer; ob leer erlaubt ist, entscheidet das Feld (Pflicht).</summary>
    public bool IsValid => Error is null;
}

/// <summary>Erzeugt <see cref="InputResult{T}"/>; statisch und nicht-generisch, weil statische Member in generischen Typen verboten sind (CA1000).</summary>
public static class InputResult
{
    public static InputResult<T> Empty<T>() where T : struct => new(null, null, true);

    public static InputResult<T> Ok<T>(T value) where T : struct => new(value, null);

    public static InputResult<T> Fail<T>(string error) where T : struct => new(null, error);
}

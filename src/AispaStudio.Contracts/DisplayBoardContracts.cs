namespace AispaStudio.Contracts;

/// <summary>
/// Was die Anzeige im Pausenraum zeigt (Slice 105): nur lesen, Initialen statt Gastnamen, keine Behandlung, keine Notiz, kein Preis, nie Gesundheitsdaten.
/// Die API rechnet alles aus (Dienste, freie Räume, nächste Termine); die Tafel zeichnet nur.
/// </summary>
public sealed record DisplayBoard(
    string LocationName,
    string TimeZone,
    DateTimeOffset Now,
    DateTimeOffset DayStart,
    DateTimeOffset DayEnd,
    IReadOnlyList<DisplayPerson> People,
    IReadOnlyList<DisplayRoom> Rooms,
    IReadOnlyList<DisplayNextAppointment> Next);

public enum DisplayPersonState
{
    OffShift,
    Free,
    InTreatment,
}

/// <summary>Tagesleiste einer Person: Dienst und Termine als Zeiträume, ohne Gast und Behandlung.</summary>
public sealed record DisplayPerson(
    string Name,
    string Color,
    DisplayPersonState State,
    IReadOnlyList<DisplaySpan> Shifts,
    IReadOnlyList<DisplaySpan> Appointments);

public sealed record DisplaySpan(DateTimeOffset Start, DateTimeOffset End);

/// <summary>Ein Raum jetzt: frei bis <paramref name="Until"/> (leer: bis zum Ende des Tages) bzw. belegt bis <paramref name="Until"/>.</summary>
public sealed record DisplayRoom(string Name, bool Free, DateTimeOffset? Until);

/// <summary>Nächster Termin mit den Initialen des Gastes („A. H.“).</summary>
public sealed record DisplayNextAppointment(DateTimeOffset Start, string Initials, string Person, string Room);

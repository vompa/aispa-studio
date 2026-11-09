namespace AispaStudio.Contracts;

/// <summary>Über welchen Weg eine Änderung kam (Änderungsverlauf, ADR 0082). Ohne Anfrage des Teams oder des Kundenbereichs gilt <see cref="Job"/>.</summary>
public enum ChangeChannel
{
    Team,
    Online,
    Sms,
    Mail,
    WhatsApp,
    Phone,
    Treatwell,
    Ai,
    Job,
    Sync,
    StaffApp,
}

/// <summary>Ein Eintrag im Verlauf eines Datensatzes: Zeitpunkt und ein Satz für Menschen („Verschoben von Lena: 14:00 → 15:30“).</summary>
public sealed record HistoryEntry(DateTimeOffset At, string Text);

/// <summary>Art eines Eintrags im Verlauf eines Standorts (Seite „Verlauf“). Zahlungen kommen aus der Kassa, alles andere aus dem Änderungsverlauf (ADR 0082).</summary>
public enum ActivityKind
{
    Booked,
    Moved,
    Cancelled,
    Restored,
    StatusChanged,
    AppointmentChanged,
    Paid,
    Deposit,
    ReceiptVoided,
    CustomerCreated,
    CustomerChanged,
    TreatmentChanged,
    RosterChanged,
    AccessChanged,
    CompanyChanged,
}

/// <summary>Über welchen Weg ein Eintrag entstand, für den Filter der Seite: Rezeption (Team, Mitarbeiter-App), online vom Gast, Terminannahme über einen Kanal, Hintergrund (Job, Abgleich).</summary>
public enum ActivityChannel
{
    Reception,
    Online,
    Assistant,
    Background,
}

/// <summary>Ein geänderter Wert: Bezeichnung, vorher und nachher, schon für Menschen formatiert („Preis“, „79,00 €“, „89,00 €“).</summary>
public sealed record ActivityValue(string Label, string? Old, string? New);

/// <summary>
/// Ein Eintrag im Verlauf eines Standorts. <c>Seq</c> ist der Seitenzeiger (Verlaufszeile oder negative Pseudo-Nummer eines Belegs), <c>Text</c> der Satz wie im Verlauf
/// des Datensatzes, <c>Actor</c> die Person zur Zeit des Lesens; <c>LateCancellation</c> nur bei Stornos: innerhalb der Stornofrist des Standorts abgesagt;
/// <c>ReceiptId</c> nur bei Zahlungen: der Beleg für den Sprung in die Kassa.
/// </summary>
public sealed record ActivityEntry(
    long Seq,
    DateTimeOffset At,
    ActivityKind Kind,
    string Text,
    string? Actor,
    ActivityChannel Channel,
    Guid? CustomerId,
    string? CustomerName,
    Guid? AppointmentId,
    Guid? TreatmentId,
    Guid? EmployeeId,
    IReadOnlyList<ActivityValue> Values,
    bool? LateCancellation,
    Guid? ReceiptId = null);

/// <summary>Eine Person, die im Zeitraum etwas getan hat (Benutzer-Id und Anzeigename zur Zeit des Lesens), für den Filter „Person“ der Seite.</summary>
public sealed record ActivityActor(string Id, string Name);

/// <summary>
/// Eine Seite des Verlaufs, neueste zuerst; <c>NextBefore</c> ist der Zeiger für die nächste Seite, <c>null</c> am Ende. <c>Actors</c> nennt die Personen, die im Zeitraum
/// und unter den übrigen Filtern vorkommen (unabhängig vom Filter „Person“), nach Namen sortiert.
/// </summary>
public sealed record ActivityPage(IReadOnlyList<ActivityEntry> Items, long? NextBefore, IReadOnlyList<ActivityActor>? Actors = null);

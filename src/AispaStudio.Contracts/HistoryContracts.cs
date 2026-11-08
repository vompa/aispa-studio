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

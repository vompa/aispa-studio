namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Die Sprache einer Nachricht an einen Gast, an einer einzigen Stelle (ADR 0073, Ä52): Erinnerungen, Bestätigungen, Absagen, Rückzahlungen und die Antworten der
/// Terminannahme schreiben in einer der vier Sprachen des Kundenbereichs (de, en, fr, it). Gibt es für die Sprache eines Gastes keine Vorlage (z. B. Spanisch) oder keine Angabe, geht die
/// Nachricht <b>überall</b> auf Deutsch: Österreich ist der erste Markt, und Deutsch ist die Sprache, die im Team jede Person lesen kann.
/// </summary>
internal static class GuestLanguage
{
    /// <summary>Die Sprache, auf die jede ohne Vorlage zurückfällt.</summary>
    public const string Fallback = "de";

    /// <summary>Die Sprachen, für die es Vorlagen gibt; weitere Sprachen kommen über diese Stelle dazu.</summary>
    public static readonly IReadOnlyList<string> Supported = [Fallback, "en", "fr", "it"];

    /// <summary>Kleingeschriebenes Kürzel einer unterstützten Sprache („EN-gb“ ergibt „en“); leer, unbekannt und nicht unterstützt ergeben <see cref="Fallback"/>.</summary>
    public static string Normalize(string? code)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        c = c.Length >= 2 ? c[..2] : c;
        return Supported.Contains(c) ? c : Fallback;
    }

    /// <summary>Die Sprache der Nachrichten an diesen Gast: seine Sprache aus den Kundendaten, sonst <see cref="Fallback"/>.</summary>
    public static string Resolve(Customer customer) => Normalize(customer.Language);
}

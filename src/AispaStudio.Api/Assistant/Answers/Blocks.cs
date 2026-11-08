using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>Baut die Antwortblöcke, damit Quelle, Hinweis auf fehlende Angaben und fehlende Berechtigung überall gleich lauten.</summary>
internal static class Blocks
{
    public const int MaxRows = 10;

    /// <summary>Höchstzahl der Zeilen einer Listen-Antwort (Ä316); die Oberfläche zeigt zehn und klappt den Rest mit „Alle zeigen“ auf.</summary>
    public const int MaxItems = 50;

    private static readonly IReadOnlyList<AssistantFollowUp> None = [];

    public static AssistantBlock Answer(BusinessTopic topic, string text, string source, AssistantTable? table = null, AssistantLink? link = null,
        string? copy = null, string? period = null, IReadOnlyList<AssistantFollowUp>? followUps = null, IReadOnlyList<AssistantItem>? items = null) =>
        new(AssistantBlockKind.Answer, topic.ToString(), text, table, source, link, copy, period, followUps ?? None, Items: items);

    /// <summary>
    /// Listen-Antwort ohne Betriebsthema (Tagesüberblick, freie Zeiten, Kundensuche; <paramref name="topic"/> ist die Art der Frage): <paramref name="text"/> ist nur der
    /// Kopfsatz mit der Zahl, die Einträge stehen einmal in <paramref name="items"/> (Ä316).
    /// </summary>
    public static AssistantBlock List(string topic, string text, string source, IReadOnlyList<AssistantItem> items) =>
        new(AssistantBlockKind.Answer, topic, text, null, source, null, null, null, None, Items: items);

    /// <summary>Das Feld ist im Spa nicht eingetragen. <paramref name="what"/> sagt, was fehlt; ohne <paramref name="link"/> (fehlendes Recht) wen man fragt.</summary>
    public static AssistantBlock NotStored(BusinessTopic topic, string what, string source, AssistantLink? link) =>
        new(AssistantBlockKind.NotStored, topic.ToString(),
            link is null ? $"Dazu ist nichts hinterlegt: {what}. Eintragen kann das die Standortleitung." : $"Dazu ist nichts hinterlegt: {what}.",
            null, source, link, null, null, None);

    /// <summary>Es gibt nichts zu der Frage (kein Gast, keine Behandlung, keine Person dieses Namens).</summary>
    public static AssistantBlock NotFound(BusinessTopic topic, string text, string source, AssistantTable? table = null, string? period = null) =>
        new(AssistantBlockKind.NotFound, topic.ToString(), text, table, source, null, null, period, None);

    /// <summary>Dafür fehlt die Berechtigung; <paramref name="offer"/> bietet, wenn es sie gibt, die Auswertung ohne Beträge oder ohne Kundendaten an.</summary>
    public static AssistantBlock Denied(BusinessTopic topic, string reason, AssistantFollowUp? offer = null) => Denied(topic.ToString(), reason, offer);

    /// <summary>Wie oben, für eine Frage, die kein Betriebsthema ist (<paramref name="topic"/> ist dann die Art der Frage, z. B. <c>FreeSlots</c>).</summary>
    public static AssistantBlock Denied(string topic, string reason, AssistantFollowUp? offer = null) =>
        new(AssistantBlockKind.Denied, topic, $"Dafür fehlt die Berechtigung. {reason}", null, "Berechtigungen dieses Zugangs", null, null, null,
            offer is null ? None : [offer]);

    /// <summary>
    /// Fragen zu Kalender, Team, Gästen und Betrieb braucht den Kalender (Recht „Kalender ansehen“). Das Backoffice hat ihn nicht (ADR 0070) und bekommt vom Assistenten
    /// nur Auswertungen. Gibt den Block der Ablehnung zurück, oder <c>null</c>, wenn die Person fragen darf.
    /// </summary>
    public static AssistantBlock? RequireCalendar(BusinessTopic topic, AnswerContext context) =>
        context.Has(AccessRight.ViewCalendar)
            ? null
            : Denied(topic, "Dieser Zugang nutzt den Assistenten für Auswertungen; Kalender, Team und Gäste sieht er nicht.");

    public static AssistantTable Table(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows) =>
        new(columns, [.. rows.Take(MaxRows)]);
}

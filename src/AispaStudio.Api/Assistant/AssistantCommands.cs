using System.Text.RegularExpressions;

namespace AispaStudio.Api.Assistant;

/// <summary>Ein Auftrag an den Assistenten (Ä319): er öffnet die passende Maske vorausgefüllt, gespeichert wird erst per Knopf der Person.</summary>
public enum AssistantCommand
{
    Book,
    Move,
    Cancel,
    SendQuestionnaire,
    Offer,
    OpenBill,
}

/// <summary>
/// Erkennt Aufträge an einer festen Regel: ein Befehl am Satzanfang („buche …“, „verschiebe …“, „sag … ab“, „schick … den Bogen“, „biete … an“, „offene Rechnung …“).
/// So bleibt „Wie verschiebe ich einen Termin?“ eine Frage an die Hilfe und „Frau Moser sagt ab – fällt eine Gebühr an?“ eine Frage nach der Gebühr; die Antwort ist mit
/// und ohne KI-Schlüssel dieselbe.
/// </summary>
internal static partial class AssistantCommands
{
    public static AssistantCommand? Detect(string question)
    {
        var text = question.Trim().ToLowerInvariant();
        return text switch
        {
            _ when Book().IsMatch(text) => AssistantCommand.Book,
            _ when Move().IsMatch(text) => AssistantCommand.Move,
            _ when Cancel().IsMatch(text) => AssistantCommand.Cancel,
            _ when Questionnaire().IsMatch(text) => AssistantCommand.SendQuestionnaire,
            _ when Offer().IsMatch(text) => AssistantCommand.Offer,
            _ when OpenBill().IsMatch(text) => AssistantCommand.OpenBill,
            _ => null,
        };
    }

    /// <summary>
    /// Der Gast des Auftrags: was nach Befehl, Füllwörtern, Zeitangaben und den Wörtern aus Behandlungs- und Personennamen (<paramref name="known"/>) übrig bleibt;
    /// <c>null</c>, wenn nichts übrig bleibt. Die Anrede („Frau“, „Herr“) bleibt stehen, die Gastsuche nutzt sie.
    /// </summary>
    public static string? GuestName(string question, IEnumerable<string> known)
    {
        var skip = new HashSet<string>(StopWords, StringComparer.Ordinal);
        foreach (var name in known)
        {
            skip.UnionWith(Word().Matches(name.ToLowerInvariant()).Select(m => m.Value));
        }

        var words = Word().Matches(question).Select(m => m.Value).Where(w => !skip.Contains(w.ToLowerInvariant())).ToList();
        return words.Count == 0 ? null : string.Join(' ', words);
    }

    private static readonly string[] StopWords =
    [
        "buche", "buch", "buchen", "verschiebe", "verschieb", "verschieben", "sag", "sage", "ab", "absagen", "storniere", "stornier", "stornieren", "schick", "schicke",
        "sende", "send", "senden", "biete", "biet", "bieten", "an", "anbieten", "offene", "offenen", "offener", "rechnung", "rechnungen", "termin", "termine", "bogen",
        "anamnese", "anamnesebogen", "fragebogen", "den", "die", "das", "der", "dem", "des", "von", "vom", "für", "mit", "bei", "auf", "um", "am", "im", "in", "zu", "zum",
        "zur", "nach", "ein", "eine", "einen", "bitte", "noch", "mal", "neuen", "neu", "uhr", "heute", "morgen", "übermorgen", "nächste", "nächsten", "nächster",
        "kommende", "kommenden", "woche", "montag", "dienstag", "mittwoch", "donnerstag", "freitag", "samstag", "sonntag", "vormittag", "vormittags", "nachmittag",
        "nachmittags", "abend", "abends", "früh", "mittag", "mittags", "jänner", "januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september",
        "oktober", "november", "dezember", "gast", "kunde", "kundin", "warteliste", "platz", "frei", "freien", "freie", "zeit", "ihr", "ihm", "ihn", "sie", "er",
        "es", "und", "oder", "jetzt", "gleich", "sofort", "statt", "wieder",
    ];

    [GeneratedRegex(@"^\W*(buche|buch)\b")]
    private static partial Regex Book();

    [GeneratedRegex(@"^\W*verschieb\w*\b")]
    private static partial Regex Move();

    [GeneratedRegex(@"^\W*(sag\b.*\bab\b|stornier\w*\b)")]
    private static partial Regex Cancel();

    [GeneratedRegex(@"^\W*(schick\w*|sende|send)\b.*\b(bogen|\w*anamnese\w*|fragebogen)\b")]
    private static partial Regex Questionnaire();

    [GeneratedRegex(@"^\W*(biete?\b.*\ban\b|anbieten\b)")]
    private static partial Regex Offer();

    [GeneratedRegex(@"^\W*offene\w*\s+rechnung\w*\b")]
    private static partial Regex OpenBill();

    [GeneratedRegex(@"[\p{L}][\p{L}'-]+")]
    private static partial Regex Word();
}

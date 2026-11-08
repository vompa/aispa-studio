using System.Text.RegularExpressions;
using AispaStudio.Api.BusinessInfo;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Findet Einträge der Betriebsinformationen zu den Wörtern einer Frage (ADR 0074): die Wörter ohne Füllwörter („wer“, „zuständig“, „Nummer“) müssen in Bezeichnung,
/// Firma, Ansprechperson, Zuständigkeit oder Kategorie vorkommen, als Wort, als Wortanfang („Heizung“ in „Heizungsnotdienst“) oder mit gleichem langem Anfang
/// („Steuerberater“ zu „Steuerberatung“). Steht eine Kategorie fest, sucht es nur dort.
/// </summary>
internal static partial class BusinessContactSearch
{
    private const int MinimumWord = 4;

    private static readonly HashSet<string> Filler = new(
        (
            "wer wen wem wie was wo wann woher ist sind war unser unsere unseren unserem unserer unseres uns wir euer eure ihr ich mir mich mein meine fur die der das den dem des "
            + "ein eine einen einem einer eines und oder auch bei an am im in um von vom zu zum zur mit nach auf aus aber nicht kein keine zustandig zustandige zustandigkeit "
            + "kummert kummern sich nummer nummern telefon telefonnummer rufnummer kontakt kontakte kontaktdaten ansprechpartner ansprechpartnerin ansprechperson mail email "
            + "adresse rufe rufen ruf anrufen anruf erreiche erreichen erreicht gibt haben hat hinterlegt bitte mal noch gerade jetzt heute brauche brauch muss soll kann "
            + "kannst konnen sage zeige welche welcher welches welchen name namen dazu dafur damit wegen"
        ).Split(' ').Select(NameMatch.Key));

    /// <summary>Die Wörter der Frage, die etwas suchen: Schlüssel zum Vergleichen und die Schreibweise zum Anzeigen.</summary>
    public static List<(string Key, string Display)> Words(string text) =>
    [
        .. WordPattern().Matches(text)
            .Select(m => (Key: NameMatch.Key(m.Value), Display: m.Value))
            .Where(w => w.Key.Length >= MinimumWord && !Filler.Contains(w.Key))
            .DistinctBy(w => w.Key),
    ];

    /// <summary>
    /// Die gefundenen Einträge, die besten zuerst (mehr getroffene Wörter, dann Kategorie und Bezeichnung). Nennt die Frage Wörter und trifft keines: keine.
    /// Nennt sie nur eine Kategorie: alle Einträge der Kategorie.
    /// </summary>
    public static List<BusinessContact> Find(IEnumerable<BusinessContact> all, BusinessContactCategory? category, IReadOnlyList<(string Key, string Display)> words)
    {
        var pool = (category is { } wanted ? all.Where(c => c.Category == wanted) : all).ToList();
        if (words.Count == 0)
        {
            return pool;
        }

        var scored = pool.Select(c => (Contact: c, Score: Score(c, words))).Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Contact.Category).ThenBy(x => x.Contact.Designation, StringComparer.Ordinal)
            .Select(x => x.Contact).ToList();
        return scored;
    }

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'-]*")]
    private static partial Regex WordPattern();

    private static int Score(BusinessContact c, IReadOnlyList<(string Key, string Display)> words)
    {
        var haystack = NameMatch.KeyWords(string.Join(' ', new[] { c.Designation, c.Company, c.ContactPerson, c.Responsibility, BusinessContactLabels.Keywords(c.Category) }
            .Where(v => !string.IsNullOrWhiteSpace(v))));
        return words.Count(w => haystack.Any(h => Matches(w.Key, h)));
    }

    /// <summary>Gleiches Wort, ein Wort als Anfang des anderen oder ein gemeinsamer Anfang von mindestens fünf Zeichen und drei Vierteln des kürzeren Wortes.</summary>
    private static bool Matches(string word, string other)
    {
        if (word == other)
        {
            return true;
        }

        var (shorter, longer) = word.Length <= other.Length ? (word, other) : (other, word);
        if (shorter.Length >= MinimumWord && longer.StartsWith(shorter, StringComparison.Ordinal))
        {
            return true;
        }

        var common = 0;
        while (common < shorter.Length && shorter[common] == longer[common])
        {
            common++;
        }

        return common >= 5 && common * 4 >= shorter.Length * 3;
    }
}

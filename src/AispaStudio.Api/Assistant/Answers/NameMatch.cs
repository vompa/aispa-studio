using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>Namen aus einer Frage mit Namen im Spa vergleichen: ohne Groß- und Kleinschreibung, Umlaute und Satzzeichen („Thai-Massage“ = „Thai Massage“).</summary>
internal static partial class NameMatch
{
    /// <summary>Kleinbuchstaben, Umlaute und Akzente aufgelöst, alles außer Buchstaben und Ziffern entfernt.</summary>
    public static string Key(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Replace("ß", "ss", StringComparison.Ordinal);
    }

    /// <summary>
    /// Der Name steht im Text, als ganzes Wort oder ganze Wörter und ohne auf Trennzeichen zu achten („Thai-Massage“ = „Thai Massage“); „Massagen“ trifft „Massage“,
    /// aber „Thai-Massage“ trifft nicht „Massage“. Namen unter drei Zeichen zählen nie.
    /// </summary>
    public static bool Mentions(string text, string name) => Take(KeyWords(text), name);

    /// <summary>Die Wörter des Textes als Schlüssel (siehe <see cref="Key"/>), zum Abarbeiten mit <see cref="Take"/>.</summary>
    public static List<string> KeyWords(string text) => [.. Words().Matches(text).Select(m => Key(m.Value)).Where(w => w.Length > 0)];

    /// <summary>
    /// Ob der Name in den Wörtern steht; wenn ja, nimmt es die gefundenen Wörter heraus. So zählt ein kürzerer Name („Massage“) nicht noch einmal in einem längeren
    /// („Hot Stone Massage“), steht aber trotzdem für sich, wenn er auch allein im Satz vorkommt.
    /// </summary>
    public static bool Take(List<string> words, string name)
    {
        var key = Key(name);
        if (key.Length < 3)
        {
            return false;
        }

        for (var start = 0; start < words.Count; start++)
        {
            var joined = "";
            for (var end = start; end < Math.Min(words.Count, start + 5) && joined.Length <= key.Length + 2; end++)
            {
                joined += words[end];
                if (joined == key || (joined.StartsWith(key, StringComparison.Ordinal) && Suffixes.Contains(joined[key.Length..])))
                {
                    words.RemoveRange(start, end - start + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly HashSet<string> Suffixes = ["n", "en", "s", "e", "es"];

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'-]*")]
    private static partial Regex Words();

    /// <summary>Ab so vielen Buchstaben verzeiht der Vergleich einen Tippfehler (Ä315); kürzere Namen müssen genau stimmen („Lena“ ist nicht „Lina“).</summary>
    private const int MinTypoLength = 5;

    /// <summary>
    /// Namensteil einer Frage: die Namenswörter (höchstens zwei, Schreibung wie getippt) und die Anrede, falls die Frage eine nennt. Anrede (Herr, Hr., Frau, Fr.),
    /// Titel (Mag., Dr. …) und Füllwörter gehören nicht zum Namen; „der“/„die“ und „Hr.“/„Fr.“ zählen als Anrede nur direkt vor einem Namen („die Moser“), nicht vor
    /// einem Füllwort („der Gast“, „am Fr. einen Termin“).
    /// </summary>
    public static NameQuery ParseName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new([], null);
        }

        var tokens = NameToken().Matches(text).Select(m => m.Value.TrimEnd('.')).Where(t => t.Length > 0).ToList();
        bool IsName(int i) => i < tokens.Count && !IsNoName(tokens[i].ToLowerInvariant());
        CustomerSalutation? spoken = null;
        CustomerSalutation? article = null;
        var words = new List<string>();
        for (var i = 0; i < tokens.Count && words.Count < 2; i++)
        {
            var lower = tokens[i].ToLowerInvariant();
            if (Salutations.TryGetValue(lower, out var salutation) || (ShortSalutations.TryGetValue(lower, out salutation) && IsName(i + 1)))
            {
                spoken ??= salutation;
            }
            else if (Articles.TryGetValue(lower, out salutation) && IsName(i + 1))
            {
                article ??= salutation;
            }
            else if (!IsNoName(lower))
            {
                words.Add(tokens[i]);
            }
        }

        return new(words, spoken ?? article);
    }

    /// <summary>
    /// Wie gut die Namenswörter zu den Namen passen: 0 genau, 1 mit einem Tippfehler (ein Buchstabe fehlt, ist zu viel, ist falsch oder zwei Nachbarn sind
    /// vertauscht, nur bei Wörtern ab <see cref="MinTypoLength"/> Buchstaben), <c>null</c> nicht. Jedes Wort muss einen der Namen oder ein Wort davon treffen
    /// („Huber“ trifft „Lena Huber“); Schreibung und Umlaute zählen nicht (<see cref="Key"/>).
    /// </summary>
    public static int? Fit(IReadOnlyList<string> words, params string?[] names)
    {
        var keys = words.Select(Key).Where(k => k.Length > 0).ToList();
        var nameKeys = names.Where(n => !string.IsNullOrWhiteSpace(n))
            .SelectMany(n => n!.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Select(Key).Append(Key(n!)))
            .Where(k => k.Length > 0)
            .ToHashSet();
        if (keys.Count == 0 || nameKeys.Count == 0)
        {
            return null;
        }

        var worst = 0;
        foreach (var key in keys)
        {
            int? fit = nameKeys.Contains(key) ? 0 : key.Length >= MinTypoLength && nameKeys.Any(n => OneEdit(key, n)) ? 1 : null;
            if (fit is not { } value)
            {
                return null;
            }

            worst = Math.Max(worst, value);
        }

        return worst;
    }

    /// <summary>Die am besten passenden Einträge: genaue Treffer, und nur ohne sie die mit einem Tippfehler (sonst bekäme „Steiner“ auch „Steimer“ angeboten).</summary>
    public static List<T> Best<T>(IEnumerable<T> items, Func<T, int?> fit)
    {
        var scored = items.Select(item => (Item: item, Fit: fit(item))).Where(x => x.Fit is not null).ToList();
        if (scored.Count == 0)
        {
            return [];
        }

        var best = scored.Min(x => x.Fit!.Value);
        return [.. scored.Where(x => x.Fit == best).Select(x => x.Item)];
    }

    /// <summary>Nur die Einträge mit der genannten Anrede, wenn es solche gibt; sonst alle (eine fehlende oder andere Anrede in der Kartei verhindert keine Antwort).</summary>
    public static List<T> BySalutation<T>(List<T> items, CustomerSalutation? wanted, Func<T, CustomerSalutation> salutation)
    {
        if (wanted is not { } value)
        {
            return items;
        }

        var matching = items.Where(item => salutation(item) == value).ToList();
        return matching.Count > 0 ? matching : items;
    }

    /// <summary>Genau ein Tippfehler: ein Buchstabe falsch, fehlt oder ist zu viel, oder zwei Nachbarn vertauscht. Gleiche Wörter zählen nicht.</summary>
    private static bool OneEdit(string a, string b)
    {
        if (a == b || Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        if (a.Length == b.Length)
        {
            var diffs = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
            return diffs.Count == 1
                || (diffs.Count == 2 && diffs[1] == diffs[0] + 1 && a[diffs[0]] == b[diffs[1]] && a[diffs[1]] == b[diffs[0]]);
        }

        var (longer, shorter) = a.Length > b.Length ? (a, b) : (b, a);
        var same = 0;
        while (same < shorter.Length && shorter[same] == longer[same])
        {
            same++;
        }

        return shorter.AsSpan(same).SequenceEqual(longer.AsSpan(same + 1));
    }

    private static bool IsNoName(string lower) =>
        Salutations.ContainsKey(lower) || ShortSalutations.ContainsKey(lower) || Titles.Contains(lower) || BusinessIntents.Stop.Contains(lower) || Fillers.Contains(lower);

    private static readonly Dictionary<string, CustomerSalutation> Salutations = new()
    {
        ["herr"] = CustomerSalutation.Male,
        ["herrn"] = CustomerSalutation.Male,
        ["frau"] = CustomerSalutation.Female,
    };

    private static readonly Dictionary<string, CustomerSalutation> ShortSalutations = new()
    {
        ["hr"] = CustomerSalutation.Male,
        ["fr"] = CustomerSalutation.Female,
    };

    private static readonly Dictionary<string, CustomerSalutation> Articles = new()
    {
        ["der"] = CustomerSalutation.Male,
        ["die"] = CustomerSalutation.Female,
    };

    private static readonly HashSet<string> Titles = ["dr", "mag", "ing", "di", "dipl", "prof", "univ", "mmag", "dkfm", "msc", "mba", "bsc", "phd", "ba", "ma"];

    /// <summary>Wörter, die in Gastfragen neben dem Namen stehen und nie selbst ein Name sind (ergänzt die Füllwörter von <see cref="BusinessIntents"/>).</summary>
    private static readonly HashSet<string> Fillers =
    [
        "sie", "er", "ihn", "ihm", "ihr", "sein", "seine", "seinen", "seinem", "seiner", "seines", "ihre", "ihren", "ihrem", "ihrer", "ihres", "zur", "zum", "ins",
        "mal", "kommen", "bitte", "terminen", "gast", "da",
    ];

    [GeneratedRegex(@"[\p{L}][\p{L}'.-]*")]
    private static partial Regex NameToken();
}

/// <summary>Namensteil einer Frage (<see cref="NameMatch.ParseName"/>).</summary>
internal sealed record NameQuery(IReadOnlyList<string> Words, CustomerSalutation? Salutation)
{
    public bool IsEmpty => Words.Count == 0;
}

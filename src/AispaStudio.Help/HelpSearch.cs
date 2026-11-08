using System.Text.RegularExpressions;

namespace AispaStudio.Help;

/// <summary>Wer fragt und von wo: Oberfläche (<c>windows-app</c>, <c>verwaltung</c>, <c>handy</c>), Rolle und aktuelle Seite.</summary>
public sealed record HelpContext(string? Surface = null, string? Role = null, string? Page = null);

/// <summary>Ein Treffer: Thema und, wenn ein Abschnitt besser passt, dessen Anker.</summary>
public sealed record HelpHit(HelpTopic Topic, string? Section, string Title, string Excerpt, double Score);

/// <summary>
/// Suche im Handbuch ohne KI und ohne Server: Wörter der Frage gegen Titel, Stichwörter, Überschriften und Text, mit
/// einfacher Wortstamm- und Wortteil-Erkennung („verschiebe“ findet „verschieben“, „Termin“ findet „Terminverschiebung“).
/// Abläufe stehen vor Einstellungen; Rolle, Oberfläche und Seite der fragenden Person heben passende Themen an.
/// Gleiche Rangfolge wie <c>web/src/help/search.ts</c>.
/// </summary>
public static partial class HelpSearch
{
    private const double MinScore = 4;

    private static readonly HashSet<string> StopWords =
    [
        "wie", "mache", "machen", "macht", "man", "ich", "kann", "koennen", "kannst", "einen", "eine", "einer", "ein", "der", "die", "das",
        "den", "dem", "des", "und", "oder", "mit", "fuer", "im", "in", "am", "an", "auf", "zu", "zum", "zur", "was", "wo", "wann", "wer",
        "bitte", "hilfe", "anleitung", "geht", "gehe", "mich", "mir", "wir", "sie", "es", "ist", "sind", "soll", "sollte", "muss", "moechte",
        "will", "hier", "dort", "da", "denn", "jetzt", "noch", "schon", "auch", "nicht", "kein", "keine", "von", "vom", "bei", "nach",
        "funktioniert", "finde", "finden", "erklaer", "erklaere", "zeig", "zeige", "how", "do", "to", "the", "can", "where", "what", "is",
        "my", "a", "of", "for", "on", "it", "this", "that", "i",
    ];

    public static IReadOnlyList<HelpHit> Search(IEnumerable<HelpTopic> topics, string query, HelpContext? context = null, int max = 5)
    {
        var words = Words(query).Where(w => !StopWords.Contains(w)).Distinct().ToList();
        if (words.Count == 0)
        {
            return [];
        }

        context ??= new();
        return [.. topics
            .Select(t => Score(t, words, context))
            .Where(h => h is not null && h.Score >= MinScore)
            .OrderByDescending(h => h!.Score)
            .ThenBy(h => h!.Title, StringComparer.Ordinal)
            .Take(max)
            .Select(h => h!)];
    }

    private static HelpHit? Score(HelpTopic topic, List<string> words, HelpContext context)
    {
        var title = Words(topic.Title).ToList();
        var keywords = topic.Meta.Keywords.SelectMany(Words).ToList();
        var sections = Sections(topic);
        var allHeadings = sections.SelectMany(s => s.Heading).ToList();
        var allBody = sections.SelectMany(s => s.Body).ToList();

        double score = 0;
        var matched = 0;
        foreach (var word in words)
        {
            var best = Math.Max(Math.Max(6 * Best(word, title), 5 * Best(word, keywords)), Math.Max(3 * Best(word, allHeadings), 1 * Best(word, allBody)));
            score += best;
            matched += best > 0 ? 1 : 0;
        }

        if (matched == 0)
        {
            return null;
        }

        score *= 0.5 + (0.5 * matched / words.Count);
        score *= topic.Meta.Kind switch
        {
            HelpKind.Task => 1.3,
            HelpKind.Page => 1.0,
            _ => 0.9,
        };
        if (context.Role is { } role && (topic.Meta.Roles.Count == 0 || topic.Meta.Roles.Contains(role)))
        {
            score *= 1.15;
        }

        if (context.Surface is { } surface && topic.Meta.Surfaces.Contains(surface))
        {
            score *= 1.15;
        }

        if (context.Page is { } page && string.Equals(topic.Meta.Page, page, StringComparison.OrdinalIgnoreCase))
        {
            score *= 1.2;
        }

        // Der Abschnitt, dessen Überschrift die Frage trifft; sonst das ganze Thema.
        var section = sections.Where(s => s.Id is not null)
            .Select(s => (s, Value: words.Sum(w => (3 * Best(w, s.Heading)) + Best(w, s.Body)), Heading: words.Sum(w => Best(w, s.Heading))))
            .Where(x => x.Heading > 0)
            .OrderByDescending(x => x.Value)
            .Select(x => x.s)
            .FirstOrDefault();
        var excerpt = section?.Excerpt is { Length: > 0 } e ? e : topic.Summary;
        return new HelpHit(topic, section?.Id, section is null ? topic.Title : $"{topic.Title}: {section.Title}", excerpt, score);
    }

    /// <summary>Wie gut ein Wort der Frage zu einem der Wörter passt: gleich 1, gleicher Stamm 0,8, als Wortteil 0,6.</summary>
    private static double Best(string word, List<string> candidates)
    {
        double best = 0;
        foreach (var c in candidates)
        {
            var value = c == word ? 1
                : Stem(c) == Stem(word) && Stem(word).Length >= 4 ? 0.8
                : word.Length >= 5 && c.Contains(Stem(word), StringComparison.Ordinal) ? 0.6
                : c.Length >= 5 && word.Contains(Stem(c), StringComparison.Ordinal) ? 0.6
                : 0;
            if (value > best)
            {
                best = value;
                if (best >= 1)
                {
                    break;
                }
            }
        }

        return best;
    }

    /// <summary>Grobe Grundform: übliche deutsche Endungen ab, aber nie kürzer als vier Buchstaben.</summary>
    internal static string Stem(string word)
    {
        foreach (var ending in (string[])["ungen", "ung", "en", "er", "es", "e", "n", "s", "t"])
        {
            if (word.Length - ending.Length >= 4 && word.EndsWith(ending, StringComparison.Ordinal))
            {
                return word[..^ending.Length];
            }
        }

        return word;
    }

    internal static IEnumerable<string> Words(string text) =>
        WordPattern().Matches(HelpMarkdown.Fold(text)).Select(m => m.Value).Where(w => w.Length >= 2);

    private sealed record Part(string? Id, string Title, List<string> Heading, List<string> Body, string Excerpt);

    /// <summary>Das Thema in Abschnitte zerlegt; der Teil vor der ersten Überschrift hat keine Kennung.</summary>
    private static List<Part> Sections(HelpTopic topic)
    {
        List<Part> parts = [new(null, topic.Title, [], [], "")];
        foreach (var block in topic.Blocks)
        {
            var current = parts[^1];
            switch (block)
            {
                case HelpHeading h:
                    parts.Add(new(h.Id, h.Text, [.. Words(h.Text)], [], ""));
                    break;
                case HelpParagraph p:
                    var text = HelpMarkdown.Plain(p.Spans);
                    current.Body.AddRange(Words(text));
                    if (current.Excerpt.Length == 0)
                    {
                        parts[^1] = current with { Excerpt = text };
                    }

                    break;
                case HelpList l:
                    foreach (var item in l.Items)
                    {
                        current.Body.AddRange(Words(HelpMarkdown.Plain(item)));
                    }

                    if (current.Excerpt.Length == 0 && l.Items.Count > 0)
                    {
                        parts[^1] = current with { Excerpt = HelpMarkdown.Plain(l.Items[0]) };
                    }

                    break;
                case HelpImage i:
                    current.Body.AddRange(Words(i.Alt));
                    break;
            }
        }

        return parts;
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordPattern();
}

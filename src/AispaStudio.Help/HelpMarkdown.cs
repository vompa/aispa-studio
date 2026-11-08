using System.Text.RegularExpressions;

namespace AispaStudio.Help;

/// <summary>
/// Kleiner Markdown-Leser für die Hilfetexte, dieselbe Teilmenge wie <c>web/src/help/markdown.ts</c>: Kopfangaben zwischen
/// <c>---</c>, Überschriften mit optionalem Anker <c>## Titel {#anker}</c>, Absätze, Aufzählungen, nummerierte Schritte,
/// Bilder in eigener Zeile, dazu fett, Code und Links.
/// </summary>
public static partial class HelpMarkdown
{
    public static HelpTopic ToTopic(string id, string source)
    {
        var (meta, body) = SplitFrontMatter(source);
        var blocks = Parse(body);
        return blocks is [HelpHeading { Level: 1 } title, .. var rest] ? new(id, title.Text, rest, meta) : new(id, id, blocks, meta);
    }

    public static string Slug(string text) => NonWord().Replace(Fold(text), "-").Trim('-');

    /// <summary>Klein und ohne Umlaute, damit „Räume“ und „raeume“ gleich verglichen werden.</summary>
    public static string Fold(string text) => text.ToLowerInvariant().Replace("ä", "ae", StringComparison.Ordinal)
        .Replace("ö", "oe", StringComparison.Ordinal).Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal);

    public static string Plain(IEnumerable<HelpSpan> spans) => string.Concat(spans.Select(s => s.Text));

    /// <summary>Thema und Anker eines Verweises <c>hilfe:thema#anker</c>, sonst <c>null</c>.</summary>
    public static (string Topic, string? Section)? HelpLink(string? href)
    {
        if (href is null || !href.StartsWith("hilfe:", StringComparison.Ordinal))
        {
            return null;
        }

        var target = href["hilfe:".Length..];
        var hash = target.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? (target, null) : (target[..hash], target[(hash + 1)..]);
    }

    public static (HelpMeta Meta, string Body) SplitFrontMatter(string source)
    {
        var text = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (HelpMeta.Empty, text);
        }

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return (HelpMeta.Empty, text);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text[4..end].Split('\n'))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                values[line[..colon].Trim().ToLowerInvariant()] = line[(colon + 1)..].Trim();
            }
        }

        IReadOnlyList<string> List(string key) => values.TryGetValue(key, out var v)
            ? [.. v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)]
            : [];
        string? One(string key) => values.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

        var kind = One("art") switch
        {
            "ablauf" => HelpKind.Task,
            "seite" => HelpKind.Page,
            _ => HelpKind.Setting,
        };
        var meta = new HelpMeta(kind, One("bereich"), [.. List("oberflaeche").Select(s => s.ToLowerInvariant())],
            [.. List("rollen").Select(s => s.ToLowerInvariant())], One("seite"), List("stichwoerter"));
        var bodyStart = text.IndexOf('\n', end + 1);
        return (meta, bodyStart < 0 ? "" : text[(bodyStart + 1)..]);
    }

    public static List<HelpSpan> ParseInline(string text)
    {
        List<HelpSpan> spans = [];
        var last = 0;
        foreach (Match m in Inline().Matches(text))
        {
            if (m.Index > last)
            {
                spans.Add(new(text[last..m.Index]));
            }

            spans.Add(m.Groups[1].Success ? new(m.Groups[1].Value, Bold: true)
                : m.Groups[2].Success ? new(m.Groups[2].Value, Code: true)
                : new(m.Groups[3].Value, Href: m.Groups[4].Value));
            last = m.Index + m.Length;
        }

        if (last < text.Length)
        {
            spans.Add(new(text[last..]));
        }

        return spans;
    }

    public static List<HelpBlock> Parse(string source)
    {
        List<HelpBlock> blocks = [];
        List<string> paragraph = [];
        List<string>? items = null;
        var ordered = false;

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new HelpParagraph(ParseInline(string.Join(' ', paragraph))));
            }

            if (items is not null)
            {
                blocks.Add(new HelpList(ordered, [.. items.Select(i => (IReadOnlyList<HelpSpan>)ParseInline(i))]));
            }

            paragraph = [];
            items = null;
        }

        foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            var heading = Heading().Match(line);
            var image = Image().Match(line);
            var bullet = Bullet().Match(line);
            var numbered = Numbered().Match(line);
            if (line.Length == 0)
            {
                Flush();
            }
            else if (heading.Success)
            {
                Flush();
                var text = heading.Groups[2].Value;
                blocks.Add(new HelpHeading(heading.Groups[1].Length, text, heading.Groups[3].Success ? heading.Groups[3].Value : Slug(text)));
            }
            else if (image.Success)
            {
                Flush();
                blocks.Add(new HelpImage(image.Groups[2].Value, image.Groups[1].Value));
            }
            else if (bullet.Success || numbered.Success)
            {
                if (paragraph.Count > 0 || (items is not null && ordered != numbered.Success))
                {
                    Flush();
                }

                if (items is null)
                {
                    items = [];
                    ordered = numbered.Success;
                }

                items.Add((bullet.Success ? bullet : numbered).Groups[1].Value);
            }
            else if (items is not null && raw.StartsWith("  ", StringComparison.Ordinal))
            {
                items[^1] += " " + line;
            }
            else
            {
                if (items is not null)
                {
                    Flush();
                }

                paragraph.Add(line);
            }
        }

        Flush();
        return blocks;
    }

    [GeneratedRegex(@"^(#{1,4})\s+(.*?)(?:\s*\{#([a-z0-9-]+)\})?$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^!\[([^\]]*)\]\(images/((?:app|web)/[a-z0-9-]+)\.png\)$")]
    private static partial Regex Image();

    [GeneratedRegex(@"^[-*]\s+(.*)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^\d+\.\s+(.*)$")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"\*\*(.+?)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex Inline();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
}

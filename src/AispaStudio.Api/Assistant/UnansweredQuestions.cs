using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant;

/// <summary>
/// Bereinigt eine Frage ohne Antwort, bevor die Oberfläche sie für die Liste „Offene Fragen an die Hilfe“ auf dem PC ablegt (Slice 56):
/// E-Mail-Adressen, Telefonnummern und die Namen von Kunden des Spas werden zu Platzhaltern. Gespeichert wird hier nichts.
/// </summary>
internal static partial class UnansweredQuestions
{
    /// <summary>
    /// Ersetzt E-Mail-Adressen, Namen und Telefonnummern (ab sieben Ziffern, auch mit Leerzeichen, Punkt, Schrägstrich oder Bindestrich).
    /// Ein Wort ist ein Name, wenn es selbst, ohne angehängtes „s“ bzw. „'s“ (Genitiv) oder einer seiner Bindestrich-Teile in <paramref name="names"/>
    /// steht (klein geschrieben). Datum, ISO-Datum, Uhrzeit und Jahresbereiche bleiben stehen.
    /// </summary>
    public static string Clean(string question, IReadOnlyCollection<string> names)
    {
        var text = Email().Replace(question, "[E-Mail]");
        text = Word().Replace(text, m => Forms(m.Value).Any(names.Contains) ? "[Name]" : m.Value);

        // Datum und Uhrzeit vor der Telefonsuche verstecken, sonst wird „26.10.2026 14“ eine Nummer.
        var kept = new List<string>();
        text = DateOrTime().Replace(text, m =>
        {
            kept.Add(m.Value);
            return $"{kept.Count - 1}";
        });
        text = Phone().Replace(text, "[Telefon]");
        return Kept().Replace(text, m => kept[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    /// <summary>Wie <see cref="Clean(string, IReadOnlyCollection{string})"/> mit den Vor-, Nach- und Rufnamen der Kunden, die in der Frage vorkommen.</summary>
    public static async Task<string> CleanAsync(AispaDbContext db, string question, CancellationToken ct)
    {
        var forms = Word().Matches(question).SelectMany(m => Forms(m.Value)).Where(w => w.Length > 1).Distinct().ToList();
        // Die Wörter bestehen nur aus Buchstaben, Ziffern, Bindestrich und Apostroph: ILIKE vergleicht sie ohne Platzhalter; die Muster finden
        // das Wort auch als Teil eines Doppelnamens („Müller“ in „Müller-Lüdenscheidt“, „Lena“ in „Anna Lena“).
        var patterns = forms.SelectMany(w => new[] { w, $"{w}-%", $"%-{w}", $"{w} %", $"% {w}" }).ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => patterns.Any(p => EF.Functions.ILike(c.FirstName, p) || EF.Functions.ILike(c.LastName, p)
                || (c.PreferredName != null && EF.Functions.ILike(c.PreferredName, p))))
            .Select(c => new { c.FirstName, c.LastName, c.PreferredName })
            .ToListAsync(ct);
        var names = customers.SelectMany(c => new[] { c.FirstName, c.LastName, c.PreferredName }).OfType<string>().SelectMany(NameParts).ToHashSet();
        return Clean(question, names);
    }

    /// <summary>Ein gespeicherter Name klein geschrieben, ganz und in seinen Teilen an Bindestrich und Leerzeichen.</summary>
    internal static IEnumerable<string> NameParts(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        return lower.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Append(lower).Distinct();
    }

    /// <summary>Formen eines Worts der Frage: ganz, ohne Genitiv-Endung und seine Bindestrich-Teile, klein geschrieben.</summary>
    private static IEnumerable<string> Forms(string word)
    {
        var lower = word.ToLowerInvariant();
        foreach (var part in lower.Split('-').Append(lower).Distinct())
        {
            yield return part;
            if (part.EndsWith("'s", StringComparison.Ordinal))
            {
                yield return part[..^2];
            }
            else if (part.Length > 3 && part.EndsWith('s'))
            {
                yield return part[..^1];
            }
        }
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+?\d(?:[\s./-]{0,3}\d){6,}")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\b\d{1,2}\.\d{1,2}\.(?:\d{4}|\d{2})?|\b\d{4}-\d{2}-\d{2}\b|\b(?:19|20)\d{2}\s?[-–]\s?(?:19|20)\d{2}\b|\b\d{1,2}:\d{2}\b")]
    private static partial Regex DateOrTime();

    [GeneratedRegex("(\\d+)")]
    private static partial Regex Kept();

    [GeneratedRegex(@"[\p{L}\p{Nd}]+(?:['-][\p{L}\p{Nd}]+)*")]
    private static partial Regex Word();
}

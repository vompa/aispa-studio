using System.Globalization;
using System.Text.RegularExpressions;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>Liest Abo-Inhalte: ICS aus dem Abruf oder einer Datei, CSV nur aus einer Datei (Rückfallebene ohne Adresse).</summary>
internal static class FeedReader
{
    /// <summary>Größtes erlaubtes Abo oder Import in Bytes.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>Horizont für gespeicherte Tage: laufendes Jahr plus zwei (ADR 0075).</summary>
    public static (DateOnly From, DateOnly Until) Horizon(DateOnly today) => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year + 2, 12, 31));

    public static FeedParseResult Read(string content, DateOnly today, TimeZoneInfo zone)
    {
        var (from, until) = Horizon(today);
        return content.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)
            ? IcsParser.Parse(content, from, until, zone)
            : CsvCalendarParser.Parse(content, from, until);
    }
}

/// <summary>
/// CSV mit „Datum;Titel“ oder „Von;Bis;Titel“ (Trenner Strichpunkt, Komma oder Tabulator; Kopfzeile erlaubt). Datum als 26.10.2026 oder 2026-10-26.
/// </summary>
internal static class CsvCalendarParser
{
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"];

    public static FeedParseResult Parse(string text, DateOnly horizonFrom, DateOnly horizonUntil)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
            .Select(l => l.Trim().TrimStart('﻿')).Where(l => l.Length > 0).ToList();
        var warnings = new List<string>();
        if (lines.Count == 0)
        {
            return new([], warnings, false);
        }

        var delimiter = lines[0].Contains(';', StringComparison.Ordinal) ? ';' : lines[0].Contains('\t', StringComparison.Ordinal) ? '\t' : ',';
        var events = new List<FeedEvent>();
        var invalid = 0;
        var outside = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var cells = lines[i].Split(delimiter).Select(c => c.Trim().Trim('"').Trim()).ToArray();
            if (!TryDate(cells[0], out var from))
            {
                // Die Kopfzeile („Datum;Titel“) ist erlaubt, jede andere Zeile ohne Datum ein Fehler.
                if (i > 0)
                {
                    invalid++;
                }

                continue;
            }

            var until = from;
            var title = cells.Length > 1 ? cells[1] : "";
            if (cells.Length >= 3 && TryDate(cells[1], out var second))
            {
                until = second;
                title = cells[2];
            }

            if (title.Length == 0 || until < from || until.DayNumber - from.DayNumber > 62)
            {
                invalid++;
                continue;
            }

            if (until < horizonFrom || from > horizonUntil)
            {
                outside++;
                continue;
            }

            events.Add(new FeedEvent(from, until, null, null, title.Length > 200 ? title[..200] : title, null));
        }

        if (invalid > 0)
        {
            warnings.Add(invalid == 1 ? "1 Zeile ohne lesbares Datum oder Titel übersprungen." : $"{invalid} Zeilen ohne lesbares Datum oder Titel übersprungen.");
        }

        if (outside > 0)
        {
            warnings.Add($"{outside} Zeilen außerhalb von {horizonFrom.Year} bis {horizonUntil.Year} nicht übernommen.");
        }

        return new([.. events], warnings, events.Count > 0 || outside > 0);
    }

    private static bool TryDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

/// <summary>
/// Ordnet einen Eintrag eines Feiertagskalenders ein. Die öffentlichen Feiertagskalender von Google nennen in der Beschreibung „Gesetzlicher Feiertag“,
/// „Feiertag in Bayern, Sachsen“ (nur dort) oder „Gedenktag“ (Muttertag, Silvester: kein Feiertag). Ohne Beschreibung gilt der Eintrag als Feiertag.
/// </summary>
internal static partial class EntryClassifier
{
    public static (EntryScope Scope, string? RegionNames) Classify(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return (EntryScope.Holiday, null);
        }

        var first = description.Split('\n')[0].Trim();
        if (first.StartsWith("Gedenktag", StringComparison.OrdinalIgnoreCase))
        {
            return (EntryScope.Observance, null);
        }

        if (RegionalDescription().Match(first) is { Success: true } match)
        {
            var names = match.Groups[1].Value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var list = string.Join(", ", names);
            return (EntryScope.Regional, list.Length > 400 ? list[..400] : list);
        }

        return (EntryScope.Holiday, null);
    }

    [GeneratedRegex(@"^Feiertag in (.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RegionalDescription();
}

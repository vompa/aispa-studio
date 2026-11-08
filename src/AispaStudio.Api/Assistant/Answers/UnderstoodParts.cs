using System.Globalization;
using System.Text.RegularExpressions;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Was der Assistent in einer Frage erkannt hat, die er nicht beantworten kann (Ä316): einen Tag und Personen des Teams. Daraus werden Fragen, die er beantwortet
/// („Was steht morgen an?“, „Ist Anna morgen da?“).
/// </summary>
/// <param name="DayWord">Der Tag so, wie er in eine Frage passt: „heute“, „morgen“, „übermorgen“, „am Freitag“, „am 05.03.2027“.</param>
/// <param name="People">Namen der Personen, die die Frage nennt.</param>
internal sealed record UnderstoodParts(string? DayWord, IReadOnlyList<string> People)
{
    private static readonly string[] Weekdays = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];

    private static readonly string[] Relative = ["übermorgen", "morgen", "heute"];

    /// <summary>Das Erkannte in Worten, der Tag zuerst („morgen, Anna“).</summary>
    public IReadOnlyList<string> Parts => [.. DayWord is null ? People : People.Prepend(DayWord)];

    /// <summary>Fragen zum Erkannten: zum Tag der Tagesüberblick und wer arbeitet, zu einer Person, ob sie an dem Tag (sonst heute) da ist.</summary>
    public IReadOnlyList<string> Questions() =>
    [
        .. DayWord is { } day ? [$"Was steht {day} an?", $"Wer arbeitet {day}?"] : Array.Empty<string>(),
        .. People.Select(person => $"Ist {person} {DayWord ?? "heute"} da?"),
    ];

    /// <summary>Der Tag, den die Frage nennt: ein Datum, „übermorgen“, „morgen“, „heute“ oder ein Wochentag; sonst <c>null</c>.</summary>
    public static string? Day(string question, DateOnly today)
    {
        var text = question.ToLowerInvariant();
        if (RuleBasedAssistantClassifier.Date(text, today) is { } date)
        {
            return "am " + date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        var relative = Relative.FirstOrDefault(word => Regex.IsMatch(text, $@"\b{word}\b"));
        if (relative is not null)
        {
            return relative;
        }

        var weekday = Weekdays.FirstOrDefault(day => Regex.IsMatch(text, $@"\b{day.ToLowerInvariant()}\b"));
        return weekday is null ? null : $"am {weekday}";
    }
}

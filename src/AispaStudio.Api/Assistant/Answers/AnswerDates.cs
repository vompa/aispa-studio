using System.Globalization;
using System.Text.RegularExpressions;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Tage und Zeiträume aus einer Frage in Kalendertage des Standorts übersetzen („heute“, „Samstag“, „1. November“, „letzte Woche“, „im September“,
/// „letztes Quartal“, „seit 1.10.“). Rein rechnerisch, ohne KI; was die Frage nicht nennt, bleibt <c>null</c>.
/// </summary>
internal static partial class AnswerDates
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly (string Name, int Month)[] Months =
    [
        ("januar", 1), ("jänner", 1), ("februar", 2), ("märz", 3), ("maerz", 3), ("april", 4), ("mai", 5), ("juni", 6), ("juli", 7), ("august", 8),
        ("september", 9), ("oktober", 10), ("november", 11), ("dezember", 12),
    ];

    private static readonly (string Name, DayOfWeek Day)[] Weekdays =
    [
        ("montag", DayOfWeek.Monday), ("dienstag", DayOfWeek.Tuesday), ("mittwoch", DayOfWeek.Wednesday), ("donnerstag", DayOfWeek.Thursday),
        ("freitag", DayOfWeek.Friday), ("samstag", DayOfWeek.Saturday), ("sonnabend", DayOfWeek.Saturday), ("sonntag", DayOfWeek.Sunday),
    ];

    /// <summary>Ein einzelner Tag: heute, morgen, übermorgen, gestern, ein Wochentag (der nächste ab heute), „1.11.“ oder „1. November“.</summary>
    public static DateOnly? Day(string text, DateOnly today)
    {
        var lower = text.ToLowerInvariant();
        if (Numeric(lower, today) is { } numeric)
        {
            return numeric;
        }

        if (MonthDay(lower, today) is { } named)
        {
            return named;
        }

        if (Regex.IsMatch(lower, @"\bübermorgen\b|\bueber ?morgen\b"))
        {
            return today.AddDays(2);
        }

        if (Regex.IsMatch(lower, @"\bvorgestern\b"))
        {
            return today.AddDays(-2);
        }

        if (Regex.IsMatch(lower, @"\bgestern\b"))
        {
            return today.AddDays(-1);
        }

        if (Regex.IsMatch(lower, @"\bmorgen\b|\btomorrow\b"))
        {
            return today.AddDays(1);
        }

        if (Regex.IsMatch(lower, @"\bheute\b|\bjetzt\b|\bgerade\b|\btoday\b"))
        {
            return today;
        }

        foreach (var (name, day) in Weekdays)
        {
            if (Regex.IsMatch(lower, $@"\b{name}s?\b"))
            {
                var ahead = (((int)day - (int)today.DayOfWeek) + 7) % 7;
                // „nächsten Montag“ am Montag meint den in einer Woche.
                return today.AddDays(ahead == 0 && Regex.IsMatch(lower, $@"(nächste[nrm]?|kommende[nrm]?)\s+{name}") ? 7 : ahead);
            }
        }

        return null;
    }

    /// <summary>Ein Datum wie „1.11.“ oder „1.11.2027“; ohne Jahr das nächste solche Datum ab heute.</summary>
    private static DateOnly? Numeric(string lower, DateOnly today)
    {
        var match = NumericDate().Match(lower);
        return match.Success
            ? Build(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : null, today)
            : null;
    }

    /// <summary>Ein Datum wie „1. November“ oder „am 5. Mai 2027“.</summary>
    private static DateOnly? MonthDay(string lower, DateOnly today)
    {
        var match = NamedDate().Match(lower);
        if (!match.Success || MonthOf(match.Groups[2].Value) is not { } month)
        {
            return null;
        }

        return Build(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), month,
            match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : null, today);
    }

    private static DateOnly? Build(int day, int month, int? year, DateOnly today)
    {
        if (month is < 1 or > 12 || day < 1)
        {
            return null;
        }

        var y = year ?? today.Year;
        if (day > DateTime.DaysInMonth(y, month))
        {
            return null;
        }

        var date = new DateOnly(y, month, day);
        return year is null && date < today && day <= DateTime.DaysInMonth(y + 1, month) ? date.AddYears(1) : date;
    }

    private static int? MonthOf(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var (month, number) in Months.Select(m => (m.Name, m.Month)))
        {
            if (month == lower)
            {
                return number;
            }
        }

        return null;
    }

    /// <summary>
    /// Ein Zeitraum aus der Frage; <c>null</c>, wenn sie keinen nennt. Mit <paramref name="toToday"/> enden „diese Woche“, „diesen Monat“ und „dieses Quartal“
    /// heute (für Auswertungen: es gibt noch keine Zahlen aus der Zukunft); ohne reicht der Zeitraum bis zum Ende (für Pläne und Aktionen).
    /// <paramref name="future"/>: ein Monatsname ohne Jahr meint den nächsten solchen Monat (Aktionen), sonst den letzten (Auswertungen).
    /// </summary>
    public static (DateOnly From, DateOnly Until)? Range(string text, DateOnly today, bool toToday, bool future)
    {
        var lower = text.ToLowerInvariant();
        DateOnly End(DateOnly end) => toToday && end > today ? today : end;

        if (Regex.Match(lower, @"seit\s+(?<d>\d{1,2}\.\d{1,2}\.(?:\d{4})?|\d{1,2}\.\s*[a-zäöü]+(?:\s+\d{4})?)") is { Success: true } since
            && (Numeric(since.Groups["d"].Value, today) ?? MonthDay(since.Groups["d"].Value, today)) is { } sinceDay)
        {
            return (sinceDay > today ? sinceDay.AddYears(-1) : sinceDay, today);
        }

        if (Regex.Match(lower, @"(?:vom|von)\s+(?<a>\d{1,2}\.\d{1,2}\.(?:\d{4})?)\s+bis\s+(?<b>\d{1,2}\.\d{1,2}\.(?:\d{4})?)") is { Success: true } between
            && Numeric(between.Groups["a"].Value, today) is { } first && Numeric(between.Groups["b"].Value, today) is { } last && first <= last)
        {
            return (first, last);
        }

        if (Regex.Match(lower, @"(?:letzten?|vergangenen?|last)\s+(\d{1,3})\s+(?:tage|days)") is { Success: true } days)
        {
            return (today.AddDays(1 - int.Parse(days.Groups[1].Value, CultureInfo.InvariantCulture)), today);
        }

        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var month = new DateOnly(today.Year, today.Month, 1);
        var quarter = new DateOnly(today.Year, ((today.Month - 1) / 3 * 3) + 1, 1);
        if (Regex.IsMatch(lower, @"(?:letzte[nr]?|vergangene[nr]?|vorige[nr]?)\s+woche|last week"))
        {
            return (monday.AddDays(-7), monday.AddDays(-1));
        }

        if (Regex.IsMatch(lower, @"(?:nächste[nr]?|kommende[nr]?)\s+woche|next week"))
        {
            return (monday.AddDays(7), monday.AddDays(13));
        }

        if (Regex.IsMatch(lower, @"(?:diese[rn]?|aktuelle[rn]?)\s+woche|this week"))
        {
            return (monday, End(monday.AddDays(6)));
        }

        if (Regex.IsMatch(lower, @"(?:letzte[nrm]?|vergangene[nrm]?|vorige[nrm]?)\s+monat|last month"))
        {
            return (month.AddMonths(-1), month.AddDays(-1));
        }

        if (Regex.IsMatch(lower, @"(?:nächste[nrm]?|kommende[nrm]?)\s+monat|next month"))
        {
            return (month.AddMonths(1), month.AddMonths(2).AddDays(-1));
        }

        if (Regex.IsMatch(lower, @"(?:diese[nrm]?|aktuelle[nrm]?)\s+monat|this month"))
        {
            return (month, End(month.AddMonths(1).AddDays(-1)));
        }

        if (Regex.IsMatch(lower, @"(?:letzte[nrms]?|vergangene[nrms]?|vorige[nrms]?)\s+quartal|last quarter"))
        {
            return (quarter.AddMonths(-3), quarter.AddDays(-1));
        }

        if (Regex.IsMatch(lower, @"(?:nächste[nrms]?|kommende[nrms]?)\s+quartal|next quarter"))
        {
            return (quarter.AddMonths(3), quarter.AddMonths(6).AddDays(-1));
        }

        if (Regex.IsMatch(lower, @"(?:diese[nrms]?|aktuelle[nrms]?)\s+quartal|this quarter"))
        {
            return (quarter, End(quarter.AddMonths(3).AddDays(-1)));
        }

        if (Regex.Match(lower, @"(?:im\s+)?(?<n>[1-4])\.?\s*quartal(?:\s+(?<y>\d{4}))?|\bq(?<n>[1-4])(?:\s+(?<y>\d{4}))?\b") is { Success: true } numbered)
        {
            var year = numbered.Groups["y"].Success ? int.Parse(numbered.Groups["y"].Value, CultureInfo.InvariantCulture) : today.Year;
            var start = new DateOnly(year, ((int.Parse(numbered.Groups["n"].Value, CultureInfo.InvariantCulture) - 1) * 3) + 1, 1);
            if (!numbered.Groups["y"].Success && start > today && !future)
            {
                start = start.AddYears(-1);
            }

            return (start, End(start.AddMonths(3).AddDays(-1)));
        }

        if (Regex.IsMatch(lower, @"(?:letzte[nrms]?|vergangene[nrms]?|vorige[nrms]?)\s+jahr|last year"))
        {
            return (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31));
        }

        if (Regex.IsMatch(lower, @"(?:diese[nrms]?|aktuelle[nrms]?)\s+jahr|this year"))
        {
            return (new DateOnly(today.Year, 1, 1), End(new DateOnly(today.Year, 12, 31)));
        }

        if (MonthName().Match(lower) is { Success: true } named && MonthOf(named.Groups["m"].Value) is { } m)
        {
            int year;
            if (named.Groups["y"].Success)
            {
                year = int.Parse(named.Groups["y"].Value, CultureInfo.InvariantCulture);
            }
            else if (future)
            {
                year = m >= today.Month ? today.Year : today.Year + 1;
            }
            else
            {
                year = m <= today.Month ? today.Year : today.Year - 1;
            }

            var start = new DateOnly(year, m, 1);
            return (start, End(start.AddMonths(1).AddDays(-1)));
        }

        if (Day(lower, today) is { } day && DayWords().IsMatch(lower))
        {
            return (day, day);
        }

        return null;
    }

    /// <summary>Der Zeitraum in Worten: „Oktober 2026“, „3. Quartal 2026“, „am 22.10.2026“ oder „01.10.2026 bis 22.10.2026“.</summary>
    public static string Describe(DateOnly from, DateOnly until)
    {
        if (from == until)
        {
            return $"am {from.ToString("dd.MM.yyyy", German)}";
        }

        if (from.Day == 1 && until == from.AddMonths(1).AddDays(-1))
        {
            return from.ToString("MMMM yyyy", German);
        }

        if (from.Day == 1 && (from.Month - 1) % 3 == 0 && until == from.AddMonths(3).AddDays(-1))
        {
            return $"{((from.Month - 1) / 3) + 1}. Quartal {from.Year}";
        }

        return $"{from.ToString("dd.MM.yyyy", German)} bis {until.ToString("dd.MM.yyyy", German)}";
    }

    /// <summary>„Freitag, 23.10.2026“.</summary>
    public static string Long(DateOnly day) => day.ToString("dddd, dd.MM.yyyy", German);

    /// <summary>
    /// Die Zeit, die eine Frage an ihrem Tag meint, in UTC (Beginn einschließlich, Ende ausschließlich): ohne Tageszeit der ganze Tag, mit Tageszeit („heute nachmittag“) die
    /// Grenzen der Warteliste (<see cref="Online.Waitlist.Window"/>).
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset Until) Window(BusinessQuestion question, DateOnly today, TimeZoneInfo zone) =>
        Online.Waitlist.Window(question.Part ?? Contracts.DayPart.Any, question.From ?? today, zone);

    /// <summary>Die Tageszeit in Worten, „vormittags“, „nachmittags“, „abends“; ohne Tageszeit leer.</summary>
    public static string PartWord(Contracts.DayPart? part) => part switch
    {
        Contracts.DayPart.Morning => "vormittags",
        Contracts.DayPart.Afternoon => "nachmittags",
        Contracts.DayPart.Evening => "abends",
        _ => "",
    };

    /// <summary>Die Tageszeit einer Frage („heute nachmittag“, „vormittags“, „am Abend“); ohne Angabe <c>null</c>.</summary>
    public static Contracts.DayPart? Part(string text)
    {
        var lower = text.ToLowerInvariant();
        return Regex.IsMatch(lower, @"\bvormittags?\b") ? Contracts.DayPart.Morning
            : Regex.IsMatch(lower, @"\bnachmittags?\b") ? Contracts.DayPart.Afternoon
            : Regex.IsMatch(lower, @"\babends?\b") ? Contracts.DayPart.Evening
            : null;
    }

    [GeneratedRegex(@"\b(\d{1,2})\.\s?(\d{1,2})\.(?:\s?(\d{4}))?")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"\b(\d{1,2})\.?\s+(januar|jänner|februar|märz|maerz|april|mai|juni|juli|august|september|oktober|november|dezember)\b(?:\s+(\d{4}))?")]
    private static partial Regex NamedDate();

    [GeneratedRegex(@"\b(?<m>januar|jänner|februar|märz|maerz|april|mai|juni|juli|august|september|oktober|november|dezember)\b(?:\s+(?<y>\d{4}))?")]
    private static partial Regex MonthName();

    [GeneratedRegex(@"\b(heute|gestern|vorgestern|morgen|übermorgen|today|tomorrow|am\s+\w+tag|am\s+samstag|am\s+sonntag|\d{1,2}\.)")]
    private static partial Regex DayWords();
}

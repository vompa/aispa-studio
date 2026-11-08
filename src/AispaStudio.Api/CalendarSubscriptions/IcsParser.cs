using System.Globalization;
using System.Text;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>Ein Eintrag aus einer ICS- oder CSV-Datei: Zeitraum in Kalendertagen (<see cref="EndInclusive"/> einschließlich), bei Terminen mit Uhrzeit auch diese.</summary>
internal sealed record FeedEvent(DateOnly Start, DateOnly EndInclusive, TimeOnly? StartTime, TimeOnly? EndTime, string Summary, string? Description)
{
    public bool AllDay => StartTime is null;
}

/// <summary>Ergebnis des Lesens. <c>IsCalendar</c> ist falsch, wenn die Datei kein Kalender ist (kein <c>BEGIN:VCALENDAR</c>); <c>Warnings</c> nennen Überspringtes in einem Satz.</summary>
internal sealed record FeedParseResult(IReadOnlyList<FeedEvent> Events, IReadOnlyList<string> Warnings, bool IsCalendar);

/// <summary>
/// Schlanker Leser für ICS (RFC 5545): VEVENT mit DTSTART, DTEND oder DURATION, ganztägig oder mit Uhrzeit (UTC, TZID, schwebend), EXDATE, RECURRENCE-ID und
/// Wiederholungen (RRULE mit DAILY, WEEKLY, MONTHLY und YEARLY samt INTERVAL, COUNT, UNTIL, BYMONTH, BYMONTHDAY, BYDAY mit Ordnungszahl). Wiederholungen werden
/// nur im Horizont ausgeschrieben. Nicht Unterstütztes (BYSETPOS, BYWEEKNO, BYYEARDAY, RDATE) wird übersprungen und als Warnung gemeldet.
/// Kein Fremdpaket: die Feiertagskalender sind einfache Dateien von wenigen hundert Kilobyte.
/// </summary>
internal static class IcsParser
{
    public const int MaxEvents = 5000;
    private const int MaxIterations = 20000;
    private const int MaxSpanDays = 62;

    public static FeedParseResult Parse(string text, DateOnly horizonFrom, DateOnly horizonUntil, TimeZoneInfo zone)
    {
        var warnings = new List<string>();
        var lines = Unfold(text);
        if (!lines.Any(l => l.Name == "BEGIN" && l.Value.Equals("VCALENDAR", StringComparison.OrdinalIgnoreCase)))
        {
            return new([], warnings, false);
        }

        var raw = new List<RawEvent>();
        RawEvent? current = null;
        var skipDepth = 0;
        foreach (var line in lines)
        {
            if (line.Name == "BEGIN")
            {
                if (current is not null || skipDepth > 0)
                {
                    skipDepth++;
                }
                else if (line.Value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new RawEvent();
                }
                else if (!line.Value.Equals("VCALENDAR", StringComparison.OrdinalIgnoreCase))
                {
                    skipDepth = 1;
                }

                continue;
            }

            if (line.Name == "END")
            {
                if (skipDepth > 0)
                {
                    skipDepth--;
                }
                else if (current is not null && line.Value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    raw.Add(current);
                    current = null;
                }

                continue;
            }

            if (current is not null && skipDepth == 0)
            {
                current.Properties.Add(line);
            }
        }

        var events = new List<FeedEvent>();
        var overrides = raw.Where(r => r.Has("RECURRENCE-ID") && r.Get("UID") is not null).ToList();
        var replaced = new HashSet<(string, DateOnly)>();
        foreach (var o in overrides)
        {
            if (ReadDateTime(o.Properties.First(p => p.Name == "RECURRENCE-ID"), zone) is { } at)
            {
                replaced.Add((o.Get("UID")!, DateOnly.FromDateTime(at.Local)));
            }
        }

        var skippedRules = 0;
        var broken = 0;
        foreach (var ev in raw)
        {
            if (string.Equals(ev.Get("STATUS"), "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ReadEvent(ev, zone) is not { } basis)
            {
                broken++;
                continue;
            }

            var summary = Clean(Unescape(ev.Get("SUMMARY") ?? ""), 200);
            if (summary.Length == 0)
            {
                summary = "(ohne Titel)";
            }

            var description = ev.Get("DESCRIPTION") is { } d ? Unescape(d).Trim() : null;
            var uid = ev.Get("UID");
            foreach (var (start, end) in Occurrences(ev, basis, horizonFrom, horizonUntil, zone, ref skippedRules))
            {
                if (end < horizonFrom || start > horizonUntil)
                {
                    continue;
                }

                if (!ev.Has("RECURRENCE-ID") && uid is not null && replaced.Contains((uid, start)))
                {
                    continue;
                }

                events.Add(new FeedEvent(start, end, basis.StartTime, basis.EndTime, summary, description));
                if (events.Count >= MaxEvents)
                {
                    warnings.Add($"Mehr als {MaxEvents} Einträge im Horizont: der Rest wurde nicht übernommen.");
                    return new([.. events], warnings, true);
                }
            }
        }

        if (broken > 0)
        {
            warnings.Add(broken == 1 ? "1 Eintrag ohne lesbaren Beginn übersprungen." : $"{broken} Einträge ohne lesbaren Beginn übersprungen.");
        }

        if (skippedRules > 0)
        {
            warnings.Add(skippedRules == 1 ? "1 Wiederholung mit nicht unterstützter Regel nur einmal übernommen." : $"{skippedRules} Wiederholungen mit nicht unterstützter Regel nur einmal übernommen.");
        }

        return new([.. events], warnings, true);
    }

    // ---------- Zeilen ----------

    private sealed record Line(string Name, IReadOnlyDictionary<string, string> Parameters, string Value);

    private sealed class RawEvent
    {
        public List<Line> Properties { get; } = [];

        public bool Has(string name) => Properties.Any(p => p.Name == name);

        public string? Get(string name) => Properties.FirstOrDefault(p => p.Name == name)?.Value;
    }

    private static List<Line> Unfold(string text)
    {
        var physical = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var unfolded = new List<string>();
        foreach (var line in physical)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && unfolded.Count > 0)
            {
                unfolded[^1] += line[1..];
            }
            else if (line.Length > 0)
            {
                unfolded.Add(line);
            }
        }

        var result = new List<Line>(unfolded.Count);
        foreach (var line in unfolded)
        {
            if (SplitLine(line) is { } parsed)
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static Line? SplitLine(string line)
    {
        // Der erste Doppelpunkt außerhalb von Anführungszeichen trennt Name samt Parametern vom Wert.
        var quoted = false;
        var colon = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                quoted = !quoted;
            }
            else if (line[i] == ':' && !quoted)
            {
                colon = i;
                break;
            }
        }

        if (colon <= 0)
        {
            return null;
        }

        var head = line[..colon];
        var parts = new List<string>();
        var start = 0;
        quoted = false;
        for (var i = 0; i < head.Length; i++)
        {
            if (head[i] == '"')
            {
                quoted = !quoted;
            }
            else if (head[i] == ';' && !quoted)
            {
                parts.Add(head[start..i]);
                start = i + 1;
            }
        }

        parts.Add(head[start..]);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
            {
                parameters[part[..eq]] = part[(eq + 1)..].Trim('"');
            }
        }

        return new Line(parts[0].ToUpperInvariant(), parameters, line[(colon + 1)..]);
    }

    private static string Unescape(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
        {
            return value;
        }

        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
                text.Append(value[i] is 'n' or 'N' ? '\n' : value[i]);
            }
            else
            {
                text.Append(value[i]);
            }
        }

        return text.ToString();
    }

    private static string Clean(string value, int max)
    {
        var text = string.Join(' ', value.Split(['\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    // ---------- Zeitpunkte ----------

    private readonly record struct Moment(DateTime Local, bool DateOnly);

    private static Moment? ReadDateTime(Line line, TimeZoneInfo fallback)
    {
        var value = line.Value.Trim();
        var isDate = (line.Parameters.TryGetValue("VALUE", out var kind) && kind.Equals("DATE", StringComparison.OrdinalIgnoreCase)) || value.Length == 8;
        if (isDate)
        {
            return DateTime.TryParseExact(value[..Math.Min(8, value.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? new Moment(day, true)
                : null;
        }

        var utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(utc ? value[..^1] : value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        if (utc)
        {
            return new Moment(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(local, DateTimeKind.Utc), fallback), false);
        }

        if (line.Parameters.TryGetValue("TZID", out var tzid) && !string.IsNullOrWhiteSpace(tzid) && FindZone(tzid) is { } source && source.Id != fallback.Id)
        {
            var instant = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), source);
            return new Moment(TimeZoneInfo.ConvertTimeFromUtc(instant, fallback), false);
        }

        return new Moment(local, false);
    }

    private static TimeZoneInfo? FindZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private sealed record Basis(DateOnly Start, DateOnly EndInclusive, TimeOnly? StartTime, TimeOnly? EndTime)
    {
        public int SpanDays => Math.Min(MaxSpanDays, EndInclusive.DayNumber - Start.DayNumber);
    }

    private static Basis? ReadEvent(RawEvent ev, TimeZoneInfo zone)
    {
        if (ev.Properties.FirstOrDefault(p => p.Name == "DTSTART") is not { } startLine || ReadDateTime(startLine, zone) is not { } start)
        {
            return null;
        }

        var startDay = DateOnly.FromDateTime(start.Local);
        DateOnly endDay;
        TimeOnly? endTime = null;
        if (ev.Properties.FirstOrDefault(p => p.Name == "DTEND") is { } endLine && ReadDateTime(endLine, zone) is { } end)
        {
            if (start.DateOnly)
            {
                endDay = DateOnly.FromDateTime(end.Local).AddDays(-1);
            }
            else
            {
                endDay = DateOnly.FromDateTime(end.Local);
                endTime = TimeOnly.FromDateTime(end.Local);
                if (endTime == TimeOnly.MinValue && endDay > startDay)
                {
                    endDay = endDay.AddDays(-1);
                    endTime = new TimeOnly(23, 59);
                }
            }
        }
        else if (ev.Get("DURATION") is { } duration && ReadDuration(duration) is { } span)
        {
            var until = start.Local + span;
            endDay = start.DateOnly ? DateOnly.FromDateTime(until).AddDays(-1) : DateOnly.FromDateTime(until);
            endTime = start.DateOnly ? null : TimeOnly.FromDateTime(until);
        }
        else
        {
            endDay = startDay;
            endTime = start.DateOnly ? null : TimeOnly.FromDateTime(start.Local);
        }

        if (endDay < startDay)
        {
            endDay = startDay;
        }

        return new Basis(startDay, endDay, start.DateOnly ? null : TimeOnly.FromDateTime(start.Local), start.DateOnly ? null : endTime);
    }

    private static TimeSpan? ReadDuration(string value)
    {
        var text = value.Trim();
        var negative = text.StartsWith('-');
        text = text.TrimStart('+', '-');
        if (!text.StartsWith('P'))
        {
            return null;
        }

        var total = TimeSpan.Zero;
        var number = 0;
        var inTime = false;
        foreach (var c in text[1..])
        {
            if (char.IsAsciiDigit(c))
            {
                number = (number * 10) + (c - '0');
                continue;
            }

            switch (c)
            {
                case 'T':
                    inTime = true;
                    break;
                case 'W':
                    total += TimeSpan.FromDays(7 * number);
                    break;
                case 'D':
                    total += TimeSpan.FromDays(number);
                    break;
                case 'H' when inTime:
                    total += TimeSpan.FromHours(number);
                    break;
                case 'M' when inTime:
                    total += TimeSpan.FromMinutes(number);
                    break;
                case 'S' when inTime:
                    total += TimeSpan.FromSeconds(number);
                    break;
                default:
                    return null;
            }

            number = 0;
        }

        return negative ? -total : total;
    }

    // ---------- Wiederholungen ----------

    private static List<(DateOnly Start, DateOnly End)> Occurrences(
        RawEvent ev, Basis basis, DateOnly from, DateOnly until, TimeZoneInfo zone, ref int skippedRules)
    {
        var rule = ev.Get("RRULE");
        if (rule is null && !ev.Has("RDATE"))
        {
            return [(basis.Start, basis.EndInclusive)];
        }

        var span = basis.SpanDays;
        var excluded = new HashSet<DateOnly>();
        foreach (var ex in ev.Properties.Where(p => p.Name == "EXDATE"))
        {
            foreach (var part in ex.Value.Split(','))
            {
                if (ReadDateTime(new Line("EXDATE", ex.Parameters, part), zone) is { } at)
                {
                    excluded.Add(DateOnly.FromDateTime(at.Local));
                }
            }
        }

        if (rule is null || !RecurrenceRule.TryParse(rule, out var parsed))
        {
            skippedRules++;
            return [(basis.Start, basis.EndInclusive)];
        }

        return parsed!.Expand(basis.Start, from.AddDays(-span), until)
            .Where(d => !excluded.Contains(d))
            .Select(d => (d, d.AddDays(span)))
            .ToList();
    }

    /// <summary>Eine Wiederholungsregel, soweit der Leser sie kennt.</summary>
    private sealed class RecurrenceRule
    {
        private string _freq = "";
        private int _interval = 1;
        private int? _count;
        private DateOnly? _until;
        private List<int> _months = [];
        private List<int> _monthDays = [];
        private List<(int Ordinal, DayOfWeek Day)> _weekDays = [];

        public static bool TryParse(string text, out RecurrenceRule? rule)
        {
            rule = null;
            var parsed = new RecurrenceRule();
            foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=', StringComparison.Ordinal);
                if (eq <= 0)
                {
                    return false;
                }

                var key = part[..eq].ToUpperInvariant();
                var value = part[(eq + 1)..];
                switch (key)
                {
                    case "FREQ":
                        parsed._freq = value.ToUpperInvariant();
                        break;
                    case "INTERVAL" when int.TryParse(value, CultureInfo.InvariantCulture, out var interval) && interval >= 1:
                        parsed._interval = interval;
                        break;
                    case "COUNT" when int.TryParse(value, CultureInfo.InvariantCulture, out var count) && count >= 1:
                        parsed._count = count;
                        break;
                    case "UNTIL":
                        var day = value.Length >= 8 ? value[..8] : value;
                        if (!DateOnly.TryParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until))
                        {
                            return false;
                        }

                        parsed._until = until;
                        break;
                    case "BYMONTH":
                        parsed._months = [.. value.Split(',').Select(v => int.TryParse(v, CultureInfo.InvariantCulture, out var m) ? m : -1)];
                        break;
                    case "BYMONTHDAY":
                        parsed._monthDays = [.. value.Split(',').Select(v => int.TryParse(v, CultureInfo.InvariantCulture, out var m) ? m : 0)];
                        break;
                    case "BYDAY":
                        var days = new List<(int, DayOfWeek)>();
                        foreach (var item in value.Split(','))
                        {
                            if (!TryWeekday(item, out var entry))
                            {
                                return false;
                            }

                            days.Add(entry);
                        }

                        parsed._weekDays = days;
                        break;
                    case "WKST":
                        break;
                    default:
                        return false;
                }
            }

            if (parsed._freq is not ("DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY")
                || parsed._months.Any(m => m is < 1 or > 12) || parsed._monthDays.Any(d => d == 0 || Math.Abs(d) > 31))
            {
                return false;
            }

            rule = parsed;
            return true;
        }

        private static bool TryWeekday(string item, out (int Ordinal, DayOfWeek Day) entry)
        {
            entry = default;
            if (item.Length < 2)
            {
                return false;
            }

            var code = item[^2..].ToUpperInvariant();
            DayOfWeek? day = code switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => null,
            };
            var prefix = item[..^2];
            var ordinal = 0;
            if (day is null || (prefix.Length > 0 && !int.TryParse(prefix, CultureInfo.InvariantCulture, out ordinal)))
            {
                return false;
            }

            entry = (ordinal, day.Value);
            return true;
        }

        /// <summary>Alle Vorkommen ab <paramref name="start"/> (zählt für COUNT), geliefert nur die ab <paramref name="from"/> bis <paramref name="until"/>.</summary>
        public IEnumerable<DateOnly> Expand(DateOnly start, DateOnly from, DateOnly until)
        {
            var emitted = 0;
            var limit = _until is { } u && u < until ? u : until;
            for (var n = 0; n < MaxIterations; n++)
            {
                if (PeriodStart(start, n) is not { } periodStart || periodStart > limit)
                {
                    yield break;
                }

                foreach (var date in Period(start, n).Where(d => d >= start).Distinct().Order())
                {
                    if (date > limit)
                    {
                        yield break;
                    }

                    emitted++;
                    if (_count is { } count && emitted > count)
                    {
                        yield break;
                    }

                    if (date >= from)
                    {
                        yield return date;
                    }
                }
            }
        }

        /// <summary>Erster Tag der n-ten Periode (Tag, Woche ab Montag, Monat oder Jahr); leer, wenn das Datum außerhalb des Kalenders liegt. Beginnt eine Periode nach dem Ende der Suche, ist die Suche fertig.</summary>
        private DateOnly? PeriodStart(DateOnly start, int n)
        {
            try
            {
                return _freq switch
                {
                    "DAILY" => start.AddDays(n * _interval),
                    "WEEKLY" => start.AddDays(-(((int)start.DayOfWeek + 6) % 7) + (7 * n * _interval)),
                    "MONTHLY" => new DateOnly(start.Year, start.Month, 1).AddMonths(n * _interval),
                    _ => new DateOnly(start.Year + (n * _interval), 1, 1),
                };
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private IEnumerable<DateOnly> Period(DateOnly start, int n)
        {
            switch (_freq)
            {
                case "DAILY":
                    var day = start.AddDays(n * _interval);
                    if ((_months.Count == 0 || _months.Contains(day.Month)) && (_weekDays.Count == 0 || _weekDays.Any(w => w.Day == day.DayOfWeek))
                        && (_monthDays.Count == 0 || MatchesMonthDay(day)))
                    {
                        yield return day;
                    }

                    yield break;
                case "WEEKLY":
                    var monday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7) + (7 * n * _interval));
                    var wanted = _weekDays.Count == 0 ? [start.DayOfWeek] : _weekDays.Select(w => w.Day).ToList();
                    foreach (var w in wanted)
                    {
                        var date = monday.AddDays(((int)w + 6) % 7);
                        if (_months.Count == 0 || _months.Contains(date.Month))
                        {
                            yield return date;
                        }
                    }

                    yield break;
                case "MONTHLY":
                    var month = new DateOnly(start.Year, start.Month, 1).AddMonths(n * _interval);
                    if (_months.Count > 0 && !_months.Contains(month.Month))
                    {
                        yield break;
                    }

                    foreach (var date in InMonth(month.Year, month.Month, start))
                    {
                        yield return date;
                    }

                    yield break;
                default:
                    var year = start.Year + (n * _interval);
                    if (year > 9998)
                    {
                        yield break;
                    }

                    foreach (var m in _months.Count == 0 ? (IEnumerable<int>)[start.Month] : _months.Order())
                    {
                        foreach (var date in InMonth(year, m, start))
                        {
                            yield return date;
                        }
                    }

                    yield break;
            }
        }

        private bool MatchesMonthDay(DateOnly date) =>
            _monthDays.Any(d => d > 0 ? date.Day == d : date.Day == DateTime.DaysInMonth(date.Year, date.Month) + d + 1);

        private IEnumerable<DateOnly> InMonth(int year, int month, DateOnly start)
        {
            var length = DateTime.DaysInMonth(year, month);
            if (_monthDays.Count > 0)
            {
                foreach (var d in _monthDays.Select(d => d > 0 ? d : length + d + 1).Where(d => d >= 1 && d <= length).Order())
                {
                    var date = new DateOnly(year, month, d);
                    if (_weekDays.Count == 0 || _weekDays.Any(w => w.Day == date.DayOfWeek))
                    {
                        yield return date;
                    }
                }

                yield break;
            }

            if (_weekDays.Count > 0)
            {
                foreach (var (ordinal, weekday) in _weekDays)
                {
                    var matches = Enumerable.Range(1, length).Select(d => new DateOnly(year, month, d)).Where(d => d.DayOfWeek == weekday).ToList();
                    if (ordinal == 0)
                    {
                        foreach (var d in matches)
                        {
                            yield return d;
                        }
                    }
                    else if (Math.Abs(ordinal) <= matches.Count)
                    {
                        yield return ordinal > 0 ? matches[ordinal - 1] : matches[matches.Count + ordinal];
                    }
                }

                yield break;
            }

            if (start.Day <= length)
            {
                yield return new DateOnly(year, month, start.Day);
            }
        }
    }
}

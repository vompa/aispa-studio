using System.Globalization;

namespace AispaStudio.Api.Exports;

/// <summary>Ein Termin für die Kalenderdatei; <paramref name="Alarm"/> ist die Vorlaufzeit der Erinnerung, ohne Wert keine.</summary>
internal sealed record ICalEvent(
    Guid Id,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Summary,
    string? Location,
    string? Description,
    string? Url,
    TimeSpan? Alarm);

/// <summary>
/// Schreibt Termine als iCalendar 2.0 (RFC 5545): ein <c>VCALENDAR</c> mit <c>VTIMEZONE</c> des Standorts (Outlook braucht sie zur TZID), je Termin ein <c>VEVENT</c>
/// mit Ortszeit und stabiler <c>UID</c> aus der Termin-Id; Zeilen nach 75 Oktetten umgebrochen. Kein Fremdpaket; der Leser dazu ist <c>IcsParser</c>.
/// </summary>
internal static class ICalWriter
{
    public const string ProductId = "-//AISPA Studio//DE";

    private const string UidSuffix = "@aispa-studio";

    public static byte[] Write(IEnumerable<ICalEvent> events, TimeZoneInfo zone, DateTimeOffset stamp)
    {
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", $"PRODID:{ProductId}", "CALSCALE:GREGORIAN", "METHOD:PUBLISH" };
        lines.AddRange(TimeZoneLines(zone));
        var stampText = stamp.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        foreach (var e in events)
        {
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:{e.Id}{UidSuffix}");
            lines.Add($"DTSTAMP:{stampText}");
            lines.Add($"DTSTART;TZID={zone.Id}:{Local(e.Start, zone)}");
            lines.Add($"DTEND;TZID={zone.Id}:{Local(e.End, zone)}");
            lines.Add($"SUMMARY:{TextLines.Escape(e.Summary)}");
            if (!string.IsNullOrWhiteSpace(e.Location))
            {
                lines.Add($"LOCATION:{TextLines.Escape(e.Location)}");
            }

            if (!string.IsNullOrWhiteSpace(e.Description))
            {
                lines.Add($"DESCRIPTION:{TextLines.Escape(e.Description)}");
            }

            if (!string.IsNullOrWhiteSpace(e.Url))
            {
                lines.Add($"URL:{e.Url}");
            }

            if (e.Alarm is { } alarm && alarm > TimeSpan.Zero)
            {
                lines.Add("BEGIN:VALARM");
                lines.Add("ACTION:DISPLAY");
                lines.Add($"DESCRIPTION:{TextLines.Escape(e.Summary)}");
                lines.Add($"TRIGGER:-PT{(alarm.TotalMinutes % 60 == 0 ? $"{(int)alarm.TotalHours}H" : $"{(int)alarm.TotalMinutes}M")}");
                lines.Add("END:VALARM");
            }

            lines.Add("END:VEVENT");
        }

        lines.Add("END:VCALENDAR");
        return TextLines.ToBytes(lines);
    }

    private static string Local(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>VTIMEZONE</c> aus den Regeln der Zeitzone: eine wiederkehrende Sommerzeitregel („letzter Sonntag im März“), wenn die Zone eine hat, sonst nur die Normalzeit.
    /// Bevorzugt wird die letzte Regel mit gleitenden Übergängen; die Regel-Daten je Jahr (Linux) taugen nicht für eine Wiederholung.
    /// </summary>
    private static IEnumerable<string> TimeZoneLines(TimeZoneInfo zone)
    {
        yield return "BEGIN:VTIMEZONE";
        yield return $"TZID:{zone.Id}";
        var rules = zone.GetAdjustmentRules();
        var rule = rules.LastOrDefault(r => !r.DaylightTransitionStart.IsFixedDateRule && !r.DaylightTransitionEnd.IsFixedDateRule) ?? rules.LastOrDefault();
        var standard = zone.BaseUtcOffset + (rule?.BaseUtcOffsetDelta ?? TimeSpan.Zero);
        if (rule is null || rule.DaylightDelta == TimeSpan.Zero)
        {
            yield return "BEGIN:STANDARD";
            yield return "DTSTART:19700101T000000";
            yield return $"TZOFFSETFROM:{Offset(standard)}";
            yield return $"TZOFFSETTO:{Offset(standard)}";
            yield return "END:STANDARD";
            yield return "END:VTIMEZONE";
            yield break;
        }

        var daylight = standard + rule.DaylightDelta;
        yield return "BEGIN:DAYLIGHT";
        yield return $"DTSTART:{FirstOccurrence(rule.DaylightTransitionStart)}";
        yield return $"TZOFFSETFROM:{Offset(standard)}";
        yield return $"TZOFFSETTO:{Offset(daylight)}";
        yield return $"RRULE:{Recurrence(rule.DaylightTransitionStart)}";
        yield return "END:DAYLIGHT";
        yield return "BEGIN:STANDARD";
        yield return $"DTSTART:{FirstOccurrence(rule.DaylightTransitionEnd)}";
        yield return $"TZOFFSETFROM:{Offset(daylight)}";
        yield return $"TZOFFSETTO:{Offset(standard)}";
        yield return $"RRULE:{Recurrence(rule.DaylightTransitionEnd)}";
        yield return "END:STANDARD";
        yield return "END:VTIMEZONE";
    }

    private static string Offset(TimeSpan offset) =>
        $"{(offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(offset.Hours):00}{Math.Abs(offset.Minutes):00}";

    private static string Recurrence(TimeZoneInfo.TransitionTime transition) => transition.IsFixedDateRule
        ? $"FREQ=YEARLY;BYMONTH={transition.Month};BYMONTHDAY={transition.Day}"
        : $"FREQ=YEARLY;BYMONTH={transition.Month};BYDAY={(transition.Week == 5 ? -1 : transition.Week)}{Weekday(transition.DayOfWeek)}";

    private static string Weekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "MO",
        DayOfWeek.Tuesday => "TU",
        DayOfWeek.Wednesday => "WE",
        DayOfWeek.Thursday => "TH",
        DayOfWeek.Friday => "FR",
        DayOfWeek.Saturday => "SA",
        _ => "SU",
    };

    /// <summary>Das Datum des Übergangs im Jahr 1970, wie es in <c>VTIMEZONE</c> üblich ist.</summary>
    private static string FirstOccurrence(TimeZoneInfo.TransitionTime transition)
    {
        var day = transition.IsFixedDateRule ? transition.Day : NthWeekday(1970, transition.Month, transition.Week, transition.DayOfWeek);
        return new DateTime(1970, transition.Month, day, transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, 0, DateTimeKind.Unspecified)
            .ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
    }

    private static int NthWeekday(int year, int month, int week, DayOfWeek dayOfWeek)
    {
        if (week == 5)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-(((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7)).Day;
        }

        var first = new DateTime(year, month, 1);
        return first.AddDays((((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7) + ((week - 1) * 7)).Day;
    }
}

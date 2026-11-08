using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wie lange haben wir heute offen?“, „Haben wir am Samstag offen?“, „Wann sperren wir morgen zu?“, „Ist am 1. November offen?“, „Sind wir im Jänner geschlossen?“:
/// aus den Öffnungszeiten des Standorts, seinen Schließzeiten (ADR 0073: Betriebsurlaub, Sonderschließung, Ausfall) und den Feiertagen seines Landes und
/// Bundeslandes (Regeln und aktive Kalender-Abos, ADR 0075). Eine Schließzeit geht vor: an ihrem Tag nennt die Antwort sie statt der Öffnungszeit des Wochentags.
/// </summary>
internal sealed partial class OpeningHoursAnswers(AispaDbContext db, HolidayCalendar holidays) : IAnswerGiver
{
    /// <summary>So viele Tage voraus nennt die Wochenübersicht Schließzeiten.</summary>
    private const int WeekAhead = 14;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.OpeningHours];

    public IReadOnlyCollection<string> Fields(BusinessQuestion question) => [FieldIds.Region];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstAsync(l => l.Id == context.Location.Id, ct);
        var source = $"Öffnungszeiten des {context.PlaceOf}";
        var manage = context.Setting(AccessRight.ManageLocation, "Schließzeiten verwalten", "masterData", "closures");
        if (question.Detail == "range" && question.From is { } rangeFrom)
        {
            return await ClosuresInAsync(question, context, rangeFrom, question.Until ?? rangeFrom, manage, ct);
        }

        if (location.OpeningHours.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Öffnungszeiten", source, context.Fill(FieldIds.OpeningHours));
        }

        if (question.Detail == "week")
        {
            var weekText = $"Öffnungszeiten des {context.PlaceOf} laut Wochenplan.";
            var upcoming = await ClosureService.InAsync(db, location.Id, context.Now, context.Now.AddDays(WeekAhead), context.Zone, ct);
            if (upcoming.Count > 0)
            {
                weekText += $" In den nächsten {WeekAhead} Tagen gibt es eine Schließzeit: {ClosureText.Title(upcoming[0].Kind)}, "
                    + $"{ClosureText.Period(upcoming[0].FromDate, upcoming[0].FromTime, upcoming[0].UntilDate, upcoming[0].UntilTime, context.Today)}.";
            }

            return Blocks.Answer(question.Topic, weekText, upcoming.Count > 0 ? $"Öffnungszeiten und Schließzeiten des {context.PlaceOf}" : source,
                Blocks.Table(["Wochentag", "Geöffnet"], WeekOrder.Select(d => (IReadOnlyList<string>)[DayName(d), Hours(location, d)])), upcoming.Count > 0 ? manage : null);
        }

        var day = question.From ?? context.Today;
        var hours = location.OpeningHours.Where(h => h.Day == day.DayOfWeek).OrderBy(h => h.Opens).ToList();
        var (dayFrom, dayUntil) = LocalTime.Day(day, context.Zone);
        var closed = await ClosureService.InAsync(db, location.Id, dayFrom, dayUntil, context.Zone, ct);
        var holiday = await holidays.OnAsync(location.Id, day, ct);
        var asks = question.Text.ToLowerInvariant();
        var yesNo = YesNoQuestion().IsMatch(asks);
        var when = AnswerDates.Long(day);

        // Mit Schließzeit zählen nur die Zeiten des Tages, die sie übrig lässt.
        var spans = closed.Select(c => new ClosedSpan(c.StartsAt(context.Zone), c.EndsAt(context.Zone), c.Kind)).ToList();
        var ranges = ResourcePlanner.OpeningWindows(location.OpeningHours, context.Zone, day, spans)
            .Select(w => (Opens: LocalClock(w.Start, context), Closes: LocalClock(w.End, context))).ToList();

        string text;
        if (hours.Count == 0)
        {
            text = $"{(yesNo ? "Nein, am" : "Am")} {when} ist geschlossen: Der Wochenplan des Standorts hat an einem {DayName(day.DayOfWeek)} keine Öffnungszeit.";
        }
        else if (ranges.Count == 0)
        {
            text = $"{(yesNo ? "Nein, am" : "Am")} {when} ist geschlossen: {ClosureText.Sentence(ClosureText.Describe(closed[0], day))}";
        }
        else
        {
            var range = string.Join(" und ", ranges.Select(h => $"{Clock(h.Opens)} bis {Clock(h.Closes)}"));
            var total = ranges.Sum(h => (h.Closes - h.Opens).TotalHours);
            var length = $"{total.ToString("0.#", AnswerContext.German)} Stunden";
            var closing = Clock(ranges[^1].Closes);
            text = ClosingQuestion().IsMatch(asks)
                ? $"Am {when} wird um {closing} Uhr zugesperrt (geöffnet {range} Uhr, {length})."
                : $"{(yesNo ? "Ja, am" : "Am")} {when} ist von {range} Uhr geöffnet ({length}).";
            if (closed.Count > 0)
            {
                text += $" Der Rest des Tages ist geschlossen: {ClosureText.Sentence(ClosureText.Describe(closed[0], day))}";
            }
        }

        if (holiday is not null)
        {
            var name = holiday.FromRules && PublicHolidays.Region(location.Region) is { } region ? $"{holiday.Name}, {region.Name}" : holiday.Name;
            text += $" Der Tag ist ein gesetzlicher Feiertag ({name})"
                + (closed.Count > 0 ? "." : $"; geöffnet wie an einem {DayName(day.DayOfWeek)}, sofern keine Schließzeit eingetragen ist.");
            if (!holiday.FromRules)
            {
                source += $"; Feiertag aus dem {holiday.Source}, {HolidayCalendar.AsOfText(holiday.AsOf, context.Today, context.Zone)}";
            }
        }

        if (closed.Count == 0)
        {
            return Blocks.Answer(question.Topic, text, source);
        }

        return Blocks.Answer(question.Topic, text, $"Öffnungszeiten und Schließzeiten des {context.PlaceOf}", ClosureTable(closed, context), manage);
    }

    /// <summary>„Sind wir im Jänner geschlossen?“: die Schließzeiten, die den Zeitraum schneiden.</summary>
    private async Task<AssistantBlock> ClosuresInAsync(BusinessQuestion question, AnswerContext context, DateOnly from, DateOnly until, AssistantLink? manage, CancellationToken ct)
    {
        var (start, _) = LocalTime.Day(from, context.Zone);
        var (_, end) = LocalTime.Day(until, context.Zone);
        var closed = await ClosureService.InAsync(db, context.Location.Id, start, end, context.Zone, ct);
        var period = AnswerDates.Describe(from, until);
        var source = $"Schließzeiten des {context.PlaceOf}";
        if (closed.Count == 0)
        {
            return Blocks.Answer(question.Topic, $"Nein, im Zeitraum {period} ist keine Schließzeit eingetragen. Es gelten die Öffnungszeiten laut Wochenplan.", source, link: manage, period: period);
        }

        var whole = closed.Any(c => c.StartsAt(context.Zone) <= start && end <= c.EndsAt(context.Zone));
        var text = whole
            ? $"Ja, im Zeitraum {period} ist ganz geschlossen: {ClosureText.Title(closed[0].Kind)}."
            : closed.Count == 1
                ? $"Teilweise: im Zeitraum {period} gibt es eine Schließzeit ({ClosureText.Title(closed[0].Kind)}, {Period(closed[0], context)})."
                : $"Teilweise: im Zeitraum {period} gibt es {closed.Count} Schließzeiten.";
        return Blocks.Answer(question.Topic, text, source, ClosureTable(closed, context), manage, period: period);
    }

    private static AssistantTable ClosureTable(IEnumerable<Closure> closed, AnswerContext context) =>
        Blocks.Table(["Art", "Zeitraum", "Text für Gäste"],
            closed.Select(c => (IReadOnlyList<string>)[ClosureText.Title(c.Kind), Period(c, context), ClosureText.GuestText(c.Kind, c.GuestText)]));

    private static string Period(Closure c, AnswerContext context) => ClosureText.Period(c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, context.Today);

    private static TimeOnly LocalClock(DateTimeOffset at, AnswerContext context) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, context.Zone).DateTime);

    private static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static string DayName(DayOfWeek day) => AnswerContext.German.DateTimeFormat.GetDayName(day);

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Hours(Location location, DayOfWeek day)
    {
        var hours = location.OpeningHours.Where(h => h.Day == day).OrderBy(h => h.Opens).ToList();
        return hours.Count == 0 ? "geschlossen" : string.Join(" und ", hours.Select(h => $"{Clock(h.Opens)} bis {Clock(h.Closes)}"));
    }

    [GeneratedRegex(@"^\W*(haben|ist|sind|hat|gibt)\b")]
    private static partial Regex YesNoQuestion();

    [GeneratedRegex(@"zusperr|sperr\w*\s+.*\bzu\b|zumach|schließ|schliess|bis wann|wie spät")]
    private static partial Regex ClosingQuestion();
}

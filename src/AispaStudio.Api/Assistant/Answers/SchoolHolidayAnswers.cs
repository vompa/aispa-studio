using System.Globalization;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann sind Ferien?“, „Wann sind die Herbstferien?“, „Sind am 28. Oktober Ferien?“: aus den aktiven Kalender-Abos der Art „Schulferien“, die am Standort gelten
/// (ADR 0075). Ohne ein solches Abo ist nichts hinterlegt und die Antwort verweist (mit dem Recht dazu) auf die Verwaltung. Die Quelle nennt das Abo und den Stand seines Abrufs.
/// </summary>
internal sealed class SchoolHolidayAnswers(HolidayCalendar calendar) : IAnswerGiver
{
    private const int Show = Blocks.MaxRows;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.SchoolHolidays];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var link = context.Setting(AccessRight.ManageLocation, "Kalender-Abo für Schulferien anlegen", "masterData", "subscriptions");
        var from = question.From ?? context.Today;
        var until = question.Until ?? from.AddYears(1).AddDays(-1);
        var all = await calendar.SchoolHolidaysAsync(context.Location.Id, context.Today.AddYears(-1), context.Today.AddYears(3), ct);
        if (all.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Schulferien (kein aktives Kalender-Abo für Schulferien am Standort)", $"Kalender-Abos des {context.PlaceOf}", link);
        }

        // Ein Name in der Frage („Herbstferien“) schränkt auf Ferien mit diesem Namen ein.
        var name = question.Subject is { Length: > 0 } wanted ? wanted : null;
        var found = name is null ? all : [.. all.Where(h => h.Title.Contains(name, StringComparison.OrdinalIgnoreCase))];
        string Source(IEnumerable<PeriodHit> hits) => "Abo " + string.Join(", ", hits.Select(h => (h.Source["Abo ".Length..], h.AsOf)).Distinct()
            .Select(s => $"{s.Item1}, {HolidayCalendar.AsOfText(s.AsOf, context.Today, context.Zone)}"));
        var what = name is null ? "Schulferien" : char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];

        switch (question.Detail)
        {
            case "date" when question.From is { } day:
                var onDay = all.Where(h => h.From <= day && day <= h.Until).ToList();
                return onDay.Count > 0
                    ? Blocks.Answer(question.Topic, $"Ja, am {AnswerDates.Long(day)} sind Ferien: {onDay[0].Title}, {Span(onDay[0])}.", Source(onDay))
                    : Blocks.Answer(question.Topic, $"Nein, am {AnswerDates.Long(day)} sind laut Abo keine Schulferien.{Next(all, day)}", Source(all));
            case "year" or "range" when question.From is { } rangeFrom && question.Until is { } rangeUntil:
                var inRange = found.Where(h => h.Until >= rangeFrom && h.From <= rangeUntil).ToList();
                var period = AnswerDates.Describe(rangeFrom, rangeUntil);
                if (inRange.Count == 0)
                {
                    return Blocks.NotFound(question.Topic, $"Im Zeitraum {period} gibt es laut Abo keine {what}.", Source(all), period: period);
                }

                var text = inRange.Count > Show
                    ? $"{what} {period}: {inRange.Count}; die ersten {Show} stehen in der Tabelle."
                    : $"{what} {period}: {inRange.Count}.";
                return Blocks.Answer(question.Topic, text, Source(inRange), TableOf(inRange), period: period);
            default:
                var upcoming = found.Where(h => h.Until >= context.Today).ToList();
                if (upcoming.Count == 0)
                {
                    return Blocks.NotFound(question.Topic, $"Im Abo stehen keine kommenden {what}. Bitte prüfen, ob der Kalender bis zum nächsten Jahr reicht.", Source(all));
                }

                var first = upcoming[0];
                var daysAway = first.From.DayNumber - context.Today.DayNumber;
                var lead = first.From <= context.Today
                    ? $"zurzeit, bis {first.Until.ToString("dd.MM.yyyy", AnswerContext.German)}"
                    : daysAway == 1 ? "morgen" : $"in {daysAway} Tagen";
                return Blocks.Answer(question.Topic, $"Die nächsten {(name is null ? "Schulferien" : what)}: {first.Title}, {Span(first)} ({lead}).", Source(upcoming.Take(5)),
                    TableOf(upcoming.Take(5)));
        }
    }

    /// <summary>„27.10. bis 31.10.2026“, im selben Jahr nur einmal das Jahr; ein einzelner Tag „am 02.11.2026“.</summary>
    private static string Span(PeriodHit hit) =>
        hit.From == hit.Until ? $"am {hit.From.ToString("dd.MM.yyyy", AnswerContext.German)}"
        : hit.From.Year == hit.Until.Year ? $"{hit.From.ToString("dd.MM.", AnswerContext.German)} bis {hit.Until.ToString("dd.MM.yyyy", AnswerContext.German)}"
        : $"{hit.From.ToString("dd.MM.yyyy", AnswerContext.German)} bis {hit.Until.ToString("dd.MM.yyyy", AnswerContext.German)}";

    private static string Next(IReadOnlyList<PeriodHit> all, DateOnly day) =>
        all.FirstOrDefault(h => h.From > day) is { } next ? $" Die nächsten sind {next.Title}, {Span(next)}." : "";

    private static AssistantTable TableOf(IEnumerable<PeriodHit> hits) =>
        Blocks.Table(["Zeitraum", "Ferien"], hits.Select(h => (IReadOnlyList<string>)[Span(h), h.Title]));
}

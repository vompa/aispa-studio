using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann ist der nächste Feiertag?“, „Ist der 1. November ein Feiertag?“, „Feiertage im Dezember“: aus der Feiertagstabelle des Landes und der Region
/// (Bundesland, Kanton) des Standorts (<see cref="PublicHolidays"/>, bewegliche Feiertage über die Osterformel) und aus aktiven Kalender-Abos der Art „gesetzliche
/// Feiertage“ (<see cref="HolidayCalendar"/>, ADR 0075); die Quelle nennt das Abo und den Stand seines Abrufs. Kein externer Dienst bei der Antwort.
/// </summary>
internal sealed class HolidayAnswers(AispaDbContext db, HolidayCalendar holidays) : IAnswerGiver
{
    private const int Show = Blocks.MaxRows;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.Holiday];

    public IReadOnlyCollection<string> Fields(BusinessQuestion question) => [FieldIds.Region];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var place = (await PublicHolidays.LocationsAsync(db, ct))[context.Location.Id];
        var region = PublicHolidays.Region(place.Region);
        var where = region is null ? CountryName(place.Country) : $"{region.Name} ({CountryName(place.Country)})";
        var source = region is null
            ? $"Feiertagstabelle für {CountryName(place.Country)} (für den {context.Place} ist kein Bundesland oder Kanton eingetragen)"
            : $"Feiertagstabelle für {where}, Region des {context.PlaceOf}";
        var note = region is null ? " Für den Standort ist kein Bundesland oder Kanton eingetragen; es gelten nur die landesweiten Feiertage." : "";
        var link = region is null ? context.Fill(FieldIds.Region) : null;

        async Task<IReadOnlyList<(DateOnly Date, string Name, string? Subscription, DateTimeOffset? AsOf)>> In(DateOnly from, DateOnly until) =>
            [.. (await holidays.ForLocationAsync(context.Location.Id, from, until, ct))
                .Select(h => (h.Date, h.Name, h.FromRules ? null : h.Source, h.AsOf))];

        AssistantTable TableOf(IEnumerable<(DateOnly Date, string Name, string? Subscription, DateTimeOffset? AsOf)> found) =>
            Blocks.Table(["Datum", "Feiertag"], found.Select(h => (IReadOnlyList<string>)[AnswerDates.Long(h.Date), h.Name]));

        // „Abo ‚Feiertage Salzburg‘, Stand gestern“: die Quelle nennt jedes Abo, aus dem ein genannter Tag stammt.
        string WithSubscriptions(IEnumerable<(DateOnly Date, string Name, string? Subscription, DateTimeOffset? AsOf)> found) =>
            string.Join("", found.Where(h => h.Subscription is not null).Select(h => (h.Subscription!, h.AsOf)).Distinct()
                .Select(s => $"; ergänzt aus dem {s.Item1}, {HolidayCalendar.AsOfText(s.AsOf, context.Today, context.Zone)}"));

        switch (question.Detail)
        {
            case "date" when question.From is { } day:
                var onDay = await In(day, day);
                var hit = onDay.Count > 0 ? onDay[0] : default;
                var closed = onDay.Count == 0 ? "" : await ClosureNoteAsync(day, context, ct);
                return Blocks.Answer(question.Topic, onDay.Count == 0
                    ? $"Nein, am {AnswerDates.Long(day)} ist in {where} kein gesetzlicher Feiertag.{note}"
                    : $"Ja, am {AnswerDates.Long(day)} ist {hit.Name} (gesetzlicher Feiertag in {where}).{note}{closed}", source + WithSubscriptions(onDay), link: link);
            case "year" or "range" when question.From is { } from && question.Until is { } until:
                var list = await In(from, until);
                var period = AnswerDates.Describe(from, until);
                var text = list.Count == 0
                    ? $"Im Zeitraum {period} gibt es in {where} keinen gesetzlichen Feiertag.{note}"
                    : list.Count > Show
                        ? $"Gesetzliche Feiertage in {where}, {period}: {list.Count}; die ersten {Show} stehen in der Tabelle.{note}"
                        : $"Gesetzliche Feiertage in {where}, {period}: {list.Count}.{note}";
                return Blocks.Answer(question.Topic, text, source + WithSubscriptions(list), list.Count == 0 ? null : TableOf(list), link, period: period);
            default:
                var next = await In(context.Today, context.Today.AddYears(1).AddDays(-1));
                if (next.Count == 0)
                {
                    return Blocks.NotFound(question.Topic, $"In den nächsten zwölf Monaten ist in {where} kein gesetzlicher Feiertag hinterlegt.", source);
                }

                var first = next[0];
                var days = first.Date.DayNumber - context.Today.DayNumber;
                var lead = days == 0 ? "heute" : days == 1 ? "morgen" : $"in {days} Tagen";
                return Blocks.Answer(question.Topic, $"Der nächste Feiertag in {where} ist {first.Name}, {AnswerDates.Long(first.Date)} ({lead}).{note}{await ClosureNoteAsync(first.Date, context, ct)}",
                    source + WithSubscriptions(next.Take(5)), TableOf(next.Take(5)), link);
        }
    }

    /// <summary>Liegt der Tag in einer Schließzeit des Standorts (ADR 0073), steht das hinter dem Feiertag: „An diesem Tag ist geschlossen: Betriebsurlaub bis 2.1.“</summary>
    private async Task<string> ClosureNoteAsync(DateOnly day, AnswerContext context, CancellationToken ct)
    {
        var (from, until) = LocalTime.Day(day, context.Zone);
        return (await ClosureService.InAsync(db, context.Location.Id, from, until, context.Zone, ct)).FirstOrDefault() is { } closure
            ? $" An diesem Tag ist geschlossen: {ClosureText.Describe(closure, day)} (Schließzeit des {context.PlaceOf})."
            : "";
    }

    private static string CountryName(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => "Österreich",
        FiscalCountry.DE => "Deutschland",
        _ => "Schweiz",
    };
}

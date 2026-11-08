using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann ist der Paarraum morgen frei?“ und „Ist der Hamam gesperrt?“ (Slice 136): die freien Zeiten eines Raums an einem Tag aus Öffnungszeit, Belegung im Kalender und Raumsperren
/// (ADR 0076), und die Raumsperren mit Art und Grund. Heute zählt erst ab jetzt. Feiertage und Schließzeiten des Standorts gehören zur Öffnungszeit (Thema „Öffnungszeiten“) und
/// ändern diese Zeiten hier nicht; ein Mehrplatzraum gilt wie in „Welcher Raum ist frei?“ als belegt, sobald ein Platz gebucht ist.
/// </summary>
internal sealed class RoomTimesAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.RoomFreeTimes];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var source = $"Belegung (Kalender), Öffnungszeiten und Raumsperren des {context.PlaceOf}";
        var closure = question.Detail == "closure";
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == context.Location.Id && r.IsActive && (closure || !r.IsArea)).OrderBy(r => r.Name).ToListAsync(ct);
        if (rooms.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Räume", $"Räume des {context.PlaceOf}", context.Fill(FieldIds.Rooms));
        }

        var named = question.Subject is { Length: > 0 } text ? rooms.Where(r => NameMatch.Mentions(text, r.Name)).ToList() : [];
        var first = question.From ?? context.Today;
        var until = question.Until is { } end && end >= first ? end : first;
        return closure ? await ClosuresAsync(question, rooms, named, first, until, context, source, ct) : await FreeTimesAsync(question, rooms, named, first, context, source, ct);
    }

    // ---------- Raumsperren ----------

    private async Task<AssistantBlock> ClosuresAsync(
        BusinessQuestion question, List<Room> rooms, List<Room> named, DateOnly first, DateOnly until, AnswerContext context, string source, CancellationToken ct)
    {
        var from = LocalTime.Day(first, context.Zone).From;
        var to = LocalTime.Day(until, context.Zone).Until;
        var closures = await RoomClosureService.InAsync(db, context.Location.Id, from, to, context.Zone, ct);
        var scope = named.Count > 0 ? named : rooms;
        var sentences = scope.SelectMany(r => closures.Where(c => c.RoomId == r.Id).Select(c => ClosureSentence(r.Name, c)))
            .Concat(scope.Where(r => closures.All(c => c.RoomId != r.Id) && named.Count > 0).Select(r => $"{r.Name} ist {When(first, until)} nicht gesperrt: keine Raumsperre eingetragen."))
            .ToList();
        if (sentences.Count == 0)
        {
            return Blocks.Answer(question.Topic, $"Im {context.Place} ist {When(first, until)} kein Raum gesperrt: keine Raumsperre eingetragen.", source);
        }

        return Blocks.Answer(question.Topic, string.Join(' ', sentences), source);
    }

    private static string ClosureSentence(string room, RoomClosure closure)
    {
        var title = RoomClosureText.Title(closure.Kind);
        var why = string.IsNullOrWhiteSpace(closure.Reason) ? title : $"{title}: {closure.Reason.Trim()}";
        var from = closure.FromTime is { } fromTime ? $" ab {fromTime.ToString("HH:mm", AnswerContext.German)} Uhr" : "";
        var until = closure.UntilTime is { } untilTime ? $" bis {untilTime.ToString("HH:mm", AnswerContext.German)} Uhr" : "";
        var when = closure.FromDate == closure.UntilDate
            ? $"am {AnswerDates.Long(closure.FromDate)}{from}{until}"
            : $"vom {closure.FromDate.ToString("dd.MM.yyyy", AnswerContext.German)}{from} bis {closure.UntilDate.ToString("dd.MM.yyyy", AnswerContext.German)}{until}";
        return $"{room} ist {when} gesperrt ({why}).";
    }

    private static string When(DateOnly first, DateOnly until) => first == until ? $"am {AnswerDates.Long(first)}" : AnswerDates.Describe(first, until);

    // ---------- Freie Zeiten ----------

    private async Task<AssistantBlock> FreeTimesAsync(
        BusinessQuestion question, List<Room> rooms, List<Room> named, DateOnly day, AnswerContext context, string source, CancellationToken ct)
    {
        var when = AnswerDates.Long(day);
        if (named.Count == 0)
        {
            return Blocks.NotFound(question.Topic, "Welcher Raum ist gemeint? Im Standort stehen zur Wahl:", source,
                Blocks.Table(["Raum"], rooms.Select(r => (IReadOnlyList<string>)[r.Name])));
        }

        var hours = await db.Set<LocationOpeningHours>().AsNoTracking().Where(h => h.LocationId == context.Location.Id && h.Day == day.DayOfWeek).OrderBy(h => h.Opens).ToListAsync(ct);
        if (hours.Count == 0)
        {
            return Blocks.Answer(question.Topic, $"Am {when} hat der {context.Place} nicht geöffnet.", source);
        }

        var (dayFrom, dayUntil) = LocalTime.Day(day, context.Zone);
        var ids = named.Select(r => r.Id).ToList();
        var bookings = await db.ResourceBookings.AsNoTracking().Where(b => b.Kind == ResourceKind.Room && ids.Contains(b.ResourceId) && b.From < dayUntil && b.Until > dayFrom).ToListAsync(ct);
        var closures = (await RoomClosureService.InAsync(db, context.Location.Id, dayFrom, dayUntil, context.Zone, ct)).Where(c => ids.Contains(c.RoomId)).ToList();
        var open = hours.Select(h => (From: LocalTime.ToUtc(day, h.Opens, context.Zone), Until: LocalTime.ToUtc(day, h.Closes, context.Zone)))
            .Select(h => day == context.Today && h.From < context.Now ? (From: context.Now, h.Until) : h).Where(h => h.From < h.Until).ToList();

        var lines = new List<string>();
        foreach (var room in named)
        {
            var busy = bookings.Where(b => b.ResourceId == room.Id).Select(b => (b.From, b.Until))
                .Concat(closures.Where(c => c.RoomId == room.Id).Select(c => (From: c.StartsAt(context.Zone), Until: c.EndsAt(context.Zone)))).ToList();
            var free = open.SelectMany(o => Subtract(o, busy)).ToList();
            var covering = closures.Where(c => c.RoomId == room.Id && open.All(o => c.StartsAt(context.Zone) <= o.From && c.EndsAt(context.Zone) >= o.Until)).ToList();
            lines.Add(covering.Count > 0 && open.Count > 0 ? string.Join(' ', covering.Select(c => ClosureSentence(room.Name, c)))
                : free.Count == 0 ? $"{room.Name} ist am {when} nicht frei."
                : $"{room.Name} ist am {when} frei: {Spans(free, context)} Uhr.");
        }

        return Blocks.Answer(question.Topic, string.Join(' ', lines), source);
    }

    /// <summary>Was von einem Zeitraum übrig bleibt, wenn die belegten Zeiträume abgezogen sind.</summary>
    private static IEnumerable<(DateTimeOffset From, DateTimeOffset Until)> Subtract((DateTimeOffset From, DateTimeOffset Until) span, List<(DateTimeOffset From, DateTimeOffset Until)> busy)
    {
        var cursor = span.From;
        foreach (var b in busy.Where(b => b.Until > span.From && b.From < span.Until).OrderBy(b => b.From))
        {
            if (b.From > cursor)
            {
                yield return (cursor, b.From);
            }

            cursor = b.Until > cursor ? b.Until : cursor;
        }

        if (cursor < span.Until)
        {
            yield return (cursor, span.Until);
        }
    }

    private static string Spans(List<(DateTimeOffset From, DateTimeOffset Until)> spans, AnswerContext context)
    {
        var parts = spans.Select(s => $"{context.Time(s.From)}–{context.Time(s.Until)}").ToList();
        return parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts.Take(parts.Count - 1))} und {parts[^1]}";
    }
}

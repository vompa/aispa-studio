using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Welcher Raum ist jetzt frei?“ und „Ist die Sauna voll?“: aus der Belegung der Räume im Kalender und dem Verlauf der Eintritte in Bereiche
/// (<see cref="AreaVisitService"/>). „Jetzt“ ist die Uhrzeit der Frage in Ortszeit des Standorts.
/// </summary>
internal sealed class RoomAnswers(AispaDbContext db, AreaVisitService areas) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.RoomFree, BusinessTopic.AreaLoad];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        return question.Topic == BusinessTopic.AreaLoad ? await AreaAsync(question, context, ct) : await RoomsAsync(question, context, ct);
    }

    private async Task<AssistantBlock> RoomsAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var source = $"Belegung (Kalender) der Räume des {context.PlaceOf}, Stand {context.Time(context.Now)} Uhr";
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == context.Location.Id && r.IsActive && !r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);
        if (rooms.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Räume", $"Räume des {context.PlaceOf}", context.Fill(FieldIds.Rooms));
        }

        var ids = rooms.Select(r => r.Id).ToList();
        // Nur die Belegungen bis zum Ende des heutigen Tages: „frei bis 10:40 Uhr“ darf nicht den nächsten Morgen meinen.
        var endOfDay = LocalTime.Day(context.Today, context.Zone).Until;
        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.Kind == ResourceKind.Room && ids.Contains(b.ResourceId) && b.Until > context.Now && b.From < endOfDay)
            .Select(b => new { b.ResourceId, b.From, b.Until })
            .ToListAsync(ct);
        var rows = rooms.Select(r =>
        {
            var own = bookings.Where(b => b.ResourceId == r.Id).OrderBy(b => b.From).ToList();
            var current = own.FirstOrDefault(b => b.From <= context.Now);
            if (current is not null)
            {
                // Zusammenhängende Belegungen zählen als eine.
                var until = current.Until;
                foreach (var next in own.Where(b => b.From > context.Now).OrderBy(b => b.From))
                {
                    if (next.From <= until)
                    {
                        until = next.Until > until ? next.Until : until;
                    }
                }

                return (Room: r, Free: false, Text: $"belegt bis {context.Time(until)} Uhr");
            }

            var upcoming = own.FirstOrDefault();
            return (Room: r, Free: true, Text: upcoming is null ? "frei, heute nichts mehr gebucht" : $"frei bis {context.Time(upcoming.From)} Uhr");
        }).ToList();
        var wanted = question.Subject is { Length: > 0 } name && rooms.Any(r => NameMatch.Mentions(name, r.Name)) ? rows.Where(r => NameMatch.Mentions(name, r.Room.Name)).ToList() : rows;
        var free = wanted.Where(r => r.Free).ToList();
        var busy = wanted.Where(r => !r.Free).ToList();
        var text = free.Count == 0
            ? $"Jetzt ({context.Time(context.Now)} Uhr) ist im {context.Place} kein Raum frei."
            : $"Jetzt ({context.Time(context.Now)} Uhr) {(free.Count == 1 ? "ist" : "sind")} frei: {string.Join(", ", free.Select(r => r.Room.Name))}.";
        if (busy.Count > 0 && free.Count > 0)
        {
            text += $" Belegt: {string.Join(", ", busy.Select(r => r.Room.Name))}.";
        }

        return Blocks.Answer(question.Topic, text, source, Blocks.Table(["Raum", "Stand"], wanted.Select(r => (IReadOnlyList<string>)[r.Room.Name, r.Text])));
    }

    private async Task<AssistantBlock> AreaAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var day = question.From ?? context.Today;
        var all = await areas.DayAsync(context.Location.Id, day, ct) ?? [];
        var source = $"Bereiche und Eintritte (Kalender) des {context.PlaceOf}";
        if (all.Count == 0)
        {
            return Blocks.NotStored(question.Topic, "Bereiche wie Sauna oder Ruheraum", source, context.Fill(FieldIds.Areas));
        }

        var wanted = question.Subject is { Length: > 0 } name && all.Any(a => Mentions(a.Name, name)) ? [.. all.Where(a => Mentions(a.Name, name))] : all;
        var now = day == context.Today;
        var rows = wanted.Select(a =>
        {
            var occupied = now
                ? a.Load.LastOrDefault(l => l.Start <= context.Now)?.Occupied ?? 0
                : a.Load.Select(l => l.Occupied).DefaultIfEmpty(0).Max();
            return (Area: a, Occupied: Math.Min(occupied, a.Places));
        }).ToList();
        var text = string.Join(" ", rows.Select(r => now
            ? $"{r.Area.Name}: {r.Occupied} von {r.Area.Places} Plätzen belegt, {r.Area.Places - r.Occupied} frei."
            : $"{r.Area.Name}: am {AnswerDates.Long(day)} höchstens {r.Occupied} von {r.Area.Places} Plätzen gleichzeitig belegt."));
        return Blocks.Answer(question.Topic, now ? $"Jetzt ({context.Time(context.Now)} Uhr) — {text}" : text, source,
            Blocks.Table(["Bereich", "Plätze", now ? "Belegt jetzt" : "Höchstens belegt", "Frei"],
                rows.Select(r => (IReadOnlyList<string>)[r.Area.Name, r.Area.Places.ToString(AnswerContext.German), r.Occupied.ToString(AnswerContext.German),
                    (r.Area.Places - r.Occupied).ToString(AnswerContext.German)])));
    }

    /// <summary>„Sauna“ in der Frage trifft „Finnische Sauna“; „Ruheraum“ trifft „Ruheraum Nord“.</summary>
    private static bool Mentions(string areaName, string subject) =>
        NameMatch.Key(areaName).Contains(NameMatch.Key(subject), StringComparison.Ordinal) && NameMatch.Key(subject).Length >= 3;
}

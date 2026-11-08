using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Termine am Standort der Frage (Ä317): „Wer kommt als Nächstes?“ (die nächsten fünf gebuchten Termine ab jetzt, mit Tageszeit nur in diesem Zeitraum), „Wer ist noch
/// nicht da?“ (laufende Termine ohne Einchecken) und „Wer hat heute abgesagt?“ (heute abgesagt, nicht vom Betrieb; „spät“ innerhalb der Stornofrist). Je Zeile ein Sprung
/// in den Kalender. Therapeut:innen sehen nur eigene Termine.
/// </summary>
internal sealed class ArrivalAnswers(AispaDbContext db, OnlineRules rules) : IAnswerGiver
{
    private const int MaxNext = 5;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.NextArrivals, BusinessTopic.NotCheckedIn, BusinessTopic.CancelledToday];

    private sealed record Row(Guid Id, DateTimeOffset Start, DateTimeOffset End, DateTimeOffset UpdatedAt, string FirstName, string LastName, string? PreferredName,
        Guid CustomerId, Guid TreatmentId, string Treatment, Guid EmployeeId, Guid LocationId, string? CancelReason)
    {
        public string Guest => PersonNames.InHouse(FirstName, LastName, PreferredName);
    }

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var query = GuestFinder.Visible(db.Appointments.AsNoTracking(), context).Where(a => a.LocationId == context.Location.Id);
        var employees = (await db.Employees.AsNoTracking().ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        string Detail(Row row) => context.OwnOnly ? row.Treatment : $"{row.Treatment} · {employees.GetValueOrDefault(row.EmployeeId)}";
        var source = context.OwnOnly ? $"Ihre Termine im Kalender des {context.PlaceOf}" : $"Kalender des {context.PlaceOf}";
        var where = context.OwnOnly ? "für Sie" : $"im {context.Place}";
        return question.Topic switch
        {
            BusinessTopic.NextArrivals => await NextAsync(question, query, context, Detail, source, where, ct),
            BusinessTopic.NotCheckedIn => await NotCheckedInAsync(question, query, context, Detail, source, ct),
            _ => await CancelledAsync(question, query, context, source, where, ct),
        };
    }

    private static IQueryable<Row> Rows(IQueryable<Appointment> query) =>
        query.Select(a => new Row(a.Id, a.Start, a.End, a.UpdatedAt, a.Customer.FirstName, a.Customer.LastName, a.Customer.PreferredName, a.CustomerId, a.TreatmentId, a.Treatment.Name, a.EmployeeId, a.LocationId,
            a.CancelReason));

    private static AssistantSuggestion Show(Row row, AnswerContext context) =>
        new(AssistantSuggestionKind.OpenAppointment, $"{context.Time(row.Start)} {row.Treatment}", row.LocationId, row.TreatmentId, row.EmployeeId, row.CustomerId, row.Id,
            row.Start);

    private static async Task<AssistantBlock> NextAsync(BusinessQuestion question, IQueryable<Appointment> query, AnswerContext context, Func<Row, string> detail,
        string source, string where, CancellationToken ct)
    {
        var from = context.Now;
        DateTimeOffset? until = null;
        if (question.Part is { } part && part != DayPart.Any)
        {
            // „Wer kommt heute nachmittag?“: nur dieser Zeitraum, und davon nur, was noch kommt.
            var window = AnswerDates.Window(question, context.Today, context.Zone);
            from = window.From > context.Now ? window.From : context.Now;
            until = window.Until;
        }

        var rows = await Rows(query.Where(a => a.Status == AppointmentStatus.Booked && a.Start >= from && (until == null || a.Start < until))
                .OrderBy(a => a.Start).ThenBy(a => a.Id)
                .Take(MaxNext))
            .ToListAsync(ct);
        var items = rows.Select(r => new AssistantItem(r.Guest, detail(r),
            GuestFinder.Local(r.Start, context) == context.Today ? context.Time(r.Start) : $"{GuestFinder.Local(r.Start, context).ToString("dd.MM.", AnswerContext.German)} {context.Time(r.Start)}",
            Show(r, context), null, "Im Kalender zeigen")).ToList();
        var partWord = AnswerDates.PartWord(question.Part);
        var text = $"Als Nächstes {where}{(partWord == "" ? "" : $" ({partWord})")}: " + (rows.Count == 0 ? "keine Termine." : $"{rows.Count} Termin{(rows.Count == 1 ? "" : "e")}.");
        return Blocks.Answer(question.Topic, text, source, items: items);
    }

    private static async Task<AssistantBlock> NotCheckedInAsync(BusinessQuestion question, IQueryable<Appointment> query, AnswerContext context, Func<Row, string> detail,
        string source, CancellationToken ct)
    {
        var now = context.Now;
        var rows = await Rows(query.Where(a => a.Status == AppointmentStatus.Booked && a.Start <= now && a.End > now)
                .OrderBy(a => a.Start).ThenBy(a => a.Id)
                .Take(Blocks.MaxItems))
            .ToListAsync(ct);
        var items = rows.Select(r => new AssistantItem(r.Guest, $"{detail(r)} · seit {(int)(now - r.Start).TotalMinutes} Min.", context.Time(r.Start), Show(r, context), null,
            "Im Kalender zeigen")).ToList();
        var text = rows.Count switch
        {
            0 => "Alle Gäste mit laufendem Termin sind eingecheckt.",
            1 => "1 Gast ist noch nicht eingecheckt.",
            _ => $"{rows.Count} Gäste sind noch nicht eingecheckt.",
        };
        return Blocks.Answer(question.Topic, text, source, items: items);
    }

    private async Task<AssistantBlock> CancelledAsync(BusinessQuestion question, IQueryable<Appointment> query, AnswerContext context, string source, string where,
        CancellationToken ct)
    {
        var day = question.From ?? context.Today;
        var (from, until) = LocalTime.Day(day, context.Zone);
        var rows = await Rows(query.Where(a => a.Status == AppointmentStatus.Cancelled && !a.CancelledByBusiness && a.UpdatedAt >= from && a.UpdatedAt < until)
                .OrderBy(a => a.UpdatedAt).ThenBy(a => a.Id)
                .Take(Blocks.MaxItems))
            .ToListAsync(ct);
        var deadline = TimeSpan.FromHours((await rules.ForAsync(context.Location.Id, ct)).CancellationDeadlineHours);
        var items = rows.Select(r =>
        {
            var reason = string.IsNullOrWhiteSpace(r.CancelReason) ? "" : $" · Grund: {r.CancelReason.Trim()}";
            var late = r.Start - r.UpdatedAt < deadline ? " · spät" : "";
            return new AssistantItem(r.Guest, $"abgesagt für {GuestFinder.When(r.Start, context)} · {r.Treatment}{reason}{late}", context.Time(r.UpdatedAt), Show(r, context),
                null, "Im Kalender zeigen");
        }).ToList();
        var when = day == context.Today ? "Heute" : $"Am {AnswerDates.Long(day)}";
        var text = rows.Count == 0
            ? $"{when} hat {where} niemand abgesagt."
            : $"{when} abgesagt {where}: {rows.Count} Termin{(rows.Count == 1 ? "" : "e")}.";
        return Blocks.Answer(question.Topic, text, source, items: items);
    }
}

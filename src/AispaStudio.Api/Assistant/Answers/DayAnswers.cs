using AispaStudio.Api.Data;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wie viele Termine haben wir heute noch?“ und „Wie voll sind wir am Freitag?“: Anzahl der Termine nach Stand aus dem Kalender, Auslastung laut Dienstplan aus dem
/// Tagesüberblick (<see cref="DailyReports"/>). Therapeut:innen sehen auch hier nur die eigenen Termine.
/// </summary>
internal sealed class DayAnswers(AispaDbContext db, DailyReports reports) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.RemainingAppointments, BusinessTopic.DayLoad];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var day = question.From ?? context.Today;
        return question.Topic == BusinessTopic.DayLoad ? await LoadAsync(day, context, ct) : await CountAsync(question, day, context, ct);
    }

    private async Task<AssistantBlock> CountAsync(BusinessQuestion question, DateOnly day, AnswerContext context, CancellationToken ct)
    {
        var (from, until) = AnswerDates.Window(question, context.Today, context.Zone);
        var part = AnswerDates.PartWord(question.Part);
        var own = !context.Has(AccessRight.Book) && !context.Has(AccessRight.ViewFinance);
        var query = db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == context.Location.Id && a.Start >= from && a.Start < until && a.Status != AppointmentStatus.Cancelled);
        if (own)
        {
            var employee = context.Access.EmployeeId ?? Guid.Empty;
            query = query.Where(a => a.EmployeeId == employee);
        }

        var appointments = await query.Select(a => new { a.Status, a.Start, a.End }).ToListAsync(ct);
        var who = own ? "für Sie" : $"im {context.Place}";
        var source = own ? $"Ihre Termine im Kalender des {context.PlaceOf}" : $"Kalender des {context.PlaceOf}";
        var booked = appointments.Count(a => a.Status == AppointmentStatus.Booked);
        var checkedIn = appointments.Count(a => a.Status == AppointmentStatus.CheckedIn);
        var done = appointments.Count(a => a.Status == AppointmentStatus.Completed);
        var noShow = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        var table = Blocks.Table(["Stand", "Termine"],
        [
            ["Gebucht", booked.ToString(AnswerContext.German)],
            ["Eingecheckt", checkedIn.ToString(AnswerContext.German)],
            ["Abgeschlossen", done.ToString(AnswerContext.German)],
            ["Nicht erschienen", noShow.ToString(AnswerContext.German)],
        ]);

        if (question.Detail == "remaining" && day == context.Today)
        {
            var upcoming = appointments.Count(a => a.Status == AppointmentStatus.Booked && a.Start >= context.Now);
            var running = appointments.Count(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn && a.Start < context.Now && a.End > context.Now);
            var text = upcoming + running == 0
                ? $"Heute stehen {who} keine Termine mehr an."
                : $"Heute stehen {who} noch {upcoming} Termin{(upcoming == 1 ? "" : "e")} an"
                    + (running > 0 ? running == 1 ? "; 1 läuft gerade" : $"; {running} laufen gerade" : "") + $". Insgesamt sind es heute {appointments.Count}, davon {done} abgeschlossen.";
            return Blocks.Answer(question.Topic, text, source, table);
        }

        return Blocks.Answer(question.Topic,
            appointments.Count == 0
                ? $"Am {AnswerDates.Long(day)}{(part == "" ? "" : " " + part)} stehen {who} keine Termine im Kalender."
                : $"Am {AnswerDates.Long(day)}{(part == "" ? "" : " " + part)} stehen {who} {appointments.Count} Termin{(appointments.Count == 1 ? "" : "e")} im Kalender.",
            source, table);
    }

    private async Task<AssistantBlock> LoadAsync(DateOnly day, AnswerContext context, CancellationToken ct)
    {
        var source = $"Tagesüberblick (Kalender und Dienstplan) des {context.PlaceOf}";
        var briefing = await reports.BriefingAsync(context.Location.Id, day, ct);
        if (briefing is null)
        {
            return Blocks.NotFound(BusinessTopic.DayLoad, "Zu diesem Standort gibt es keinen Tagesüberblick.", source);
        }

        var text = briefing.Appointments == 0
            ? $"Am {AnswerDates.Long(day)} sind im {context.Place} noch keine Termine gebucht."
            : $"Am {AnswerDates.Long(day)} sind im {context.Place} {briefing.Appointments} Termin{(briefing.Appointments == 1 ? "" : "e")} gebucht; "
                + $"die Auslastung laut Dienstplan liegt bei {briefing.Utilization} %.";
        return Blocks.Answer(BusinessTopic.DayLoad, text, source,
            Blocks.Table(["Kennzahl", "Wert"], [["Termine", briefing.Appointments.ToString(AnswerContext.German)], ["Auslastung laut Dienstplan", $"{briefing.Utilization} %"]]));
    }
}

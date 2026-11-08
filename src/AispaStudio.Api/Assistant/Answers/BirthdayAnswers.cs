using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wer hat heute Geburtstag?“, „Wer hat diese Woche Geburtstag?“ (Runde 5b, Ä45): nur Gäste, die im Zeitraum einen Termin am Standort haben, nie eine Liste des ganzen
/// Kundenstamms. Die Tabelle nennt Gast, Tag des Geburtstags und die Uhrzeit des Termins (liegt er an einem anderen Tag, mit Datum); das Alter steht nicht dabei.
/// Wer nur die eigenen Termine sieht (Therapie), bekommt nur Gäste aus den eigenen Terminen. Der 29. Februar zählt in Jahren ohne Schalttag am 28.
/// </summary>
internal sealed class BirthdayAnswers(AispaDbContext db) : IAnswerGiver
{
    /// <summary>Längster Zeitraum, nach dem die Frage fragen darf.</summary>
    private const int MaxDays = 31;

    private const int NamedGuests = 3;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.Birthdays];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var from = question.From ?? context.Today;
        var until = question.Until is { } end && end >= from ? end : from;
        if (until.DayNumber - from.DayNumber >= MaxDays)
        {
            until = from.AddDays(MaxDays - 1);
        }

        var period = AnswerDates.Describe(from, until);
        var own = !context.Has(AccessRight.Book) && !context.Has(AccessRight.ViewFinance);
        var source = own ? "Ihre Termine und Kundendaten" : "Termine und Kundendaten";
        var (start, _) = LocalTime.Day(from, context.Zone);
        var (_, stop) = LocalTime.Day(until, context.Zone);
        var query = db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == context.Location.Id && a.Start >= start && a.Start < stop && a.PlaceholderGuest == null
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.Completed));
        if (own)
        {
            var employee = context.Access.EmployeeId ?? Guid.Empty;
            query = query.Where(a => a.EmployeeId == employee);
        }

        var appointments = await query.Select(a => new { a.CustomerId, a.Start }).ToListAsync(ct);
        var ids = appointments.Select(a => a.CustomerId).Distinct().ToList();
        var guests = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id) && c.BirthDate != null).ToListAsync(ct);

        var rows = new List<(string Name, DateOnly Birthday, DateTimeOffset At, bool SameDay)>();
        foreach (var guest in guests)
        {
            var birthday = Within(guest.BirthDate!.Value, from, until);
            if (birthday is null)
            {
                continue;
            }

            var theirs = appointments.Where(a => a.CustomerId == guest.Id).OrderBy(a => a.Start).ToList();
            var onTheDay = theirs.FirstOrDefault(a => Local(a.Start, context) == birthday);
            var shown = onTheDay ?? theirs[0];
            rows.Add((guest.InHouseName, birthday.Value, shown.Start, onTheDay is not null));
        }

        rows = [.. rows.OrderBy(r => r.Birthday).ThenBy(r => r.At).ThenBy(r => r.Name, StringComparer.Ordinal)];
        var when = from == until ? (from == context.Today ? "Heute" : $"Am {AnswerDates.Long(from)}") : $"Im Zeitraum {period}";
        var table = rows.Count == 0
            ? null
            : Blocks.Table(["Gast", "Datum", "Uhrzeit"], rows.Select(r => (IReadOnlyList<string>)
                [r.Name, AnswerDates.Long(r.Birthday), r.SameDay ? $"{context.Time(r.At)} Uhr" : $"{AnswerDates.Long(Local(r.At, context))}, {context.Time(r.At)} Uhr"]));
        var text = rows.Count == 0
            ? $"{when} hat kein Gast mit Termin im {context.Place} Geburtstag."
            : $"{when} {(rows.Count == 1 ? "hat" : "haben")} {Names(rows.Select(r => r.Name).ToList())} Geburtstag (Gäste mit Termin im {context.Place}).";
        return Blocks.Answer(question.Topic, text, source, table, period: from == until ? null : period);
    }

    /// <summary>Der Geburtstag im Zeitraum, falls einer darin liegt (der Zeitraum kann über den Jahreswechsel gehen).</summary>
    private static DateOnly? Within(DateOnly birth, DateOnly from, DateOnly until)
    {
        for (var year = from.Year; year <= until.Year; year++)
        {
            var date = Birthdays.InYear(birth, year);
            if (date >= from && date <= until)
            {
                return date;
            }
        }

        return null;
    }

    private static DateOnly Local(DateTimeOffset at, AnswerContext context) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, context.Zone).DateTime);

    private static string Names(List<string> names) =>
        names.Count <= NamedGuests
            ? names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} und {names[^1]}"
            : $"{string.Join(", ", names.Take(NamedGuests))} u. a.";
}

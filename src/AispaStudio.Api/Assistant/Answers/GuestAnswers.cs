using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann war Frau Müller zuletzt da?“ und „Hat Herr Berger nächste Woche einen Termin?“: aus den Terminen im Kalender. Die Antwort nennt nur Tag, Behandlung und
/// Person, keine Kontaktdaten, Notizen oder Gesundheitsangaben, und nur Termine an Standorten, die die Person sehen darf.
/// Namen dürfen unscharf sein (Ä315): klein geschrieben, mit Anrede, Rufname oder einem Tippfehler (<see cref="NameMatch"/>). Passen mehrere Gäste, bietet die Antwort
/// sie zur Auswahl an; ohne Namen meint die Frage den Gast, auf den sie sich bezieht (offener Termin, Kundenkarte, zuletzt genannter Gast).
/// </summary>
internal sealed class GuestAnswers(AispaDbContext db, GuestFinder finder) : IAnswerGiver
{
    private const string Source = "Termine im Kalender";

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.GuestLastVisit, BusinessTopic.GuestNextVisit];

    private sealed record Visit(Guid CustomerId, DateTimeOffset Start, AppointmentStatus Status, string Treatment, Guid EmployeeId);

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var found = await finder.FindAsync(question, context, Source, ct);
        if (found.Block is { } notFound)
        {
            return notFound;
        }

        if (found.Guests.Count > 1)
        {
            return await finder.ChoiceAsync(question, found, context, Source, ct);
        }

        var guest = found.Guests[0];
        var visits = await GuestFinder.Visible(db.Appointments.AsNoTracking(), context)
            .Where(a => a.CustomerId == guest.Id)
            .Select(a => new Visit(a.CustomerId, a.Start, a.Status, a.Treatment.Name, a.EmployeeId))
            .ToListAsync(ct);
        var employees = (await db.Employees.AsNoTracking().ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        var block = question.Topic == BusinessTopic.GuestLastVisit ? Last(guest, visits, context)
            : question.Detail == "next" ? NextOrLast(guest, visits, employees, context)
            : Next(question, guest, visits, employees, context);
        return block with { GuestId = guest.Id };
    }

    private static Visit? Upcoming(List<Visit> visits, GuestFinder.Guest guest, AnswerContext context) =>
        visits.Where(v => v.CustomerId == guest.Id && v.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn && v.Start >= context.Now.AddHours(-1))
            .OrderBy(v => v.Start).FirstOrDefault();

    private static Visit? LastVisit(List<Visit> visits, GuestFinder.Guest guest, AnswerContext context) =>
        visits.Where(v => v.CustomerId == guest.Id && v.Start <= context.Now && v.Status is AppointmentStatus.Completed or AppointmentStatus.CheckedIn)
            .OrderByDescending(v => v.Start).FirstOrDefault();

    private static string When(DateTimeOffset start, AnswerContext context) => GuestFinder.When(start, context);

    private static DateOnly Local(DateTimeOffset at, AnswerContext context) => GuestFinder.Local(at, context);

    private static AssistantBlock Last(GuestFinder.Guest guest, List<Visit> visits, AnswerContext context) =>
        LastVisit(visits, guest, context) is { } last
            ? Blocks.Answer(BusinessTopic.GuestLastVisit, $"{guest.Name} war zuletzt am {When(last.Start, context)} da ({last.Treatment}).", Source)
            : Blocks.Answer(BusinessTopic.GuestLastVisit, $"Für {guest.Name} ist kein abgeschlossener Besuch im Kalender eingetragen.", Source);

    /// <summary>„Wann hat er seinen Termin?“ ohne Zeitraum: der nächste Termin, ohne kommenden der letzte Besuch.</summary>
    private static AssistantBlock NextOrLast(GuestFinder.Guest guest, List<Visit> visits, Dictionary<Guid, string> employees, AnswerContext context)
    {
        if (Upcoming(visits, guest, context) is { } next)
        {
            var person = employees.GetValueOrDefault(next.EmployeeId);
            return Blocks.Answer(BusinessTopic.GuestNextVisit,
                $"{guest.Name} hat den nächsten Termin am {When(next.Start, context)} ({next.Treatment}{(person is null ? "" : $", {person}")}).", Source);
        }

        return Blocks.Answer(BusinessTopic.GuestNextVisit, LastVisit(visits, guest, context) is { } last
            ? $"{guest.Name} hat keinen Termin geplant. Letzter Besuch: {When(last.Start, context)} ({last.Treatment})."
            : $"{guest.Name} hat keinen Termin geplant und noch keinen Besuch im Kalender.", Source);
    }

    private static AssistantBlock Next(BusinessQuestion question, GuestFinder.Guest guest, List<Visit> visits, Dictionary<Guid, string> employees, AnswerContext context)
    {
        var from = question.From ?? context.Today;
        var until = question.Until ?? from;
        var period = AnswerDates.Describe(from, until);
        var open = visits.Where(v => v.CustomerId == guest.Id && v.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn && v.Start >= context.Now.AddHours(-1))
            .OrderBy(v => v.Start).ToList();
        var inRange = open.Where(v => Local(v.Start, context) >= from && Local(v.Start, context) <= until).ToList();
        if (inRange.Count == 0)
        {
            var later = open.FirstOrDefault(v => Local(v.Start, context) > until);
            return Blocks.Answer(BusinessTopic.GuestNextVisit, $"{guest.Name}: {period} kein Termin"
                + (later is null ? "." : $"; der nächste ist am {When(later.Start, context)} ({later.Treatment})."), Source, period: period);
        }

        var rows = inRange.Select(v => (IReadOnlyList<string>)[AnswerDates.Long(Local(v.Start, context)), $"{context.Time(v.Start)} Uhr", v.Treatment,
            employees.GetValueOrDefault(v.EmployeeId, "")]);
        return Blocks.Answer(BusinessTopic.GuestNextVisit, $"{guest.Name}: {inRange.Count} Termin{(inRange.Count == 1 ? "" : "e")} {period}.", Source,
            Blocks.Table(["Tag", "Uhrzeit", "Behandlung", "Person"], rows), period: period);
    }
}

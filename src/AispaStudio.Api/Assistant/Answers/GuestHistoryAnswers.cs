using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Historie Christian Steiner“ (Ä317): Kopfsatz mit der Zahl der Besuche, darunter als Liste der Sprung in die Kundenkarte, der nächste Termin, die letzten zehn
/// Besuche, Nichterscheinen und Absagen der letzten zwölf Monate und mit dem Recht zu buchen die Zahl der Belege (ohne Beträge). Therapeut:innen sehen nur eigene Termine.
/// </summary>
internal sealed class GuestHistoryAnswers(AispaDbContext db, GuestFinder finder) : IAnswerGiver
{
    private const string Source = "Termine im Kalender";
    private const int MaxVisits = 10;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.GuestHistory];

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
        var appointments = await GuestFinder.Visible(db.Appointments.AsNoTracking(), context)
            .Where(a => a.CustomerId == guest.Id)
            .Select(a => new
            {
                a.Id,
                a.Start,
                a.Status,
                a.CancelledByBusiness,
                a.LocationId,
                a.TreatmentId,
                a.EmployeeId,
                Treatment = a.Treatment.Name,
            })
            .ToListAsync(ct);
        var employees = (await db.Employees.AsNoTracking().ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        string Detail(string treatment, Guid employee) => context.OwnOnly ? treatment : $"{treatment} · {employees.GetValueOrDefault(employee)}";

        var visits = appointments.Where(a => a.Start <= context.Now && (a.Status is AppointmentStatus.Completed or AppointmentStatus.CheckedIn))
            .OrderByDescending(a => a.Start).ToList();
        var next = appointments.Where(a => a.Start >= context.Now && a.Status == AppointmentStatus.Booked).MinBy(a => a.Start);
        var yearAgo = context.Now.AddYears(-1);
        var noShows = appointments.Count(a => a.Status == AppointmentStatus.NoShow && a.Start >= yearAgo);
        var cancelled = appointments.Count(a => a.Status == AppointmentStatus.Cancelled && !a.CancelledByBusiness && a.Start >= yearAgo);

        List<AssistantItem> items =
        [
            new(guest.Name, null, null,
                new AssistantSuggestion(AssistantSuggestionKind.OpenCustomer, guest.Name, null, null, null, guest.Id, null, null), null, "Kundenkarte öffnen"),
            next is null
                ? new("Kein Termin geplant", null, null, null, null, "")
                : new($"Nächster Termin: {AnswerDates.Long(GuestFinder.Local(next.Start, context))}", Detail(next.Treatment, next.EmployeeId), context.Time(next.Start),
                    new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"{context.Time(next.Start)} {next.Treatment}", next.LocationId, next.TreatmentId,
                        next.EmployeeId, guest.Id, next.Id, next.Start), null, "Im Kalender zeigen"),
            .. visits.Take(MaxVisits).Select(v => new AssistantItem(AnswerDates.Long(GuestFinder.Local(v.Start, context)), Detail(v.Treatment, v.EmployeeId),
                context.Time(v.Start),
                new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"{context.Time(v.Start)} {v.Treatment}", v.LocationId, v.TreatmentId, v.EmployeeId,
                    guest.Id, v.Id, v.Start), null, "Im Kalender zeigen")),
        ];
        if (noShows > 0)
        {
            items.Add(new($"Nicht erschienen: {noShows}", "in den letzten 12 Monaten", null, null, null, ""));
        }

        if (cancelled > 0)
        {
            items.Add(new($"Abgesagt: {cancelled}", "in den letzten 12 Monaten, ohne Absagen des Betriebs", null, null, null, ""));
        }

        if (context.Has(AccessRight.Book) && await PurchasesAsync(guest.Id, context, ct) is { } purchases)
        {
            items.Add(purchases);
        }

        var header = context.OwnOnly ? $"{guest.Name}, Ihre Termine" : guest.Name;
        var text = visits.Count == 0
            ? $"{header}: noch kein Besuch."
            : $"{header}: {visits.Count} Besuch{(visits.Count == 1 ? "" : "e")}, zuletzt am {GuestFinder.Local(visits[0].Start, context):dd.MM.yyyy}.";
        var source = context.OwnOnly ? $"Ihre {Source}" : Source;
        return Blocks.Answer(question.Topic, text, source, items: items) with { GuestId = guest.Id };
    }

    /// <summary>„Käufe: 3 Belege, zuletzt am 01.03.2027“: Verkaufsbelege des Gastes an den sichtbaren Standorten, ohne Beträge.</summary>
    private async Task<AssistantItem?> PurchasesAsync(Guid guestId, AnswerContext context, CancellationToken ct)
    {
        var visible = context.Access.LocationIds.ToList();
        var all = context.Access.AllLocations;
        var registers = db.CashRegisters.AsNoTracking().Where(r => all || visible.Contains(r.LocationId)).Select(r => r.Id);
        var receipts = await db.Receipts.AsNoTracking()
            .Where(r => r.CustomerId == guestId && r.Kind == ReceiptKind.Sale && registers.Contains(r.RegisterId))
            .Select(r => r.IssuedAt)
            .ToListAsync(ct);
        return receipts.Count == 0
            ? null
            : new AssistantItem($"Käufe: {receipts.Count} Beleg{(receipts.Count == 1 ? "" : "e")}", $"zuletzt am {GuestFinder.Local(receipts.Max(), context):dd.MM.yyyy}",
                null, null, null, "");
    }
}

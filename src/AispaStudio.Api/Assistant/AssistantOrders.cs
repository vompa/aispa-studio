using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant;

/// <summary>
/// Aufträge an den Assistenten (Ä319, ADR 0072 Nachtrag Slice 147): aus „buche …“, „verschiebe …“, „sag … ab“, „schick … den Bogen“, „biete … an“ und
/// „offene Rechnung …“ wird ein Vorschlag, mit dem die App die bestehende Maske vorausgefüllt öffnet. Gespeichert wird hier nichts; was der Auftrag nicht nennt,
/// bleibt leer und die Antwort sagt es. Gast, Termin, Wunsch und Rechnung kommen aus Kundenstamm, Kalender, Warteliste und Kassa; die KI liest höchstens
/// Behandlung, Person, Tag und Uhrzeit aus dem Satz (<see cref="IBookingAssistant"/>, wie bei den freien Zeiten).
/// </summary>
internal sealed class AssistantOrders(AispaDbContext db, OnlineDbContext online, BusinessAnswers answers, GuestFinder guests, IBookingAssistant reader)
{
    private const string Source = "Kundenstamm und Kalender";

    /// <summary>Text der Antwort, die Vorschläge (höchstens einer) und bei mehreren passenden Gästen die Auswahl.</summary>
    internal sealed record Result(string Text, IReadOnlyList<AssistantSuggestion> Suggestions, AssistantBlock? Block = null);

    /// <summary>Ohne Buchungsrecht öffnet kein Auftrag eine Maske (alle Masken der Aufträge verlangen „Buchen“, auch Kassa und Anamnese-Versand).</summary>
    public static string Denied => "Termine buchen, verschieben und absagen darf, wer das Recht „Buchen“ hat; eine Maske dafür öffnet der Assistent hier nicht.";

    public async Task<Result> AnswerAsync(AssistantCommand command, string question, AnswerContext context, CancellationToken ct)
    {
        if (!context.Has(AccessRight.Book))
        {
            return new(Denied, []);
        }

        var location = context.Location;
        var treatments = await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, location.Id, ct);
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == location.Id)).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);
        string[] known = [location.Name, .. treatments.Select(t => t.Name), .. employees.Select(e => e.DisplayName), .. employees.Select(e => e.PreferredName ?? "")];
        var asked = new BusinessQuestion(BusinessTopic.GuestNextVisit, question, AssistantCommands.GuestName(question, known), null, null);
        var found = asked.Subject is null && context.ChosenGuestId is null && context.ReferredGuestId is null ? null : await guests.FindAsync(asked, context, Source, ct);
        if (found is { Guests.Count: > 1 })
        {
            var choice = await guests.ChoiceAsync(asked, found, context, Source, ct) with { Topic = command.ToString() };
            return new(choice.Text, [], choice);
        }

        var guest = found is { Guests: [var only] } ? only : null;
        if (command == AssistantCommand.Book)
        {
            var analysis = await reader.AnalyzeAsync(RoleAssistant.Request(question, context.Now, location, treatments, employees), ct);
            return Book(analysis, guest, found?.Block, treatments, employees, context);
        }

        if (guest is null && !(command is AssistantCommand.Move or AssistantCommand.Cancel or AssistantCommand.SendQuestionnaire && context.ReferredAppointmentId is not null))
        {
            var missing = found?.Block ?? Blocks.NotFound(BusinessTopic.GuestNextVisit, "Welcher Gast ist gemeint? Bitte den Namen nennen, zum Beispiel „Frau Müller“.", Source);
            return new(missing.Text, [], missing with { Topic = command.ToString() });
        }

        return command switch
        {
            AssistantCommand.Offer => await OfferAsync(guest!, context, ct),
            AssistantCommand.OpenBill => await BillAsync(guest!, context, ct),
            _ => await AppointmentAsync(command, question, guest, treatments, employees, context, ct),
        };
    }

    /// <summary>„Neuer Termin“ mit dem, was der Auftrag nennt; fehlt der Gast, die Behandlung, der Tag oder die Uhrzeit, bleibt das Feld leer.</summary>
    private static Result Book(MailAnalysis analysis, GuestFinder.Guest? guest, AssistantBlock? guestProblem, IReadOnlyList<Treatment> treatments,
        IReadOnlyList<Employee> employees, AnswerContext context)
    {
        var treatment = treatments.FirstOrDefault(t => t.Id == analysis.TreatmentId);
        var employee = employees.FirstOrDefault(e => e.Id == analysis.EmployeeId);
        var wish = analysis.Wishes.OrderBy(w => w.Date).FirstOrDefault();
        var start = wish?.From is { } from ? LocalTime.ToUtc(wish.Date, from, context.Zone) : (DateTimeOffset?)null;
        List<string> parts = [.. new[] { treatment?.Name, employee is null ? null : $"bei {employee.InHouseName}" }.OfType<string>()];
        if (start is { } at)
        {
            parts.Add(GuestFinder.When(at, context));
        }
        else if (wish is not null)
        {
            parts.Add(AnswerDates.Long(wish.Date));
        }

        List<string> missing = [.. new[] { (guest is null, "Gast"), (treatment is null, "Behandlung"), (wish is null, "Tag"), (start is null, "Uhrzeit") }
            .Where(m => m.Item1).Select(m => m.Item2)];
        var text = (guest is null && guestProblem is { Kind: AssistantBlockKind.NotFound } ? $"{guestProblem.Text} " : "")
            + $"Vorschlag: neuer Termin{(guest is null ? "" : $" für {guest.Name}")}{string.Concat(parts.Select(p => $", {p}"))}. Gebucht wird erst mit „Termin buchen“ in der Maske."
            + (missing.Count == 0 ? "" : $" Noch offen: {string.Join(", ", missing)}.");
        return new(text,
        [
            new AssistantSuggestion(AssistantSuggestionKind.NewAppointment, "Neuer Termin …", context.Location.Id, treatment?.Id, employee?.Id, guest?.Id, null, start,
                Day: wish?.Date),
        ]);
    }

    /// <summary>Verschieben, Absagen und Bogen senden: der offene Termin, wenn kein Gast genannt ist, sonst der nächste geplante Termin des Gastes.</summary>
    private async Task<Result> AppointmentAsync(AssistantCommand command, string question, GuestFinder.Guest? guest, IReadOnlyList<Treatment> treatments,
        IReadOnlyList<Employee> employees, AnswerContext context, CancellationToken ct)
    {
        var visible = GuestFinder.Visible(db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer), context)
            .Where(a => a.Status == AppointmentStatus.Booked);
        var now = context.Now;
        var appointment = guest is null
            ? await visible.FirstOrDefaultAsync(a => a.Id == context.ReferredAppointmentId, ct)
            : await visible.Where(a => a.CustomerId == guest.Id && a.Start >= now).OrderBy(a => a.Start).ThenBy(a => a.Id).FirstOrDefaultAsync(ct);
        if (appointment is null)
        {
            return new(guest is null ? "Dieser Termin ist nicht mehr offen." : $"{guest.Name} hat keinen geplanten Termin.", []);
        }

        var name = appointment.Customer.InHouseName;
        var what = $"{appointment.Treatment.Name}, {GuestFinder.When(appointment.Start, context)}";
        AssistantSuggestion Suggestion(AssistantSuggestionKind kind, string label) => new(kind, label, appointment.LocationId, appointment.TreatmentId,
            appointment.EmployeeId, appointment.CustomerId, appointment.Id, appointment.Start);
        switch (command)
        {
            case AssistantCommand.Cancel:
                var fee = await answers.AnswerAsync(new BusinessQuestion(BusinessTopic.CancellationFee, question, null, null, null),
                    context with { ChosenGuestId = null, ReferredAppointmentId = appointment.Id }, ct);
                return new($"Vorschlag: Termin von {name} absagen ({what}). {fee.Text} Abgesagt wird erst mit „Stornieren“ in der Rückfrage.",
                    [Suggestion(AssistantSuggestionKind.CancelAppointment, "Stornieren …") with { Note = fee.Text }]);
            case AssistantCommand.SendQuestionnaire:
                return new($"Vorschlag: Anamnesebogen für {name} zum Termin {what}. Gesendet wird erst im Termin unter „Anamnese“.",
                    [Suggestion(AssistantSuggestionKind.SendQuestionnaire, "Termin mit Anamnese öffnen")]);
            default:
                var analysis = await reader.AnalyzeAsync(RoleAssistant.Request(question, now, context.Location, treatments, employees), ct);
                var wish = analysis.Wishes.OrderBy(w => w.Date).FirstOrDefault();
                var newStart = wish?.From is { } from ? LocalTime.ToUtc(wish.Date, from, context.Zone) : (DateTimeOffset?)null;
                var target = newStart is { } at ? GuestFinder.When(at, context) : wish is null ? "einen neuen Tag" : AnswerDates.Long(wish.Date);
                List<string> missing = [.. new[] { (wish is null, "neuer Tag"), (newStart is null, "Uhrzeit") }.Where(m => m.Item1).Select(m => m.Item2)];
                return new($"Vorschlag: Termin von {name} ({what}) verschieben auf {target}. Verschoben wird erst mit „Verschieben“ in der Maske."
                    + (missing.Count == 0 ? "" : $" Noch offen: {string.Join(", ", missing)}."),
                    [Suggestion(AssistantSuggestionKind.MoveAppointment, "Verschieben …") with { NewStart = newStart, Day = wish?.Date }]);
        }
    }

    /// <summary>„Biete … an“: der wartende Wunsch des Gastes an einem sichtbaren Standort; die App zeigt dazu die freien Zeiten der Warteliste.</summary>
    private async Task<Result> OfferAsync(GuestFinder.Guest guest, AnswerContext context, CancellationToken ct)
    {
        var today = context.Today;
        var entries = await online.WaitlistEntries.AsNoTracking()
            .Where(w => w.CustomerId == guest.Id && w.Status == WaitlistStatus.Waiting && w.Until >= today)
            .OrderBy(w => w.From).ThenBy(w => w.CreatedAt).ThenBy(w => w.Id).ToListAsync(ct);
        if (entries.FirstOrDefault(e => context.Access.CanSee(e.LocationId)) is not { } entry)
        {
            return new($"{guest.Name} steht nicht auf der Warteliste.", []);
        }

        var treatment = await db.Treatments.AsNoTracking().Where(t => t.Id == entry.TreatmentId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "Behandlung";
        var wish = $"{entry.From.ToString("dd.MM.", AnswerContext.German)}–{entry.Until.ToString("dd.MM.", AnswerContext.German)}";
        return new($"Vorschlag: {guest.Name} einen Termin aus der Warteliste anbieten ({treatment}, {wish}). Gebucht wird erst, wenn eine freie Zeit gewählt und bestätigt ist.",
        [
            new AssistantSuggestion(AssistantSuggestionKind.OfferWaitlist, "Freie Zeiten zum Wunsch zeigen", entry.LocationId, entry.TreatmentId, null, guest.Id, null, null,
                Day: entry.From < today ? today : entry.From, TargetId: entry.Id),
        ]);
    }

    /// <summary>„Offene Rechnung …“: die jüngste offene Rechnung des Gastes an einer Kassa eines sichtbaren Standorts.</summary>
    private async Task<Result> BillAsync(GuestFinder.Guest guest, AnswerContext context, CancellationToken ct)
    {
        var bills = await db.Bills.AsNoTracking().Where(b => b.CustomerId == guest.Id && b.Status == BillStatus.Open)
            .Join(db.CashRegisters.AsNoTracking(), b => b.RegisterId, r => r.Id, (b, r) => new { b.Id, b.CreatedAt, r.LocationId })
            .OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id).ToListAsync(ct);
        if (bills.FirstOrDefault(b => context.Access.CanSee(b.LocationId)) is not { } bill)
        {
            return new($"{guest.Name} hat keine offene Rechnung.", []);
        }

        var created = GuestFinder.Local(bill.CreatedAt, context).ToString("dd.MM.yyyy", AnswerContext.German);
        return new($"Vorschlag: offene Rechnung von {guest.Name} (angelegt am {created}) in der Kassa öffnen. Kassiert wird erst in der Kassa.",
        [
            new AssistantSuggestion(AssistantSuggestionKind.OpenBill, "Rechnung in der Kassa öffnen", bill.LocationId, null, null, guest.Id, null, null, TargetId: bill.Id),
        ]);
    }
}

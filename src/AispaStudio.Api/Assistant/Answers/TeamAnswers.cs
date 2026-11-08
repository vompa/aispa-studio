using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wer arbeitet heute/morgen?“ und „Ist Lena nächste Woche da?“: aus Dienstplan und Wochenarbeitszeiten (<see cref="RosterService"/>), samt Urlaub, Krankenstand
/// und freien Tagen. Die Antwort nennt Zeiten und Status, keine Gründe für Abwesenheiten.
/// </summary>
internal sealed class TeamAnswers(RosterService roster) : IAnswerGiver
{
    private const int MaxDays = 14;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.WhoWorks, BusinessTopic.PersonAvailable];

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

        var source = $"Dienstplan und Arbeitszeiten des {context.PlaceOf}";
        var employees = await roster.EmployeesAt(context.Location.Id, from).AsNoTracking().Include(e => e.WorkingHours).AsSplitQuery().ToListAsync(ct);
        employees = [.. employees.OrderBy(e => e.InHouseName, StringComparer.CurrentCulture)];
        var plans = await roster.PlansAsync(employees, from, until, ct);
        return question.Topic == BusinessTopic.WhoWorks
            ? WhoWorks(employees, plans, from, until, context, source)
            : Person(question, employees, plans, from, until, context, source);
    }

    private static AssistantBlock WhoWorks(List<Employee> employees, Dictionary<(Guid EmployeeId, DateOnly Date), DayPlan> plans, DateOnly from, DateOnly until,
        AnswerContext context, string source)
    {
        var working = (DateOnly day) => employees
            .Select(e => (Employee: e, Shifts: plans[(e.Id, day)].Shifts.Where(s => s.LocationId == context.Location.Id).ToList()))
            .Where(x => x.Shifts.Count > 0)
            .ToList();
        if (from == until)
        {
            var today = working(from);
            var away = employees
                .Where(e => plans[(e.Id, from)].Shifts.All(s => s.LocationId != context.Location.Id)
                    && plans[(e.Id, from)].Entries.FirstOrDefault(x => x.Kind != RosterKind.Work) is not null
                    && e.WorkingHours.Any(w => w.LocationId == context.Location.Id && w.Day == from.DayOfWeek))
                .Select(e => (Employee: e, Kind: plans[(e.Id, from)].Entries.First(x => x.Kind != RosterKind.Work).Kind))
                .ToList();
            // Ä316: der Kopfsatz nennt nur die Zahlen, jede Person steht einmal in der Liste; ein Klick zeigt den Wochenplan mit diesem Tag.
            var items = today.Select(x => RosterItem(x.Employee, Times(x.Shifts), from, context))
                .Concat(away.Select(a => RosterItem(a.Employee, Label(a.Kind), from, context)))
                .Take(Blocks.MaxItems)
                .ToList();
            var text = today.Count == 0
                ? $"Am {AnswerDates.Long(from)} ist im {context.Place} niemand im Dienst eingeteilt"
                : $"Am {AnswerDates.Long(from)} {(today.Count == 1 ? "arbeitet" : "arbeiten")} im {context.Place} {today.Count} {(today.Count == 1 ? "Person" : "Personen")}";
            text += away.Count == 0 ? "." : $"; {away.Count} {(away.Count == 1 ? "ist" : "sind")} nicht da.";
            return Blocks.Answer(BusinessTopic.WhoWorks, text, source, items: items);
        }

        var period = AnswerDates.Describe(from, until);
        var days = Enumerable.Range(0, until.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).ToList();
        return Blocks.Answer(BusinessTopic.WhoWorks, $"Dienste im {context.Place}, {period}: Die Tabelle nennt je Tag, wer arbeitet.", source,
            Blocks.Table(["Tag", "Im Dienst"], days.Select(d =>
            {
                var names = working(d).Select(x => x.Employee.InHouseName).ToList();
                return (IReadOnlyList<string>)[AnswerDates.Long(d), names.Count == 0 ? "niemand eingeteilt" : string.Join(", ", names)];
            })), period: period);
    }

    private static AssistantBlock Person(BusinessQuestion question, List<Employee> employees, Dictionary<(Guid EmployeeId, DateOnly Date), DayPlan> plans,
        DateOnly from, DateOnly until, AnswerContext context, string source)
    {
        if (string.IsNullOrWhiteSpace(question.Subject))
        {
            return Blocks.NotFound(question.Topic, "Wessen Dienst soll nachgesehen werden? Bitte einen Namen aus dem Team nennen.", source);
        }

        var words = question.Subject.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var found = NameMatch.Best(employees, e => NameMatch.Fit(words, e.InHouseName, e.DisplayName, e.PreferredName));
        if (found.Count == 0)
        {
            return Blocks.NotFound(question.Topic, $"Eine Person namens „{question.Subject}“ gibt es im Team des {context.PlaceOf} nicht.", source);
        }

        if (found.Count > 1)
        {
            return Blocks.NotFound(question.Topic, $"„{question.Subject}“ passt zu mehreren Personen: {string.Join(", ", found.Select(e => e.InHouseName))}. Bitte den vollen Namen nennen.", source);
        }

        var person = found[0];
        var days = Enumerable.Range(0, until.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).ToList();
        string Status(DateOnly day)
        {
            var plan = plans[(person.Id, day)];
            var here = plan.Shifts.Where(s => s.LocationId == context.Location.Id).ToList();
            if (here.Count > 0)
            {
                return $"Dienst {Times(here)}";
            }

            if (plan.Shifts.Any())
            {
                return "Dienst an einem anderen Standort";
            }

            return plan.Entries.FirstOrDefault(x => x.Kind != RosterKind.Work) is { } other ? Label(other.Kind) : "nicht eingeteilt";
        }

        if (from == until)
        {
            var status = Status(from);
            var text = status.StartsWith("Dienst ", StringComparison.Ordinal)
                ? $"{person.InHouseName} ist am {AnswerDates.Long(from)} im Dienst: {status["Dienst ".Length..]}."
                : $"{person.InHouseName} ist am {AnswerDates.Long(from)} nicht im Dienst im {context.Place}: {status}.";
            return Blocks.Answer(question.Topic, text, source);
        }

        var period = AnswerDates.Describe(from, until);
        var workDays = days.Count(d => Status(d).StartsWith("Dienst ", StringComparison.Ordinal));
        return Blocks.Answer(question.Topic, $"{person.InHouseName}, {period}: im Dienst an {workDays} von {days.Count} Tagen.", source,
            Blocks.Table(["Tag", "Status"], days.Select(d => (IReadOnlyList<string>)[AnswerDates.Long(d), Status(d)])), period: period);
    }

    private static AssistantItem RosterItem(Employee employee, string status, DateOnly day, AnswerContext context) =>
        new(employee.InHouseName, status, null, null,
            new AssistantLink(AssistantLinkKind.Roster, RosterAction, "roster", null, context.Location.Id, day, TargetId: employee.Id), RosterAction);

    private const string RosterAction = "Im Wochenplan zeigen";

    internal static string Times(IEnumerable<RosterEntry> shifts) =>
        string.Join(", ", shifts.Select(s => $"{s.Start:HH\\:mm} bis {s.End:HH\\:mm}"));

    internal static string Label(RosterKind kind) => kind switch
    {
        RosterKind.Vacation => "Urlaub",
        RosterKind.Sick => "Krankenstand",
        RosterKind.DayOff => "frei",
        RosterKind.Training => "Schulung außer Haus",
        _ => "im Dienst",
    };
}

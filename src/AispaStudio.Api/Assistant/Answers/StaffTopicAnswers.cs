using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann hat Sophie Pause?“ und „Wer ist heute Ersthelfer?“ (Slice 136): Pause und Dienstende aus dem Dienstplan und den Sperrzeiten „Pause“ der Person (wie der Kalender sie
/// zählt), Zuständigkeiten und Ersthelfer aus den Zuständigkeiten der Personen (R05 Slice 117). Genannt werden nur Art, Person, Vertretung und ob das verlangte Zertifikat gilt,
/// nie Zertifikatsdaten oder Gründe von Abwesenheiten.
/// </summary>
internal sealed class StaffTopicAnswers(RosterService roster, AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.PersonBreak, BusinessTopic.DutyHolders];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        return question.Topic == BusinessTopic.PersonBreak ? await BreakAsync(question, context, ct) : await DutiesAsync(question, context, ct);
    }

    // ---------- Pause und Dienstende ----------

    private async Task<AssistantBlock> BreakAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var day = question.From ?? context.Today;
        var source = $"Dienstplan und Pausen des {context.PlaceOf}";
        if (string.IsNullOrWhiteSpace(question.Subject))
        {
            return Blocks.NotFound(question.Topic, "Wessen Pause oder Dienstende soll nachgesehen werden? Bitte einen Namen aus dem Team nennen.", source);
        }

        var employees = await roster.EmployeesAt(context.Location.Id, day).AsNoTracking().Include(e => e.WorkingHours).AsSplitQuery().ToListAsync(ct);
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
        var plan = (await roster.PlansAsync([person], day, day, ct))[(person.Id, day)];
        var shifts = plan.Shifts.Where(s => s.LocationId == context.Location.Id).OrderBy(s => s.Start).ToList();
        var when = AnswerDates.Long(day);
        if (shifts.Count == 0)
        {
            var status = plan.Shifts.Any() ? "Dienst an einem anderen Standort"
                : plan.Entries.FirstOrDefault(x => x.Kind != RosterKind.Work) is { } other ? TeamAnswers.Label(other.Kind) : "nicht eingeteilt";
            return Blocks.Answer(question.Topic, $"{person.InHouseName} ist am {when} nicht im Dienst im {context.Place}: {status}.", source);
        }

        var pauses = await PausesAsync(person, shifts, day, context, ct);
        var end = shifts[^1].End!.Value.ToString("HH:mm", AnswerContext.German);
        var text = $"{person.InHouseName} ist am {when} im Dienst: {TeamAnswers.Times(shifts)}. Dienstende {end} Uhr. {await PauseSentenceAsync(pauses, shifts, context, ct)}";
        return Blocks.Answer(question.Topic, text, source);
    }

    /// <summary>Die Pausen des Tages: Sperrzeiten „Pause…“ der Person und die Lücken zwischen zwei Diensten, nach Beginn sortiert.</summary>
    private async Task<List<(DateTimeOffset From, DateTimeOffset Until, bool IsGap)>> PausesAsync(
        Employee person, List<RosterEntry> shifts, DateOnly day, AnswerContext context, CancellationToken ct)
    {
        var (dayFrom, dayUntil) = LocalTime.Day(day, context.Zone);
        var blocks = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.Kind == ResourceKind.Employee && b.ResourceId == person.Id && b.AppointmentId == null && b.From < dayUntil && b.Until > dayFrom)
            .ToListAsync(ct);
        var pauses = blocks.Where(b => CalendarBreaks.IsBreak(b.Reason)).Select(b => (b.From, b.Until, IsGap: false)).ToList();
        for (var i = 0; i + 1 < shifts.Count; i++)
        {
            if (shifts[i].End < shifts[i + 1].Start)
            {
                pauses.Add((LocalTime.ToUtc(day, shifts[i].End!.Value, context.Zone), LocalTime.ToUtc(day, shifts[i + 1].Start!.Value, context.Zone), true));
            }
        }

        return [.. pauses.OrderBy(p => p.From)];
    }

    private async Task<string> PauseSentenceAsync(
        List<(DateTimeOffset From, DateTimeOffset Until, bool IsGap)> pauses, List<RosterEntry> shifts, AnswerContext context, CancellationToken ct)
    {
        if (pauses.Count > 0)
        {
            return $"Pause: {string.Join(" und ", pauses.Select(p => $"{context.Time(p.From)}–{context.Time(p.Until)}"))} Uhr{GapNote(pauses)}.";
        }

        var worked = shifts.Sum(s => (s.End!.Value - s.Start!.Value).TotalHours);
        var required = (await roster.RulesAsync(ct)).RequiredBreakMinutes(worked);
        return required > 0
            ? $"Pause: keine eingetragen; verlangt sind nach dem Arbeitszeitrecht {required} Min."
            : "Pause: keine eingetragen; nach dem Arbeitszeitrecht ist bei dieser Dienstdauer keine verlangt.";
    }

    private static string GapNote(List<(DateTimeOffset From, DateTimeOffset Until, bool IsGap)> pauses) =>
        pauses.All(p => p.IsGap) ? " (Lücke zwischen den Diensten)" : pauses.Any(p => p.IsGap) ? " (darunter eine Lücke zwischen den Diensten)" : "";

    // ---------- Ersthelfer und Zuständigkeiten ----------

    private async Task<AssistantBlock> DutiesAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var day = question.From ?? context.Today;
        var source = $"Zuständigkeiten und Dienstplan des {context.PlaceOf}";
        var wanted = KeyOf(question.Detail);
        var types = await db.Set<DutyType>().AsNoTracking().Where(t => t.IsActive && (wanted == null || t.Key == wanted)).OrderBy(t => t.Name).ToListAsync(ct);
        var duties = await db.Set<StaffDuty>().AsNoTracking().Include(d => d.Employee).Include(d => d.Deputy)
            .Where(d => d.Employee.IsActive && d.ValidFrom <= day && (d.ValidUntil == null || d.ValidUntil >= day)).ToListAsync(ct);
        var withHolders = types.Where(t => duties.Any(d => d.DutyTypeId == t.Id)).ToList();
        if (withHolders.Count == 0)
        {
            var what = wanted is null ? "keine Person mit einer Zuständigkeit" : $"keine Person mit der Zuständigkeit „{types.FirstOrDefault()?.Name ?? Label(wanted.Value)}“";
            return Blocks.NotStored(question.Topic, $"{what}; sie wird bei der Person im Bereich „Team“ eingetragen", source, null);
        }

        if (wanted == DutyTypeKey.FirstAid)
        {
            return await FirstAidAsync(question, withHolders[0], [.. duties.Where(d => d.DutyTypeId == withHolders[0].Id)], day, context, source, ct);
        }

        string Holders(DutyType type) => string.Join(", ", duties.Where(d => d.DutyTypeId == type.Id).OrderBy(d => d.Employee.InHouseName, StringComparer.CurrentCulture)
            .Select(d => d.Deputy is { } deputy ? $"{d.Employee.InHouseName} (Vertretung: {deputy.InHouseName})" : d.Employee.InHouseName));
        return withHolders.Count == 1
            ? Blocks.Answer(question.Topic, $"{withHolders[0].Name}: {Holders(withHolders[0])}.", source)
            : Blocks.Answer(question.Topic, $"Zuständigkeiten im {context.Place}, Stand {AnswerDates.Long(day)}.", source,
                Blocks.Table(["Zuständigkeit", "Person"], withHolders.Select(t => (IReadOnlyList<string>)[t.Name, Holders(t)])));
    }

    /// <summary>
    /// Ersthelfer „heute im Dienst“: gültig am Tag, am Standort eingeteilt und mit dem verlangten Zertifikat (dieselbe Regel wie die Warnung im Dienstplan, <see cref="StaffDutyRules"/>).
    /// Ist niemand dabei, nennt die Antwort die Personen mit der Zuständigkeit und warum sie heute nicht zählen.
    /// </summary>
    private async Task<AssistantBlock> FirstAidAsync(
        BusinessQuestion question, DutyType type, List<StaffDuty> duties, DateOnly day, AnswerContext context, string source, CancellationToken ct)
    {
        var ids = duties.Select(d => d.EmployeeId).Distinct().ToList();
        var people = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var plans = await roster.PlansAsync(people, day, day, ct);
        var certificates = await db.Set<StaffCertificate>().AsNoTracking().Where(c => ids.Contains(c.EmployeeId)).ToListAsync(ct);
        var rows = duties.OrderBy(d => d.Employee.InHouseName, StringComparer.CurrentCulture).Select(d =>
        {
            var shifts = plans[(d.EmployeeId, day)].Shifts.Where(s => s.LocationId == context.Location.Id).OrderBy(s => s.Start).ToList();
            var valid = StaffDutyRules.IsSatisfied(type.RequiredCertificate, certificates.Where(c => c.EmployeeId == d.EmployeeId), day);
            return (Duty: d, Shifts: shifts, Valid: valid);
        }).ToList();
        var counted = rows.Where(r => r.Valid && r.Shifts.Count > 0).ToList();
        var when = AnswerDates.Long(day);
        if (counted.Count > 0)
        {
            var names = counted.Select(r => $"{r.Duty.Employee.InHouseName} (bis {r.Shifts[^1].End!.Value.ToString("HH:mm", AnswerContext.German)} Uhr)");
            return Blocks.Answer(question.Topic, $"{type.Name} im Dienst am {when}: {string.Join(", ", names)}.", source);
        }

        string Reason((StaffDuty Duty, List<RosterEntry> Shifts, bool Valid) r) => !r.Valid ? "Zertifikat fehlt oder ist abgelaufen"
            : r.Duty.Deputy is { } deputy ? $"nicht im Dienst, Vertretung: {deputy.InHouseName}" : "nicht im Dienst";
        return Blocks.Answer(question.Topic, $"Am {when} ist niemand im Dienst, der als {type.Name} gilt.", source,
            Blocks.Table(["Person", "Stand"], rows.Select(r => (IReadOnlyList<string>)[r.Duty.Employee.InHouseName, Reason(r)])));
    }

    private static DutyTypeKey? KeyOf(string? detail) => detail switch
    {
        "firstaid" => DutyTypeKey.FirstAid,
        "cash" => DutyTypeKey.CashResponsibility,
        "stock" => DutyTypeKey.Stock,
        "fire" => DutyTypeKey.FireSafety,
        "hygiene" => DutyTypeKey.Hygiene,
        "safety" => DutyTypeKey.SafetyRep,
        "training" => DutyTypeKey.Training,
        _ => null,
    };

    private static string Label(DutyTypeKey key) => key switch
    {
        DutyTypeKey.FirstAid => "Ersthelfer",
        DutyTypeKey.CashResponsibility => "Kassa-Verantwortung",
        DutyTypeKey.Stock => "Lager",
        DutyTypeKey.FireSafety => "Brandschutz",
        DutyTypeKey.Hygiene => "Hygiene",
        DutyTypeKey.SafetyRep => "Sicherheitsvertrauensperson",
        _ => "Ausbildung",
    };
}

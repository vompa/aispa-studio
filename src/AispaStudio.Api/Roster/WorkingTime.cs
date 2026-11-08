using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Roster;

/// <summary>Der geltende Plan einer Person an einem Tag: aus dem Dienstplan, sonst aus der Wochenarbeitszeit.</summary>
public sealed record DayPlan(DateOnly Date, RosterSource Source, IReadOnlyList<RosterEntry> Entries, string? Note, bool Draft = false)
{
    public IEnumerable<RosterEntry> Shifts => Entries.Where(e => e.Kind == RosterKind.Work);

    public double Hours => Shifts.Sum(s => (s.End!.Value - s.Start!.Value).TotalHours);

    /// <summary>
    /// Der Tag, wie ihn jemand sieht, der nur bestimmte Standorte sehen darf: Dienste anderer Standorte fehlen. Abwesenheiten hängen an keinem Standort und erscheinen nur mit
    /// <paramref name="absences"/>. Bleibt nichts übrig, verrät der Tag auch weder Notiz, Entwurf noch dass er geplant ist.
    /// </summary>
    public DayPlan VisibleTo(Func<Guid, bool> canSee, bool absences)
    {
        var entries = Entries.Where(e => e.LocationId is { } location ? canSee(location) : absences).ToList();
        return entries.Count > 0 || (Entries.Count == 0 && absences)
            ? this with { Entries = entries }
            : new DayPlan(Date, RosterSource.Pattern, [], null);
    }

    public static DayPlan For(Employee employee, RosterDay? planned, DateOnly date) =>
        planned is not null
            ? new(date, RosterSource.Plan,
                [.. planned.Entries.OrderBy(e => e.Start).Select(e => new RosterEntry(e.Kind, e.LocationId, e.Start, e.End))], planned.Note)
            : new(date, RosterSource.Pattern,
                [.. employee.WorkingHours.Where(w => w.Day == date.DayOfWeek).OrderBy(w => w.Start)
                    .Select(w => new RosterEntry(RosterKind.Work, w.LocationId, w.Start, w.End))], null);

    /// <summary>Wie der Tag nach Veröffentlichung des Entwurfs aussähe.</summary>
    public static DayPlan For(Employee employee, RosterDraft draft) =>
        draft.Reset
            ? For(employee, (RosterDay?)null, draft.Date) with { Draft = true }
            : new(draft.Date, RosterSource.Plan,
                [.. draft.Entries.OrderBy(e => e.Start).Select(e => new RosterEntry(e.Kind, e.LocationId, e.Start, e.End))], draft.Note, true);
}

/// <summary>Mindestpause ab einer Arbeitszeit: mehr als <paramref name="AfterHours"/> Stunden verlangen <paramref name="Minutes"/> Minuten.</summary>
public sealed record BreakRule(double AfterHours, int Minutes);

/// <summary>
/// Gesetzliche Grenzen der Arbeitszeit je Land (Sitz des Unternehmens), als Grundlage für Hinweise:
/// <list type="bullet">
/// <item>Österreich (AZG, ARG): 10 Stunden am Tag, 11 Stunden Ruhe, 48 Stunden im Schnitt, 30 Minuten Pause ab mehr als 6 Stunden (§ 11 AZG).</item>
/// <item>Deutschland (ArbZG): 10 Stunden am Tag, 11 Stunden Ruhe, 48 Stunden, 30 Minuten Pause ab mehr als 6 und 45 Minuten ab mehr als 9 Stunden (§ 4 ArbZG).</item>
/// <item>Schweiz (ArG): keine feste Tagesgrenze, aber Tagesarbeit innerhalb von 14 Stunden einschließlich Pausen, 11 Stunden Ruhe,
/// 50 Stunden pro Woche (Dienstleistungsbetriebe; 45 Stunden nur für Industrie, Büro und Detailhandel-Großbetriebe),
/// Pausen 15, 30 und 60 Minuten ab mehr als 5,5, 7 und 9 Stunden (Art. 15 ArG).</item>
/// </list>
/// Kollektivverträge und Betriebsvereinbarungen können abweichen; deshalb bleiben es Hinweise.
/// <para>
/// <c>NoticeDays</c>: Vorlauf für die Einteilung. Österreich: Lage der Normalarbeitszeit mindestens zwei Wochen im Voraus (§ 19c Abs. 2 AZG).
/// Schweiz: Einsatzpläne in der Regel zwei Wochen im Voraus (Art. 69 ArGV 1). Deutschland: keine allgemeine Frist; vier Tage gelten bei
/// Arbeit auf Abruf (§ 12 Abs. 3 TzBfG) und dienen hier als Richtwert.
/// </para>
/// </summary>
public sealed record WorkingTimeRules(
    string Country,
    double? MaxHoursPerDay,
    double? MaxDaySpanHours,
    double MinRestHours,
    double MaxHoursPerWeek,
    IReadOnlyList<BreakRule> Breaks,
    int NoticeDays = 14,
    string NoticeBasis = "§ 19c Abs. 2 AZG")
{
    public static readonly WorkingTimeRules Austria = new("AT", 10, null, 11, 48, [new(6, 30)]);
    public static readonly WorkingTimeRules Germany = new("DE", 10, null, 11, 48, [new(6, 30), new(9, 45)], 4, "Richtwert nach § 12 Abs. 3 TzBfG");
    public static readonly WorkingTimeRules Switzerland = new("CH", null, 14, 11, 50, [new(5.5, 15), new(7, 30), new(9, 60)], 14, "Art. 69 ArGV 1");

    public static WorkingTimeRules For(string? country) => country?.ToUpperInvariant() switch
    {
        "DE" => Germany,
        "CH" => Switzerland,
        _ => Austria,
    };

    /// <summary>Verlangte Mindestpause in Minuten für die gearbeiteten Stunden eines Tages.</summary>
    public int RequiredBreakMinutes(double workedHours) =>
        Breaks.Where(b => workedHours > b.AfterHours).Select(b => b.Minutes).DefaultIfEmpty(0).Max();
}

/// <summary>
/// Hinweise zur Arbeitszeit nach den Grenzen des Landes (<see cref="WorkingTimeRules"/>), für Plan und Ist-Zeiten gleich.
/// Bewusst Hinweise statt Sperren: Kollektivverträge und Betriebsvereinbarungen erlauben Abweichungen, und ein Krankenstand
/// muss sich immer eintragen lassen.
/// </summary>
internal static class WorkingTime
{
    public static List<string> DayWarnings(DayPlan today, DayPlan? yesterday, WorkingTimeRules? rules = null) =>
        DayWarnings(
            [.. today.Shifts.Select(s => (s.Start!.Value.ToTimeSpan(), s.End!.Value.ToTimeSpan()))],
            yesterday?.Shifts.Max(s => s.End)?.ToTimeSpan(),
            rules ?? WorkingTimeRules.Austria,
            checkBreaks: false);

    /// <summary>
    /// Hinweise für die Zeitspannen eines Tages (ab Mitternacht des Tages; über Mitternacht hinaus größer als 24 Stunden).
    /// <paramref name="previousEnd"/> ist das Ende der letzten Arbeit am Vortag, ebenfalls ab dessen Mitternacht.
    /// Pausen prüft nur die Zeiterfassung: im Plan stehen Dienste meist ohne die Pause darin.
    /// </summary>
    public static List<string> DayWarnings(
        IReadOnlyList<(TimeSpan Start, TimeSpan End)> spans, TimeSpan? previousEnd, WorkingTimeRules rules, bool checkBreaks)
    {
        var warnings = new List<string>();
        var ordered = spans.OrderBy(s => s.Start).ToList();
        var hours = ordered.Sum(s => (s.End - s.Start).TotalHours);
        if (rules.MaxHoursPerDay is { } max && hours > max)
        {
            warnings.Add($"Mehr als {max:0} Stunden Arbeitszeit ({hours:0.#} h).");
        }

        if (ordered.Zip(ordered.Skip(1)).Any(p => p.Second.Start < p.First.End))
        {
            warnings.Add("Dienste überschneiden sich.");
        }

        if (ordered.Count > 0)
        {
            var span = (ordered.Max(s => s.End) - ordered[0].Start).TotalHours;
            if (rules.MaxDaySpanHours is { } maxSpan && span > maxSpan)
            {
                warnings.Add($"Arbeitstag länger als {maxSpan:0} Stunden einschließlich Pausen ({span:0.#} h).");
            }

            if (previousEnd is { } end)
            {
                var rest = TimeSpan.FromHours(24) - end + ordered[0].Start;
                if (rest.TotalHours < rules.MinRestHours)
                {
                    warnings.Add($"Nur {rest.TotalHours:0.#} Stunden Ruhezeit seit dem Vortag (mindestens {rules.MinRestHours:0}).");
                }
            }

            if (checkBreaks && rules.RequiredBreakMinutes(hours) is > 0 and var required)
            {
                var breakMinutes = ordered.Zip(ordered.Skip(1)).Sum(p => Math.Max(0, (p.Second.Start - p.First.End).TotalMinutes));
                if (breakMinutes < required)
                {
                    warnings.Add($"Pause zu kurz: {breakMinutes:0} von {required} Minuten bei {hours:0.#} h Arbeit.");
                }
            }
        }

        return warnings;
    }

    /// <param name="hoursPerWeek">Stunden im Schnitt pro Woche.</param>
    /// <param name="days">Die Tage des Zeitraums; geprüft wird jede Folge von 7 Tagen.</param>
    /// <param name="rules">Grenzen des Landes; ohne Angabe Österreich.</param>
    public static List<string> WeekWarnings(double hoursPerWeek, IReadOnlyList<DayPlan> days, WorkingTimeRules? rules = null) =>
        WeekWarnings(hoursPerWeek, [.. days.Select(d => d.Shifts.Any())], rules ?? WorkingTimeRules.Austria);

    public static List<string> WeekWarnings(double hoursPerWeek, IReadOnlyList<bool> worked, WorkingTimeRules rules)
    {
        var warnings = new List<string>();
        if (hoursPerWeek > rules.MaxHoursPerWeek)
        {
            warnings.Add($"Mehr als {rules.MaxHoursPerWeek:0} Stunden pro Woche ({hoursPerWeek:0.#} h).");
        }

        if (Enumerable.Range(0, Math.Max(0, worked.Count - 6)).Any(i => worked.Skip(i).Take(7).All(w => w)))
        {
            warnings.Add("Sieben Tage am Stück ohne freien Tag (Wochenruhe).");
        }

        return warnings;
    }
}

using System.Globalization;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Auswertungen: „Top-Behandlungen diesen Monat“, „Top-Kunden im letzten Quartal“, „Welche Therapeutin hat die meisten Termine?“, „Auslastung letzte Woche je Person
/// und Raum“, „Umsatz nach Behandlung im September“, „Wie viele Nicht-erschienen hatten wir?“. Alle Zahlen kommen vom Berichtsdienst (<see cref="ManagementReports"/>),
/// der Zeitraum steht in der Antwort. Die Auswertungen ohne Beträge (Anzahl der Behandlungen, Auslastung in Prozent, Termine je Person, Nicht-erschienen, Top-Gäste nach Besuchen)
/// beantwortet jede Person mit dem Kalenderrecht, also auch die Rezeption. Beträge nur mit dem Recht „Berichte mit Beträgen“; der Berichtsdienst lässt sie dann schon selbst weg. Kassa- und Steuer-Pflichtzahlen
/// kommen hier nicht vor und rechnet nie die KI.
/// </summary>
internal sealed class ReportAnswers(ManagementReports reports) : IAnswerGiver
{
    private const int MaxDays = 366;

    public IReadOnlyCollection<BusinessTopic> Topics { get; } =
        [BusinessTopic.TopTreatments, BusinessTopic.TopGuests, BusinessTopic.TopTherapist, BusinessTopic.Utilization, BusinessTopic.RevenueByTreatment, BusinessTopic.NoShows];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        // Auswertungen ohne Beträge (Anzahl, Prozent, Quoten) sieht auch die Rezeption mit dem Kalenderrecht und die Therapeut:in, solange es ihrem Zugang nicht abgeschaltet ist (Ä55);
        // Beträge gibt es nur mit „Berichte mit Beträgen“ (unten).
        if (!context.Access.CanSeePlainReports)
        {
            return Blocks.Denied(question.Topic, context.Has(AccessRight.ViewCalendar)
                ? "Für diesen Zugang sind die Auswertungen ohne Beträge abgeschaltet; das stellt der Inhaber, ein Manager oder die Standortleitung unter „Zugänge“ ein."
                : "Auswertungen sehen Personen mit Zugang zum Kalender oder zu den Berichten.");
        }

        var until = question.Until is { } end && end <= context.Today ? end : context.Today;
        var from = question.From is { } start && start <= until ? start : new DateOnly(context.Today.Year, context.Today.Month, 1);
        if (until.DayNumber - from.DayNumber >= MaxDays)
        {
            from = until.AddDays(1 - MaxDays);
        }

        var amounts = context.Has(AccessRight.ViewAmountReports);
        var period = AnswerDates.Describe(from, until);
        var source = $"Management-Bericht des {context.PlaceOf}, {period}";
        var link = context.Report(from, until);

        if (question.Topic == BusinessTopic.RevenueByTreatment && !amounts)
        {
            return Blocks.Denied(question.Topic, "Umsätze sehen Inhaber, Manager und Backoffice sowie Personen mit dem Recht „Berichte mit Beträgen“. Die Auswertung ohne Beträge nach Anzahl der Termine ist möglich.",
                new AssistantFollowUp("Behandlungen nach Anzahl zeigen", $"Top-Behandlungen von {Day(from)} bis {Day(until)}"));
        }

        if (question.Topic == BusinessTopic.TopGuests && question.Detail == "spending" && !amounts)
        {
            return Blocks.Denied(question.Topic, "Ausgaben von Gästen sind Beträge. Die Auswertung nach Besuchen ist ohne Beträge möglich.",
                new AssistantFollowUp("Gäste nach Besuchen zeigen", $"Top-Gäste von {Day(from)} bis {Day(until)}"));
        }

        if (question.Topic == BusinessTopic.TopGuests)
        {
            return await GuestsAsync(question, context, from, until, amounts, period, source, link, ct);
        }

        var report = await reports.ManagementAsync(context.Location.Id, from, until, amounts, ct);
        if (report is null)
        {
            return Blocks.NotFound(question.Topic, "Zu diesem Standort gibt es keinen Bericht.", source);
        }

        return question.Topic switch
        {
            BusinessTopic.TopTreatments => TopTreatments(report, context, amounts, period, source, link),
            BusinessTopic.RevenueByTreatment => RevenueByTreatment(report, context, period, source, link),
            BusinessTopic.TopTherapist => TopTherapist(report, period, source, link),
            BusinessTopic.NoShows => NoShows(report, period, source, link),
            _ => await UtilizationAsync(question, report, context, from, until, period, source, link, ct),
        };
    }

    private static AssistantBlock TopTreatments(ManagementReportDto report, AnswerContext context, bool amounts, string period, string source, AssistantLink? link)
    {
        var ranked = report.Treatments.Where(t => t.Appointments > 0).OrderByDescending(t => t.Appointments).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0)
        {
            return Blocks.Answer(BusinessTopic.TopTreatments, $"Im Zeitraum {period} gab es im {context.Place} keine Behandlungen.", source, link: link, period: period);
        }

        var entries = ranked.Select(t => new RankedEntry(t.Name, t.Appointments.ToString(AnswerContext.German), t.Appointments.ToString(AnswerContext.German),
            t.Appointments == 1 ? "1 Termin" : $"{t.Appointments} Terminen")).ToList();
        var ranks = Ranking.Ranks(entries);
        string[] columns = amounts ? ["Platz", "Behandlung", "Termine", "Umsatz netto"] : ["Platz", "Behandlung", "Termine"];
        return Blocks.Answer(BusinessTopic.TopTreatments, $"Am häufigsten gebucht, {period}: {Ranking.Lead(entries, "Behandlungen")}.", source,
            Blocks.Table(columns, ranked.Select((t, i) => (IReadOnlyList<string>)
                [ranks[i].ToString(AnswerContext.German), t.Name, t.Appointments.ToString(AnswerContext.German), .. amounts ? new[] { context.Money(t.NetRevenue ?? 0) } : []])),
            link, period: period);
    }

    private static AssistantBlock RevenueByTreatment(ManagementReportDto report, AnswerContext context, string period, string source, AssistantLink? link)
    {
        var ranked = report.Treatments.Where(t => t.NetRevenue > 0).OrderByDescending(t => t.NetRevenue).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0)
        {
            return Blocks.Answer(BusinessTopic.RevenueByTreatment, $"Im Zeitraum {period} gab es im {context.Place} keinen Umsatz mit Behandlungen.", source, link: link, period: period);
        }

        var total = ranked.Sum(t => t.NetRevenue ?? 0);
        return Blocks.Answer(BusinessTopic.RevenueByTreatment,
            $"Umsatz netto nach Behandlung, {period} (laut Terminpreisen): zusammen {context.Money(total)}, am meisten mit {ranked[0].Name} ({context.Money(ranked[0].NetRevenue ?? 0)}).", source,
            Blocks.Table(["Behandlung", "Termine", "Umsatz netto"],
                ranked.Select(t => (IReadOnlyList<string>)[t.Name, t.Appointments.ToString(AnswerContext.German), context.Money(t.NetRevenue ?? 0)])), link, period: period);
    }

    private static AssistantBlock TopTherapist(ManagementReportDto report, string period, string source, AssistantLink? link)
    {
        var ranked = report.Employees.Where(e => e.Appointments > 0).OrderByDescending(e => e.Appointments).ThenByDescending(e => e.TreatmentHours).ThenBy(e => e.Name, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0)
        {
            return Blocks.Answer(BusinessTopic.TopTherapist, $"Im Zeitraum {period} hat niemand Termine gehabt.", source, link: link, period: period);
        }

        var best = ranked.Where(e => e.Appointments == ranked[0].Appointments).ToList();
        var lead = best.Count == 1
            ? $"Die meisten Termine hatte {best[0].Name}: {best[0].Appointments} ({best[0].TreatmentHours.ToString("0.#", AnswerContext.German)} Behandlungsstunden), {period}."
            : $"Gleichauf mit je {best[0].Appointments} Terminen: {string.Join(", ", best.Select(e => e.Name))}, {period}.";
        return Blocks.Answer(BusinessTopic.TopTherapist, lead, source,
            Blocks.Table(["Person", "Termine", "Behandlungsstunden", "Auslastung"],
                ranked.Select(e => (IReadOnlyList<string>)[e.Name, e.Appointments.ToString(AnswerContext.German), e.TreatmentHours.ToString("0.#", AnswerContext.German), $"{e.Utilization} %"])),
            link, period: period);
    }

    private static AssistantBlock NoShows(ManagementReportDto report, string period, string source, AssistantLink? link)
    {
        var rate = report.Kpis.FirstOrDefault(k => k.Key == "noShowRate")?.Value ?? 0;
        var cancellationRate = report.Kpis.FirstOrDefault(k => k.Key == "cancellationRate")?.Value ?? 0;
        var text = report.NoShows == 0
            ? $"Im Zeitraum {period} war niemand „nicht erschienen“; {report.Cancellations} Termine wurden storniert."
            : $"Nicht erschienen: {report.NoShows} im Zeitraum {period} (Quote {rate.ToString("0.#", AnswerContext.German)} %); storniert: {report.Cancellations} (Quote {cancellationRate.ToString("0.#", AnswerContext.German)} %).";
        return Blocks.Answer(BusinessTopic.NoShows, text, source,
            Blocks.Table(["Stand", "Anzahl", "Quote"],
            [
                ["Nicht erschienen", report.NoShows.ToString(AnswerContext.German), $"{rate.ToString("0.#", AnswerContext.German)} %"],
                ["Storniert", report.Cancellations.ToString(AnswerContext.German), $"{cancellationRate.ToString("0.#", AnswerContext.German)} %"],
            ]), link, period: period);
    }

    private async Task<AssistantBlock> UtilizationAsync(BusinessQuestion question, ManagementReportDto report, AnswerContext context, DateOnly from, DateOnly until,
        string period, string source, AssistantLink? link, CancellationToken ct)
    {
        var persons = question.Detail != "room";
        var rooms = question.Detail != "person";
        var staff = report.Kpis.FirstOrDefault(k => k.Key == "staffUtilization")?.Value ?? 0;
        var roomTotal = report.Kpis.FirstOrDefault(k => k.Key == "roomUtilization")?.Value ?? 0;
        var rows = new List<IReadOnlyList<string>>();
        if (persons)
        {
            rows.AddRange(report.Employees.OrderByDescending(e => e.Utilization).ThenBy(e => e.Name, StringComparer.Ordinal).Take(rooms ? 5 : Blocks.MaxRows)
                .Select(e => (IReadOnlyList<string>)["Person", e.Name, $"{e.Utilization} %"]));
        }

        if (rooms)
        {
            var perRoom = await reports.RoomsAsync(context.Location.Id, from, until, ct) ?? [];
            rows.AddRange(perRoom.OrderByDescending(r => r.Utilization).ThenBy(r => r.Name, StringComparer.Ordinal).Take(persons ? 5 : Blocks.MaxRows)
                .Select(r => (IReadOnlyList<string>)["Raum", r.Name, $"{r.Utilization} %"]));
        }

        var total = string.Join(", ", new[] { persons ? $"Personal {staff.ToString("0.#", AnswerContext.German)} %" : null, rooms ? $"Räume {roomTotal.ToString("0.#", AnswerContext.German)} %" : null }
            .OfType<string>());
        return Blocks.Answer(BusinessTopic.Utilization,
            rows.Count == 0 ? $"Im Zeitraum {period} gibt es keine Auslastung auszuweisen." : $"Auslastung {period}: insgesamt {total}. Die Tabelle nennt die Auslastung je {(persons && rooms ? "Person und Raum" : persons ? "Person" : "Raum")}.",
            source, rows.Count == 0 ? null : Blocks.Table(["Art", "Name", "Auslastung"], rows), link, period: period);
    }

    private async Task<AssistantBlock> GuestsAsync(BusinessQuestion question, AnswerContext context, DateOnly from, DateOnly until, bool amounts, string period, string source,
        AssistantLink? link, CancellationToken ct)
    {
        var bySpending = amounts && question.Detail != "visits";
        var top = await reports.TopGuestsAsync(context.Location.Id, from, until, bySpending, Blocks.MaxRows, ct);
        if (top.Count == 0)
        {
            return Blocks.Answer(BusinessTopic.TopGuests, $"Im Zeitraum {period} gibt es im {context.Place} keine abgeschlossenen Besuche.", source, link: link, period: period);
        }

        var entries = top.Select(g => bySpending
            ? new RankedEntry(g.Name, (g.Spent ?? 0).ToString(CultureInfo.InvariantCulture), context.Money(g.Spent ?? 0), context.Money(g.Spent ?? 0))
            : new RankedEntry(g.Name, g.Visits.ToString(CultureInfo.InvariantCulture), g.Visits.ToString(AnswerContext.German), g.Visits == 1 ? "1 Besuch" : $"{g.Visits} Besuchen")).ToList();
        var ranks = Ranking.Ranks(entries);
        string[] columns = bySpending ? ["Platz", "Gast", "Besuche", "Ausgaben"] : ["Platz", "Gast", "Besuche"];
        return Blocks.Answer(BusinessTopic.TopGuests,
            $"Beste Gäste {(bySpending ? "nach Ausgaben" : "nach Besuchen")}, {period}: {Ranking.Lead(entries, "Gäste")}.", source,
            Blocks.Table(columns, top.Select((g, i) => (IReadOnlyList<string>)
                [ranks[i].ToString(AnswerContext.German), g.Name, g.Visits.ToString(AnswerContext.German), .. bySpending ? new[] { context.Money(g.Spent ?? 0) } : []])),
            link, period: period);
    }

    private static string Day(DateOnly day) => day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

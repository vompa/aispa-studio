using AispaStudio.Api.Promotions;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Welche Rabatte oder Aktionen laufen gerade?“, „Gibt es im November ein Angebot?“, „Gilt die Aktion auch für Pakete?“: die Aktionen des Standorts aus dem Fachbereich
/// Aktionen (<see cref="PromotionService"/>, ADR 0071), für einen Tag oder einen Zeitraum. Der Rabatt wird an der Kassa gerechnet, hier wird nur genannt, was gilt.
/// </summary>
internal sealed class PromotionAnswers(PromotionService promotions) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.Promotions];

    public IReadOnlyCollection<string> Fields(BusinessQuestion question) => [FieldIds.Promotions];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var from = question.From ?? context.Today;
        var until = question.Until is { } end && end >= from ? end : from;
        var source = $"Aktionen des {context.PlaceOf}";
        var link = context.Fill(FieldIds.Promotions);
        var running = await promotions.GetActiveAsync(context.Location.Id, from, until, ct);
        var packagesOnly = question.Detail == "packages";
        var shown = packagesOnly ? [.. running.Where(p => p.Scope == PromotionScope.SelectedPackages)] : running;
        var when = from == context.Today && until == from ? "Heute" : from == until ? $"Am {AnswerDates.Long(from)}" : $"Im Zeitraum {AnswerDates.Describe(from, until)}";
        var period = from == until ? null : AnswerDates.Describe(from, until);

        if (shown.Count == 0)
        {
            var text = packagesOnly
                ? running.Count == 0
                    ? $"{when} gilt im {context.Place} keine Aktion, auch nicht für Pakete."
                    : $"Nein, {when.ToLowerInvariant()} gilt keine Aktion für Pakete. Es laufen {running.Count}, aber nur für Behandlungen: {string.Join(", ", running.Select(p => p.Name))}."
                : from > context.Today
                    ? $"{when} ist im {context.Place} noch keine Aktion eingetragen."
                    : $"{when} läuft im {context.Place} keine Aktion.";
            return Blocks.Answer(question.Topic, text, source, link: link, period: period);
        }

        var lead = packagesOnly
            ? $"Ja, {when.ToLowerInvariant()} gilt im {context.Place} für Pakete: {string.Join(", ", shown.Select(p => p.Name))}."
            : shown.Count == 1
                ? $"{when} {(from > context.Today ? "gilt" : "läuft")} im {context.Place} die Aktion {shown[0].Name}: {shown[0].GuestText}"
                : $"{when} {(from > context.Today ? "gelten" : "laufen")} im {context.Place} {shown.Count} Aktionen: {string.Join(", ", shown.Select(p => $"{p.Name} ({p.DiscountText})"))}.";
        return Blocks.Answer(question.Topic, lead, source,
            Blocks.Table(["Aktion", "Rabatt", "Gilt für", "Zeitraum"], shown.Select(p => (IReadOnlyList<string>)
                [p.Name, p.DiscountText, Scope(p), $"{p.ValidFrom.ToString("dd.MM.", AnswerContext.German)} bis {p.ValidUntil.ToString("dd.MM.yyyy", AnswerContext.German)}"])),
            link, period: period);
    }

    private static string Scope(ActivePromotionDto promotion)
    {
        var names = string.Join(", ", promotion.Treatments.Take(3).Select(t => t.Name)) + (promotion.Treatments.Count > 3 ? " u. a." : "");
        return promotion.Scope switch
        {
            PromotionScope.AllTreatments => "alle Behandlungen",
            PromotionScope.SelectedTreatments => names,
            _ => $"Pakete: {names}",
        };
    }
}

using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wer steht auf der Warteliste?“ (Ä317): wartende Einträge am Standort der Frage, deren Wunschzeitraum heute oder später endet (mit Tag: die diesen Tag einschließen);
/// je Eintrag Gast, Behandlung, Wunschzeit und wann er angelegt wurde, ein Klick öffnet die Kundenkarte. Anbieten ist ein Auftrag und gehört nicht hierher.
/// </summary>
internal sealed class WaitlistAnswers(AispaDbContext db, OnlineDbContext online) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.Waitlist];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        var source = $"Warteliste des {context.PlaceOf}";
        var day = question.From ?? context.Today;
        var exact = question.From is not null;
        var entries = await online.WaitlistEntries.AsNoTracking()
            .Where(w => w.LocationId == context.Location.Id && w.Status == WaitlistStatus.Waiting && w.Until >= day && (!exact || w.From <= day))
            .OrderBy(w => w.From).ThenBy(w => w.CreatedAt).ThenBy(w => w.Id)
            .Take(Blocks.MaxItems)
            .ToListAsync(ct);
        if (entries.Count == 0)
        {
            return Blocks.Answer(question.Topic, $"Auf der Warteliste des {context.PlaceOf} steht derzeit niemand.", source);
        }

        var customerIds = entries.Select(e => e.CustomerId).Distinct().ToList();
        var treatmentIds = entries.Select(e => e.TreatmentId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.PreferredName }).ToDictionaryAsync(c => c.Id, c => PersonNames.InHouse(c.FirstName, c.LastName, c.PreferredName), ct);
        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var items = entries.Select(e =>
        {
            var name = customers.GetValueOrDefault(e.CustomerId, "Gast");
            var part = AnswerDates.PartWord(e.DayPart);
            var wish = $"{e.From.ToString("dd.MM.", AnswerContext.German)}–{e.Until.ToString("dd.MM.", AnswerContext.German)}{(part == "" ? "" : $", {part}")}";
            var created = GuestFinder.Local(e.CreatedAt, context).ToString("dd.MM.yyyy", AnswerContext.German);
            return new AssistantItem(name, $"{treatments.GetValueOrDefault(e.TreatmentId, "Behandlung")} · {wish} · angelegt am {created}", null,
                new AssistantSuggestion(AssistantSuggestionKind.OpenCustomer, name, null, null, null, e.CustomerId, null, null), null, "Kundenkarte öffnen");
        }).ToList();
        var text = $"Auf der Warteliste des {context.PlaceOf}: {entries.Count} {(entries.Count == 1 ? "Eintrag" : "Einträge")}.";
        return Blocks.Answer(question.Topic, text, source, items: items);
    }
}

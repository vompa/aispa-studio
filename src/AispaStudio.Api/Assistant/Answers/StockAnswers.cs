using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Haben wir das Arganöl noch?“ und „Was ist bald aus?“ (Slice 136): der Bestand eines Materials je Lagerort mit dem Mindestbestand, und die Materialien unter dem Mindestbestand mit
/// der Menge, die schon bestellt ist. Das Lager sehen alle mit dem Recht „Kalender ansehen“ (wie die Lager-Übersicht); Einstandspreise und Werte nennt der Assistent nie. Den Mindestbestand
/// je Lagerort nennt die Antwort nicht, nur den des Materials.
/// </summary>
internal sealed class StockAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.StockLevel, BusinessTopic.StockLow];

    /// <summary>Wörter einer Bestandsfrage, die nicht zum Namen des Materials gehören.</summary>
    private static readonly Regex Noise = new(@"\b(bestand|lager|haben|wir|noch|genug|wie ?viel(e)?|auf|vorrätig|lagernd|ist|sind|da|übrig|im|das|die|der|den)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        return question.Topic == BusinessTopic.StockLow ? await LowAsync(question, context, ct) : await LevelAsync(question, context, ct);
    }

    private async Task<AssistantBlock> LevelAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var source = $"Lagerbestand des {context.PlaceOf}";
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToListAsync(ct);
        var found = CatalogAnswers.Mentioned(items, Noise.Replace(question.Text, " "), i => i.Name);
        if (found.Count == 0)
        {
            return Blocks.NotFound(question.Topic, $"Im Lager des {context.PlaceOf} gibt es dazu kein Material.", source);
        }

        var ids = found.Select(i => i.Id).ToList();
        var rows = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == context.Location.Id && ids.Contains(m.ItemId))
            .GroupBy(m => new { m.ItemId, m.StoragePlaceId }).Select(g => new { g.Key.ItemId, g.Key.StoragePlaceId, Quantity = g.Sum(m => m.Quantity) }).ToListAsync(ct);
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == context.Location.Id).ToDictionaryAsync(p => p.Id, ct);
        var lines = found.Select(item =>
        {
            var own = rows.Where(r => r.ItemId == item.Id && r.Quantity != 0)
                .OrderBy(r => places.TryGetValue(r.StoragePlaceId, out var p) ? p.Position : int.MaxValue)
                .ThenBy(r => places.TryGetValue(r.StoragePlaceId, out var p) ? p.Name : "", StringComparer.CurrentCulture)
                .ToList();
            var total = rows.Where(r => r.ItemId == item.Id).Sum(r => r.Quantity);
            var line = own.Count == 0
                ? $"{item.Name}: kein Bestand im {context.Place}."
                : $"{item.Name}: {string.Join(", ", own.Select(o => $"{Qty(o.Quantity)} {item.Unit} im {(places.TryGetValue(o.StoragePlaceId, out var p) ? p.Name : "Lager")}"))}; gesamt {Qty(total)} {item.Unit}.";
            return total < item.MinimumQuantity ? $"{line} Unter dem Mindestbestand von {Qty(item.MinimumQuantity)} {item.Unit}." : line;
        });
        return Blocks.Answer(question.Topic, string.Join(' ', lines), source);
    }

    private async Task<AssistantBlock> LowAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var source = $"Lagerbestand und Bestellungen des {context.PlaceOf}";
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToListAsync(ct);
        var quantities = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == context.Location.Id)
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) }).ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        var below = items.Where(i => quantities.GetValueOrDefault(i.Id) < i.MinimumQuantity).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        if (below.Count == 0)
        {
            return Blocks.Answer(question.Topic, "Kein Material liegt unter dem Mindestbestand.", source);
        }

        var lines = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.LocationId == context.Location.Id && (o.Status == PurchaseOrderStatus.Ordered || o.Status == PurchaseOrderStatus.PartiallyReceived))
            .SelectMany(o => o.Lines).Select(l => new { l.ItemId, l.Quantity, l.Received }).ToListAsync(ct);
        var ordered = lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.Received)));
        var text = below.Count == 1 ? "1 Material liegt unter dem Mindestbestand." : $"{below.Count} Materialien liegen unter dem Mindestbestand.";
        if (below.Count > Blocks.MaxRows)
        {
            text += $" Die Tabelle zeigt die ersten {Blocks.MaxRows}.";
        }

        return Blocks.Answer(question.Topic, text, source, Blocks.Table(["Material", "Bestand", "Mindestbestand", "Bestellt"],
            below.Select(i => (IReadOnlyList<string>)[i.Name, $"{Qty(quantities.GetValueOrDefault(i.Id))} {i.Unit}", $"{Qty(i.MinimumQuantity)} {i.Unit}",
                ordered.GetValueOrDefault(i.Id) > 0 ? $"{Qty(ordered[i.Id])} {i.Unit}" : "–"])));
    }

    private static string Qty(decimal value) => value.ToString("0.##", AnswerContext.German);
}

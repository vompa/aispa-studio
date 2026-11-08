using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wann kommt die Lieferung?“ (Slice 136): die bestellten, noch nicht ganz gelieferten Bestellungen des Standorts. Das System speichert keinen zugesagten Liefertermin; die Antwort
/// rechnet ihn als „voraussichtlich“ aus dem Bestelldatum und der Lieferzeit des Lieferanten und sagt das in der Quelle. Entwürfe sind noch nicht bestellt und fehlen. Bestellungen sieht,
/// wer das Lager des Standorts verwaltet (wie die Bestell-Endpunkte).
/// </summary>
internal sealed class OrderAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.OpenOrders];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        if (!context.Has(AccessRight.ManageLocation))
        {
            return Blocks.Denied(question.Topic, "Bestellungen sieht, wer das Lager des Standorts verwaltet.");
        }

        var source = $"Bestellungen des {context.PlaceOf} (Bestelldatum plus Lieferzeit des Lieferanten)";
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.LocationId == context.Location.Id && (o.Status == PurchaseOrderStatus.Ordered || o.Status == PurchaseOrderStatus.PartiallyReceived))
            .ToListAsync(ct);
        if (orders.Count == 0)
        {
            return Blocks.Answer(question.Topic, "Es gibt keine offene Bestellung.", source);
        }

        var suppliers = await db.StockSuppliers.AsNoTracking().ToListAsync(ct);
        var items = await db.StockItems.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var rows = orders.Select(o =>
        {
            var orderedOn = LocalTime.LocalDate(o.OrderedAt ?? o.CreatedAt, context.Zone);
            var lead = suppliers.FirstOrDefault(s => string.Equals(s.Name.Trim(), o.Supplier.Trim(), StringComparison.OrdinalIgnoreCase))?.LeadTimeDays;
            DateOnly? expected = lead is { } days ? orderedOn.AddDays(days) : null;
            var open = o.Lines.Where(l => l.Quantity > l.Received).Select(l => items.TryGetValue(l.ItemId, out var item)
                ? $"{item.Name}: {Qty(l.Quantity - l.Received)} {item.Unit}" : $"Material: {Qty(l.Quantity - l.Received)}").OrderBy(line => line, StringComparer.Ordinal);
            return (Order: o, OrderedOn: orderedOn, Expected: expected, Open: string.Join("; ", open));
        }).OrderBy(r => r.Expected ?? DateOnly.MaxValue).ThenBy(r => r.OrderedOn).ThenBy(r => r.Order.Supplier, StringComparer.Ordinal).ThenBy(r => r.Order.Number).ToList();

        string Expected(DateOnly? expected) => expected is not { } day ? "Lieferzeit nicht hinterlegt"
            : day < context.Today ? $"überfällig seit {Date(day)}" : AnswerDates.Long(day);
        var next = rows.Where(r => r.Expected >= context.Today).Select(r => r.Expected).FirstOrDefault();
        var text = (rows.Count == 1 ? "1 offene Bestellung." : $"{rows.Count} offene Bestellungen.")
            + (next is { } nextDay ? $" Die nächste Lieferung kommt voraussichtlich am {AnswerDates.Long(nextDay)}." : "");
        return Blocks.Answer(question.Topic, text, source, Blocks.Table(["Lieferant", "Bestellt am", "Voraussichtlich", "Offen"],
            rows.Select(r => (IReadOnlyList<string>)[r.Order.Supplier, Date(r.OrderedOn), Expected(r.Expected), r.Open])));
    }

    private static string Date(DateOnly day) => day.ToString("dd.MM.yyyy", AnswerContext.German);

    private static string Qty(decimal value) => value.ToString("0.##", AnswerContext.German);
}

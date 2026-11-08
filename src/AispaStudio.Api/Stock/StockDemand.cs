using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Bedarfsprüfung: rechnet den Verbrauch der gebuchten, noch offenen Termine (Material der Behandlung und Extras von „Zum Termin bereitstellen“)
/// zeitlich geordnet gegen den Bestand eines Standorts und meldet, wo der Bestand nicht reicht, die Mindestmenge unterschritten wird
/// oder nur noch für wenige Behandlungen bleibt. Gebucht wird nichts: abgebucht wird weiter erst beim Abschließen der Behandlung.
/// </summary>
internal sealed class StockDemandService(AispaDbContext db, TimeProvider time)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    /// <summary>Ab so wenigen weiteren Behandlungen, die der Rest nach den gebuchten Terminen noch deckt, kommt der Hinweis „reicht noch für N Behandlungen“.</summary>
    public const int LowCoverageTreatments = 3;

    public const int MaxDays = 31;

    /// <summary>Beim Buchen wird nur geprüft, was in diesem Zeitraum liegt; weiter hinaus ist Nachbestellen üblich.</summary>
    public const int BookingHorizonDays = StockService.PlanningDays;

    /// <summary>Ein Termin mit dem, was er verbraucht.</summary>
    internal sealed record DemandEvent(Guid Id, DateTimeOffset At, IReadOnlyDictionary<Guid, decimal> Use);

    internal sealed record ItemResult(StockItem Item, decimal OnHand, decimal Needed, int Uses, decimal Rest, int TreatmentsLeft,
        StockWarningKind? Kind, DateTimeOffset? ShortFrom);

    public async Task<StockDemandDto?> DemandAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        if (await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
        {
            return null;
        }

        var zone = LocalTime.Zone(timeZone);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var events = await EventsAsync(locationId, start, end, ct);
        var lines = ToLines(await EvaluateAsync(locationId, events, ct), zone);
        return new(locationId, from, until, events.Count, lines, [.. lines.Where(l => l.Warning != null)]);
    }

    /// <summary>
    /// Was ein Termin dieser Behandlung an dieser Zeit am Standort an Grenzen reißt: Meldungen zu Material, das die Behandlung braucht und das mit dem
    /// Termin knapper wird als ohne ihn. <paramref name="existingId"/>: der Termin ist schon gebucht (er zählt nicht doppelt). Leer, wenn der Termin
    /// jenseits des Prüfzeitraums liegt.
    /// </summary>
    public async Task<List<StockDemandLine>> BookingCheckAsync(Guid locationId, Guid treatmentId, DateTimeOffset start, Guid? existingId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (start < now || start >= now.AddDays(BookingHorizonDays)
            || await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
        {
            return [];
        }

        var recipe = await StockRecipes.ForAsync(db, treatmentId, ct);
        if (recipe.Count == 0)
        {
            return [];
        }

        var others = (await EventsAsync(locationId, now, now.AddDays(BookingHorizonDays), ct)).Where(e => e.Id != existingId).ToList();
        var added = new DemandEvent(existingId ?? Guid.NewGuid(), start, recipe);
        var without = (await EvaluateAsync(locationId, others, ct)).ToDictionary(r => r.Item.Id);
        var with = await EvaluateAsync(locationId, [.. others, added], ct);
        var zone = LocalTime.Zone(timeZone);
        return [.. ToLines(with, zone).Where(l => recipe.ContainsKey(l.ItemId) && l.Warning is { } kind
            && (!without.TryGetValue(l.ItemId, out var previous) || previous.Kind is not { } before || kind < before))];
    }

    private async Task<List<DemandEvent>> EventsAsync(Guid locationId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
    {
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && Open.Contains(a.Status))
            .Select(a => new { a.Id, a.TreatmentId, a.Start }).ToListAsync(ct);
        var recipes = await StockRecipes.ForAsync(db, appointments.Select(a => a.TreatmentId).Distinct().ToList(), ct);
        return [.. appointments.Where(a => recipes.ContainsKey(a.TreatmentId)).OrderBy(a => a.Start).ThenBy(a => a.Id)
            .Select(a => new DemandEvent(a.Id, a.Start, recipes[a.TreatmentId]))];
    }

    private async Task<List<ItemResult>> EvaluateAsync(Guid locationId, IReadOnlyList<DemandEvent> events, CancellationToken ct)
    {
        var itemIds = events.SelectMany(e => e.Use.Keys).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive && itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var onHand = await db.StockMovements.AsNoTracking().Where(m => m.LocationId == locationId && itemIds.Contains(m.ItemId))
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Quantity, ct);
        return Evaluate(events, items, onHand);
    }

    /// <summary>Reine Rechnung: je Material Bedarf, Rest, Reichweite und Stufe der Meldung; die Termine laufen in zeitlicher Reihenfolge.</summary>
    internal static List<ItemResult> Evaluate(IEnumerable<DemandEvent> events, IReadOnlyDictionary<Guid, StockItem> items,
        IReadOnlyDictionary<Guid, decimal> onHand)
    {
        var ordered = events.OrderBy(e => e.At).ThenBy(e => e.Id).ToList();
        var results = new List<ItemResult>();
        foreach (var item in items.Values.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            var stock = onHand.GetValueOrDefault(item.Id);
            var running = stock;
            DateTimeOffset? shortFrom = null;
            decimal needed = 0;
            var uses = 0;
            foreach (var e in ordered.Where(e => e.Use.GetValueOrDefault(item.Id) > 0))
            {
                var use = e.Use[item.Id];
                needed += use;
                uses++;
                running -= use;
                if (running < 0 && shortFrom is null)
                {
                    shortFrom = e.At;
                }
            }

            if (uses == 0)
            {
                continue;
            }

            var rest = stock - needed;
            var perUse = needed / uses;
            var left = perUse > 0 ? (int)Math.Floor(Math.Max(0, rest) / perUse) : 0;
            StockWarningKind? kind = needed > stock ? StockWarningKind.Insufficient
                : item.MinimumQuantity > 0 && rest < item.MinimumQuantity ? StockWarningKind.BelowMinimum
                : left <= LowCoverageTreatments ? StockWarningKind.LowCoverage
                : null;
            results.Add(new(item, stock, needed, uses, rest, left, kind, shortFrom));
        }

        return results;
    }

    private static List<StockDemandLine> ToLines(IEnumerable<ItemResult> results, TimeZoneInfo zone) =>
        [.. results.Select(r => new StockDemandLine(r.Item.Id, r.Item.Name, r.Item.Unit, r.OnHand, r.Needed, r.Item.MinimumQuantity, r.Uses,
            r.TreatmentsLeft, r.Kind, r.ShortFrom, r.Kind is null ? null : Message(r, zone), Math.Max(0, -r.Rest)))];

    internal static string Message(ItemResult r, TimeZoneInfo zone)
    {
        var unit = r.Item.Unit;
        return r.Kind switch
        {
            StockWarningKind.Insufficient =>
                $"{r.Item.Name}: Bestand {Amount(r.OnHand)} {unit} reicht nicht für alle gebuchten Termine (Bedarf {Amount(r.Needed)} {unit}, "
                    + $"{r.Uses} {(r.Uses == 1 ? "Termin" : "Termine")}); er reicht noch für {Math.Min(r.Uses, (int)Math.Floor(Math.Max(0, r.OnHand) / (r.Needed / r.Uses)))} "
                    + $"{(r.Uses == 1 ? "Behandlung" : "Behandlungen")}, ab {Local(r.ShortFrom!.Value, zone)} fehlt es.",
            StockWarningKind.BelowMinimum =>
                $"{r.Item.Name}: nach den gebuchten Terminen bleiben {Amount(r.Rest)} {unit}, unter der Mindestmenge von {Amount(r.Item.MinimumQuantity)} {unit}; "
                    + $"das reicht noch für {r.TreatmentsLeft} {(r.TreatmentsLeft == 1 ? "Behandlung" : "Behandlungen")}.",
            _ => $"{r.Item.Name} reicht nach den gebuchten Terminen noch für {r.TreatmentsLeft} {(r.TreatmentsLeft == 1 ? "Behandlung" : "Behandlungen")} "
                + $"(Rest {Amount(r.Rest)} {unit}).",
        };
    }

    private static string Amount(decimal value) => value.ToString("0.###", German);

    private static string Local(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone).ToString("dd.MM. HH:mm", German);
}

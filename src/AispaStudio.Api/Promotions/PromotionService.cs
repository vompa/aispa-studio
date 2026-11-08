using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Promotions;

/// <summary>
/// Aktionen verwalten und abfragen (ADR 0071). Die Verwaltung gehört Inhaber und Standortleitung (<see cref="AccessRight.ManageCatalog"/>);
/// wer nur einzelne Standorte sieht, verwaltet nur Aktionen dieser Standorte und keine für alle. Lesen für Kassa und Assistent:
/// <see cref="GetActiveAsync(Guid, DateOnly, CancellationToken)"/>.
/// </summary>
internal sealed class PromotionService(AispaDbContext db, TimeProvider time)
{
    // ---------- Abfrage für Kassa und Assistent ----------

    /// <summary>
    /// Die Aktionen, die an diesem Standort an diesem Kalendertag laufen: aktiv, nicht archiviert, im Zeitraum, am Standort gültig. Mit Gästetext,
    /// Rabatt und Gültigkeit (Behandlungen mit Namen). Der Tag ist der Kalendertag in der Zeitzone des Standorts, <see cref="TodayAsync"/> liefert „heute“.
    /// Reihenfolge: früheres Ende zuerst, dann nach Name. Gedacht für den Assistenten (Frage „Welche Aktionen laufen gerade?“) und die Kassa.
    /// </summary>
    public Task<IReadOnlyList<ActivePromotionDto>> GetActiveAsync(Guid locationId, DateOnly date, CancellationToken ct) =>
        GetActiveAsync(locationId, date, date, ct);

    /// <summary>
    /// Die Aktionen, die an diesem Standort an mindestens einem Kalendertag von <paramref name="from"/> bis <paramref name="until"/> laufen (beide eingeschlossen),
    /// zum Beispiel für „Gibt es im November ein Angebot?“. Mit gleichem Anfang und Ende dasselbe wie für einen einzelnen Tag.
    /// </summary>
    public async Task<IReadOnlyList<ActivePromotionDto>> GetActiveAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var currency = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.Currency).FirstOrDefaultAsync(ct);
        if (currency is null)
        {
            return [];
        }

        var promotions = await RunningAsync(db, locationId, from, until, ct);
        var treatmentIds = promotions.SelectMany(p => p.Treatments.Select(t => t.TreatmentId)).Distinct().ToList();
        var names = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        return [.. promotions.Select(p => new ActivePromotionDto(p.Id, p.Name, p.GuestText, p.ValidFrom, p.ValidUntil, p.DiscountPercent, p.DiscountAmount,
            PromotionRules.DiscountText(p, currency), p.Scope,
            [.. p.Treatments.Select(t => new PromotionTreatmentDto(t.TreatmentId, names.GetValueOrDefault(t.TreatmentId, "")))
                .OrderBy(t => t.Name, StringComparer.Ordinal)]))];
    }

    /// <summary>Laufende Aktionen mit Standorten und Behandlungen (ohne Verfolgung), früheres Ende zuerst; auch für die Kassa.</summary>
    public static async Task<List<Promotion>> RunningAsync(AispaDbContext db, Guid locationId, DateOnly date, CancellationToken ct) =>
        [.. (await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery()
                .Where(p => p.IsActive && p.ArchivedAt == null && p.ValidFrom <= date && date <= p.ValidUntil)
                .ToListAsync(ct))
            .Where(p => PromotionRules.IsRunning(p, locationId, date))
            .OrderBy(p => p.ValidUntil).ThenBy(p => p.Name, StringComparer.Ordinal)];

    /// <summary>Aktionen, die an mindestens einem Tag von <paramref name="from"/> bis <paramref name="until"/> laufen; mit Standorten und Behandlungen, früheres Ende zuerst.</summary>
    public static async Task<List<Promotion>> RunningAsync(AispaDbContext db, Guid locationId, DateOnly from, DateOnly until, CancellationToken ct) =>
        [.. (await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery()
                .Where(p => p.IsActive && p.ArchivedAt == null && p.ValidFrom <= until && from <= p.ValidUntil)
                .ToListAsync(ct))
            .Where(p => PromotionRules.IsRunningDuring(p, locationId, from, until))
            .OrderBy(p => p.ValidUntil).ThenBy(p => p.Name, StringComparer.Ordinal)];

    /// <summary>Der heutige Kalendertag in der Zeitzone des Standorts; null, wenn es den Standort nicht gibt.</summary>
    public async Task<DateOnly?> TodayAsync(Guid locationId, CancellationToken ct) =>
        await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is { } zone
            ? LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(zone))
            : null;

    // ---------- Verwaltung ----------

    public async Task<List<PromotionDto>> ListAsync(bool includeArchived, UserAccess access, CancellationToken ct)
    {
        var query = db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery();
        var promotions = await (includeArchived ? query : query.Where(p => p.ArchivedAt == null)).ToListAsync(ct);
        var zones = await ZonesAsync(ct);
        return [.. promotions.Where(p => Manages(access, p))
            .OrderBy(p => p.ValidFrom).ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => ToDto(p, zones))];
    }

    public async Task<PromotionDto?> GetAsync(Guid id, UserAccess access, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, false, ct);
        return promotion is not null && Manages(access, promotion) ? ToDto(promotion, await ZonesAsync(ct)) : null;
    }

    public async Task<(PromotionDto? Promotion, Errors? Errors)> CreateAsync(PromotionInput input, UserAccess access, CancellationToken ct)
    {
        var errors = await ValidateAsync(input, null, access, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        var promotion = new Promotion { Name = input.Name.Trim(), GuestText = input.GuestText.Trim(), CreatedAt = time.GetUtcNow() };
        Apply(promotion, input);
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync(ct);
        return (ToDto(promotion, await ZonesAsync(ct)), null);
    }

    /// <summary>Ändert eine Aktion. <c>(null, null)</c>: gibt es nicht (oder gehört zu Standorten, die die Person nicht verwalten darf).</summary>
    public async Task<(PromotionDto? Promotion, Errors? Errors)> UpdateAsync(Guid id, PromotionInput input, UserAccess access, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, true, ct);
        if (promotion is null || !Manages(access, promotion))
        {
            return (null, null);
        }

        var errors = await ValidateAsync(input, promotion, access, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        promotion.Name = input.Name.Trim();
        promotion.GuestText = input.GuestText.Trim();
        db.PromotionLocations.RemoveRange(promotion.Locations);
        db.PromotionTreatments.RemoveRange(promotion.Treatments);
        promotion.Locations.Clear();
        promotion.Treatments.Clear();
        Apply(promotion, input);
        await db.SaveChangesAsync(ct);
        return (ToDto(promotion, await ZonesAsync(ct)), null);
    }

    /// <summary>Archivieren oder zurückholen. Die Aktion bleibt im Verlauf; archiviert läuft sie nie mehr. Null, wenn es sie nicht gibt.</summary>
    public async Task<PromotionDto?> SetArchivedAsync(Guid id, bool archived, UserAccess access, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, true, ct);
        if (promotion is null || !Manages(access, promotion))
        {
            return null;
        }

        promotion.ArchivedAt = archived ? promotion.ArchivedAt ?? time.GetUtcNow() : null;
        await db.SaveChangesAsync(ct);
        return ToDto(promotion, await ZonesAsync(ct));
    }

    private async Task<Promotion?> LoadAsync(Guid id, bool track, CancellationToken ct)
    {
        var query = db.Promotions.Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery();
        return await (track ? query : query.AsNoTracking()).FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    private async Task<Errors> ValidateAsync(PromotionInput input, Promotion? existing, UserAccess access, CancellationToken ct)
    {
        var errors = new Errors();
        var name = input.Name?.Trim() ?? "";
        errors.Check(name.Length is > 0 and <= PromotionModel.NameLength, nameof(input.Name), $"Name mit höchstens {PromotionModel.NameLength} Zeichen angeben.");
        var guestText = input.GuestText?.Trim() ?? "";
        errors.Check(guestText.Length is > 0 and <= PromotionModel.GuestTextLength, nameof(input.GuestText),
            $"Gästetext mit höchstens {PromotionModel.GuestTextLength} Zeichen angeben.");
        var self = existing?.Id ?? Guid.Empty;
        var taken = await db.Promotions.AsNoTracking().Where(p => p.ArchivedAt == null && p.Id != self).Select(p => p.Name).ToListAsync(ct);
        errors.Check(!taken.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)), nameof(input.Name),
            "Eine Aktion mit diesem Namen gibt es schon. Bitte einen anderen Namen wählen oder die alte archivieren.");
        errors.Check(input.ValidFrom <= input.ValidUntil, nameof(input.ValidUntil), "Das Ende darf nicht vor dem Beginn liegen.");

        errors.Check((input.DiscountPercent is null) != (input.DiscountAmount is null), nameof(input.DiscountPercent),
            "Entweder Prozent oder einen Betrag angeben, nicht beides.");
        if (input.DiscountPercent is { } percent)
        {
            errors.Check(percent is > 0 and < 100 && decimal.Round(percent, 2) == percent, nameof(input.DiscountPercent),
                "Rabatt in Prozent zwischen 0,01 und 99,99.");
        }

        if (input.DiscountAmount is { } amount)
        {
            errors.Check(amount is > 0 and < 100_000 && decimal.Round(amount, 2) == amount, nameof(input.DiscountAmount),
                "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.");
        }

        var locationIds = (input.LocationIds ?? []).Distinct().ToList();
        if (input.AllLocations)
        {
            errors.Check(locationIds.Count == 0, nameof(input.LocationIds), "Bei „alle Standorte“ keine einzelnen Standorte wählen.");
            errors.Check(access.AllLocations, nameof(input.AllLocations), "Aktionen für alle Standorte legt nur die Inhaberin oder der Inhaber an.");
        }
        else
        {
            errors.Check(locationIds.Count > 0, nameof(input.LocationIds), "Mindestens einen Standort wählen.");
            errors.Check(locationIds.All(access.CanSee), nameof(input.LocationIds), "Für einen dieser Standorte hast du keinen Zugang.");
            errors.Check(await db.Locations.CountAsync(l => locationIds.Contains(l.Id), ct) == locationIds.Count, nameof(input.LocationIds), "Standort nicht gefunden.");
        }

        var treatmentIds = (input.TreatmentIds ?? []).Distinct().ToList();
        if (!Enum.IsDefined(input.Scope))
        {
            errors.Add(nameof(input.Scope), "Gültigkeit wählen.");
        }
        else if (input.Scope == PromotionScope.AllTreatments)
        {
            errors.Check(treatmentIds.Count == 0, nameof(input.TreatmentIds), "Bei „alle Behandlungen“ keine einzelnen Behandlungen wählen.");
        }
        else
        {
            errors.Check(treatmentIds.Count > 0, nameof(input.TreatmentIds), "Mindestens eine Behandlung wählen.");
            errors.Check(await db.Treatments.CountAsync(t => treatmentIds.Contains(t.Id), ct) == treatmentIds.Count, nameof(input.TreatmentIds), "Behandlung nicht gefunden.");
        }

        return errors;
    }

    /// <summary>Übernimmt die Eingabe (ohne Name und Gästetext, die der Aufrufer getrimmt setzt); Standorte und Behandlungen werden neu angelegt.</summary>
    private void Apply(Promotion promotion, PromotionInput input)
    {
        promotion.ValidFrom = input.ValidFrom;
        promotion.ValidUntil = input.ValidUntil;
        promotion.AllLocations = input.AllLocations;
        promotion.DiscountPercent = input.DiscountPercent;
        promotion.DiscountAmount = input.DiscountAmount;
        promotion.Scope = input.Scope;
        promotion.IsActive = input.IsActive;
        if (!input.AllLocations)
        {
            foreach (var locationId in (input.LocationIds ?? []).Distinct())
            {
                var link = new PromotionLocation { PromotionId = promotion.Id, LocationId = locationId };
                promotion.Locations.Add(link);
                db.PromotionLocations.Add(link);
            }
        }

        if (input.Scope != PromotionScope.AllTreatments)
        {
            foreach (var treatmentId in (input.TreatmentIds ?? []).Distinct())
            {
                var link = new PromotionTreatment { PromotionId = promotion.Id, TreatmentId = treatmentId };
                promotion.Treatments.Add(link);
                db.PromotionTreatments.Add(link);
            }
        }
    }

    /// <summary>Wer nur einzelne Standorte sieht, verwaltet nur Aktionen, die ganz in seinen Standorten liegen.</summary>
    private static bool Manages(UserAccess access, Promotion promotion) =>
        access.AllLocations || (!promotion.AllLocations && promotion.Locations.All(l => access.CanSee(l.LocationId)));

    private async Task<Dictionary<Guid, TimeZoneInfo>> ZonesAsync(CancellationToken ct) =>
        (await db.Locations.AsNoTracking().Select(l => new { l.Id, l.TimeZone }).ToListAsync(ct)).ToDictionary(l => l.Id, l => LocalTime.Zone(l.TimeZone));

    /// <summary>Der Zustand zählt am heutigen Tag des ersten Standorts der Aktion (bei „alle“ des ersten Standorts des Spas).</summary>
    private PromotionDto ToDto(Promotion p, Dictionary<Guid, TimeZoneInfo> zones)
    {
        var zone = (p.Locations.Count > 0 ? zones.GetValueOrDefault(p.Locations.Min(l => l.LocationId)) : null) ?? zones.Values.FirstOrDefault() ?? TimeZoneInfo.Utc;
        return new(p.Id, p.Name, p.GuestText, p.ValidFrom, p.ValidUntil, p.AllLocations, [.. p.Locations.Select(l => l.LocationId).Order()],
            p.DiscountPercent, p.DiscountAmount, p.Scope, [.. p.Treatments.Select(t => t.TreatmentId).Order()], p.IsActive, p.ArchivedAt is not null,
            PromotionRules.PhaseOf(p, LocalTime.LocalDate(time.GetUtcNow(), zone)));
    }
}

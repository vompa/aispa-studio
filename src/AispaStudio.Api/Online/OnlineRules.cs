using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Online;

/// <summary>Eigene Regeln der Online-Buchung eines Standorts (Stammdaten, mandantengetrennt). Fehlt der Eintrag, gelten die Vorgaben.</summary>
public sealed class LocationOnlineSettings
{
    public Guid LocationId { get; init; }
    public bool BookingEnabled { get; set; } = true;
    public int MinLeadMinutes { get; set; }
    public int MaxDaysAhead { get; set; }
    public int CancellationDeadlineHours { get; set; }
    public int SlotStepMinutes { get; set; }
    public string? GuestNote { get; set; }
    public int NoShowFeePercent { get; set; }
    public int CardHoldHours { get; set; }

    /// <summary>Ausfallgebühr und Anzahlung nur von Gästen verlangen, die noch keine abgeschlossene Behandlung im Spa hatten.</summary>
    public bool NoShowFeeNewCustomersOnly { get; set; }

    /// <summary>Anzahlung beim Online-Buchen in Prozent des Preises (0 = keine, 100 = Vorauszahlung); ersetzt die Kartenhinterlegung.</summary>
    public int DepositPercent { get; set; }

    /// <summary>Stornoregel in eigenen Worten für Team und Assistent (nicht für den Ablauf der Online-Buchung), z. B. „Absage bis 24 Stunden vorher kostenlos“.</summary>
    public string? CancellationPolicy { get; set; }
}

internal static class OnlineRulesModel
{
    public const int MaxGuestNoteLength = 300;
    public const int MaxCancellationPolicyLength = 500;

    public static void Configure(ModelBuilder builder) =>
        builder.Entity<LocationOnlineSettings>(e =>
        {
            e.HasKey(s => s.LocationId);
            e.HasOne<Location>().WithOne().HasForeignKey<LocationOnlineSettings>(s => s.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.GuestNote).HasMaxLength(MaxGuestNoteLength);
            e.Property(s => s.CancellationPolicy).HasMaxLength(MaxCancellationPolicyLength);
        });
}

/// <summary>
/// Liefert die geltenden Regeln je Standort: eigene Einstellung aus den Stammdaten oder die Vorgaben aus
/// <see cref="OnlineBookingOptions"/>. Merkt sich die Regeln für die Dauer der Anfrage.
/// </summary>
internal sealed class OnlineRules(AispaDbContext db, IOptions<OnlineBookingOptions> options)
{
    public static readonly int[] SlotSteps = [5, 10, 15, 20, 30, 60];

    /// <summary>Höchstens eine Woche: länger hält niemand einen Termin ohne Karte frei.</summary>
    public const int MaxCardHoldHours = 168;

    private readonly Dictionary<Guid, LocationOnlineRules> _cache = [];

    public LocationOnlineRules Defaults => new(true, options.Value.MinLeadMinutes, options.Value.MaxDaysAhead,
        options.Value.CancellationDeadlineHours, SlotSteps.Contains(options.Value.SlotStepMinutes) ? options.Value.SlotStepMinutes : 15, null, true,
        Math.Clamp(options.Value.NoShowFeePercent, 0, 100), Math.Clamp(options.Value.CardHoldHours, 0, MaxCardHoldHours),
        options.Value.NoShowFeeNewCustomersOnly, Math.Clamp(options.Value.DepositPercent, 0, 100));

    public async Task<LocationOnlineRules> ForAsync(Guid locationId, CancellationToken ct)
    {
        if (_cache.TryGetValue(locationId, out var cached))
        {
            return cached;
        }

        var settings = await db.LocationOnlineSettings.AsNoTracking().FirstOrDefaultAsync(s => s.LocationId == locationId, ct);
        return _cache[locationId] = settings is null ? Defaults : ToRules(settings);
    }

    public async Task<IReadOnlyDictionary<Guid, LocationOnlineRules>> ForAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken ct)
    {
        var missing = locationIds.Where(id => !_cache.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var found = await db.LocationOnlineSettings.AsNoTracking().Where(s => missing.Contains(s.LocationId)).ToDictionaryAsync(s => s.LocationId, ct);
            foreach (var id in missing)
            {
                _cache[id] = found.TryGetValue(id, out var s) ? ToRules(s) : Defaults;
            }
        }

        return locationIds.Distinct().ToDictionary(id => id, id => _cache[id]);
    }

    public static Errors Validate(LocationOnlineRules input)
    {
        var errors = new Errors();
        errors.Check(input.MinLeadMinutes is >= 0 and <= 7 * 24 * 60, nameof(input.MinLeadMinutes), "Vorlauf von 0 Minuten bis 7 Tage.");
        errors.Check(input.MaxDaysAhead is >= 1 and <= 365, nameof(input.MaxDaysAhead), "Buchungshorizont von 1 bis 365 Tagen.");
        errors.Check(input.CancellationDeadlineHours is >= 0 and <= 14 * 24, nameof(input.CancellationDeadlineHours), "Stornofrist von 0 bis 336 Stunden.");
        errors.Check(SlotSteps.Contains(input.SlotStepMinutes), nameof(input.SlotStepMinutes), "Raster 5, 10, 15, 20, 30 oder 60 Minuten.");
        errors.Check((input.GuestNote?.Length ?? 0) <= OnlineRulesModel.MaxGuestNoteLength, nameof(input.GuestNote),
            $"Höchstens {OnlineRulesModel.MaxGuestNoteLength} Zeichen.");
        errors.Check((input.CancellationPolicy?.Length ?? 0) <= OnlineRulesModel.MaxCancellationPolicyLength, nameof(input.CancellationPolicy),
            $"Höchstens {OnlineRulesModel.MaxCancellationPolicyLength} Zeichen.");
        errors.Check(input.NoShowFeePercent is >= 0 and <= 100, nameof(input.NoShowFeePercent), "Ausfallgebühr von 0 bis 100 Prozent.");
        errors.Check(input.CardHoldHours is >= 0 and <= MaxCardHoldHours, nameof(input.CardHoldHours), "Frist für Karte oder Anzahlung von 0 bis 168 Stunden.");
        errors.Check(input.DepositPercent is >= 0 and <= 100, nameof(input.DepositPercent), "Anzahlung von 0 bis 100 Prozent.");
        return errors;
    }

    public static LocationOnlineRules ToRules(LocationOnlineSettings s) =>
        new(s.BookingEnabled, s.MinLeadMinutes, s.MaxDaysAhead, s.CancellationDeadlineHours, s.SlotStepMinutes, s.GuestNote, false, s.NoShowFeePercent, s.CardHoldHours,
            s.NoShowFeeNewCustomersOnly, s.DepositPercent, s.CancellationPolicy);
}

/// <summary>Regeln der Online-Buchung je Standort pflegen (Standortleitung aufwärts).</summary>
internal static class OnlineRulesEndpoints
{
    public static IEndpointRouteBuilder MapOnlineRulesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations/{locationId:guid}/online-rules").WithTags("Stammdaten");

        group.MapGet("", async Task<Results<Ok<LocationOnlineRules>, NotFound>> (Guid locationId, AispaDbContext db, OnlineRules rules, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) ? TypedResults.Ok(await rules.ForAsync(locationId, ct)) : TypedResults.NotFound())
            .WithName("GetLocationOnlineRules")
            .WithSummary("Regeln der Online-Buchung des Standorts (eigene oder Vorgaben).")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("", async Task<Results<Ok<LocationOnlineRules>, NotFound, ValidationProblem>> (
                Guid locationId, LocationOnlineRules input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                if (OnlineRules.Validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                var settings = await db.LocationOnlineSettings.FirstOrDefaultAsync(s => s.LocationId == locationId, ct);
                if (settings is null)
                {
                    db.LocationOnlineSettings.Add(settings = new LocationOnlineSettings { LocationId = locationId });
                }

                settings.BookingEnabled = input.BookingEnabled;
                settings.MinLeadMinutes = input.MinLeadMinutes;
                settings.MaxDaysAhead = input.MaxDaysAhead;
                settings.CancellationDeadlineHours = input.CancellationDeadlineHours;
                settings.SlotStepMinutes = input.SlotStepMinutes;
                settings.GuestNote = string.IsNullOrWhiteSpace(input.GuestNote) ? null : input.GuestNote.Trim();
                settings.NoShowFeePercent = input.NoShowFeePercent;
                settings.CardHoldHours = input.CardHoldHours;
                settings.NoShowFeeNewCustomersOnly = input.NoShowFeeNewCustomersOnly;
                settings.DepositPercent = input.DepositPercent;
                settings.CancellationPolicy = string.IsNullOrWhiteSpace(input.CancellationPolicy) ? null : input.CancellationPolicy.Trim();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(OnlineRules.ToRules(settings));
            })
            .WithName("SetLocationOnlineRules")
            .WithSummary("Eigene Regeln der Online-Buchung für den Standort speichern.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("", async Task<Results<Ok<LocationOnlineRules>, NotFound>> (
                Guid locationId, AispaDbContext db, OnlineRules rules, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                await db.LocationOnlineSettings.Where(s => s.LocationId == locationId).ExecuteDeleteAsync(ct);
                return TypedResults.Ok(rules.Defaults);
            })
            .WithName("ResetLocationOnlineRules")
            .WithSummary("Eigene Regeln entfernen; danach gelten wieder die Vorgaben.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }
}

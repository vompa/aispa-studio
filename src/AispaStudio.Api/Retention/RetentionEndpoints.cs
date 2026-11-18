using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Retention;

internal static class RetentionSetup
{
    /// <summary>Erinnerungen an die nächste Behandlung: Planung im Hintergrund (<c>Retention:PollSeconds</c>), Schreiben und Versand über die Queue.</summary>
    public static IServiceCollection AddRetention(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<RetentionOptions>(configuration.GetSection(RetentionOptions.Section));
        services.AddScoped<MarketingConsents>();
        services.AddScoped<RebookPlanner>();
        services.AddScoped<RebookSender>();
        services.AddSingleton<RebookScheduler>();
        if (modules.IsOn(AppModule.Rebooking))
        {
            services.AddBackgroundService<RebookScheduler>(modules);
        }

        return services;
    }
}

internal static class RetentionEndpoints
{
    /// <summary>Kürzer ist eher lästig als hilfreich, länger als zwei Jahre erinnert sich niemand an die Einwilligung.</summary>
    private const int MinDays = 7;

    private const int MaxDays = 730;

    public static IEndpointRouteBuilder MapRetentionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Wiederbuchungs-Hinweis");

        group.MapGet("/retention/settings", async (AispaDbContext db, CancellationToken ct) => await LoadAsync(db, ct))
            .WithName("GetRebookSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Ob und nach wie vielen Tagen je Behandlung ein Wiederbuchungs-Hinweis verschickt wird (ohne Eintrag aus).");

        group.MapPut("/retention/settings", async Task<Results<Ok<RebookSettingsDto>, ValidationProblem>> (
                RebookSettingsDto input, AispaDbContext db, CancellationToken ct) =>
            {
                var intervals = input.Intervals ?? [];
                var silent = input.SilentTreatmentIds ?? [];
                var categories = input.CategoryIntervals ?? [];
                var errors = new Errors();
                errors.Check(intervals.All(i => i.Days is >= MinDays and <= MaxDays), nameof(input.Intervals),
                    $"Zwischen {MinDays} und {MaxDays} Tagen.");
                errors.Check(intervals.Select(i => i.TreatmentId).Concat(silent).Distinct().Count() == intervals.Count + silent.Count, nameof(input.Intervals),
                    "Jede Behandlung nur einmal.");
                var ids = intervals.Select(i => i.TreatmentId).Concat(silent).Distinct().ToList();
                errors.Check(await db.Treatments.CountAsync(t => ids.Contains(t.Id), ct) == ids.Count, nameof(input.Intervals),
                    "Unbekannte Behandlung.");
                errors.Check(input.DefaultDays is null or >= MinDays and <= MaxDays, nameof(input.DefaultDays), $"Zwischen {MinDays} und {MaxDays} Tagen.");
                errors.Check(categories.All(c => c.Days is null or >= MinDays and <= MaxDays), nameof(input.CategoryIntervals),
                    $"Zwischen {MinDays} und {MaxDays} Tagen.");
                errors.Check(categories.Select(c => c.Category?.Trim() ?? "").Distinct(StringComparer.OrdinalIgnoreCase).Count() == categories.Count, nameof(input.CategoryIntervals),
                    "Jede Kategorie nur einmal.");
                var known = (await db.Treatments.AsNoTracking().Where(t => t.Category != null).Select(t => t.Category!).Distinct().ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                errors.Check(categories.All(c => c.Category is { } name && known.Contains(name.Trim())), nameof(input.CategoryIntervals), "Unbekannte Kategorie.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var settings = await db.RebookSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
                if (settings is null)
                {
                    settings = new RebookSettings();
                    db.RebookSettings.Add(settings);
                }

                settings.Enabled = input.Enabled;
                var withCategories = input.CategoryIntervals is not null;
                if (withCategories)
                {
                    settings.DefaultDays = input.DefaultDays;
                    await SyncCategoriesAsync(db, categories, known, ct);
                }

                await SyncTreatmentsAsync(db, intervals, silent, keepSilent: !withCategories, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await LoadAsync(db, ct));
            })
            .WithName("UpdateRebookSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Wiederbuchungs-Hinweis einstellen: Standardabstand und Ausnahmen je Kategorie oder Behandlung. Er geht nur an Gäste mit bestätigter Einwilligung.");

        group.MapGet("/retention/reminders", async (AispaDbContext db, CancellationToken ct) =>
            {
                var reminders = await db.RebookReminders.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
                var customerIds = reminders.Select(r => r.CustomerId).Distinct().ToList();
                var treatmentIds = reminders.Select(r => r.TreatmentId).Distinct().ToList();
                var names = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.InHouseName, ct);
                var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
                return reminders.Select(r => new RebookReminderDto(
                    r.Id, r.CustomerId, names.GetValueOrDefault(r.CustomerId, ""), treatments.GetValueOrDefault(r.TreatmentId, ""), r.State.ToString(),
                    r.AiWritten, r.Text, r.CreatedAt, r.SentAt, r.Error)).ToList();
            })
            .WithName("ListRebookReminders")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Die letzten 100 Wiederbuchungs-Hinweise: an wen, mit welchem Text, mit welchem Ergebnis.");

        group.MapGet("/customers/{customerId:guid}/marketing-consent", async Task<Results<Ok<MarketingConsentDto>, NotFound>> (
                Guid customerId, MarketingConsents consents, EntityHistory history, CancellationToken ct) =>
                await consents.ForCustomerAsync(customerId, ct) is { } dto ? TypedResults.Ok(await WithRecorderAsync(dto, customerId, history, ct)) : TypedResults.NotFound())
            .WithName("GetMarketingConsent")
            .RequirePermission(AccessRight.ViewCalendar)
            .WithSummary("Einwilligung des Gastes in Wiederbuchungs-Hinweise.");

        group.MapPost("/customers/{customerId:guid}/marketing-consent", async Task<Results<Ok<MarketingConsentDto>, NotFound, ValidationProblem>> (
                Guid customerId, MarketingConsentRequest input, MarketingConsents consents, CancellationToken ct) =>
                await consents.RequestAsync(customerId, input.Language, MarketingConsents.SourceTeam, ct) switch
                {
                    MarketingConsents.Outcome.Done d => TypedResults.Ok(d.Consent),
                    MarketingConsents.Outcome.Refused r => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [r.Field] = [r.Message] }),
                    _ => TypedResults.NotFound(),
                })
            .WithName("RequestMarketingConsent")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Fragt den Gast per Mail um Einwilligung (Double-Opt-in). Erst nach seiner Bestätigung gehen Wiederbuchungs-Hinweise.");

        group.MapPut("/customers/{customerId:guid}/marketing-consent", async Task<Results<Ok<MarketingConsentDto>, NotFound, ValidationProblem>> (
                Guid customerId, MarketingConsentDecision input, MarketingConsents consents, EntityHistory history, CancellationToken ct) =>
                await consents.DecideAsync(customerId, input.State, ct) switch
                {
                    MarketingConsents.Outcome.Done d => TypedResults.Ok(await WithRecorderAsync(d.Consent, customerId, history, ct)),
                    MarketingConsents.Outcome.Refused r => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [r.Field] = [r.Message] }),
                    _ => TypedResults.NotFound(),
                })
            .WithName("DecideMarketingConsent")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Trägt ein, was der Gast am Empfang gesagt hat: Ja (Werbung per Mail, Quelle „Rezeption“), Nein oder nicht gefragt. Eine per Mail bestätigte Einwilligung bleibt bei Ja bestehen.");

        group.MapDelete("/customers/{customerId:guid}/marketing-consent", async Task<Results<Ok<MarketingConsentDto>, NotFound>> (
                Guid customerId, MarketingConsents consents, CancellationToken ct) =>
                await consents.WithdrawAsync(customerId, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .WithName("WithdrawMarketingConsent")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Widerruf, z. B. wenn der Gast es am Empfang sagt.");

        var open = routes.MapGroup("/api/online/consent").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        open.MapGet("/{token}", async Task<Results<Ok<OnlineConsentDto>, NotFound>> (string token, MarketingConsents consents, CancellationToken ct) =>
                await consents.ForTokenAsync(token, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .WithName("GetOnlineConsent")
            .WithSummary("Stand der Einwilligung über den Link aus der Mail. Ändert nichts (Virenscanner rufen Links vorab auf).");

        open.MapPost("/{token}/confirm", async Task<Results<Ok<OnlineConsentDto>, NotFound>> (string token, MarketingConsents consents, CancellationToken ct) =>
                await consents.ConfirmAsync(token, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("ConfirmOnlineConsent")
            .WithSummary("Bestätigt die Einwilligung (zweiter Schritt des Double-Opt-in).");

        open.MapPost("/{token}/withdraw", async Task<Results<Ok<OnlineConsentDto>, NotFound>> (string token, MarketingConsents consents, CancellationToken ct) =>
                await consents.WithdrawAsync(token, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("WithdrawOnlineConsent")
            .WithSummary("Widerruft die Einwilligung; danach gehen keine Wiederbuchungs-Hinweise mehr.");

        return routes;
    }

    /// <summary>Am Empfang erfasst: die Person aus dem Verlauf dazu („Am Empfang erfasst am 9.11.2026 von Lena“); per Mail bestätigt oder aus dem Import ohne Person.</summary>
    private static async Task<MarketingConsentDto> WithRecorderAsync(MarketingConsentDto consent, Guid customerId, EntityHistory history, CancellationToken ct) =>
        consent is { Source: MarketingConsents.SourceReception, State: MarketingConsentState.Confirmed or MarketingConsentState.Refused }
            ? consent with { RecordedBy = await history.ConsentRecorderAsync(customerId, consent.State, ct) }
            : consent;

    /// <summary>Ersetzt die Ausnahmen je Kategorie durch die gesendeten; die Schreibweise der Kategorie ist die der Behandlungen.</summary>
    private static async Task SyncCategoriesAsync(AispaDbContext db, IReadOnlyList<RebookCategoryIntervalDto> wanted, HashSet<string> known, CancellationToken ct)
    {
        var existing = await db.CategoryRebookIntervals.ToListAsync(ct);
        var names = wanted.Select(c => known.TryGetValue(c.Category.Trim(), out var actual) ? actual : c.Category.Trim()).ToList();
        db.CategoryRebookIntervals.RemoveRange(existing.Where(e => !names.Contains(e.Category, StringComparer.OrdinalIgnoreCase)));
        for (var i = 0; i < wanted.Count; i++)
        {
            if (existing.FirstOrDefault(e => string.Equals(e.Category, names[i], StringComparison.OrdinalIgnoreCase)) is { } row)
            {
                row.Days = wanted[i].Days;
            }
            else
            {
                db.CategoryRebookIntervals.Add(new CategoryRebookInterval { Category = names[i], Days = wanted[i].Days });
            }
        }
    }

    /// <summary>
    /// Ersetzt die Ausnahmen je Behandlung: die mit Tagen und die ohne Hinweis. Ein älterer Aufruf ohne Kategorien (<paramref name="keepSilent"/>)
    /// lässt die Behandlungen ohne Hinweis stehen, ausgenommen eine bekommt jetzt Tage.
    /// </summary>
    private static async Task SyncTreatmentsAsync(AispaDbContext db, IReadOnlyList<RebookIntervalDto> intervals, IReadOnlyList<Guid> silent, bool keepSilent, CancellationToken ct)
    {
        var existing = await db.TreatmentRebookIntervals.ToListAsync(ct);
        var wanted = intervals.ToDictionary(i => i.TreatmentId, i => (int?)i.Days);
        foreach (var id in silent)
        {
            wanted[id] = null;
        }

        db.TreatmentRebookIntervals.RemoveRange(existing.Where(e => !wanted.ContainsKey(e.TreatmentId) && !(keepSilent && e.Days is null)));
        foreach (var (treatmentId, days) in wanted)
        {
            if (existing.FirstOrDefault(e => e.TreatmentId == treatmentId) is { } row)
            {
                row.Days = days;
            }
            else
            {
                db.TreatmentRebookIntervals.Add(new TreatmentRebookInterval { TreatmentId = treatmentId, Days = days });
            }
        }
    }

    private static async Task<RebookSettingsDto> LoadAsync(AispaDbContext db, CancellationToken ct)
    {
        var settings = await db.RebookSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        var treatments = await db.TreatmentRebookIntervals.AsNoTracking().OrderBy(i => i.Id).ToListAsync(ct);
        var categories = await db.CategoryRebookIntervals.AsNoTracking().OrderBy(i => i.Category).ToListAsync(ct);
        return new RebookSettingsDto(settings?.Enabled ?? false, [.. treatments.Where(i => i.Days is not null).Select(i => new RebookIntervalDto(i.TreatmentId, i.Days!.Value))])
        {
            DefaultDays = settings?.DefaultDays,
            CategoryIntervals = [.. categories.Select(c => new RebookCategoryIntervalDto(c.Category, c.Days))],
            SilentTreatmentIds = [.. treatments.Where(i => i.Days is null).Select(i => i.TreatmentId)],
        };
    }
}

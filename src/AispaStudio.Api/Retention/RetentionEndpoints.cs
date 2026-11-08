using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
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
                var errors = new Errors();
                var intervals = input.Intervals ?? [];
                errors.Check(intervals.All(i => i.Days is >= MinDays and <= MaxDays), nameof(input.Intervals),
                    $"Zwischen {MinDays} und {MaxDays} Tagen.");
                errors.Check(intervals.Select(i => i.TreatmentId).Distinct().Count() == intervals.Count, nameof(input.Intervals),
                    "Jede Behandlung nur einmal.");
                var ids = intervals.Select(i => i.TreatmentId).ToList();
                errors.Check(await db.Treatments.CountAsync(t => ids.Contains(t.Id), ct) == ids.Distinct().Count(), nameof(input.Intervals),
                    "Unbekannte Behandlung.");
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
                var existing = await db.TreatmentRebookIntervals.ToListAsync(ct);
                db.TreatmentRebookIntervals.RemoveRange(existing.Where(e => !ids.Contains(e.TreatmentId)));
                foreach (var interval in intervals)
                {
                    if (existing.FirstOrDefault(e => e.TreatmentId == interval.TreatmentId) is { } row)
                    {
                        row.Days = interval.Days;
                    }
                    else
                    {
                        db.TreatmentRebookIntervals.Add(new TreatmentRebookInterval { TreatmentId = interval.TreatmentId, Days = interval.Days });
                    }
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await LoadAsync(db, ct));
            })
            .WithName("UpdateRebookSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Wiederbuchungs-Hinweis einstellen. Er geht nur an Gäste mit bestätigter Einwilligung.");

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
                Guid customerId, MarketingConsents consents, CancellationToken ct) =>
                await consents.ForCustomerAsync(customerId, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
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

    private static async Task<RebookSettingsDto> LoadAsync(AispaDbContext db, CancellationToken ct)
    {
        var enabled = await db.RebookSettings.OrderBy(s => s.Id).Select(s => s.Enabled).FirstOrDefaultAsync(ct);
        var intervals = await db.TreatmentRebookIntervals.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new RebookIntervalDto(i.TreatmentId, i.Days)).ToListAsync(ct);
        return new RebookSettingsDto(enabled, intervals);
    }
}

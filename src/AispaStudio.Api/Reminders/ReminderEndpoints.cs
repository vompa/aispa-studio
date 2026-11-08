using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reminders;

internal static class ReminderSetup
{
    /// <summary>Terminerinnerungen: Planung im Hintergrund (<c>Reminders:PollSeconds</c>), Versand über die Queue.</summary>
    public static IServiceCollection AddReminders(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<ReminderOptions>(configuration.GetSection(ReminderOptions.Section));
        services.AddScoped<ReminderPlanner>();
        services.AddScoped<ReminderSender>();
        services.AddSingleton<ReminderScheduler>();
        if (modules.IsOn(AppModule.Reminders))
        {
            services.AddBackgroundService<ReminderScheduler>(modules);
        }

        return services;
    }
}

internal static class ReminderEndpoints
{
    /// <summary>Höchstens eine Woche vorher; Erinnerungen danach verpuffen eher als dass sie helfen.</summary>
    private const int MaxHoursBefore = 168;

    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Terminerinnerungen");

        group.MapGet("/reminders/settings", async (AispaDbContext db, CancellationToken ct) =>
                ToDto(await ReminderSettingsStore.LoadAsync(db, ct)))
            .WithName("GetReminderSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Einstellungen der Terminerinnerungen des Spas (ohne Eintrag die Vorgaben: 48 und 2 Stunden vorher, per Mail).");

        group.MapPut("/reminders/settings", async Task<Results<Ok<ReminderSettingsDto>, ValidationProblem>> (
                ReminderSettingsDto input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(input.FirstHoursBefore is >= 0 and <= MaxHoursBefore, nameof(input.FirstHoursBefore),
                    $"Zwischen 0 und {MaxHoursBefore} Stunden.");
                errors.Check(input.SecondHoursBefore is >= 0 and <= MaxHoursBefore, nameof(input.SecondHoursBefore),
                    $"Zwischen 0 und {MaxHoursBefore} Stunden.");
                errors.Check(input.FirstHoursBefore == 0 || input.SecondHoursBefore == 0 || input.FirstHoursBefore > input.SecondHoursBefore,
                    nameof(input.SecondHoursBefore), "Die zweite Erinnerung muss näher am Termin liegen als die erste.");
                errors.Check(input.Channel != ReminderChannel.WhatsApp || !string.IsNullOrWhiteSpace(input.WhatsAppTemplate),
                    nameof(input.WhatsAppTemplate), "Für WhatsApp braucht es eine freigegebene Vorlage.");
                errors.Check(input.WhatsAppTemplate is null || input.WhatsAppTemplate.Trim().Length <= 200, nameof(input.WhatsAppTemplate),
                    "Höchstens 200 Zeichen.");
                errors.Check(!string.IsNullOrWhiteSpace(input.WhatsAppTemplateLanguage) && input.WhatsAppTemplateLanguage.Trim().Length <= 16,
                    nameof(input.WhatsAppTemplateLanguage), "Sprache der Vorlage, z. B. „de“.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var settings = await db.ReminderSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
                if (settings is null)
                {
                    settings = new ReminderSettings();
                    db.ReminderSettings.Add(settings);
                }

                settings.Enabled = input.Enabled;
                settings.FirstHoursBefore = input.FirstHoursBefore;
                settings.SecondHoursBefore = input.SecondHoursBefore;
                settings.Channel = input.Channel;
                settings.WhatsAppTemplate = string.IsNullOrWhiteSpace(input.WhatsAppTemplate) ? null : input.WhatsAppTemplate.Trim();
                settings.WhatsAppTemplateLanguage = input.WhatsAppTemplateLanguage.Trim();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(settings));
            })
            .WithName("UpdateReminderSettings")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Terminerinnerungen einstellen. Gilt für Termine, deren Erinnerung noch nicht fällig war.");

        group.MapGet("/appointments/{appointmentId:guid}/reminders", async Task<Results<Ok<List<AppointmentReminderDto>>, NotFound>> (
                Guid appointmentId, AispaDbContext db, AccessService access, CancellationToken ct) =>
            {
                var locationId = await db.Appointments.Where(a => a.Id == appointmentId).Select(a => (Guid?)a.LocationId).FirstOrDefaultAsync(ct);
                if (locationId is null || (await access.CurrentAsync(ct))?.CanSee(locationId.Value) != true)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(await db.AppointmentReminders.AsNoTracking().Where(r => r.AppointmentId == appointmentId)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new AppointmentReminderDto(r.Stage, r.Channel, r.State.ToString(), r.CreatedAt, r.SentAt, r.Error))
                    .ToListAsync(ct));
            })
            .WithName("ListAppointmentReminders")
            .RequirePermission(AccessRight.ViewCalendar)
            .WithSummary("Erinnerungen eines Termins: wann, über welchen Kanal, mit welchem Ergebnis.");

        return routes;
    }

    private static ReminderSettingsDto ToDto(ReminderSettings s) =>
        new(s.Enabled, s.FirstHoursBefore, s.SecondHoursBefore, s.Channel, s.WhatsAppTemplate, s.WhatsAppTemplateLanguage);
}

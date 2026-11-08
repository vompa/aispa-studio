using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.Reminders;

/// <summary>Abschnitt <c>Reminders</c>: wie oft nach fälligen Erinnerungen gesucht wird (0 = gar nicht, z. B. in Tests).</summary>
public sealed class ReminderOptions
{
    public const string Section = "Reminders";

    public int PollSeconds { get; set; } = 300;
}

/// <summary>Queue: diese Erinnerung versenden.</summary>
public sealed record SendReminder(Guid ReminderId);

public static class SendReminderHandler
{
    public static async Task Handle(SendReminder message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.AppointmentReminders.Where(r => r.Id == message.ReminderId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<ReminderSender>().SendAsync(message.ReminderId, ct);
    }
}

/// <summary>Sucht regelmäßig fällige Erinnerungen in allen Spas (<c>Reminders:PollSeconds</c>).</summary>
internal sealed partial class ReminderScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<ReminderOptions> options,
    StartupState startup,
    ILogger<ReminderScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
    }

    /// <summary>Ein Durchlauf über alle Spas; liefert die Zahl der neu angestoßenen Erinnerungen.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Nur Spas, deren Abo-Stufe Erinnerungen enthält (ADR 0015).
            var plans = Plans.With(AppModule.Reminders);
            tenants = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
                .Where(t => plans.Contains(t.Plan)).Select(t => t.Id).ToListAsync(ct);
        }

        var queued = 0;
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            queued += await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().PlanAsync(now, ct);
        }

        return queued;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Terminerinnerungen konnten nicht geplant werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>Legt für fällige Termine des aktuellen Spas genau eine Erinnerung je Stufe an und stellt sie in die Queue.</summary>
internal sealed class ReminderPlanner(AispaDbContext db, IMessageBus bus, IOptions<TextChannelOptions> textChannels)
{
    private const string UniqueViolation = "23505";

    public async Task<int> PlanAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = await ReminderSettingsStore.LoadAsync(db, ct);
        var stages = Stages(settings);
        if (!settings.Enabled || stages.Count == 0)
        {
            return 0;
        }

        var horizon = now.AddHours(stages.Max(s => s.Hours));
        var due = await db.Appointments.AsNoTracking().Include(a => a.Customer)
            .Where(a => a.Status == AppointmentStatus.Booked && a.HeldSince == null && a.Start > now && a.Start <= horizon)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            return 0;
        }

        var ids = due.Select(a => a.Id).ToList();
        var done = (await db.AppointmentReminders.AsNoTracking().Where(r => ids.Contains(r.AppointmentId)).ToListAsync(ct))
            .Select(r => (r.AppointmentId, r.Stage, r.AppointmentStart)).ToHashSet();

        var queued = 0;
        foreach (var appointment in due)
        {
            // Nur die nächstliegende fällige Stufe: wer erst 30 Stunden vorher bucht, bekommt keine „48-Stunden“-Erinnerung,
            // und nach einem Ausfall kommt nicht die erste und zweite zugleich.
            var stage = stages
                .Where(s => appointment.Start.AddHours(-s.Hours) <= now && appointment.CreatedAt <= appointment.Start.AddHours(-s.Hours))
                .OrderBy(s => s.Hours)
                .FirstOrDefault();
            if (stage is null || stages.Any(s => s.Hours < stage.Hours && done.Contains((appointment.Id, s.Number, appointment.Start)))
                || done.Contains((appointment.Id, stage.Number, appointment.Start)))
            {
                continue;
            }

            var channel = ChannelFor(settings, appointment.Customer);
            var reminder = new AppointmentReminder
            {
                AppointmentId = appointment.Id,
                Stage = stage.Number,
                AppointmentStart = appointment.Start,
                Channel = channel ?? settings.Channel,
                State = channel is null ? ReminderState.Skipped : ReminderState.Queued,
                Error = channel is null ? "Keine Mailadresse und keine Telefonnummer." : null,
                CreatedAt = now,
            };
            db.Add(reminder);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
            {
                // Ein zweiter Durchlauf war schneller.
                db.ChangeTracker.Clear();
                continue;
            }

            if (reminder.State == ReminderState.Queued)
            {
                await bus.PublishAsync(new SendReminder(reminder.Id));
                queued++;
            }
        }

        return queued;
    }

    private sealed record ReminderStage(int Number, int Hours);

    private static List<ReminderStage> Stages(ReminderSettings settings) =>
        [.. new[] { new ReminderStage(1, settings.FirstHoursBefore), new ReminderStage(2, settings.SecondHoursBefore) }.Where(s => s.Hours > 0)];

    /// <summary>Bevorzugter Kanal, sonst der, für den Kontaktdaten und Anbieter da sind. Null, wenn es keinen gibt.</summary>
    private ReminderChannel? ChannelFor(ReminderSettings settings, Customer customer)
    {
        var phone = !string.IsNullOrEmpty(PhoneNumbers.Normalize(customer.Phone ?? ""));
        var email = !string.IsNullOrWhiteSpace(customer.Email);
        var sms = phone && !string.IsNullOrWhiteSpace(textChannels.Value.SmsProvider);
        var whatsApp = phone && !string.IsNullOrWhiteSpace(textChannels.Value.WhatsAppProvider) && !string.IsNullOrWhiteSpace(settings.WhatsAppTemplate);
        return settings.Channel switch
        {
            ReminderChannel.WhatsApp when whatsApp => ReminderChannel.WhatsApp,
            ReminderChannel.WhatsApp or ReminderChannel.Sms when sms => ReminderChannel.Sms,
            _ when email => ReminderChannel.Email,
            _ when sms => ReminderChannel.Sms,
            _ => null,
        };
    }
}

/// <summary>Versendet eine Erinnerung mit Link zum Termin (ansehen, absagen) und hält das Ergebnis fest.</summary>
internal sealed class ReminderSender(
    AispaDbContext db,
    OnlineDbContext online,
    AccessLinks links,
    OnlinePortal portal,
    SpaMailSender mails,
    ITextMessageSender texts,
    TimeProvider time)
{
    public async Task SendAsync(Guid reminderId, CancellationToken ct)
    {
        var reminder = await db.AppointmentReminders.FirstOrDefaultAsync(r => r.Id == reminderId, ct);
        if (reminder is null || reminder.State == ReminderState.Sent)
        {
            return;
        }

        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .FirstOrDefaultAsync(a => a.Id == reminder.AppointmentId, ct);
        if (appointment is null || appointment.Status != AppointmentStatus.Booked || appointment.Start != reminder.AppointmentStart)
        {
            // Inzwischen storniert oder verschoben (für den neuen Beginn gibt es eine eigene Erinnerung).
            reminder.State = ReminderState.Skipped;
            reminder.Error = "Termin storniert oder verschoben.";
            await db.SaveChangesAsync(ct);
            return;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var settings = await ReminderSettingsStore.LoadAsync(db, ct);
        // Die Sprache der Online-Buchung, sonst die des Gastes aus den Kundendaten.
        var language = await online.OnlineBookings.AsNoTracking().Where(b => b.AppointmentId == appointment.Id).Select(b => b.Language).FirstOrDefaultAsync(ct) is { } booked
            ? GuestLanguage.Normalize(booked)
            : GuestLanguage.Resolve(appointment.Customer);
        var token = links.Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointment.Id, appointment.End.AddDays(30));
        await online.SaveChangesAsync(ct);
        var text = new ReminderTexts.Facts(
            GuestName.Of(appointment.Customer, location.TimeZone),
            appointment.Treatment.Name,
            ReminderTexts.When(appointment.Start, LocalTime.Zone(location.TimeZone), language),
            location.Name,
            portal.Url($"termin/{token}"),
            language,
            appointment.Treatment.GuestHint);

        try
        {
            switch (reminder.Channel)
            {
                case ReminderChannel.Email:
                    await mails.SendAsync(new OutgoingMail(appointment.Customer.Email!, ReminderTexts.Subject(text), ReminderTexts.Mail(text)), ct);
                    break;
                case ReminderChannel.WhatsApp:
                    var whatsApp = await IntakeRoutes.OwnAsync(db, BookingChannel.WhatsApp, ct);
                    await texts.SendAsync(new OutgoingText(BookingChannel.WhatsApp, PhoneNumbers.Normalize(appointment.Customer.Phone!), ReminderTexts.Short(text),
                        new TextTemplate(settings.WhatsAppTemplate!, settings.WhatsAppTemplateLanguage,
                            [text.Guest.FirstName, text.Treatment, text.When, text.Location, text.Link]),
                        whatsApp?.Address, whatsApp?.SenderId), ct);
                    break;
                default:
                    // Von der eigenen Nummer des Spas, damit eine Antwort des Gastes wieder beim richtigen Spa landet.
                    var sms = await IntakeRoutes.OwnAsync(db, BookingChannel.Sms, ct);
                    await texts.SendAsync(new OutgoingText(BookingChannel.Sms, PhoneNumbers.Normalize(appointment.Customer.Phone!), ReminderTexts.Short(text),
                        From: sms?.Address, SenderId: sms?.SenderId), ct);
                    break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or MailKit.Net.Smtp.SmtpCommandException
            or MailKit.Net.Smtp.SmtpProtocolException or IOException)
        {
            reminder.State = ReminderState.Failed;
            reminder.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        reminder.State = ReminderState.Sent;
        reminder.Error = null;
        reminder.SentAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
}

internal static class ReminderSettingsStore
{
    public static async Task<ReminderSettings> LoadAsync(AispaDbContext db, CancellationToken ct) =>
        await db.ReminderSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new ReminderSettings();
}

/// <summary>Feste Texte der Erinnerung (keine KI), in den Sprachen des Kundenbereichs (<see cref="GuestLanguage"/>).</summary>
internal static class ReminderTexts
{
    /// <summary><c>Hint</c>: Hinweis der Behandlung an Gäste (nur in der Mail, SMS bleiben kurz).</summary>
    public sealed record Facts(GuestName Guest, string Treatment, string When, string Location, string Link, string Language, string? Hint = null);

    public static string When(DateTimeOffset start, TimeZoneInfo zone, string language)
    {
        var local = TimeZoneInfo.ConvertTime(start, zone);
        var pattern = language switch
        {
            "en" => "dddd, d MMMM 'at' HH:mm",
            "fr" => "dddd d MMMM 'à' HH:mm",
            "it" => "dddd d MMMM 'alle' HH:mm",
            _ => "dddd, d. MMMM 'um' HH:mm 'Uhr'",
        };
        return local.ToString(pattern, OnlineLanguage.Culture(language));
    }

    public static string Subject(Facts f) => f.Language switch
    {
        "en" => $"Reminder: {f.Treatment} on {f.When}",
        "fr" => $"Rappel : {f.Treatment} le {f.When}",
        "it" => $"Promemoria: {f.Treatment}, {f.When}",
        _ => $"Erinnerung: {f.Treatment} am {f.When}",
    };

    public static string Mail(Facts f) => f.Language switch
    {
        "en" => $"""
           Hello {f.Guest.FirstName},

           this is a reminder of your appointment:

           {f.Treatment}
           {f.When}
           {f.Location}

           {HintLine(f)}Please confirm that you are coming, or cancel: {f.Link}

           We look forward to seeing you.
           {f.Location}
           """,
        "fr" => $"""
           Bonjour {f.Guest.FirstName},

           Nous vous rappelons votre rendez-vous :

           {f.Treatment}
           {f.When}
           {f.Location}

           {HintLine(f)}Merci de confirmer votre venue ou d’annuler : {f.Link}

           Nous nous réjouissons de vous accueillir.
           {f.Location}
           """,
        "it" => $"""
           Buongiorno {f.Guest.FirstName},

           le ricordiamo il suo appuntamento:

           {f.Treatment}
           {f.When}
           {f.Location}

           {HintLine(f)}La preghiamo di confermare la sua presenza o di annullare: {f.Link}

           Saremo lieti di accoglierla.
           {f.Location}
           """,
        _ => $"""
           Guten Tag {f.Guest.GreetingName},

           wir erinnern Sie an Ihren Termin:

           {f.Treatment}
           {f.When}
           {f.Location}

           {HintLine(f)}Bitte bestätigen Sie kurz, dass Sie kommen, oder sagen Sie ab: {f.Link}

           Wir freuen uns auf Sie.
           {f.Location}
           """,
    };

    public static string Short(Facts f) => f.Language switch
    {
        "en" => $"Reminder: {f.Treatment} on {f.When} at {f.Location}. Confirm or cancel: {f.Link}",
        "fr" => $"Rappel : {f.Treatment} le {f.When} ({f.Location}). Confirmer ou annuler : {f.Link}",
        "it" => $"Promemoria: {f.Treatment}, {f.When}, presso {f.Location}. Confermare o annullare: {f.Link}",
        _ => $"Erinnerung: {f.Treatment} am {f.When} in {f.Location}. Bestätigen oder absagen: {f.Link}",
    };

    private static string HintLine(Facts f) => f.Hint is { } hint ? $"{hint}\n\n" : "";
}

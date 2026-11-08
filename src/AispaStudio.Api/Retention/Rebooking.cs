using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.Retention;

/// <summary>Abschnitt <c>Retention</c>: wie oft nach fälligen Erinnerungen an die nächste Behandlung gesucht wird (0 = gar nicht).</summary>
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    public int PollSeconds { get; set; } = 3600;
}

/// <summary>Queue: diese Erinnerung an die nächste Behandlung schreiben und versenden.</summary>
public sealed record SendRebookReminder(Guid ReminderId);

public static class SendRebookReminderHandler
{
    public static async Task Handle(SendRebookReminder message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.RebookReminders.Where(r => r.Id == message.ReminderId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<RebookSender>().SendAsync(message.ReminderId, ct);
    }
}

/// <summary>Sucht regelmäßig in allen Spas, wer an die nächste Behandlung erinnert werden möchte (<c>Retention:PollSeconds</c>).</summary>
internal sealed partial class RebookScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<RetentionOptions> options,
    StartupState startup,
    ILogger<RebookScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(60, options.Value.PollSeconds)));
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

    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var plans = Plans.With(AppModule.Rebooking);
            tenants = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
                .Where(t => t.LockedAt == null && plans.Contains(t.Plan)).Select(t => t.Id).ToListAsync(ct);
        }

        var queued = 0;
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            queued += await scope.ServiceProvider.GetRequiredService<RebookPlanner>().PlanAsync(now, ct);
        }

        return queued;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erinnerungen an die nächste Behandlung konnten nicht geplant werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>
/// Findet im aktuellen Spa Gäste mit bestätigter Einwilligung, deren letzte abgeschlossene Behandlung so lange her ist wie
/// eingestellt. Keine Erinnerung, wenn schon ein Termin gebucht ist, nach diesem Termin schon erinnert wurde oder der Gast in
/// den letzten 30 Tagen eine bekommen hat; höchstens eine je Gast und Lauf.
/// </summary>
internal sealed class RebookPlanner(AispaDbContext db, IMessageBus bus)
{
    /// <summary>Wer so lange nach dem Fälligwerden nicht erinnert wurde (z. B. weil ausgeschaltet), bekommt keine verspätete Erinnerung mehr.</summary>
    public const int WindowDays = 14;

    public const int QuietDays = 30;

    private const string UniqueViolation = "23505";

    public async Task<int> PlanAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = await db.RebookSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        var intervals = await db.TreatmentRebookIntervals.AsNoTracking().ToDictionaryAsync(i => i.TreatmentId, i => i.Days, ct);
        if (settings is not { Enabled: true } || intervals.Count == 0)
        {
            return 0;
        }

        var consents = await db.MarketingConsents.AsNoTracking().Where(c => c.State == MarketingConsentState.Confirmed)
            .Select(c => new { c.CustomerId, c.Email }).ToListAsync(ct);
        if (consents.Count == 0)
        {
            return 0;
        }

        var customerIds = consents.Select(c => c.CustomerId).ToList();
        var treatmentIds = intervals.Keys.ToList();
        var since = now.AddDays(-(intervals.Values.Max() + WindowDays));
        var completed = await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Completed && a.End > since && a.End <= now
                && customerIds.Contains(a.CustomerId) && treatmentIds.Contains(a.TreatmentId))
            .Select(a => new { a.Id, a.CustomerId, a.TreatmentId, a.End })
            .ToListAsync(ct);
        var due = completed
            .GroupBy(a => (a.CustomerId, a.TreatmentId))
            .Select(g => g.MaxBy(a => a.End)!)
            .Select(a => (Appointment: a, DueAt: a.End.AddDays(intervals[a.TreatmentId])))
            .Where(d => d.DueAt <= now && now < d.DueAt.AddDays(WindowDays))
            .ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        var dueCustomers = due.Select(d => d.Appointment.CustomerId).Distinct().ToList();
        var booked = (await db.Appointments.AsNoTracking()
            .Where(a => dueCustomers.Contains(a.CustomerId) && a.Status == AppointmentStatus.Booked && a.Start > now)
            .Select(a => a.CustomerId).Distinct().ToListAsync(ct)).ToHashSet();
        var quietFrom = now.AddDays(-QuietDays);
        var recent = (await db.RebookReminders.AsNoTracking()
            .Where(r => dueCustomers.Contains(r.CustomerId) && r.CreatedAt > quietFrom)
            .Select(r => r.CustomerId).Distinct().ToListAsync(ct)).ToHashSet();
        var afterIds = due.Select(d => d.Appointment.Id).ToList();
        var done = (await db.RebookReminders.AsNoTracking().Where(r => afterIds.Contains(r.AfterAppointmentId))
            .Select(r => r.AfterAppointmentId).ToListAsync(ct)).ToHashSet();
        var emails = (await db.Customers.AsNoTracking().Where(c => dueCustomers.Contains(c.Id))
            .Select(c => new { c.Id, c.Email }).ToListAsync(ct)).ToDictionary(c => c.Id, c => c.Email);
        var consentEmail = consents.ToDictionary(c => c.CustomerId, c => c.Email);

        var queued = 0;
        foreach (var (appointment, _) in due.OrderBy(d => d.DueAt).DistinctBy(d => d.Appointment.CustomerId))
        {
            var customerId = appointment.CustomerId;
            if (booked.Contains(customerId) || recent.Contains(customerId) || done.Contains(appointment.Id)
                || !string.Equals(emails.GetValueOrDefault(customerId)?.Trim(), consentEmail[customerId], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reminder = new RebookReminder
            {
                CustomerId = customerId,
                TreatmentId = appointment.TreatmentId,
                AfterAppointmentId = appointment.Id,
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

            await bus.PublishAsync(new SendRebookReminder(reminder.Id));
            queued++;
        }

        return queued;
    }
}

/// <summary>Angaben für den persönlichen Teil; keine Zahlen, Preise oder Termine, damit die KI nichts davon erfinden kann.</summary>
public sealed record RebookFacts(GuestName Guest, string Treatment, string Spa, string Language);

/// <summary>Schreibt zwei, drei persönliche Sätze für die Erinnerung. Null, wenn nichts Brauchbares kommt; dann gilt der feste Text.</summary>
public interface IRebookWriter
{
    Task<string?> WriteAsync(RebookFacts facts, CancellationToken ct);
}

internal sealed class NoRebookWriter : IRebookWriter
{
    public Task<string?> WriteAsync(RebookFacts facts, CancellationToken ct) => Task.FromResult<string?>(null);
}

internal sealed class ClaudeRebookWriter(ClaudeJson claude) : IRebookWriter
{
    private const string System = """
        Du schreibst für ein Day-Spa den persönlichen Teil einer Mail an einen Stammgast: zwei bis drei kurze Sätze, dass es Zeit
        für die nächste Behandlung sein könnte, freundlich und unaufdringlich. Schreibe in der Sprache mit dem angegebenen Code
        (de, en, fr, it) und in der höflichen Anrede (Sie, vous, Lei). Keine Begrüßung, keine Grußformel, kein Link: die stehen
        schon in der Mail. Verwende nur die Angaben aus der Anfrage. Nenne keine Preise, Rabatte, Angebote, Daten, Uhrzeiten,
        Zahlen und keine Heilversprechen. Antworte nur im JSON-Format.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = ClaudeJson.Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "text" },
        properties = new { text = new { type = "string" } },
    });

    public async Task<string?> WriteAsync(RebookFacts facts, CancellationToken ct)
    {
        var prompt = $"""
            Sprache: {facts.Language}
            Name des Gastes: {facts.Guest.For(facts.Language)}
            Letzte Behandlung: {facts.Treatment}
            Spa: {facts.Spa}
            """;
        return (await claude.AskAsync<TextJson>(System, prompt, Schema, ct)).Text;
    }

    private sealed record TextJson(string Text);
}

/// <summary>
/// Versendet eine Erinnerung an die nächste Behandlung: persönlicher Teil von der KI (oder fester Text), Link zum Buchen,
/// Hinweis auf die KI und Abmeldelink. Prüft vorher noch einmal Einwilligung und gebuchte Termine.
/// </summary>
internal sealed partial class RebookSender(
    AispaDbContext db,
    OnlineDbContext online,
    MarketingConsents consents,
    OnlinePortal portal,
    IRebookWriter writer,
    SpaMailSender mails,
    TenantContext tenants,
    TimeProvider time,
    ILogger<RebookSender> log)
{
    private const int MaxText = 600;

    public async Task SendAsync(Guid reminderId, CancellationToken ct)
    {
        var reminder = await db.RebookReminders.FirstOrDefaultAsync(r => r.Id == reminderId, ct);
        if (reminder is null || reminder.State is ReminderState.Sent or ReminderState.Skipped)
        {
            return;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == reminder.CustomerId, ct);
        var consent = await db.MarketingConsents.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == reminder.CustomerId, ct);
        var now = time.GetUtcNow();
        string? skip = null;
        if (consent is not { State: MarketingConsentState.Confirmed }
            || !string.Equals(customer.Email?.Trim(), consent.Email, StringComparison.OrdinalIgnoreCase))
        {
            skip = "Keine gültige Einwilligung mehr.";
        }
        else if (await db.Appointments.AnyAsync(a => a.CustomerId == customer.Id && a.Status == AppointmentStatus.Booked && a.Start > now, ct))
        {
            skip = "Inzwischen ist ein Termin gebucht.";
        }

        if (skip is not null)
        {
            reminder.State = ReminderState.Skipped;
            reminder.Error = skip;
            await db.SaveChangesAsync(ct);
            return;
        }

        var last = await db.Appointments.AsNoTracking().Include(a => a.Treatment).FirstAsync(a => a.Id == reminder.AfterAppointmentId, ct);
        var tenantId = tenants.TenantId!.Value;
        var spa = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstAsync(ct);
        var language = consent!.Language;
        var zone = await db.Locations.Where(l => l.Id == last.LocationId).Select(l => l.TimeZone).FirstAsync(ct);
        var facts = new RebookFacts(GuestName.Of(customer, zone), last.Treatment.Name, spa, language);

        var text = await AiTextAsync(facts, ct);
        reminder.AiWritten = text is not null;
        reminder.Text = text ?? RebookTexts.Fallback(facts);

        var book = portal.Url($"?standort={last.LocationId}&behandlung={last.TreatmentId}", tenantId);
        var unsubscribe = consents.UnsubscribeUrl(customer.Id);
        await online.SaveChangesAsync(ct);
        try
        {
            await mails.SendAsync(RebookTexts.Mail(consent.Email, facts, reminder.Text, reminder.AiWritten, book, unsubscribe), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sichtbar machen; die Queue wiederholt den Versand.
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

    /// <summary>Text der KI, wenn er die Regeln einhält (keine Ziffern, Links, Preise; nicht zu lang). Fehler der KI führen zum festen Text.</summary>
    private async Task<string?> AiTextAsync(RebookFacts facts, CancellationToken ct)
    {
        string? text;
        try
        {
            text = (await writer.WriteAsync(facts, ct))?.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AiFailed(log, ex);
            return null;
        }

        return Acceptable(text, facts) ? text : null;
    }

    /// <summary>Ziffern im Namen des Spas oder der Behandlung (z. B. „Spa 1080“) zählen nicht; jede andere Zahl hat die KI erfunden.</summary>
    public static bool Acceptable(string? text, RebookFacts facts)
    {
        if (text is not { Length: >= 20 and <= MaxText })
        {
            return false;
        }

        var rest = text.Replace(facts.Spa, "", StringComparison.Ordinal).Replace(facts.Treatment, "", StringComparison.Ordinal);
        return !rest.Any(char.IsDigit)
            && !rest.Contains("http", StringComparison.OrdinalIgnoreCase)
            && rest.IndexOfAny(['@', '%', '€', '$', '£']) < 0
            && !rest.Contains("CHF", StringComparison.Ordinal)
            && !rest.Contains("EUR", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Die KI konnte keine Erinnerung schreiben; es gilt der feste Text.")]
    private static partial void AiFailed(ILogger logger, Exception ex);
}

/// <summary>Rahmen der Mail und fester Text ohne KI, in den Sprachen des Kundenbereichs.</summary>
internal static class RebookTexts
{
    public const string AiNoticeDe = "Dieser Text wurde mit KI verfasst.";

    public static string Fallback(RebookFacts f) => f.Language switch
    {
        "en" => $"It has been a while since your last visit for “{f.Treatment}”. Whenever you feel like treating yourself again, you can book your next appointment online.",
        "fr" => $"Votre dernière visite pour « {f.Treatment} » remonte à quelque temps. Si vous souhaitez vous faire à nouveau du bien, réservez simplement votre prochain rendez-vous en ligne.",
        "it" => $"È passato un po’ di tempo dalla sua ultima visita per «{f.Treatment}». Se desidera concedersi di nuovo un momento di benessere, può prenotare il prossimo appuntamento online.",
        _ => $"Ihr letzter Besuch bei uns für „{f.Treatment}“ ist schon eine Weile her. Wenn Sie sich wieder etwas Gutes tun möchten, buchen Sie Ihren nächsten Termin einfach online.",
    };

    public static OutgoingMail Mail(string to, RebookFacts f, string text, bool aiWritten, string book, string unsubscribe)
    {
        var (subject, greeting, bookLabel, aiNotice, why, unsubscribeLabel) = f.Language switch
        {
            "en" => ($"{f.Spa}: time for your next appointment?", "Hello", "Book an appointment", "This text was written with the help of AI.",
                "You receive this email because you agreed to reminders about your next treatment.", "Unsubscribe"),
            "fr" => ($"{f.Spa} : envie d’un nouveau rendez-vous ?", "Bonjour", "Réserver un rendez-vous", "Ce texte a été rédigé avec l’aide de l’IA.",
                "Vous recevez ce message parce que vous avez accepté de recevoir des rappels pour votre prochain soin.", "Se désabonner"),
            "it" => ($"{f.Spa}: è ora del prossimo appuntamento?", "Buongiorno", "Prenotare un appuntamento", "Questo testo è stato scritto con l’aiuto dell’IA.",
                "Riceve questa e-mail perché ha acconsentito ai promemoria per il prossimo trattamento.", "Annullare l’iscrizione"),
            _ => ($"{f.Spa}: Zeit für Ihren nächsten Termin?", "Guten Tag", "Termin buchen", AiNoticeDe,
                "Sie bekommen diese Mail, weil Sie in Erinnerungen an Ihre nächste Behandlung eingewilligt haben.", "Abmelden"),
        };

        return new OutgoingMail(to, subject, $"""
            {greeting} {f.Guest.For(f.Language)},

            {text}

            {bookLabel}: {book}

            {f.Spa}

            --
            {(aiWritten ? aiNotice + "\n" : "")}{why}
            {unsubscribeLabel}: {unsubscribe}
            """);
    }
}

using AispaStudio.Api.Data;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>Queue: eine Mail ist gespeichert und soll verarbeitet werden.</summary>
public sealed record InboundMailReceived(Guid MailId);

/// <summary>Queue: Antwortentwurf neu schreiben (z. B. nachdem die Rezeption einen Vorschlag gebucht hat).</summary>
public sealed record ReplyDraftRequested(Guid MailId);

/// <summary>Queue: Antwort ist freigegeben und soll gesendet werden.</summary>
public sealed record ReplySendRequested(Guid MailId);

/// <summary>Queue: Nachricht und Antwortentwurf ins Deutsche übersetzen (für die Rezeption).</summary>
public sealed record MailTranslationRequested(Guid MailId);

/// <summary>Queue: Antwortentwurf von der KI überarbeiten lassen.</summary>
public sealed record ReplyRevisionRequested(Guid MailId, ReplyRevisionKind Kind, string? Instruction);

/// <summary>Queue: „Neu vorschlagen“ mit optionalem Hinweis der Rezeption.</summary>
public sealed record ReproposeRequested(Guid MailId, string? Hint);

/// <summary>
/// Wolverine-Handler der Terminannahme. Sie laufen in der eigenen Queue „mail-intake“ nacheinander, damit zwei Mails
/// (z. B. Treatwell-Buchung und -Änderung) sich nicht überholen und die KI nicht mit Spitzen belastet wird.
/// </summary>
public static class MailIntakeHandler
{
    public static Task Handle(InboundMailReceived message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.ProcessAsync(message.MailId, ct), ct);

    public static Task Handle(ReplyDraftRequested message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.RedraftAsync(message.MailId, ct), ct);

    public static Task Handle(ReplySendRequested message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.SendAsync(message.MailId, ct), ct);

    public static Task Handle(MailTranslationRequested message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.TranslateAsync(message.MailId, ct), ct);

    public static Task Handle(ReplyRevisionRequested message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.ReviseAsync(message.MailId, message.Kind, message.Instruction, ct), ct);

    public static Task Handle(ReproposeRequested message, IServiceScopeFactory scopes, CancellationToken ct) =>
        RunAsync(scopes, message.MailId, p => p.ReproposeAsync(message.MailId, message.Hint, ct), ct);

    /// <summary>Rückweg aus der Fehlerablage (<see cref="MailIntakeFailurePolicy"/>): Die Antwort wird wieder Entwurf mit Hinweis.</summary>
    internal static Task ReturnUnsentReplyAsync(IServiceScopeFactory scopes, Guid mailId, CancellationToken ct) =>
        RunAsync(scopes, mailId, p => p.ReturnUnsentReplyAsync(mailId, ct), ct);

    // Eigener Scope: der Prozessor und der BookingService sind interne Dienste, die Wolverine nicht selbst erzeugt.
    // Ohne HTTP-Anfrage kennt der Scope keinen Mandanten; es gilt der Mandant der Mail.
    private static async Task RunAsync(IServiceScopeFactory scopes, Guid mailId, Func<MailIntakeProcessor, Task> work, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.InboundMails.Where(m => m.Id == mailId), ct) is { } tenantId)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        }

        await work(scope.ServiceProvider.GetRequiredService<MailIntakeProcessor>());
    }
}

/// <summary>Speichert eingehende Mails genau einmal und stellt sie in die Queue. Gemeinsamer Weg für Postfach und Test-Postfach.</summary>
internal sealed class MailIngestion(AispaDbContext db, IMessageBus bus, TenantContext tenants, IOptions<MailIntakeOptions> options)
{
    private const string UniqueViolation = "23505";

    private static readonly SubscriptionPlan[] IntakePlans = Plans.With(AppModule.BookingIntake);

    public async Task<Guid> IngestAsync(IncomingMail incoming, CancellationToken ct)
    {
        if (tenants.TenantId is null)
        {
            // Ohne ?spa= im Webhook und ohne eigenes Postfach: das Spa, dem die angeschriebene Nummer oder Adresse gehört,
            // sonst das konfigurierte (oder das einzige).
            if (await IntakeRoutes.FindAsync(db, incoming.Channel, incoming.To, ct) is { } route)
            {
                tenants.Use(route.TenantId);
            }
            else
            {
                var single = await db.Tenants.Select(t => t.Id).Take(2).ToListAsync(ct);
                tenants.Use(options.Value.TenantId ?? (single.Count == 1 ? single[0]
                    : throw new InvalidOperationException("MailIntake:TenantId fehlt: welchem Spa gehört das Postfach?")));
            }
        }

        var existing = await db.InboundMails.Where(m => m.MessageId == incoming.MessageId).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        if (existing is { } id)
        {
            return id;
        }

        var mail = new InboundMail
        {
            MessageId = Cut(incoming.MessageId, 500),
            From = Cut(incoming.From.Trim(), 320),
            FromName = incoming.FromName is null ? null : Cut(incoming.FromName.Trim(), 200),
            To = Cut(incoming.To, 320),
            Subject = Cut(incoming.Subject, 1000),
            Body = incoming.Body,
            ReceivedAt = incoming.ReceivedAt,
            SenderVerified = incoming.SenderVerified,
            Channel = incoming.Channel,
        };
        db.InboundMails.Add(mail);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Gleichzeitig schon von einem anderen Abruf gespeichert.
            db.ChangeTracker.Clear();
            return await db.InboundMails.Where(m => m.MessageId == incoming.MessageId).Select(m => m.Id).FirstAsync(ct);
        }

        // Ohne Terminannahme in der Abo-Stufe (ADR 0015) bleibt die Nachricht gespeichert, aber die KI bearbeitet sie nicht.
        if (await db.Tenants.AnyAsync(t => t.Id == tenants.TenantId && IntakePlans.Contains(t.Plan), ct))
        {
            await bus.PublishAsync(new InboundMailReceived(mail.Id));
        }

        return mail.Id;
    }

    private static string Cut(string value, int length) => value.Length <= length ? value : value[..length];
}

/// <summary>
/// Fragt regelmäßig das konfigurierte Postfach und die eigenen Postfächer der Spas ab (<c>MailIntake:PollSeconds</c>). Fehler
/// werden protokolliert bzw. beim Spa-Postfach vermerkt, der nächste Abruf versucht es wieder.
/// </summary>
internal sealed partial class MailboxPoller(
    IServiceScopeFactory scopes,
    IMailbox mailbox,
    SpaMailboxRegistry spaMailboxes,
    ISpaMailboxFactory factory,
    SpaMailboxSecrets secrets,
    TimeProvider time,
    IOptions<MailIntakeOptions> options,
    StartupState startup,
    ModuleSwitches modules,
    ILogger<MailboxPoller> log) : BackgroundService
{
    // Abruf im Takt und „Jetzt prüfen“ nicht gleichzeitig: eine offene IMAP-Verbindung verträgt nur einen Aufrufer.
    private readonly SemaphoreSlim _spaLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPollFailed(log, ex);
            }

            try
            {
                await PollSpaMailboxesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPollFailed(log, ex);
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken ct)
    {
        var mails = await mailbox.FetchNewAsync(ct);
        if (mails.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<MailIngestion>();
        foreach (var mail in mails)
        {
            await ingestion.IngestAsync(mail, ct);
            await mailbox.MarkProcessedAsync(mail.MessageId, ct);
        }
    }

    /// <summary>Ein Durchlauf über die eingeschalteten Postfächer aller Spas; jedes nacheinander, ein Fehler hält die anderen nicht auf.</summary>
    public async Task PollSpaMailboxesAsync(CancellationToken ct)
    {
        await _spaLock.WaitAsync(ct);
        try
        {
            var boxes = await LoadSpaMailboxesAsync(null, ct);
            spaMailboxes.Retain([.. boxes.Select(b => b.Box.Id)]);
            foreach (var (box, tenantId) in boxes)
            {
                await PollSpaMailboxAsync(box, tenantId, verifySender: false, ct);
            }
        }
        finally
        {
            _spaLock.Release();
        }
    }

    /// <summary>„Jetzt prüfen“ eines Spas: Postfach sofort abrufen und, wenn es selbst versendet, die Anmeldung zum Versand prüfen.</summary>
    public async Task CheckSpaMailboxAsync(Guid tenantId, CancellationToken ct)
    {
        await _spaLock.WaitAsync(ct);
        try
        {
            foreach (var (box, owner) in await LoadSpaMailboxesAsync(tenantId, ct))
            {
                await PollSpaMailboxAsync(box, owner, verifySender: true, ct);
            }
        }
        finally
        {
            // Ohne Takt (Hosting:Role=Api) räumt Retain nie auf; die Verbindung hielte die Zugangsdaten sonst bis zum Neustart.
            // Seit T-048 läuft „Jetzt prüfen“ dort nur noch für eine Nachricht, die beim Wechsel von All auf Api in der lokalen Queue lag.
            if (!modules.RunsBackgroundServices)
            {
                spaMailboxes.Retain([]);
            }

            _spaLock.Release();
        }
    }

    /// <summary>Eingeschaltete Postfächer der Spas, deren Abo-Stufe die Terminannahme enthält (ADR 0015).</summary>
    private async Task<List<(SpaMailbox Box, Guid TenantId)>> LoadSpaMailboxesAsync(Guid? tenantId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var plans = Plans.With(AppModule.BookingIntake);
        var found = await db.SpaMailboxes.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Enabled && (tenantId == null || EF.Property<Guid>(b, TenantModel.Property) == tenantId))
            .Where(b => db.Tenants.Any(t => t.Id == EF.Property<Guid>(b, TenantModel.Property) && plans.Contains(t.Plan)))
            .Select(b => new { Box = b, TenantId = EF.Property<Guid>(b, TenantModel.Property) })
            .ToListAsync(ct);
        return [.. found.Select(b => (b.Box, b.TenantId))];
    }

    private async Task PollSpaMailboxAsync(SpaMailbox box, Guid tenantId, bool verifySender, CancellationToken ct)
    {
        string? error = null;
        string? sendError = box.LastSendError;
        try
        {
            var secret = box.ProtectedSecret is null ? null : secrets.Unprotect(tenantId, box.Id, box.ProtectedSecret);
            var settings = SpaMailboxSettings.Of(box, secret);
            var source = spaMailboxes.Get(settings, box.UpdatedAt);
            var mails = await source.FetchNewAsync(ct);
            if (mails.Count > 0)
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
                var ingestion = scope.ServiceProvider.GetRequiredService<MailIngestion>();
                foreach (var mail in mails)
                {
                    await ingestion.IngestAsync(mail, ct);
                    await source.MarkProcessedAsync(mail.MessageId, ct);
                }
            }

            if (verifySender && SpaMailSender.SendsItself(box))
            {
                sendError = null;
                try
                {
                    await factory.VerifySenderAsync(settings, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    sendError = Short(ex.Message);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falsches Passwort, Server weg, Schlüssel verloren: beim Spa sichtbar machen statt nur im Log.
            error = Short(ex.Message);
            LogSpaMailboxFailed(log, ex, box.Id);
        }

        await using var status = scopes.CreateAsyncScope();
        var now = time.GetUtcNow();
        await status.ServiceProvider.GetRequiredService<AispaDbContext>().SpaMailboxes.IgnoreQueryFilters()
            .Where(b => b.Id == box.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.LastCheckedAt, now).SetProperty(b => b.LastError, error)
                .SetProperty(b => b.LastSendError, sendError), CancellationToken.None);
    }

    private static string Short(string message) => message.Length > 500 ? message[..500] : message;

    public override void Dispose()
    {
        _spaLock.Dispose();
        base.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Postfach konnte nicht abgerufen werden.")]
    private static partial void LogPollFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Postfach {MailboxId} eines Spas konnte nicht abgerufen werden.")]
    private static partial void LogSpaMailboxFailed(ILogger logger, Exception ex, Guid mailboxId);
}

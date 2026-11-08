using System.Diagnostics;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Mail-Queue mit mehreren Instanzen (ADR 0087, T-048): In den Rollen Api und Worker laufen die Nachrichten der Terminannahme über
/// PostgreSQL-Queues von Wolverine, nur der Worker verarbeitet sie, der Reihe nach. Jeder Test hat eine eigene Datenbank.
/// </summary>
public sealed class MailQueueTransportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(90);

    private static readonly string[] QueueTables =
    [
        "wolverine_queue_mail_intake", "wolverine_queue_mail_intake_scheduled", "wolverine_queue_mailbox_check", "wolverine_queue_mailbox_check_scheduled",
    ];

    [Fact]
    public async Task Rolle_All_legt_keine_Queue_Tabellen_an_und_versendet_im_eigenen_Prozess()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = Started(Host(connection, "All"));
        var to = Address();
        var mailId = await SendingMailAsync(host, await TenantAsync(host), to);

        await PublishAsync(host, new ReplySendRequested(mailId));

        await WaitUntilAsync(() => Task.FromResult(fixture.Mailbox.To(to).Count == 1), "Die Antwort ging nicht hinaus.");
        Assert.Empty(await QueueTablesAsync(connection));
    }

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public async Task Api_und_Worker_legen_die_Queue_Tabellen_im_Schema_wolverine_queues_an(string role)
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);

        await using var host = Started(Host(connection, role));

        Assert.Equal(QueueTables, await QueueTablesAsync(connection));
    }

    [Fact]
    public async Task Alle_acht_Typen_kommen_wertgleich_ueber_die_Queue_im_Worker_an()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var worker = Started(Host(connection, "Worker"));
        await using var api = Started(Host(connection, "Api"));
        // Ohne Spa im Anruf (TenantId leer) gilt das einzige Spa der Datenbank.
        await TenantAsync(api);
        object[] messages =
        [
            new InboundMailReceived(Guid.NewGuid()),
            new ReplyDraftRequested(Guid.NewGuid()),
            new ReplySendRequested(Guid.NewGuid()),
            new MailTranslationRequested(Guid.NewGuid()),
            new ReplyRevisionRequested(Guid.NewGuid(), ReplyRevisionKind.Formal, "Bitte förmlich, mit „Grüßen“"),
            new ReplyRevisionRequested(Guid.NewGuid(), ReplyRevisionKind.Custom, null),
            new ReproposeRequested(Guid.NewGuid(), "lieber abends"),
            new ReproposeRequested(Guid.NewGuid(), null),
            new TwilioRecordingReceived(null, "CA1", "+43 664 1111111", "+43 1 2222222", "https://api.twilio.com/recordings/RE1", null,
                // UTC wie im Webhook (GetUtcNow); Bruchteile von Sekunden prüfen die volle Genauigkeit.
                new DateTimeOffset(2027, 3, 1, 10, 15, 30, TimeSpan.Zero).AddTicks(1234567)),
            new CheckSpaMailbox(Guid.NewGuid()),
        ];
        var workerNode = NodeOf(worker);

        foreach (var message in messages)
        {
            var session = await worker.Services.TrackActivity(Limit).AlsoTrack(api.Services).IncludeExternalTransports()
                .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => PublishAsync(api, message)));

            var executed = session.AllRecordsInOrder()
                .Where(r => r.MessageEventType == MessageEventType.ExecutionFinished && r.Envelope?.Message?.GetType() == message.GetType())
                .ToList();
            var record = Assert.Single(executed);
            Assert.Equal(workerNode, record.UniqueNodeId);
            Assert.Equal("postgresql", record.Envelope!.Destination!.Scheme);
            Assert.Equal(message, record.Envelope.Message);
            if (message is TwilioRecordingReceived call)
            {
                var received = ((TwilioRecordingReceived)record.Envelope.Message!).ReceivedAt;
                Assert.Equal((call.ReceivedAt.Ticks, call.ReceivedAt.Offset), (received.Ticks, received.Offset));
            }
        }
    }

    [Fact]
    public async Task Ohne_Worker_bleibt_die_Antwort_in_der_Queue_und_ein_spaeter_gestarteter_Worker_sendet_sie()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        // Die Api startet zuerst und ist allein im Cluster; der Worker muss den Hörer trotzdem bekommen.
        await using var api = Started(Host(connection, "Api"));
        var tenantId = await TenantAsync(api);
        var first = Address();
        var firstId = await SendingMailAsync(api, tenantId, first);

        await PublishAsync(api, new ReplySendRequested(firstId));
        await Task.Delay(TimeSpan.FromSeconds(5), Ct);

        Assert.Empty(fixture.Mailbox.To(first));
        Assert.Equal(ReplyStatus.Sending, (await MailAsync(api, firstId)).ReplyStatus);
        Assert.Equal(1, await CountAsync(connection, "wolverine_queues.wolverine_queue_mail_intake"));

        var started = Stopwatch.StartNew();
        var worker = Started(Host(connection, "Worker"));
        try
        {
            await WaitUntilAsync(() => Task.FromResult(fixture.Mailbox.To(first).Count == 1), "Der Worker hat die Antwort nicht gesendet.");
            TestContext.Current.TestOutputHelper?.WriteLine($"Erster Worker: gesendet {started.ElapsedMilliseconds} ms nach dem Start.");
        }
        finally
        {
            // Update: erst stoppen, dann starten. Was dazwischen kommt, wartet in der Queue.
            await worker.DisposeAsync();
        }

        var second = Address();
        var secondId = await SendingMailAsync(api, tenantId, second);
        await PublishAsync(api, new ReplySendRequested(secondId));
        started.Restart();
        await using var next = Started(Host(connection, "Worker"));
        await WaitUntilAsync(() => Task.FromResult(fixture.Mailbox.To(second).Count == 1), "Der neue Worker hat die Antwort nicht gesendet.");
        TestContext.Current.TestOutputHelper?.WriteLine($"Neuer Worker: gesendet {started.ElapsedMilliseconds} ms nach dem Start.");

        // Gesendet wird vor dem Speichern des Status.
        await WaitUntilAsync(async () => (await MailAsync(api, secondId)).ReplyStatus == ReplyStatus.Sent, "Die Antwort steht nicht auf Gesendet.");
        Assert.Single(fixture.Mailbox.To(first));
    }

    [Fact]
    public async Task Jetzt_pruefen_an_der_Api_laeuft_im_Worker()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var api = Started(Host(connection, "Api"));
        var tenantId = await TenantAsync(api);
        var address = Address();
        // Nur der Prüfweg von „Jetzt prüfen“ meldet die Anmeldung zum Versand; der Takt im Worker lässt diesen Wert stehen.
        fixture.SpaMailboxes.FailingSend[address] = "Anmeldung zum Versand abgelehnt (Test).";
        var boxId = await SendingMailboxAsync(api, tenantId, address);

        await PublishAsync(api, new CheckSpaMailbox(tenantId));
        await Task.Delay(TimeSpan.FromSeconds(5), Ct);

        Assert.Null((await MailboxAsync(api, boxId)).LastSendError);
        Assert.Equal(0, api.Services.GetRequiredService<SpaMailboxRegistry>().OpenCount);

        await using var worker = Started(Host(connection, "Worker"));
        await WaitUntilAsync(async () => (await MailboxAsync(api, boxId)).LastSendError is not null, "„Jetzt prüfen“ lief nicht im Worker.");
        Assert.Equal("Anmeldung zum Versand abgelehnt (Test).", (await MailboxAsync(api, boxId)).LastSendError);
        Assert.Equal(0, api.Services.GetRequiredService<SpaMailboxRegistry>().OpenCount);
    }

    [Fact]
    public async Task Scheitert_die_erste_Antwort_einmal_geht_die_zweite_trotzdem_erst_danach_hinaus()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var worker = Started(Host(connection, "Worker"));
        await using var api = Started(Host(connection, "Api"));
        var tenantId = await TenantAsync(api);
        var (first, second) = (Address(), Address());
        var firstId = await SendingMailAsync(api, tenantId, first);
        var secondId = await SendingMailAsync(api, tenantId, second);
        fixture.Mailbox.FailFor(first, times: 1);

        await PublishAsync(api, new ReplySendRequested(firstId));
        await PublishAsync(api, new ReplySendRequested(secondId));

        await WaitUntilAsync(() => Task.FromResult(fixture.Mailbox.OrderOf(first, second).Count == 2), "Nicht beide Antworten gingen hinaus.");
        Assert.Equal([first, second], fixture.Mailbox.OrderOf(first, second));
        Assert.Equal(2, fixture.Mailbox.AttemptsFor(first));
    }

    [Fact]
    public async Task Scheitert_der_Versand_dauerhaft_landet_die_Nachricht_in_der_Fehlerablage_und_die_Antwort_wird_wieder_Entwurf()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        var log = new LogCapture();
        await using var worker = Started(Host(connection, "Worker", b => b.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(log))));
        await using var api = Started(Host(connection, "Api"));
        var tenantId = await TenantAsync(api);
        var (failing, next) = (Address(), Address());
        var failingId = await SendingMailAsync(api, tenantId, failing);
        var nextId = await SendingMailAsync(api, tenantId, next);
        fixture.Mailbox.FailFor(failing);

        await PublishAsync(api, new ReplySendRequested(failingId));
        await PublishAsync(api, new ReplySendRequested(nextId));

        // Die zweite Antwort wartet, bis die erste aufgegeben ist: erster Versuch und eine Wiederholung je Pause.
        var attemptsWhenNextWasSent = 0;
        await WaitUntilAsync(() =>
        {
            if (fixture.Mailbox.To(next).Count == 0)
            {
                return Task.FromResult(false);
            }

            attemptsWhenNextWasSent = fixture.Mailbox.AttemptsFor(failing);
            return Task.FromResult(true);
        }, "Die zweite Antwort ging nicht hinaus.");
        var attempts = 1 + MailIntakeFailurePolicy.Cooldowns.Length;
        Assert.Equal(attempts, attemptsWhenNextWasSent);
        await WaitUntilAsync(async () => (await MailAsync(api, failingId)).ReplyStatus != ReplyStatus.Sending, "Die Antwort hängt bei „Wird gesendet“.");
        var unsent = await MailAsync(api, failingId);
        Assert.Equal(ReplyStatus.Draft, unsent.ReplyStatus);
        Assert.Equal(MailIntakeProcessor.UnsentNotice, unsent.ReplyNotice);
        Assert.Contains(log.Lines, l => l.Level == LogLevel.Error && l.Message.Contains("in der Fehlerablage", StringComparison.Ordinal));

        // Kein geplanter Nachlauf nach der Fehlerablage, auch nicht mit vier globalen Stufen von je 1 s (Host).
        await Task.Delay(TimeSpan.FromSeconds(4), Ct);
        Assert.Equal(attempts, fixture.Mailbox.AttemptsFor(failing));
        var counts = await worker.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin.FetchCountsAsync();
        Assert.Equal(1, counts.DeadLetter);
        Assert.Equal(0, counts.Scheduled);
        Assert.Equal(0, await CountAsync(connection, "wolverine_queues.wolverine_queue_mail_intake_scheduled"));

        // Das Team sendet erneut, der Mailserver ist wieder da.
        fixture.Mailbox.FailFor(failing, times: 0);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            await scope.ServiceProvider.GetRequiredService<MailIntakeProcessor>().QueueSendAsync(await db.InboundMails.SingleAsync(m => m.Id == failingId, Ct), Ct);
        }

        await WaitUntilAsync(() => Task.FromResult(fixture.Mailbox.To(failing).Count == 1), "Erneutes Senden ging nicht hinaus.");
        await WaitUntilAsync(async () => (await MailAsync(api, failingId)).ReplyStatus == ReplyStatus.Sent, "Die Antwort steht nicht auf gesendet.");
        Assert.Null((await MailAsync(api, failingId)).ReplyNotice);
    }

    [Fact]
    public async Task Doppelt_zugestellte_Antwort_geht_nur_einmal_hinaus()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = Started(Host(connection, "All"));
        var to = Address();
        var mailId = await SendingMailAsync(host, await TenantAsync(host), to);

        await host.Services.TrackActivity(Limit).ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
        {
            await PublishAsync(host, new ReplySendRequested(mailId));
            await PublishAsync(host, new ReplySendRequested(mailId));
        }));

        Assert.Single(fixture.Mailbox.To(to));
        Assert.Equal(ReplyStatus.Sent, (await MailAsync(host, mailId)).ReplyStatus);
    }

    /// <summary>Vier globale Wiederholstufen (die Fixture setzt drei): Fiele die Mail-Queue auf ScheduleRetry zurück, gäbe es einen fünften Versuch.</summary>
    private WebApplicationFactory<Program> Host(string connection, string role, Action<IWebHostBuilder>? configure = null) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Role", role);
            b.UseSetting("Ai:Local:Url", "");
            b.UseSetting("Queue:RetrySeconds:3", "1");
            configure?.Invoke(b);
        });

    private static WebApplicationFactory<Program> Started(WebApplicationFactory<Program> host)
    {
        _ = host.Server;
        return host;
    }

    private static string Address() => $"queue-{Guid.NewGuid():N}@example.at";

    private static Guid NodeOf(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<IWolverineRuntime>().Options.UniqueNodeId;

    private static async Task PublishAsync(WebApplicationFactory<Program> host, object message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(message);
    }

    private static async Task<Guid> TenantAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var tenant = new Tenant { Name = "Spa Queue", Country = "AT" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(Ct);
        return tenant.Id;
    }

    /// <summary>Freigegebene Antwort, wie nach „Senden“ im Posteingang (<c>ReplyStatus.Sending</c>).</summary>
    private static async Task<Guid> SendingMailAsync(WebApplicationFactory<Program> host, Guid tenantId, string to)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var mail = new InboundMail
        {
            MessageId = $"<{Guid.NewGuid():N}@example.at>",
            From = to,
            To = "spa@example.at",
            Subject = "Termin",
            Body = "Hallo",
            ReplyTo = to,
            ReplySubject = "Re: Termin",
            ReplyBody = "Danke für Ihre Anfrage.",
            ReplyStatus = ReplyStatus.Sending,
        };
        db.InboundMails.Add(mail);
        await db.SaveChangesAsync(Ct);
        return mail.Id;
    }

    /// <summary>Eigenes Postfach des Spas, das selbst versendet (sonst prüft „Jetzt prüfen“ die Anmeldung zum Versand nicht).</summary>
    private static async Task<Guid> SendingMailboxAsync(WebApplicationFactory<Program> host, Guid tenantId, string address)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var box = new SpaMailbox
        {
            Kind = MailboxKind.Imap,
            Address = address,
            Host = "imap.spa.example",
            UserName = address,
            SendReplies = true,
            SmtpHost = "smtp.spa.example",
        };
        box.ProtectedSecret = scope.ServiceProvider.GetRequiredService<SpaMailboxSecrets>().Protect(tenantId, box.Id, "geheim-123");
        db.SpaMailboxes.Add(box);
        await db.SaveChangesAsync(Ct);
        return box.Id;
    }

    private static async Task<InboundMail> MailAsync(WebApplicationFactory<Program> host, Guid mailId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().InboundMails.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(m => m.Id == mailId, Ct);
    }

    private static async Task<SpaMailbox> MailboxAsync(WebApplicationFactory<Program> host, Guid boxId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().SpaMailboxes.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(b => b.Id == boxId, Ct);
    }

    private static async Task<string[]> QueueTablesAsync(string connection)
    {
        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'wolverine_queues' ORDER BY table_name COLLATE \"C\"", sql);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var tables = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            tables.Add(reader.GetString(0));
        }

        return [.. tables];
    }

    private static async Task<long> CountAsync(string connection, string table)
    {
        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", sql);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string failure)
    {
        var deadline = DateTimeOffset.UtcNow + Limit;
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, failure);
            await Task.Delay(100, Ct);
        }
    }
}

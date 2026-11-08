using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ApiFixture))]

namespace AispaStudio.Api.Tests;

/// <summary>
/// Startet einmal pro Testlauf PostgreSQL im Container und die API dagegen (Umgebung Development).
/// Ohne Docker: <c>AISPA_TEST_POSTGRES</c> auf einen vorhandenen Server setzen; jeder Lauf bekommt dort eine eigene Datenbank.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private static readonly string? ExternalServer = Environment.GetEnvironmentVariable("AISPA_TEST_POSTGRES");

    // 300 statt 100 Verbindungen: Tests mit eigener Datenbank und eigenem Host (Einzelplatz, Abgleich, Startzustand) halten ihre Pools offen.
    private readonly PostgreSqlContainer? _postgres = ExternalServer is null ? new PostgreSqlBuilder("postgres:17-alpine").WithCommand("-c", "max_connections=300").Build() : null;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Verbindung zur Testdatenbank; für Tests, die eine eigene leere Datenbank brauchen (<see cref="CreateDatabaseAsync"/>).</summary>
    public string ConnectionString { get; private set; } = "";

    /// <summary>Alle Mails, die die API verschickt hätte (Anmeldecodes, Bestätigungen).</summary>
    public TestMailbox Mailbox { get; } = new();

    /// <summary>Alle SMS und WhatsApp-Nachrichten, die die API verschickt hätte.</summary>
    public TestTextSender Texts { get; } = new();

    /// <summary>Abruf der Kalender-Abos: nie ins Netz, der Test legt fest, was eine Adresse liefert.</summary>
    internal FakeCalendarFeed CalendarFeed { get; } = new();

    /// <summary>Eigene Postfächer der Spas, im Speicher statt per IMAP oder Graph.</summary>
    public TestSpaMailboxes SpaMailboxes { get; } = new();

    /// <summary>Testterminal, das bei einem bestimmten Betrag wie ein abgerissenes Gerät wirft.</summary>
    internal ThrowingCardTerminal CardTerminal { get; } = new();

    /// <summary>Test-Zugänge der Webhooks für SMS, WhatsApp und Anrufe (keine echten Schlüssel).</summary>
    public const string TwilioToken = "twilio-test-token";
    public const string MetaSecret = "meta-test-secret";
    public const string VoiceKey = "voice-test-key";
    public const string OperatorKey = "operator-test-key";

    public async ValueTask InitializeAsync()
    {
        string connectionString;
        if (_postgres is null)
        {
            var database = $"aispa_test_{Guid.NewGuid():N}";
            await using (var server = new NpgsqlConnection(ExternalServer))
            {
                await server.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", server);
                await create.ExecuteNonQueryAsync();
            }

            connectionString = $"{ExternalServer};Database={database}";
        }
        else
        {
            await _postgres.StartAsync();
            connectionString = _postgres.GetConnectionString();
        }

        ConnectionString = connectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:aispadb", connectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Database:SeedDemoData", "false");
            // Abfragen mit mehreren Collection-Includes ohne AsSplitQuery() sollen in den Tests auffallen (Ä73).
            builder.UseSetting("Database:ThrowOnMultipleCollectionInclude", "true");
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
            // Keine echten Claude-Aufrufe in Tests: ein Schlüssel in den User-Secrets des Rechners (Development lädt sie) würde sonst genutzt und kostet Geld.
            builder.UseSetting("Ai:Provider", "RuleBased");
            // Testdaten liegen im März 2027; alle Tests kommen von derselben Adresse.
            builder.UseSetting("OnlineBooking:MaxDaysAhead", "1000");
            builder.UseSetting("OnlineBooking:SensitiveRequestsPerMinute", "100000");
            builder.UseSetting("OnlineBooking:PublicRequestsPerMinute", "100000");
            builder.UseSetting("TextChannels:Twilio:AccountSid", "ACtest");
            builder.UseSetting("TextChannels:Twilio:AuthToken", TwilioToken);
            builder.UseSetting("TextChannels:Twilio:WebhookUrl", "https://aispa.test/api/hooks/twilio/messages");
            builder.UseSetting("TextChannels:Meta:AppSecret", MetaSecret);
            builder.UseSetting("TextChannels:Meta:VerifyToken", "verify-me");
            builder.UseSetting("TextChannels:Voice:ApiKey", VoiceKey);
            // Versand fängt TestTextSender ab; die Anbieter müssen nur genannt sein, damit SMS und WhatsApp als Kanal gelten.
            builder.UseSetting("TextChannels:SmsProvider", "Twilio");
            builder.UseSetting("TextChannels:WhatsAppProvider", "Meta");
            // Erinnerungen stoßen die Tests selbst an, je Spa und mit fester Uhrzeit.
            builder.UseSetting("Reminders:PollSeconds", "0");
            builder.UseSetting("Retention:PollSeconds", "0");
            builder.UseSetting("OnlineBooking:CardCleanupPollSeconds", "0");
            builder.UseSetting("OnlineBooking:CardHoldPollSeconds", "0");
            builder.UseSetting("OnlineBooking:PaymentRecoveryPollSeconds", "0");
            builder.UseSetting("AccessExpiry:PollSeconds", "0");
            builder.UseSetting("CalendarSubscriptions:PollSeconds", "0");
            builder.UseSetting("Operator:ApiKey", OperatorKey);
            builder.UseSetting("Queue:RetrySeconds:0", "1");
            builder.UseSetting("Queue:RetrySeconds:1", "1");
            builder.UseSetting("Queue:RetrySeconds:2", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICardTerminal>();
                services.AddSingleton<ICardTerminal, LocalCardTerminal>();
                services.AddSingleton<ICardTerminal>(CardTerminal);
                services.AddSingleton<IMailSender>(Mailbox);
                services.AddSingleton<ITextMessageSender>(Texts);
                services.AddSingleton<ISpaMailboxFactory>(SpaMailboxes);
                services.AddSingleton<IAnamnesisReader, FakeAnamnesisReader>();
                services.AddSingleton<IBriefingSummarizer, FakeBriefingSummarizer>();
                services.AddSingleton<IRebookWriter, FakeRebookWriter>();
                services.AddSingleton<IQuestionAnswerer, FakeQuestionAnswerer>();
                services.AddSingleton<IMessageTranslator, FakeMessageTranslator>();
                services.AddSingleton<IReplyReviser, FakeReplyReviser>();
                services.AddSingleton<IAiJson, FakeAiJson>();
                services.AddSingleton<ITwilioRecordings, FakeTwilioRecordings>();
                services.AddSingleton<ICalendarFeedClient>(CalendarFeed);
            });
        });

        // Erzwingt den Start (inkl. Migrationen), damit Fehler hier statt im ersten Test auftauchen.
        _ = Factory.Server;
    }

    /// <summary>Legt auf demselben Server eine weitere, leere Datenbank an (z. B. für die Einrichtung im Einzelplatz-Betrieb).</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken ct)
    {
        var database = $"aispa_extra_{Guid.NewGuid():N}";
        await using var server = new NpgsqlConnection(ConnectionString);
        await server.OpenAsync(ct);
        await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", server);
        await create.ExecuteNonQueryAsync(ct);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>Client ohne automatische Cookies, damit Tests das Anmelde-Cookie gezielt setzen können.</summary>
    public HttpClient CreateClient(string? cookie = null)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        return client;
    }
}

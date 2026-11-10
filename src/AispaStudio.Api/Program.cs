using System.Reflection;
using System.Text.Json.Serialization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Accounting;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.Auth;
using AispaStudio.Api.BusinessInfo;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Costs;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.Live;
using AispaStudio.Api.LocalAi;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Api.Pos.VatIds;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Api.Stock;
using AispaStudio.Api.Sync;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Api.TimeTracking;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

const string DatabaseName = "aispadb";

// Beim Build erzeugt "GetDocument.Insider" das OpenAPI-Dokument; dabei gibt es keine Datenbank.
var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (isOpenApiGeneration)
{
    builder.Configuration[$"ConnectionStrings:{DatabaseName}"] = "Host=localhost;Database=openapi";
    // Das Dokument beschreibt immer alle Endpunkte, auch wenn Hosting__Role in der Umgebung des Builds steht (ADR 0087).
    builder.Configuration["Hosting:Role"] = nameof(HostingRole.All);
    // Der Build fasst die Datenbank nicht an, auch wenn DataProtection__Store=Database in der Umgebung steht.
    builder.Configuration["DataProtection:Store"] = nameof(DataProtectionStore.FileSystem);
    // Der Build verbindet sich nicht mit Redis und kennt keine Verbindungszeichenfolge, auch wenn Live__Backplane=Redis in der Umgebung des Builds steht.
    builder.Configuration["Live:Backplane"] = nameof(LiveBackplaneMode.None);
}

// Ohne Verbindung scheitert sonst erst Wolverine mit einer englischen Meldung, die den Weg nicht zeigt.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(DatabaseName)))
{
    throw new InvalidOperationException(
        $"Keine Datenbankverbindung (ConnectionStrings:{DatabaseName}). In Visual Studio oder Rider als Startprojekt "
        + "„AispaStudio.AppHost“ wählen bzw. „dotnet run --project src/AispaStudio.AppHost“; Aspire startet PostgreSQL mit. "
        + "Die API allein braucht eine eigene Datenbank, z. B. dotnet user-secrets set \"ConnectionStrings:aispadb\" "
        + "\"Host=localhost;Database=aispadb;Username=postgres;Password=…\" --project src/AispaStudio.Api.");
}

// Ohne Pooling: der Kontext bekommt den Mandanten des Aufrufs in den Konstruktor (Abfragefilter).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<InboxChangeInterceptor>();
// Person und Kanal je Anfrage für den Änderungsverlauf (ADR 0082); Singleton, weil der Online-Kontext gepoolt ist.
builder.Services.AddSingleton<ChangeActorInterceptor>();
// Die Tests schalten „Database:ThrowOnMultipleCollectionInclude“ ein: eine Abfrage mit mehreren Collection-Includes ohne AsSplitQuery() wirft dann,
// statt im Betrieb nur eine Warnung zu protokollieren (Ä73).
void ThrowOnMultipleCollectionInclude(DbContextOptionsBuilder options)
{
    if (builder.Configuration.GetValue<bool>("Database:ThrowOnMultipleCollectionInclude"))
    {
        options.ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
    }
}

builder.Services.AddDbContext<AispaDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString(DatabaseName)).AddInterceptors(sp.GetRequiredService<InboxChangeInterceptor>(),
        sp.GetRequiredService<ChangeActorInterceptor>());
    ThrowOnMultipleCollectionInclude(o);
});
builder.EnrichNpgsqlDbContext<AispaDbContext>();

// Kundenbereich: eigener Kontext (Kundenkonten, Links, Anamnese) mit eigenen Migrationen in derselben Datenbank.
builder.Services.AddDbContextPool<OnlineDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString(DatabaseName), OnlineDbContextSetup.Configure)
        .AddInterceptors(sp.GetRequiredService<ChangeActorInterceptor>());
    ThrowOnMultipleCollectionInclude(o);
});
builder.EnrichNpgsqlDbContext<OnlineDbContext>();

// Schlüsselring von Data Protection: wie bisher im Dateisystem oder, für mehrere Instanzen, in derselben Datenbank (ADR 0087).
var keyStore = builder.Services.AddKeyStore(builder.Configuration, builder.Configuration.GetConnectionString(DatabaseName)!);

// Betriebsart und Webmodule (ADR 0041): im Einzelplatz-Betrieb sind alle Webmodule aus, jedes lässt sich einzeln zuschalten.
// Vor Wolverine, weil die Rolle die Mail-Queue bestimmt (ADR 0087).
var modules = ModuleSwitches.From(builder.Configuration);
builder.Services.AddSingleton(modules);

// Nachrichten-Queue: Aufträge laufen entkoppelt vom HTTP-Aufruf, dauerhaft gespeichert in PostgreSQL (Outbox/Inbox).
builder.UseWolverine(opts =>
{
    if (isOpenApiGeneration)
    {
        opts.Durability.Mode = DurabilityMode.MediatorOnly;
        return;
    }

    var persistence = opts.PersistMessagesWithPostgresql(builder.Configuration.GetConnectionString(DatabaseName)!, "wolverine");
    opts.AddMailIntakeQueue(persistence, modules);
    opts.Policies.UseDurableLocalQueues();

    // Ohne Regel landet eine Nachricht beim ersten Fehler in der Fehlerablage, auch bei einem kurzen Ausfall des Mailservers. Geplant statt
    // mit Wartezeit im Handler, damit die Queue weiterläuft. Ausgenommen sind Handler mit [MoveToErrorQueueOn]: dort schadet ein zweiter Lauf,
    // und in den Rollen Api und Worker die Mail-Queue (MailIntakeFailurePolicy): dort ginge die Reihenfolge verloren.
    var retrySeconds = builder.Configuration.GetSection("Queue:RetrySeconds").Get<int[]>() ?? [60, 300, 900];
    opts.Policies.OnAnyException().ScheduleRetry([.. retrySeconds.Select(s => TimeSpan.FromSeconds(s))]);
});

// Zahlen nur als Zahlen akzeptieren; so beschreibt das OpenAPI-Dokument sie als "integer" statt "integer | string".
// Aufzählungen als Text ("Booked" statt 0), lesbar und stabil gegenüber neuen Werten.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "AISPA Studio API";
    document.Info.Version = "v1";
    ApiVersion.VersionPaths(document);
    return Task.CompletedTask;
}));
// ADR 0085: /api/v1/… auf die Endpunkte unter /api/… umschreiben, bevor das Routing und jede Pfadprüfung laufen.
builder.Services.AddApiVersion();
var signalR = builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);

// SignalR-Backplane für mehrere Instanzen (ADR 0087); die Prüfungen laufen auch beim Befehl migrate.
var liveBackplane = signalR.AddLiveBackplane(builder.Configuration, modules);
// Weitergeleitete Kopfzeilen hinter Proxy und Ingress (ADR 0087); die Prüfungen laufen auch beim Befehl migrate.
var forwardedHeaders = builder.Services.AddForwardedHeaders(builder.Configuration, modules);
builder.Services.AddSingleton<TenantLocks>();
builder.Services.AddPasskeyIdentity(builder.Configuration, modules);
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<CustomerAnonymizer>();
builder.Services.AddScoped<AppointmentHistory>();
builder.Services.AddScoped<EntityHistory>();
builder.Services.AddScoped<PaymentHistory>();
builder.Services.AddScoped<ActivityFeed>();
builder.Services.AddSingleton<HrFileCleanup>();
builder.Services.AddBackgroundService<HrFileCleanup>(modules);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<OpeningCache>();
builder.Services.AddScoped<AreaVisitService>();
builder.Services.AddScoped<AbsenceAssistant>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<RosterService>();
builder.Services.AddScoped<DisplayBoards>();
builder.Services.AddScoped<TimeTrackingService>();
builder.Services.AddMail(builder.Configuration);
builder.Services.AddOnlineBooking(builder.Configuration, modules);
builder.AddMailIntake(modules);
builder.Services.AddReminders(builder.Configuration, modules);
builder.Services.AddRetention(builder.Configuration, modules);
builder.Services.AddAccessExpiry(builder.Configuration, modules);
builder.Services.AddCalendarSubscriptions(builder.Configuration, modules);

// Kassa: Kassenrecht je Land. Österreich signiert für den Prototyp mit der Test-Signatureinrichtung;
// eine zertifizierte Einrichtung (Signaturkarte, HSM, Cloud) ersetzt nur IRksvSignatureDevice. Deutschland braucht eine ITseClient-Umsetzung.
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payments"));
builder.Services.AddScoped<PosService>();
builder.Services.AddSingleton<SiteIdentity>();
builder.Services.AddScoped<CardTerminalService>();
builder.Services.AddSingleton<ICardTerminal, TestCardTerminal>();
builder.Services.AddSingleton<ICardTerminal, LocalCardTerminal>();
// UID-Prüfung: VIES der EU über die Queue; VatIdCheck:Provider=Test prüft ohne Netz (Entwicklung, Tests).
builder.Services.AddScoped<VatIdChecks>();
if (string.Equals(builder.Configuration["VatIdCheck:Provider"], "Test", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IVatIdValidator, TestVatIdValidator>();
}
else
{
    builder.Services.AddHttpClient(ViesVatIdValidator.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddSingleton<IVatIdValidator, ViesVatIdValidator>();
}
builder.Services.AddScoped<AccountingExport>();
builder.Services.AddScoped<CostAnalysis>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<StockDemandService>();
builder.Services.AddScoped<StockCountSessions>();
builder.Services.AddScoped<StockUsageReport>();
builder.Services.AddScoped<StoragePlaces>();
builder.Services.AddScoped<StockSuppliers>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<BusinessContactService>();
builder.Services.AddScoped<ClosureService>();
builder.Services.AddScoped<RoomClosureService>();
builder.Services.AddScoped<ClosureCancellations>();
builder.Services.AddScoped<ClosureNoticeSender>();
builder.Services.AddScoped<DepositReturns>();
builder.Services.AddScoped<PurchaseOrders>();
builder.Services.AddScoped<TreatmentCards>();
builder.Services.AddScoped<DailyReports>();
builder.Services.AddScoped<ServingService>();
builder.Services.AddScoped<RoomRestockService>();
builder.Services.AddScoped<RoomCareService>();
builder.Services.AddScoped<BriefingSummaries>();
builder.Services.AddScoped<ManagementReports>();
builder.Services.AddScoped<PayrollExport>();
builder.Services.AddScoped<Benchmark>();
// Prototyp: Beispieldaten. Der zentrale Vergleichsdienst ersetzt nur IBenchmarkPeers.
builder.Services.AddSingleton<IBenchmarkPeers, SampleBenchmarkPeers>();
builder.Services.AddScoped<RoleAssistant>();
builder.Services.AddScoped<AssistantOrders>();
builder.Services.AddBusinessAnswers();
builder.Services.AddScoped<CalendarRequestService>();
builder.Services.AddScoped<FiscalizationResolver>();
builder.Services.AddScoped<DsfinvkExport>();
builder.Services.AddScoped<TseExports>();
builder.Services.AddScoped<IFiscalization, RksvFiscalization>();
builder.Services.AddScoped<IFiscalization, KassenSichVFiscalization>();
builder.Services.AddScoped<IFiscalization, SwissFiscalization>();
builder.Services.AddScoped<IRksvSignatureDevice, TestSignatureDevice>();
builder.Services.AddSingleton<TestSignatureDeviceSwitch>();
if (builder.Configuration.GetValue<bool>("Fiscal:TestTse"))
{
    // Nur Entwicklung und Prototyp: deutsche Kassen signieren mit der Test-TSE. Nie in Produktion.
    builder.Services.AddScoped<ITseClient, TestTseClient>();
}

// Startzustand für Health-Check „startup“ und /api/system/status; ohne Migration und Beispieldaten von Anfang an bereit.
builder.Services.AddSingleton<StartupState>();
builder.Services.AddHealthChecks().AddCheck<StartupHealthCheck>("startup");

if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    builder.Services.AddHostedService<DatabaseMigrator>();
}

// Abgleich eines Einzelplatzes mit der zentralen Datenbank (ADR 0041); nach dem Migrator, damit das Protokoll schon steht.
builder.Services.AddSync(builder.Configuration, modules);
// Einzelplatz: die Windows-App beendet die API geordnet über eine Datei statt sie hart abzubrechen.
builder.Services.AddStopFile(builder.Configuration);

if (builder.Configuration.GetValue<bool>("Database:SeedDemoData"))
{
    // Nach dem Migrator registriert, damit die Tabellen schon existieren.
    builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection(DemoDataOptions.Section));
    builder.Services.AddHostedService<DemoSeeder>();
}

var app = builder.Build();
// Befehl „migrate“ (ADR 0087, Aufgabe D): migrieren und beenden, ohne Host-Start; Exitcode über Environment.ExitCode, damit der normale Pfad unverändert bleibt.
if (MigrateCommand.IsRequested(args))
{
    Environment.ExitCode = await MigrateCommand.RunAsync(app, builder.Configuration.GetConnectionString(DatabaseName)!, keyStore);
    // Entsorgen leert OpenTelemetry; sonst gehen die Protokollzeilen des Befehls (auch ein Fehler) nie an den Collector.
    await app.DisposeAsync();
    return;
}

app.LogHostingRole(modules);
app.LogLiveBackplane(liveBackplane);
// Vor RunAsync: Data Protection lädt den Schlüsselring beim Start, die übernommenen Schlüssel müssen dann schon in der Tabelle stehen.
await app.PrepareKeyStoreAsync(keyStore);

app.UseConfiguredForwardedHeaders(forwardedHeaders);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseClientAbort();

// Der Worker (Hosting:Role=Worker) liefert keine Web-Oberfläche, kein OpenAPI-Dokument und außer Health keine Endpunkte (ADR 0087).
if (modules.ServesRequests)
{
    app.UseTeamWebRoot();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
}

app.UseAuthentication();
app.UseDisplaySignIn();
app.UseAuthorization();
app.UseChannelGate();
app.UseRateLimiter();
app.UseOnlineTenant();
app.UseTenantLock();

app.MapDefaultEndpoints();
if (modules.ServesRequests)
{
    MapApplicationEndpoints(app);
}

await app.RunAsync();

// Alle Endpunkte außer Health, dazu der Live-Hub; nur in den Rollen All und Api.
static void MapApplicationEndpoints(WebApplication app)
{
    app.MapSystemEndpoints();
    app.MapAiEndpoints();
    app.MapSyncEndpoints();
    app.MapAuthEndpoints(app.Environment);
    app.MapStandaloneAuthEndpoints();
    app.MapMasterDataEndpoints();
    app.MapPriceRuleEndpoints();
    app.MapCustomerEndpoints();
    app.MapCustomerFlagEndpoints();
    app.MapCustomerEmailEndpoints();
    app.MapCalendarEndpoints();
    app.MapAbsenceEndpoints();
    app.MapStaffEndpoints();
    app.MapHistoryEndpoints();
    app.MapActivityEndpoints();
    app.MapDeviceSignIn();
    app.MapDisplayEndpoints();
    app.MapAccessChannelEndpoints();
    app.MapQuickSwitch();
    app.MapPosEndpoints();
    app.MapPromotionEndpoints();
    app.MapCalendarSubscriptionEndpoints();
    app.MapBusinessContactEndpoints();
    app.MapClosureEndpoints();
    app.MapRoomClosureEndpoints();
    app.MapTerminalEndpoints();
    app.MapRosterEndpoints();
    app.MapAbsenceRequestEndpoints();
    app.MapTimeTrackingEndpoints();
    app.ForModule(AppModule.OnlineBooking).MapOnlineEndpoints();
    // Anamnese am Tablet und auf Papier gehört zum Kern, auch ohne Kundenbereich.
    app.MapAnamnesisEndpoints();
    app.MapAnamnesisStaffEndpoints();
    app.MapTenantEndpoints();
    app.ForModule(AppModule.BookingIntake).MapMailIntakeEndpoints(app.Environment);
    app.ForModule(AppModule.BookingIntake).MapChannelEndpoints();
    app.MapAccountingEndpoints();
    app.MapCostEndpoints();
    app.MapStockEndpoints();
    app.MapProductRecommendationEndpoints();
    app.MapReportEndpoints();
    app.MapOperationsEndpoints();
    app.MapRoomCareEndpoints();
    app.MapPayrollEndpoints();
    app.MapHrFileEndpoints();
    app.MapStaffRegulationEndpoints();
    app.MapStaffDutyEndpoints();
    app.ForModule(AppModule.Assistant).MapAssistantEndpoints();
    app.ForModule(AppModule.Reminders).MapReminderEndpoints();
    app.ForModule(AppModule.Rebooking).MapRetentionEndpoints();
    app.MapHub<LiveHub>(LiveHubContract.Path).AllowChannel(AccessChannel.Display);
}

public partial class Program;

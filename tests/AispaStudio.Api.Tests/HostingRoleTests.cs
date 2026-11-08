using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Sync;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AispaStudio.Api.Tests;

/// <summary>Rolle der Instanz im Mehrinstanz-Betrieb (ADR 0087): Hintergrunddienste nur in All und Worker, Endpunkte nur in All und Api.</summary>
public sealed class HostingRoleTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Dienste, die je Installation nur einmal laufen dürfen.</summary>
    private static readonly Type[] WorkerServices =
    [
        typeof(SavedCardCleanup), typeof(UnsecuredBookingRelease), typeof(PaymentRecovery), typeof(MailboxPoller), typeof(RebookScheduler),
        typeof(ReminderScheduler), typeof(AccessExpiryScheduler), typeof(HistoryCleanup), typeof(HrFileCleanup), typeof(CalendarSubscriptionScheduler),
    ];

    /// <summary>Dienste der Instanz, in jeder Rolle; in den Tests nur der Migrator (<c>Database:MigrateOnStartup</c>).</summary>
    private static readonly Type[] InstanceServices = [typeof(DatabaseMigrator)];

    [Fact]
    public async Task Ohne_Rolle_gilt_All_mit_allen_Hintergrunddiensten_und_Endpunkten()
    {
        await using var host = Host(role: null);

        Assert.Equal(Names([.. WorkerServices, .. InstanceServices]), OwnHostedServices(host));
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/modules", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rolle_Api_startet_keine_Hintergrunddienste_und_die_Dienste_bleiben_aufloesbar()
    {
        await using var host = Host("Api");

        Assert.Equal(Names(InstanceServices), OwnHostedServices(host));
        // Endpunkte und Wolverine-Handler holen die Dienste direkt (z. B. „Jetzt prüfen“ im Postfach, Zahlungsabgleich).
        Assert.NotNull(host.Services.GetRequiredService<PaymentRecovery>());
        Assert.NotNull(host.Services.GetRequiredService<MailboxPoller>());
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/modules", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rolle_Worker_startet_die_Hintergrunddienste_und_antwortet_nur_auf_Health()
    {
        await using var host = Host("Worker");

        Assert.Equal(Names([.. WorkerServices, .. InstanceServices]), OwnHostedServices(host));
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/system/modules", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/system/status", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rolle_ohne_Ruecksicht_auf_Schreibweise_und_Leerzeichen()
    {
        await using var host = Host(" worker ");

        Assert.Equal(HostingRole.Worker, host.Services.GetRequiredService<ModuleSwitches>().Role);
        Assert.Equal(Names([.. WorkerServices, .. InstanceServices]), OwnHostedServices(host));
    }

    [Theory]
    [InlineData("Api", false)]
    [InlineData("All", true)]
    public async Task Jetzt_pruefen_laesst_in_der_Rolle_Api_keine_Verbindung_zum_Postfach_offen(string role, bool keepsConnection)
    {
        await using var host = Host(role);
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var address = $"rolle-{Guid.NewGuid():N}@spa.example";
        var input = new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993, MailboxSecurity.SslOnConnect,
            address, null, null, null, null, null);
        Assert.Equal(HttpStatusCode.OK,
            (await spa.Client.PutAsJsonAsync("/api/intake/mailbox", input with { Secret = "geheim-123" }, TestSpa.Json, Ct)).StatusCode);

        await host.Services.GetRequiredService<MailboxPoller>().CheckSpaMailboxAsync(spa.TenantId, Ct);

        // Ohne Takt (Rolle Api) räumt niemand auf; die Verbindung hielte die Zugangsdaten sonst bis zum Neustart.
        var open = host.Services.GetRequiredService<SpaMailboxRegistry>().OpenCount;
        Assert.True(keepsConnection ? open > 0 : open == 0, $"{role}: {open} offene Verbindungen");
    }

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public async Task Einzelplatz_mit_anderer_Rolle_als_All_bricht_den_Start_ab(string role)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Hosting:Role", role);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Der Einzelplatz läuft immer mit Hosting:Role=All", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Backend")]
    [InlineData("1")]
    public async Task Ungueltige_Rolle_bricht_den_Start_ab(string role)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:Role", role));

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith($"Hosting:Role „{role}“ ist ungültig", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Ohne lokales Modell, sonst hinge die Liste an den User-Secrets des Rechners (<c>LocalModelMonitor</c>).</summary>
    private WebApplicationFactory<Program> Host(string? role) => fixture.Factory.WithWebHostBuilder(b =>
    {
        b.UseSetting("Ai:Local:Url", "");
        if (role is not null)
        {
            b.UseSetting("Hosting:Role", role);
        }
    });

    /// <summary>Gestartete Hintergrunddienste der API selbst; die von Wolverine und ASP.NET Core zählen nicht.</summary>
    private static string[] OwnHostedServices(WebApplicationFactory<Program> host) =>
        Names([.. host.Services.GetServices<IHostedService>().Select(s => s.GetType()).Where(t => t.Assembly == typeof(Program).Assembly)]);

    private static string[] Names(Type[] types) => [.. types.Select(t => t.Name).Order(StringComparer.Ordinal)];
}

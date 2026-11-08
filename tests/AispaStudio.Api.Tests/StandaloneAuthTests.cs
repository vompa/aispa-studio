using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Klassische Benutzerverwaltung im Einzelplatz-Betrieb (ADR 0041): Einrichtung, Passwort-Anmeldung, Zugänge mit Startpasswort.</summary>
public sealed class StandaloneAuthTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HttpClient Client(WebApplicationFactory<Program> host, string? cookie = null)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        return client;
    }

    private static string CookieOf(HttpResponseMessage response) =>
        string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

    [Fact]
    public async Task Einrichtung_Anmeldung_Zugang_mit_Startpasswort_und_Passwortwechsel()
    {
        // Eigene leere Datenbank: die Einrichtung gibt es nur, solange noch kein Spa angelegt ist.
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Standalone", "true");
        });
        using var anonymous = Client(host);

        var state = await anonymous.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", Ct);
        Assert.True(state!.Required);

        // Passkeys und Spa-Registrierung gibt es im Einzelplatz nicht.
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/api/auth/passkey/request-options", null, Ct)).StatusCode);

        var tooShort = await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Wien", "Ida Inhaberin", "ida", "kurz"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var problem = await tooShort.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Contains("mindestens 8 Zeichen", problem!.Errors["password"][0], StringComparison.Ordinal);

        var setup = await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Wien", "Ida Inhaberin", "ida", "sommer-2026"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var owner = await setup.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct);
        Assert.Equal(("ida", StaffRole.Owner, false), (owner!.UserName, owner.Access!.Role, owner.MustChangePassword));
        var ownerCookie = CookieOf(setup);

        state = await anonymous.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", Ct);
        Assert.False(state!.Required);
        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Noch eins", "AT", "Graz", "Max", "max", "sommer-2026"), TestSpa.Json, Ct)).StatusCode);

        using var ownerClient = Client(host, ownerCookie);
        var locations = await ownerClient.GetFromJsonAsync<List<LocationDto>>("/api/locations", TestSpa.Json, Ct);
        Assert.Equal(("Wien", "Europe/Vienna", "EUR"), (locations![0].Name, locations[0].TimeZone, locations[0].Currency));

        // Einladungen gibt es im Einzelplatz nicht; Zugänge legt die Leitung direkt an.
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync("/api/staff/invitations", Ct)).StatusCode);
        var created = await ownerClient.PostAsJsonAsync("/api/staff/accounts",
            new StaffAccountInput("rita", "Rita Rezeption", "start-1234", StaffRole.Reception, false, [locations[0].Id], null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rita = await created.Content.ReadFromJsonAsync<StaffMemberDto>(TestSpa.Json, Ct);
        Assert.True(rita!.MustChangePassword);

        var duplicate = await ownerClient.PostAsJsonAsync("/api/staff/accounts",
            new StaffAccountInput("rita", "Rita Zwei", "start-1234", StaffRole.Reception, false, [locations[0].Id], null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("gibt es schon", (await duplicate.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors["userName"][0], StringComparison.Ordinal);

        // Falsches Passwort: 401; richtiges: angemeldet, aber das Startpasswort muss geändert werden.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin("rita", "falsch-1234"), TestSpa.Json, Ct)).StatusCode);
        var login = await anonymous.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("rita", "start-1234", true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var ritaUser = await login.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct);
        Assert.True(ritaUser!.MustChangePassword);
        Assert.Equal(StaffRole.Reception, ritaUser.Access!.Role);

        using var ritaClient = Client(host, CookieOf(login));
        var wrongOld = await ritaClient.PostAsJsonAsync("/api/auth/standalone/password", new PasswordChange("nicht-das", "neues-passwort"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongOld.StatusCode);
        var changed = await ritaClient.PostAsJsonAsync("/api/auth/standalone/password", new PasswordChange("start-1234", "neues-passwort"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.False((await changed.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct))!.MustChangePassword);

        // Leitung setzt ein neues Startpasswort: das alte gilt nicht mehr, das neue muss wieder geändert werden.
        var reset = await ownerClient.PostAsJsonAsync($"/api/staff/{rita.UserId}/password", new PasswordReset("reset-5678"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin("rita", "neues-passwort"), TestSpa.Json, Ct)).StatusCode);
        var again = await anonymous.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("rita", "reset-5678"), TestSpa.Json, Ct);
        Assert.True((await again.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct))!.MustChangePassword);

        // Das eigene Passwort setzt niemand über den Leitungsweg zurück.
        Assert.Equal(HttpStatusCode.Forbidden, (await ownerClient.PostAsJsonAsync($"/api/staff/{owner.Id}/password", new PasswordReset("egal-1234"), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Einrichtung_ueber_das_Netz_wird_abgelehnt_Anmeldung_geht()
    {
        // Mehrplatz im Netz: ein anderer Arbeitsplatz darf das Spa nicht vor der Inhaberin anlegen, sich danach aber anmelden.
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var remoteHost = host.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(new RemoteAddress(IPAddress.Parse("192.168.1.30")))));
        using var remote = Client(remoteHost);

        var refused = await remote.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Fremdes Spa", "AT", "Wien", "Max", "max", "sommer-2026"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.True((await remote.GetFromJsonAsync<StandaloneSetupState>("/api/auth/standalone/setup", Ct))!.Required);

        using var local = Client(host);
        Assert.Equal(HttpStatusCode.OK, (await local.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Wien", "Ida", "ida", "sommer-2026"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await remote.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin("ida", "sommer-2026"), TestSpa.Json, Ct)).StatusCode);
    }

    /// <summary>Wie eine Anfrage von einem anderen PC im Netz; im Testserver gibt es sonst keine Adresse.</summary>
    private sealed class RemoteAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    [Fact]
    public async Task Fuenf_Fehlversuche_sperren_fuer_fuenf_Minuten()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Standalone", "true");
        });
        using var client = Client(host);
        await client.PostAsJsonAsync("/api/auth/standalone/setup", new StandaloneSetup("Spa", "DE", "Berlin", "Ida", "ida", "sommer-2026"), TestSpa.Json, Ct);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/standalone/login",
                new PasswordLogin("ida", "falsch"), TestSpa.Json, Ct)).StatusCode);
        }

        // Der fünfte Fehlversuch sperrt; danach hilft auch das richtige Passwort fünf Minuten nicht.
        Assert.Equal(HttpStatusCode.Locked, (await client.PostAsJsonAsync("/api/auth/standalone/login",
            new PasswordLogin("ida", "falsch"), TestSpa.Json, Ct)).StatusCode);
        var locked = await client.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("ida", "sommer-2026"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
    }

    [Fact]
    public async Task Passwort_vergessen_ohne_Leitung_mit_dem_Wiederherstellungsschluessel()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        var file = Path.Combine(Path.GetTempPath(), $"aispa-wiederherstellung-{Guid.NewGuid():N}.json");
        const string HealthKey = "q7Rk1yQyq2f1m3k5n7p9r1t3v5x7z9B1D3F5H7J9L1M=";
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("Hosting:RecoveryFile", file);
            b.UseSetting("HealthData:CurrentKeyId", "lokal1");
            b.UseSetting("HealthData:Keys:lokal1", HealthKey);
        });
        await using var remoteHost = host.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(new RemoteAddress(IPAddress.Parse("192.168.1.30")))));
        using var client = Client(host);
        await client.PostAsJsonAsync("/api/auth/standalone/setup", new StandaloneSetup("Spa", "AT", "Wien", "Ida", "ida", "sommer-2026"), TestSpa.Json, Ct);
        var code = KeySeal.NewCode();
        Task<HttpResponseMessage> RecoverAsync(HttpClient c, string user, string recoveryCode, string password) =>
            c.PostAsJsonAsync("/api/auth/standalone/recover", new PasswordRecovery(user, recoveryCode, password), TestSpa.Json, Ct);

        try
        {
            // Ohne Sicherung kein Schlüssel: Hinweis auf die Leitung.
            var none = await RecoverAsync(client, "ida", code, "winter-2026");
            Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
            Assert.Contains("Zugänge verwalten", (await none.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail, StringComparison.Ordinal);

            // Eine ausgetauschte Datei mit eigenem Code hilft nicht: sie enthält nicht den Schlüssel dieses PCs.
            await File.WriteAllTextAsync(file, KeySeal.Seal("lokal1", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", code), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, (await RecoverAsync(client, "ida", code, "winter-2026")).StatusCode);

            await File.WriteAllTextAsync(file, KeySeal.Seal("lokal1", HealthKey, code), Ct);
            var wrong = await RecoverAsync(client, "ida", KeySeal.NewCode(), "winter-2026");
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
            Assert.Contains("recoveryCode", await wrong.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
            var unknown = await RecoverAsync(client, "niemand", code, "winter-2026");
            Assert.Contains("userName", await unknown.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
            using (var remote = Client(remoteHost))
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await RecoverAsync(remote, "ida", code, "winter-2026")).StatusCode);
            }

            // Zu kurz: abgelehnt, das alte Passwort gilt weiter.
            Assert.Equal(HttpStatusCode.BadRequest, (await RecoverAsync(client, "ida", code, "kurz")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/standalone/login",
                new PasswordLogin("ida", "sommer-2026"), TestSpa.Json, Ct)).StatusCode);

            // Gesperrt nach Fehlversuchen: der Schlüssel hebt die Sperre auf, setzt das Passwort und meldet an.
            for (var i = 0; i < 5; i++)
            {
                await client.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("ida", "falsch"), TestSpa.Json, Ct);
            }

            var recovered = await RecoverAsync(client, " ida ", code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal), "winter-2026");
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            var owner = await recovered.Content.ReadFromJsonAsync<CurrentUser>(TestSpa.Json, Ct);
            Assert.Equal(("ida", StaffRole.Owner, false), (owner!.UserName, owner.Access!.Role, owner.MustChangePassword));
            using (var signedIn = Client(host, CookieOf(recovered)))
            {
                Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/locations", Ct)).StatusCode);
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/standalone/login",
                new PasswordLogin("ida", "sommer-2026"), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/standalone/login",
                new PasswordLogin("ida", "winter-2026"), TestSpa.Json, Ct)).StatusCode);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Im_Server_Betrieb_gibt_es_die_Passwort_Anmeldung_nicht()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/standalone/login", new PasswordLogin("ida", "sommer-2026"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Nur im Einzelplatz-Betrieb.", (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title);
    }
}

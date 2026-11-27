using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Claude-Schlüssel je Spa in der App (ADR 0103). Kein echter Claude-Aufruf: die Prüfung ist eine Attrappe.</summary>
public sealed class ClaudeKeyTests(ApiFixture fixture)
{
    // Erfundene Testwerte, aus Teilen zusammengesetzt, damit kein Token-Muster im Quelltext steht.
    private const string Key = "sk-" + "ant-" + "test-only-0000000000a1b2";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schluessel_nur_mit_dem_Recht_fuer_die_Verwaltung()
    {
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception);

        var status = await reception.GetAsync("/api/system/ai/claude-key", Ct);
        var save = await reception.PutAsJsonAsync("/api/system/ai/claude-key", new ClaudeKeyInput(Key), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
    }

    [Fact]
    public async Task Gespeicherter_Schluessel_kommt_nur_maskiert_zurueck_und_gilt_ohne_Neustart_bis_zum_Entfernen()
    {
        await using var host = Host(configuredKey: "");
        using var owner = await OwnerAsync(host);

        var save = await owner.PutAsJsonAsync("/api/system/ai/claude-key", new ClaudeKeyInput(Key), Ct);
        var saved = await save.Content.ReadAsStringAsync(Ct);
        var status = await owner.GetFromJsonAsync<ClaudeKeyStatus>("/api/system/ai/claude-key", TestSpa.Json, Ct);
        var aiOn = await owner.GetFromJsonAsync<AiStatus>("/api/system/ai", TestSpa.Json, Ct);
        await owner.DeleteAsync("/api/system/ai/claude-key", Ct);
        var aiOff = await owner.GetFromJsonAsync<AiStatus>("/api/system/ai", TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.DoesNotContain(Key, saved, StringComparison.Ordinal);
        Assert.Equal(new ClaudeKeyStatus(true, ClaudeKeySource.App, "sk-ant-…a1b2", true), status);
        Assert.True(aiOn!.Claude);
        Assert.False(aiOff!.Claude);
        Assert.Equal(0, await PlainKeysInDatabaseAsync());
    }

    [Fact]
    public async Task Schluessel_aus_der_Konfiguration_hat_Vorrang_und_sperrt_Speichern_und_Entfernen()
    {
        await using var host = Host(configuredKey: "sk-" + "ant-" + "config-test-only-00000000wxyz");
        using var owner = await OwnerAsync(host);

        var status = await owner.GetFromJsonAsync<ClaudeKeyStatus>("/api/system/ai/claude-key", TestSpa.Json, Ct);
        var save = await owner.PutAsJsonAsync("/api/system/ai/claude-key", new ClaudeKeyInput(Key), Ct);
        var remove = await owner.DeleteAsync("/api/system/ai/claude-key", Ct);

        Assert.Equal(new ClaudeKeyStatus(true, ClaudeKeySource.Config, "sk-ant-…wxyz", true), status);
        Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
    }

    [Fact]
    public async Task Pruefen_meldet_einen_ungueltigen_Schluessel_verstaendlich()
    {
        await using var host = Host(configuredKey: "");
        using var owner = await OwnerAsync(host);
        var withoutKey = await owner.PostAsync("/api/system/ai/claude-key/check", null, Ct);
        await owner.PutAsJsonAsync("/api/system/ai/claude-key", new ClaudeKeyInput(Key), Ct);

        var check = await owner.PostAsync("/api/system/ai/claude-key/check", null, Ct);
        var result = await check.Content.ReadFromJsonAsync<ClaudeKeyCheck>(TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, withoutKey.StatusCode);
        Assert.Equal(ClaudeKeyCheckOutcome.Invalid, result!.Outcome);
        Assert.StartsWith("Der Schlüssel ist ungültig.", result.Message, StringComparison.Ordinal);
    }

    /// <summary>Claude eingeschaltet (nicht RuleBased), Schlüssel der Konfiguration fest vorgegeben, damit keiner aus den User-Secrets des Rechners gilt.</summary>
    private WebApplicationFactory<Program> Host(string configuredKey) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Ai:Provider", "Claude");
            b.UseSetting("Ai:Claude:ApiKey", configuredKey);
            b.ConfigureTestServices(s => s.AddSingleton<IClaudeKeyCheck, RejectingKeyCheck>());
        });

    private async Task<HttpClient> OwnerAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", await TestUsers.RegisterAsync(fixture, Ct));
        return client;
    }

    private async Task<long> PlainKeysInDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM aispa."ClaudeKeySetting" WHERE "ProtectedKey" LIKE '%test-only%'""", connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed class RejectingKeyCheck : IClaudeKeyCheck
    {
        public Task<ClaudeKeyCheck> CheckAsync(string key, CancellationToken ct) => Task.FromResult(ClaudeKeyChecks.Invalid);
    }
}

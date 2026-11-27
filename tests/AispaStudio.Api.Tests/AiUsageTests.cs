using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Verbrauch von Claude messen (T-112a). Kein echter Claude-Aufruf: die Tests melden den Verbrauch an der Stelle, an der jede Antwort von Claude
/// ihr <c>usage</c> abgibt (<see cref="AiUsageRecorder"/>), so wie ein Dienst im Scope seines Spas.
/// </summary>
public sealed class AiUsageTests(ApiFixture fixture)
{
    private const string Model = "claude-test-model";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verbrauch_wird_je_Spa_Aufgabe_und_Modell_summiert_und_der_Endpunkt_liefert_die_Summe()
    {
        var (owner, spa) = await OwnerAsync();
        var (_, other) = await OwnerAsync();

        await RecordAsync(spa, AiUsageTask.ReplyDraft, Model, new AiTokens(100, 20, 1000, 50));
        await RecordAsync(spa, AiUsageTask.ReplyDraft, Model, new AiTokens(30, 5, 0, 0));
        await RecordAsync(spa, AiUsageTask.Translation, Model, new AiTokens(7, 3, 0, 0));
        await RecordAsync(other, AiUsageTask.ReplyDraft, Model, new AiTokens(999, 999, 999, 999));
        var report = await owner.GetFromJsonAsync<AiUsageReport>("/api/system/ai/usage?days=30", TestSpa.Json, Ct);

        Assert.Equal(30, report!.Days);
        Assert.Equal(
            [new AiUsageRow(AiUsageTask.ReplyDraft, Model, 2, 130, 25, 1000, 50), new AiUsageRow(AiUsageTask.Translation, Model, 1, 7, 3, 0, 0)],
            report.Rows);
    }

    [Fact]
    public async Task Verbrauch_nur_mit_dem_Recht_fuer_die_Verwaltung()
    {
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception);

        var response = await reception.GetAsync("/api/system/ai/usage", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scheitert_das_Schreiben_der_Messung_Then_laeuft_der_KI_Aufruf_weiter()
    {
        var (owner, spa) = await OwnerAsync();

        // Zu langer Modellname: die Datenbank lehnt die Zeile ab. Würfe die Messung, scheiterte hier der Test wie sonst der KI-Aufruf.
        await RecordAsync(spa, AiUsageTask.ReplyDraft, new string('x', AiUsageModel.ModelLength + 1), new AiTokens(1, 1, 0, 0));
        var report = await owner.GetFromJsonAsync<AiUsageReport>("/api/system/ai/usage", TestSpa.Json, Ct);

        Assert.Empty(report!.Rows);
    }

    private async Task<(HttpClient Owner, Guid TenantId)> OwnerAsync()
    {
        var owner = fixture.CreateClient(await TestUsers.RegisterAsync(fixture, Ct));
        var me = await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        return (owner, me!.Access!.TenantId);
    }

    /// <summary>Wie ein Dienst mit Claude in einer Anfrage oder einem Hintergrundauftrag des Spas (<see cref="TenantContext.Use"/>).</summary>
    private async Task RecordAsync(Guid tenantId, AiUsageTask task, string model, AiTokens tokens)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<AiUsageRecorder>().RecordAsync(task, model, tokens, Ct);
    }
}

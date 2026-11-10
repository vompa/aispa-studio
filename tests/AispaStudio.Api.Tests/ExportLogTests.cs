using System.Net;
using AispaStudio.Api.Exports;
using AispaStudio.Api.Sync;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Jeder Export hinterlässt eine Zeile im Verlauf („Export Kundenliste (Excel, 312 Zeilen) …“), damit die Leitung sieht, wer Daten herausgeholt hat.</summary>
public sealed class ExportLogTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ein_Export_steht_mit_Art_Format_und_Zeilenzahl_im_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Außerhalb eines Aufrufs gibt es keine angemeldete Person; der Kanal „Team“ ergibt „vom Team“ (die Endpunkte liefern „von Lena“).
        using var actor = ChangeActor.Use(ChangeChannel.Team);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            var log = scope.ServiceProvider.GetRequiredService<ExportLog>();
            await log.RecordAsync("Kundenliste", ExportFormat.Xlsx, 312, "Suche „Huber“", Ct);
            await log.RecordAsync("Newsletter-Liste", ExportFormat.Csv, 40, null, Ct);
        }

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);

        Assert.Equal(2, history.Count);
        Assert.Equal("Export Newsletter-Liste (CSV, 40 Zeilen) vom Team", history[0].Text);
        Assert.Equal("Export Kundenliste (Excel, 312 Zeilen, Suche „Huber“) vom Team", history[1].Text);
    }

    [Fact]
    public async Task Das_Protokoll_sieht_nur_die_Leitung_und_nur_den_eigenen_Mandanten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        using var actor = ChangeActor.Use(ChangeChannel.Team);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(other.TenantId);
            await scope.ServiceProvider.GetRequiredService<ExportLog>().RecordAsync("Kundenliste", ExportFormat.Vcf, 3, null, Ct);
        }

        Assert.Empty(await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct));
        Assert.Equal("Export Kundenliste (vCard, 3 Zeilen) vom Team", Assert.Single(await other.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct)).Text);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/exports/history", Ct)).StatusCode);
    }

    [Fact]
    public void Formate_haben_Namen_fuer_den_Verlauf_und_Inhaltstypen()
    {
        Assert.Equal(("Excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"), (ExportFiles.Label(ExportFormat.Xlsx), ExportFiles.ContentType(ExportFormat.Xlsx)));
        Assert.Equal(("CSV", "text/csv; charset=utf-8"), (ExportFiles.Label(ExportFormat.Csv), ExportFiles.ContentType(ExportFormat.Csv)));
        Assert.Equal(("Portal-CSV", "text/csv; charset=utf-8"), (ExportFiles.Label(ExportFormat.Portal), ExportFiles.ContentType(ExportFormat.Portal)));
        Assert.Equal(("vCard", "text/vcard; charset=utf-8"), (ExportFiles.Label(ExportFormat.Vcf), ExportFiles.ContentType(ExportFormat.Vcf)));
        Assert.Equal(("iCal", "text/calendar; charset=utf-8"), (ExportFiles.Label(ExportFormat.Ics), ExportFiles.ContentType(ExportFormat.Ics)));
    }
}

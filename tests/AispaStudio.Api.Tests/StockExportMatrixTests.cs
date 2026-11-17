using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

/// <summary>Der Testdatensatz des Beispiel-Spa in einer eigenen Datenbank, einmal für alle Fälle der Export-Matrix: Inhaberin als Client, Wien als Standort.</summary>
public sealed class DemoStockFixture(ApiFixture api) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _host;

    public HttpClient Owner { get; private set; } = null!;

    public Guid LocationId { get; private set; }

    public DateOnly Today { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = await api.CreateDatabaseAsync(ct);
        _host = api.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        var now = TimeProvider.System.GetUtcNow();
        var tenantId = await DemoSeeder.SeedAsync(_host.Services, now, new DemoDataOptions(), NullLogger.Instance, ct);

        await using var scope = _host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var vienna = await db.Locations.AsNoTracking().FirstAsync(l => l.Name == "Wien Innere Stadt", ct);
        LocationId = vienna.Id;
        Today = LocalTime.LocalDate(now, LocalTime.Zone(vienna.TimeZone));

        using var anonymous = _host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await anonymous.PostAsJsonAsync("/api/auth/dev/login", new DevLogin(DemoAccounts.Email("inhaberin")), TestSpa.Json, ct);
        login.EnsureSuccessStatusCode();
        Owner = _host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        Owner.DefaultRequestHeaders.Add("Cookie", string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        await SubmitCountAsync(ct);
    }

    /// <summary>Das Beispiel-Spa hat keine eingereichte Zählung: so entsteht eine (Hauptlager, erstes Material, 1 Stück weniger gezählt), wie die Leitung sie am Bildschirm einreicht.</summary>
    private async Task SubmitCountAsync(CancellationToken ct)
    {
        var basePath = $"/api/stock/locations/{LocationId}";
        var overview = await Owner.GetFromJsonAsync<StockOverviewDto>(basePath, TestSpa.Json, ct);
        var item = overview!.Items.First(i => i.Places is { Count: > 0 });
        var main = (await Owner.GetFromJsonAsync<List<StoragePlaceDto>>($"{basePath}/storage-places", TestSpa.Json, ct))!.First(p => p.Kind == StoragePlaceKind.Main);
        var start = await Owner.PostAsync($"{basePath}/storage-places/{main.Id}/count-sessions", null, ct);
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<StockCountSessionDto>(TestSpa.Json, ct))!;
        (await Owner.PutAsJsonAsync($"{basePath}/count-sessions/{session.Id}/lines/{item.ItemId}", new StockCountLineInput(1m), TestSpa.Json, ct)).EnsureSuccessStatusCode();
        (await Owner.PostAsync($"{basePath}/count-sessions/{session.Id}/submit", null, ct)).EnsureSuccessStatusCode();
    }

    public ValueTask DisposeAsync()
    {
        Owner?.Dispose();
        _host?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Alle fünf Listen des Lager-Exports in Excel und CSV gegen das Beispiel-Spa (Aufgabe „Export funktioniert nicht“, P7): Status, Inhaltstyp, Dateiname, Kopfzeile,
/// mindestens eine Datenzeile; bei CSV zusätzlich Byte-Order-Mark, Strichpunkt, Umlaute und gleiche Spaltenzahl in jeder Zeile (so öffnet Excel in Österreich die Datei direkt).
/// Dazu: die Bewegungen von 30 Tagen bleiben unter der Zeilengrenze und brauchen vertretbar lange.
/// </summary>
public sealed class StockExportMatrixTests(DemoStockFixture demo) : IClassFixture<DemoStockFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Dictionary<string, (string Title, string[] Headers)> Lists = new()
    {
        ["levels"] = ("Lagerbestand", ["Material", "Lagerort", "Menge", "Einheit", "Mindestbestand", "Unter Mindestbestand"]),
        ["count"] = ("Inventur", ["Material", "Lagerort", "Einheit", "Bestand", "Gezählt", "Differenz"]),
        ["movements"] = ("Lagerbewegungen", ["Tag", "Zeit", "Material", "Lagerort", "Art", "Menge", "Einheit", "Charge", "Grund"]),
        ["orders"] = ("Bestellungen", ["Nummer", "Lieferant", "Status", "Angelegt", "Bestellt", "Positionen", "Summe netto"]),
        ["expiry"] = ("Ablaufliste", ["Material", "Charge", "Ablaufdatum", "Menge", "Einheit", "Lagerort", "Abgelaufen"]),
    };

    public static TheoryData<string, string> Combinations()
    {
        var data = new TheoryData<string, string>();
        foreach (var list in Lists.Keys)
        {
            data.Add(list, "xlsx");
            data.Add(list, "csv");
        }

        return data;
    }

    private string Url(string list, string format, int days = 30) =>
        $"/api/stock/locations/{demo.LocationId}/export?list={list}&from={demo.Today.AddDays(-days):yyyy-MM-dd}&until={demo.Today:yyyy-MM-dd}&format={format}";

    [Theory]
    [MemberData(nameof(Combinations))]
    public async Task Jede_Liste_in_Excel_und_CSV_liefert_eine_brauchbare_Datei(string list, string format)
    {
        using var response = await demo.Owner.GetAsync(Url(list, format), Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{list}/{format}: {(int)response.StatusCode} {body}");
        var (title, headers) = Lists[list];
        Assert.Equal(format == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv", response.Content.Headers.ContentType?.MediaType);
        var name = ExportResponses.FileName(response);
        Assert.StartsWith($"{title}_", name, StringComparison.Ordinal);
        Assert.EndsWith($".{format}", name, StringComparison.Ordinal);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        SampleFiles.Save(name, bytes);
        if (format == "xlsx")
        {
            var sheet = Assert.Single(new XLWorkbook(new MemoryStream(bytes)).Worksheets);
            Assert.Equal(headers, ExportResponses.Headers(sheet));
            Assert.True(sheet.LastRowUsed()!.RowNumber() >= 2, $"{list}: keine Datenzeile");
            return;
        }

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.DoesNotContain('�', text);
        var rows = ParseCsv(text);
        Assert.Equal(headers, rows[0]);
        Assert.True(rows.Count >= 2, $"{list}: keine Datenzeile");
        Assert.All(rows, r => Assert.Equal(headers.Length, r.Count));
    }

    [Fact]
    public async Task Bewegungen_der_letzten_30_Tage_bleiben_unter_der_Zeilengrenze_und_brauchen_wenige_Sekunden()
    {
        var clock = Stopwatch.StartNew();
        using var response = await demo.Owner.GetAsync(Url("movements", "xlsx"), Ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        clock.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = Assert.Single(new XLWorkbook(new MemoryStream(bytes)).Worksheets).LastRowUsed()!.RowNumber() - 1;
        Console.WriteLine($"Lagerbewegungen 30 Tage: {rows} Zeilen, {bytes.Length / 1024} KB, {clock.ElapsedMilliseconds} ms");
        Assert.InRange(rows, 1, 50_000);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"{clock.Elapsed.TotalSeconds:0.0} s");
    }

    /// <summary>Einfaches Lesen von Strichpunkt-CSV mit Anführungszeichen, wie Excel es liest (Zeilenwechsel in Anführungszeichen bleibt im Feld).</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ';':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        return rows;
    }
}

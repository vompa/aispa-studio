using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ersetzt den Speicherdialog von Windows: nimmt die Bytes entgegen oder bricht ab und merkt sich, was gefragt wurde.</summary>
internal sealed class FakeFileSaver : IFileSaver
{
    public bool Cancels { get; init; }

    public List<(string Name, byte[] Content)> Saved { get; } = [];

    public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
    {
        if (Cancels)
        {
            return Task.FromResult(false);
        }

        Saved.Add((suggestedName, content));
        return Task.FromResult(true);
    }
}

/// <summary>
/// R05 Slice 102 (Ä191): native Maske „Buchhaltung“ nach web/src/accounting. Die Tests prüfen Adresse, gesendeten Inhalt und unveränderte Bytes; die Buchungslogik und das Dateiformat prüfen die
/// API-Tests des Exports.
/// </summary>
public sealed partial class AdminAccountingTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private const string DatevExport = "GET /api/accounting/export?format=Datev&from=2026-10-01&until=2026-10-31";

    private const string OctoberPostings = "GET /api/accounting/postings?from=2026-10-01&until=2026-10-31";

    /// <summary>Kopf und eine Zeile eines Buchungsstapels in Windows-1252: „ü“ ist das Byte 0xFC, die Zeile endet mit CR LF. Fest eingetragen, nicht aus dem Code gerechnet.</summary>
    private static readonly byte[] WebExportBytes =
    [
        (byte)'E', (byte)'X', (byte)'T', (byte)'F', (byte)';', 0x0D, 0x0A,
        (byte)'M', 0xFC, (byte)'h', (byte)'e', (byte)';', (byte)'1', (byte)'9', (byte)',', (byte)'0', (byte)'0', 0x0D, 0x0A,
    ];

    private static AccountingPosting Posting(decimal tax = 19m, string? key = "3") =>
        new(new DateOnly(2026, 10, 5), "AT1-7", "1600", "4000", 89m, tax, "Beleg 7", "SPA", "EUR", key, FiscalCountry.DE);

    private static StubApi Api() => new StubApi()
        .On("GET /api/pos/revenue-accounts", new List<RevenueAccountDto>())
        .On("GET /api/accounting/settings", new AccountingSettingsDto(null, null, 4, 1, "1370", "1000", "1360", "1200", "3250", "1460", "1370", "6960", "4960", "19=3;7=2"))
        .On(OctoberPostings, new List<AccountingPosting> { Posting() })
        .OnBytes(DatevExport, WebExportBytes);

    private static AccountingViewModel Accounting(StubApi api, IFileSaver? files) => new(api.Client(), TestData.Owner(), new FixedTime())
    {
        Files = files,
        From = new DateOnly(2026, 10, 1),
        Until = new DateOnly(2026, 10, 31),
    };

    [Fact]
    public async Task Given_der_DATEV_Export_fuer_Oktober_When_er_nativ_gespeichert_wird_Then_ist_die_Datei_Byte_fuer_Byte_gleich_dem_bisherigen_Web_Export()
    {
        var api = Api();
        var files = new FakeFileSaver();
        var accounting = Accounting(api, files);
        await accounting.EnsureLoadedAsync();

        await accounting.ExportDatevCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == DatevExport);
        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal("datev-buchungsstapel.csv", name);
        Assert.Equal(WebExportBytes, content);
    }

    [Fact]
    public async Task Bricht_die_Inhaberin_den_Speicherdialog_ab_entsteht_keine_Datei_und_keine_Erfolgsmeldung()
    {
        var files = new FakeFileSaver { Cancels = true };
        var accounting = Accounting(Api(), files);
        await accounting.EnsureLoadedAsync();

        await accounting.ExportDatevCommand.ExecuteAsync(null);

        Assert.Empty(files.Saved);
        Assert.Null(accounting.ExportMessage);
        Assert.Null(accounting.ExportError);
    }

    [Fact]
    public async Task Ein_Ende_vor_dem_Beginn_wird_am_Feld_gemeldet_und_nichts_exportiert()
    {
        var api = Api();
        var files = new FakeFileSaver();
        var accounting = Accounting(api, files);
        await accounting.EnsureLoadedAsync();
        accounting.Until = new DateOnly(2026, 9, 30);

        await accounting.ExportDatevCommand.ExecuteAsync(null);

        Assert.Equal("Das Ende darf nicht vor dem Beginn liegen.", accounting.UntilError);
        Assert.Empty(files.Saved);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/accounting/export", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Schlaegt_das_Laden_eines_Landes_fehl_wird_die_Zuordnung_des_vorigen_Landes_nicht_dorthin_gespeichert()
    {
        var api = Api()
            .On("GET /api/accounting/settings?country=AT", new { }, System.Net.HttpStatusCode.InternalServerError)
            .On("PUT /api/accounting/settings?country=AT", new { });
        var accounting = Accounting(api, null);
        await accounting.EnsureLoadedAsync();

        accounting.SelectedCountry = accounting.Countries.Single(c => c.Value == FiscalCountry.AT);
        await Task.Yield();

        Assert.NotNull(accounting.Error);
        Assert.False(accounting.HasUnsavedChanges);
        Assert.False(await accounting.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/accounting/settings?country=AT");
    }
}

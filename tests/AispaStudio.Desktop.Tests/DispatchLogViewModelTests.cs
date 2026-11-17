using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>P8 (17.11.2026): das Versandprotokoll als Seitenleiste neben den Erinnerungen; Filter, Zeitraum und Zeilen kommen aus der API.</summary>
public sealed class DispatchLogViewModelTests
{
    private static readonly Guid Guest = Guid.NewGuid();

    internal static readonly DispatchLogEntryDto Rebook = new(Guid.NewGuid(), DispatchKind.Rebook, Guest, "Thomas Leitner", "Ayurveda Abhyanga", ReminderChannel.Email, "Sent",
        new DateTimeOffset(2026, 11, 2, 19, 9, 0, TimeSpan.Zero), null, "Ihr letzter Besuch bei uns ist schon eine Weile her.", null);

    internal static readonly DispatchLogEntryDto Failed = new(Guid.NewGuid(), DispatchKind.Rebook, Guest, "Anna Bauer", "Sportmassage", ReminderChannel.Email, "Failed",
        new DateTimeOffset(2026, 10, 29, 16, 43, 0, TimeSpan.Zero), null, null, "Postfach voll");

    internal static readonly DispatchLogEntryDto Reminder = new(Guid.NewGuid(), DispatchKind.Reminder, Guest, "Nina Fischer", "Massage", ReminderChannel.Sms, "Sent",
        new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero), null, null);

    private static readonly TimeProvider Now = new FixedTime(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    internal static StubApi Api() => new StubApi()
        .On("GET /api/dispatch-log?days=30", new[] { Rebook, Failed, Reminder })
        .On("GET /api/dispatch-log?kind=reminder&days=30", new[] { Reminder })
        .On("GET /api/dispatch-log?kind=rebook&days=30", new[] { Rebook, Failed })
        .On("GET /api/dispatch-log?result=failed&days=30", new[] { Failed })
        .On("GET /api/dispatch-log?days=366", new[] { Rebook, Failed, Reminder });

    private static DispatchLogViewModel Log(StubApi api) => new(api.Client(), TimeZoneInfo.Utc, Now);

    [Fact]
    public async Task Given_Erinnerungen_When_Versandprotokoll_geoeffnet_Then_nur_Terminerinnerungen_mit_Termin_und_Kanal()
    {
        var api = Api();
        var log = Log(api);

        await log.OpenAsync(DispatchFilter.Reminder);

        Assert.True(log.IsOpen);
        Assert.Equal(DispatchFilter.Reminder, log.Current);
        var row = Assert.Single(log.Rows);
        Assert.Equal("29.10. 09:00", row.When);
        Assert.Equal("Nina Fischer", row.Recipient);
        Assert.Equal("Erinnerung · Massage · Termin 31.10., 09:00 · SMS", row.Detail);
        Assert.Equal("Gesendet", row.StateText);
        Assert.False(row.HasDetail);
        Assert.Contains(api.Calls, c => c.Route == "GET /api/dispatch-log?kind=reminder&days=30");
    }

    [Fact]
    public async Task Given_Wiederbuchung_When_Zeile_angeklickt_Then_klappt_der_Text_auf_und_ein_Fehler_nennt_den_Grund()
    {
        var log = Log(Api());
        await log.OpenAsync(DispatchFilter.Rebook);

        var sent = log.Rows[0];
        var failed = log.Rows[1];
        Assert.Equal("Wiederbuchung · Ayurveda Abhyanga", sent.Detail);
        Assert.Equal("▸", sent.Chevron);
        Assert.False(sent.IsExpanded);

        sent.ToggleCommand.Execute(null);

        Assert.True(sent.IsExpanded);
        Assert.Equal("▾", sent.Chevron);
        Assert.Equal("„Ihr letzter Besuch bei uns ist schon eine Weile her.“", sent.Text);
        Assert.Equal("Fehlgeschlagen", failed.StateText);
        Assert.Equal("crit", failed.StateClass);
        Assert.Equal("Grund: Postfach voll", failed.Reason);
    }

    [Fact]
    public async Task Given_offenes_Protokoll_When_Filter_und_Zeitraum_gewechselt_Then_laedt_die_API_mit_den_Filtern_neu()
    {
        var api = Api();
        var log = Log(api);
        await log.OpenAsync(DispatchFilter.Rebook);

        log.SelectedFilter = DispatchLogViewModel.Filters.Single(f => f.Filter == DispatchFilter.Failed);
        await Settled(() => log.Rows.Count == 1);
        Assert.Equal("Anna Bauer", Assert.Single(log.Rows).Recipient);

        log.SelectedFilter = DispatchLogViewModel.Filters[0];
        log.Period = DispatchLogViewModel.Periods.Single(p => p.Days == 366);
        await Settled(() => api.Calls.Any(c => c.Route == "GET /api/dispatch-log?days=366"));
        Assert.Equal(3, log.Rows.Count);
    }

    [Fact]
    public async Task Given_Export_When_Format_gewaehlt_Then_dieselben_Filter_wie_die_Liste()
    {
        var log = Log(Api());
        await log.OpenAsync(DispatchFilter.Rebook);
        log.Period = DispatchLogViewModel.Periods.Single(p => p.Days == 90);

        Assert.Equal(["Versandprotokoll (Excel)", "Versandprotokoll (CSV)"], log.Export.Options.Select(o => o.Label));
        Assert.Equal("api/dispatch-log/export?kind=rebook&days=90&format=xlsx", log.Export.Options[0].Path());
        Assert.Equal("api/dispatch-log/export?kind=rebook&days=90&format=csv", log.Export.Options[1].Path());
    }

    [Fact]
    public async Task Given_nichts_verschickt_When_geoeffnet_Then_sagt_die_Leiste_das_in_einem_Satz()
    {
        var log = Log(Api().On("GET /api/dispatch-log?result=failed&days=30", Array.Empty<DispatchLogEntryDto>()));

        await log.OpenAsync(DispatchFilter.Failed);

        Assert.True(log.ShowEmpty);
        Assert.Equal("Nichts ist fehlgeschlagen. Alles ist angekommen.", log.EmptyText);
    }

    [Fact]
    public async Task Given_API_Fehler_When_geoeffnet_Then_Fehlermeldung_statt_leerer_Liste()
    {
        var log = Log(Api().On("GET /api/dispatch-log?days=30", new { title = "Fehler" }, System.Net.HttpStatusCode.InternalServerError));

        await log.OpenAsync(DispatchFilter.All);

        Assert.True(log.HasError);
        Assert.False(log.ShowEmpty);
        Assert.Empty(log.Rows);
    }

    [Fact]
    public async Task Given_Eintrag_aus_dem_Vorjahr_When_geladen_Then_steht_das_Jahr_in_der_Zeit()
    {
        var old = Rebook with { At = new DateTimeOffset(2025, 12, 24, 8, 5, 0, TimeSpan.Zero) };
        var log = Log(Api().On("GET /api/dispatch-log?days=30", new[] { old }));

        await log.OpenAsync(DispatchFilter.All);

        Assert.Equal("24.12.25 08:05", Assert.Single(log.Rows).When);
    }

    private static async Task Settled(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(done());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

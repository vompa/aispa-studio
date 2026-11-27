using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Startauswahl auf einem PC ohne Einrichtung (ADR 0104, T-132).</summary>
public sealed class StartChoiceTests
{
    [Fact]
    public void Startauswahl_gilt_nur_ohne_Schalter_Umgebungsadresse_und_Bestand()
    {
        Assert.True(StartChoice.IsDue([], new DesktopSettings(), null));
        Assert.True(StartChoice.IsDue([], new DesktopSettings(), " "));

        foreach (var arg in new[] { "--standalone", "--server", "--DEMO" })
        {
            Assert.False(StartChoice.IsDue([arg], new DesktopSettings(), null));
        }

        Assert.False(StartChoice.IsDue([], new DesktopSettings(), "http://localhost:5086/"));
        Assert.False(StartChoice.IsDue([], new DesktopSettings(Standalone: true), null));
        Assert.False(StartChoice.IsDue([], new DesktopSettings(ServerAddress: "https://empfang-pc:55443/"), null));
        Assert.False(StartChoice.IsDue([], new DesktopSettings(StartChoiceMade: true), null));

        // Schalter merken die Wahl, --demo nicht.
        var settings = new MemorySettingsStore();
        StandaloneMode.Resolve(["--demo"], settings);
        Assert.False(settings.Load().StartChoiceMade);
        StandaloneMode.Resolve(["--server"], settings);
        Assert.True(settings.Load().StartChoiceMade);
        Assert.False(settings.Load().Standalone);
    }

    [Fact]
    public async Task Ohne_Server_steht_die_Startauswahl_mit_Server_läuft_der_Start_wie_bisher()
    {
        var down = new StubApi { Down = true };
        var main = new MainViewModel(down.Client()) { StartChoice = new StartChoiceViewModel(new MemorySettingsStore(), _ => { }), ProbeWindow = TimeSpan.Zero };
        await main.StartAsync();
        Assert.True(main.IsStartChoiceShown);
        Assert.False(main.IsConnected);

        // „Mit einem vorhandenen Spa verbinden“ zeigt die Verbindungskarten; „Erneut versuchen“ bringt die Auswahl nicht zurück.
        main.StartChoice!.ConnectToExistingCommand.Execute(null);
        Assert.False(main.IsStartChoiceShown);
        await main.StartAsync();
        Assert.False(main.IsStartChoiceShown);

        var up = new StubApi().On("GET /api/system/status", new AispaStudio.Contracts.SystemStatus("1.0.0+abc", "Test", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", null, System.Net.HttpStatusCode.Unauthorized);
        var settings = new MemorySettingsStore();
        var served = new MainViewModel(up.Client()) { StartChoice = new StartChoiceViewModel(settings, _ => { }), ProbeWindow = TimeSpan.Zero };
        await served.StartAsync();
        Assert.False(served.IsStartChoiceShown);
        Assert.True(served.IsConnected);
        Assert.True(settings.Load().StartChoiceMade);

        // Mit lokalem Stand (ADR 0005) steht keine Auswahl, auch wenn der Server nicht antwortet.
        var cache = new OfflineCache(new MemoryOfflineStore());
        cache.Put("api/auth/me", "{}");
        var cached = new MainViewModel(new StubApi { Down = true }.Client(cache))
        {
            StartChoice = new StartChoiceViewModel(new MemorySettingsStore(), _ => { }),
            ProbeWindow = TimeSpan.Zero,
        };
        await cached.StartAsync();
        Assert.False(cached.IsStartChoiceShown);
    }

    [Fact]
    public void Neues_Spa_speichert_Einzelplatz_und_Wahl_und_startet_neu_das_Beispiel_speichert_nichts()
    {
        var settings = new MemorySettingsStore();
        var restarts = new List<bool>();
        var choice = new StartChoiceViewModel(settings, restarts.Add);

        choice.ShowDemoCommand.Execute(null);
        Assert.Equal(new DesktopSettings(), settings.Load());

        choice.SetUpCommand.Execute(null);
        Assert.True(settings.Load().Standalone);
        Assert.True(settings.Load().StartChoiceMade);
        Assert.Equal([true, false], restarts);
    }
}

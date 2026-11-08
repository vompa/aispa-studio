using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Leerlauf-Warnung und Banner der Shell als InfoBar (Runde R01, Slice 03: W18, W22).</summary>
public sealed partial class ViewTests
{
    private sealed class IdleTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = TestData.At(10);

        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static List<InfoBar> VisibleBars(Window window) => window.GetVisualDescendants().OfType<InfoBar>().Where(b => b.IsEffectivelyVisible).ToList();

    [Fact]
    public Task Leerlauf_Warnung_ist_InfoBar_Warnung_assertive_mit_Aktion_Weiter_arbeiten() => Render("hell", "leerlauf-warnung", async (window, main) =>
    {
        var time = new IdleTime();
        var api = new StubApi()
            .On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow))
            .On("GET /api/auth/quick-switch", new QuickSwitchState(QuickSwitchTests.Device, QuickSwitchTests.People))
            .On("GET /api/locations", new List<LocationDto>())
            .On("GET /api/auth/me", TestData.Reception());
        var idle = new MainViewModel(api.Client(), time: time);
        window.DataContext = idle;
        await idle.StartAsync();
        time.Now += TimeSpan.FromSeconds(4 * 60 + 40);
        await idle.TickAsync();
        await Pump();

        var bar = VisibleBars(window).Single(b => b.Message == idle.IdleText);
        Assert.Equal(InfoBarSeverity.Warning, bar.Severity);
        Assert.Equal("Weiter arbeiten", bar.ActionText);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));
        bar.ActionCommand!.Execute(null);
        Assert.False(idle.IsIdleWarning);
        return idle;
    });

    [Fact]
    public Task Banner_der_Shell_sind_InfoBars_mit_Schweregrad_und_Aktion()
    {
        var stub = Api();
        var offline = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        return Render("hell", "shell-banner", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            stub.Down = true;
            await calendar.LoadAsync();
            var id = Guid.NewGuid();
            offline.Enqueue(new PendingChange(id, "api/appointments", "{}", "Sabine Gruber, Mo. 19.10. 13:00: verschieben", DateTimeOffset.UtcNow, null));
            offline.Complete(id, new FailedChange(id, "Sabine Gruber, Mo. 19.10. 13:00: verschieben", "Lena Huber hat zu dieser Zeit schon einen Termin.",
                DateTimeOffset.UtcNow));
            await Pump();

            var bars = VisibleBars(window);
            var failed = Assert.Single(bars, b => b.Title == "Beim Abgleich nicht übernommen");
            Assert.Equal(InfoBarSeverity.Error, failed.Severity);
            var offlineBar = Assert.Single(bars, b => b.ActionText == "Verbindung prüfen");
            Assert.Equal(InfoBarSeverity.Warning, offlineBar.Severity);
            return calendar;
        }, stub, offline);
    }
}

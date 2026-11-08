using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Meldungen der Anmelde-, Start- und Verbindungsseiten als InfoBar (Runde R01, Slice 03: W5, W9, W18, W22; meldungen.md, Brief 5.15).</summary>
public sealed partial class MessageBarTests
{
    private static readonly string ViewsDir = Path.Combine(FindRepoRoot(), "src", "AispaStudio.Desktop", "Views");

    [Theory]
    [InlineData("LoginView.axaml")]
    [InlineData("MainWindow.axaml")]
    [InlineData("ShellView.axaml")]
    [InlineData("JoinView.axaml")]
    [InlineData("RestoreView.axaml")]
    [InlineData("NetworkConnectView.axaml")]
    [InlineData("NetworkView.axaml")]
    public void Meldungen_der_Start_und_Verbindungsseiten_sind_InfoBars_und_kein_Border_alert(string file)
        => Assert.DoesNotContain("Classes=\"alert\"", File.ReadAllText(Path.Combine(ViewsDir, file)), StringComparison.Ordinal);

    [Theory]
    [InlineData("NetworkView.axaml")]
    [InlineData("RestoreView.axaml")]
    public void Statuszeilen_von_Netz_und_Wiederherstellung_sind_hoeflich_angesagt(string file)
    {
        var line = MessageLine().Match(File.ReadAllText(Path.Combine(ViewsDir, file)));
        Assert.True(line.Success);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", line.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Drei_anstehende_Banner_zeigen_Abgleich_und_Offline_und_den_Hinweis_eine_weitere_Meldung()
    {
        var stack = ShellBanners.For(failed: true, offline: true, backup: true, showAll: false);
        Assert.True(stack.Failed);
        Assert.True(stack.Offline);
        Assert.False(stack.Backup);
        Assert.Equal("1 weitere Meldung", stack.HiddenText);

        var all = ShellBanners.For(failed: true, offline: true, backup: true, showAll: true);
        Assert.True(all.Backup);
        Assert.Null(all.HiddenText);
    }

    [Fact]
    public void Mit_zwei_Bannern_gibt_es_keinen_Hinweis_auf_weitere()
    {
        var stack = ShellBanners.For(failed: false, offline: true, backup: true, showAll: false);
        Assert.True(stack.Offline);
        Assert.True(stack.Backup);
        Assert.Null(stack.HiddenText);
    }

    [Fact]
    public async Task Offline_Banner_Verbindung_pruefen_fragt_den_Server_an()
    {
        var stub = new StubApi().On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow));
        var client = stub.Client(new OfflineCache(new MemoryOfflineStore(), TimeProvider.System));
        var shell = new ShellViewModel(client, TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        stub.Down = true;
        await client.GetAsync<SystemStatus>("api/system/status", TestContext.Current.CancellationToken);
        Assert.True(client.IsOffline);
        stub.Down = false;
        var before = stub.Calls.Count;

        await shell.CheckConnectionCommand.ExecuteAsync(null);

        Assert.Contains(stub.Calls.Skip(before), c => c.Route == "GET /api/system/status");
        Assert.False(client.IsOffline);
    }

    internal static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    [GeneratedRegex(@"<TextBlock[^>]*Text=""\{Binding Message\}""[^>]*>")]
    private static partial Regex MessageLine();
}

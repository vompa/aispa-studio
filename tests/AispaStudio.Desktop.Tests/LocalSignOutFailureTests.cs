using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    private sealed class ObservedSettings : ISettingsStore
    {
        private DesktopSettings _settings = new();
        public Action? Saving { get; set; }
        public DesktopSettings Load() => _settings;
        public void Save(DesktopSettings settings)
        {
            Saving?.Invoke();
            _settings = settings;
        }
    }

    private sealed class DeniedSession(Exception failure) : ISessionStore
    {
        public string? Load() => "test-session";
        public void Save(string cookie) { }
        public void Clear() => throw failure;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Lokaler_Abmeldefehler_schliesst_die_Shell_vor_dem_Speichern_und_fokussiert_Beenden(bool historyFails) => Session.Dispatch(async () =>
    {
        var settings = new ObservedSettings();
        Exception failure = historyFails ? new IOException("Verlauf") : new UnauthorizedAccessException("Sitzung");
        ISessionStore session = historyFails ? new MemorySessionStore() : new DeniedSession(failure);
        session.Save("test-session");
        var stub = Api().On("POST /api/auth/logout", null, HttpStatusCode.NoContent);
        var main = new MainViewModel(stub.Client(session: session), theme: new ThemeController(settings));
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            await main.StartAsync();
            await Pump();
            var shell = Assert.IsType<ShellViewModel>(main.Content);
            var dialog = main.DialogHost.AskAsync(new ConfirmRequest("Termin stornieren?", "Test", "Stornieren"));
            bool? closedWhenSaving = null;
            settings.Saving = () =>
            {
                closedWhenSaving = !main.IsSignedIn && main.Content is null && !main.DialogHost.HasDialog;
                if (historyFails)
                {
                    throw failure;
                }
            };
            var thrown = await Record.ExceptionAsync(() => shell.SignOutCommand.ExecuteAsync(null));
            Assert.Same(failure, thrown);
            Assert.True(closedWhenSaving);
            Assert.Equal(ConfirmChoice.Cancelled, (await dialog).Choice);
            Assert.Null(main.Content);
            Assert.False(main.IsSignedIn);
            Assert.False(main.StartCommand.CanExecute(null));
            await Pump();
            var screen = window.GetVisualDescendants().OfType<StartupScreen>().Single();
            var quit = Assert.Single(screen.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible);
            Assert.Equal("Beenden", quit.Content);
            Assert.Same(quit, window.FocusManager?.GetFocusedElement());
            Assert.Contains(screen.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Abmeldung konnte nicht gespeichert werden");
            Assert.DoesNotContain(screen.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Keine Verbindung");
            var requests = stub.Requests.Count;
            await main.StartAsync();
            Assert.Equal(requests, stub.Requests.Count);
        }
        finally
        {
            window.Close();
        }
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("GET /api/auth/me", true)]
    [InlineData("GET /api/auth/quick-switch", true)]
    [InlineData("GET /api/auth/quick-switch", false)]
    public Task Serverkontext_ignoriert_die_spaete_Startantwort(string route, bool signedIn) => Session.Dispatch(async () =>
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stub = Api().Hold(route, release.Task);
        if (!signedIn)
        {
            stub.On("GET /api/auth/me", null, HttpStatusCode.Unauthorized);
        }
        var api = stub.Client();
        var main = new MainViewModel(api);
        var starting = main.StartAsync();
        try
        {
            await PumpUntil(() => stub.Calls.Any(c => c.Route == route));
            await main.EndConnectionContextAsync(api.ForgetSignIn);
            main.ShowLocalSessionFailure("Verbindung nicht bestätigt", "Gespeicherten Stand beim nächsten Start prüfen.");
        }
        finally
        {
            release.TrySetResult();
            await starting;
        }
        Assert.Null(main.Content);
        Assert.Null(main.User);
        Assert.Null(main.Device);
        Assert.False(main.StartCommand.CanExecute(null));
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Serverkontext_beendet_bei_Widerrufsfehler_das_aktive_Terminal() => Session.Dispatch(async () =>
    {
        var terminal = new PaymentTerminalDto(Guid.NewGuid(), TestData.LocationId, "Test", TerminalProvider.Local, "127.0.0.1:9", true, TerminalProtocol.Opi);
        var settings = new MemorySettingsStore();
        settings.Save(settings.Load() with { TerminalId = terminal.Id });
        var stub = Api().On("GET /api/locations", new[] { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new[] { terminal });
        var main = new MainViewModel(stub.Client(), theme: new ThemeController(settings));
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Devices.InitializeAsync();
        Assert.NotNull(shell.Devices.Agent);
        var failure = new IOException("Testwiderruf");
        var closedWhenRevoking = false;
        var thrown = await Record.ExceptionAsync(() => main.EndConnectionContextAsync(() =>
        {
            closedWhenRevoking = main.Content is null && !main.IsSignedIn && !main.DialogHost.HasDialog;
            throw failure;
        }));
        Assert.Same(failure, thrown);
        Assert.True(closedWhenRevoking);
        Assert.Null(shell.Devices.Agent);
        return true;
    }, CancellationToken.None);
}

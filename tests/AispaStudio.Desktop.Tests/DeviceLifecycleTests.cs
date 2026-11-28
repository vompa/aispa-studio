using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Geraeteladen_beim_Widerruf_wird_abgewartet_und_meldet_nur_einen_fremden_Abbruch(bool foreignCancellation) => Session.Dispatch(async () =>
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var route = $"GET /api/pos/terminals?locationId={TestData.LocationId}";
        using var external = new CancellationTokenSource();
        external.Cancel();
        var error = new OperationCanceledException(external.Token);
        var stub = Api().On("GET /api/locations", new[] { TestData.Location }).Hold(route, gate.Task)
            .OnJson(route, _ => foreignCancellation ? throw error : Array.Empty<PaymentTerminalDto>())
            .On("POST /api/auth/logout", null, HttpStatusCode.NoContent);
        var diagnostics = new List<Exception>();
        var session = new MemorySessionStore();
        session.Save("test-session");
        var main = new MainViewModel(stub.Client(session: session)) { LogDeviceLoadFailure = diagnostics.Add };
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await PumpUntil(() => stub.Calls.Any(call => call.Route == route));
        var ending = shell.SignOutCommand.ExecuteAsync(null);
        try
        {
            Assert.False(ending.IsCompleted);
            Assert.Null(main.Content);
            Assert.Null(session.Load());
        }
        finally
        {
            gate.SetResult();
            await ending;
        }
        Assert.IsType<LoginViewModel>(main.Content);
        Assert.Null(shell.Devices.Agent);
        if (foreignCancellation)
        {
            Assert.Same(error, Assert.Single(diagnostics));
        }
        else
        {
            Assert.Empty(diagnostics);
        }
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("liste")]
    [InlineData("json")]
    [InlineData("abbruch")]
    public Task Geraeteladen_meldet_die_Originalursache_vor_der_Abmeldung(string response) => Session.Dispatch(async () =>
    {
        var session = new MemorySessionStore();
        session.Save("test-session");
        var route = $"GET /api/pos/terminals?locationId={TestData.LocationId}";
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var foreignStop = new CancellationTokenSource();
        foreignStop.Cancel();
        var stub = Api().On("GET /api/locations", new[] { TestData.Location }).Hold(route, gate.Task)
            .On("POST /api/auth/logout", null, HttpStatusCode.NoContent);
        if (response == "abbruch")
        {
            stub.OnJson(route, _ => throw new OperationCanceledException(foreignStop.Token));
        }
        else
        {
            stub.On(route, response == "json" ? "keine Liste" : Array.Empty<PaymentTerminalDto>());
        }

        var diagnostics = new List<Exception>();
        var main = new MainViewModel(stub.Client(session: session)) { LogDeviceLoadFailure = diagnostics.Add };
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            await main.StartAsync();
            var shell = Assert.IsType<ShellViewModel>(main.Content);
            await PumpUntil(() => stub.Calls.Any(call => call.Route == route));
            gate.SetResult();
            var loadFailure = await Record.ExceptionAsync(shell.Devices.InitializeAsync);
            await Pump();
            if (response == "liste")
            {
                Assert.Null(loadFailure);
                Assert.Empty(diagnostics);
            }
            else
            {
                if (response == "json")
                {
                    Assert.IsType<JsonException>(loadFailure);
                }
                else
                {
                    Assert.IsAssignableFrom<OperationCanceledException>(loadFailure);
                }
                Assert.Same(loadFailure, Assert.Single(diagnostics));
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
                    t => t.IsEffectivelyVisible && t.Text == "Die Geräte konnten nicht geladen werden.");
            }

            await shell.SignOutCommand.ExecuteAsync(null);
            Assert.IsType<LoginViewModel>(main.Content);
            Assert.False(main.HasLocalSessionFailure);
            Assert.Null(session.Load());
            Assert.Equal(response == "liste" ? 0 : 1, diagnostics.Count);
        }
        finally
        {
            gate.TrySetResult();
            window.Close();
        }
        return true;
    }, CancellationToken.None);
}

public sealed class DeviceLifecycleTests
{
    private sealed class HeldPolls(StubApi stub) : DelegatingHandler(stub)
    {
        private int _inboxReads;
        public TaskCompletionSource TerminalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CheckInStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReturnErrors { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public InvalidDataException TerminalFailure { get; } = new("Terminalabfrage fehlgeschlagen.");
        public InvalidDataException CheckInFailure { get; } = new("Posteingangsabfrage fehlgeschlagen.");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/jobs", StringComparison.Ordinal))
            {
                TerminalStarted.TrySetResult();
                await ReturnErrors.Task;
                throw TerminalFailure;
            }
            if (path.EndsWith("/inbox", StringComparison.Ordinal) && Interlocked.Increment(ref _inboxReads) > 1)
            {
                CheckInStarted.TrySetResult();
                await ReturnErrors.Task;
                throw CheckInFailure;
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task Geraeteabbau_erhaelt_beide_waehrend_der_Abmeldung_eintreffenden_Fehler()
    {
        var terminal = new PaymentTerminalDto(Guid.NewGuid(), TestData.LocationId, "Test", TerminalProvider.Local, "127.0.0.1:9", true, TerminalProtocol.Opi);
        var settings = new MemorySettingsStore();
        settings.Save(settings.Load() with { TerminalId = terminal.Id });
        var stub = new StubApi().On("GET /api/locations", new[] { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new[] { terminal });
        using var handler = new HeldPolls(stub);
        var api = new ApiClient(new Uri("http://localhost/"), new MemorySessionStore(), handler);
        var logoutRequested = false;
        var shell = new ShellViewModel(api, TestData.Reception(), null, TimeProvider.System, new ThemeController(settings),
            "Test", () => { logoutRequested = true; return Task.CompletedTask; }, checkInterval: TimeSpan.FromMilliseconds(30));
        try
        {
            await shell.InitializeAsync();
            await Task.WhenAll(handler.TerminalStarted.Task, handler.CheckInStarted.Task).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var ending = shell.SignOutCommand.ExecuteAsync(null);
            Assert.True(logoutRequested);
            Assert.False(ending.IsCompleted);
            handler.ReturnErrors.SetResult();
            var error = await Assert.ThrowsAsync<AggregateException>(() => ending);
            var causes = error.Flatten().InnerExceptions;
            Assert.Contains(handler.TerminalFailure, causes);
            Assert.Contains(handler.CheckInFailure, causes);
            Assert.Null(shell.Devices.Agent);
        }
        finally
        {
            handler.ReturnErrors.TrySetResult();
        }
    }
}

using System.Net;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class ConnectionReleaseTests
{
    public enum ConnectionAction { Release, Forget, Connect }

    [Theory]
    [InlineData(false, ConnectionAction.Release)]
    [InlineData(true, ConnectionAction.Release)]
    [InlineData(false, ConnectionAction.Forget)]
    [InlineData(true, ConnectionAction.Forget)]
    [InlineData(false, ConnectionAction.Connect)]
    [InlineData(true, ConnectionAction.Connect)]
    public async Task Zielwechsel_startet_ohne_alte_Arbeitsflaeche_und_erhaelt_Auftraege(bool mainPc, ConnectionAction action)
    {
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(ServerAddress: "https://localhost/", ServerCode: mainPc ? "test" : null));
        var current = ServerChoice.Resolve(settings.Load(), null);
        var session = new MemorySessionStore();
        var disk = new MemoryOfflineStore();
        var cache = new OfflineCache(disk);
        var user = TestData.Reception();
        using var server = new StubApi().OnSignIn("POST /api/auth/dev/login", user);
        var api = new ApiClient(current.Address, session, server, cache);
        Assert.True((await api.SignInAsTestAccountAsync("test", TestContext.Current.CancellationToken)).Ok);
        Assert.NotNull(session.Load());
        Assert.NotNull(new OfflineCache(disk).Get("api/auth/me"));
        var tenant = user.Access!.TenantId;
        cache.Tenant = tenant;
        var pending = new PendingChange(Guid.NewGuid(), "api/customers", "{}", "Kunde", DateTimeOffset.UtcNow, null, TenantId: tenant);
        var failed = new FailedChange(Guid.NewGuid(), "Kunde", "Abgelehnt", DateTimeOffset.UtcNow, tenant);
        cache.Enqueue(pending);
        cache.Complete(failed.Id, failed);
        cache.Put("api/customers/probe", "{}");
        cache.Flush();

        using var unavailable = new StubApi { Down = true };
        MainViewModel? restarted = null;
        Task? startup = null;
        void Restart()
        {
            var target = ServerChoice.Resolve(settings.Load(), null);
            if (action == ConnectionAction.Connect)
            {
                Assert.Equal("neues-spa", target.Address.Host);
                Assert.Equal(mainPc ? ServerSource.MainPc : ServerSource.Server, target.Source);
            }
            else
            {
                Assert.Equal(ServerSource.Default, target.Source);
            }
            restarted = new MainViewModel(new ApiClient(target.Address, session, unavailable, new OfflineCache(disk)))
            {
                StartChoice = StartChoice.IsDue([], settings.Load(), null) ? new StartChoiceViewModel(settings, _ => { }) : null,
                ProbeWindow = TimeSpan.Zero,
            };
            startup = restarted.StartAsync();
        }

        if (action != ConnectionAction.Release)
        {
            server.On("GET /api/system/status", null, HttpStatusCode.InternalServerError)
                .On("GET /api/auth/me", user);
            var initial = new MainViewModel(new ApiClient(current.Address, session, server, new OfflineCache(disk))) { ProbeWindow = TimeSpan.Zero };
            await initial.StartAsync();
            Assert.True(initial.IsSignedIn);
            Assert.False(initial.IsOffline);

            server.On("GET /api/auth/me", null, HttpStatusCode.InternalServerError);
            api = new ApiClient(current.Address, session, server, new OfflineCache(disk));
            var connect = new ServerConnectViewModel(settings, Restart, (_, _) => Task.FromResult<string?>(null), api.ForgetSignIn);
            var network = new NetworkConnectViewModel(settings, Restart, (_, _, _) => Task.FromResult<string?>(null), api.ForgetSignIn);
            var start = new MainViewModel(api) { ServerConnect = connect, Connect = network, ProbeWindow = TimeSpan.Zero };
            await start.StartAsync();
            Assert.True(start.IsOffline && !start.IsStartChoiceShown && !start.IsBlocked);
            Assert.True(mainPc ? network.HasSaved : connect.HasSaved);
            Assert.NotNull(session.Load());
            Assert.NotNull(new OfflineCache(disk).Get("api/auth/me"));
            if (action == ConnectionAction.Forget)
            {
                (mainPc ? network.ForgetCommand : connect.ForgetCommand).Execute(null);
            }
            else
            {
                network.Address = connect.Address = "https://neues-spa/";
                network.Code = "1111-2222-3333-4444-5555";
                await (mainPc ? network.ConnectCommand : connect.ConnectCommand).ExecuteAsync(null);
            }
        }
        else
        {
            var link = new ServerLinkViewModel(settings, current, Restart, api.ForgetSignIn);
            link.ReleaseCommand.Execute(null);
        }
        Assert.NotNull(startup);
        await startup;
        Assert.NotNull(restarted);
        Assert.True(restarted.IsOffline && !restarted.IsSignedIn, $"Startauswahl={restarted.IsStartChoiceShown}, sichtbare Arbeitsfläche={restarted.Content is ShellViewModel && restarted.CanShowContent}.");
        Assert.Equal(action != ConnectionAction.Connect, restarted.IsStartChoiceShown);
        Assert.False(restarted.IsSignedIn);
        Assert.Null(api.SessionCookie);
        Assert.Null(session.Load());
        var reopened = new OfflineCache(disk) { Tenant = tenant };
        Assert.Null(reopened.Get("api/auth/me"));
        Assert.Null(reopened.Get("api/customers/probe"));
        Assert.Equal(pending, Assert.Single(reopened.Pending));
        Assert.Equal(failed, Assert.Single(reopened.Failed));
    }

    private sealed class LockedSession : ISessionStore
    {
        public IOException Failure { get; } = new("Sitzungsspeicher ist gesperrt.");
        public string? Load() => null;
        public void Save(string value) => throw Failure;
        public void Clear() => throw Failure;
    }

    [Fact]
    public void Gesperrter_Sitzungsspeicher_stoppt_Adresswechsel_und_Neustart()
    {
        var settings = new MemorySettingsStore();
        var original = new DesktopSettings(ServerAddress: "https://localhost/", ServerCode: "test");
        settings.Save(original);
        var current = ServerChoice.Resolve(original, null);
        var session = new LockedSession();
        var api = new ApiClient(current.Address, session);
        var restarts = 0;
        var link = new ServerLinkViewModel(settings, current, () => restarts++, api.ForgetSignIn);

        var failure = Assert.Throws<IOException>(() => link.ReleaseCommand.Execute(null));

        Assert.Same(session.Failure, failure);
        Assert.Equal(original, settings.Load());
        Assert.Equal(0, restarts);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Startkarte_zeigt_Fehler_und_behaelt_das_Ziel_bei_gesperrtem_Sitzungsspeicher(bool mainPc, bool connect)
    {
        var settings = new MemorySettingsStore();
        var original = new DesktopSettings(ServerAddress: "https://localhost/", ServerCode: mainPc ? "test" : null);
        settings.Save(original);
        var session = new LockedSession();
        var api = new ApiClient(ServerChoice.Resolve(original, null).Address, session);
        var restarts = 0;
        var server = new ServerConnectViewModel(settings, () => restarts++, (_, _) => Task.FromResult<string?>(null), api.ForgetSignIn) { Address = "https://neues-spa/" };
        var network = new NetworkConnectViewModel(settings, () => restarts++, (_, _, _) => Task.FromResult<string?>(null), api.ForgetSignIn)
        {
            Address = server.Address,
            Code = "1111-2222-3333-4444-5555",
        };
        if (connect)
        {
            await Assert.ThrowsAsync<IOException>(() => (mainPc ? network.ConnectCommand : server.ConnectCommand).ExecuteAsync(null));
        }
        else
        {
            Assert.Throws<IOException>(() => (mainPc ? network.ForgetCommand : server.ForgetCommand).Execute(null));
        }

        Assert.Contains("Anmeldung", mainPc ? network.Error : server.Error, StringComparison.Ordinal);
        Assert.Equal(original, settings.Load());
        Assert.Equal(0, restarts);
    }
}

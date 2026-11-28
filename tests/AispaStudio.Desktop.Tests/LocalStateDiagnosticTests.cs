using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

public sealed class LocalStateDiagnosticTests
{
    private sealed class DeniedSession : ISessionStore
    {
        public UnauthorizedAccessException Error { get; } = new("Test-Sitzung");
        public string? Load() => null;
        public void Save(string cookie) => throw new NotSupportedException();
        public void Clear() => throw Error;
    }

    private sealed class DeniedCache : IOfflineStore
    {
        public IOException Error { get; } = new("Test-Lesestand");
        public OfflineState Load() => OfflineState.Empty();
        public void Save(OfflineState state) => throw Error;
        public void Clear() => throw Error;
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Lokaler_Doppelfehler_bleibt_mit_beiden_Ursachen_diagnostizierbar(HttpStatusCode denial)
    {
        var session = new DeniedSession();
        var offline = new DeniedCache();
        var api = new StubApi().On("GET /api/auth/me", null, denial).Client(new OfflineCache(offline), session);
        var transport = new List<Exception>();
        var local = new List<Exception>();
        api.TransportFailed += transport.Add;
        api.LocalStateFailed += local.Add;
        var result = await api.GetAsync<CurrentUser>("api/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(denial, result.Status);
        Assert.Contains("lokale Abmeldung", result.Error, StringComparison.Ordinal);
        Assert.Empty(transport);
        var error = Assert.IsType<IOException>(Assert.Single(local));
        var causes = Assert.IsType<AggregateException>(error.InnerException);
        Assert.Collection(causes.InnerExceptions, first => Assert.Same(session.Error, first), second => Assert.Same(offline.Error, second));
    }
}

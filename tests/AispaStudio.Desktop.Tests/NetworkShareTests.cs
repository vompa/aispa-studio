using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Mehrplatz im eigenen Netz (ADR 0041): Zertifikat, Verbindungscode und Verbinden eines weiteren PCs.</summary>
public sealed class NetworkShareTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static X509Certificate2 Certificate() => NetworkShare.CreateCertificate("empfang-pc", DateTimeOffset.UtcNow);

    [Fact]
    public void Verbindungscode_ist_der_Fingerabdruck_und_gilt_nur_fuer_sein_Zertifikat()
    {
        using var mine = Certificate();
        using var other = Certificate();
        var code = NetworkShare.Code(mine);

        Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){4}$", code);
        Assert.True(NetworkShare.Matches(mine, code));
        Assert.True(NetworkShare.Matches(mine, code.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant()));
        Assert.False(NetworkShare.Matches(other, code));
        Assert.False(NetworkShare.Matches(null, code));
        Assert.Null(NetworkShare.NormalizeCode("3F9A-12C4"));
        Assert.Null(NetworkShare.NormalizeCode("ZZZZ-12C4-77B0-E1D2-9A0B"));
    }

    [Theory]
    [InlineData("empfang-pc", "https://empfang-pc:55443/")]
    [InlineData(" 192.168.1.20:6000 ", "https://192.168.1.20:6000/")]
    [InlineData("https://Empfang-PC:55443/", "https://empfang-pc:55443/")]
    [InlineData("https://empfang-pc", "https://empfang-pc:55443/")]
    [InlineData("http://empfang-pc:55443", null)]
    [InlineData("", null)]
    public void Adresse_wird_ergaenzt_aber_nie_unverschluesselt(string input, string? expected) =>
        Assert.Equal(expected, NetworkShare.NormalizeAddress(input)?.ToString());

    [Fact]
    public async Task Weiterer_PC_erreicht_den_Haupt_PC_nur_mit_dem_richtigen_Code()
    {
        using var certificate = Certificate();
        using var other = Certificate();
        await using var host = TlsHost.Start(certificate);

        Assert.Null(await NetworkConnectViewModel.ProbeAsync(host.Address, NetworkShare.Code(certificate), Ct));

        var wrong = await NetworkConnectViewModel.ProbeAsync(host.Address, NetworkShare.Code(other), Ct);
        Assert.StartsWith("Der Verbindungscode passt nicht zu 127.0.0.1.", wrong, StringComparison.Ordinal);

        // Ohne Code (wie im Server-Betrieb) gilt das selbst ausgestellte Zertifikat ebenfalls nicht.
        var api = new ApiClient(host.Address, new MemorySessionStore());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.StatusAsync(Ct));
    }

    [Fact]
    public async Task Verbinden_prueft_merkt_sich_Adresse_und_Code_und_startet_neu()
    {
        var settings = new MemorySettingsStore();
        var restarts = 0;
        var probed = new List<(Uri, string)>();
        string? answer = "Der Verbindungscode passt nicht zu empfang-pc.";
        var connect = new NetworkConnectViewModel(settings, () => restarts++, (address, code, _) =>
        {
            probed.Add((address, code));
            return Task.FromResult<string?>(answer);
        });
        Assert.False(connect.ConnectCommand.CanExecute(null));

        connect.Address = "http://empfang-pc";
        connect.Code = "3f9a 12c4 77b0 e1d2 9a0b";
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.Contains("https://empfang-pc:55443", connect.Error, StringComparison.Ordinal);

        connect.Address = "empfang-pc";
        connect.Code = "3F9A-12C4";
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.Contains("20 Zeichen", connect.Error, StringComparison.Ordinal);
        Assert.Empty(probed);

        connect.Code = "3f9a 12c4 77b0 e1d2 9a0b";
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(answer, connect.Error);
        Assert.Equal((0, null), (restarts, settings.Load().ServerAddress));

        answer = null;
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.Null(connect.Error);
        Assert.Equal((new Uri("https://empfang-pc:55443/"), "3F9A-12C4-77B0-E1D2-9A0B"), probed[^1]);
        Assert.Equal(("https://empfang-pc:55443/", "3F9A-12C4-77B0-E1D2-9A0B"), (settings.Load().ServerAddress, settings.Load().ServerCode));
        Assert.Equal(1, restarts);
        Assert.True(connect.HasSaved);

        connect.ForgetCommand.Execute(null);
        Assert.Equal((null, null), (settings.Load().ServerAddress, settings.Load().ServerCode));
        Assert.Equal(2, restarts);
    }

    [Fact]
    public async Task Haupt_PC_gibt_frei_und_nimmt_die_Freigabe_zurueck()
    {
        var local = new SharingLocal();
        var readOnly = new NetworkViewModel(local);
        Assert.False(readOnly.ShareCommand.CanExecute(null));

        var network = new NetworkViewModel(local) { CanConfigure = true };
        Assert.False(network.IsShared);
        Assert.False(network.StopSharingCommand.CanExecute(null));

        await network.ShareCommand.ExecuteAsync(null);
        Assert.True(network.IsShared);
        Assert.Equal("3F9A-12C4-77B0-E1D2-9A0B", network.Code);
        Assert.Contains("https://empfang-pc:55443/", network.AddressesText, StringComparison.Ordinal);
        Assert.Contains("Firewall", network.Message, StringComparison.Ordinal);
        Assert.False(network.ShareCommand.CanExecute(null));

        await network.StopSharingCommand.ExecuteAsync(null);
        Assert.False(network.IsShared);
        Assert.Equal([true, false], local.Calls);
    }

    private sealed class SharingLocal : ILocalServer
    {
        public List<bool> Calls { get; } = [];

        public NetworkShareInfo? Network { get; private set; }

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default)
        {
            Calls.Add(share);
            Network = share ? new NetworkShareInfo([new Uri("https://empfang-pc:55443/")], "3F9A-12C4-77B0-E1D2-9A0B") : null;
            return Task.FromResult(true);
        }

        public Uri ApiAddress { get; } = new("http://127.0.0.1:55086/");

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public string? CentralDescription => null;

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default) => Task.FromResult(false);

        public string? ShareCode => null;

        public string SetShareCode(string? code) => code ?? "";

        public bool HasRecoveryKey => false;

        public string CreateRecoveryKey() => "";

        public Task<string> BackupAsync(string folder, CancellationToken ct = default) => Task.FromResult("");

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>Kleinster HTTPS-Server mit dem Zertifikat des Haupt-PCs; beantwortet jede Anfrage mit einem Status.</summary>
    internal sealed class TlsHost : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private readonly X509Certificate2 _certificate;

        private TlsHost(X509Certificate2 certificate)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Address = new Uri($"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            // Über PKCS#12, damit der Schlüssel auch unter Windows für TLS taugt.
            _certificate = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
            _loop = Task.Run(() => ServeAsync(_certificate));
        }

        public Uri Address { get; }

        public static TlsHost Start(X509Certificate2 certificate) => new(certificate);

        private async Task ServeAsync(X509Certificate2 certificate)
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        try
                        {
                            await using var tls = new SslStream(client.GetStream());
                            await tls.AuthenticateAsServerAsync(certificate);
                            var buffer = new byte[4096];
                            var request = "";
                            while (!request.Contains("\r\n\r\n", StringComparison.Ordinal))
                            {
                                var read = await tls.ReadAsync(buffer);
                                if (read == 0)
                                {
                                    return;
                                }

                                request += Encoding.ASCII.GetString(buffer, 0, read);
                            }

                            var body = """{"version":"1.0.0","environment":"Production","serverTime":"2026-10-19T09:00:00Z"}""";
                            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
                            await tls.WriteAsync(Encoding.ASCII.GetBytes(response));
                        }
                        catch (Exception ex) when (ex is IOException or System.Security.Authentication.AuthenticationException)
                        {
                            // Der andere PC hat das Zertifikat abgelehnt.
                        }
                    }
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            await _loop;
            _listener.Dispose();
            _stop.Dispose();
            _certificate.Dispose();
        }
    }
}

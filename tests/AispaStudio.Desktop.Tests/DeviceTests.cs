using System.Net;
using System.Net.Sockets;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.Services.Zvt;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kartenterminal per ZVT gegen ein nachgebautes Terminal im selben Prozess, dazu die Ansteuerung durch die App.</summary>
public sealed class DeviceTests
{
    /// <summary>Ein Terminal, das je Verbindung ein festes Drehbuch abspielt und mitschreibt, was die Kasse schickt.</summary>
    private sealed class FakeTerminal : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<byte[], IEnumerable<byte[]>> _answer;
        private readonly Task _loop;

        public FakeTerminal(Func<byte[], IEnumerable<byte[]>> answer)
        {
            _answer = answer;
            _listener.Start();
            _loop = Task.Run(ServeAsync);
        }

        public List<byte[]> Received { get; } = [];

        public string Address => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();
                    try
                    {
                        while (true)
                        {
                            var packet = await ReadAsync(stream);
                            if (packet[0] == 0x80)
                            {
                                continue; // Bestätigung der Kasse
                            }

                            lock (Received)
                            {
                                Received.Add(packet);
                            }

                            await stream.WriteAsync(new byte[] { 0x80, 0x00, 0x00 });
                            foreach (var reply in _answer(packet))
                            {
                                await stream.WriteAsync(reply);
                                var ack = await ReadAsync(stream);
                                Assert.Equal(0x80, ack[0]);
                            }
                        }
                    }
                    catch (EndOfStreamException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException or InvalidOperationException)
            {
            }
        }

        private static async Task<byte[]> ReadAsync(NetworkStream stream)
        {
            var header = new byte[3];
            await stream.ReadExactlyAsync(header);
            var data = new byte[header[2]];
            await stream.ReadExactlyAsync(data);
            return [.. header, .. data];
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop;
            _listener.Dispose();
        }
    }

    private static readonly byte[] Completion = [0x06, 0x0F, 0x00];

    /// <summary>Statusinformation: Ergebnis 00, Trace 123, Belegnummer 0042, PAN mit maskierten Stellen, Kartenname, Genehmigungsnummer.</summary>
    private static byte[] Status(byte result = 0x00)
    {
        byte[] data =
        [
            0x27, result,
            0x0B, 0x00, 0x01, 0x23,
            0x87, 0x00, 0x42,
            0x22, 0xF0, 0xF8, 0x67, 0x00, 0xEE, 0xEE, 0xEE, 0xEE, 0x12, 0x34,
            0x8B, 0xF0, 0xF4, (byte)'V', (byte)'I', (byte)'S', (byte)'A',
            0x3B, (byte)'A', (byte)'B', (byte)'1', (byte)'2', (byte)'3', (byte)'4', 0x00, 0x00,
        ];
        return [0x04, 0x0F, (byte)data.Length, .. data];
    }

    private static IEnumerable<byte[]> Script(byte[] packet, Func<byte[], IEnumerable<byte[]>> command) =>
        packet[0] == 0x06 && packet[1] == 0x00 ? [Completion] : command(packet);

    [Fact]
    public void Betraege_als_BCD()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x89, 0x50 }, ZvtClient.Bcd(8950, 6));
        Assert.Equal(new byte[] { 0x09, 0x78 }, ZvtClient.Bcd(978, 2));
    }

    [Fact]
    public async Task Zahlung_genehmigt_mit_Kartendaten()
    {
        await using var terminal = new FakeTerminal(p => Script(p, _ => [[0x04, 0xFF, 0x01, 0x0A], Status(), Completion]));
        var result = await new ZvtClient(terminal.Address).PayAsync(89.50m, CancellationToken.None);

        Assert.True(result.Approved);
        Assert.Equal("VISA", result.CardBrand);
        Assert.Equal("•••• 1234", result.MaskedPan);
        Assert.Equal("AB1234", result.AuthorizationCode);
        Assert.Equal("42", result.Reference);

        Assert.Equal(new byte[] { 0x06, 0x00, 0x06, 0x00, 0x00, 0x00, 0x88, 0x09, 0x78 }, terminal.Received[0]);
        Assert.Equal(new byte[] { 0x06, 0x01, 0x0A, 0x04, 0x00, 0x00, 0x00, 0x00, 0x89, 0x50, 0x49, 0x09, 0x78 }, terminal.Received[1]);
    }

    [Fact]
    public async Task Abgelehnt_mit_Grund()
    {
        await using var terminal = new FakeTerminal(p => Script(p, _ => [Status(0xB5), Completion]));
        var result = await new ZvtClient(terminal.Address).PayAsync(20m, CancellationToken.None);
        Assert.False(result.Approved);
        Assert.Equal("Falsche PIN", result.Message);
    }

    [Fact]
    public async Task Abbruch_am_Terminal()
    {
        await using var terminal = new FakeTerminal(p => Script(p, _ => [[0x06, 0x1E, 0x01, 0x6C]]));
        var result = await new ZvtClient(terminal.Address).PayAsync(20m, CancellationToken.None);
        Assert.False(result.Approved);
        Assert.Contains("Abbruch", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Storno_mit_Belegnummer_des_Terminals()
    {
        await using var terminal = new FakeTerminal(p => Script(p, _ => [Status(), Completion]));
        var result = await new ZvtClient(terminal.Address).ReverseAsync("42", CancellationToken.None);
        Assert.True(result.Approved);
        Assert.Equal("Am Terminal storniert.", result.Message);
        Assert.Equal(new byte[] { 0x06, 0x30, 0x06, 0x00, 0x00, 0x00, 0x87, 0x00, 0x42 }, terminal.Received[1]);

        var missing = await new ZvtClient(terminal.Address).ReverseAsync(null, CancellationToken.None);
        Assert.False(missing.Approved);
        Assert.Equal(2, terminal.Received.Count); // ohne Belegnummer geht nichts ans Gerät
    }

    [Fact]
    public async Task Gutschrift_mit_Passwort_Betrag_und_Waehrung()
    {
        await using var terminal = new FakeTerminal(p => Script(p, _ => [Status(), Completion]));
        var result = await new ZvtClient(terminal.Address).RefundAsync(89.5m, CancellationToken.None);
        Assert.True(result.Approved);
        Assert.Equal("Am Terminal gutgeschrieben.", result.Message);
        Assert.Equal(new byte[] { 0x06, 0x31, 0x0D, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x89, 0x50, 0x49, 0x09, 0x78 }, terminal.Received[1]);
    }

    [Fact]
    public async Task Nicht_erreichbar_heisst_nichts_geschickt()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await Assert.ThrowsAsync<TerminalUnreachableException>(() => new ZvtClient($"127.0.0.1:{port}").PayAsync(10m, CancellationToken.None));
    }

    private sealed class FakeDriver(Func<CardTerminalResult> pay) : ICardTerminalDriver
    {
        public List<string> Calls { get; } = [];

        public Task<CardTerminalResult> PayAsync(decimal amount, CancellationToken ct)
        {
            Calls.Add($"pay {amount}");
            return Task.FromResult(pay());
        }

        public Task<CardTerminalResult> ReverseAsync(string? reference, CancellationToken ct)
        {
            Calls.Add($"reverse {reference}");
            return Task.FromResult(new CardTerminalResult(true, "Am Terminal storniert.", null, null, null, reference));
        }

        public Task<CardTerminalResult> RefundAsync(decimal amount, CancellationToken ct)
        {
            Calls.Add($"refund {amount}");
            return Task.FromResult(new CardTerminalResult(true, "Am Terminal gutgeschrieben.", null, null, null, null));
        }
    }

    private static readonly Guid TerminalId = Guid.NewGuid();

    private static CardPaymentDto Job(CardPaymentStatus status, string? reference = null) =>
        new(Guid.NewGuid(), SpaSample.BillId, TerminalId, 89m, "EUR", status, null, null, null, null, reference, null, SpaSample.At(11, 40), null);

    [Fact]
    public async Task Auftraege_gehen_nur_einmal_ans_Geraet_und_werden_gemeldet()
    {
        var pay = Job(CardPaymentStatus.Pending);
        var reverse = Job(CardPaymentStatus.ReversalPending, "42");
        var refund = Job(CardPaymentStatus.RefundPending, "17");
        var stub = new StubApi()
            .On($"GET /api/pos/terminals/{TerminalId}/jobs", new List<CardPaymentDto> { pay, reverse, refund })
            .On($"POST /api/pos/card-payments/{pay.Id}/result", pay with { Status = CardPaymentStatus.Approved })
            .On($"POST /api/pos/card-payments/{reverse.Id}/result", reverse with { Status = CardPaymentStatus.Reversed })
            .On($"POST /api/pos/card-payments/{refund.Id}/result", refund with { Status = CardPaymentStatus.Refunded });
        var driver = new FakeDriver(() => new(true, null, "VISA", "•••• 1234", "AB1234", "43"));
        await using var agent = new TerminalAgent(stub.Client(), TerminalId, driver);

        await agent.PollOnceAsync(CancellationToken.None);
        await agent.PollOnceAsync(CancellationToken.None);

        Assert.Equal(["pay 89", "reverse 42", "refund 89"], driver.Calls);
        var reported = stub.Calls.Single(c => c.Route == $"POST /api/pos/card-payments/{pay.Id}/result").Body!;
        Assert.Contains("\"approved\":true", reported, StringComparison.Ordinal);
        Assert.Contains("AB1234", reported, StringComparison.Ordinal);
        Assert.Equal("Gutschrift 89,00 € gutgeschrieben.", agent.Status);
        Assert.False(agent.NeedsAttention);
    }

    [Fact]
    public async Task Ungewisser_Ausgang_wird_nicht_automatisch_gemeldet()
    {
        var pay = Job(CardPaymentStatus.Pending);
        var stub = new StubApi().On($"GET /api/pos/terminals/{TerminalId}/jobs", new List<CardPaymentDto> { pay });
        await using var agent = new TerminalAgent(stub.Client(), TerminalId, new FakeDriver(() => throw new IOException("Verbindung weg")));

        await agent.PollOnceAsync(CancellationToken.None);

        Assert.True(agent.NeedsAttention);
        Assert.Contains("letzten Beleg prüfen", agent.Status, StringComparison.Ordinal);
        Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/result", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Terminal_nicht_erreichbar_wird_als_abgelehnt_gemeldet()
    {
        var pay = Job(CardPaymentStatus.Pending);
        var stub = new StubApi()
            .On($"GET /api/pos/terminals/{TerminalId}/jobs", new List<CardPaymentDto> { pay })
            .On($"POST /api/pos/card-payments/{pay.Id}/result", pay with { Status = CardPaymentStatus.Declined });
        await using var agent = new TerminalAgent(stub.Client(), TerminalId,
            new FakeDriver(() => throw new TerminalUnreachableException("Terminal unter 10.0.0.9 nicht erreichbar.")));

        await agent.PollOnceAsync(CancellationToken.None);

        var reported = stub.Calls.Single(c => c.Route.EndsWith("/result", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"approved\":false", reported, StringComparison.Ordinal);
        Assert.Contains("nicht erreichbar", reported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leitung_legt_ein_OPI_Terminal_an_und_entfernt_es_wieder()
    {
        var created = new PaymentTerminalDto(Guid.NewGuid(), TestData.LocationId, "Empfang", TerminalProvider.Local, "192.168.1.60:20002", true,
            TerminalProtocol.Opi);
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new List<PaymentTerminalDto>())
            .On("POST /api/pos/terminals", created)
            .On($"DELETE /api/pos/terminals/{created.Id}", null, HttpStatusCode.NoContent);
        var settings = new MemorySettingsStore();
        await using var devices = new DevicesViewModel(stub.Client(), settings) { CanManageTerminals = true };
        await devices.InitializeAsync();

        var canAddEmpty = devices.AddTerminalCommand.CanExecute(null);
        devices.NewTerminalProtocol = devices.Protocols.Single(p => p.Protocol == TerminalProtocol.Opi);
        devices.NewTerminalAddress = "192.168.1.60:20002";
        await devices.AddTerminalCommand.ExecuteAsync(null);
        var body = stub.Calls.Single(c => c.Route == "POST /api/pos/terminals").Body!;
        var selected = devices.Terminal;
        var canRemove = devices.RemoveTerminalCommand.CanExecute(null);
        await devices.SaveCommand.ExecuteAsync(null);
        var agent = devices.HasAgent;

        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        devices.Dialogs = dialogs;
        await devices.AskRemoveTerminalCommand.ExecuteAsync(null);
        Assert.DoesNotContain(stub.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Equal("Terminal entfernen?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        await devices.AskRemoveTerminalCommand.ExecuteAsync(null);

        Assert.False(canAddEmpty);
        Assert.Contains("\"protocol\":\"Opi\"", body, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"Local\"", body, StringComparison.Ordinal);
        Assert.Equal("Empfang · Wien · OPI 192.168.1.60:20002", selected?.Title);
        Assert.True(canRemove);
        Assert.True(agent);
        Assert.Single(devices.Terminals);
        Assert.False(devices.HasAgent);
        Assert.Null(settings.Load().TerminalId);
        Assert.Equal("„Empfang“ entfernt.", devices.TerminalText);
    }

    [Fact]
    public async Task Leitung_widerruft_eine_Anzeige_erst_nach_der_Rueckfrage()
    {
        var display = new DisplayDeviceDto(Guid.NewGuid(), "Pausenraum", TestData.LocationId, DateTimeOffset.UtcNow, null);
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new List<PaymentTerminalDto>())
            .On($"GET /api/locations/{TestData.LocationId}/displays", new List<DisplayDeviceDto> { display })
            .On($"DELETE /api/displays/{display.Id}", null, HttpStatusCode.NoContent);
        await using var devices = new DevicesViewModel(stub.Client(), new MemorySettingsStore()) { CanManageTerminals = true };
        await devices.InitializeAsync();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        devices.Dialogs = dialogs;

        var listed = devices.Displays.ToList();
        await devices.AskRevokeDisplayCommand.ExecuteAsync(display);
        var afterCancel = stub.Calls.Count(c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        await devices.AskRevokeDisplayCommand.ExecuteAsync(display);

        Assert.Equal([display], listed);
        Assert.Equal(0, afterCancel);
        Assert.Equal("Anzeige widerrufen?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        Assert.Empty(devices.Displays);
        Assert.False(devices.HasDisplays);
        Assert.Equal("„Pausenraum“ widerrufen.", devices.DisplayText);
    }

    [Fact]
    public async Task Ohne_Recht_Standort_verwalten_fragt_die_Seite_keine_Anzeigen_ab()
    {
        var stub = new StubApi().On("GET /api/locations", new List<LocationDto> { TestData.Location });
        await using var devices = new DevicesViewModel(stub.Client(), new MemorySettingsStore());
        await devices.InitializeAsync();
        devices.DisplayCode = "K7QM-2XRT";

        Assert.False(devices.PairDisplayCommand.CanExecute(null));
        Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/displays", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ist_das_Modul_Dashboard_aus_fehlt_der_Abschnitt_Anzeigen_und_die_Seite_fragt_sie_nicht_ab()
    {
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new List<PaymentTerminalDto>());
        await using var devices = new DevicesViewModel(stub.Client(), new MemorySettingsStore()) { CanManageTerminals = true, DashboardOn = false };
        await devices.InitializeAsync();
        devices.DisplayCode = "K7QM-2XRT";

        Assert.False(devices.CanManageDisplays);
        Assert.False(devices.PairDisplayCommand.CanExecute(null));
        Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/displays", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ohne_Recht_kein_Terminal_anlegen()
    {
        var stub = new StubApi().On("GET /api/locations", new List<LocationDto> { TestData.Location });
        await using var devices = new DevicesViewModel(stub.Client(), new MemorySettingsStore());
        await devices.InitializeAsync();
        devices.NewTerminalAddress = "192.168.1.60";
        Assert.False(devices.AddTerminalCommand.CanExecute(null));
    }
}

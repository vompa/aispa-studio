using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.Services.Opi;
using AispaStudio.Desktop.Services.Zvt;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kartenterminal per OPI gegen ein nachgebautes Terminal im selben Prozess.</summary>
public sealed class OpiTests
{
    /// <summary>Beantwortet jede Anfrage mit dem Drehbuch und schreibt mit, was die Kasse schickt.</summary>
    private sealed class FakeOpiTerminal : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<XElement, IEnumerable<XElement>> _answer;
        private readonly Task _loop;

        public FakeOpiTerminal(Func<XElement, IEnumerable<XElement>> answer)
        {
            _answer = answer;
            _listener.Start();
            _loop = Task.Run(ServeAsync);
        }

        public List<XElement> Received { get; } = [];

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
                            var header = new byte[4];
                            await stream.ReadExactlyAsync(header);
                            var body = new byte[BinaryPrimitives.ReadInt32BigEndian(header)];
                            await stream.ReadExactlyAsync(body);
                            var request = XDocument.Parse(Encoding.UTF8.GetString(body)).Root!;
                            lock (Received)
                            {
                                Received.Add(request);
                            }

                            foreach (var reply in _answer(request))
                            {
                                var bytes = Encoding.UTF8.GetBytes(reply.ToString(SaveOptions.DisableFormatting));
                                var length = new byte[4];
                                BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
                                await stream.WriteAsync(length);
                                await stream.WriteAsync(bytes);
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

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop;
            _listener.Dispose();
        }
    }

    private static XElement Reply(XElement request, string result, params object[] content) =>
        new(request.Name.LocalName.Replace("Request", "Response", StringComparison.Ordinal),
            new XAttribute("RequestType", request.Attribute("RequestType")!.Value),
            new XAttribute("WorkstationID", request.Attribute("WorkstationID")!.Value),
            new XAttribute("RequestID", request.Attribute("RequestID")!.Value),
            new XAttribute("OverallResult", result),
            content);

    private static readonly object[] Approved =
    [
        new XElement("Terminal", new XAttribute("TerminalID", "60012345"), new XAttribute("STAN", "000815")),
        new XElement("Tender",
            new XElement("TotalAmount", new XAttribute("Currency", "CHF"), "89.50"),
            new XElement("Authorisation", new XAttribute("CardCircuit", "VISA"), new XAttribute("CardPAN", "476173******1234"),
                new XAttribute("ApprovalCode", "AB1234"))),
    ];

    /// <summary>Anmeldung immer erfolgreich, die Kartenanfrage nach Wunsch.</summary>
    private static Func<XElement, IEnumerable<XElement>> Script(Func<XElement, IEnumerable<XElement>> card) =>
        request => request.Name.LocalName == "ServiceRequest" ? [Reply(request, "Success")] : card(request);

    private static readonly OpiOptions Swiss = new(Currency: "CHF", Now: () => new DateTimeOffset(2026, 10, 19, 10, 30, 0, TimeSpan.FromHours(2)));

    [Fact]
    public async Task Zahlung_genehmigt_mit_Kartendaten()
    {
        await using var terminal = new FakeOpiTerminal(Script(r => [Reply(r, "Success", Approved)]));
        var result = await new OpiClient(terminal.Address, Swiss).PayAsync(89.5m, CancellationToken.None);

        Assert.Equal(new CardTerminalResult(true, null, "VISA", "•••• 1234", "AB1234", "000815"), result);
        var login = terminal.Received[0];
        var payment = terminal.Received[1];
        Assert.Equal(("ServiceRequest", "Login"), (login.Name.LocalName, login.Attribute("RequestType")!.Value));
        Assert.Equal(("CardServiceRequest", "CardPayment"), (payment.Name.LocalName, payment.Attribute("RequestType")!.Value));
        Assert.Equal("AISPA", payment.Attribute("WorkstationID")!.Value);
        Assert.NotEqual(login.Attribute("RequestID")!.Value, payment.Attribute("RequestID")!.Value);
        Assert.Equal("2026-10-19T10:30:00", payment.Element("POSdata")!.Element("POSTimeStamp")!.Value);
        Assert.Equal(("CHF", "89.50"), (payment.Element("TotalAmount")!.Attribute("Currency")!.Value, payment.Element("TotalAmount")!.Value));
    }

    [Fact]
    public async Task Abgelehnt_und_Abbruch_mit_Grund()
    {
        await using var declined = new FakeOpiTerminal(Script(r => [Reply(r, "Failure")]));
        await using var aborted = new FakeOpiTerminal(Script(r => [Reply(r, "Aborted")]));

        var no = await new OpiClient(declined.Address).PayAsync(20m, CancellationToken.None);
        var stop = await new OpiClient(aborted.Address).PayAsync(20m, CancellationToken.None);

        Assert.Equal((false, "Abgelehnt"), (no.Approved, no.Message));
        Assert.Equal((false, "Abbruch am Terminal"), (stop.Approved, stop.Message));
    }

    [Fact]
    public async Task Abgelehnte_Anmeldung_startet_keine_Zahlung()
    {
        await using var terminal = new FakeOpiTerminal(r => [Reply(r, "LoggedOut")]);
        var result = await new OpiClient(terminal.Address).PayAsync(20m, CancellationToken.None);

        Assert.False(result.Approved);
        Assert.Equal("Anmeldung am Terminal abgelehnt (Terminal ist abgemeldet).", result.Message);
        Assert.Single(terminal.Received);
    }

    [Fact]
    public async Task Fremde_Nachrichten_dazwischen_werden_uebergangen()
    {
        await using var terminal = new FakeOpiTerminal(Script(r =>
        [
            new XElement("CardServiceResponse", new XAttribute("RequestID", "999"), new XAttribute("OverallResult", "Failure")),
            Reply(r, "Success", Approved),
        ]));
        var result = await new OpiClient(terminal.Address).PayAsync(10m, CancellationToken.None);
        Assert.True(result.Approved);
    }

    [Fact]
    public async Task Storno_mit_STAN_und_Gutschrift_mit_Betrag()
    {
        await using var terminal = new FakeOpiTerminal(Script(r => [Reply(r, "Success")]));
        var client = new OpiClient(terminal.Address);

        var reversed = await client.ReverseAsync("000815", CancellationToken.None);
        var refunded = await client.RefundAsync(89.5m, CancellationToken.None);
        var missing = await client.ReverseAsync(null, CancellationToken.None);

        Assert.Equal((true, "Am Terminal storniert."), (reversed.Approved, reversed.Message));
        Assert.Equal((true, "Am Terminal gutgeschrieben."), (refunded.Approved, refunded.Message));
        Assert.False(missing.Approved);
        Assert.Equal(4, terminal.Received.Count); // ohne STAN geht nichts ans Gerät
        Assert.Equal("PaymentReversal", terminal.Received[1].Attribute("RequestType")!.Value);
        Assert.Equal("000815", terminal.Received[1].Element("OriginalTransaction")!.Attribute("STAN")!.Value);
        Assert.Null(terminal.Received[1].Element("TotalAmount"));
        Assert.Equal("PaymentRefund", terminal.Received[3].Attribute("RequestType")!.Value);
        Assert.Equal("89.50", terminal.Received[3].Element("TotalAmount")!.Value);
    }

    [Fact]
    public async Task Kein_XML_vom_Terminal_ist_ein_Abbruch_mit_offenem_Ausgang()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ct = TestContext.Current.CancellationToken;
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            var stream = client.GetStream();
            var header = new byte[4];
            await stream.ReadExactlyAsync(header, ct);
            await stream.ReadExactlyAsync(new byte[BinaryPrimitives.ReadInt32BigEndian(header)], ct);
            await stream.WriteAsync(new byte[] { 0, 0, 0, 3, (byte)'a', (byte)'b', (byte)'c' }, ct);
        }, ct);

        await Assert.ThrowsAsync<OpiException>(() => new OpiClient($"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}").PayAsync(10m, CancellationToken.None));
        await serve;
        listener.Stop();
    }

    [Fact]
    public async Task Nicht_erreichbar_heisst_nichts_geschickt()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await Assert.ThrowsAsync<TerminalUnreachableException>(() => new OpiClient($"127.0.0.1:{port}").PayAsync(10m, CancellationToken.None));
    }

    [Fact]
    public void Treiber_nach_Protokoll_und_Waehrung_des_Standorts()
    {
        var terminal = new PaymentTerminalDto(Guid.NewGuid(), Guid.NewGuid(), "Empfang", TerminalProvider.Local, "192.168.1.50", true);

        Assert.IsType<ZvtClient>(CardTerminalDrivers.Create(terminal, "EUR"));
        Assert.IsType<OpiClient>(CardTerminalDrivers.Create(terminal with { Protocol = TerminalProtocol.Opi }, "CHF"));
        Assert.Equal("OPI", CardTerminalDrivers.ProtocolName(TerminalProtocol.Opi));
    }
}

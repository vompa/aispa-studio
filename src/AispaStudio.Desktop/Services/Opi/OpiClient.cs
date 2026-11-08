using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services.Opi;

/// <summary>
/// Kartenterminal im Netz am Empfang nach OPI (Open Payment Initiative, IFSF POS-EPS), wie es Worldline und viele Schweizer Geräte anbieten.
/// Unterstützt das, was die Kassa braucht: Anmelden (Login), Zahlung (CardPayment), Storno (PaymentReversal) und Gutschrift (PaymentRefund).
/// Die Belege druckt das Terminal selbst.
/// </summary>
/// <remarks>
/// Jede Nachricht ist ein XML-Dokument mit vorangestellter Länge (4 Byte, höchstwertiges zuerst). Die Kasse fragt auf Kanal 0 und bekommt
/// die Antwort auf derselben Verbindung. Ausgaben des Terminals an Drucker oder Anzeige der Kasse (Kanal 1) nutzt die App nicht.
/// </remarks>
public sealed class OpiClient(string address, OpiOptions? options = null) : ICardTerminalDriver
{
    private readonly OpiOptions _options = options ?? new OpiOptions();
    private int _requestId;

    public Task<CardTerminalResult> PayAsync(decimal amount, CancellationToken ct) =>
        RunAsync("CardPayment", amount, null, done: null, ct);

    /// <summary>Gutschrift auf die Karte, die der Gast vorlegt; geht auch nach dem Tagesabschluss des Terminals.</summary>
    public Task<CardTerminalResult> RefundAsync(decimal amount, CancellationToken ct) =>
        RunAsync("PaymentRefund", amount, null, done: "Am Terminal gutgeschrieben.", ct);

    /// <param name="reference">STAN der genehmigten Zahlung, wie sie das Terminal gemeldet hat.</param>
    public async Task<CardTerminalResult> ReverseAsync(string? reference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new(false, "Für das Storno fehlt die Belegnummer des Terminals; bitte direkt am Terminal stornieren.", null, null, null, null);
        }

        return await RunAsync("PaymentReversal", null, reference, done: "Am Terminal storniert.", ct).ConfigureAwait(false);
    }

    private async Task<CardTerminalResult> RunAsync(string requestType, decimal? amount, string? originalStan, string? done, CancellationToken ct)
    {
        await using var connection = await OpiConnection.OpenAsync(address, _options, ct).ConfigureAwait(false);

        var login = await connection.ExchangeAsync(Login(NextId()), ct).ConfigureAwait(false);
        if (Result(login) is not "Success" and var loginResult)
        {
            // Ohne Anmeldung startet das Terminal keine Zahlung; es ist also sicher nichts abgebucht.
            return new(false, $"Anmeldung am Terminal abgelehnt ({ResultText(loginResult)}).", null, null, null, null);
        }

        var response = await connection.ExchangeAsync(CardService(requestType, NextId(), amount, originalStan), ct).ConfigureAwait(false);
        var result = Result(response);
        if (result != "Success")
        {
            return new(false, ResultText(result), null, null, null, null);
        }

        var authorisation = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "Authorisation");
        var terminal = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "Terminal");
        return new(true, done, Attribute(authorisation, "CardCircuit"), Masked(Attribute(authorisation, "CardPAN")),
            Attribute(authorisation, "ApprovalCode"), Attribute(terminal, "STAN"));
    }

    private string NextId() => Interlocked.Increment(ref _requestId).ToString(CultureInfo.InvariantCulture);

    private XDocument Login(string requestId) => new(new XDeclaration("1.0", "utf-8", null),
        new XElement("ServiceRequest",
            new XAttribute("RequestType", "Login"),
            new XAttribute("ApplicationSender", _options.ApplicationSender),
            new XAttribute("WorkstationID", _options.WorkstationId),
            new XAttribute("RequestID", requestId),
            PosData()));

    private XDocument CardService(string requestType, string requestId, decimal? amount, string? originalStan) => new(
        new XDeclaration("1.0", "utf-8", null),
        new XElement("CardServiceRequest",
            new XAttribute("RequestType", requestType),
            new XAttribute("ApplicationSender", _options.ApplicationSender),
            new XAttribute("WorkstationID", _options.WorkstationId),
            new XAttribute("RequestID", requestId),
            PosData(),
            originalStan is null ? null : new XElement("OriginalTransaction", new XAttribute("STAN", originalStan)),
            amount is { } value
                ? new XElement("TotalAmount", new XAttribute("Currency", _options.Currency), value.ToString("0.00", CultureInfo.InvariantCulture))
                : null));

    private XElement PosData() => new("POSdata",
        new XAttribute("LanguageCode", "de"),
        new XElement("POSTimeStamp", (_options.Now?.Invoke() ?? DateTimeOffset.Now).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));

    private static string? Result(XDocument response) => response.Root?.Attribute("OverallResult")?.Value;

    private static string? Attribute(XElement? element, string name) => element?.Attribute(name)?.Value is { Length: > 0 } value ? value : null;

    /// <summary>Das Terminal liefert die PAN meist schon maskiert; gezeigt werden nur die letzten vier Ziffern.</summary>
    private static string? Masked(string? pan)
    {
        var digits = pan is null ? "" : new string(pan.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 4 ? $"•••• {digits[^4..]}" : null;
    }

    /// <summary>Übliche Werte von <c>OverallResult</c> auf Deutsch.</summary>
    public static string ResultText(string? result) => result switch
    {
        "Failure" => "Abgelehnt",
        "Aborted" => "Abbruch am Terminal",
        "TimedOut" => "Zeitüberschreitung am Terminal",
        "Busy" => "Terminal ist beschäftigt",
        "DeviceUnavailable" => "Kartenleser nicht bereit",
        "LoggedOut" => "Terminal ist abgemeldet",
        "PrintLastTicket" => "Bitte den letzten Terminalbeleg prüfen",
        "FormatError" or "ParsingError" or "ValidationError" or "MissingMandatoryData" => $"Terminal versteht die Anfrage nicht ({result})",
        null or "" => "Keine Angabe vom Terminal",
        _ => $"Terminal meldet {result}",
    };
}

/// <param name="WorkstationId">Kennung der Kasse, wie sie am Terminal eingetragen ist.</param>
/// <param name="Currency">ISO-Code der Währung am Standort, z. B. EUR oder CHF.</param>
/// <param name="Now">Uhr für den Zeitstempel der Kasse (für Tests).</param>
public sealed record OpiOptions(
    string WorkstationId = "AISPA",
    string Currency = "EUR",
    string ApplicationSender = "AISPA Studio",
    int TimeoutSeconds = 180,
    Func<DateTimeOffset>? Now = null);

/// <summary>Das Terminal hat etwas geschickt, das kein OPI ist; ob die Zahlung lief, ist offen.</summary>
public sealed class OpiException(string message) : Exception(message);

internal sealed class OpiConnection : IAsyncDisposable
{
    private const int MaxMessage = 1 << 20;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _timeout;

    private OpiConnection(TcpClient client, TimeSpan timeout, CancellationToken ct)
    {
        _client = client;
        _stream = client.GetStream();
        _timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _timeout.CancelAfter(timeout);
    }

    public static async Task<OpiConnection> OpenAsync(string address, OpiOptions options, CancellationToken ct)
    {
        var (host, port) = NetworkAddress.Parse(address, 20002);
        var client = new TcpClient();
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connect.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(host, port, connect.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            client.Dispose();
            throw new TerminalUnreachableException($"Terminal unter {address} nicht erreichbar.");
        }

        return new OpiConnection(client, TimeSpan.FromSeconds(options.TimeoutSeconds), ct);
    }

    /// <summary>Anfrage schicken und auf die Antwort mit derselben RequestID warten; andere Nachrichten werden übergangen.</summary>
    public async Task<XDocument> ExchangeAsync(XDocument request, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _timeout.Token);
        var body = Encoding.UTF8.GetBytes(request.Declaration + request.ToString(SaveOptions.DisableFormatting));
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, body.Length);
        await _stream.WriteAsync(header, linked.Token).ConfigureAwait(false);
        await _stream.WriteAsync(body, linked.Token).ConfigureAwait(false);

        var requestId = request.Root!.Attribute("RequestID")!.Value;
        while (true)
        {
            var response = await ReceiveAsync(linked.Token).ConfigureAwait(false);
            if (response.Root?.Attribute("RequestID")?.Value == requestId)
            {
                return response;
            }
        }
    }

    private async Task<XDocument> ReceiveAsync(CancellationToken ct)
    {
        var header = new byte[4];
        await _stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is <= 0 or > MaxMessage)
        {
            throw new OpiException("Unerwartete Antwort vom Terminal.");
        }

        var body = new byte[length];
        await _stream.ReadExactlyAsync(body, ct).ConfigureAwait(false);
        try
        {
            using var stream = new MemoryStream(body);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new OpiException("Unerwartete Antwort vom Terminal.");
        }
    }

    public ValueTask DisposeAsync()
    {
        _timeout.Dispose();
        _stream.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

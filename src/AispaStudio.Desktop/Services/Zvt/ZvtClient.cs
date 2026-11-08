using System.Globalization;
using System.Net.Sockets;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services.Zvt;

/// <summary>
/// Kartenterminal im Netz am Empfang nach dem ZVT-Protokoll (ZVT 700, TCP). Unterstützt das, was die Kassa braucht:
/// Anmelden (06 00), Zahlung (06 01), Storno (06 30) und Gutschrift (06 31). Die Belege druckt das Terminal selbst.
/// </summary>
/// <remarks>
/// Ein ZVT-Paket (APDU) ist Klasse, Befehl, Länge und Daten. Die Daten bestehen aus Feldern (BMP) mit einer Kennung und
/// fester oder vorangestellter Länge. Jede Nachricht des Terminals wird mit 80 00 00 bestätigt.
/// </remarks>
public sealed class ZvtClient(string address, ZvtOptions? options = null) : ICardTerminalDriver
{
    private static readonly byte[] Ack = [0x80, 0x00, 0x00];

    private readonly ZvtOptions _options = options ?? new ZvtOptions();

    public async Task<CardTerminalResult> PayAsync(decimal amount, CancellationToken ct)
    {
        await using var connection = await ZvtConnection.OpenAsync(address, _options, ct).ConfigureAwait(false);
        await RegisterAsync(connection, ct).ConfigureAwait(false);

        List<byte> data = [0x04, .. Amount(amount), 0x49, .. Bcd(_options.CurrencyCode, 2)];
        return await RunAsync(connection, [0x06, 0x01], data, done: null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gutschrift (06 31): Passwort, Betrag und Währung. Anders als das Storno geht sie auch nach dem Kassenschnitt des Terminals;
    /// der Gast legt dazu die Karte erneut vor.
    /// </summary>
    public async Task<CardTerminalResult> RefundAsync(decimal amount, CancellationToken ct)
    {
        await using var connection = await ZvtConnection.OpenAsync(address, _options, ct).ConfigureAwait(false);
        await RegisterAsync(connection, ct).ConfigureAwait(false);
        List<byte> data = [.. Bcd(_options.Password, 3), 0x04, .. Amount(amount), 0x49, .. Bcd(_options.CurrencyCode, 2)];
        return await RunAsync(connection, [0x06, 0x31], data, done: "Am Terminal gutgeschrieben.", ct).ConfigureAwait(false);
    }

    private static byte[] Amount(decimal amount) => Bcd(checked((long)Math.Round(amount * 100, MidpointRounding.AwayFromZero)), 6);

    public async Task<CardTerminalResult> ReverseAsync(string? reference, CancellationToken ct)
    {
        if (!int.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out var receiptNumber))
        {
            return new(false, "Für das Storno fehlt die Belegnummer des Terminals; bitte direkt am Terminal stornieren.", null, null, null, null);
        }

        await using var connection = await ZvtConnection.OpenAsync(address, _options, ct).ConfigureAwait(false);
        await RegisterAsync(connection, ct).ConfigureAwait(false);
        List<byte> data = [.. Bcd(_options.Password, 3), 0x87, .. Bcd(receiptNumber, 2)];
        return await RunAsync(connection, [0x06, 0x30], data, done: "Am Terminal storniert.", ct).ConfigureAwait(false);
    }

    /// <summary>Anmeldung: Passwort, Konfigurationsbyte und Währung. Das Terminal antwortet mit 80 00 00 und 06 0F.</summary>
    private async Task RegisterAsync(ZvtConnection connection, CancellationToken ct)
    {
        List<byte> data = [.. Bcd(_options.Password, 3), _options.ConfigByte, .. Bcd(_options.CurrencyCode, 2)];
        await connection.SendAsync([0x06, 0x00], data, ct).ConfigureAwait(false);
        await ExpectAckAsync(connection, ct).ConfigureAwait(false);
        while (true)
        {
            var message = await connection.ReceiveAsync(ct).ConfigureAwait(false);
            await connection.SendRawAsync(Ack, ct).ConfigureAwait(false);
            if (message.Is(0x06, 0x0F))
            {
                return;
            }

            if (message.Is(0x06, 0x1E))
            {
                throw new ZvtException($"Anmeldung am Terminal abgelehnt ({ResultText(message.Data.FirstOrDefault())}).");
            }
        }
    }

    /// <summary>Befehl senden, Zwischenstände bestätigen, Statusinformation (04 0F) auswerten, bis Abschluss (06 0F) oder Abbruch (06 1E).</summary>
    private static async Task<CardTerminalResult> RunAsync(ZvtConnection connection, byte[] command, List<byte> data, string? done, CancellationToken ct)
    {
        await connection.SendAsync(command, data, ct).ConfigureAwait(false);
        await ExpectAckAsync(connection, ct).ConfigureAwait(false);

        var fields = new ZvtFields();
        while (true)
        {
            var message = await connection.ReceiveAsync(ct).ConfigureAwait(false);
            await connection.SendRawAsync(Ack, ct).ConfigureAwait(false);
            if (message.Is(0x04, 0x0F))
            {
                fields = ZvtFields.Parse(message.Data);
            }
            else if (message.Is(0x06, 0x0F))
            {
                var approved = fields.ResultCode is null or 0;
                var text = approved ? done : ResultText(fields.ResultCode!.Value);
                return new(approved, text, fields.CardName, fields.MaskedPan, fields.AuthorizationCode,
                    fields.ReceiptNumber?.ToString(CultureInfo.InvariantCulture));
            }
            else if (message.Is(0x06, 0x1E))
            {
                return new(false, ResultText(message.Data.FirstOrDefault()), null, null, null, null);
            }

            // 04 FF (Zwischenstand, z. B. „Karte einstecken“) und Druckzeilen (06 D1, 06 D3) nur bestätigen.
        }
    }

    private static async Task ExpectAckAsync(ZvtConnection connection, CancellationToken ct)
    {
        var reply = await connection.ReceiveAsync(ct).ConfigureAwait(false);
        if (reply.Is(0x84, null) && reply.Instruction != 0x00)
        {
            throw new ZvtException($"Terminal lehnt den Befehl ab ({ResultText(reply.Instruction)}).");
        }

        if (!reply.Is(0x80, 0x00) && !reply.Is(0x84, 0x00))
        {
            throw new ZvtException("Unerwartete Antwort vom Terminal.");
        }
    }

    /// <summary>Zahl als gepackte BCD-Ziffern fester Länge, z. B. 1250 Cent in 6 Byte = 00 00 00 00 12 50.</summary>
    public static byte[] Bcd(long value, int bytes)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture).PadLeft(bytes * 2, '0');
        if (digits.Length > bytes * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Zu groß für das Feld.");
        }

        var result = new byte[bytes];
        for (var i = 0; i < bytes; i++)
        {
            result[i] = (byte)(((digits[2 * i] - '0') << 4) | (digits[(2 * i) + 1] - '0'));
        }

        return result;
    }

    /// <summary>Häufige Ergebniscodes nach ZVT; alles andere mit Code.</summary>
    public static string ResultText(byte code) => code switch
    {
        0x00 => "Erfolgreich",
        0x64 => "Karte nicht lesbar",
        0x65 => "Kartendaten nicht vorhanden",
        0x66 => "Fehler beim Lesen der Karte",
        0x6C => "Abbruch durch Karteninhaber oder Zeitüberschreitung",
        0x6F => "Falsche Währung",
        0x78 => "Karte gesperrt",
        0x7A => "Karte nicht zugelassen",
        0x7C => "Funktion nicht möglich",
        0x83 => "Storno nicht möglich",
        0x9C => "Terminal belegt, bitte wiederholen",
        0xA0 => "Kein Netz zum Rechenzentrum",
        0xB1 => "Zu wenig Guthaben oder Limit überschritten",
        0xB5 => "Falsche PIN",
        0xFF => "Systemfehler am Terminal",
        _ => $"Abgelehnt, Code {code:X2}",
    };
}

/// <summary>Einstellungen für ZVT; die Voreinstellung passt für die meisten Geräte in Österreich und Deutschland.</summary>
/// <param name="Password">Kassenpasswort des Terminals, meist 000000.</param>
/// <param name="ConfigByte">Anmeldekonfiguration: 0x88 = Zwischenstände an die Kasse, Belege druckt das Terminal.</param>
/// <param name="CurrencyCode">ISO 4217 numerisch: 978 Euro, 756 Franken.</param>
public sealed record ZvtOptions(int Password = 0, byte ConfigByte = 0x88, int CurrencyCode = 978, int TimeoutSeconds = 180);

public sealed class ZvtException(string message) : Exception(message);

/// <summary>Ein empfangenes ZVT-Paket.</summary>
internal sealed record ZvtMessage(byte Class, byte Instruction, byte[] Data)
{
    public bool Is(byte @class, byte? instruction) => Class == @class && (instruction is null || Instruction == instruction);
}

/// <summary>TCP-Verbindung zum Terminal mit Zeitlimit je Vorgang.</summary>
internal sealed class ZvtConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _timeout;

    private ZvtConnection(TcpClient client, TimeSpan timeout, CancellationToken ct)
    {
        _client = client;
        _stream = client.GetStream();
        _timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _timeout.CancelAfter(timeout);
    }

    public static async Task<ZvtConnection> OpenAsync(string address, ZvtOptions options, CancellationToken ct)
    {
        var (host, port) = NetworkAddress.Parse(address, 20007);
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

        return new ZvtConnection(client, TimeSpan.FromSeconds(options.TimeoutSeconds), ct);
    }

    public Task SendAsync(byte[] command, List<byte> data, CancellationToken ct)
    {
        List<byte> packet = [.. command];
        if (data.Count < 0xFF)
        {
            packet.Add((byte)data.Count);
        }
        else
        {
            packet.AddRange([0xFF, (byte)(data.Count & 0xFF), (byte)(data.Count >> 8)]);
        }

        packet.AddRange(data);
        return SendRawAsync([.. packet], ct);
    }

    public async Task SendRawAsync(byte[] bytes, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _timeout.Token);
        await _stream.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
    }

    public async Task<ZvtMessage> ReceiveAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _timeout.Token);
        var header = await ReadAsync(3, linked.Token).ConfigureAwait(false);
        int length = header[2];
        if (length == 0xFF)
        {
            var extended = await ReadAsync(2, linked.Token).ConfigureAwait(false);
            length = extended[0] | (extended[1] << 8);
        }

        return new ZvtMessage(header[0], header[1], await ReadAsync(length, linked.Token).ConfigureAwait(false));
    }

    private async Task<byte[]> ReadAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        await _stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        return buffer;
    }

    public ValueTask DisposeAsync()
    {
        _timeout.Dispose();
        _stream.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Was die Kassa aus der Statusinformation (04 0F) braucht.</summary>
internal sealed record ZvtFields
{
    public byte? ResultCode { get; init; }

    public int? ReceiptNumber { get; init; }

    public int? TraceNumber { get; init; }

    public string? MaskedPan { get; init; }

    public string? CardName { get; init; }

    public string? AuthorizationCode { get; init; }

    // Feste Längen der Felder nach ZVT; 0xFE = LLVAR, 0xFD = LLLVAR, 0xFC = TLV-Container.
    private static readonly Dictionary<byte, int> Lengths = new()
    {
        [0x01] = 1,
        [0x02] = 1,
        [0x03] = 1,
        [0x04] = 6,
        [0x05] = 1,
        [0x06] = 0xFC,
        [0x0B] = 3,
        [0x0C] = 3,
        [0x0D] = 2,
        [0x0E] = 2,
        [0x17] = 2,
        [0x19] = 1,
        [0x22] = 0xFE,
        [0x23] = 0xFE,
        [0x24] = 0xFD,
        [0x27] = 1,
        [0x29] = 4,
        [0x2A] = 15,
        [0x37] = 3,
        [0x3A] = 2,
        [0x3B] = 8,
        [0x3C] = 0xFD,
        [0x3D] = 3,
        [0x49] = 2,
        [0x60] = 0xFD,
        [0x87] = 2,
        [0x88] = 3,
        [0x8A] = 1,
        [0x8B] = 0xFE,
        [0x8C] = 1,
        [0x9A] = 0xFD,
        [0xA0] = 1,
        [0xA7] = 0xFE,
        [0xAA] = 3,
        [0xAF] = 0xFD,
        [0xBA] = 5,
        [0xD0] = 1,
        [0xD1] = 0xFE,
        [0xD2] = 1,
        [0xD3] = 1,
        [0xE0] = 1,
        [0xE1] = 0xFE,
        [0xE2] = 0xFE,
        [0xE3] = 0xFE,
        [0xE4] = 0xFE,
        [0xE5] = 0xFE,
        [0xE6] = 0xFE,
        [0xE7] = 0xFE,
        [0xE8] = 0xFE,
        [0xE9] = 0xFE,
        [0xEA] = 1,
        [0xF0] = 1,
    };

    /// <summary>Liest die Felder bis zum Ende oder bis zu einer unbekannten Kennung (danach lässt sich die Länge nicht bestimmen).</summary>
    public static ZvtFields Parse(byte[] data)
    {
        var fields = new ZvtFields();
        var i = 0;
        while (i < data.Length && Lengths.TryGetValue(data[i], out var kind))
        {
            var id = data[i++];
            int length;
            switch (kind)
            {
                case 0xFE or 0xFD:
                    var digits = kind == 0xFE ? 2 : 3;
                    if (i + digits > data.Length)
                    {
                        return fields;
                    }

                    length = 0;
                    for (var d = 0; d < digits; d++)
                    {
                        length = (length * 10) + (data[i + d] & 0x0F);
                    }

                    i += digits;
                    break;
                case 0xFC:
                    (length, var size) = TlvLength(data, i);
                    i += size;
                    break;
                default:
                    length = kind;
                    break;
            }

            if (length < 0 || i + length > data.Length)
            {
                return fields;
            }

            var value = data.AsSpan(i, length);
            fields = id switch
            {
                0x27 => fields with { ResultCode = value[0] },
                0x87 => fields with { ReceiptNumber = BcdNumber(value) },
                0x0B => fields with { TraceNumber = BcdNumber(value) },
                0x22 => fields with { MaskedPan = Pan(value) },
                0x8B => fields with { CardName = System.Text.Encoding.Latin1.GetString(value).Trim('\0', ' ') },
                0x3B => fields with { AuthorizationCode = System.Text.Encoding.ASCII.GetString(value).Trim('\0', ' ') },
                _ => fields,
            };
            i += length;
        }

        return fields;
    }

    private static (int Length, int Size) TlvLength(byte[] data, int at)
    {
        if (at >= data.Length)
        {
            return (-1, 0);
        }

        var first = data[at];
        return first switch
        {
            < 0x80 => (first, 1),
            0x81 when at + 1 < data.Length => (data[at + 1], 2),
            0x82 when at + 2 < data.Length => ((data[at + 1] << 8) | data[at + 2], 3),
            _ => (-1, 0),
        };
    }

    private static int BcdNumber(ReadOnlySpan<byte> value)
    {
        var number = 0;
        foreach (var b in value)
        {
            number = (number * 100) + ((b >> 4) * 10) + (b & 0x0F);
        }

        return number;
    }

    /// <summary>PAN in BCD, maskierte Stellen als 0xE; ausgegeben wird nur das Ende, z. B. „•••• 1234“.</summary>
    private static string Pan(ReadOnlySpan<byte> value)
    {
        var digits = new System.Text.StringBuilder();
        foreach (var b in value)
        {
            foreach (var nibble in new[] { b >> 4, b & 0x0F })
            {
                digits.Append(nibble switch { <= 9 => (char)('0' + nibble), 0x0F => ' ', _ => '*' });
            }
        }

        var text = digits.ToString().Trim();
        return text.Length > 4 ? $"•••• {text[^4..]}" : text;
    }
}

using System.Globalization;
using System.Net.Sockets;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services.Opi;
using AispaStudio.Desktop.Services.Zvt;

namespace AispaStudio.Desktop.Services;

/// <summary>Kartenterminal am Empfang, angesteuert von dieser App (per ZVT oder OPI).</summary>
public interface ICardTerminalDriver
{
    Task<CardTerminalResult> PayAsync(decimal amount, CancellationToken ct);

    /// <param name="reference">Belegnummer des Terminals aus der genehmigten Zahlung.</param>
    Task<CardTerminalResult> ReverseAsync(string? reference, CancellationToken ct);

    /// <summary>Gutschrift des Betrags auf die Karte, die der Gast am Terminal vorlegt (nach dem Storno eines Belegs).</summary>
    Task<CardTerminalResult> RefundAsync(decimal amount, CancellationToken ct);
}

/// <summary>Wählt den Treiber nach dem Protokoll, das am Terminal eingerichtet ist.</summary>
public static class CardTerminalDrivers
{
    /// <param name="currency">ISO-Code der Währung am Standort des Terminals (EUR oder CHF).</param>
    public static ICardTerminalDriver Create(PaymentTerminalDto terminal, string currency) => terminal.Protocol switch
    {
        TerminalProtocol.Opi => new OpiClient(terminal.Address ?? "", new OpiOptions(Currency: currency)),
        _ => new ZvtClient(terminal.Address ?? "", new ZvtOptions(CurrencyCode: currency == "CHF" ? 756 : 978)),
    };

    public static string ProtocolName(TerminalProtocol protocol) => protocol == TerminalProtocol.Opi ? "OPI" : "ZVT";
}

/// <summary>Das Terminal war nicht erreichbar; es wurde nichts an das Gerät geschickt.</summary>
public sealed class TerminalUnreachableException(string message) : Exception(message);

/// <summary>Bondrucker am Empfang.</summary>
public interface IReceiptPrinter
{
    /// <summary>Schickt fertige ESC/POS-Befehle; gibt eine Fehlermeldung zurück oder null.</summary>
    Task<string?> PrintAsync(byte[] escPos, CancellationToken ct);
}

/// <summary>
/// Bondrucker im Netz (Port 9100, „RAW“), wie ihn die üblichen Geräte von Epson, Star und Bixolon anbieten.
/// So braucht es keinen Windows-Druckertreiber; den Beleg samt QR-Code und Schnitt erzeugt der Server.
/// </summary>
public sealed class NetworkReceiptPrinter(string address) : IReceiptPrinter
{
    public async Task<string?> PrintAsync(byte[] escPos, CancellationToken ct)
    {
        var (host, port) = NetworkAddress.Parse(address, 9100);
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            await stream.WriteAsync(escPos, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            return $"Bondrucker unter {address} nicht erreichbar.";
        }
    }
}

public static class NetworkAddress
{
    /// <summary>„192.168.1.50:20007“ oder nur „192.168.1.50“ (dann der übliche Port).</summary>
    public static (string Host, int Port) Parse(string address, int defaultPort)
    {
        var text = address.Trim();
        var colon = text.LastIndexOf(':');
        return colon > 0 && int.TryParse(text[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536
            ? (text[..colon], port)
            : (text, defaultPort);
    }
}

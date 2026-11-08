using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AispaStudio.Desktop.Services;

/// <summary>Adressen und Verbindungscode eines im Netz freigegebenen Einzelplatzes, zum Abschreiben an den anderen PCs.</summary>
public sealed record NetworkShareInfo(IReadOnlyList<Uri> Addresses, string Code);

/// <summary>
/// Mehrplatz im eigenen Netz (ADR 0041): Ein Einzelplatz gibt seine API zusätzlich per HTTPS im Netz frei, die anderen Windows-Apps
/// verbinden sich wie mit einem Server. Das Zertifikat stellt der PC selbst aus; statt einer Zertifizierungsstelle prüfen die anderen
/// PCs den Verbindungscode, die ersten 20 Stellen des SHA-256-Fingerabdrucks. Damit kann sich niemand im Netz dazwischenschalten.
/// </summary>
public static class NetworkShare
{
    public const int DefaultPort = 55443;

    /// <summary>Hex-Stellen des Fingerabdrucks im Code (80 Bit), in Vierergruppen.</summary>
    private const int CodeLength = 20;

    /// <summary>Selbst ausgestelltes Zertifikat für diesen PC, zehn Jahre gültig; der Name ist nur Beschriftung, geprüft wird der Code.</summary>
    public static X509Certificate2 CreateCertificate(string machineName, DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={machineName}, O=AISPA Studio", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(machineName);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));
    }

    /// <summary>Verbindungscode des Zertifikats, z. B. „3F9A-12C4-77B0-E1D2-9A0B“.</summary>
    public static string Code(X509Certificate certificate) =>
        Group(Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()))[..CodeLength]);

    /// <summary>Der Code in der Schreibweise von <see cref="Code"/>, egal ob mit Leerzeichen, Bindestrichen oder klein; sonst <c>null</c>.</summary>
    public static string? NormalizeCode(string input)
    {
        var digits = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());
        return digits.Length == CodeLength && digits.All(char.IsAsciiHexDigit) ? Group(digits) : null;
    }

    /// <summary>Nur das Zertifikat mit genau diesem Code gilt; Name, Aussteller und Ablauf spielen keine Rolle.</summary>
    public static bool Matches(X509Certificate? certificate, string code) =>
        certificate is not null && string.Equals(Code(certificate), NormalizeCode(code), StringComparison.Ordinal);

    /// <summary>
    /// Adresse aus der Eingabe am anderen PC: „empfang-pc“, „192.168.1.20:55443“ oder vollständig mit https. Ohne Port gilt
    /// <see cref="DefaultPort"/>; unverschlüsselt (http) gibt es im Netz nicht.
    /// </summary>
    public static Uri? NormalizeAddress(string input)
    {
        var text = input.Trim();
        if (text.Length == 0 || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0)
        {
            return null;
        }

        var port = uri.IsDefaultPort && !text.Contains($":{uri.Port}", StringComparison.Ordinal) ? DefaultPort : uri.Port;
        return new Uri($"https://{uri.Host}:{port.ToString(CultureInfo.InvariantCulture)}/");
    }

    /// <summary>Unter diesen Adressen erreichen die anderen PCs diesen: der PC-Name und jede IPv4-Adresse im Netz.</summary>
    public static IReadOnlyList<Uri> Addresses(int port)
    {
        var hosts = new List<string> { Environment.MachineName };
        try
        {
            hosts.AddRange(NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Select(a => a.ToString()));
        }
        catch (NetworkInformationException)
        {
            // Dann eben nur der Name.
        }

        return [.. hosts.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(h => new Uri($"https://{h}:{port.ToString(CultureInfo.InvariantCulture)}/"))];
    }

    private static string Group(string digits) => string.Join('-', digits.Chunk(4).Select(c => new string(c)));
}

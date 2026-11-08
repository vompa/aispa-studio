using System.Text.RegularExpressions;

namespace AispaStudio.Api.MailIntake.Mailboxes;

/// <summary>
/// Liest die Kopfzeile „Authentication-Results“ (RFC 8601), die der empfangende Mailserver nach SPF-, DKIM- und
/// DMARC-Prüfung einträgt. Bestätigt ist ein Absender nur, wenn DMARC bestanden hat oder DKIM bzw. SPF für die
/// Domain im „From“ bestanden haben. Kopfzeilen fremder Server zählen nicht, denn jeder Absender kann sie selbst mitschicken.
/// </summary>
public static partial class MailAuthentication
{
    /// <param name="headers">Alle Authentication-Results-Kopfzeilen, die oberste (zuletzt eingetragene) zuerst.</param>
    /// <param name="fromAddress">Absenderadresse aus „From“.</param>
    /// <param name="trustedServers">
    /// Kennungen (authserv-id) der eigenen Empfangsserver, z. B. „mx.example.at“ oder „spf.protection.outlook.com“.
    /// Leer: nur die oberste Kopfzeile zählt, die der eigene Server zuletzt eingetragen hat.
    /// </param>
    public static bool IsVerified(IEnumerable<string> headers, string fromAddress, IReadOnlyCollection<string> trustedServers)
    {
        var at = fromAddress.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var fromDomain = fromAddress[(at + 1)..].Trim().TrimEnd('>').ToLowerInvariant();
        var candidates = trustedServers.Count == 0
            ? headers.Take(1)
            : headers.Where(h => trustedServers.Contains(ServerId(h), StringComparer.OrdinalIgnoreCase));

        return candidates.Any(h => Passes(h, fromDomain));
    }

    private static string ServerId(string header) => header.Split(';', 2)[0].Trim().Split(' ', 2)[0];

    private static bool Passes(string header, string fromDomain)
    {
        foreach (var part in header.Split(';').Skip(1))
        {
            if (Result().Match(part) is not { Success: true } m || !m.Groups["result"].Value.Equals("pass", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var method = m.Groups["method"].Value.ToLowerInvariant();
            if (method == "dmarc")
            {
                // DMARC prüft die Übereinstimmung mit „From“ selbst; header.from nennt die geprüfte Domain.
                var checkedDomain = Property(part, "header.from");
                if (checkedDomain is null || Aligned(checkedDomain, fromDomain))
                {
                    return true;
                }
            }
            else if (method == "dkim" && Property(part, "header.d") is { } signer && Aligned(signer, fromDomain))
            {
                return true;
            }
            else if (method == "spf" && Property(part, "smtp.mailfrom") is { } mailFrom && Aligned(mailFrom, fromDomain))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Property(string part, string name) =>
        Regex.Match(part, $@"\b{Regex.Escape(name)}=(?<v>[^\s;()]+)", RegexOptions.IgnoreCase) is { Success: true } m
            ? m.Groups["v"].Value
            : null;

    /// <summary>Gleiche Domain oder Subdomain (entspannte Übereinstimmung wie bei DMARC).</summary>
    private static bool Aligned(string value, string fromDomain)
    {
        var domain = value[(value.LastIndexOf('@') + 1)..].Trim('"').ToLowerInvariant();
        return domain.Contains('.', StringComparison.Ordinal)
            && (domain == fromDomain || domain.EndsWith($".{fromDomain}", StringComparison.Ordinal)
                || fromDomain.EndsWith($".{domain}", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^\s*(?<method>[a-z0-9-]+)\s*=\s*(?<result>[a-z]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Result();
}

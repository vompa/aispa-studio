using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace AispaStudio.Api.MailIntake.Mailboxes;

/// <summary>Wandelt Mails aus IMAP oder Graph in <see cref="IncomingMail"/>: nur Text, Anhänge bleiben im Postfach.</summary>
public static partial class MimeMail
{
    /// <summary>Längere Mails werden gekürzt; für eine Terminanfrage reicht der Anfang, und die KI liest nicht unnötig viel.</summary>
    public const int MaxBodyLength = 20_000;

    public static IncomingMail ToIncoming(MimeMessage message, DateTimeOffset? receivedAt, IReadOnlyCollection<string> trustedServers)
    {
        var from = message.From.Mailboxes.FirstOrDefault();
        var sender = from?.Address ?? "";
        var body = message.TextBody is { Length: > 0 } text ? text : HtmlToText(message.HtmlBody ?? "");
        var authentication = message.Headers.Where(h => h.Id == HeaderId.AuthenticationResults).Select(h => h.Value);

        return new IncomingMail(
            MessageId: message.MessageId is { Length: > 0 } id ? $"<{id}>" : $"<{Guid.NewGuid():N}@ohne-id.aispa>",
            // Bewusst „From“ und nicht „Reply-To“: nur „From“ ist durch SPF/DKIM/DMARC gedeckt (Treatwell-Erkennung).
            From: sender,
            FromName: string.IsNullOrWhiteSpace(from?.Name) ? null : from.Name,
            To: message.To.Mailboxes.FirstOrDefault()?.Address ?? "",
            Subject: message.Subject ?? "",
            Body: Trim(body),
            // Eingangszeit des Servers, nicht das vom Absender gesetzte „Date“.
            ReceivedAt: receivedAt ?? (message.Date == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : message.Date),
            SenderVerified: MailAuthentication.IsVerified(authentication, sender, trustedServers));
    }

    /// <summary>Einfacher HTML-zu-Text-Wandler: Zeilenumbrüche für Absätze, Tags weg, Entitäten aufgelöst.</summary>
    public static string HtmlToText(string html)
    {
        var text = Hidden().Replace(html, "");
        text = LineBreak().Replace(text, "\n");
        text = CellEnd().Replace(text, " ");
        text = Tag().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ");
        return BlankLines().Replace(text, "\n\n").Trim();
    }

    public static string Trim(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= MaxBodyLength ? normalized : normalized[..MaxBodyLength];
    }

    [GeneratedRegex(@"<!--.*?-->|<(script|style|head)\b.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<\s*(br|/p|/div|/tr|/li|/ul|/ol|/table|/h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    /// <summary>Ende einer Tabellenzelle: Bezeichnung und Wert in zwei Zellen („Kunde:“, „Max Mustermann“) bleiben getrennte Wörter.</summary>
    [GeneratedRegex(@"<\s*/(td|th)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex CellEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n\s*\n+")]
    private static partial Regex BlankLines();
}

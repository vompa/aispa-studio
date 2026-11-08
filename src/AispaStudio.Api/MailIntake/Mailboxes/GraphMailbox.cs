using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AispaStudio.Api.MailIntake.Mailboxes;

/// <summary>
/// Postfach in Microsoft 365 über Microsoft Graph (Abschnitt <c>MailIntake:Graph</c>). Anmeldung als App mit
/// Client-Credentials; die App braucht die Anwendungsberechtigung <c>Mail.ReadWrite</c> (und <c>Mail.Send</c>, wenn das Spa
/// über sein Postfach antwortet), am besten per Application Access Policy auf das eine Postfach beschränkt. Das Geheimnis kommt nie ins Repository.
/// </summary>
public sealed class GraphOptions
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Adresse oder Id des Postfachs, z. B. termine@spa.at.</summary>
    public string? Mailbox { get; set; }

    /// <summary>Ordner: Name eines bekannten Ordners („inbox“) oder dessen Id.</summary>
    public string Folder { get; set; } = "inbox";

    /// <summary>Kategorie, mit der verarbeitete Mails markiert werden; im Outlook-Client sichtbar.</summary>
    public string ProcessedCategory { get; set; } = "AISPA verarbeitet";

    public int LookbackDays { get; set; } = 7;

    /// <summary>Exchange Online trägt <c>spf.protection.outlook.com</c> ein. Leer: nur die oberste Kopfzeile zählt.</summary>
    public string[] TrustedAuthServers { get; set; } = [];

    public Uri Authority { get; set; } = new("https://login.microsoftonline.com/");
    public Uri GraphBase { get; set; } = new("https://graph.microsoft.com/v1.0/");
}

/// <summary>
/// Liest neue Mails über Graph und markiert verarbeitete mit einer Kategorie statt als gelesen. Versendet für ein Spa auch
/// Antworten aus seinem Postfach (<see cref="SendAsync(MimeMessage, CancellationToken)"/>).
/// </summary>
internal sealed class GraphMailbox(IHttpClientFactory http, IOptions<GraphOptions> options, TimeProvider time) : IMailbox, IDisposable
{
    public const string HttpClientName = "graph-mailbox";
    private const int MaxMails = 50;
    private const int MaxPages = 10;

    private readonly ConcurrentDictionary<string, (string Id, IReadOnlyList<string> Categories)> _pending = new();
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private (string Value, DateTimeOffset Expires)? _token;

    public async Task<IReadOnlyList<IncomingMail>> FetchNewAsync(CancellationToken ct)
    {
        var o = options.Value;
        var since = time.GetUtcNow().AddDays(-Math.Max(1, o.LookbackDays)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var url = $"users/{Uri.EscapeDataString(Require(o.Mailbox, "Mailbox"))}/mailFolders/{Uri.EscapeDataString(o.Folder)}/messages"
            + $"?$filter=receivedDateTime ge {since}&$orderby=receivedDateTime&$top=50"
            + "&$select=id,internetMessageId,subject,from,toRecipients,receivedDateTime,body,categories,internetMessageHeaders";

        // Verarbeitete Mails bleiben im Ordner; deshalb weiterblättern, bis genug neue beisammen sind.
        var mails = new List<IncomingMail>();
        Uri? next = new(o.GraphBase, url);
        for (var pages = 0; next is not null && pages < MaxPages && mails.Count < MaxMails; pages++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
            var page = await SendAsync<MessagePage>(request, ct);
            next = page.NextLink;
            foreach (var m in page.Value ?? [])
            {
                if (mails.Count < MaxMails && ToIncoming(m, o) is { } mail)
                {
                    mails.Add(mail);
                }
            }
        }

        return mails;
    }

    private IncomingMail? ToIncoming(GraphMessage m, GraphOptions o)
    {
        var categories = m.Categories ?? [];
        if (categories.Contains(o.ProcessedCategory, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var from = m.From?.EmailAddress;
        var authentication = (m.InternetMessageHeaders ?? [])
            .Where(h => h.Name.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value);
        var messageId = m.InternetMessageId is { Length: > 0 } id ? id : $"<{m.Id}@graph.aispa>";
        _pending[messageId] = (m.Id, categories);
        return new IncomingMail(
            messageId,
            from?.Address ?? "",
            string.IsNullOrWhiteSpace(from?.Name) ? null : from.Name,
            (m.ToRecipients is { Count: > 0 } to ? to[0].EmailAddress?.Address : null) ?? o.Mailbox!,
            m.Subject ?? "",
            MimeMail.Trim(m.Body?.ContentType == "html" ? MimeMail.HtmlToText(m.Body.Content ?? "") : m.Body?.Content ?? ""),
            m.ReceivedDateTime,
            MailAuthentication.IsVerified(authentication, from?.Address ?? "", o.TrustedAuthServers));
    }

    public void Dispose() => _tokenLock.Dispose();

    public async Task MarkProcessedAsync(string messageId, CancellationToken ct)
    {
        if (!_pending.TryRemove(messageId, out var entry))
        {
            return;
        }

        var o = options.Value;
        var url = $"users/{Uri.EscapeDataString(Require(o.Mailbox, "Mailbox"))}/messages/{Uri.EscapeDataString(entry.Id)}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri(o.GraphBase, url))
        {
            Content = JsonContent.Create(new { categories = entry.Categories.Append(o.ProcessedCategory).Distinct().ToArray() }),
        };
        using var response = await SendRawAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Mail aus diesem Postfach senden. Als MIME, damit In-Reply-To und References erhalten bleiben und die Antwort beim Gast
    /// im selben Verlauf landet; Exchange legt sie unter „Gesendete Elemente“ ab.
    /// </summary>
    public async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var mime = new MemoryStream();
        await message.WriteToAsync(mime, ct);
        var url = $"users/{Uri.EscapeDataString(Require(o.Mailbox, "Mailbox"))}/sendMail";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(o.GraphBase, url))
        {
            Content = new StringContent(Convert.ToBase64String(mime.GetBuffer(), 0, (int)mime.Length), Encoding.ASCII, "text/plain"),
        };
        using var response = await SendRawAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(response.StatusCode == HttpStatusCode.Forbidden
                ? "Microsoft 365 lehnt den Versand ab: Der App fehlt die Berechtigung Mail.Send für dieses Postfach."
                : $"Microsoft 365 hat die Mail nicht angenommen ({(int)response.StatusCode}).", null, response.StatusCode);
        }
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await SendRawAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new InvalidOperationException("Leere Antwort von Graph.");
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        return await http.CreateClient(HttpClientName).SendAsync(request, ct);
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is { } cached && cached.Expires > time.GetUtcNow())
            {
                return cached.Value;
            }

            var o = options.Value;
            var url = new Uri(o.Authority, $"{Uri.EscapeDataString(Require(o.TenantId, "TenantId"))}/oauth2/v2.0/token");
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Require(o.ClientId, "ClientId"),
                ["client_secret"] = Require(o.ClientSecret, "ClientSecret"),
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials",
            });
            using var response = await http.CreateClient(HttpClientName).PostAsync(url, form, ct);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct) ?? throw new InvalidOperationException("Kein Token von Microsoft Entra.");
            _token = (token.AccessToken, time.GetUtcNow().AddSeconds(Math.Max(60, token.ExpiresIn) - 60));
            return token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"MailIntake:Graph:{name} fehlt.") : value;

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record MessagePage(
        IReadOnlyList<GraphMessage>? Value,
        [property: JsonPropertyName("@odata.nextLink")] Uri? NextLink);

    private sealed record GraphMessage(
        string Id,
        string? InternetMessageId,
        string? Subject,
        Recipient? From,
        IReadOnlyList<Recipient>? ToRecipients,
        DateTimeOffset ReceivedDateTime,
        ItemBody? Body,
        IReadOnlyList<string>? Categories,
        IReadOnlyList<MessageHeader>? InternetMessageHeaders);

    private sealed record Recipient(EmailAddress? EmailAddress);

    private sealed record EmailAddress(string? Name, string? Address);

    private sealed record ItemBody(string? ContentType, string? Content);

    private sealed record MessageHeader(string Name, string Value);
}

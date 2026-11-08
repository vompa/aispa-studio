using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>Prüft Adressen von Kalender-Abos: nur <c>https://</c> (ein <c>webcal://</c> wird zu <c>https://</c>), keine Anmeldedaten, keine internen Ziele.</summary>
internal static partial class SubscriptionUrl
{
    public const string HttpClientName = "calendar-feed";

    /// <summary>
    /// Prüft und vereinheitlicht eine Adresse. Leer ist erlaubt (Abo nur für Datei-Import). Fehlertext in Sätzen für die Maske.
    /// </summary>
    public static bool TryNormalize(string? text, out string normalized, out string? error)
    {
        normalized = "";
        error = null;
        var url = (text ?? "").Trim();
        if (url.Length == 0)
        {
            return true;
        }

        if (url.Length > CalendarSubscriptionModel.UrlLength)
        {
            error = $"Höchstens {CalendarSubscriptionModel.UrlLength} Zeichen.";
            return false;
        }

        var webcal = WebcalScheme().Match(url);
        if (webcal.Success)
        {
            url = "https://" + url[webcal.Length..];
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Die Adresse muss mit https:// oder webcal:// beginnen.";
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            error = "Die Adresse darf keinen Benutzernamen und kein Passwort enthalten.";
            return false;
        }

        if (!uri.IsDefaultPort)
        {
            error = "Nur der Standardanschluss (443) ist erlaubt.";
            return false;
        }

        if (SecretInQuery().IsMatch(uri.Query) || PrivatePath().IsMatch(uri.AbsolutePath))
        {
            error = "Die Adresse darf keinen Schlüssel oder Zugangscode enthalten. Es sind nur öffentliche Kalender-Adressen erlaubt.";
            return false;
        }

        if (IsInternalHost(uri.Host))
        {
            error = "Die Adresse zeigt auf einen internen Rechner und ist nicht erlaubt.";
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }

    /// <summary>Interne Namen und Adressen, die ein Abo nie abrufen darf (Schutz vor Zugriffen ins eigene Netz).</summary>
    public static bool IsInternalHost(string host)
    {
        var name = host.TrimEnd('.').ToLowerInvariant();
        if (name is "localhost" or "" || name.EndsWith(".local", StringComparison.Ordinal) || name.EndsWith(".internal", StringComparison.Ordinal)
            || name.EndsWith(".localhost", StringComparison.Ordinal) || !name.Contains('.', StringComparison.Ordinal) && !IPAddress.TryParse(name.Trim('[', ']'), out _))
        {
            return true;
        }

        return IPAddress.TryParse(name.Trim('[', ']'), out var ip) && IsInternal(ip);
    }

    public static bool IsInternal(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 16)
        {
            // Eindeutige lokale IPv6-Adressen (fc00::/7).
            return (bytes[0] & 0xFE) == 0xFC;
        }

        return bytes[0] switch
        {
            0 or 10 or 127 => true,
            100 => bytes[1] is >= 64 and <= 127,
            169 => bytes[1] == 254,
            172 => bytes[1] is >= 16 and <= 31,
            192 => bytes[1] == 168 || (bytes[1] == 0 && bytes[2] == 0),
            198 => bytes[1] is 18 or 19,
            >= 224 => true,
            _ => false,
        };
    }

    [GeneratedRegex(@"^webcals?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebcalScheme();

    [GeneratedRegex(@"(token|secret|passw|pwd|apikey|api_key|access_key|auth|sig(nature)?|credential|[?&]key)=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretInQuery();

    /// <summary>Private Kalender-Adressen (z. B. von Google) tragen den Zugangscode im Pfad.</summary>
    [GeneratedRegex(@"/private-[0-9a-f]{8,}/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivatePath();
}

/// <summary>Antwort eines Abrufs: der Text oder ein Fehlertext für die Meldung (kein Ausnahmefall, sondern erwartbar: Netz, Adresse, Größe).</summary>
internal sealed record FeedDownload(string? Content, string? Error)
{
    public static FeedDownload Ok(string content) => new(content, null);

    public static FeedDownload Fail(string error) => new(null, error);
}

/// <summary>Holt den Inhalt einer Abo-Adresse. Eine Schnittstelle, damit Tests nie ins Netz gehen.</summary>
internal interface ICalendarFeedClient
{
    Task<FeedDownload> GetAsync(string url, CancellationToken ct);
}

/// <summary>
/// Der echte Abruf: nur ausgehend, ohne Anmeldung, höchstens drei Weiterleitungen (jede wieder nur <c>https</c> auf einen öffentlichen Rechner),
/// Zeitlimit und Größenlimit. Der Anschluss an interne Adressen scheitert schon beim Verbinden (<see cref="Connect"/>), auch wenn ein Name dorthin zeigt.
/// </summary>
internal sealed class HttpCalendarFeedClient(IHttpClientFactory clients) : ICalendarFeedClient
{
    private const int MaxRedirects = 3;

    public async Task<FeedDownload> GetAsync(string url, CancellationToken ct)
    {
        var current = url;
        try
        {
            using var http = clients.CreateClient(SubscriptionUrl.HttpClientName);
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/calendar"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.1));
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod
                    or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    var next = response.Headers.Location is { } location ? new Uri(new Uri(current), location).AbsoluteUri : null;
                    if (next is null || !SubscriptionUrl.TryNormalize(next, out current, out _) || current.Length == 0)
                    {
                        return FeedDownload.Fail("Die Weiterleitung der Adresse ist nicht erlaubt.");
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return FeedDownload.Fail($"Der Server antwortete mit {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                if (response.Content.Headers.ContentLength > FeedReader.MaxBytes)
                {
                    return FeedDownload.Fail($"Der Kalender ist größer als {FeedReader.MaxBytes / (1024 * 1024)} MB.");
                }

                return await ReadLimitedAsync(response, ct);
            }

            return FeedDownload.Fail("Zu viele Weiterleitungen.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return FeedDownload.Fail("Zeitüberschreitung beim Abruf.");
        }
        catch (HttpRequestException ex)
        {
            return FeedDownload.Fail($"Der Kalender ist nicht erreichbar ({ex.HttpRequestError}).");
        }
    }

    private static async Task<FeedDownload> ReadLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > FeedReader.MaxBytes)
            {
                return FeedDownload.Fail($"Der Kalender ist größer als {FeedReader.MaxBytes / (1024 * 1024)} MB.");
            }
        }

        return FeedDownload.Ok(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
    }

    /// <summary>Löst den Namen auf und verbindet nur mit öffentlichen Adressen (Schutz vor Umleitung des Namens ins eigene Netz).</summary>
    public static async ValueTask<Stream> Connect(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
        var allowed = addresses.Where(a => !SubscriptionUrl.IsInternal(a)).ToList();
        if (allowed.Count == 0)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Ziel nicht erlaubt.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync([.. allowed], context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public sealed class CalendarSubscriptionOptions
{
    public const string Section = "CalendarSubscriptions";

    /// <summary>
    /// Wie oft der Hintergrundjob nach fälligen Abos sucht (alle zehn Minuten; ein Abo ist erst nach seinem Termin fällig, ein neues Spa bekommt seine Feiertage also bald); 0 schaltet ihn aus
    /// (Tests stoßen den Abruf selbst an).
    /// </summary>
    public int PollSeconds { get; set; } = 600;
}

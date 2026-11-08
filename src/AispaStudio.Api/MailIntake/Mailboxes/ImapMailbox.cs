using System.Collections.Concurrent;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Mailboxes;

/// <summary>
/// Postfach über IMAP (z. B. das bestehende Termine-Postfach beim Hoster). Abschnitt <c>MailIntake:Imap</c>; Benutzer und
/// Passwort kommen aus User-Secrets oder dem Geheimnisspeicher, nie aus dem Repository.
/// </summary>
public sealed class ImapOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 993;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.SslOnConnect;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string Folder { get; set; } = "INBOX";

    /// <summary>Verarbeitete Mails hierher verschieben; leer: im Ordner lassen und nur markieren.</summary>
    public string? ProcessedFolder { get; set; }

    /// <summary>Nur Mails der letzten Tage abrufen, damit ein volles Postfach nicht auf einmal in die Queue kommt.</summary>
    public int LookbackDays { get; set; } = 7;

    /// <summary>Kennungen der eigenen Empfangsserver in „Authentication-Results“ (siehe <see cref="MailAuthentication"/>).</summary>
    public string[] TrustedAuthServers { get; set; } = [];
}

/// <summary>
/// Liest neue Mails per IMAP und markiert verarbeitete mit dem Schlagwort <see cref="ProcessedKeyword"/> statt als gelesen,
/// damit das Team im Mailprogramm weiter sieht, was neu ist. Kennt der Server keine eigenen Schlagwörter, wird verschoben
/// (<see cref="ImapOptions.ProcessedFolder"/>) oder als gelesen markiert.
/// </summary>
internal sealed partial class ImapMailbox(IOptions<ImapOptions> options, TimeProvider time, ILogger<ImapMailbox> log) : IMailbox, IDisposable
{
    public const string ProcessedKeyword = "$AispaVerarbeitet";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, UniqueId> _pending = new();
    private ImapClient? _client;

    public async Task<IReadOnlyList<IncomingMail>> FetchNewAsync(CancellationToken ct)
    {
        var o = options.Value;
        await _lock.WaitAsync(ct);
        try
        {
            var folder = await OpenAsync(ct);
            var since = time.GetUtcNow().AddDays(-Math.Max(1, o.LookbackDays)).UtcDateTime;
            var query = SearchQuery.DeliveredAfter(since).And(SearchQuery.NotKeyword(ProcessedKeyword));
            if (!SupportsKeywords(folder))
            {
                query = SearchQuery.DeliveredAfter(since).And(SearchQuery.NotSeen);
            }

            var uids = await folder.SearchAsync(query, ct);
            var summaries = await folder.FetchAsync(uids.Take(50).ToList(), MessageSummaryItems.InternalDate | MessageSummaryItems.UniqueId, ct);
            var mails = new List<IncomingMail>();
            foreach (var summary in summaries.OrderBy(s => s.InternalDate))
            {
                var message = await folder.GetMessageAsync(summary.UniqueId, ct);
                var mail = MimeMail.ToIncoming(message, summary.InternalDate, o.TrustedAuthServers);
                _pending[mail.MessageId] = summary.UniqueId;
                mails.Add(mail);
            }

            return mails;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DropConnectionAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkProcessedAsync(string messageId, CancellationToken ct)
    {
        if (!_pending.TryRemove(messageId, out var uid))
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            var folder = await OpenAsync(ct);
            if (options.Value.ProcessedFolder is { Length: > 0 } target)
            {
                var destination = await _client!.GetFolderAsync(target, ct);
                await folder.MoveToAsync(uid, destination, ct);
            }
            else if (SupportsKeywords(folder))
            {
                await folder.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.None) { Keywords = { ProcessedKeyword }, Silent = true }, ct);
            }
            else
            {
                await folder.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen) { Silent = true }, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nicht schlimm: Die Message-ID ist gespeichert, beim nächsten Abruf wird die Mail nur noch übersprungen.
            LogMarkFailed(log, ex, messageId);
            await DropConnectionAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _lock.Dispose();
    }

    private static bool SupportsKeywords(IMailFolder folder) =>
        folder.PermanentFlags.HasFlag(MessageFlags.UserDefined) || folder.PermanentKeywords.Contains(ProcessedKeyword);

    private async Task<IMailFolder> OpenAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (_client is not { IsConnected: true, IsAuthenticated: true })
        {
            await DropConnectionAsync();
            _client = new ImapClient();
            await _client.ConnectAsync(o.Host ?? throw new InvalidOperationException("MailIntake:Imap:Host fehlt."), o.Port, o.Security, ct);
            await _client.AuthenticateAsync(o.UserName ?? "", o.Password ?? "", ct);
        }

        var folder = o.Folder.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? _client.Inbox : await _client.GetFolderAsync(o.Folder, ct);
        if (!folder.IsOpen || folder.Access != FolderAccess.ReadWrite)
        {
            await folder.OpenAsync(FolderAccess.ReadWrite, ct);
        }

        return folder;
    }

    private async Task DropConnectionAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(true, CancellationToken.None);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDisconnectFailed(log, ex);
        }

        _client.Dispose();
        _client = null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail {MessageId} konnte im IMAP-Postfach nicht als verarbeitet markiert werden.")]
    private static partial void LogMarkFailed(ILogger logger, Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IMAP-Verbindung ließ sich nicht sauber trennen.")]
    private static partial void LogDisconnectFailed(ILogger logger, Exception ex);
}

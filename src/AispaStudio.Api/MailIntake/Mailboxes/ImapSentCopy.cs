using MailKit;
using MailKit.Net.Imap;
using MimeKit;

namespace AispaStudio.Api.MailIntake.Mailboxes;

/// <summary>
/// Legt eine über SMTP gesendete Mail im Ordner „Gesendet“ des IMAP-Postfachs ab, damit das Spa sie in seinem Mailprogramm
/// sieht. Eigene kurze Verbindung, damit der Abruf im Takt seine offene Verbindung behält.
/// </summary>
internal static class ImapSentCopy
{
    /// <summary>Übliche Namen, falls der Server keinen Ordner als „Gesendet“ kennzeichnet (SPECIAL-USE bzw. XLIST).</summary>
    public static readonly string[] CommonNames =
    [
        "Sent", "Gesendet", "Sent Items", "Gesendete Elemente", "Sent Messages", "Gesendete Objekte",
        "INBOX.Sent", "INBOX.Gesendet", "INBOX/Sent", "Envoyés", "Posta inviata",
    ];

    public static Task AppendAsync(ImapOptions options, string? folderName, MimeMessage message, CancellationToken ct) =>
        WithFolderAsync(options, folderName, folder => folder.AppendAsync(new AppendRequest(message, MessageFlags.Seen), ct), ct);

    /// <summary>Für „Jetzt prüfen“: gibt es den Ordner, ohne etwas abzulegen.</summary>
    public static Task VerifyAsync(ImapOptions options, string? folderName, CancellationToken ct) =>
        WithFolderAsync(options, folderName, _ => Task.CompletedTask, ct);

    private static async Task WithFolderAsync(ImapOptions options, string? folderName, Func<IMailFolder, Task> work, CancellationToken ct)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(options.Host ?? throw new InvalidOperationException("IMAP-Server fehlt."), options.Port, options.Security, ct);
        await client.AuthenticateAsync(options.UserName ?? "", options.Password ?? "", ct);
        var folder = await FindAsync(client, folderName, ct)
            ?? throw new InvalidOperationException("Kein Ordner „Gesendet“ gefunden. Bitte den Ordner beim Postfach angeben.");
        await work(folder);
        await client.DisconnectAsync(true, ct);
    }

    private static async Task<IMailFolder?> FindAsync(ImapClient client, string? folderName, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            return await client.GetFolderAsync(folderName.Trim(), ct);
        }

        if ((client.Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) != 0 && client.GetFolder(SpecialFolder.Sent) is { } sent)
        {
            return sent;
        }

        foreach (var name in CommonNames)
        {
            try
            {
                return await client.GetFolderAsync(name, ct);
            }
            catch (FolderNotFoundException)
            {
                // nächsten Namen versuchen
            }
        }

        return null;
    }
}

/// <summary>Die Mail ist versendet, nur die Kopie in „Gesendet“ fehlt. Darf die Mail nicht noch einmal auslösen.</summary>
public sealed class SentCopyFailedException(Exception inner)
    : Exception($"Gesendet, aber keine Kopie unter „Gesendet“: {inner.Message}", inner);

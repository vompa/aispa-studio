using System.Collections.Concurrent;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Mailboxes;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AispaStudio.Api.MailIntake;

/// <summary>Eigenes Postfach eines Spas (IMAP oder Microsoft Graph). Das Geheimnis liegt nur verschlüsselt in der Datenbank.</summary>
public sealed class SpaMailbox
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public MailboxKind Kind { get; set; }
    public required string Address { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Host { get; set; }
    public int Port { get; set; } = 993;
    public MailboxSecurity Security { get; set; } = MailboxSecurity.SslOnConnect;
    public string? UserName { get; set; }
    public string Folder { get; set; } = "INBOX";
    public string? ProcessedFolder { get; set; }
    public string? DirectoryTenantId { get; set; }
    public string? ClientId { get; set; }

    /// <summary>Antworten und Erinnerungen aus diesem Postfach senden statt über den gemeinsamen Versand.</summary>
    public bool SendReplies { get; set; }

    /// <summary>Nur IMAP: SMTP-Server für den Versand; angemeldet wird mit Benutzer und Passwort des Postfachs.</summary>
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;
    public MailboxSecurity SmtpSecurity { get; set; } = MailboxSecurity.StartTls;

    /// <summary>Nur IMAP: gesendete Mails zusätzlich im Ordner „Gesendet“ ablegen. Aus bei Anbietern, die das selbst tun (Gmail).</summary>
    public bool SaveSentCopy { get; set; } = true;

    /// <summary>Ordner für die Kopie; leer heißt: den als „Gesendet“ gekennzeichneten Ordner bzw. einen üblichen Namen suchen.</summary>
    public string? SentFolder { get; set; }

    /// <summary>Mit ASP.NET Data Protection verschlüsselt, an den Mandanten gebunden.</summary>
    public string? ProtectedSecret { get; set; }

    /// <summary>Ändert sich bei jeder Änderung; der Abruf baut dann die Verbindung neu auf.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
    public string? LastSendError { get; set; }
}

/// <summary>
/// Nummer oder Adresse, über die Gäste ein bestimmtes Spa erreichen. Eindeutig über alle Spas, damit eine eingehende Nachricht
/// genau einem Spa gehört. Anlegen darf nur der Betreiber: die Nummern liegen in seinem Anbieterkonto, und ein Spa dürfte
/// sonst die Nummer eines anderen beanspruchen.
/// </summary>
public sealed class IntakeRoute
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public BookingChannel Channel { get; init; }
    public required string Address { get; init; }
    public string? SenderId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal static class IntakeModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SpaMailbox>(e =>
        {
            e.Property(b => b.Id).ValueGeneratedNever();
            e.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.Security).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.Address).HasMaxLength(320);
            e.Property(b => b.Host).HasMaxLength(255);
            e.Property(b => b.UserName).HasMaxLength(320);
            e.Property(b => b.Folder).HasMaxLength(200);
            e.Property(b => b.ProcessedFolder).HasMaxLength(200);
            e.Property(b => b.DirectoryTenantId).HasMaxLength(100);
            e.Property(b => b.ClientId).HasMaxLength(100);
            e.Property(b => b.ProtectedSecret).HasMaxLength(8000);
            e.Property(b => b.LastError).HasMaxLength(500);
            e.Property(b => b.SmtpHost).HasMaxLength(255);
            e.Property(b => b.SmtpSecurity).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.LastSendError).HasMaxLength(500);
            e.Property(b => b.SaveSentCopy).HasDefaultValue(true).HasSentinel(true);
            e.Property(b => b.SentFolder).HasMaxLength(200);
        });
        builder.Entity<IntakeRoute>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Address).HasMaxLength(320);
            e.Property(r => r.SenderId).HasMaxLength(100);
            e.HasIndex(r => new { r.Channel, r.Address }).IsUnique();
        });
    }
}

/// <summary>Welchem Spa eine Nummer oder Adresse gehört, und von welcher Nummer ein Spa schreibt.</summary>
internal static class IntakeRoutes
{
    /// <summary>Mailadressen klein, Nummern als E.164; leer, wenn nichts Brauchbares übrig bleibt.</summary>
    public static string Normalize(BookingChannel channel, string address) =>
        channel == BookingChannel.Email ? address.Trim().ToLowerInvariant() : PhoneNumbers.Normalize(address);

    /// <summary>
    /// Zuordnung für die angeschriebene Nummer oder Adresse, ohne Mandantenfilter (der Mandant ist ja gerade gesucht). Bevorzugt
    /// derselbe Kanal; ein Anruf auf die SMS-Nummer gehört aber demselben Spa.
    /// </summary>
    public static async Task<(Guid TenantId, IntakeRoute Route)?> FindAsync(AispaDbContext db, BookingChannel channel, string to, CancellationToken ct)
    {
        var address = Normalize(channel, to);
        if (address.Length == 0)
        {
            return null;
        }

        var email = channel == BookingChannel.Email;
        var matches = await db.IntakeRoutes.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Address == address && (r.Channel == BookingChannel.Email) == email)
            .Select(r => new { Route = r, TenantId = EF.Property<Guid>(r, TenantModel.Property) })
            .ToListAsync(ct);
        var match = matches.FirstOrDefault(m => m.Route.Channel == channel) ?? matches.FirstOrDefault();
        return match is null ? null : (match.TenantId, match.Route);
    }

    /// <summary>Absender für Nachrichten des aktuellen Spas, die nicht auf eine Nachricht antworten (z. B. Erinnerungen).</summary>
    public static Task<IntakeRoute?> OwnAsync(AispaDbContext db, BookingChannel channel, CancellationToken ct) =>
        db.IntakeRoutes.AsNoTracking().Where(r => r.Channel == channel).OrderBy(r => r.CreatedAt).FirstOrDefaultAsync(ct);
}

/// <summary>Verschlüsselt Passwort bzw. Client-Geheimnis eines Spa-Postfachs, gebunden an Spa und Postfach.</summary>
internal sealed class SpaMailboxSecrets(IDataProtectionProvider protection)
{
    private IDataProtector Protector(Guid tenantId, Guid mailboxId) =>
        protection.CreateProtector("AispaStudio.SpaMailbox.Secret", tenantId.ToString(), mailboxId.ToString());

    public string Protect(Guid tenantId, Guid mailboxId, string secret) => Protector(tenantId, mailboxId).Protect(secret);

    public string Unprotect(Guid tenantId, Guid mailboxId, string protectedSecret) => Protector(tenantId, mailboxId).Unprotect(protectedSecret);
}

/// <summary>Alles, was es für die Verbindung zu einem Spa-Postfach braucht, mit entschlüsseltem Geheimnis (nur im Speicher).</summary>
public sealed record SpaMailboxSettings(
    Guid Id,
    MailboxKind Kind,
    string Address,
    string? Host,
    int Port,
    MailboxSecurity Security,
    string? UserName,
    string? Secret,
    string Folder,
    string? ProcessedFolder,
    string? DirectoryTenantId,
    string? ClientId,
    string? SmtpHost = null,
    int SmtpPort = 587,
    MailboxSecurity SmtpSecurity = MailboxSecurity.StartTls,
    bool SaveSentCopy = true,
    string? SentFolder = null)
{
    public static SpaMailboxSettings Of(SpaMailbox box, string? secret) =>
        new(box.Id, box.Kind, box.Address, box.Host, box.Port, box.Security, box.UserName, secret, box.Folder, box.ProcessedFolder,
            box.DirectoryTenantId, box.ClientId, box.SmtpHost, box.SmtpPort, box.SmtpSecurity, box.SaveSentCopy, box.SentFolder);
}

/// <summary>Baut das Postfach und den Versand aus den Einstellungen eines Spas; in Tests ersetzbar.</summary>
public interface ISpaMailboxFactory
{
    IMailbox Create(SpaMailboxSettings settings);

    /// <summary>Versand aus dem Postfach: IMAP über den SMTP-Server des Spas, Graph über sendMail.</summary>
    IMailSender CreateSender(SpaMailboxSettings settings);

    /// <summary>Anmeldung zum Versand prüfen, ohne etwas zu senden (IMAP: SMTP-Anmeldung und Ordner „Gesendet“; Graph: nichts zusätzlich, das Lesen prüft die App).</summary>
    Task VerifySenderAsync(SpaMailboxSettings settings, CancellationToken ct);
}

internal sealed class SpaMailboxFactory(
    IHttpClientFactory http,
    TimeProvider time,
    IOptions<ImapOptions> imapDefaults,
    IOptions<GraphOptions> graphDefaults,
    ILoggerFactory logs) : ISpaMailboxFactory
{
    public IMailbox Create(SpaMailboxSettings s) => s.Kind switch
    {
        MailboxKind.Graph => Graph(s),
        _ => new ImapMailbox(Options.Create(Imap(s)), time, logs.CreateLogger<ImapMailbox>()),
    };

    public IMailSender CreateSender(SpaMailboxSettings s) => s.Kind switch
    {
        MailboxKind.Graph => new GraphSender(Graph(s), s.Address),
        _ => new SmtpSender(Smtp(s), s.Address, s.SaveSentCopy ? (message, ct) => ImapSentCopy.AppendAsync(Imap(s), s.SentFolder, message, ct) : null),
    };

    public async Task VerifySenderAsync(SpaMailboxSettings s, CancellationToken ct)
    {
        if (s.Kind == MailboxKind.Graph)
        {
            return;
        }

        await SmtpTransport.VerifyAsync(Smtp(s), ct);
        if (s.SaveSentCopy)
        {
            try
            {
                await ImapSentCopy.VerifyAsync(Imap(s), s.SentFolder, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Senden klappt, aber keine Ablage unter „Gesendet“: {ex.Message}", ex);
            }
        }
    }

    private ImapOptions Imap(SpaMailboxSettings s) => new()
    {
        Host = s.Host,
        Port = s.Port,
        Security = Socket(s.Security),
        UserName = s.UserName,
        Password = s.Secret,
        Folder = s.Folder,
        ProcessedFolder = s.ProcessedFolder,
        TrustedAuthServers = imapDefaults.Value.TrustedAuthServers,
    };

    private static SmtpOptions Smtp(SpaMailboxSettings s) => new()
    {
        Host = s.SmtpHost,
        Port = s.SmtpPort,
        Security = Socket(s.SmtpSecurity),
        UserName = s.UserName,
        Password = s.Secret,
    };

    private GraphMailbox Graph(SpaMailboxSettings s) => new(http, Options.Create(new GraphOptions
    {
        TenantId = s.DirectoryTenantId,
        ClientId = s.ClientId,
        ClientSecret = s.Secret,
        Mailbox = s.Address,
        Folder = string.IsNullOrWhiteSpace(s.Folder) || s.Folder == "INBOX" ? "inbox" : s.Folder,
        TrustedAuthServers = graphDefaults.Value.TrustedAuthServers,
    }), time);

    private static SecureSocketOptions Socket(MailboxSecurity security) => security switch
    {
        MailboxSecurity.StartTls => SecureSocketOptions.StartTls,
        MailboxSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.SslOnConnect,
    };

    /// <summary>SMTP des Spas; danach auf Wunsch eine Kopie in „Gesendet“, deren Fehler die schon versendete Mail nicht wiederholt.</summary>
    private sealed class SmtpSender(SmtpOptions smtp, string address, Func<MimeMessage, CancellationToken, Task>? saveCopy) : IMailSender
    {
        public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            var message = MailMessages.Build(mail, new MailboxAddress(mail.FromName, address));
            await SmtpTransport.SendAsync(message, smtp, ct);
            if (saveCopy is null)
            {
                return;
            }

            try
            {
                await saveCopy(message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new SentCopyFailedException(ex);
            }
        }
    }

    private sealed class GraphSender(GraphMailbox mailbox, string address) : IMailSender
    {
        public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            using (mailbox)
            {
                await mailbox.SendAsync(MailMessages.Build(mail, new MailboxAddress(mail.FromName, address)), ct);
            }
        }
    }
}

/// <summary>Hält die Verbindungen zu den Spa-Postfächern offen und baut sie nach einer Änderung neu auf.</summary>
internal sealed class SpaMailboxRegistry(ISpaMailboxFactory factory) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset Version, IMailbox Mailbox)> _open = new();

    public IMailbox Get(SpaMailboxSettings settings, DateTimeOffset version)
    {
        if (_open.TryGetValue(settings.Id, out var entry) && entry.Version == version)
        {
            return entry.Mailbox;
        }

        var mailbox = factory.Create(settings);
        if (_open.TryGetValue(settings.Id, out var stale))
        {
            (stale.Mailbox as IDisposable)?.Dispose();
        }

        _open[settings.Id] = (version, mailbox);
        return mailbox;
    }

    /// <summary>Verbindungen zu gelöschten oder abgeschalteten Postfächern schließen.</summary>
    public void Retain(IReadOnlyCollection<Guid> ids)
    {
        foreach (var id in _open.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            if (_open.TryRemove(id, out var entry))
            {
                (entry.Mailbox as IDisposable)?.Dispose();
            }
        }
    }

    /// <summary>Zahl der offenen Verbindungen; die Tests der Rolle <c>Api</c> prüfen damit, dass keine offen bleibt.</summary>
    public int OpenCount => _open.Count;

    public void Dispose() => Retain([]);
}

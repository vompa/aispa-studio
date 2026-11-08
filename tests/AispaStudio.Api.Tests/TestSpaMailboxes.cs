using System.Collections.Concurrent;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Mailboxes;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Spa-Postfächer im Speicher: je Adresse ein Test-Postfach. Merkt sich die Einstellungen, mit denen es gebaut wurde, und
/// was aus dem Postfach gesendet wurde.
/// </summary>
public sealed class TestSpaMailboxes : ISpaMailboxFactory
{
    private readonly ConcurrentDictionary<string, TestInbox> _inboxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SpaMailboxSettings> _settings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adressen, deren Abruf fehlschlägt (wie ein falsches Passwort).</summary>
    public ConcurrentDictionary<string, string> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public TestInbox Inbox(string address) => _inboxes.GetOrAdd(address, _ => new TestInbox());

    public SpaMailboxSettings? SettingsOf(string address) => _settings.GetValueOrDefault(address);

    /// <summary>Adressen, deren Versand fehlschlägt.</summary>
    public ConcurrentDictionary<string, string> FailingSend { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adressen, bei denen die Mail rausgeht, aber die Kopie in „Gesendet“ fehlschlägt.</summary>
    public ConcurrentDictionary<string, string> FailingCopy { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Aus dem Postfach gesendete Mails mit den Einstellungen, mit denen gesendet wurde.</summary>
    public ConcurrentQueue<(SpaMailboxSettings Settings, OutgoingMail Mail)> Sent { get; } = new();

    public IMailSender CreateSender(SpaMailboxSettings settings) => new Sender(this, settings);

    public Task VerifySenderAsync(SpaMailboxSettings settings, CancellationToken ct) =>
        FailingSend.TryGetValue(settings.Address, out var error) ? throw new InvalidOperationException(error) : Task.CompletedTask;

    public IMailbox Create(SpaMailboxSettings settings)
    {
        _settings[settings.Address] = settings;
        return new Source(this, settings.Address);
    }

    private sealed class Sender(TestSpaMailboxes owner, SpaMailboxSettings settings) : IMailSender
    {
        public Task SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            if (owner.FailingSend.TryGetValue(settings.Address, out var error))
            {
                throw new InvalidOperationException(error);
            }

            owner.Sent.Enqueue((settings, mail));
            return owner.FailingCopy.TryGetValue(settings.Address, out var copyError)
                ? throw new SentCopyFailedException(new InvalidOperationException(copyError))
                : Task.CompletedTask;
        }
    }

    private sealed class Source(TestSpaMailboxes owner, string address) : IMailbox
    {
        public Task<IReadOnlyList<IncomingMail>> FetchNewAsync(CancellationToken ct) =>
            owner.Failing.TryGetValue(address, out var error)
                ? throw new InvalidOperationException(error)
                : owner.Inbox(address).FetchNewAsync(ct);

        public Task MarkProcessedAsync(string messageId, CancellationToken ct) => owner.Inbox(address).MarkProcessedAsync(messageId, ct);
    }
}

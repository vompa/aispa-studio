using System.Collections.Concurrent;
using AispaStudio.Api.Mail;

namespace AispaStudio.Api.Tests;

/// <summary>Fängt Mails ab, statt sie zu verschicken. Mails laufen über die Queue, deshalb wartet <see cref="WaitForAsync"/>.</summary>
public sealed class TestMailbox : IMailSender
{
    private readonly ConcurrentQueue<OutgoingMail> _mails = new();
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Die ersten <paramref name="times"/> Versuche an diese Adresse werfen, wie ein ausgefallener Mailserver.</summary>
    public void FailFor(string to, int times = int.MaxValue)
    {
        _attempts[to] = 0;
        _failures[to] = times;
    }

    public int AttemptsFor(string to) => _attempts[to];

    public Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        if (_failures.TryGetValue(mail.To, out var failures) && _attempts.AddOrUpdate(mail.To, 1, (_, count) => count + 1) <= failures)
        {
            throw new InvalidOperationException("Mailserver im Test ausgefallen.");
        }

        _mails.Enqueue(mail);
        return Task.CompletedTask;
    }

    /// <summary>Wartet auf die Mail; <paramref name="wait"/> ist die längste Zeit (Standard 15 Sekunden), die Queue braucht bei voller Last von vielen parallelen Tests länger.</summary>
    public async Task<OutgoingMail> WaitForAsync(string to, Func<OutgoingMail, bool> match, CancellationToken ct, TimeSpan? wait = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(wait ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            if (_mails.FirstOrDefault(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && match(m)) is { } mail)
            {
                return mail;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    public IReadOnlyList<OutgoingMail> To(string to) =>
        [.. _mails.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Empfänger in der Reihenfolge des Versands, nur die genannten Adressen.</summary>
    public IReadOnlyList<string> OrderOf(params string[] to) =>
        [.. _mails.Select(m => m.To).Where(a => to.Contains(a, StringComparer.OrdinalIgnoreCase))];
}

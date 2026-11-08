using System.Collections.Concurrent;
using AispaStudio.Api.MailIntake.Channels;

namespace AispaStudio.Api.Tests;

/// <summary>Fängt SMS und WhatsApp-Nachrichten ab, statt sie zu verschicken.</summary>
public sealed class TestTextSender : ITextMessageSender
{
    private readonly ConcurrentQueue<OutgoingText> _messages = new();

    public Task SendAsync(OutgoingText message, CancellationToken ct)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<OutgoingText> To(string number) => [.. _messages.Where(m => m.To == number)];
}

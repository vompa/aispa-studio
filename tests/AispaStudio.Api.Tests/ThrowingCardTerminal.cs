using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Das Testterminal; beim <see cref="FailingAmount"/> wirft es und zählt mit, wie oft es angesprochen wurde.</summary>
internal sealed class ThrowingCardTerminal : ICardTerminal
{
    public const decimal FailingAmount = 77.77m;

    private readonly TestCardTerminal _inner = new();
    private int _failedAttempts;

    public TerminalProvider Provider => _inner.Provider;

    public int FailedAttempts => Volatile.Read(ref _failedAttempts);

    public Task<CardTerminalResult?> PayAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct)
    {
        if (payment.Amount == FailingAmount)
        {
            Interlocked.Increment(ref _failedAttempts);
            throw new InvalidOperationException("Terminal im Test nicht erreichbar.");
        }

        return _inner.PayAsync(terminal, payment, ct);
    }

    public Task<CardTerminalResult?> ReverseAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        _inner.ReverseAsync(terminal, payment, ct);

    public Task<CardTerminalResult?> RefundAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        _inner.RefundAsync(terminal, payment, ct);
}

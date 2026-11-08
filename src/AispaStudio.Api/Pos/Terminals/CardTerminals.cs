using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Attributes;

namespace AispaStudio.Api.Pos.Terminals;

/// <summary>Kartenterminal an einem Standort.</summary>
public sealed class PaymentTerminal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public required string Name { get; set; }
    public TerminalProvider Provider { get; init; }
    public string? Address { get; set; }

    /// <summary>Wie die Windows-App mit einem Gerät im Netz spricht.</summary>
    public TerminalProtocol Protocol { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Eine Zahlung am Terminal für eine offene Rechnung. Sie wird beim Kassieren genau einem Beleg zugeordnet;
/// genehmigt, aber ohne Beleg, sperrt sie den Tagesabschluss, weil Geld geflossen ist.
/// </summary>
public sealed class CardPayment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid BillId { get; init; }
    public Guid TerminalId { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public CardPaymentStatus Status { get; set; }
    public string? Message { get; set; }
    public string? CardBrand { get; set; }
    public string? MaskedPan { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? Reference { get; set; }
    public Guid? ReceiptId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? UserId { get; init; }
}

/// <summary>
/// Anbindung an ein Terminal. Ein Ergebnis gleich beim Start (Testterminal, Cloud-Schnittstellen) oder null, wenn es später
/// gemeldet wird (Gerät im Netz am Empfang über die Windows-App). ZVT, OPI und Cloud-Anbieter kommen als weitere Umsetzungen dazu.
/// </summary>
public interface ICardTerminal
{
    TerminalProvider Provider { get; }

    Task<CardTerminalResult?> PayAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct);

    Task<CardTerminalResult?> ReverseAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct);

    /// <summary>Gutschrift auf die Karte, nachdem der Beleg storniert wurde (auch nach dem Kassenschnitt des Terminals möglich).</summary>
    Task<CardTerminalResult?> RefundAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct);
}

/// <summary>Testterminal ohne Gerät. Beträge auf ,13 werden abgelehnt, damit sich auch der Fehlerfall vorführen lässt.</summary>
internal sealed class TestCardTerminal : ICardTerminal
{
    public TerminalProvider Provider => TerminalProvider.Test;

    public Task<CardTerminalResult?> PayAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(payment.Amount * 100 % 100 == 13
            ? new(false, "Abgelehnt (Testbetrag endet auf 13 Cent).", null, null, null, null)
            : new(true, "Genehmigt (Testterminal).", "Testkarte", "**** **** **** 0000",
                $"{payment.Id.GetHashCode() & 0xFFFFF:000000}"[..6], $"TEST-{payment.Id:N}"[..17]));

    public Task<CardTerminalResult?> ReverseAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(new(true, "Storniert (Testterminal).", null, null, null, null));

    public Task<CardTerminalResult?> RefundAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(new(true, "Gutgeschrieben (Testterminal).", null, null, null, null));
}

/// <summary>Gerät im Netz am Empfang: die Windows-App holt offene Aufträge ab und meldet das Ergebnis.</summary>
internal sealed class LocalCardTerminal : ICardTerminal
{
    public TerminalProvider Provider => TerminalProvider.Local;

    public Task<CardTerminalResult?> PayAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(null);

    public Task<CardTerminalResult?> ReverseAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(null);

    public Task<CardTerminalResult?> RefundAsync(PaymentTerminal terminal, CardPayment payment, CancellationToken ct) =>
        Task.FromResult<CardTerminalResult?>(null);
}

/// <summary>Nachricht in der Queue: Zahlung oder Storno an das Terminal schicken. So wartet kein HTTP-Aufruf auf das Gerät.</summary>
public sealed record SendToCardTerminal(Guid CardPaymentId);

// Nicht wiederholen: ein zweiter Lauf nach einem Fehler beim Speichern schickt Zahlung oder Gutschrift ein zweites Mal ans Gerät, ein Terminal kennt keinen Idempotenzschlüssel.
[MoveToErrorQueueOn(typeof(Exception))]
public static class SendToCardTerminalHandler
{
    public static async Task Handle(SendToCardTerminal message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.CardPayments.Where(c => c.Id == message.CardPaymentId), ct) is not { } tenantId)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        await services.GetRequiredService<CardTerminalService>().SendAsync(message.CardPaymentId, ct);
    }
}

/// <summary>
/// Nach dem Storno eines Belegs: Kartenzahlungen, die über ein angebundenes Terminal liefen, auf die Karte gutschreiben.
/// Der Stornobeleg weist die Kartenzahlung schon negativ aus; hier fließt das Geld tatsächlich zurück.
/// </summary>
public static class CardRefundOnVoidHandler
{
    public static async Task Handle(ReceiptVoided message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.CardPayments.Where(c => c.ReceiptId == message.ReceiptId), ct) is not { } tenantId)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        await services.GetRequiredService<CardTerminalService>().RefundAfterVoidAsync(message.ReceiptId, ct);
    }
}

internal sealed class CardTerminalService(AispaDbContext db, IEnumerable<ICardTerminal> terminals, IMessageBus bus, TimeProvider time)
{
    public async Task<PosResult<CardPaymentDto>> StartAsync(Guid billId, CardPaymentRequest request, string? userId, CancellationToken ct)
    {
        var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null)
        {
            return new PosError(PosErrorKind.NotFound, "billId", "Rechnung nicht gefunden.");
        }

        var register = await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == bill.RegisterId, ct);
        var terminal = await db.PaymentTerminals.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TerminalId && t.LocationId == register.LocationId && t.IsActive, ct);
        if (terminal is null)
        {
            return new PosError(PosErrorKind.Invalid, nameof(request.TerminalId), "Kein aktives Terminal an diesem Standort.");
        }

        if (bill.Status != BillStatus.Open)
        {
            return new PosError(PosErrorKind.Conflict, "", "Die Rechnung ist schon abgeschlossen.");
        }

        // Offen ist, was weder bezahlt noch schon am Terminal genehmigt oder unterwegs ist.
        var open = bill.Lines.Sum(l => l.Amount - l.PaidAmount);
        var reserved = await db.CardPayments.Where(c => c.BillId == billId && c.ReceiptId == null
                && (c.Status == CardPaymentStatus.Pending || c.Status == CardPaymentStatus.Approved))
            .SumAsync(c => c.Amount, ct);
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount || request.Amount > open - reserved)
        {
            return new PosError(PosErrorKind.Invalid, nameof(request.Amount),
                $"Betrag zwischen 0,01 und {open - reserved:0.00}; mehr ist auf der Rechnung nicht offen.");
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == register.LocationId, ct);
        var payment = new CardPayment
        {
            BillId = billId,
            TerminalId = terminal.Id,
            Amount = request.Amount,
            Currency = location.Currency,
            Status = CardPaymentStatus.Pending,
            CreatedAt = time.GetUtcNow(),
            UserId = userId,
        };
        db.CardPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new SendToCardTerminal(payment.Id));
        return new PosResult<CardPaymentDto>(ToDto(payment), null);
    }

    /// <summary>Aus der Queue: Auftrag an das Terminal; liefert es gleich ein Ergebnis, wird es gespeichert.</summary>
    public async Task SendAsync(Guid id, CancellationToken ct)
    {
        var payment = await db.CardPayments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (payment?.Status is not (CardPaymentStatus.Pending or CardPaymentStatus.ReversalPending or CardPaymentStatus.RefundPending))
        {
            return;
        }

        var terminal = await db.PaymentTerminals.AsNoTracking().FirstAsync(t => t.Id == payment.TerminalId, ct);
        var driver = terminals.Single(t => t.Provider == terminal.Provider);
        var result = payment.Status switch
        {
            CardPaymentStatus.Pending => await driver.PayAsync(terminal, payment, ct),
            CardPaymentStatus.ReversalPending => await driver.ReverseAsync(terminal, payment, ct),
            _ => await driver.RefundAsync(terminal, payment, ct),
        };
        if (result is not null)
        {
            Apply(payment, result);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Genehmigte Kartenzahlungen eines stornierten Belegs zur Gutschrift an ihr Terminal schicken.</summary>
    public async Task RefundAfterVoidAsync(Guid receiptId, CancellationToken ct)
    {
        var payments = await db.CardPayments.Where(c => c.ReceiptId == receiptId && c.Status == CardPaymentStatus.Approved).ToListAsync(ct);
        foreach (var payment in payments)
        {
            payment.Status = CardPaymentStatus.RefundPending;
            payment.Message = "Beleg storniert: Gutschrift am Terminal läuft.";
        }

        await db.SaveChangesAsync(ct);
        foreach (var payment in payments)
        {
            await bus.PublishAsync(new SendToCardTerminal(payment.Id));
        }
    }

    public async Task<PosResult<CardPaymentDto>> CancelAsync(Guid id, CancellationToken ct)
    {
        var payment = await db.CardPayments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (payment is null)
        {
            return new PosError(PosErrorKind.NotFound, "cardPaymentId", "Kartenzahlung nicht gefunden.");
        }

        switch (payment.Status)
        {
            case CardPaymentStatus.Pending:
                payment.Status = CardPaymentStatus.Cancelled;
                payment.Message = "An der Kasse abgebrochen.";
                payment.CompletedAt = time.GetUtcNow();
                break;
            case CardPaymentStatus.Approved when payment.ReceiptId is null:
                // Geld ist geflossen: Storno am Terminal, nicht einfach verwerfen.
                payment.Status = CardPaymentStatus.ReversalPending;
                await db.SaveChangesAsync(ct);
                await bus.PublishAsync(new SendToCardTerminal(payment.Id));
                return new PosResult<CardPaymentDto>(ToDto(payment), null);
            case CardPaymentStatus.Approved:
                return new PosError(PosErrorKind.Conflict, "", "Die Zahlung steht schon auf einem Beleg; dafür den Beleg stornieren.");
            default:
                return new PosError(PosErrorKind.Conflict, "", "Diese Zahlung ist schon abgeschlossen.");
        }

        await db.SaveChangesAsync(ct);
        return new PosResult<CardPaymentDto>(ToDto(payment), null);
    }

    /// <summary>
    /// Ergebnis vom Gerät, gemeldet von der Windows-App. Es zählt immer, was das Terminal sagt: eine Genehmigung nach einem
    /// Abbruch an der Kasse wird trotzdem gespeichert, damit sie kassiert oder storniert wird und nicht verloren geht.
    /// </summary>
    public async Task<PosResult<CardPaymentDto>> ReportAsync(Guid id, CardTerminalResult result, CancellationToken ct)
    {
        var payment = await db.CardPayments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (payment is null)
        {
            return new PosError(PosErrorKind.NotFound, "cardPaymentId", "Kartenzahlung nicht gefunden.");
        }

        if (payment.Status is not (CardPaymentStatus.Pending or CardPaymentStatus.ReversalPending or CardPaymentStatus.RefundPending
                or CardPaymentStatus.Cancelled)
            || (payment.Status == CardPaymentStatus.Cancelled && !result.Approved))
        {
            return new PosError(PosErrorKind.Conflict, "", "Für diese Zahlung ist kein Ergebnis offen.");
        }

        if (payment.Status == CardPaymentStatus.Cancelled)
        {
            payment.Status = CardPaymentStatus.Pending;
            Apply(payment, result with { Message = "Nach dem Abbruch an der Kasse doch genehmigt: kassieren oder stornieren." });
        }
        else
        {
            Apply(payment, result);
        }

        await db.SaveChangesAsync(ct);
        return new PosResult<CardPaymentDto>(ToDto(payment), null);
    }

    /// <summary>Aufträge für ein Gerät im Netz am Empfang: zu bezahlen, zu stornieren oder gutzuschreiben.</summary>
    public async Task<List<CardPaymentDto>> JobsAsync(Guid terminalId, CancellationToken ct) =>
        (await db.CardPayments.AsNoTracking()
            .Where(c => c.TerminalId == terminalId && (c.Status == CardPaymentStatus.Pending || c.Status == CardPaymentStatus.ReversalPending
                || c.Status == CardPaymentStatus.RefundPending))
            .OrderBy(c => c.CreatedAt).ToListAsync(ct))
        .Select(ToDto).ToList();

    private void Apply(CardPayment payment, CardTerminalResult result)
    {
        if (payment.Status == CardPaymentStatus.RefundPending)
        {
            payment.Status = result.Approved ? CardPaymentStatus.Refunded : CardPaymentStatus.RefundFailed;
            payment.Message = result.Approved
                ? result.Message ?? "Am Terminal gutgeschrieben."
                : $"Gutschrift am Terminal fehlgeschlagen ({result.Message ?? "ohne Grund"}). Bitte direkt am Terminal gutschreiben.";
        }
        else if (payment.Status == CardPaymentStatus.ReversalPending)
        {
            payment.Status = result.Approved ? CardPaymentStatus.Reversed : CardPaymentStatus.Approved;
            payment.Message = result.Message ?? (result.Approved ? "Am Terminal storniert." : "Storno am Terminal fehlgeschlagen.");
        }
        else
        {
            payment.Status = result.Approved ? CardPaymentStatus.Approved : CardPaymentStatus.Declined;
            payment.Message = result.Message;
            payment.CardBrand = Trim(result.CardBrand, 40);
            payment.MaskedPan = Trim(result.MaskedPan, 30);
            payment.AuthorizationCode = Trim(result.AuthorizationCode, 20);
            payment.Reference = Trim(result.Reference, 100);
        }

        payment.Message = Trim(payment.Message, 500);
        payment.CompletedAt = time.GetUtcNow();
    }

    private static string? Trim(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var t && t.Length > length ? t[..length] : t;

    public static CardPaymentDto ToDto(CardPayment c) => new(c.Id, c.BillId, c.TerminalId, c.Amount, c.Currency, c.Status, c.Message,
        c.CardBrand, c.MaskedPan, c.AuthorizationCode, c.Reference, c.ReceiptId, c.CreatedAt, c.CompletedAt);
}

internal static class TerminalModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<PaymentTerminal>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100);
            e.Property(t => t.Provider).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Address).HasMaxLength(200);
            e.Property(t => t.Protocol).HasConversion<string>().HasMaxLength(8);
            e.HasOne<Location>().WithMany().HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CardPayment>(e =>
        {
            e.Property(c => c.Amount).HasPrecision(10, 2);
            e.Property(c => c.Currency).HasMaxLength(3).IsFixedLength();
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Message).HasMaxLength(500);
            e.Property(c => c.CardBrand).HasMaxLength(40);
            e.Property(c => c.MaskedPan).HasMaxLength(30);
            e.Property(c => c.AuthorizationCode).HasMaxLength(20);
            e.Property(c => c.Reference).HasMaxLength(100);
            e.Property(c => c.UserId).HasMaxLength(450);
            e.HasIndex(c => c.BillId);
            e.HasIndex(c => new { c.TerminalId, c.Status });
            e.HasOne<Bill>().WithMany().HasForeignKey(c => c.BillId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentTerminal>().WithMany().HasForeignKey(c => c.TerminalId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(c => c.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

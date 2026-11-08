using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Anzahlung retournieren (ADR 0073, Nachtrag zu Ä47): das Geld geht nie an der Kassa vorbei zurück, sondern über den Storno-Beleg des Anzahlungsbelegs
/// (<see cref="PosService.VoidAsync"/>, mit Signatur und im Tagesabschluss). Der Storno-Beleg spiegelt die Zahlung der Einzahlung (Karte, Bar, Gutschein), also geht
/// das Geld auf demselben Weg zurück; eine Online-Zahlung erstattet danach der Zahlungsanbieter über das Ereignis <see cref="ReceiptVoided"/>, ein Kartenterminal der
/// Standort wie bei jedem Storno. Zwei Wege führen hierher: die Absage durch den Betrieb (Schließzeit) retourniert automatisch, in allen anderen Fällen retourniert
/// ein Manager oder der Inhaber von Hand, mit Grund (<see cref="DepositReturnRequest"/>).
/// </summary>
internal sealed class DepositReturns(AispaDbContext db, PosService pos, IMessageBus bus)
{
    /// <summary>Das Ergebnis: der zurückgezahlte Betrag und der Storno-Beleg.</summary>
    public sealed record Returned(decimal Amount, ReceiptDto VoidReceipt)
    {
        /// <summary>Der Zahlungsweg der Rückzahlung, wie der der Einzahlung: „Bar“, „Karte“, „Bar + Karte“.</summary>
        public string Way => PaymentMethods.Way(VoidReceipt.Payments.Select(p => p.Method));

        /// <summary>Die Anzahlung war ganz bar bezahlt: der Gast bekommt das Geld bar zurück (Ä53).</summary>
        public bool Cash => VoidReceipt.Payments.Count > 0 && VoidReceipt.Payments.All(p => p.Method == PaymentMethod.Cash);
    }

    /// <summary>Die offenen Anzahlungen eines Termins: bezahlt, beim Beleg der Behandlung noch nicht verrechnet und der Anzahlungsbeleg nicht storniert. Höchstens eine je Termin.</summary>
    public async Task<List<AppointmentDeposit>> OpenAsync(Guid appointmentId, CancellationToken ct) =>
        await db.AppointmentDeposits.AsNoTracking()
            .Where(d => d.AppointmentId == appointmentId && d.VoidedAt == null && d.SettledOnReceiptId == null)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync(ct);

    /// <summary>
    /// Die Angaben für den Knopf „Anzahlung retournieren“ am Termin; <c>null</c>, wenn es keine offene Anzahlung gibt. <paramref name="canReturn"/> ist das Recht der angemeldeten
    /// Person (nur Inhaber und Manager).
    /// </summary>
    public async Task<DepositReturnInfo?> InfoAsync(Guid appointmentId, bool canReturn, CancellationToken ct)
    {
        if (await OpenAsync(appointmentId, ct) is not [{ } deposit, ..])
        {
            return null;
        }

        var receipt = await db.Receipts.AsNoTracking().Include(r => r.Payments).FirstAsync(r => r.Id == deposit.ReceiptId, ct);
        var register = await db.CashRegisters.AsNoTracking().Where(c => c.Id == receipt.RegisterId).Select(c => new { c.Code, c.LocationId }).FirstAsync(ct);
        var currency = await db.Locations.AsNoTracking().Where(l => l.Id == register.LocationId).Select(l => l.Currency).FirstAsync(ct);
        var way = PaymentMethods.Way(receipt.Payments.Select(p => p.Method));
        return new DepositReturnInfo(receipt.Id, $"{register.Code} Nr. {receipt.Number}, {receipt.IssuedAt:dd.MM.yyyy}", deposit.Amount, way, receipt.IssuedAt, canReturn, currency);
    }

    /// <summary>
    /// Retourniert die offene Anzahlung des Termins: Storno-Beleg über die Kassa (als Kassenaufsicht des Systems, der Grund steht auf dem Beleg) und das Ereignis, auf
    /// das Online-Zahlung und Kartenterminal hören. Ohne offene Anzahlung <see cref="PosErrorKind.NotFound"/>; schlägt das Storno fehl (z. B. Kassa außer Betrieb), steht der Fehler im Ergebnis
    /// und nichts hat sich geändert.
    /// </summary>
    public async Task<PosResult<Returned>> ReturnAsync(Guid appointmentId, string reason, string? userId, CancellationToken ct)
    {
        if (await OpenAsync(appointmentId, ct) is not [{ } deposit, ..])
        {
            return new PosError(PosErrorKind.NotFound, "appointmentId", "Für diesen Termin gibt es keine offene Anzahlung.");
        }

        var voided = await pos.VoidAsync(deposit.ReceiptId, reason, userId, supervisor: true, ct);
        if (voided.Value is not { } receipt)
        {
            return voided.Error!;
        }

        // Z. B. erstattet der Kundenbereich daraufhin eine Online-Zahlung (ADR 0038).
        await bus.PublishAsync(new ReceiptVoided(deposit.ReceiptId, receipt.Id, reason, userId));
        return new PosResult<Returned>(new Returned(deposit.Amount, receipt), null);
    }
}

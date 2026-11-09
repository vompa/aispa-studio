using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Zahlungen als Sätze für den Verlauf (Arbeitsanweisung Verlauf, Aufgabe A): Verkaufsbelege mit Termin oder Ware („Bezahlt bar: 79,00 € von Lena
/// (Beleg WIEN-1 Nr. 12)“), Anzahlungsbelege („Anzahlung 23,70 € Karte …“) und deren Storno-Belege („Beleg WIEN-1 Nr. 12 storniert von Lena: Grund“). Die Kassa bleibt
/// die Quelle (RKSV/TSE); hier wird nur gelesen. Ein Beleg bekommt als Seitenzeiger eine negative Pseudo-Nummer aus seiner Id (<see cref="PseudoSeq"/>), damit er
/// mit den Zeilen aus <c>sync.history</c> in einer Reihenfolge steht.
/// </summary>
public sealed class PaymentHistory(AispaDbContext db)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Eine Zahlung im Verlauf: Satz, Art und die Verweise auf Termin, Gast und Behandlung.</summary>
    public sealed record PaymentEvent(
        long Seq, Guid ReceiptId, DateTimeOffset At, ActivityKind Kind, string Text, string? UserId, string? Actor, ActivityChannel Channel,
        Guid? CustomerId, Guid? AppointmentId, Guid? TreatmentId);

    /// <summary>Die ersten 8 Bytes der Beleg-Id als negative Zahl; Belege mit Id nach Version 7 stehen damit in zeitlicher Reihenfolge. Gegenstück in SQL: <see cref="PseudoSeqSql"/>.</summary>
    public static long PseudoSeq(Guid receiptId) => -long.Parse(receiptId.ToString("N")[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Dieselbe Zahl in SQL für die Belegtabelle mit dem Alias <c>r</c>.</summary>
    public const string PseudoSeqSql = """-(('x' || left(replace(r."Id"::text, '-', ''), 16))::bit(64)::bigint)""";

    /// <summary>Die Zahlungen eines Termins: Verkauf, Anzahlung und Stornos davon; neueste zuerst.</summary>
    public async Task<List<PaymentEvent>> ForAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var sales = await db.Receipts.AsNoTracking()
            .Where(r => r.Kind == ReceiptKind.Sale && r.Lines.Any(l => l.AppointmentId == appointmentId))
            .Select(r => r.Id).ToListAsync(ct);
        var deposits = await db.AppointmentDeposits.AsNoTracking().Where(d => d.AppointmentId == appointmentId).Select(d => d.ReceiptId).ToListAsync(ct);
        var originals = sales.Concat(deposits).Distinct().ToList();
        var voids = await db.Receipts.AsNoTracking().Where(r => r.VoidsReceiptId != null && originals.Contains(r.VoidsReceiptId.Value)).Select(r => r.Id).ToListAsync(ct);
        var events = await ForReceiptsAsync([.. originals, .. voids], ct);
        return [.. events.OrderByDescending(e => e.At).ThenByDescending(e => e.Seq)];
    }

    /// <summary>Sätze zu den genannten Belegen, ohne Reihenfolge; ein Verkauf ohne Termin nennt dazu die Ware.</summary>
    public async Task<List<PaymentEvent>> ForReceiptsAsync(IReadOnlyCollection<Guid> receiptIds, CancellationToken ct)
    {
        if (receiptIds.Count == 0)
        {
            return [];
        }

        var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery()
            .Where(r => receiptIds.Contains(r.Id)).ToListAsync(ct);
        var originalIds = receipts.Select(r => r.VoidsReceiptId).OfType<Guid>().Where(id => receipts.All(r => r.Id != id)).ToList();
        var originals = await db.Receipts.AsNoTracking().Include(r => r.Lines).AsSplitQuery().Where(r => originalIds.Contains(r.Id)).ToListAsync(ct);
        var all = receipts.Concat(originals).ToDictionary(r => r.Id);
        var allIds = all.Keys.ToList();
        var deposits = await db.AppointmentDeposits.AsNoTracking().Where(d => allIds.Contains(d.ReceiptId)).ToListAsync(ct);
        var depositByReceipt = deposits.GroupBy(d => d.ReceiptId).ToDictionary(g => g.Key, g => g.First());
        var registerIds = all.Values.Select(r => r.RegisterId).Distinct().ToList();
        var registers = await db.CashRegisters.AsNoTracking().Where(c => registerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var appointmentIdsOfAll = all.Values.SelectMany(r => r.Lines).Select(l => l.AppointmentId).Concat(deposits.Select(d => (Guid?)d.AppointmentId)).OfType<Guid>().Distinct().ToList();
        var treatments = await db.Appointments.AsNoTracking().Where(a => appointmentIdsOfAll.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.TreatmentId, ct);
        var userIds = receipts.Select(r => r.CreatedByUserId).OfType<string>().Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var events = new List<PaymentEvent>();
        foreach (var receipt in receipts)
        {
            var original = receipt.VoidsReceiptId is { } voided && all.TryGetValue(voided, out var o) ? o : receipt;
            var deposit = depositByReceipt.GetValueOrDefault(original.Id);
            var appointmentIds = original.Lines.Select(l => l.AppointmentId).OfType<Guid>().Distinct().ToList();
            // Ein Verkauf ohne Termin (Gutschein, Artikel, freie Zeile) nennt die Ware, sonst wüsste niemand, wofür bezahlt wurde.
            var goods = deposit is null && appointmentIds.Count == 0 ? $" für {Goods(original.Lines)}" : "";

            // Der Anzahlungsbeleg einer Online-Zahlung trägt einen Pseudo-Benutzer; er steht ohne Person als „online“.
            var actor = receipt.CreatedByUserId is { } userId && userId != ReceiptOnlineDepositHandler.OnlineUser ? people.GetValueOrDefault(userId, Sync.HistoryActor.DeletedPerson) : null;
            var by = actor is null ? "" : $" von {actor}";
            var code = registers.GetValueOrDefault(original.RegisterId, "–");
            var kind = receipt.Kind == ReceiptKind.Void ? ActivityKind.ReceiptVoided : deposit is not null ? ActivityKind.Deposit : ActivityKind.Paid;
            var text = kind switch
            {
                ActivityKind.ReceiptVoided => $"Beleg {code} Nr. {original.Number} storniert{by}: {receipt.VoidReason}",
                ActivityKind.Deposit => $"Anzahlung {Money(deposit!.Amount)} {Way(receipt.Payments)}{by} (Beleg {code} Nr. {receipt.Number})",
                _ => $"Bezahlt {Way(receipt.Payments)}: {Money(receipt.Total)}{by}{goods} (Beleg {code} Nr. {receipt.Number})",
            };
            var channel = actor is not null ? ActivityChannel.Reception : kind == ActivityKind.Deposit ? ActivityChannel.Online : ActivityChannel.Background;
            var appointmentId = deposit?.AppointmentId ?? (appointmentIds.Count == 1 ? appointmentIds[0] : null);
            var treatmentId = appointmentId is { } a && treatments.TryGetValue(a, out var t) ? t : (Guid?)null;
            events.Add(new PaymentEvent(PseudoSeq(receipt.Id), receipt.Id, receipt.IssuedAt, kind, text, receipt.CreatedByUserId, actor, channel, receipt.CustomerId,
                appointmentId, treatmentId));
        }

        return events;
    }

    /// <summary>„bar“, „Karte“, „bar + Gutschein“: die Zahlungsarten des Belegs wie <see cref="PaymentMethods.Way"/>, Bargeld klein als Umstandswort.</summary>
    internal static string Way(IEnumerable<ReceiptPayment> payments)
    {
        var ways = payments.Select(p => p.Method).Distinct().Select(m => m == PaymentMethod.Cash ? "bar" : PaymentMethods.Label(m)).ToList();
        return ways.Count == 0 ? "ohne Zahlung" : string.Join(" + ", ways);
    }

    /// <summary>„Tee“, „Gutschein 50 € und 2 weitere“: die erste Zeile des Belegs und wie viele noch folgen.</summary>
    private static string Goods(List<ReceiptLine> lines)
    {
        var first = lines.OrderBy(l => l.Position).Select(l => l.Description).FirstOrDefault() ?? "–";
        return lines.Count > 1 ? $"{first} und {lines.Count - 1} weitere" : first;
    }

    private static string Money(decimal amount) => $"{amount.ToString("N2", German)} €";
}

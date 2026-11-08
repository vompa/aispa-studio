using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wie viel ist auf Gutschein 1234 noch drauf?“: Restwert, Gültigkeit und Stand des Gutscheins. Das ist die Auskunft an der Kassa und braucht deshalb dasselbe Recht
/// wie das Nachsehen eines Gutscheins dort (Rezeption aufwärts). Summen über alle Gutscheine sind eine Auswertung mit Beträgen und gehören nicht hierher.
/// </summary>
internal sealed class VoucherAnswers(AispaDbContext db) : IAnswerGiver
{
    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.VoucherBalance];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (!context.Has(AccessRight.Book))
        {
            return Blocks.Denied(question.Topic, "Gutscheine schlägt die Rezeption an der Kassa nach.");
        }

        const string Source = "Gutscheine der Kassa";
        var code = PosService.NormalizeCode(question.Subject);
        if (code.Length == 0)
        {
            return Blocks.NotFound(question.Topic, "Welcher Gutschein? Bitte die Nummer oder den Code vom Gutschein nennen.", Source);
        }

        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code, ct);
        if (voucher is null && code.Replace("-", "", StringComparison.Ordinal).Length >= 4)
        {
            // „1234“ meint den Gutschein, dessen Code so endet, wenn es genau einen gibt.
            var tail = code.Replace("-", "", StringComparison.Ordinal);
            var candidates = await db.Vouchers.AsNoTracking().Where(v => v.Code.Replace("-", "").EndsWith(tail)).Take(2).ToListAsync(ct);
            voucher = candidates.Count == 1 ? candidates[0] : null;
        }

        if (voucher is null)
        {
            return Blocks.NotFound(question.Topic, $"Einen Gutschein „{question.Subject}“ gibt es nicht. Bitte die Nummer prüfen.", Source);
        }

        var state = voucher.Status switch
        {
            VoucherStatus.Redeemed => "Er ist eingelöst.",
            VoucherStatus.Voided => "Er ist storniert.",
            _ => voucher.ValidUntil is { } until && until < context.Today ? $"Er ist seit {until.ToString("dd.MM.yyyy", AnswerContext.German)} abgelaufen." : "",
        };
        var validity = voucher.ValidUntil is { } valid ? $"gültig bis {valid.ToString("dd.MM.yyyy", AnswerContext.German)}" : "ohne Ablaufdatum";
        string text;
        List<IReadOnlyList<string>> rows;
        if (voucher.Units is { } units)
        {
            var treatment = voucher.TreatmentId is { } id ? await db.Treatments.AsNoTracking().Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(ct) : null;
            text = $"Auf dem Paket {voucher.Code} sind noch {voucher.UnitsLeft ?? 0} von {units} Behandlungen offen{(treatment is null ? "" : $" ({treatment})")}, {validity}. {state}".TrimEnd();
            rows = [["Offen", $"{voucher.UnitsLeft ?? 0} von {units}"], ["Gültigkeit", validity]];
        }
        else
        {
            text = $"Auf dem Gutschein {voucher.Code} sind noch {context.Money(voucher.Balance)} (ursprünglich {context.Money(voucher.InitialValue)}), {validity}. {state}".TrimEnd();
            rows = [["Restwert", context.Money(voucher.Balance)], ["Ursprünglicher Wert", context.Money(voucher.InitialValue)], ["Gültigkeit", validity]];
        }

        return Blocks.Answer(question.Topic, text, Source, Blocks.Table(["Angabe", "Wert"], rows));
    }
}

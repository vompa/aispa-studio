using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Hat sie noch einen Gutschein?“, „Offene Rechnung Frau Moser?“ (Ä317): Gutscheine mit Restwert, Pakete mit offenen Einheiten, offene Rechnungen, offene Anzahlungen und
/// der Kundenrabatt eines Gastes. Das ist die Auskunft der Kassa und braucht das Recht zu buchen. Wertgutscheine tragen keinen Kunden: sie gehören zum Gast, wenn sie auf
/// seinem Beleg verkauft oder mit ihm eingelöst wurden. Die Frage nach nur einem Teil zeigt nur diesen Teil.
/// </summary>
internal sealed class GuestBalanceAnswers(AispaDbContext db, GuestFinder finder) : IAnswerGiver
{
    private const string Source = "Kassa: Gutscheine, Rechnungen und Anzahlungen";

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.GuestBalance];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        if (Blocks.RequireCalendar(question.Topic, context) is { } denied)
        {
            return denied;
        }

        if (!context.Has(AccessRight.Book))
        {
            return Blocks.Denied(question.Topic, "Gutscheine, Rechnungen und Anzahlungen sieht die Rezeption an der Kassa.");
        }

        var found = await finder.FindAsync(question, context, Source, ct);
        if (found.Block is { } notFound)
        {
            return notFound;
        }

        if (found.Guests.Count > 1)
        {
            return await finder.ChoiceAsync(question, found, context, Source, ct);
        }

        var guest = found.Guests[0];
        bool Wants(string part) => question.Detail is null || question.Detail == part;
        var items = new List<AssistantItem>();
        var parts = new List<string>();
        if (Wants("voucher"))
        {
            var vouchers = await VouchersAsync(guest.Id, context, ct);
            items.AddRange(vouchers.Items);
            parts.AddRange(vouchers.Parts);
        }

        if (Wants("bill"))
        {
            var bills = await BillsAsync(guest.Id, context, ct);
            items.AddRange(bills);
            Count(parts, bills.Count, "offene Rechnung", "offene Rechnungen");
        }

        if (Wants("deposit"))
        {
            var deposits = await DepositsAsync(guest.Id, context, ct);
            items.AddRange(deposits);
            Count(parts, deposits.Count, "offene Anzahlung", "offene Anzahlungen");
        }

        if (Wants("discount") && await DiscountAsync(guest.Id, ct) is { } discount)
        {
            items.Add(discount);
            parts.Add($"{discount.Title} {discount.Detail}");
        }

        var text = parts.Count > 0 ? $"{guest.Name}: {string.Join(", ", parts)}." : $"{guest.Name}: {Nothing(question.Detail)}.";
        return Blocks.Answer(question.Topic, text, Source, items: items) with { GuestId = guest.Id };
    }

    private static string Nothing(string? detail) => detail switch
    {
        "voucher" => "kein Gutschein und kein Paket mit Restwert",
        "bill" => "keine offene Rechnung",
        "deposit" => "keine offene Anzahlung",
        "discount" => "kein Kundenrabatt",
        _ => "keine Gutscheine, offenen Rechnungen, Anzahlungen oder Kundenrabatt",
    };

    private static void Count(List<string> parts, int count, string one, string many)
    {
        if (count > 0)
        {
            parts.Add($"{count} {(count == 1 ? one : many)}");
        }
    }

    /// <summary>Aktive, nicht abgelaufene Gutscheine des Gastes mit Restwert und Pakete mit offenen Einheiten.</summary>
    private async Task<(List<AssistantItem> Items, List<string> Parts)> VouchersAsync(Guid guestId, AnswerContext context, CancellationToken ct)
    {
        var receipts = db.Receipts.AsNoTracking().Where(r => r.CustomerId == guestId).Select(r => r.Id);
        var redeemed = db.Receipts.AsNoTracking().Where(r => r.CustomerId == guestId).SelectMany(r => r.Payments).Where(p => p.VoucherId != null).Select(p => p.VoucherId);
        var today = context.Today;
        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(v => v.Status == VoucherStatus.Active && (v.ValidUntil == null || v.ValidUntil >= today)
                && (v.CustomerId == guestId || receipts.Contains(v.SoldOnReceiptId) || redeemed.Contains(v.Id)))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Code)
            .ToListAsync(ct);
        var treatmentIds = vouchers.Where(v => v.TreatmentId != null).Select(v => v.TreatmentId!.Value).Distinct().ToList();
        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        string Validity(DateOnly? until) => until is { } valid ? $", gültig bis {valid.ToString("dd.MM.yyyy", AnswerContext.German)}" : "";

        var items = new List<AssistantItem>();
        var packages = 0;
        var values = 0;
        foreach (var voucher in vouchers)
        {
            var treatment = voucher.TreatmentId is { } id ? treatments.GetValueOrDefault(id) : null;
            if (voucher.Units is { } units)
            {
                if ((voucher.UnitsLeft ?? 0) > 0)
                {
                    packages++;
                    items.Add(new($"Paket {treatment ?? voucher.Code}", $"noch {voucher.UnitsLeft} von {units} Einheiten{Validity(voucher.ValidUntil)} · Code {voucher.Code}",
                        null, null, null, ""));
                }
            }
            else if (voucher.Balance > 0)
            {
                values++;
                var title = voucher.Kind == VoucherKind.MultiPurpose ? $"Wertgutschein {voucher.Code}" : $"Gutschein {voucher.Code}";
                items.Add(new(title, $"Rest {context.Money(voucher.Balance)}{(treatment is null ? "" : $" für {treatment}")}{Validity(voucher.ValidUntil)}", null, null, null, ""));
            }
        }

        var parts = new List<string>();
        Count(parts, values, "Gutschein mit Restwert", "Gutscheine mit Restwert");
        Count(parts, packages, "Paket", "Pakete");
        return (items, parts);
    }

    /// <summary>Offene Rechnungen des Gastes an den Kassen der sichtbaren Standorte mit dem noch offenen Betrag.</summary>
    private async Task<List<AssistantItem>> BillsAsync(Guid guestId, AnswerContext context, CancellationToken ct)
    {
        var visible = context.Access.LocationIds.ToList();
        var all = context.Access.AllLocations;
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.CustomerId == guestId && b.Status == BillStatus.Open)
            .Join(db.CashRegisters.AsNoTracking().Where(r => all || visible.Contains(r.LocationId)), b => b.RegisterId, r => r.Id,
                (b, r) => new { b.Id, b.CreatedAt, Register = r.Code, Open = b.Lines.Sum(l => l.Amount - l.PaidAmount) })
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);
        return
        [
            .. bills.Select(b => new AssistantItem("Offene Rechnung",
                $"{context.Money(b.Open)} · Kassa {b.Register} · angelegt am {GuestFinder.Local(b.CreatedAt, context).ToString("dd.MM.yyyy", AnswerContext.German)}",
                null, null, null, "")),
        ];
    }

    /// <summary>
    /// Bezahlte, noch nicht verrechnete und nicht stornierte Anzahlungen auf Termine des Gastes an sichtbaren Standorten; auch auf abgesagten Terminen, denn das Geld liegt
    /// dort, bis jemand es retourniert oder mit der Gebühr verrechnet (<c>DepositReturns</c>).
    /// </summary>
    private async Task<List<AssistantItem>> DepositsAsync(Guid guestId, AnswerContext context, CancellationToken ct)
    {
        var appointments = GuestFinder.Visible(db.Appointments.AsNoTracking(), context)
            .Where(a => a.CustomerId == guestId);
        var deposits = await db.AppointmentDeposits.AsNoTracking()
            .Where(d => d.SettledOnReceiptId == null && d.VoidedAt == null)
            .Join(appointments, d => d.AppointmentId, a => a.Id,
                (d, a) => new { d.Amount, a.Id, a.Start, a.LocationId, a.TreatmentId, a.EmployeeId, Treatment = a.Treatment.Name, a.Status })
            .OrderBy(d => d.Start).ThenBy(d => d.Id)
            .ToListAsync(ct);
        return
        [
            .. deposits.Select(d => new AssistantItem("Anzahlung", $"{context.Money(d.Amount)} für den Termin am {GuestFinder.When(d.Start, context)}{(d.Status == AppointmentStatus.Cancelled ? " (abgesagt)" : "")}", null,
                new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, $"{context.Time(d.Start)} {d.Treatment}", d.LocationId, d.TreatmentId, d.EmployeeId, guestId,
                    d.Id, d.Start), null, "Im Kalender zeigen")),
        ];
    }

    private async Task<AssistantItem?> DiscountAsync(Guid guestId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == guestId).Select(c => new { c.DiscountPercent, c.DiscountScope }).FirstAsync(ct);
        if (customer.DiscountPercent is not { } percent || percent <= 0)
        {
            return null;
        }

        var scope = customer.DiscountScope switch
        {
            CustomerDiscountScope.Treatments => "auf Behandlungen",
            CustomerDiscountScope.Products => "auf Produkte",
            _ => "auf alles",
        };
        return new AssistantItem("Kundenrabatt", $"{percent.ToString("0.##", AnswerContext.German)} % {scope}", null, null, null, "");
    }
}

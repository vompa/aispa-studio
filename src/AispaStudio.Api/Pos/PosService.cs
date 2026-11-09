using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Api.Pos.VatIds;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Kassieren: offene Rechnungen, Bezahlen (auch geteilt und mit Gutscheinen), Storno, Einlagen/Entnahmen und Tagesabschluss.
/// Alles, was einen Beleg erzeugt, läuft in einer Transaktion, die die Kasse sperrt: Belegnummern, Umsatzzähler und Signaturkette bleiben lückenlos,
/// auch wenn an zwei Plätzen gleichzeitig kassiert wird.
/// </summary>
internal sealed partial class PosService(AispaDbContext db, FiscalizationResolver fiscal, TimeProvider time, IOptions<PaymentOptions> paymentOptions,
    SiteIdentity site)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    // ---------- Kassen ----------

    public async Task<PosResult<CashRegisterDto>> CreateRegisterAsync(CashRegisterInput input, CancellationToken ct)
    {
        try
        {
            var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == input.LocationId, ct)
                ?? throw PosFailure.NotFound("Standort");
            if (input.Code is null || !RegisterCode().IsMatch(input.Code))
            {
                throw PosFailure.Invalid(nameof(input.Code), "Kassen-ID aus Buchstaben, Ziffern und Bindestrich, höchstens 50 Zeichen, z. B. „WIEN-1“.");
            }

            if (string.IsNullOrWhiteSpace(input.ReceiptHeader))
            {
                throw PosFailure.Invalid(nameof(input.ReceiptHeader), "Belegkopf mit Firmenname und Anschrift ist Pflicht.");
            }

            var iban = input.Iban?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
            if (!string.IsNullOrEmpty(iban) && !IsValidIban(iban))
            {
                throw PosFailure.Invalid(nameof(input.Iban), "Keine gültige IBAN.");
            }

            if (await db.CashRegisters.AnyAsync(r => r.Code == input.Code, ct))
            {
                throw PosFailure.Invalid(nameof(input.Code), "Diese Kassen-ID gibt es schon.");
            }

            var register = new CashRegister
            {
                LocationId = location.Id,
                Code = input.Code,
                Country = input.Country,
                ReceiptHeader = input.ReceiptHeader.Trim(),
                PayeeName = Blank(input.PayeeName),
                Iban = string.IsNullOrEmpty(iban) ? null : iban,
                Bic = Blank(input.Bic)?.ToUpperInvariant(),
                CreatedAt = time.GetUtcNow(),
                SiteId = site.Get(db),
            };

            var fiscalization = fiscal.For(register.Country);
            if (await fiscalization.InitializeAsync(register, ct) is { } problem)
            {
                throw PosFailure.Invalid(nameof(input.Country), problem);
            }

            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                db.CashRegisters.Add(register);
                await db.SaveChangesAsync(ct);

                // Der Startbeleg (Betrag 0) eröffnet die Signaturkette; ohne funktionierende Signatureinrichtung geht die Kasse nicht in Betrieb.
                var start = await IssueAsync(register, ReceiptKind.Start, ct, automatic: false);
                if (start.SignatureDeviceFailed)
                {
                    throw PosFailure.Invalid(nameof(input.Country), "Die Signatureinrichtung ist nicht erreichbar; der Startbeleg muss signiert sein.");
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });

            return new PosResult<CashRegisterDto>(ToDto(register), null);
        }
        catch (PosFailure f)
        {
            db.ChangeTracker.Clear();
            return f.Error;
        }
    }

    public CashRegisterDto ToDto(CashRegister r)
    {
        var warnings = new List<string>();
        if (r.SignatureDeviceFailedSince is { } since)
        {
            warnings.Add($"Signatureinrichtung ausgefallen seit {since.ToUniversalTime():yyyy-MM-dd HH:mm} UTC. " +
                "Belege tragen den Hinweis „Sicherheitseinrichtung ausgefallen“.");
            if (time.GetUtcNow() - since > TimeSpan.FromHours(48))
            {
                warnings.Add("Der Ausfall dauert länger als 48 Stunden: bitte unverzüglich über FinanzOnline melden.");
            }
        }

        if (!r.IsActive)
        {
            warnings.Add("Kasse außer Betrieb (Schlussbeleg erstellt).");
        }

        if (fiscal.For(r.Country).Notice is { } notice)
        {
            warnings.Add(notice);
        }

        var otherSite = IsOtherSite(r);
        if (otherSite)
        {
            warnings.Add(OtherSiteMessage);
        }

        return new(r.Id, r.LocationId, r.Code, r.Country, r.ReceiptHeader, r.IsActive, r.LastReceiptNumber,
            r.SignatureDeviceFailedSince, r.CertificateSerial, warnings, PaymentMethods.Available(r.Country, paymentOptions.Value), otherSite);
    }

    /// <summary>Nullbeleg auf Wunsch, z. B. zur Prüfung mit der Belegcheck-App.</summary>
    public Task<PosResult<ReceiptDto>> NullReceiptAsync(Guid registerId, CancellationToken ct) =>
        ReceiptTransactionAsync(registerId, async register =>
        {
            EnsureActive(register);
            return await IssueAsync(register, ReceiptKind.Null, ct);
        }, ct);

    /// <summary>Kasse außer Betrieb nehmen: Schlussbeleg, danach sind keine Belege mehr möglich.</summary>
    public Task<PosResult<ReceiptDto>> DecommissionAsync(Guid registerId, CancellationToken ct) =>
        ReceiptTransactionAsync(registerId, async register =>
        {
            EnsureActive(register);
            if (await db.Bills.AnyAsync(b => b.RegisterId == registerId && b.Status == BillStatus.Open && b.Lines.Any(l => l.PaidAmount > 0), ct))
            {
                throw PosFailure.Invalid("bills", "Es gibt noch teilweise bezahlte Rechnungen.");
            }

            var receipt = await IssueAsync(register, ReceiptKind.Closing, ct);
            register.IsActive = false;
            return receipt;
        }, ct);

    // ---------- Rechnungen ----------

    public Task<PosResult<BillDto>> CreateBillAsync(BillInput input, CancellationToken ct) =>
        BillTransactionAsync(input.RegisterId, async register =>
        {
            EnsureActive(register);
            if (input.CustomerId is { } customerId && !await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            {
                throw PosFailure.NotFound("Kunde");
            }

            var bill = new Bill { RegisterId = register.Id, CustomerId = input.CustomerId, CreatedAt = time.GetUtcNow() };
            db.Bills.Add(bill);
            foreach (var appointmentId in input.AppointmentIds ?? [])
            {
                await AddLineAsync(register, bill, new BillLineInput(BillLineKind.Appointment, appointmentId, null, 1, null, 0, null, null, null, null, null), ct);
            }

            foreach (var visitId in input.AreaVisitIds ?? [])
            {
                await AddLineAsync(register, bill, new BillLineInput(BillLineKind.AreaVisit, null, null, 1, null, 0, null, null, null, null, null, visitId), ct);
            }

            return bill.Id;
        }, ct);

    public async Task<PosResult<BillDto>> AddLineAsync(Guid billId, BillLineInput input, bool supervisor, CancellationToken ct) =>
        await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            var line = await AddLineAsync(register, bill, input, ct);
            // Nur ein von Hand gegebener Rabatt; der Kundenrabatt wurde beim Festlegen in der Kundenkartei geprüft.
            if (input.Discount != 0)
            {
                await EnsureDiscountAllowedAsync(register, input.Discount, line.UnitPrice * line.Quantity, supervisor, ct);
            }

            return bill.Id;
        }, ct);

    public async Task<PosResult<BillDto>> RemoveLineAsync(Guid billId, Guid lineId, CancellationToken ct) =>
        await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            var line = bill.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw PosFailure.NotFound("Position");
            if (line.PaidAmount != 0)
            {
                throw PosFailure.Invalid("lineId", "Die Position ist schon (teilweise) bezahlt; zuerst den Beleg stornieren.");
            }

            bill.Lines.Remove(line);
            db.BillLines.Remove(line);
            return bill.Id;
        }, ct);

    /// <summary>
    /// Rabatt einer offenen Position in Prozent oder als Betrag ändern (0 nimmt ihn zurück). Gutscheine werden zum vollen Wert
    /// verkauft, bezahlte Positionen bleiben, wie sie sind. Über der Rabattgrenze des Spas nur mit Kassenaufsicht.
    /// </summary>
    /// <param name="approvedBy">Person mit Kassenaufsicht, die den Rabatt per PIN freigegeben hat; sie gilt dann als <paramref name="supervisor"/>.</param>
    public async Task<PosResult<BillDto>> SetDiscountAsync(Guid billId, Guid lineId, LineDiscountInput input, bool supervisor, string? approvedBy,
        CancellationToken ct) =>
        await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            var line = bill.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw PosFailure.NotFound("Position");
            if (line.PaidAmount != 0)
            {
                throw PosFailure.Invalid("lineId", "Die Position ist schon (teilweise) bezahlt; zuerst den Beleg stornieren.");
            }

            var gross = line.UnitPrice * line.Quantity;
            if (input.Percent is { } percent && (percent < 0 || percent >= 100 || decimal.Round(percent, 2) != percent))
            {
                throw PosFailure.Invalid(nameof(input.Percent), "Rabatt in Prozent zwischen 0 und 99,99.");
            }

            var discount = input.Percent is { } p ? decimal.Round(gross * p / 100, 2, MidpointRounding.AwayFromZero) : input.Discount;
            if (line.Kind is BillLineKind.Voucher or BillLineKind.Deposit && discount != 0)
            {
                throw PosFailure.Invalid(nameof(input.Discount), line.Kind == BillLineKind.Voucher
                    ? "Gutscheine werden zum vollen Wert verkauft."
                    : "Auf eine Anzahlung gibt es keinen Rabatt.");
            }

            if (discount < 0 || discount >= gross || decimal.Round(discount, 2) != discount)
            {
                throw PosFailure.Invalid(nameof(input.Discount), "Rabatt muss zwischen 0 und dem Positionsbetrag liegen.");
            }

            if (discount != 0)
            {
                await EnsureDiscountAllowedAsync(register, discount, gross, supervisor, ct);
            }

            line.Discount = discount;
            line.DiscountNote = discount == 0 ? null : ManualNote(discount, gross, input.Percent);
            line.DiscountApprovedByUserId = discount == 0 ? null : approvedBy;
            line.Amount = gross - discount;
            return bill.Id;
        }, ct);

    /// <summary>
    /// Schließt eine durch Storno wieder geöffnete Rechnung, deren Positionen nicht neu kassiert werden sollen (z. B. online
    /// bezahlt und zurückerstattet): unbezahlte Positionen entfernen. Belege bleiben, deshalb wird die Rechnung nicht gelöscht.
    /// </summary>
    public async Task<PosError?> CloseAfterVoidAsync(Guid billId, CancellationToken ct)
    {
        var result = await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            if (bill.Lines.Any(l => l.PaidAmount != 0))
            {
                throw PosFailure.Invalid("billId", "Auf diese Rechnung ist noch etwas bezahlt.");
            }

            db.BillLines.RemoveRange(bill.Lines);
            bill.Lines.Clear();
            bill.Status = BillStatus.Settled;
            return bill.Id;
        }, ct, loadAfter: false);
        return result.Error;
    }

    /// <summary>Offene Rechnung ohne Zahlungen verwerfen. Das ist kein Umsatz und braucht keinen Beleg.</summary>
    public async Task<PosError?> DeleteBillAsync(Guid billId, CancellationToken ct)
    {
        var result = await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            if (bill.Lines.Any(l => l.PaidAmount != 0) || await db.Receipts.AnyAsync(r => r.BillId == billId, ct))
            {
                throw PosFailure.Invalid("billId", "Auf diese Rechnung wurde schon bezahlt; zuerst die Belege stornieren.");
            }

            var cards = await db.CardPayments.Where(c => c.BillId == billId).ToListAsync(ct);
            if (cards.Any(c => c.Status is CardPaymentStatus.Pending or CardPaymentStatus.Approved or CardPaymentStatus.ReversalPending))
            {
                throw PosFailure.Invalid("billId", "Am Terminal läuft oder liegt eine Kartenzahlung für diese Rechnung; zuerst kassieren oder stornieren.");
            }

            db.CardPayments.RemoveRange(cards);
            db.Bills.Remove(bill);
            return bill.Id;
        }, ct, loadAfter: false);
        return result.Error;
    }

    public async Task<BillDto?> GetBillAsync(Guid billId, CancellationToken ct)
    {
        var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null)
        {
            return null;
        }

        var customer = bill.CustomerId is { } id
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            : null;
        var receipts = await db.Receipts.AsNoTracking().Where(r => r.BillId == billId).OrderBy(r => r.Number).Select(r => r.Id).ToListAsync(ct);
        var lines = bill.Lines.OrderBy(l => l.Position).ToList();
        var total = lines.Sum(l => l.Amount);
        var paid = lines.Sum(l => l.PaidAmount);
        // Bezahlte Anzahlungen auf noch offene Termine der Rechnung werden beim Kassieren abgezogen.
        var openAppointments = lines.Where(l => l.Kind == BillLineKind.Appointment && l.Amount > l.PaidAmount && l.AppointmentId != null)
            .Select(l => l.AppointmentId!.Value).ToList();
        var deposits = openAppointments.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.AppointmentDeposits.AsNoTracking()
                .Where(d => openAppointments.Contains(d.AppointmentId) && d.SettledOnReceiptId == null && d.VoidedAt == null)
                .GroupBy(d => d.AppointmentId).Select(g => new { AppointmentId = g.Key, Amount = g.Sum(d => d.Amount) })
                .ToDictionaryAsync(d => d.AppointmentId, d => d.Amount, ct);
        decimal CreditOf(BillLine l) =>
            l.Kind == BillLineKind.Appointment && l.Amount > l.PaidAmount && l.AppointmentId is { } a && deposits.TryGetValue(a, out var amount) ? amount : 0;
        var credit = lines.Sum(CreditOf);
        var cashPercent = await CashDiscountPercentAsync(bill.RegisterId, ct);
        var cashDiscount = CashDiscountParts(lines.Select(l => (l.Kind, l.TaxCategory, l.TaxPercent, l.RevenueAccountId, l.Amount - l.PaidAmount - CreditOf(l))),
            cashPercent).Sum(p => p.Discount);
        var (prepaid, prepaidReference) = await PlatformPrepaidAsync(lines.Select(l => (l.Kind, l.AppointmentId, l.Amount - l.PaidAmount - CreditOf(l))), ct);
        return new BillDto(bill.Id, bill.RegisterId, bill.CustomerId, customer?.InHouseName, bill.Status,
            [.. lines.Select(l => new BillLineDto(l.Id, l.Kind, l.Description, l.Quantity, l.UnitPrice, l.Discount, l.Amount, l.PaidAmount,
                l.TaxCategory, l.TaxPercent, l.RevenueAccountId, l.AppointmentId, l.AreaVisitId, l.DiscountNote, l.TreatmentId, CreditOf(l)))],
            total, paid, total - paid - credit, receipts, cashPercent, cashDiscount, credit, prepaid, prepaidReference, customer?.LegalName);
    }

    /// <summary>
    /// Betrag der Termine, den der Gast vorab über Treatwell bezahlt hat und der noch nicht an einer Kassa verrechnet wurde (höchstens der offene
    /// Betrag je Termin), samt Buchungsnummern. Die Zahlungsart „Über Treatwell bezahlt“ darf nur diesen Betrag decken; der Umsatz läuft
    /// trotzdem über die Kassa (ADR 0010, ADR 0062).
    /// </summary>
    private async Task<(decimal Amount, string? Reference)> PlatformPrepaidAsync(IEnumerable<(BillLineKind Kind, Guid? AppointmentId, decimal Open)> lines, CancellationToken ct)
    {
        var open = lines.Where(l => l is { Kind: BillLineKind.Appointment, AppointmentId: not null, Open: > 0 })
            .GroupBy(l => l.AppointmentId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Open));
        if (open.Count == 0)
        {
            return (0, null);
        }

        var ids = open.Keys.ToList();
        var prepaid = await db.Appointments.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.PlatformPayment == PlatformPayment.Prepaid && a.PrepaidAmount > 0 && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.Id, Amount = a.PrepaidAmount!.Value })
            .ToListAsync(ct);
        // Schon an einem (nicht stornierten) Beleg verrechnet: nicht ein zweites Mal.
        var used = await db.Receipts.AsNoTracking()
            .Where(r => r.Kind == ReceiptKind.Sale && r.Payments.Any(p => p.Method == PaymentMethod.Treatwell)
                && !db.Receipts.Any(v => v.VoidsReceiptId == r.Id))
            .SelectMany(r => r.Lines).Where(l => l.AppointmentId != null).Select(l => l.AppointmentId!.Value).Distinct()
            .ToListAsync(ct);
        var usable = prepaid.Where(p => !used.Contains(p.Id)).ToList();
        if (usable.Count == 0)
        {
            return (0, null);
        }

        var usableIds = usable.Select(p => p.Id).ToList();
        var references = await db.ChannelBookings.AsNoTracking()
            .Where(b => b.Channel == BookingChannel.Treatwell && b.AppointmentId != null && usableIds.Contains(b.AppointmentId.Value))
            .Select(b => b.ExternalId).ToListAsync(ct);
        return (usable.Sum(p => Math.Min(p.Amount, open[p.Id])), references.Count == 0 ? null : string.Join(", ", references));
    }

    private async Task<BillLine> AddLineAsync(CashRegister register, Bill bill, BillLineInput input, CancellationToken ct)
    {
        var hadCustomer = bill.CustomerId is not null;
        var quantity = input.Quantity <= 0 ? 1 : input.Quantity;
        string description;
        decimal unitPrice;
        TaxCategory category;
        Guid account;
        Guid? appointmentId = null, treatmentId = null, articleId = null, areaVisitId = null;
        VoucherKind? voucherKind = null;
        int? units = null;

        switch (input.Kind)
        {
            case BillLineKind.Appointment:
                {
                    var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
                        .FirstOrDefaultAsync(a => a.Id == input.AppointmentId, ct) ?? throw PosFailure.NotFound("Termin");
                    if (appointment.LocationId != register.LocationId)
                    {
                        throw PosFailure.Invalid(nameof(input.AppointmentId), "Der Termin gehört zu einem anderen Standort.");
                    }

                    if (appointment.Status == AppointmentStatus.Cancelled)
                    {
                        throw PosFailure.Invalid(nameof(input.AppointmentId), "Der Termin ist storniert.");
                    }

                    // L1: Kassiert wird nur, wer da war; die Kassa-Liste bietet sonst „Einchecken und kassieren“ an.
                    if (appointment.Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.Completed))
                    {
                        throw PosFailure.Conflict("Der Gast ist noch nicht eingecheckt. Zuerst im Kalender einchecken.");
                    }

                    if (await db.BillLines.AnyAsync(l => l.AppointmentId == appointment.Id && l.Kind == BillLineKind.Appointment, ct))
                    {
                        throw PosFailure.Conflict("Dieser Termin steht schon auf einer Rechnung.");
                    }

                    var zone = await ZoneAsync(register, ct);
                    var local = TimeZoneInfo.ConvertTime(appointment.Start, zone);
                    description = $"{appointment.Treatment.Name} {local:dd.MM.yyyy HH:mm}";
                    unitPrice = appointment.Price;
                    (category, account) = await TreatmentSettingsAsync(appointment.TreatmentId, ct);
                    appointmentId = appointment.Id;
                    treatmentId = appointment.TreatmentId;
                    quantity = 1;
                    bill.CustomerId ??= appointment.CustomerId;
                    break;
                }

            case BillLineKind.Deposit:
                {
                    // Anzahlung auf einen bevorstehenden Termin: versteuert wie die Behandlung, verrechnet beim Kassieren der Behandlung.
                    var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
                        .FirstOrDefaultAsync(a => a.Id == input.AppointmentId, ct) ?? throw PosFailure.NotFound("Termin");
                    if (appointment.LocationId != register.LocationId)
                    {
                        throw PosFailure.Invalid(nameof(input.AppointmentId), "Der Termin gehört zu einem anderen Standort.");
                    }

                    // Bewusst auch für stornierte Termine: kommt eine Online-Zahlung nach dem Storno an, braucht sie trotzdem ihren
                    // Beleg, bevor sie erstattet wird.
                    if (await db.BillLines.AnyAsync(l => l.AppointmentId == appointment.Id && l.Kind == BillLineKind.Appointment && l.PaidAmount > 0, ct))
                    {
                        throw PosFailure.Conflict("Die Behandlung ist schon kassiert; eine Anzahlung ist nicht mehr möglich.");
                    }

                    if (await db.AppointmentDeposits.AnyAsync(d => d.AppointmentId == appointment.Id && d.VoidedAt == null, ct)
                        || await db.BillLines.AnyAsync(l => l.AppointmentId == appointment.Id && l.Kind == BillLineKind.Deposit && l.PaidAmount == 0, ct))
                    {
                        throw PosFailure.Conflict("Auf diesen Termin gibt es schon eine Anzahlung.");
                    }

                    unitPrice = input.UnitPrice ?? 0;
                    if (unitPrice > appointment.Price)
                    {
                        throw PosFailure.Invalid(nameof(input.UnitPrice), $"Die Anzahlung darf den Preis von {Money(appointment.Price)} nicht übersteigen.");
                    }

                    var zone = await ZoneAsync(register, ct);
                    var local = TimeZoneInfo.ConvertTime(appointment.Start, zone);
                    description = $"Anzahlung {appointment.Treatment.Name} {local:dd.MM.yyyy HH:mm}";
                    (category, account) = await TreatmentSettingsAsync(appointment.TreatmentId, ct);
                    appointmentId = appointment.Id;
                    treatmentId = appointment.TreatmentId;
                    quantity = 1;
                    bill.CustomerId ??= appointment.CustomerId;
                    break;
                }

            case BillLineKind.AreaVisit:
                {
                    var visit = await db.AreaVisits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.AreaVisitId, ct) ?? throw PosFailure.NotFound("Eintritt");
                    if (visit.LocationId != register.LocationId)
                    {
                        throw PosFailure.Invalid(nameof(input.AreaVisitId), "Der Eintritt gehört zu einem anderen Standort.");
                    }

                    if (visit.IsCancelled)
                    {
                        throw PosFailure.Invalid(nameof(input.AreaVisitId), "Der Eintritt ist storniert.");
                    }

                    if (await db.BillLines.AnyAsync(l => l.AreaVisitId == visit.Id, ct))
                    {
                        throw PosFailure.Conflict("Dieser Eintritt steht schon auf einer Rechnung.");
                    }

                    var room = await db.Rooms.AsNoTracking().FirstAsync(r => r.Id == visit.RoomId, ct);
                    var article = room.AdmissionArticleId is { } admission
                        ? await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == admission && a.IsActive, ct)
                        : null;
                    if (article is null)
                    {
                        throw PosFailure.Invalid(nameof(input.AreaVisitId),
                            $"Für „{room.Name}“ ist kein Artikel für den Eintritt hinterlegt (Stammdaten › Standorte › Räume).");
                    }

                    var zone = await ZoneAsync(register, ct);
                    var local = TimeZoneInfo.ConvertTime(visit.From, zone);
                    description = $"{article.Name} {local:dd.MM.yyyy HH:mm}, {room.Name}";
                    unitPrice = input.UnitPrice ?? article.Price;
                    category = article.TaxCategory;
                    account = article.RevenueAccountId;
                    articleId = article.Id;
                    areaVisitId = visit.Id;
                    quantity = visit.Persons;
                    bill.CustomerId ??= visit.CustomerId;
                    break;
                }

            case BillLineKind.Article:
                {
                    var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.ArticleId && a.IsActive, ct)
                        ?? throw PosFailure.NotFound("Artikel");
                    description = article.Name;
                    unitPrice = article.Price;
                    category = article.TaxCategory;
                    account = article.RevenueAccountId;
                    articleId = article.Id;
                    break;
                }

            case BillLineKind.Voucher:
                {
                    quantity = 1;
                    voucherKind = input.VoucherKind ?? VoucherKind.MultiPurpose;
                    if (voucherKind == VoucherKind.SinglePurpose)
                    {
                        var treatment = await db.Treatments.AsNoTracking().Include(t => t.LocationPrices)
                            .FirstOrDefaultAsync(t => t.Id == input.VoucherTreatmentId && t.IsActive, ct)
                            ?? throw PosFailure.NotFound("Behandlung");
                        description = $"Gutschein {treatment.Name}";
                        unitPrice = input.UnitPrice ?? treatment.PriceAt(register.LocationId);
                        (category, account) = await TreatmentSettingsAsync(treatment.Id, ct);
                        treatmentId = treatment.Id;
                        if (input.Units is { } count)
                        {
                            if (count is < 2 or > 50)
                            {
                                throw PosFailure.Invalid(nameof(input.Units), "Ein Paket hat 2 bis 50 Behandlungen.");
                            }

                            if (bill.CustomerId is null)
                            {
                                throw PosFailure.Invalid(nameof(input.Units), "Ein Paket gehört einem Gast: zuerst einen Termin oder Eintritt des Gastes auf die Rechnung setzen.");
                            }

                            if (input.ValidUntil is { } until && until <= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
                            {
                                throw PosFailure.Invalid(nameof(input.ValidUntil), "Das Ablaufdatum muss in der Zukunft liegen.");
                            }

                            units = count;
                            description = $"Paket {count}× {treatment.Name}";
                            unitPrice = input.UnitPrice ?? treatment.PriceAt(register.LocationId) * count;
                        }
                    }
                    else
                    {
                        description = "Wertgutschein";
                        unitPrice = input.UnitPrice ?? 0;
                        category = TaxCategory.Zero;
                        account = PosModel.MultiPurposeVouchers.Id;
                    }

                    break;
                }

            default:
                {
                    if (string.IsNullOrWhiteSpace(input.Description))
                    {
                        throw PosFailure.Invalid(nameof(input.Description), "Pflichtfeld.");
                    }

                    description = input.Description.Trim();
                    unitPrice = input.UnitPrice ?? 0;
                    category = input.TaxCategory ?? TaxCategory.Normal;
                    account = input.RevenueAccountId ?? PosModel.Products.Id;
                    break;
                }
        }

        var line = NewBillLine(register, bill, input,
            new LineBasis(description, unitPrice, quantity, category, account, appointmentId, treatmentId, articleId, areaVisitId, voucherKind, units));
        if (!await db.RevenueAccounts.AnyAsync(a => a.Id == account, ct))
        {
            throw PosFailure.NotFound("Erlöskonto");
        }

        db.BillLines.Add(line);

        // Kundenrabatt: auf die neue Position; kam die Kundin erst mit dieser Position (Termin, Eintritt), auch auf die schon offenen.
        await ApplyCustomerDiscountAsync(bill, hadCustomer ? [line] : bill.Lines, ct);
        return line;
    }

    /// <summary>Was die Art einer Position festlegt: Text, Preis, Steuer, Erlöskonto und Bezug (aus Termin, Artikel, Behandlung …).</summary>
    internal sealed record LineBasis(
        string Description, decimal UnitPrice, int Quantity, TaxCategory Category, Guid Account, Guid? AppointmentId, Guid? TreatmentId,
        Guid? ArticleId, Guid? AreaVisitId, VoucherKind? VoucherKind, int? Units);

    /// <summary>Prüft Preis, Rabatt und Steuersatz und hängt die neue Position an die Rechnung (ohne Datenbank).</summary>
    internal static BillLine NewBillLine(CashRegister register, Bill bill, BillLineInput input, LineBasis basis)
    {
        var (unitPrice, quantity) = (basis.UnitPrice, basis.Quantity);
        var position = bill.Lines.Count == 0 ? 1 : bill.Lines.Max(l => l.Position) + 1;
        if (unitPrice <= 0 || decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw PosFailure.Invalid(nameof(input.UnitPrice), "Preis muss größer als 0 sein und höchstens zwei Nachkommastellen haben.");
        }

        var gross = unitPrice * quantity;
        if (input.Discount < 0 || input.Discount >= gross || decimal.Round(input.Discount, 2) != input.Discount)
        {
            throw PosFailure.Invalid(nameof(input.Discount), "Rabatt muss zwischen 0 und dem Positionsbetrag liegen.");
        }

        if (input.Kind is BillLineKind.Voucher or BillLineKind.Deposit && input.Discount != 0)
        {
            throw PosFailure.Invalid(nameof(input.Discount), input.Kind == BillLineKind.Voucher
                ? "Gutscheine werden zum vollen Wert verkauft."
                : "Auf eine Anzahlung gibt es keinen Rabatt.");
        }

        if (!TaxRates.TryGet(register.Country, basis.Category, out var percent))
        {
            throw PosFailure.Invalid(nameof(input.TaxCategory), $"Steuersatz {basis.Category} gibt es in {register.Country} nicht.");
        }

        var line = new BillLine
        {
            BillId = bill.Id,
            Position = position,
            Kind = input.Kind,
            Description = basis.Description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Discount = input.Discount,
            DiscountNote = input.Discount == 0 ? null : ManualNote(input.Discount, gross, null),
            Amount = gross - input.Discount,
            TaxCategory = basis.Category,
            TaxPercent = percent,
            RevenueAccountId = basis.Account,
            AppointmentId = basis.AppointmentId,
            TreatmentId = basis.TreatmentId,
            ArticleId = basis.ArticleId,
            VoucherKind = basis.VoucherKind,
            VoucherUnits = basis.Units,
            VoucherValidUntil = basis.Units is null ? null : input.ValidUntil,
            AreaVisitId = basis.AreaVisitId,
        };
        bill.Lines.Add(line);
        return line;
    }

    private const string ManualDiscount = "Rabatt";

    /// <summary>Der Beleg nennt immer den Prozentsatz, auch wenn der Rabatt als Betrag gegeben wurde.</summary>
    private static string ManualNote(decimal discount, decimal gross, decimal? percent) =>
        $"{ManualDiscount} {PercentText(percent ?? decimal.Round(discount * 100 / gross, 1, MidpointRounding.AwayFromZero))} %";

    internal static string PercentText(decimal percent) => percent.ToString("0.##", German);

    /// <summary>Rabattgrenze des Spas (Standard 20 %): darüber nur mit Kassenaufsicht.</summary>
    private async Task EnsureDiscountAllowedAsync(CashRegister register, decimal discount, decimal gross, bool supervisor, CancellationToken ct)
    {
        if (supervisor)
        {
            return;
        }

        var limit = await DiscountLimitAsync(register.Id, ct);
        if (discount * 100 > limit * gross)
        {
            throw PosFailure.Forbidden($"Rabatte über {PercentText(limit)} % gibt die Standortleitung oder die Inhaberin.");
        }
    }

    private async Task<decimal> DiscountLimitAsync(Guid registerId, CancellationToken ct)
    {
        var tenantId = await TenantModel.TenantOfAsync(db.CashRegisters.Where(r => r.Id == registerId), ct);
        return await db.Tenants.Where(t => t.Id == tenantId).Select(t => (decimal?)t.DiscountLimitPercent).FirstOrDefaultAsync(ct) ?? 20m;
    }

    private async Task<decimal> CashDiscountPercentAsync(Guid registerId, CancellationToken ct)
    {
        var tenantId = await TenantModel.TenantOfAsync(db.CashRegisters.Where(r => r.Id == registerId), ct);
        return await db.Tenants.Where(t => t.Id == tenantId).Select(t => (decimal?)t.CashDiscountPercent).FirstOrDefaultAsync(ct) ?? 0m;
    }

    /// <summary>
    /// Barzahlungsrabatt je Steuersatz und Erlöskonto, damit die Umsatzsteuer vom verminderten Betrag gerechnet wird und die
    /// Buchhaltung den Nachlass beim richtigen Erlös sieht. Verkaufte Gutscheine bleiben beim vollen Wert.
    /// </summary>
    internal static List<(TaxCategory TaxCategory, decimal TaxPercent, Guid RevenueAccountId, decimal Discount)> CashDiscountParts(
        IEnumerable<(BillLineKind Kind, TaxCategory TaxCategory, decimal TaxPercent, Guid RevenueAccountId, decimal Amount)> lines, decimal percent) =>
        percent <= 0
            ? []
            : [.. lines.Where(l => l.Kind is not (BillLineKind.Voucher or BillLineKind.Deposit) && l.Amount > 0)
                .GroupBy(l => (l.TaxCategory, l.TaxPercent, l.RevenueAccountId))
                .Select(g => (g.Key.TaxCategory, g.Key.TaxPercent, g.Key.RevenueAccountId,
                    Discount: decimal.Round(g.Sum(l => l.Amount) * percent / 100, 2, MidpointRounding.AwayFromZero)))
                .Where(p => p.Discount > 0)
                .OrderBy(p => p.TaxPercent).ThenBy(p => p.RevenueAccountId)];

    /// <summary>
    /// Zieht den Rabatt aus der Kundenkartei von offenen Positionen ohne eigenen Rabatt ab. Gutscheine bleiben beim vollen Wert,
    /// ein von Hand gegebener Rabatt geht vor.
    /// </summary>
    private async Task ApplyCustomerDiscountAsync(Bill bill, IEnumerable<BillLine> lines, CancellationToken ct)
    {
        if (bill.CustomerId is not { } customerId)
        {
            return;
        }

        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == customerId)
            .Select(c => new { c.DiscountPercent, c.DiscountScope }).FirstOrDefaultAsync(ct);
        if (customer?.DiscountPercent is { } percent)
        {
            DiscountLines(lines, percent, customer.DiscountScope);
        }
    }

    /// <summary>Zieht den Rabatt der Kundenkartei von offenen Positionen ohne eigenen Rabatt ab (ohne Datenbank).</summary>
    internal static void DiscountLines(IEnumerable<BillLine> lines, decimal percent, CustomerDiscountScope scope)
    {
        if (percent <= 0)
        {
            return;
        }

        var note = $"Kundenrabatt {PercentText(percent)} %";
        foreach (var line in lines.Where(l => l.Discount == 0 && l.PaidAmount == 0 && CustomerDiscountApplies(scope, l.Kind)))
        {
            var gross = line.UnitPrice * line.Quantity;
            var discount = decimal.Round(gross * percent / 100, 2, MidpointRounding.AwayFromZero);
            if (discount <= 0 || discount >= gross)
            {
                continue;
            }

            line.Discount = discount;
            line.DiscountNote = note;
            line.Amount = gross - discount;
        }
    }

    internal static bool CustomerDiscountApplies(CustomerDiscountScope scope, BillLineKind kind) => kind is not (BillLineKind.Voucher or BillLineKind.Deposit) && scope switch
    {
        CustomerDiscountScope.Treatments => kind == BillLineKind.Appointment,
        CustomerDiscountScope.Products => kind == BillLineKind.Article,
        _ => true,
    };

    private async Task<(TaxCategory, Guid)> TreatmentSettingsAsync(Guid treatmentId, CancellationToken ct) =>
        await db.TreatmentPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TreatmentId == treatmentId, ct) is { } s
            ? (s.TaxCategory, s.RevenueAccountId)
            : (TaxCategory.Normal, PosModel.Treatments.Id);

    // ---------- Bezahlen ----------

    public async Task<PosResult<ReceiptDto>> CheckoutAsync(Guid billId, CheckoutRequest request, string? userId, CancellationToken ct) =>
        await ReceiptTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            EnsureActive(register);
            var bill = await LoadOpenBillAsync(register, billId, ct);
            var now = time.GetUtcNow();
            var today = LocalTime.LocalDate(now, await ZoneAsync(register, ct));
            var draft = await BuildCheckoutAsync(register, bill, request, now, today, PaymentMethods.Available(register.Country, paymentOptions.Value),
                new DbCheckoutLookups(this, db), ct);
            db.Vouchers.AddRange(draft.NewVouchers);
            db.AppointmentDeposits.AddRange(draft.NewDeposits);
            return await IssueAsync(register, ReceiptKind.Sale, ct, (number, at) => new Receipt
            {
                Id = draft.ReceiptId,
                RegisterId = register.Id,
                Number = number,
                Kind = ReceiptKind.Sale,
                IssuedAt = at,
                BillId = bill.Id,
                CustomerId = bill.CustomerId,
                Total = draft.Total,
                Change = draft.Change,
                CreatedByUserId = userId,
                Lines = draft.Lines,
                Payments = draft.Payments,
            });
        }, ct);

    /// <summary>Die Abfragen des Kassierens gegen die Datenbank.</summary>
    private sealed class DbCheckoutLookups(PosService owner, AispaDbContext db) : ICheckoutLookups
    {
        public Task<string> NewVoucherCodeAsync(CancellationToken ct) => owner.NewVoucherCodeAsync(ct);

        public Task<Voucher?> FindVoucherAsync(string code, CancellationToken ct) => db.Vouchers.FirstOrDefaultAsync(v => v.Code == code, ct);

        public Task<List<AppointmentDeposit>> OpenDepositsAsync(Guid appointmentId, CancellationToken ct) =>
            db.AppointmentDeposits
                .Where(d => d.AppointmentId == appointmentId && d.SettledOnReceiptId == null && d.VoidedAt == null)
                .OrderBy(d => d.CreatedAt).ToListAsync(ct);

        public async Task<(long Number, string RegisterCode)> DepositReceiptAsync(Guid receiptId, CashRegister current, CancellationToken ct)
        {
            var depositReceipt = await db.Receipts.AsNoTracking().Where(r => r.Id == receiptId)
                .Select(r => new { r.Number, r.RegisterId }).FirstAsync(ct);
            var code = depositReceipt.RegisterId == current.Id
                ? current.Code
                : await db.CashRegisters.AsNoTracking().Where(r => r.Id == depositReceipt.RegisterId).Select(r => r.Code).FirstAsync(ct);
            return (depositReceipt.Number, code);
        }

        public Task<decimal> CashDiscountPercentAsync(Guid registerId, CancellationToken ct) => owner.CashDiscountPercentAsync(registerId, ct);

        public Task<(decimal Amount, string? Reference)> PlatformPrepaidAsync(IEnumerable<(BillLineKind Kind, Guid? AppointmentId, decimal Open)> lines,
            CancellationToken ct) => owner.PlatformPrepaidAsync(lines, ct);

        public Task<CardPayment?> FindCardPaymentAsync(Guid id, Guid billId, CancellationToken ct) =>
            db.CardPayments.FirstOrDefaultAsync(c => c.Id == id && c.BillId == billId, ct);
    }

    /// <summary>Was ein Kassiervorgang ergibt, bevor der Beleg nummeriert und signiert wird.</summary>
    internal sealed record CheckoutDraft(Guid ReceiptId, List<ReceiptLine> Lines, List<ReceiptPayment> Payments, decimal Total, decimal Change,
        List<Voucher> NewVouchers, List<AppointmentDeposit> NewDeposits);

    /// <summary>Was das Kassieren aus der Datenbank (oder dem Speicher beim Einspielen der Beispieldaten) braucht.</summary>
    internal interface ICheckoutLookups
    {
        Task<string> NewVoucherCodeAsync(CancellationToken ct);

        Task<Voucher?> FindVoucherAsync(string code, CancellationToken ct);

        /// <summary>Offene (weder verrechnete noch stornierte) Anzahlungen auf einen Termin, älteste zuerst.</summary>
        Task<List<AppointmentDeposit>> OpenDepositsAsync(Guid appointmentId, CancellationToken ct);

        /// <summary>Nummer und Kassen-ID des Anzahlungsbelegs.</summary>
        Task<(long Number, string RegisterCode)> DepositReceiptAsync(Guid receiptId, CashRegister current, CancellationToken ct);

        Task<decimal> CashDiscountPercentAsync(Guid registerId, CancellationToken ct);

        Task<(decimal Amount, string? Reference)> PlatformPrepaidAsync(IEnumerable<(BillLineKind Kind, Guid? AppointmentId, decimal Open)> lines, CancellationToken ct);

        Task<CardPayment?> FindCardPaymentAsync(Guid id, Guid billId, CancellationToken ct);
    }

    /// <summary>
    /// Verteilt die Zahlung auf die Positionen der Rechnung und baut Zeilen und Zahlungen des Belegs (Gutscheine, Anzahlungen, Rabatte,
    /// Rundung). Ändert die Rechnung (bezahlte Beträge, Status) und die eingelösten Gutscheine; speichert nichts.
    /// </summary>
    internal static async Task<CheckoutDraft> BuildCheckoutAsync(CashRegister register, Bill bill, CheckoutRequest request, DateTimeOffset now,
        DateOnly today, IReadOnlyList<PaymentMethod> available, ICheckoutLookups lookups, CancellationToken ct)
    {
        var receiptId = Guid.CreateVersion7();
        if ((request.Payments ?? []).FirstOrDefault(p => !available.Contains(p.Method)) is { } unavailable)
        {
            throw PosFailure.Invalid("payments", $"{PaymentMethods.Label(unavailable.Method)} ist an dieser Kasse nicht verfügbar.");
        }

        // Welche Beträge welcher Positionen jetzt bezahlt werden; ohne Angabe alles noch Offene.
        var allocations = request.Lines is { Count: > 0 } requested
            ? requested.GroupBy(a => a.LineId).Select(g =>
            {
                var line = bill.Lines.FirstOrDefault(l => l.Id == g.Key) ?? throw PosFailure.NotFound("Position");
                return (Line: line, Amount: g.Sum(a => a.Amount));
            }).ToList()
            : [.. bill.Lines.Where(l => l.Amount > l.PaidAmount).OrderBy(l => l.Position).Select(l => (Line: l, Amount: l.Amount - l.PaidAmount))];
        if (allocations.Count == 0)
        {
            throw PosFailure.Invalid("lines", "Auf dieser Rechnung ist nichts mehr offen.");
        }

        var lines = new List<ReceiptLine>();
        var newVouchers = new List<Voucher>();
        var newDeposits = new List<AppointmentDeposit>();
        var position = 0;
        foreach (var (line, amount) in allocations.OrderBy(a => a.Line.Position))
        {
            var open = line.Amount - line.PaidAmount;
            if (amount <= 0 || amount > open || decimal.Round(amount, 2) != amount)
            {
                throw PosFailure.Invalid("lines", $"{line.Description}: offen sind {Money(open)}.");
            }

            if (line.Kind is BillLineKind.Voucher or BillLineKind.Deposit && amount != line.Amount)
            {
                throw PosFailure.Invalid("lines", line.Kind == BillLineKind.Voucher
                    ? "Gutscheine können nicht geteilt bezahlt werden."
                    : "Eine Anzahlung wird in einem Betrag bezahlt.");
            }

            if (line.Kind == BillLineKind.Deposit)
            {
                // Der Anzahlungsbeleg: ab jetzt verrechnet die Kasse den Betrag beim Kassieren der Behandlung.
                newDeposits.Add(new AppointmentDeposit
                {
                    AppointmentId = line.AppointmentId!.Value,
                    Amount = line.Amount,
                    TaxCategory = line.TaxCategory,
                    TaxPercent = line.TaxPercent,
                    RevenueAccountId = line.RevenueAccountId,
                    ReceiptId = receiptId,
                    CreatedAt = now,
                });
            }

            Guid? voucherId = null;
            if (line.Kind == BillLineKind.Voucher)
            {
                var voucher = new Voucher
                {
                    Code = await lookups.NewVoucherCodeAsync(ct),
                    Kind = line.VoucherKind!.Value,
                    InitialValue = line.Amount,
                    Balance = line.Amount,
                    TreatmentId = line.VoucherKind == VoucherKind.SinglePurpose ? line.TreatmentId : null,
                    Units = line.VoucherUnits,
                    UnitsLeft = line.VoucherUnits,
                    CustomerId = line.VoucherUnits is null ? null : bill.CustomerId,
                    ValidUntil = line.VoucherValidUntil,
                    SoldOnReceiptId = receiptId,
                    CreatedAt = now,
                };
                newVouchers.Add(voucher);
                voucherId = voucher.Id;
            }

            line.PaidAmount += amount;
            lines.Add(new ReceiptLine
            {
                ReceiptId = receiptId,
                Position = ++position,
                Description = amount == line.Amount ? line.Description : $"{line.Description} (Teilbetrag)",
                Quantity = line.Quantity,
                Amount = amount,
                // Rabatt nur bei voll bezahlter Position ausweisen; ein Teilbetrag hat keinen eigenen Listenpreis.
                Discount = amount == line.Amount ? line.Discount : 0,
                DiscountNote = amount == line.Amount && line.Discount != 0
                    ? line.DiscountNote ?? ManualNote(line.Discount, line.UnitPrice * line.Quantity, null)
                    : null,
                TaxCategory = line.TaxCategory,
                TaxPercent = line.TaxPercent,
                RevenueAccountId = line.RevenueAccountId,
                BillLineId = line.Id,
                AppointmentId = line.AppointmentId,
                VoucherId = voucherId,
            });
        }

        // Bezahlte Anzahlungen auf die kassierten Termine: als negative Zeile mit dem Steuersatz des Anzahlungsbelegs abgezogen,
        // damit die Behandlung nur einmal versteuert wird und der Beleg den Rest ausweist.
        foreach (var treated in lines.Where(l => l.AppointmentId is not null && l.Amount > 0
                     && bill.Lines.Any(b => b.Id == l.BillLineId && b.Kind == BillLineKind.Appointment)).ToList())
        {
            var deposits = await lookups.OpenDepositsAsync(treated.AppointmentId!.Value, ct);
            foreach (var deposit in deposits)
            {
                if (deposit.Amount > treated.Amount)
                {
                    throw PosFailure.Invalid("lines",
                        $"{treated.Description}: die Anzahlung von {Money(deposit.Amount)} übersteigt den Betrag; bitte den vollen Preis ohne Rabatt zuordnen oder den Anzahlungsbeleg stornieren.");
                }

                var (depositNumber, code) = await lookups.DepositReceiptAsync(deposit.ReceiptId, register, ct);
                deposit.SettledOnReceiptId = receiptId;
                lines.Add(new ReceiptLine
                {
                    ReceiptId = receiptId,
                    Position = ++position,
                    Description = $"Anzahlung verrechnet (Beleg {code} Nr. {depositNumber})",
                    Quantity = 1,
                    Amount = -deposit.Amount,
                    TaxCategory = deposit.TaxCategory,
                    TaxPercent = deposit.TaxPercent,
                    RevenueAccountId = deposit.RevenueAccountId,
                    BillLineId = treated.BillLineId,
                    AppointmentId = treated.AppointmentId,
                    DepositId = deposit.Id,
                });
            }
        }

        // Gutscheine: Einzweckgutscheine mindern die passende Behandlung (schon beim Verkauf versteuert), Wertgutscheine sind ein Zahlungsmittel.
        var payments = new List<ReceiptPayment>();
        foreach (var input in request.Payments ?? [])
        {
            if (input.Method != PaymentMethod.Voucher)
            {
                continue;
            }

            var code = NormalizeCode(input.VoucherCode);
            var voucher = await lookups.FindVoucherAsync(code, ct) ?? throw PosFailure.NotFound("Gutschein");
            if (voucher.Status != VoucherStatus.Active || (voucher.Units is null ? voucher.Balance <= 0 : voucher.UnitsLeft <= 0))
            {
                throw PosFailure.Invalid("payments", $"Gutschein {voucher.Code} ist nicht mehr gültig (Restwert {Money(voucher.Balance)}).");
            }

            if (voucher.ValidUntil is { } until && until < today)
            {
                throw PosFailure.Invalid("payments", $"Gutschein {voucher.Code} ist am {until:dd.MM.yyyy} abgelaufen.");
            }

            if (voucher.Units is not null)
            {
                RedeemPackage(voucher, bill, lines, receiptId, ref position);
            }
            else if (voucher.Kind == VoucherKind.SinglePurpose)
            {
                var matching = lines.FirstOrDefault(l => Redeemable(l, bill, voucher, lines));
                if (matching is null)
                {
                    throw PosFailure.Invalid("payments", $"Gutschein {voucher.Code} gilt nur für eine bestimmte Behandlung, die nicht auf diesem Beleg steht.");
                }

                var credit = Math.Min(voucher.Balance, matching.Amount);
                voucher.Balance -= credit;
                lines.Add(new ReceiptLine
                {
                    ReceiptId = receiptId,
                    Position = ++position,
                    Description = $"Einzweckgutschein {voucher.Code} eingelöst",
                    Quantity = 1,
                    Amount = -credit,
                    TaxCategory = matching.TaxCategory,
                    TaxPercent = matching.TaxPercent,
                    RevenueAccountId = matching.RevenueAccountId,
                    BillLineId = matching.BillLineId,
                    VoucherId = voucher.Id,
                });
            }
            else
            {
                if (input.Amount <= 0 || input.Amount > voucher.Balance)
                {
                    throw PosFailure.Invalid("payments", $"Gutschein {voucher.Code}: höchstens {Money(voucher.Balance)} einlösbar.");
                }

                voucher.Balance -= input.Amount;
                payments.Add(new ReceiptPayment
                {
                    ReceiptId = receiptId,
                    Method = PaymentMethod.Voucher,
                    Amount = input.Amount,
                    VoucherId = voucher.Id,
                });
            }

            if (voucher.Units is null ? voucher.Balance == 0 : voucher.UnitsLeft == 0)
            {
                voucher.Status = VoucherStatus.Redeemed;
            }
        }

        // Barzahlungsrabatt: nur wenn der ganze Beleg bar bezahlt wird; eigene Zeile je Steuersatz und Erlöskonto.
        if (request.Payments is { Count: > 0 } paying && paying.All(p => p.Method == PaymentMethod.Cash)
            && await lookups.CashDiscountPercentAsync(register.Id, ct) is var cashPercent and > 0)
        {
            // Verrechnete Anzahlungen sind schon bezahlt; der Rabatt gilt nur für das, was jetzt bar bezahlt wird.
            var settled = lines.Where(l => l.DepositId is not null && l.BillLineId is not null).GroupBy(l => l.BillLineId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            var sold = allocations.Select(a => (a.Line.Kind, a.Line.TaxCategory, a.Line.TaxPercent, a.Line.RevenueAccountId,
                a.Amount + settled.GetValueOrDefault(a.Line.Id)));
            foreach (var part in CashDiscountParts(sold, cashPercent))
            {
                lines.Add(new ReceiptLine
                {
                    ReceiptId = receiptId,
                    Position = ++position,
                    Description = $"Barzahlungsrabatt {PercentText(cashPercent)} %",
                    Quantity = 1,
                    Amount = -part.Discount,
                    TaxCategory = part.TaxCategory,
                    TaxPercent = part.TaxPercent,
                    RevenueAccountId = part.RevenueAccountId,
                });
            }
        }

        var total = lines.Sum(l => l.Amount);
        foreach (var input in (request.Payments ?? []).Where(p => PaymentMethods.IsElectronic(p.Method)))
        {
            if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount)
            {
                throw PosFailure.Invalid("payments", "Zahlbeträge müssen größer als 0 sein.");
            }

            var reference = Blank(input.Reference);
            if (input.Method == PaymentMethod.Treatwell)
            {
                // Nur für das, was der Gast vorab über Treatwell bezahlt hat; der Rest wird wie sonst kassiert.
                var (prepaid, prepaidReference) = await lookups.PlatformPrepaidAsync(allocations.Select(a => (a.Line.Kind, a.Line.AppointmentId, a.Amount)), ct);
                if (input.Amount > prepaid)
                {
                    throw PosFailure.Invalid("payments",
                        prepaid > 0
                            ? $"Über Treatwell vorab bezahlt sind für diese Termine {Money(prepaid)}."
                            : "Für diese Termine gibt es keine Vorauszahlung über Treatwell (mehr).");
                }

                reference ??= prepaidReference is null ? null : $"Treatwell {prepaidReference}";
            }

            if (input.CardPaymentId is { } cardPaymentId)
            {
                // Zahlung vom angebundenen Terminal: genau einmal, in voller Höhe, für diese Rechnung.
                var card = await lookups.FindCardPaymentAsync(cardPaymentId, bill.Id, ct)
                    ?? throw PosFailure.NotFound("Kartenzahlung");
                if (input.Method != PaymentMethod.Card || card.Status != CardPaymentStatus.Approved || card.ReceiptId is not null || card.Amount != input.Amount)
                {
                    throw PosFailure.Invalid("payments", $"Die Kartenzahlung über {Money(card.Amount)} ist nicht genehmigt oder schon verbucht.");
                }

                card.ReceiptId = receiptId;
                reference = card.Reference ?? card.Id.ToString();
            }

            payments.Add(new ReceiptPayment
            {
                ReceiptId = receiptId,
                Method = input.Method,
                Amount = input.Amount,
                Reference = reference,
            });
        }

        var nonCash = payments.Sum(p => p.Amount);
        if (nonCash > total)
        {
            throw PosFailure.Invalid("payments", $"Zu viel bezahlt: offen sind {Money(total)}, bezahlt {Money(nonCash)}. Rückgeld gibt es nur bar.");
        }

        // Schweiz: Barzahlungen werden auf 5 Rappen gerundet; die Differenz steht als eigene, steuerfreie Zeile auf dem Beleg.
        if (register.Country == FiscalCountry.CH && CashRounding(total - nonCash) is var rounding and not 0)
        {
            lines.Add(new ReceiptLine
            {
                ReceiptId = receiptId,
                Position = ++position,
                Description = "Rundungsdifferenz (5 Rappen)",
                Quantity = 1,
                Amount = rounding,
                TaxCategory = TaxCategory.Zero,
                TaxPercent = 0,
                RevenueAccountId = PosModel.Treatments.Id,
            });
            total += rounding;
        }

        var cashGiven = (request.Payments ?? []).Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var cashDue = total - nonCash;
        if (cashGiven < 0 || cashGiven < cashDue)
        {
            throw PosFailure.Invalid("payments", $"Es fehlen {Money(cashDue - cashGiven)}.");
        }

        if (cashDue > 0)
        {
            payments.Add(new ReceiptPayment { ReceiptId = receiptId, Method = PaymentMethod.Cash, Amount = cashDue });
        }

        if (bill.Lines.All(l => l.PaidAmount == l.Amount))
        {
            bill.Status = BillStatus.Settled;
        }

        return new CheckoutDraft(receiptId, lines, payments, total, cashGiven - cashDue, newVouchers, newDeposits);
    }


    /// <summary>Differenz zum nächsten Vielfachen von 5 Rappen, z. B. 12,47 → 12,45 (−0,02) und 12,48 → 12,50 (+0,02).</summary>
    internal static decimal CashRounding(decimal cashDue) =>
        cashDue <= 0 ? 0 : (Math.Round(cashDue * 20m, MidpointRounding.AwayFromZero) / 20m) - cashDue;

    // ---------- Storno ----------

    /// <summary>So lange darf die Rezeption einen eigenen Beleg selbst stornieren (Fehlgriff beim Kassieren); danach nur die Kassenaufsicht.</summary>
    public static readonly TimeSpan OwnVoidWindow = TimeSpan.FromMinutes(15);

    public async Task<PosResult<ReceiptDto>> VoidAsync(Guid receiptId, string? reason, string? userId, bool supervisor, CancellationToken ct)
    {
        var registerId = await db.Receipts.Where(r => r.Id == receiptId).Select(r => (Guid?)r.RegisterId).FirstOrDefaultAsync(ct);
        if (registerId is null)
        {
            return new PosError(PosErrorKind.NotFound, "receiptId", "Beleg nicht gefunden.");
        }

        return await ReceiptTransactionAsync(registerId.Value, async register =>
        {
            EnsureActive(register);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw PosFailure.Invalid(nameof(reason), "Für ein Storno ist ein Grund Pflicht.");
            }

            var original = await db.Receipts.Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery().FirstAsync(r => r.Id == receiptId, ct);
            return await IssueAsync(register, ReceiptKind.Void, ct,
                await BuildVoidAsync(original, reason, userId, supervisor, time.GetUtcNow(), new DbVoidLookups(db), ct));
        }, ct);
    }

    /// <summary>Was das Stornieren aus der Datenbank (oder dem Speicher beim Einspielen der Beispieldaten) braucht.</summary>
    internal interface IVoidLookups
    {
        Task<bool> IsVoidedAsync(Guid receiptId, CancellationToken ct);

        Task<List<Voucher>> VouchersSoldOnAsync(Guid receiptId, CancellationToken ct);

        /// <summary>Anzahlungen mit diesem Anzahlungsbeleg, die nicht storniert sind.</summary>
        Task<List<AppointmentDeposit>> OpenDepositsOfAsync(Guid receiptId, CancellationToken ct);

        Task<List<AppointmentDeposit>> DepositsSettledOnAsync(Guid receiptId, CancellationToken ct);

        Task<Voucher> VoucherAsync(Guid id, CancellationToken ct);

        Task<Bill> BillAsync(Guid id, CancellationToken ct);
    }

    /// <summary>
    /// Prüft das Storno und setzt zurück, was der Verkaufsbeleg bewirkt hat (Gutscheine, Anzahlungen, Rechnung). Liefert den Aufbau des
    /// Stornobelegs, der danach nummeriert und signiert wird; speichert nichts.
    /// </summary>
    internal static async Task<Func<long, DateTimeOffset, Receipt>> BuildVoidAsync(Receipt original, string reason, string? userId, bool supervisor,
        DateTimeOffset now, IVoidLookups lookups, CancellationToken ct)
    {
        if (original.Kind != ReceiptKind.Sale)
        {
            throw PosFailure.Invalid(nameof(original.Id), "Nur Verkaufsbelege können storniert werden.");
        }

        if (!supervisor && (userId is null || original.CreatedByUserId != userId || now - original.IssuedAt > OwnVoidWindow))
        {
            throw PosFailure.Forbidden(
                $"Stornieren darf die Standortleitung. Eigene Belege kann die Rezeption bis {OwnVoidWindow.TotalMinutes:0} Minuten nach dem Kassieren selbst stornieren.");
        }

        if (await lookups.IsVoidedAsync(original.Id, ct))
        {
            throw PosFailure.Conflict("Dieser Beleg ist schon storniert.");
        }

        // Verkaufte Gutscheine: nur stornierbar, solange nichts davon eingelöst ist.
        foreach (var voucher in await lookups.VouchersSoldOnAsync(original.Id, ct))
        {
            if (voucher.Balance != voucher.InitialValue || voucher.UnitsLeft != voucher.Units || voucher.Status != VoucherStatus.Active)
            {
                throw PosFailure.Invalid(nameof(original.Id), $"Gutschein {voucher.Code} wurde schon eingelöst; der Beleg kann nicht storniert werden.");
            }

            voucher.Status = VoucherStatus.Voided;
            voucher.Balance = 0;
        }

        // Anzahlungsbelege: nur stornierbar, solange die Anzahlung nicht verrechnet ist; danach zuerst den Beleg der Behandlung stornieren.
        foreach (var deposit in await lookups.OpenDepositsOfAsync(original.Id, ct))
        {
            if (deposit.SettledOnReceiptId is not null)
            {
                throw PosFailure.Invalid(nameof(original.Id), "Die Anzahlung wurde schon beim Beleg der Behandlung verrechnet; zuerst diesen Beleg stornieren.");
            }

            deposit.VoidedAt = now;
        }

        // Verrechnete Anzahlungen stehen nach dem Storno des Behandlungsbelegs wieder zur Verrechnung bereit.
        foreach (var deposit in await lookups.DepositsSettledOnAsync(original.Id, ct))
        {
            deposit.SettledOnReceiptId = null;
        }

        // Eingelöste Gutscheine bekommen ihren Wert zurück, Pakete je Einlösung eine Behandlung.
        var credits = original.Payments.Where(p => p.VoucherId is not null).Select(p => (Id: p.VoucherId!.Value, p.Amount))
            .Concat(original.Lines.Where(l => l.VoucherId is not null && l.Amount < 0).Select(l => (Id: l.VoucherId!.Value, Amount: -l.Amount)));
        foreach (var (voucherId, amount) in credits)
        {
            var voucher = await lookups.VoucherAsync(voucherId, ct);
            if (voucher.Units is { } units)
            {
                voucher.UnitsLeft = Math.Min(units, (voucher.UnitsLeft ?? 0) + 1);
                voucher.Balance = voucher.UnitsLeft == units
                    ? voucher.InitialValue
                    : Math.Min(voucher.InitialValue, voucher.Balance + decimal.Round(voucher.InitialValue / units, 2, MidpointRounding.AwayFromZero));
            }
            else
            {
                voucher.Balance += amount;
            }

            voucher.Status = VoucherStatus.Active;
        }

        // Die Rechnung ist wieder offen; die Positionen können neu bezahlt oder entfernt werden.
        if (original.BillId is { } billId)
        {
            var bill = await lookups.BillAsync(billId, ct);
            foreach (var line in original.Lines.Where(l => l.BillLineId is not null && l.Amount > 0))
            {
                bill.Lines.First(b => b.Id == line.BillLineId).PaidAmount -= line.Amount;
            }

            bill.Status = BillStatus.Open;
        }

        var voidId = Guid.CreateVersion7();
        var lines = original.Lines.OrderBy(l => l.Position).Select(l => new ReceiptLine
        {
            ReceiptId = voidId,
            Position = l.Position,
            Description = l.Description,
            Quantity = l.Quantity,
            Amount = -l.Amount,
            Discount = -l.Discount,
            DiscountNote = l.DiscountNote,
            TaxCategory = l.TaxCategory,
            TaxPercent = l.TaxPercent,
            RevenueAccountId = l.RevenueAccountId,
            BillLineId = l.BillLineId,
            AppointmentId = l.AppointmentId,
            VoucherId = l.VoucherId,
            DepositId = l.DepositId,
        }).ToList();
        var payments = original.Payments.Select(p => new ReceiptPayment
        {
            ReceiptId = voidId,
            Method = p.Method,
            Amount = -p.Amount,
            VoucherId = p.VoucherId,
            Reference = p.Reference,
        }).ToList();

        return (number, at) => new Receipt
        {
            Id = voidId,
            RegisterId = original.RegisterId,
            Number = number,
            Kind = ReceiptKind.Void,
            IssuedAt = at,
            BillId = original.BillId,
            CustomerId = original.CustomerId,
            Total = -original.Total,
            VoidsReceiptId = original.Id,
            VoidReason = reason.Trim(),
            CreatedByUserId = userId,
            Lines = lines,
            Payments = payments,
        };
    }

    /// <summary>Die Abfragen des Stornierens gegen die Datenbank.</summary>
    private sealed class DbVoidLookups(AispaDbContext db) : IVoidLookups
    {
        public Task<bool> IsVoidedAsync(Guid receiptId, CancellationToken ct) => db.Receipts.AnyAsync(r => r.VoidsReceiptId == receiptId, ct);

        public Task<List<Voucher>> VouchersSoldOnAsync(Guid receiptId, CancellationToken ct) =>
            db.Vouchers.Where(v => v.SoldOnReceiptId == receiptId).ToListAsync(ct);

        public Task<List<AppointmentDeposit>> OpenDepositsOfAsync(Guid receiptId, CancellationToken ct) =>
            db.AppointmentDeposits.Where(d => d.ReceiptId == receiptId && d.VoidedAt == null).ToListAsync(ct);

        public Task<List<AppointmentDeposit>> DepositsSettledOnAsync(Guid receiptId, CancellationToken ct) =>
            db.AppointmentDeposits.Where(d => d.SettledOnReceiptId == receiptId).ToListAsync(ct);

        public Task<Voucher> VoucherAsync(Guid id, CancellationToken ct) => db.Vouchers.FirstAsync(v => v.Id == id, ct);

        public Task<Bill> BillAsync(Guid id, CancellationToken ct) => db.Bills.Include(b => b.Lines).FirstAsync(b => b.Id == id, ct);
    }

    // ---------- Kassenlade und Tagesabschluss ----------

    public async Task<PosResult<CashMovementDto>> AddCashMovementAsync(
        Guid registerId, CashMovementInput input, string? userId, bool supervisor, CancellationToken ct)
    {
        if (input.Kind == CashMovementKind.Withdrawal && !supervisor)
        {
            return new PosError(PosErrorKind.Forbidden, "", "Bargeld entnehmen darf die Standortleitung; Einlagen darf auch die Rezeption buchen.");
        }

        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount)
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.Amount), "Betrag muss größer als 0 sein.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.Reason), "Pflichtfeld.");
        }

        var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
        if (register is null)
        {
            return new PosError(PosErrorKind.NotFound, "registerId", "Kasse nicht gefunden.");
        }

        if (IsOtherSite(register))
        {
            return new PosError(PosErrorKind.Conflict, "registerId", OtherSiteMessage);
        }

        var movement = new CashMovement
        {
            RegisterId = registerId,
            Kind = input.Kind,
            Amount = input.Amount,
            Reason = input.Reason.Trim(),
            At = time.GetUtcNow(),
            UserId = userId,
        };
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return new PosResult<CashMovementDto>(new(movement.Id, movement.Kind, movement.Amount, movement.Reason, movement.At), null);
    }

    /// <summary>Bericht seit dem letzten Abschluss; mit <paramref name="close"/> wird er festgeschrieben (Z-Bericht), sonst nur angezeigt (X-Bericht).</summary>
    public async Task<PosResult<DayClosingDto>> DayReportAsync(Guid registerId, DayClosingInput? close, string? userId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var register = await LockRegisterAsync(registerId, ct);
                if (close is not null)
                {
                    ClaimForThisSite(register);
                }

                var last = await db.DayClosings.AsNoTracking().Where(c => c.RegisterId == registerId).OrderByDescending(c => c.Until).FirstOrDefaultAsync(ct);
                var from = last?.Until ?? register.CreatedAt;
                var until = time.GetUtcNow();
                var opening = last?.CountedCash ?? 0;

                var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments)
                    .Where(r => r.RegisterId == registerId && r.IssuedAt >= from && r.IssuedAt < until)
                    .Where(r => r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void)
                    .AsSplitQuery()
                    .ToListAsync(ct);
                var movements = await db.CashMovements.AsNoTracking()
                    .Where(m => m.RegisterId == registerId && m.At >= from && m.At < until).ToListAsync(ct);
                var accounts = await db.RevenueAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);

                var report = BuildDayReport(registerId, from, until, opening, receipts, movements, accounts, close);

                if (close is not null)
                {
                    if (close.CountedCash < 0)
                    {
                        throw PosFailure.Invalid(nameof(close.CountedCash), "Gezählter Bestand kann nicht negativ sein.");
                    }

                    // Genehmigte Kartenzahlung ohne Beleg: Geld ist geflossen, also erst kassieren oder am Terminal stornieren.
                    if (await db.CardPayments.AsNoTracking()
                            .Where(c => c.Status == CardPaymentStatus.Approved && c.ReceiptId == null)
                            .Join(db.Bills.Where(b => b.RegisterId == registerId), c => c.BillId, b => b.Id, (c, _) => c.Amount)
                            .ToListAsync(ct) is { Count: > 0 } open)
                    {
                        throw PosFailure.Conflict(
                            $"Kartenzahlung über {Money(open.Sum())} ist genehmigt, aber noch keinem Beleg zugeordnet: zuerst kassieren oder am Terminal stornieren.");
                    }

                    db.DayClosings.Add(NewDayClosing(report, userId));
                    await db.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
                return new PosResult<DayClosingDto>(report, null);
            });
        }
        catch (PosFailure f)
        {
            db.ChangeTracker.Clear();
            return f.Error;
        }
    }

    /// <summary>
    /// Der Bericht einer Kasse für die Zeit seit dem letzten Abschluss aus Belegen (mit Zeilen und Zahlungen) und Kassenbewegungen; mit
    /// <paramref name="close"/> trägt er den gezählten Bestand (Z-Bericht). Reine Rechnung ohne Datenbank.
    /// </summary>
    internal static DayClosingDto BuildDayReport(Guid registerId, DateTimeOffset from, DateTimeOffset until, decimal opening,
        IReadOnlyList<Receipt> receipts, IReadOnlyList<CashMovement> movements, IReadOnlyDictionary<Guid, RevenueAccount> accounts, DayClosingInput? close)
    {
        var lines = receipts.SelectMany(r => r.Lines).ToList();
        var payments = receipts.SelectMany(r => r.Payments).GroupBy(p => p.Method).OrderBy(g => g.Key)
            .Select(g => new PaymentTotal(g.Key, g.Sum(p => p.Amount))).ToList();
        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var deposits = movements.Where(m => m.Kind == CashMovementKind.Deposit).Sum(m => m.Amount);
        var withdrawals = movements.Where(m => m.Kind == CashMovementKind.Withdrawal).Sum(m => m.Amount);
        var expected = opening + cash + deposits - withdrawals;
        var counted = close?.CountedCash ?? expected;

        var report = new DayClosingDto(
            Guid.CreateVersion7(),
            registerId,
            from,
            until,
            receipts.Count(r => r.Kind == ReceiptKind.Sale),
            receipts.Count(r => r.Kind == ReceiptKind.Void),
            receipts.Sum(r => r.Total),
            payments,
            TaxRates.Summarize(lines.Select(l => (l.TaxCategory, l.TaxPercent, l.Amount))),
            [.. lines.GroupBy(l => l.RevenueAccountId)
                .Select(g => new AccountTotal(g.Key, accounts[g.Key].Code, accounts[g.Key].Name, accounts[g.Key].CostCenter,
                    g.Sum(l => l.Amount), g.Sum(l => TaxRates.Net(l.Amount, l.TaxPercent))))
                .OrderBy(a => a.Code, StringComparer.Ordinal)],
            opening,
            deposits,
            withdrawals,
            expected,
            counted,
            counted - expected,
            Blank(close?.Note));

        return report;
    }

    /// <summary>Der festgeschriebene Tagesabschluss zu einem Bericht.</summary>
    internal static DayClosing NewDayClosing(DayClosingDto report, string? userId) => new()
    {
        Id = report.Id,
        RegisterId = report.RegisterId,
        From = report.From,
        Until = report.Until,
        CountedCash = report.CountedCash,
        UserId = userId,
        Report = JsonSerializer.Serialize(report, PosJson.Options),
    };

    // ---------- Belege ----------

    public async Task<ReceiptDto?> GetReceiptAsync(Guid receiptId, CancellationToken ct) =>
        (await GetReceiptsAsync([receiptId], ct)).SingleOrDefault();

    /// <summary>
    /// Mehrere Belege mit einer festen Zahl von Abfragen, egal wie viele es sind (Belegliste der Kassa lud früher jeden einzeln, über 300 Abfragen
    /// bei 100 Belegen). Reihenfolge wie <paramref name="receiptIds"/>; unbekannte Ids fehlen im Ergebnis.
    /// </summary>
    public async Task<List<ReceiptDto>> GetReceiptsAsync(IReadOnlyList<Guid> receiptIds, CancellationToken ct)
    {
        var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery()
            .Where(r => receiptIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        if (receipts.Count == 0)
        {
            return [];
        }

        var ids = receipts.Keys.ToList();
        var registerIds = receipts.Values.Select(r => r.RegisterId).Distinct().ToList();
        var registers = await db.CashRegisters.AsNoTracking().Where(r => registerIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var accountIds = receipts.Values.SelectMany(r => r.Lines).Select(l => l.RevenueAccountId).Distinct().ToList();
        var accounts = await db.RevenueAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code, ct);
        var voucherIds = receipts.Values.SelectMany(VoucherIdsOf).Distinct().ToList();
        var vouchers = await db.Vouchers.AsNoTracking().Where(v => voucherIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var voidedBy = (await db.Receipts.AsNoTracking().Where(r => r.VoidsReceiptId != null && ids.Contains(r.VoidsReceiptId.Value))
                .Select(r => new { Voided = r.VoidsReceiptId!.Value, r.Id }).ToListAsync(ct))
            .GroupBy(r => r.Voided).ToDictionary(g => g.Key, g => g.First().Id);
        var recipients = (await db.ReceiptRecipients.AsNoTracking().Where(r => ids.Contains(r.ReceiptId))
                .Select(r => new { r.ReceiptId, Recipient = new InvoiceRecipient(r.Name, r.Address, r.VatId, r.VatIdCheckId) }).ToListAsync(ct))
            .GroupBy(r => r.ReceiptId).ToDictionary(g => g.Key, g => g.First().Recipient);
        var locationIds = registers.Values.Select(r => r.LocationId).Distinct().ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == db.CurrentTenantId, ct);
        var deposits = (await db.AppointmentDeposits.Where(d => ids.Contains(d.ReceiptId)).Select(d => d.ReceiptId).Distinct().ToListAsync(ct)).ToHashSet();
        var sentMails = await SentDocuments.ListAsync(db, SentDocument.ReceiptKind, ids, ct);

        return [.. receiptIds.Where(receipts.ContainsKey).Select(id => Build(receipts[id]))];

        ReceiptDto Build(Receipt receipt)
        {
            var register = registers[receipt.RegisterId];
            var own = VoucherIdsOf(receipt).ToHashSet();
            var lines = receipt.Lines.OrderBy(l => l.Position).ToList();
            return new ReceiptDto(
                receipt.Id,
                register.Id,
                register.Code,
                receipt.Number,
                receipt.Kind,
                receipt.IssuedAt,
                register.ReceiptHeader,
                receipt.BillId,
                receipt.CustomerId,
                [.. lines.Select(l => new ReceiptLineDto(l.Description, l.Quantity, l.Amount, l.TaxCategory, l.TaxPercent, l.RevenueAccountId,
                    accounts[l.RevenueAccountId], l.AppointmentId, l.Discount, l.DiscountNote))],
                TaxRates.Summarize(lines.Select(l => (l.TaxCategory, l.TaxPercent, l.Amount))),
                [.. receipt.Payments.OrderBy(p => p.Method).Select(p => new PaymentDto(p.Method, p.Amount,
                    p.VoucherId is { } v ? vouchers[v].Code : null, p.Reference))],
                receipt.Total,
                receipt.Change,
                receipt.VoidsReceiptId,
                voidedBy.TryGetValue(receipt.Id, out var voidId) ? voidId : null,
                receipt.VoidReason,
                receipt.FiscalCode,
                receipt.SignatureDeviceFailed,
                receipt.Kind == ReceiptKind.Sale
                    ? [.. vouchers.Values.Where(v => v.SoldOnReceiptId == receipt.Id && own.Contains(v.Id)).Select(ToDto)]
                    : [],
                recipients.GetValueOrDefault(receipt.Id),
                CompanyProfiles.CompanyFor(locations[register.LocationId], tenant),
                SmallInvoiceLimit(register.Country),
                deposits.Contains(receipt.Id),
                sentMails.GetValueOrDefault(receipt.Id));
        }

        static IEnumerable<Guid> VoucherIdsOf(Receipt receipt) =>
            receipt.Payments.Select(p => p.VoucherId).Concat(receipt.Lines.Select(l => l.VoucherId)).OfType<Guid>();
    }

    /// <summary>
    /// Ist der Beleg die Einzahlung einer Anzahlung? Ihn hebt das allgemeine Storno an der Kassa nur für Inhaber und Manager auf (ADR 0073, Ä51); die Rückzahlung geht über
    /// <see cref="DepositReturns"/>.
    /// </summary>
    public Task<bool> IsDepositReceiptAsync(Guid receiptId, CancellationToken ct) => db.AppointmentDeposits.AnyAsync(d => d.ReceiptId == receiptId, ct);

    /// <summary>Kleinbetragsrechnung: AT § 11 Abs. 6 UStG 400 €, DE § 33 UStDV 250 €, CH vereinfachte Quittung bis 400 CHF.</summary>
    public static decimal SmallInvoiceLimit(FiscalCountry country) => country == FiscalCountry.DE ? 250m : 400m;

    /// <summary>
    /// Rechnungsempfänger zu einem Verkaufsbeleg ergänzen (Firmenkunde verlangt eine Rechnung). Einmal je Beleg; nicht für stornierte Belege.
    /// In Österreich braucht eine Rechnung über 10.000 € die UID-Nummer des Empfängers.
    /// </summary>
    public async Task<PosResult<ReceiptDto>> AddRecipientAsync(Guid receiptId, InvoiceRecipient input, string? userId, CancellationToken ct)
    {
        var receipt = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is null)
        {
            return new PosError(PosErrorKind.NotFound, "receiptId", "Beleg nicht gefunden.");
        }

        var country = await db.CashRegisters.Where(r => r.Id == receipt.RegisterId).Select(r => r.Country).FirstAsync(ct);
        var name = input.Name?.Trim() ?? "";
        var address = input.Address?.Trim() ?? "";
        var vatId = string.IsNullOrWhiteSpace(input.VatId) ? null : input.VatId.Trim().ToUpperInvariant();
        if (receipt.Kind != ReceiptKind.Sale)
        {
            return new PosError(PosErrorKind.Invalid, "receiptId", "Eine Rechnung gibt es nur zu einem Verkaufsbeleg.");
        }

        if (await db.Receipts.AnyAsync(r => r.VoidsReceiptId == receiptId, ct))
        {
            return new PosError(PosErrorKind.Invalid, "receiptId", "Der Beleg ist storniert.");
        }

        if (name.Length is 0 or > 200)
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.Name), "Name oder Firma des Empfängers (höchstens 200 Zeichen).");
        }

        if (address.Length is 0 or > 300)
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.Address), "Anschrift des Empfängers (höchstens 300 Zeichen).");
        }

        if (vatId is { Length: > 20 })
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.VatId), "UID-Nummer höchstens 20 Zeichen.");
        }

        if (country == FiscalCountry.AT && receipt.Total > 10_000m && vatId is null)
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.VatId), "Über 10.000 € braucht die Rechnung die UID-Nummer des Empfängers.");
        }

        if (input.VatIdCheckId is { } checkId && !await db.VatIdChecks.AnyAsync(c => c.Id == checkId && c.Status == VatIdCheckStatus.Valid
                && c.VatId == (VatIdChecks.Normalize(vatId) ?? vatId), ct))
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.VatIdCheckId), "Die UID-Prüfung passt nicht zu dieser UID-Nummer oder war nicht gültig.");
        }

        db.ReceiptRecipients.Add(new ReceiptRecipient
        {
            ReceiptId = receiptId,
            Name = name,
            Address = address,
            VatId = vatId,
            VatIdCheckId = input.VatIdCheckId,
            AddedAt = time.GetUtcNow(),
            UserId = userId,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new PosError(PosErrorKind.Conflict, "", "Zu diesem Beleg gibt es schon eine Rechnung.");
        }

        return new PosResult<ReceiptDto>(await GetReceiptAsync(receiptId, ct), null);
    }

    public static VoucherDto ToDto(Voucher v) =>
        new(v.Id, v.Code, v.Kind, v.InitialValue, v.Balance, v.TreatmentId, v.Status, v.ValidUntil, v.Units, v.UnitsLeft, v.CustomerId);

    /// <summary>Termin-Position dieses Belegs für die Behandlung des Gutscheins, die noch kein Gutschein mindert.</summary>
    private static bool Redeemable(ReceiptLine line, Bill bill, Voucher voucher, List<ReceiptLine> lines) =>
        line.Amount > 0 && line.VoucherId is null &&
        bill.Lines.Any(b => b.Id == line.BillLineId && b.Kind == BillLineKind.Appointment && b.TreatmentId == voucher.TreatmentId) &&
        !lines.Any(o => o.VoucherId is not null && o.Amount < 0 && o.BillLineId == line.BillLineId);

    /// <summary>
    /// Paket einlösen: je passendem Termin auf dem Beleg eine Behandlung, solange welche offen sind. Der Termin ist schon bezahlt
    /// (beim Verkauf versteuert), daher mindert die Einlösung ihn ganz; der Restwert sinkt um den Anteil einer Behandlung.
    /// </summary>
    private static void RedeemPackage(Voucher voucher, Bill bill, List<ReceiptLine> lines, Guid receiptId, ref int position)
    {
        var matching = lines.Where(l => Redeemable(l, bill, voucher, lines)).Take(voucher.UnitsLeft ?? 0).ToList();
        if (matching.Count == 0)
        {
            throw PosFailure.Invalid("payments", $"Paket {voucher.Code} gilt nur für eine bestimmte Behandlung, die nicht auf diesem Beleg steht.");
        }

        foreach (var line in matching)
        {
            var left = voucher.UnitsLeft!.Value - 1;
            var share = left == 0 ? voucher.Balance : decimal.Round(voucher.InitialValue / voucher.Units!.Value, 2, MidpointRounding.AwayFromZero);
            voucher.UnitsLeft = left;
            voucher.Balance = Math.Max(0, voucher.Balance - share);
            lines.Add(new ReceiptLine
            {
                ReceiptId = receiptId,
                Position = ++position,
                Description = $"Paket {voucher.Code} eingelöst ({voucher.Units - left}/{voucher.Units})",
                Quantity = 1,
                Amount = -line.Amount,
                TaxCategory = line.TaxCategory,
                TaxPercent = line.TaxPercent,
                RevenueAccountId = line.RevenueAccountId,
                BillLineId = line.BillLineId,
                VoucherId = voucher.Id,
            });
        }

        if (voucher.UnitsLeft == 0)
        {
            voucher.Balance = 0;
        }
    }

    public static string NormalizeCode(string? code) =>
        (code ?? "").Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

    /// <summary>
    /// Nummeriert, signiert und speichert einen Beleg. Vorher, wenn nötig, automatisch:
    /// Monats- bzw. Jahresbeleg beim ersten Beleg eines neuen Monats und Nullbeleg, sobald die Signatureinrichtung nach einem Ausfall wieder geht.
    /// </summary>
    private async Task<Receipt> IssueAsync(
        CashRegister register,
        ReceiptKind kind,
        CancellationToken ct,
        Func<long, DateTimeOffset, Receipt>? build = null,
        bool automatic = true) =>
        await IssueCoreAsync(register, kind, fiscal.For(register.Country), await ZoneAsync(register, ct), time.GetUtcNow(), build, automatic,
            receipt => db.Receipts.Add(receipt), ct);

    /// <summary>Wie <see cref="IssueAsync"/>, aber ohne Datenbank: Nummer, Signatur und Kassenstand; <paramref name="add"/> nimmt jeden fertigen Beleg entgegen.</summary>
    internal static async Task<Receipt> IssueCoreAsync(CashRegister register, ReceiptKind kind, IFiscalization fiscalization, TimeZoneInfo zone,
        DateTimeOffset now, Func<long, DateTimeOffset, Receipt>? build, bool automatic, Action<Receipt> add, CancellationToken ct)
    {
        if (automatic)
        {
            if (register.LastReceiptAt is { } last)
            {
                var previous = LocalTime.LocalDate(last, zone);
                var current = LocalTime.LocalDate(now, zone);
                if (previous.Year != current.Year)
                {
                    await IssueCoreAsync(register, ReceiptKind.Yearly, fiscalization, zone, now, null, false, add, ct);
                }
                else if (previous.Month != current.Month)
                {
                    await IssueCoreAsync(register, ReceiptKind.Monthly, fiscalization, zone, now, null, false, add, ct);
                }
            }

            if (register.SignatureDeviceFailedSince is not null && kind != ReceiptKind.Null
                && await fiscalization.IsDeviceAvailableAsync(register, ct))
            {
                await IssueCoreAsync(register, ReceiptKind.Null, fiscalization, zone, now, null, false, add, ct);
            }
        }

        var number = ++register.LastReceiptNumber;
        var result = build?.Invoke(number, now) ?? new Receipt { RegisterId = register.Id, Number = number, Kind = kind, IssuedAt = now };

        var signed = await fiscalization.SignAsync(register, new FiscalInput(
            kind, number, now, zone, [.. result.Lines.Select(l => (l.TaxCategory, l.Amount))], [.. result.Payments.Select(p => (p.Method, p.Amount))]), ct);
        result.FiscalCode = signed.Code;
        result.FiscalSignature = signed.Signature;
        result.SignatureDeviceFailed = signed.DeviceFailed;
        if (signed.DeviceFailed)
        {
            register.SignatureDeviceFailedSince ??= now;
        }
        else if (kind is ReceiptKind.Null or ReceiptKind.Start)
        {
            register.SignatureDeviceFailedSince = null;
        }

        register.LastReceiptAt = now;
        add(result);
        return result;
    }

    // ---------- Hilfen ----------

    private async Task<PosResult<ReceiptDto>> ReceiptTransactionAsync(Guid registerId, Func<CashRegister, Task<Receipt>> work, CancellationToken ct)
    {
        var result = await InRegisterAsync(registerId, async r => (await work(r)).Id, ct);
        return result.Error ?? new PosResult<ReceiptDto>(await GetReceiptAsync(result.Value, ct), null);
    }

    private async Task<PosResult<BillDto>> BillTransactionAsync(Guid registerId, Func<CashRegister, Task<Guid>> work, CancellationToken ct,
        bool loadAfter = true)
    {
        var result = await InRegisterAsync(registerId, work, ct);
        return result.Error ?? new PosResult<BillDto>(loadAfter ? await GetBillAsync(result.Value, ct) : null, null);
    }

    private async Task<(Guid Value, PosError? Error)> InRegisterAsync(Guid registerId, Func<CashRegister, Task<Guid>> work, CancellationToken ct)
    {
        if (registerId == Guid.Empty)
        {
            return (Guid.Empty, new PosError(PosErrorKind.NotFound, "registerId", "Kasse oder Rechnung nicht gefunden."));
        }

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var register = await LockRegisterAsync(registerId, ct);
                ClaimForThisSite(register);
                var value = await work(register);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return (value, (PosError?)null);
            });
        }
        catch (PosFailure f)
        {
            db.ChangeTracker.Clear();
            return (Guid.Empty, f.Error);
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            db.ChangeTracker.Clear();
            return (Guid.Empty, new PosError(PosErrorKind.Conflict, "",
                "Gleichzeitig wurde an einer anderen Kasse dasselbe geändert (Termin oder Gutschein). Bitte neu laden."));
        }
    }

    private async Task<CashRegister> LockRegisterAsync(Guid registerId, CancellationToken ct) =>
        (await db.CashRegisters.FromSql($"""SELECT * FROM aispa."CashRegisters" WHERE "Id" = {registerId} FOR UPDATE""").ToListAsync(ct))
        .FirstOrDefault() ?? throw PosFailure.NotFound("Kasse");

    private async Task<Guid> RegisterOfBillAsync(Guid billId, CancellationToken ct) =>
        await db.Bills.Where(b => b.Id == billId).Select(b => b.RegisterId).FirstOrDefaultAsync(ct);

    private async Task<Bill> LoadOpenBillAsync(CashRegister register, Guid billId, CancellationToken ct)
    {
        var bill = await db.Bills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == billId && b.RegisterId == register.Id, ct)
            ?? throw PosFailure.NotFound("Rechnung");
        return bill.Status == BillStatus.Open ? bill : throw PosFailure.Invalid("billId", "Die Rechnung ist schon bezahlt.");
    }

    private async Task<TimeZoneInfo> ZoneAsync(CashRegister register, CancellationToken ct) =>
        LocalTime.Zone(await db.Locations.Where(l => l.Id == register.LocationId).Select(l => l.TimeZone).FirstAsync(ct));

    private const string OtherSiteMessage =
        "Diese Kasse kassiert an einer anderen Stelle (anderer PC oder Server). Hier nur ansehen; zum Kassieren hier eine eigene Kasse anlegen.";

    private bool IsOtherSite(CashRegister register) => register.SiteId is { } owner && site.Get(db) is { } here && owner != here;

    /// <summary>
    /// Belege, Kassenlade und Abschluss nur an der Stelle der Kasse (ADR 0041): sonst entstünden beim Abgleich zwei Signaturketten.
    /// Ältere Kassen ohne Stelle gehören ab jetzt der, die sie zuerst benutzt.
    /// </summary>
    private void ClaimForThisSite(CashRegister register)
    {
        if (IsOtherSite(register))
        {
            throw new PosFailure(new PosError(PosErrorKind.Conflict, "registerId", OtherSiteMessage));
        }

        register.SiteId ??= site.Get(db);
    }

    private static void EnsureActive(CashRegister register)
    {
        if (!register.IsActive)
        {
            throw PosFailure.Invalid("registerId", "Die Kasse ist außer Betrieb.");
        }
    }

    private async Task<string> NewVoucherCodeAsync(CancellationToken ct)
    {
        // Ohne leicht verwechselbare Zeichen (0/O, 1/I/L); 31^12 Möglichkeiten, Codes sind nicht erratbar.
        // Mindestens eine Ziffer: der Assistent erkennt einen Gutscheincode in einer Frage nur daran (BusinessIntents), reine Buchstabencodes
        // (rund 3 % der Züge) fände er nicht.
        const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        while (true)
        {
            var chars = RandomNumberGenerator.GetItems<char>(Alphabet, 12);
            if (!chars.Any(char.IsDigit))
            {
                continue;
            }

            var code = $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
            if (!await db.Vouchers.AnyAsync(v => v.Code == code, ct))
            {
                return code;
            }
        }
    }

    private static string Money(decimal amount) => amount.ToString("C", German);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsConcurrencyConflict(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => true,
        { InnerException: { } inner } => IsConcurrencyConflict(inner),
        _ => false,
    };

    /// <summary>IBAN-Prüfsumme nach ISO 13616 (Modulo 97).</summary>
    public static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34 || !IbanFormat().IsMatch(iban))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var value = char.IsDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = value >= 10 ? ((remainder * 100) + value) % 97 : ((remainder * 10) + value) % 97;
        }

        return remainder == 1;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,50}$")]
    private static partial Regex RegisterCode();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]+$")]
    private static partial Regex IbanFormat();
}

internal static class PosJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

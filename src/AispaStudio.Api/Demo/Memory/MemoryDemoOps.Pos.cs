using System.Security.Cryptography;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Kassieren im Speicher: Rechnung, Positionen, Bezahlen, Storno, Kassenlade und Tagesabschluss. Die Rechenschritte (Position, Verteilung der Zahlung,
/// Storno, Nummerierung und Signatur, Tagesbericht) sind dieselben wie in <c>PosService</c>; hier stehen nur das Nachschlagen im Speicher und das Zurückrollen
/// bei einem Fehler, das dort die Transaktion übernimmt.
/// </summary>
internal sealed partial class MemoryDemoOps
{
    private readonly Dictionary<Guid, List<Guid>> _receiptsOfBill = [];

    // ---------- Rechnungen ----------

    public Task<PosResult<BillDto>> CreateBillAsync(BillInput input, CancellationToken ct)
    {
        if (input.AreaVisitIds is { Count: > 0 })
        {
            throw new NotSupportedException("Der Verlauf im Speicher kassiert keine Eintritte.");
        }

        return InRegisterAsync(input.RegisterId, (register, undo) =>
        {
            Claim(register);
            if (input.CustomerId is { } customerId && !_snapshot.Customers.ContainsKey(customerId))
            {
                throw PosFailure.NotFound("Kunde");
            }

            var bill = new Bill { RegisterId = register.Id, CustomerId = input.CustomerId, CreatedAt = _clock.GetUtcNow() };
            _world.Bills.Add(bill);
            _world.BillsById[bill.Id] = bill;
            undo.Add(() =>
            {
                _world.Bills.Remove(bill);
                _world.BillsById.Remove(bill.Id);
            });
            foreach (var appointmentId in input.AppointmentIds)
            {
                AddLine(register, bill, new BillLineInput(BillLineKind.Appointment, appointmentId, null, 1, null, 0, null, null, null, null, null), undo);
            }

            return Task.FromResult(bill.Id);
        }, BillOf);
    }

    public Task<PosResult<BillDto>> AddLineAsync(Guid billId, BillLineInput input, bool supervisor, CancellationToken ct)
    {
        var registerId = _world.BillsById.TryGetValue(billId, out var found) ? found.RegisterId : Guid.Empty;
        return InRegisterAsync(registerId, (register, undo) =>
        {
            Claim(register);
            var bill = OpenBill(register, billId);
            var line = AddLine(register, bill, input, undo);
            // Nur ein von Hand gegebener Rabatt; der Kundenrabatt wurde beim Festlegen in der Kundenkartei geprüft.
            if (input.Discount != 0 && !supervisor && input.Discount * 100 > _snapshot.Tenant.DiscountLimitPercent * line.UnitPrice * line.Quantity)
            {
                throw PosFailure.Forbidden($"Rabatte über {PosService.PercentText(_snapshot.Tenant.DiscountLimitPercent)} % gibt die Standortleitung oder die Inhaberin.");
            }

            return Task.FromResult(bill.Id);
        }, BillOf);
    }

    public Task<PosResult<BillDto>> ApplyPromotionAsync(Guid billId, Guid promotionId, CancellationToken ct)
    {
        var registerId = _world.BillsById.TryGetValue(billId, out var found) ? found.RegisterId : Guid.Empty;
        return InRegisterAsync(registerId, (register, undo) =>
        {
            Claim(register);
            var bill = OpenBill(register, billId);
            var promotion = _snapshot.Promotions.GetValueOrDefault(promotionId) ?? throw PosFailure.NotFound("Aktion");
            var today = LocalTime.LocalDate(_clock.GetUtcNow(), Zone(_snapshot.Locations[register.LocationId]));
            PosService.ApplyPromotion(bill, promotion, register.LocationId, today);
            foreach (var line in bill.Lines)
            {
                _world.Normalize(line);
            }

            return Task.FromResult(bill.Id);
        }, BillOf);
    }

    public Task<decimal> OpenAmountAsync(Guid billId, CancellationToken ct) => Task.FromResult(BillOf(billId).Open);

    public Task<PosError?> DeleteBillAsync(Guid billId, CancellationToken ct)
    {
        if (!_world.BillsById.TryGetValue(billId, out var bill))
        {
            return Task.FromResult<PosError?>(new PosError(PosErrorKind.NotFound, "registerId", "Kasse oder Rechnung nicht gefunden."));
        }

        if (bill.Status != BillStatus.Open)
        {
            return Task.FromResult<PosError?>(PosFailure.Invalid("billId", "Die Rechnung ist schon bezahlt.").Error);
        }

        if (bill.Lines.Any(l => l.PaidAmount != 0) || _receiptsOfBill.ContainsKey(billId))
        {
            return Task.FromResult<PosError?>(PosFailure.Invalid("billId", "Auf diese Rechnung wurde schon bezahlt; zuerst die Belege stornieren.").Error);
        }

        foreach (var line in bill.Lines.Where(l => l.AppointmentId is not null && l.Kind == BillLineKind.Appointment))
        {
            _world.BilledAppointments.Remove(line.AppointmentId!.Value);
        }

        _world.Bills.Remove(bill);
        _world.BillsById.Remove(billId);
        return Task.FromResult<PosError?>(null);
    }

    /// <summary>Eine Position auf die Rechnung: die Art bestimmt Text, Preis und Steuer, der Rest ist <c>PosService.NewBillLine</c>.</summary>
    private BillLine AddLine(CashRegister register, Bill bill, BillLineInput input, List<Action> undo)
    {
        var hadCustomer = bill.CustomerId is not null;
        var quantity = input.Quantity <= 0 ? 1 : input.Quantity;
        string description;
        decimal unitPrice;
        TaxCategory category;
        Guid account;
        Guid? appointmentId = null, treatmentId = null, articleId = null;
        VoucherKind? voucherKind = null;
        DateOnly? validUntil = null;

        switch (input.Kind)
        {
            case BillLineKind.Appointment:
                {
                    var appointment = input.AppointmentId is { } id && _world.AppointmentsById.TryGetValue(id, out var found)
                        ? found
                        : throw PosFailure.NotFound("Termin");
                    if (appointment.LocationId != register.LocationId)
                    {
                        throw PosFailure.Invalid(nameof(input.AppointmentId), "Der Termin gehört zu einem anderen Standort.");
                    }

                    if (appointment.Status == AppointmentStatus.Cancelled)
                    {
                        throw PosFailure.Invalid(nameof(input.AppointmentId), "Der Termin ist storniert.");
                    }

                    if (appointment.PlatformPayment != PlatformPayment.None)
                    {
                        throw new NotSupportedException("Der Verlauf im Speicher kassiert keine Plattformbuchungen.");
                    }

                    if (_world.BilledAppointments.Contains(appointment.Id))
                    {
                        throw PosFailure.Conflict("Dieser Termin steht schon auf einer Rechnung.");
                    }

                    var local = TimeZoneInfo.ConvertTime(appointment.Start, Zone(_snapshot.Locations[register.LocationId]));
                    description = $"{TreatmentName(appointment)} {local:dd.MM.yyyy HH:mm}";
                    unitPrice = appointment.Price;
                    (category, account) = TreatmentSettings(appointment.TreatmentId);
                    appointmentId = appointment.Id;
                    treatmentId = appointment.TreatmentId;
                    quantity = 1;
                    bill.CustomerId ??= appointment.CustomerId;
                    break;
                }

            case BillLineKind.Article:
                {
                    var article = input.ArticleId is { } id && _snapshot.Articles.TryGetValue(id, out var found) && found.IsActive
                        ? found
                        : throw PosFailure.NotFound("Artikel");
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
                    validUntil = PosService.VoucherValidUntil(register.Country, input,
                        LocalTime.LocalDate(_clock.GetUtcNow(), Zone(_snapshot.Locations[register.LocationId])));
                    if (input.Units is not null)
                    {
                        throw new NotSupportedException("Der Verlauf im Speicher verkauft keine Pakete.");
                    }

                    if (voucherKind == VoucherKind.SinglePurpose)
                    {
                        var treatment = input.VoucherTreatmentId is { } id && _snapshot.Treatments.TryGetValue(id, out var found) && found.IsActive
                            ? found
                            : throw PosFailure.NotFound("Behandlung");
                        description = $"Gutschein {treatment.Name}";
                        unitPrice = input.UnitPrice ?? treatment.PriceAt(register.LocationId);
                        (category, account) = TreatmentSettings(treatment.Id);
                        treatmentId = treatment.Id;
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
                throw new NotSupportedException($"Der Verlauf im Speicher kennt die Position {input.Kind} nicht.");
        }

        var line = PosService.NewBillLine(register, bill, input,
            new PosService.LineBasis(description, unitPrice, quantity, category, account, appointmentId, treatmentId, articleId, null, voucherKind, null, validUntil));
        undo.Add(() => bill.Lines.Remove(line));
        if (!_snapshot.Accounts.ContainsKey(account))
        {
            throw PosFailure.NotFound("Erlöskonto");
        }

        if (appointmentId is { } billed && input.Kind == BillLineKind.Appointment)
        {
            _world.BilledAppointments.Add(billed);
            undo.Add(() => _world.BilledAppointments.Remove(billed));
        }

        // Kundenrabatt: auf die neue Position; kam die Kundin erst mit dieser Position (Termin, Eintritt), auch auf die schon offenen.
        if (bill.CustomerId is { } customerId && _snapshot.Customers[customerId].DiscountPercent is { } percent)
        {
            PosService.DiscountLines(hadCustomer ? [line] : bill.Lines, percent, _snapshot.Customers[customerId].DiscountScope);
        }

        _world.Normalize(line);
        return line;
    }

    private (TaxCategory, Guid) TreatmentSettings(Guid treatmentId) =>
        _snapshot.PosSettings.TryGetValue(treatmentId, out var s) ? (s.TaxCategory, s.RevenueAccountId) : (TaxCategory.Normal, PosModel.Treatments.Id);

    private BillDto BillOf(Guid billId)
    {
        var bill = _world.BillsById[billId];
        var customer = bill.CustomerId is { } id ? _snapshot.Customers[id] : null;
        var lines = bill.Lines.OrderBy(l => l.Position).ToList();
        var total = lines.Sum(l => l.Amount);
        var paid = lines.Sum(l => l.PaidAmount);
        var cashPercent = _snapshot.Tenant.CashDiscountPercent;
        var cashDiscount = PosService.CashDiscountParts(lines.Select(l => (l.Kind, l.TaxCategory, l.TaxPercent, l.RevenueAccountId, l.Amount - l.PaidAmount)),
            cashPercent).Sum(p => p.Discount);
        return new BillDto(bill.Id, bill.RegisterId, bill.CustomerId, customer?.InHouseName, bill.Status,
            [.. lines.Select(l => new BillLineDto(l.Id, l.Kind, l.Description, l.Quantity, l.UnitPrice, l.Discount, l.Amount, l.PaidAmount,
                l.TaxCategory, l.TaxPercent, l.RevenueAccountId, l.AppointmentId, l.AreaVisitId, l.DiscountNote, l.TreatmentId, 0))],
            total, paid, total - paid, _receiptsOfBill.GetValueOrDefault(billId, []), cashPercent, cashDiscount, 0, 0, null, customer?.LegalName);
    }

    private static Bill OpenBill(CashRegister register, Bill bill) =>
        bill.RegisterId == register.Id && bill.Status == BillStatus.Open
            ? bill
            : throw PosFailure.Invalid("billId", "Die Rechnung ist schon bezahlt.");

    private Bill OpenBill(CashRegister register, Guid billId) =>
        _world.BillsById.TryGetValue(billId, out var bill) ? OpenBill(register, bill) : throw PosFailure.NotFound("Rechnung");

    // ---------- Bezahlen und Storno ----------

    public async Task<PosResult<ReceiptDto>> CheckoutAsync(Guid billId, CheckoutRequest request, string? userId, CancellationToken ct)
    {
        var registerId = _world.BillsById.TryGetValue(billId, out var found) ? found.RegisterId : Guid.Empty;
        var lookups = new MemoryCheckoutLookups(this);
        return await InRegisterAsync(registerId, async (register, undo) =>
        {
            Claim(register);
            var bill = OpenBill(register, billId);
            var states = bill.Lines.Select(l => (l, l.PaidAmount)).ToList();
            undo.Add(() =>
            {
                foreach (var (line, paid) in states)
                {
                    line.PaidAmount = paid;
                }

                bill.Status = BillStatus.Open;
                lookups.Restore();
            });
            var now = _clock.GetUtcNow();
            var zone = Zone(_snapshot.Locations[register.LocationId]);
            var draft = await PosService.BuildCheckoutAsync(register, bill, request, now, LocalTime.LocalDate(now, zone),
                PaymentMethods.Available(register.Country, _payments), lookups, ct);
            foreach (var voucher in draft.NewVouchers)
            {
                _world.AddVoucher(voucher);
            }

            var receipt = await IssueAsync(register, ReceiptKind.Sale, zone, now, (number, at) => new Receipt
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
            }, ct);
            if (!_receiptsOfBill.TryGetValue(bill.Id, out var list))
            {
                _receiptsOfBill[bill.Id] = list = [];
            }

            list.Add(receipt.Id);
            return receipt.Id;
        }, ReceiptOf);
    }

    public async Task<PosResult<ReceiptDto>> VoidAsync(Guid receiptId, string? reason, string? userId, bool supervisor, CancellationToken ct)
    {
        if (!_world.ReceiptsById.TryGetValue(receiptId, out var original))
        {
            return new PosError(PosErrorKind.NotFound, "receiptId", "Beleg nicht gefunden.");
        }

        return await InRegisterAsync(original.RegisterId, async (register, undo) =>
        {
            Claim(register);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw PosFailure.Invalid(nameof(reason), "Für ein Storno ist ein Grund Pflicht.");
            }

            var now = _clock.GetUtcNow();
            var build = await PosService.BuildVoidAsync(original, reason, userId, supervisor, now, new MemoryVoidLookups(this), ct);
            var receipt = await IssueAsync(register, ReceiptKind.Void, Zone(_snapshot.Locations[register.LocationId]), now, build, ct);
            return receipt.Id;
        }, ReceiptOf);
    }

    private async Task<Receipt> IssueAsync(CashRegister register, ReceiptKind kind, TimeZoneInfo zone, DateTimeOffset now, Func<long, DateTimeOffset, Receipt> build,
        CancellationToken ct) =>
        await PosService.IssueCoreAsync(register, kind, Fiscal(register), zone, now, build, true, _world.AddReceipt, ct);

    private ReceiptDto ReceiptOf(Guid receiptId)
    {
        var receipt = _world.ReceiptsById[receiptId];
        var register = _snapshot.Registers[receipt.RegisterId];
        var location = _snapshot.Locations[register.LocationId];
        var lines = receipt.Lines.OrderBy(l => l.Position).ToList();
        var voidedBy = _world.Receipts.FirstOrDefault(r => r.VoidsReceiptId == receipt.Id)?.Id;
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
                _snapshot.Accounts[l.RevenueAccountId].Code, l.AppointmentId, l.Discount, l.DiscountNote))],
            TaxRates.Summarize(lines.Select(l => (l.TaxCategory, l.TaxPercent, l.Amount))),
            [.. receipt.Payments.OrderBy(p => p.Method).Select(p => new PaymentDto(p.Method, p.Amount,
                p.VoucherId is { } v ? _world.VouchersById[v].Code : null, p.Reference))],
            receipt.Total,
            receipt.Change,
            receipt.VoidsReceiptId,
            voidedBy,
            receipt.VoidReason,
            receipt.FiscalCode,
            receipt.SignatureDeviceFailed,
            receipt.Kind == ReceiptKind.Sale
                ? [.. _world.Vouchers.Where(v => v.SoldOnReceiptId == receipt.Id).Select(PosService.ToDto)]
                : [],
            null,
            CompanyProfiles.CompanyFor(location, _snapshot.Tenant),
            PosService.SmallInvoiceLimit(register.Country));
    }

    // ---------- Kassenlade und Tagesabschluss ----------

    public Task<PosResult<CashMovementDto>> AddCashMovementAsync(Guid registerId, CashMovementInput input, string? userId, bool supervisor, CancellationToken ct)
    {
        if (input.Kind == CashMovementKind.Withdrawal && !supervisor)
        {
            return Task.FromResult<PosResult<CashMovementDto>>(
                new PosError(PosErrorKind.Forbidden, "", "Bargeld entnehmen darf die Standortleitung; Einlagen darf auch die Rezeption buchen."));
        }

        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount)
        {
            return Task.FromResult<PosResult<CashMovementDto>>(new PosError(PosErrorKind.Invalid, nameof(input.Amount), "Betrag muss größer als 0 sein."));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Task.FromResult<PosResult<CashMovementDto>>(new PosError(PosErrorKind.Invalid, nameof(input.Reason), "Pflichtfeld."));
        }

        if (!_snapshot.Registers.ContainsKey(registerId))
        {
            return Task.FromResult<PosResult<CashMovementDto>>(new PosError(PosErrorKind.NotFound, "registerId", "Kasse nicht gefunden."));
        }

        var movement = new CashMovement
        {
            RegisterId = registerId,
            Kind = input.Kind,
            Amount = input.Amount,
            Reason = input.Reason.Trim(),
            At = _clock.GetUtcNow(),
            UserId = userId,
        };
        _world.Normalize(movement);
        _world.CashMovements.Add(movement);
        return Task.FromResult(new PosResult<CashMovementDto>(new(movement.Id, movement.Kind, movement.Amount, movement.Reason, movement.At), null));
    }

    public Task<PosResult<DayClosingDto>> DayReportAsync(Guid registerId, DayClosingInput? close, string? userId, CancellationToken ct)
    {
        try
        {
            if (!_snapshot.Registers.TryGetValue(registerId, out var register))
            {
                throw PosFailure.NotFound("Kasse");
            }

            if (close is not null)
            {
                Claim(register);
            }

            var last = _world.DayClosings.Where(c => c.RegisterId == registerId).MaxBy(c => c.Until);
            var from = last?.Until ?? register.CreatedAt;
            var until = _clock.GetUtcNow();
            var receipts = _world.ReceiptsByRegister.GetValueOrDefault(registerId, [])
                .Where(r => r.IssuedAt >= from && r.IssuedAt < until && r.Kind is ReceiptKind.Sale or ReceiptKind.Void).ToList();
            var movements = _world.CashMovements.Where(m => m.RegisterId == registerId && m.At >= from && m.At < until).ToList();
            var report = PosService.BuildDayReport(registerId, from, until, last?.CountedCash ?? 0, receipts, movements, _snapshot.Accounts, close);
            if (close is not null)
            {
                if (close.CountedCash < 0)
                {
                    throw PosFailure.Invalid(nameof(close.CountedCash), "Gezählter Bestand kann nicht negativ sein.");
                }

                var closing = PosService.NewDayClosing(report, userId);
                _world.Normalize(closing);
                _world.DayClosings.Add(closing);
            }

            return Task.FromResult(new PosResult<DayClosingDto>(report, null));
        }
        catch (PosFailure failure)
        {
            return Task.FromResult<PosResult<DayClosingDto>>(failure.Error);
        }
    }

    // ---------- Gemeinsames ----------

    /// <summary>Die Kasse gehört dieser Stelle (sonst gäbe es beim Abgleich zwei Signaturketten); ältere Kassen ohne Stelle übernimmt sie.</summary>
    private void Claim(CashRegister register)
    {
        if (!register.IsActive)
        {
            throw PosFailure.Invalid("registerId", "Die Kasse ist außer Betrieb.");
        }

        if (register.SiteId is { } owner && _siteId is { } here && owner != here)
        {
            throw new PosFailure(new PosError(PosErrorKind.Conflict, "registerId", "Diese Kasse kassiert an einer anderen Stelle."));
        }

        register.SiteId ??= _siteId;
    }

    /// <summary>Wie <c>PosService.InRegisterAsync</c>: führt aus und macht bei einem Abbruch rückgängig, was der Vorgang im Speicher geändert hat.</summary>
    private async Task<PosResult<T>> InRegisterAsync<T>(Guid registerId, Func<CashRegister, List<Action>, Task<Guid>> work, Func<Guid, T> load)
    {
        if (registerId == Guid.Empty || !_snapshot.Registers.TryGetValue(registerId, out var register))
        {
            return new PosError(PosErrorKind.NotFound, "registerId", "Kasse oder Rechnung nicht gefunden.");
        }

        var undo = new List<Action>();
        try
        {
            return new PosResult<T>(load(await work(register, undo)), null);
        }
        catch (PosFailure failure)
        {
            for (var i = undo.Count - 1; i >= 0; i--)
            {
                undo[i]();
            }

            return failure.Error;
        }
    }

    /// <summary>Die Abfragen des Kassierens im Speicher; Gutscheine, die der Vorgang ändert, merkt er sich, damit ein Abbruch sie zurücksetzt.</summary>
    private sealed class MemoryCheckoutLookups(MemoryDemoOps owner) : PosService.ICheckoutLookups
    {
        private readonly Dictionary<Voucher, (decimal Balance, VoucherStatus Status, int? UnitsLeft)> _touched = [];

        public Task<string> NewVoucherCodeAsync(CancellationToken ct)
        {
            // Ohne leicht verwechselbare Zeichen (0/O, 1/I/L) und mit mindestens einer Ziffer; wie in PosService.
            const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
            while (true)
            {
                var chars = RandomNumberGenerator.GetItems<char>(Alphabet, 12);
                if (!chars.Any(char.IsDigit))
                {
                    continue;
                }

                var code = $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
                if (!owner._world.VouchersByCode.ContainsKey(code))
                {
                    return Task.FromResult(code);
                }
            }
        }

        public Task<Voucher?> FindVoucherAsync(string code, CancellationToken ct)
        {
            owner._world.VouchersByCode.TryGetValue(code, out var voucher);
            if (voucher is not null)
            {
                _touched.TryAdd(voucher, (voucher.Balance, voucher.Status, voucher.UnitsLeft));
            }

            return Task.FromResult(voucher);
        }

        public Task<List<AppointmentDeposit>> OpenDepositsAsync(Guid appointmentId, CancellationToken ct) => Task.FromResult<List<AppointmentDeposit>>([]);

        public Task<(long Number, string RegisterCode)> DepositReceiptAsync(Guid receiptId, CashRegister current, CancellationToken ct) =>
            throw new NotSupportedException("Der Verlauf im Speicher legt keine Anzahlungen an.");

        public Task<decimal> CashDiscountPercentAsync(Guid registerId, CancellationToken ct) => Task.FromResult(owner._snapshot.Tenant.CashDiscountPercent);

        public Task<(decimal Amount, string? Reference)> PlatformPrepaidAsync(IEnumerable<(BillLineKind Kind, Guid? AppointmentId, decimal Open)> lines,
            CancellationToken ct) => Task.FromResult((0m, (string?)null));

        public Task<CardPayment?> FindCardPaymentAsync(Guid id, Guid billId, CancellationToken ct) => Task.FromResult<CardPayment?>(null);

        public void Restore()
        {
            foreach (var (voucher, state) in _touched)
            {
                (voucher.Balance, voucher.Status, voucher.UnitsLeft) = state;
            }
        }
    }

    private sealed class MemoryVoidLookups(MemoryDemoOps owner) : PosService.IVoidLookups
    {
        public Task<bool> IsVoidedAsync(Guid receiptId, CancellationToken ct) => Task.FromResult(owner._world.VoidedReceipts.Contains(receiptId));

        public Task<List<Voucher>> VouchersSoldOnAsync(Guid receiptId, CancellationToken ct) =>
            Task.FromResult(owner._world.Vouchers.Where(v => v.SoldOnReceiptId == receiptId).ToList());

        public Task<List<AppointmentDeposit>> OpenDepositsOfAsync(Guid receiptId, CancellationToken ct) => Task.FromResult<List<AppointmentDeposit>>([]);

        public Task<List<AppointmentDeposit>> DepositsSettledOnAsync(Guid receiptId, CancellationToken ct) => Task.FromResult<List<AppointmentDeposit>>([]);

        public Task<Voucher> VoucherAsync(Guid id, CancellationToken ct) => Task.FromResult(owner._world.VouchersById[id]);

        public Task<Bill> BillAsync(Guid id, CancellationToken ct) => Task.FromResult(owner._world.BillsById[id]);
    }
}

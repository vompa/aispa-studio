using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal.Rksv;

/// <summary>
/// Österreich: jeder Beleg trägt einen signierten maschinenlesbaren Code mit verschlüsseltem Umsatzzähler und ist mit dem vorigen verkettet.
/// Fällt die Signatureinrichtung aus, wird weiter kassiert; die Belege tragen „Sicherheitseinrichtung ausgefallen“,
/// nach der Behebung folgt ein Nullbeleg (Sammelbeleg). Ab 48 Stunden Ausfall muss die Meldung an FinanzOnline erfolgen.
/// </summary>
internal sealed class RksvFiscalization(IEnumerable<IRksvSignatureDevice> devices) : IFiscalization
{
    public FiscalCountry Country => FiscalCountry.AT;

    private IRksvSignatureDevice Device(CashRegister register) =>
        devices.Single(d => d.ProviderId == (register.FiscalProvider ?? "test"));

    public bool IsTestProvider(CashRegister register) => Device(register).ZdaId == "AT0";

    public string? Notice(CashRegister register) => IsTestProvider(register)
        ? "Test-Signatureinrichtung: nicht für den Echtbetrieb. Vor dem Start eine zertifizierte Signatureinrichtung anbinden, " +
          "Kassa und Signatureinrichtung in FinanzOnline registrieren und den Startbeleg mit der BMF-Belegcheck-App prüfen."
        : null;

    public async Task<string?> InitializeAsync(CashRegister register, CancellationToken ct)
    {
        register.TurnoverKey = Convert.ToBase64String(RksvCode.NewTurnoverKey());
        register.CertificateSerial = await FiscalAdapterCalls.TryAsync(() => Device(register).SetUpAsync(register, ct), "", ct);
        return string.IsNullOrEmpty(register.CertificateSerial) ? "Die Signatureinrichtung hat die Anmeldung der Kassa nicht bestätigt." : null;
    }

    public Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct) =>
        FiscalAdapterCalls.TryAsync(() => Device(register).IsAvailableAsync(register, ct), false, ct);

    public Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) =>
        FiscalAdapterCalls.TryAsync(() => Device(register).GetCertificateAsync(register, ct), null, ct);

    public async Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct)
    {
        var device = Device(register);
        var amounts = new RksvAmounts(
            Sum(input, TaxCategory.Normal), Sum(input, TaxCategory.Reduced1), Sum(input, TaxCategory.Reduced2),
            Sum(input, TaxCategory.Zero), Sum(input, TaxCategory.Special));

        // Auch Stornos zählen (mit negativem Betrag) in den Umsatzzähler; auf dem Beleg steht dann „STO“ statt des Zählers.
        register.TurnoverCounter += (long)(amounts.Total * 100m);
        var turnover = input.Kind == ReceiptKind.Void
            ? RksvCode.VoidMarker
            : RksvCode.EncryptTurnover(register.TurnoverCounter, register.Code, input.Number, Convert.FromBase64String(register.TurnoverKey!));

        var machineCode = RksvCode.MachineCode(
            device.ZdaId,
            register.Code,
            input.Number,
            TimeZoneInfo.ConvertTimeFromUtc(input.IssuedAt.UtcDateTime, input.Zone),
            amounts,
            turnover,
            register.CertificateSerial!,
            RksvCode.ChainValue(register.LastChainValue, register.Code));
        var signingInput = RksvCode.SigningInput(machineCode);

        var signature = await FiscalAdapterCalls.TryAsync(() => device.SignAsync(register, Encoding.ASCII.GetBytes(signingInput), ct), null, ct);
        var failed = signature is null;
        signature ??= RksvCode.DeviceFailedSignature;

        var jws = RksvCode.Jws(signingInput, signature);
        register.LastChainValue = jws;
        return new FiscalResult(RksvCode.QrCode(machineCode, signature), jws, failed);
    }

    private static decimal Sum(FiscalInput input, TaxCategory category) =>
        input.Amounts.Where(a => a.Category == category).Sum(a => a.Gross);
}

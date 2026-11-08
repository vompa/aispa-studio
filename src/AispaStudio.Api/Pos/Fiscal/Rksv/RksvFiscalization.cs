using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal.Rksv;

/// <summary>
/// Österreich: jeder Beleg trägt einen signierten maschinenlesbaren Code mit verschlüsseltem Umsatzzähler und ist mit dem vorigen verkettet.
/// Fällt die Signatureinrichtung aus, wird weiter kassiert; die Belege tragen „Sicherheitseinrichtung ausgefallen“,
/// nach der Behebung folgt ein Nullbeleg (Sammelbeleg). Ab 48 Stunden Ausfall muss die Meldung an FinanzOnline erfolgen.
/// </summary>
internal sealed class RksvFiscalization(IRksvSignatureDevice device) : IFiscalization
{
    public FiscalCountry Country => FiscalCountry.AT;

    public string? Notice => device.ZdaId == "AT0"
        ? "Test-Signatureinrichtung: nicht für den Echtbetrieb. Vor dem Start eine zertifizierte Signatureinrichtung anbinden, " +
          "Kasse und Signatureinrichtung in FinanzOnline registrieren und den Startbeleg mit der BMF-Belegcheck-App prüfen."
        : null;

    public async Task<string?> InitializeAsync(CashRegister register, CancellationToken ct)
    {
        register.TurnoverKey = Convert.ToBase64String(RksvCode.NewTurnoverKey());
        register.CertificateSerial = await device.SetUpAsync(register, ct);
        return null;
    }

    public Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct) => device.IsAvailableAsync(register, ct);

    public Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) => device.GetCertificateAsync(register, ct);

    public async Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct)
    {
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

        var signature = await device.SignAsync(register, Encoding.ASCII.GetBytes(signingInput), ct);
        var failed = signature is null;
        signature ??= RksvCode.DeviceFailedSignature;

        var jws = RksvCode.Jws(signingInput, signature);
        register.LastChainValue = jws;
        return new FiscalResult(RksvCode.QrCode(machineCode, signature), jws, failed);
    }

    private static decimal Sum(FiscalInput input, TaxCategory category) =>
        input.Amounts.Where(a => a.Category == category).Sum(a => a.Gross);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>
/// Schweiz: keine Signaturpflicht für Kassen. Belege sind trotzdem lückenlos nummeriert und über einen Hash verkettet,
/// damit nachträgliche Änderungen auffallen (Aufbewahrung nach GeBüV, 10 Jahre).
/// </summary>
internal sealed class SwissFiscalization : IFiscalization
{
    public FiscalCountry Country => FiscalCountry.CH;

    public string? Notice => null;

    public Task<string?> InitializeAsync(CashRegister register, CancellationToken ct) => Task.FromResult<string?>(null);

    public Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct) => Task.FromResult(true);

    public Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) => Task.FromResult<byte[]?>(null);

    public Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct)
    {
        var total = input.Amounts.Sum(a => a.Gross);
        register.TurnoverCounter += (long)(total * 100m);
        var content = string.Join('|', register.LastChainValue ?? register.Code, input.Kind, input.Number,
            input.IssuedAt.ToUnixTimeMilliseconds(), total.ToString("0.00", CultureInfo.InvariantCulture));
        register.LastChainValue = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return Task.FromResult(new FiscalResult(null, register.LastChainValue, false));
    }
}

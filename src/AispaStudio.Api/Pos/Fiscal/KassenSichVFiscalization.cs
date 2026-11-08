using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>
/// Technische Sicherheitseinrichtung (TSE) nach KassenSichV/BSI TR-03153, z. B. als Cloud-TSE (fiskaly, Deutsche Fiskal) oder USB-TSE.
/// Vorbereitet: sobald eine Umsetzung registriert ist, signiert die deutsche Kasse über sie.
/// </summary>
internal interface ITseClient
{
    /// <summary>Test-TSE ohne Zertifizierung: Belege sind nicht für den Echtbetrieb.</summary>
    bool IsTest { get; }

    /// <summary>Seriennummer der TSE (Hash des öffentlichen Schlüssels), steht auf jedem Beleg.</summary>
    Task<string> RegisterClientAsync(string clientId, CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);

    /// <summary>Startet und beendet eine Transaktion mit Vorgangsart „Kassenbeleg-V1“ und den Vorgangsdaten nach DSFinV-K.</summary>
    Task<TseSignature?> SignTransactionAsync(string clientId, string processType, string processData, CancellationToken ct);

    Task<byte[]?> GetPublicKeyAsync(string clientId, CancellationToken ct);

    /// <summary>TSE-Export (TAR nach BSI TR-03153) der Transaktionen eines Clients im Zeitraum; läuft nur über die Queue (siehe <see cref="TseExports"/>).</summary>
    Task<byte[]?> ExportAsync(string clientId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct);
}

internal sealed record TseSignature(
    long TransactionNumber,
    long SignatureCounter,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Algorithm,
    string Signature,
    string PublicKey);

/// <summary>
/// Deutschland: jeder Beleg wird als TSE-Transaktion signiert; der QR-Code folgt der DSFinV-K.
/// Ohne angebundene TSE lässt sich keine deutsche Kasse anlegen – kassieren ohne TSE ist in Deutschland nicht zulässig.
/// </summary>
internal sealed class KassenSichVFiscalization(IEnumerable<ITseClient> clients) : IFiscalization
{
    public FiscalCountry Country => FiscalCountry.DE;

    public string? Notice => Client?.IsTest == true
        ? "Nur Test-TSE angebunden: Belege sind nicht für den Echtbetrieb. Vor dem Start eine zertifizierte Cloud- oder USB-TSE einrichten."
        : "Kasse und TSE innerhalb eines Monats nach Anschaffung über ELSTER an das Finanzamt melden (§ 146a Abs. 4 AO).";

    /// <summary>Vorgangsart für Kassenbelege nach DSFinV-K.</summary>
    public const string ProcessType = "Kassenbeleg-V1";

    private ITseClient? Client => clients.FirstOrDefault();

    public async Task<string?> InitializeAsync(CashRegister register, CancellationToken ct)
    {
        if (Client is not { } client)
        {
            return "Für Deutschland ist noch keine TSE angebunden. Bitte eine Cloud-TSE oder USB-TSE einrichten.";
        }

        register.CertificateSerial = await client.RegisterClientAsync(register.Code, ct);
        return null;
    }

    public async Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct) =>
        Client is { } client && await client.IsAvailableAsync(ct);

    public async Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct) =>
        Client is { } client ? await client.GetPublicKeyAsync(register.Code, ct) : null;

    public async Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct)
    {
        register.TurnoverCounter += (long)(input.Amounts.Sum(a => a.Gross) * 100m);

        var processData = ProcessData(input);
        var signature = Client is { } client
            ? await client.SignTransactionAsync(register.Code, ProcessType, processData, ct)
            : null;
        if (signature is null)
        {
            // Ausfall der TSE: Beleg wird ohne Signatur mit Hinweis ausgegeben und der Ausfall dokumentiert.
            return new FiscalResult(null, null, true);
        }

        var qr = string.Join(';',
            "V0", register.Code, ProcessType, processData,
            signature.TransactionNumber, signature.SignatureCounter, Time(signature.Start), Time(signature.End),
            signature.Algorithm, "utcTime", signature.Signature, signature.PublicKey);
        register.LastChainValue = signature.Signature;
        return new FiscalResult(qr, signature.Signature, false);
    }

    /// <summary>
    /// Vorgangsdaten nach DSFinV-K: Vorgangstyp, Brutto je Steuersatz (19 %, 7 %, 10,7 %, 5,5 %, 0 %) und Zahlungen als „Betrag:Bar“ bzw.
    /// „Betrag:Unbar“. Ein Storno ist ein eigener Beleg mit negativen Beträgen.
    /// </summary>
    public static string ProcessData(FiscalInput input)
    {
        string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        var amounts = string.Join('_', new[] { TaxCategory.Normal, TaxCategory.Reduced1, TaxCategory.Special, TaxCategory.Reduced2, TaxCategory.Zero }
            .Select(c => Money(input.Amounts.Where(a => a.Category == c).Sum(a => a.Gross))));
        var payments = string.Join('_', input.Payments
            .GroupBy(p => p.Method == PaymentMethod.Cash ? "Bar" : "Unbar")
            .OrderBy(g => g.Key == "Bar" ? 0 : 1)
            .Select(g => $"{Money(g.Sum(p => p.Amount))}:{g.Key}"));
        return $"Beleg^{amounts}^{payments}";
    }

    /// <summary>Zeitformat der TSE (utcTime nach BSI TR-03153): ISO 8601 in UTC mit Millisekunden.</summary>
    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

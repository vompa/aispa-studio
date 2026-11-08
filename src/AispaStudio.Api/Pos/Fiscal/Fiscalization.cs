using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>Was die Signatur eines Belegs braucht: Art, Nummer, Zeitpunkt, Bruttobeträge je Steuersatz und Zahlungen (bar ohne Rückgeld).</summary>
internal sealed record FiscalInput(
    ReceiptKind Kind,
    long Number,
    DateTimeOffset IssuedAt,
    TimeZoneInfo Zone,
    IReadOnlyList<(TaxCategory Category, decimal Gross)> Amounts,
    IReadOnlyList<(PaymentMethod Method, decimal Amount)> Payments);

/// <summary>Ergebnis der Signatur: Inhalt des QR-Codes, Signatur für das Protokoll und ob die Signatureinrichtung ausgefallen war.</summary>
internal sealed record FiscalResult(string? Code, string? Signature, bool DeviceFailed);

/// <summary>
/// Kassenrecht eines Landes. Jede Kasse signiert jeden Beleg über die Umsetzung ihres Landes; der Aufruf läuft innerhalb der Transaktion,
/// die die Kasse sperrt, damit Belegnummern und Verkettung lückenlos bleiben.
/// </summary>
internal interface IFiscalization
{
    FiscalCountry Country { get; }

    /// <summary>Hinweis für die Leitung, z. B. dass nur eine Test-Signatureinrichtung angebunden ist.</summary>
    string? Notice { get; }

    /// <summary>Richtet eine neue Kasse ein (Schlüssel, Zertifikat, TSE-Client). Liefert eine Fehlermeldung, wenn das nicht geht.</summary>
    Task<string?> InitializeAsync(CashRegister register, CancellationToken ct);

    /// <summary>Ist die Signatureinrichtung erreichbar? Nach einem Ausfall entscheidet das, ob zuerst ein Nullbeleg nötig ist.</summary>
    Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct);

    /// <summary>Signiert einen Beleg und schreibt Umsatzzähler und Verkettung in der Kasse fort.</summary>
    Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct);

    /// <summary>Zertifikat bzw. öffentlicher Schlüssel für Prüfungen, z. B. im Datenerfassungsprotokoll.</summary>
    Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct);
}

internal sealed class FiscalizationResolver(IEnumerable<IFiscalization> fiscalizations)
{
    public IFiscalization For(FiscalCountry country) =>
        fiscalizations.FirstOrDefault(f => f.Country == country)
        ?? throw new InvalidOperationException($"Keine Umsetzung des Kassenrechts für {country}.");
}

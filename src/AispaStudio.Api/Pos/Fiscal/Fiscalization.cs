using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>
/// Was die Signatur eines Belegs braucht: Art, Nummer, Zeitpunkt, Bruttobeträge je Steuersatz und Zahlungen (bar ohne Rückgeld).
/// <c>Tip</c> ist Trinkgeld für das Team (ADR 0095): in den Zahlungen enthalten, in den Bruttobeträgen nicht.
/// </summary>
internal sealed record FiscalInput(
    ReceiptKind Kind,
    long Number,
    DateTimeOffset IssuedAt,
    TimeZoneInfo Zone,
    IReadOnlyList<(TaxCategory Category, decimal Gross)> Amounts,
    IReadOnlyList<(PaymentMethod Method, decimal Amount)> Payments,
    decimal Tip = 0);

/// <summary>Ergebnis der Signatur: Inhalt des QR-Codes, Signatur für das Protokoll und ob die Signatureinrichtung ausgefallen war.</summary>
internal sealed record FiscalResult(string? Code, string? Signature, bool DeviceFailed);

/// <summary>
/// Kassenrecht eines Landes. Jede Kasse signiert jeden Beleg über die Umsetzung ihres Landes; der Aufruf läuft innerhalb der Transaktion,
/// die die Kasse sperrt, damit Belegnummern und Verkettung lückenlos bleiben.
/// </summary>
internal interface IFiscalization
{
    FiscalCountry Country { get; }
    bool IsTestProvider(CashRegister register);

    /// <summary>Hinweis für die Leitung, z. B. dass nur eine Test-Signatureinrichtung angebunden ist.</summary>
    string? Notice(CashRegister register);

    /// <summary>Richtet eine neue Kasse ein (Schlüssel, Zertifikat, TSE-Client). Liefert eine Fehlermeldung, wenn das nicht geht.</summary>
    Task<string?> InitializeAsync(CashRegister register, CancellationToken ct);

    /// <summary>Ist die Signatureinrichtung erreichbar? Nach einem Ausfall entscheidet das, ob zuerst ein Nullbeleg nötig ist.</summary>
    Task<bool> IsDeviceAvailableAsync(CashRegister register, CancellationToken ct);

    /// <summary>Signiert einen Beleg und schreibt Umsatzzähler und Verkettung in der Kasse fort.</summary>
    Task<FiscalResult> SignAsync(CashRegister register, FiscalInput input, CancellationToken ct);

    /// <summary>Zertifikat bzw. öffentlicher Schlüssel für Prüfungen, z. B. im Datenerfassungsprotokoll.</summary>
    Task<byte[]?> GetCertificateAsync(CashRegister register, CancellationToken ct);
}

/// <summary>Antworten externer Fiskaladapter können Zugangsdaten enthalten. Nur der fachliche Ausfall verlässt den Adapteraufruf.</summary>
internal static class FiscalAdapterCalls
{
    public static async Task<T> TryAsync<T>(Func<Task<T>> call, T unavailable, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var result = await call();
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            if (ex is OperationCanceledException or HttpRequestException or TimeoutException or InvalidOperationException
                or IOException or global::System.Security.Cryptography.CryptographicException or PosFailure)
            {
                return unavailable;
            }

            throw PosFailure.Conflict("Die Fiskaleinrichtung hat den Auftrag nicht abgeschlossen.");
        }
    }
}

internal sealed class FiscalizationResolver(IEnumerable<IFiscalization> fiscalizations)
{
    public IFiscalization For(FiscalCountry country) =>
        fiscalizations.FirstOrDefault(f => f.Country == country)
        ?? throw new InvalidOperationException($"Keine Umsetzung des Kassenrechts für {country}.");
}

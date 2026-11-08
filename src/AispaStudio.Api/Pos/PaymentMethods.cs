using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>Schalter für Zahlungsarten, die noch nicht überall verfügbar sind (Abschnitt „Payments“ der Konfiguration).</summary>
public sealed class PaymentOptions
{
    /// <summary>
    /// Digitaler Euro an Kassen in Euro-Ländern. Aus, bis die EZB ihn ausgibt und der Zahlungsdienstleister ihn abwickelt;
    /// danach Pflicht für alle Betriebe, die schon unbar kassieren.
    /// </summary>
    public bool DigitalEuro { get; set; }
}

/// <summary>
/// Eigenschaften der Zahlungsarten an einer Stelle, damit eine neue Art (z. B. der digitale Euro) nur hier und in den Konten dazukommt.
/// Unbare Zahlungen kommen vom Terminal oder Zahlungsdienstleister mit Referenz; Rückgeld gibt es nur bar.
/// </summary>
internal static class PaymentMethods
{
    public static string Label(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Bar",
        PaymentMethod.Card => "Karte",
        PaymentMethod.Voucher => "Gutschein",
        PaymentMethod.InstantTransfer => "Sofortüberweisung",
        PaymentMethod.DigitalEuro => "Digitaler Euro",
        PaymentMethod.Treatwell => "Über Treatwell bezahlt",
        _ => method.ToString(),
    };

    /// <summary>Der Zahlungsweg eines Belegs in Worten: die verschiedenen Zahlungsarten, mit „ + “ verbunden („Bar“, „Bar + Karte“).</summary>
    public static string Way(IEnumerable<PaymentMethod> methods) => string.Join(" + ", methods.Select(Label).Distinct());

    /// <summary>Unbar über Terminal, Bank, Zahlungsdienstleister oder die Buchungsplattform (nicht Bargeld, nicht Gutschein).</summary>
    public static bool IsElectronic(PaymentMethod method) =>
        method is PaymentMethod.Card or PaymentMethod.InstantTransfer or PaymentMethod.DigitalEuro or PaymentMethod.Treatwell;

    /// <summary>An einer Kasse wählbare Zahlungsarten. Der digitale Euro nur in Euro-Ländern und nur, wenn eingeschaltet.</summary>
    public static IReadOnlyList<PaymentMethod> Available(FiscalCountry country, PaymentOptions options) =>
        options.DigitalEuro && country != FiscalCountry.CH
            ? [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Voucher, PaymentMethod.InstantTransfer, PaymentMethod.DigitalEuro, PaymentMethod.Treatwell]
            : [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Voucher, PaymentMethod.InstantTransfer, PaymentMethod.Treatwell];
}

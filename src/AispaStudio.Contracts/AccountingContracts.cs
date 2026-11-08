namespace AispaStudio.Contracts;

/// <summary>
/// Format für die Übergabe an die Buchhaltung: DATEV (Deutschland, Buchungsstapel) oder BMD (Österreich, NTCS-Buchungsimport) für Kassen in Euro;
/// <c>Swiss</c> für Schweizer Kassen in CHF als CSV zum Einlesen in Abacus oder bexio.
/// </summary>
public enum AccountingFormat
{
    Datev,
    Bmd,
    Swiss,
    Rzl,
}

/// <summary>
/// Einstellungen für den Buchhaltungsexport. Konten als Nummern im Kontenrahmen der Steuerberatung.
/// Jeder Beleg wird über das Verrechnungskonto gebucht: Erlöse an Erlöskonto (mit Steuer), Zahlungen vom Geldkonto der Zahlungsart.
/// Einlagen und Entnahmen der Kassenlade laufen über <c>CashTransferAccount</c> (Geldtransit), Kassendifferenzen des Tagesabschlusses
/// über <c>CashShortageAccount</c> (Fehlbetrag) bzw. <c>CashSurplusAccount</c> (Überschuss).
/// <c>DatevTaxKeys</c> ordnet Steuersätzen den DATEV-BU-Schlüssel zu, z. B. „19=3;7=2“ (SKR03/04 ohne Automatikkonten).
/// <c>SwissTaxCodes</c> ordnet für Schweizer Kassen den MWST-Sätzen den MWST-Code der Buchhaltung zu, z. B. „8.1=UN81;2.6=UR26“.
/// <c>RevenueAccountCodes</c> schlüsselt Erlöskonten auf den Kontenrahmen um, z. B. „4000=3400“ für den Schweizer KMU-Kontenrahmen.
/// <c>Country</c> leer ist der Standard für alle Kassen; mit Land gilt die Zuordnung nur für dessen Kassen.
/// <c>Inherited</c> (nur Antwort): das Land hat keine eigene Zuordnung, gezeigt wird der Standard, der dort gilt.
/// </summary>
public sealed record AccountingSettingsDto(
    string? DatevConsultantNumber,
    string? DatevClientNumber,
    int AccountLength,
    int FiscalYearStartMonth,
    string TransitAccount,
    string CashAccount,
    string CardAccount,
    string InstantTransferAccount,
    string VoucherAccount,
    string CashTransferAccount,
    string DigitalEuroAccount,
    string CashShortageAccount,
    string CashSurplusAccount,
    string DatevTaxKeys,
    string SwissTaxCodes = "",
    string RevenueAccountCodes = "",
    FiscalCountry? Country = null,
    bool Inherited = false,
    string TreatwellAccount = "1400");

/// <summary>
/// Eine Buchung des Exports, so wie sie in DATEV, BMD bzw. Abacus/bexio landet; für Vorschau und Prüfung. Währung und Land der Kasse.
/// <c>TaxKey</c> ist der DATEV-BU-Schlüssel aus der Kontenzuordnung des Landes der Kasse: leer heißt Automatikkonto,
/// <c>null</c> bei einem Steuersatz heißt „fehlt“ (dann verweigert der DATEV-Export die Datei).
/// </summary>
public sealed record AccountingPosting(
    DateOnly Date,
    string Document,
    string DebitAccount,
    string CreditAccount,
    decimal Amount,
    decimal TaxPercent,
    string Text,
    string? CostCenter,
    string Currency = "EUR",
    string? TaxKey = null,
    FiscalCountry? Country = null);

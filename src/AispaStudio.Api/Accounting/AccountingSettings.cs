using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Accounting;

/// <summary>
/// Einstellungen für den Buchhaltungsexport: eine Standardzeile je Unternehmen (<see cref="Country"/> leer) und auf Wunsch
/// eine eigene Zeile je Land, die für die Kassen dieses Landes an die Stelle des Standards tritt (z. B. Schweizer Kontenrahmen
/// neben SKR03 oder dem österreichischen Einheitskontenrahmen). Fehlt eine Zeile, gelten die Vorgaben (SKR03);
/// angelegt wird sie beim ersten Speichern. So braucht es keine vorbelegte Zeile, die erst einem Mandanten zugeordnet werden müsste.
/// </summary>
public sealed class AccountingSettings
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Leer für den Standard, sonst das Land der Kassen, für die diese Zuordnung gilt.</summary>
    public FiscalCountry? Country { get; init; }

    public string? DatevConsultantNumber { get; set; }
    public string? DatevClientNumber { get; set; }
    public int AccountLength { get; set; } = 4;
    public int FiscalYearStartMonth { get; set; } = 1;
    public string TransitAccount { get; set; } = "1360";
    public string CashAccount { get; set; } = "1000";
    public string CardAccount { get; set; } = "1361";
    public string InstantTransferAccount { get; set; } = "1200";
    public string VoucherAccount { get; set; } = "3600";

    /// <summary>Geldtransit für Einlagen und Entnahmen der Kassenlade, z. B. Abschöpfung zur Bank.</summary>
    public string CashTransferAccount { get; set; } = "1460";

    /// <summary>Geldtransit für Zahlungen mit dem digitalen Euro, bis der Zahlungsdienstleister sie dem Bankkonto gutschreibt.</summary>
    public string DigitalEuroAccount { get; set; } = "1362";

    /// <summary>Forderung an Treatwell für Termine, die der Gast vorab über die Plattform bezahlt hat (Zahlungsart „Über Treatwell bezahlt“, ADR 0062).</summary>
    public string TreatwellAccount { get; set; } = "1400";

    /// <summary>Kassenfehlbetrag laut Tagesabschluss (Aufwand).</summary>
    public string CashShortageAccount { get; set; } = "4900";

    /// <summary>Kassenüberschuss laut Tagesabschluss (Ertrag).</summary>
    public string CashSurplusAccount { get; set; } = "2700";

    public string DatevTaxKeys { get; set; } = "19=3;7=2";

    /// <summary>MWST-Codes für Schweizer Kassen je Satz, z. B. „8.1=UN81“; ohne Angabe bleibt der Code leer und der Satz steht in der Datei.</summary>
    public string SwissTaxCodes { get; set; } = "";

    /// <summary>
    /// Erlöskonten im Kontenrahmen dieses Landes, z. B. „4000=3400;4800=3200“: Erlöskonto des Unternehmens=Konto in der Buchhaltung.
    /// Nicht genannte Erlöskonten gehen mit ihrer eigenen Nummer hinaus; Kostenstelle und Trennung der Bereiche bleiben gleich.
    /// </summary>
    public string RevenueAccountCodes { get; set; } = "";
}

internal static class AccountingModel
{
    /// <summary>Zuordnung für Kassen des Landes: die eigene Zeile des Landes, sonst der Standard, sonst die Vorgaben.</summary>
    public static async Task<AccountingSettings> SettingsAsync(this IQueryable<AccountingSettings> settings, CancellationToken ct,
        FiscalCountry? country = null) =>
        For(await settings.Where(s => s.Country == null || s.Country == country).ToListAsync(ct), country);

    public static AccountingSettings For(IReadOnlyCollection<AccountingSettings> rows, FiscalCountry? country) =>
        (country is null ? null : rows.FirstOrDefault(s => s.Country == country))
        ?? rows.Where(s => s.Country == null).MinBy(s => s.Id)
        ?? new AccountingSettings();

    public static void Configure(ModelBuilder builder) =>
        builder.Entity<AccountingSettings>(e =>
        {
            e.Property(s => s.DatevConsultantNumber).HasMaxLength(7);
            e.Property(s => s.DatevClientNumber).HasMaxLength(5);
            e.Property(s => s.TransitAccount).HasMaxLength(20);
            e.Property(s => s.CashAccount).HasMaxLength(20);
            e.Property(s => s.CardAccount).HasMaxLength(20);
            e.Property(s => s.InstantTransferAccount).HasMaxLength(20);
            e.Property(s => s.VoucherAccount).HasMaxLength(20);
            e.Property(s => s.CashTransferAccount).HasMaxLength(20);
            e.Property(s => s.DigitalEuroAccount).HasMaxLength(20);
            e.Property(s => s.TreatwellAccount).HasMaxLength(20);
            e.Property(s => s.CashShortageAccount).HasMaxLength(20);
            e.Property(s => s.CashSurplusAccount).HasMaxLength(20);
            e.Property(s => s.DatevTaxKeys).HasMaxLength(200);
            e.Property(s => s.SwissTaxCodes).HasMaxLength(200);
            e.Property(s => s.RevenueAccountCodes).HasMaxLength(1000);
            e.Property(s => s.Country).HasConversion<string>().HasMaxLength(2);
        });
}

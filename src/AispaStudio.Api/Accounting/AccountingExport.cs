using System.Globalization;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Accounting;

/// <summary>
/// Macht aus Kassenbelegen Buchungen für die Buchhaltung. Je Beleg: Erlöse je Erlöskonto und Steuersatz an das Verrechnungskonto,
/// Zahlungen vom Geldkonto der Zahlungsart an das Verrechnungskonto; das Verrechnungskonto ist danach je Beleg ausgeglichen.
/// Stornos erscheinen als Gegenbuchung, getrennte Erlöskonten (mit Kostenstelle) bleiben getrennt.
/// Einlagen und Entnahmen der Kassenlade werden gegen den Geldtransit gebucht, Kassendifferenzen aus dem Tagesabschluss
/// (gezählt minus Soll) als Fehlbetrag bzw. Überschuss. DATEV und BMD bekommen nur die Euro-Kassen,
/// der Schweizer Export nur die Kassen in CHF; die Vorschau zeigt alle mit ihrer Währung.
/// Jede Kasse wird mit der Kontenzuordnung ihres Landes gebucht (eigene Zeile des Landes, sonst der Standard),
/// auch der DATEV-BU-Schlüssel: österreichische 20 % brauchen einen anderen Schlüssel als deutsche 19 %.
/// </summary>
internal sealed class AccountingExport(AispaDbContext db)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <param name="canSee">Nur Kassen an Standorten, die die Person sehen darf.</param>
    /// <param name="swiss">true nur Schweizer Kassen, false nur Euro-Kassen, null alle.</param>
    public async Task<List<AccountingPosting>> PostingsAsync(DateOnly from, DateOnly until, Guid? registerId, Func<Guid, bool> canSee,
        CancellationToken ct, bool? swiss = null)
    {
        var allSettings = await db.AccountingSettings.AsNoTracking().ToListAsync(ct);
        var registers = await db.CashRegisters.AsNoTracking()
            .Where(r => registerId == null || r.Id == registerId)
            .Where(r => swiss == null || (r.Country == FiscalCountry.CH) == swiss).ToListAsync(ct);
        registers.RemoveAll(r => !canSee(r.LocationId));
        var zones = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => LocalTime.Zone(l.TimeZone), ct);
        var accounts = await db.RevenueAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var postings = new List<AccountingPosting>();

        foreach (var register in registers.OrderBy(r => r.Code, StringComparer.Ordinal))
        {
            var zone = zones[register.LocationId];
            var settings = AccountingModel.For(allSettings, register.Country);
            var revenueCodes = ParseAccountMap(settings.RevenueAccountCodes);
            var taxKeys = ParseTaxKeys(settings.DatevTaxKeys);
            var currency = register.Country == FiscalCountry.CH ? "CHF" : "EUR";
            var (start, _) = LocalTime.Day(from, zone);
            var (_, end) = LocalTime.Day(until, zone);
            var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).AsSplitQuery()
                .Where(r => r.RegisterId == register.Id && r.IssuedAt >= start && r.IssuedAt < end)
                .Where(r => r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void)
                .OrderBy(r => r.Number)
                .ToListAsync(ct);
            var movements = await db.CashMovements.AsNoTracking()
                .Where(m => m.RegisterId == register.Id && m.At >= start && m.At < end)
                .OrderBy(m => m.At)
                .ToListAsync(ct);
            var closings = await db.DayClosings.AsNoTracking()
                .Where(c => c.RegisterId == register.Id && c.Until >= start && c.Until < end)
                .OrderBy(c => c.Until)
                .ToListAsync(ct);
            var registerPostings = new List<(DateTimeOffset At, AccountingPosting Posting)>();

            foreach (var receipt in receipts)
            {
                var date = LocalTime.LocalDate(receipt.IssuedAt, zone);
                var document = $"{register.Code}-{receipt.Number}";
                var text = receipt.Kind == ReceiptKind.Void ? $"Storno Kassa {document}" : $"Kassa {document}";

                foreach (var group in receipt.Lines.GroupBy(l => (l.RevenueAccountId, l.TaxPercent)).OrderBy(g => accounts[g.Key.RevenueAccountId].Code, StringComparer.Ordinal))
                {
                    var amount = group.Sum(l => l.Amount);
                    if (amount != 0)
                    {
                        var account = accounts[group.Key.RevenueAccountId];
                        registerPostings.Add((receipt.IssuedAt, new(date, document, settings.TransitAccount, revenueCodes.GetValueOrDefault(account.Code, account.Code), amount, group.Key.TaxPercent, text, account.CostCenter, currency,
                            group.Key.TaxPercent == 0 ? null : taxKeys.GetValueOrDefault(group.Key.TaxPercent))));
                    }
                }

                foreach (var payment in receipt.Payments.GroupBy(p => p.Method).OrderBy(g => g.Key))
                {
                    var amount = payment.Sum(p => p.Amount);
                    if (amount != 0)
                    {
                        registerPostings.Add((receipt.IssuedAt, new(date, document, MoneyAccount(settings, payment.Key), settings.TransitAccount, amount, 0, text, null, currency)));
                    }
                }
            }

            foreach (var movement in movements)
            {
                var date = LocalTime.LocalDate(movement.At, zone);
                var document = $"{register.Code}-{(movement.Kind == CashMovementKind.Deposit ? "E" : "A")}{date:MMdd}";
                var (debit, credit, label) = movement.Kind == CashMovementKind.Deposit
                    ? (settings.CashAccount, settings.CashTransferAccount, "Einlage")
                    : (settings.CashTransferAccount, settings.CashAccount, "Entnahme");
                registerPostings.Add((movement.At, new(date, document, debit, credit, movement.Amount, 0, $"{label} Kassa {register.Code}: {movement.Reason}", null, currency)));
            }

            foreach (var closing in closings)
            {
                var difference = JsonSerializer.Deserialize<DayClosingDto>(closing.Report, PosJson.Options)!.Difference;
                if (difference == 0)
                {
                    continue;
                }

                var date = LocalTime.LocalDate(closing.Until, zone);
                var document = $"{register.Code}-Z{date:MMdd}";
                var (debit, credit, label) = difference < 0
                    ? (settings.CashShortageAccount, settings.CashAccount, "Kassenfehlbetrag")
                    : (settings.CashAccount, settings.CashSurplusAccount, "Kassenüberschuss");
                registerPostings.Add((closing.Until, new(date, document, debit, credit, Math.Abs(difference), 0, $"{label} Tagesabschluss {register.Code}", null, currency)));
            }

            postings.AddRange(registerPostings.OrderBy(p => p.At).Select(p => p.Posting with { Country = register.Country }));
        }

        return postings;
    }

    /// <returns>Die Datei, oder <c>Problem</c>, wenn sie so nicht in die Buchhaltung gehen darf.</returns>
    public async Task<(byte[]? Content, string FileName, string? Problem)> FileAsync(AccountingFormat format, DateOnly from, DateOnly until, Guid? registerId,
        Func<Guid, bool> canSee, CancellationToken ct)
    {
        var postings = await PostingsAsync(from, until, registerId, canSee, ct, swiss: format == AccountingFormat.Swiss);
        if (format == AccountingFormat.Datev && MissingDatevKeys(postings) is { } missing)
        {
            return (null, "", missing);
        }

        // Kopfdaten (DATEV-Berater, Sachkontenlänge, MWST-Codes) aus der Zuordnung des Landes, für das die Datei gedacht ist.
        var settings = await db.AccountingSettings.AsNoTracking().SettingsAsync(ct, format switch
        {
            AccountingFormat.Datev => FiscalCountry.DE,
            AccountingFormat.Swiss => FiscalCountry.CH,
            _ => FiscalCountry.AT,
        });
        return format switch
        {
            AccountingFormat.Datev => (Ansi.GetBytes(Datev(postings, settings, from, until)), $"EXTF_Buchungsstapel_{from:yyyyMMdd}_{until:yyyyMMdd}.csv", null),
            AccountingFormat.Bmd => (Ansi.GetBytes(Bmd(postings)), $"BMD_Buchungen_{from:yyyyMMdd}_{until:yyyyMMdd}.csv", null),
            AccountingFormat.Rzl => (Ansi.GetBytes(Rzl(postings)), $"RZL_Buchungen_{from:yyyyMMdd}_{until:yyyyMMdd}.csv", null),
            _ => ([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(Swiss(postings, settings))], $"Buchungen_CH_{from:yyyyMMdd}_{until:yyyyMMdd}.csv", null),
        };
    }

    /// <summary>
    /// Ohne BU-Schlüssel bucht DATEV einen steuerpflichtigen Erlös ohne Umsatzsteuer, außer das Erlöskonto ist ein Automatikkonto.
    /// Deshalb geht ein Steuersatz, für den das Land der Kasse keinen Schlüssel kennt, nicht still leer hinaus; „20=“ sagt ausdrücklich „Automatikkonto“.
    /// </summary>
    internal static string? MissingDatevKeys(IEnumerable<AccountingPosting> postings)
    {
        var missing = postings.Where(p => p.TaxPercent != 0 && p.TaxKey is null && p.Country is { } c && c != FiscalCountry.CH)
            .Select(p => (Country: p.Country!.Value, p.TaxPercent)).Distinct()
            .GroupBy(m => m.Country).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
            .Select(g => $"{string.Join(", ", g.Select(m => m.TaxPercent).Order().Select(r => $"{r.ToString("0.##", CultureInfo.GetCultureInfo("de-AT"))} %"))} "
                + $"(Kassen in {CountryName(g.Key)})")
            .ToList();
        return missing.Count == 0
            ? null
            : $"Für {string.Join(" und ", missing)} ist kein DATEV-BU-Schlüssel hinterlegt. Unter „Kontenzuordnung“ für dieses Land die Schlüssel "
              + "der Steuerberatung eintragen, z. B. „20=…“, oder „20=“ ohne Schlüssel, wenn das Erlöskonto ein Automatikkonto ist. "
              + "Für Österreich passen meist BMD oder RZL, die den Steuersatz selbst mitgeben.";
    }

    private static string CountryName(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => "Österreich",
        FiscalCountry.DE => "Deutschland",
        _ => country.ToString(),
    };

    /// <summary>DATEV, BMD und RZL lesen Windows-1252 (ANSI).</summary>
    private static Encoding Ansi
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }

    private static string MoneyAccount(AccountingSettings s, PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => s.CashAccount,
        PaymentMethod.Card => s.CardAccount,
        PaymentMethod.InstantTransfer => s.InstantTransferAccount,
        PaymentMethod.DigitalEuro => s.DigitalEuroAccount,
        PaymentMethod.Treatwell => s.TreatwellAccount,
        _ => s.VoucherAccount,
    };

    /// <summary>DATEV-Format, Buchungsstapel (Version 700, Kategorie 21): Kopfzeile, Spaltenüberschriften, eine Zeile je Buchung.</summary>
    internal static string Datev(IReadOnlyList<AccountingPosting> postings, AccountingSettings s, DateOnly from, DateOnly until)
    {
        var fiscalYear = new DateOnly(from.Month >= s.FiscalYearStartMonth ? from.Year : from.Year - 1, s.FiscalYearStartMonth, 1);
        var sb = new StringBuilder();
        sb.Append(string.Join(';',
            "\"EXTF\"", "700", "21", "\"Buchungsstapel\"", "13", DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", Invariant), "", "\"RE\"",
            "\"AISPA Studio\"", "\"\"", s.DatevConsultantNumber ?? "", s.DatevClientNumber ?? "", fiscalYear.ToString("yyyyMMdd", Invariant),
            s.AccountLength.ToString(Invariant), from.ToString("yyyyMMdd", Invariant), until.ToString("yyyyMMdd", Invariant),
            Quote($"Kassa {from:dd.MM.yyyy}–{until:dd.MM.yyyy}"), "\"\"", "1", "0", "0", "\"EUR\"", "", "\"\"", "", "", "", "\"\"", "", "", "\"\""));
        sb.Append("\r\n");
        sb.Append(string.Join(';', DatevColumns));
        sb.Append("\r\n");

        foreach (var p in postings)
        {
            var fields = new string[DatevColumns.Length];
            Array.Fill(fields, "");
            fields[0] = Math.Abs(p.Amount).ToString("0.00", Invariant).Replace('.', ',');
            fields[1] = p.Amount >= 0 ? "\"S\"" : "\"H\"";
            fields[2] = "\"EUR\"";
            fields[6] = p.DebitAccount;
            fields[7] = p.CreditAccount;
            fields[8] = Quote(p.TaxPercent == 0 ? "" : p.TaxKey ?? "");
            fields[9] = p.Date.ToString("ddMM", Invariant);
            fields[10] = Quote(Cut(p.Document, 36));
            fields[13] = Quote(Cut(p.Text, 60));
            fields[36] = Quote(Cut(p.CostCenter ?? "", 36));
            sb.Append(string.Join(';', fields));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>BMD NTCS Buchungsimport (CSV mit Semikolon): Kassabuchungen mit Steuersatz, Steuercode und Kostenstelle.</summary>
    internal static string Bmd(IReadOnlyList<AccountingPosting> postings)
    {
        var sb = new StringBuilder("satzart;konto;gkonto;belegnr;belegdat;buchsymbol;buchcode;prozent;steuercode;betrag;steuer;text;kost\r\n");
        foreach (var p in postings)
        {
            var tax = p.Amount - TaxRates.Net(p.Amount, p.TaxPercent);
            sb.Append(string.Join(';',
                "0",
                p.DebitAccount,
                p.CreditAccount,
                p.Document,
                p.Date.ToString("dd.MM.yyyy", Invariant),
                "KA",
                "1",
                Number(p.TaxPercent),
                p.TaxPercent == 0 ? "" : "1",
                Number(p.Amount),
                p.TaxPercent == 0 ? "" : Number(tax),
                p.Text.Replace(';', ',').ReplaceLineEndings(" "),
                p.CostCenter ?? ""));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// RZL (Österreich): CSV mit Semikolon für den Buchungsimport von RZL FIBU bzw. RZL Board, eine Zeile je Buchung mit Soll/Haben-Kennzeichen.
    /// Beträge sind immer positiv; Stornos tragen „H“. Die Spalten ordnet man beim ersten Import einmal einer Importvorlage zu.
    /// </summary>
    internal static string Rzl(IReadOnlyList<AccountingPosting> postings)
    {
        var sb = new StringBuilder("Belegdatum;Belegnummer;Konto;Gegenkonto;Betrag;S/H;Steuerprozent;Steuerbetrag;Buchungstext;Kostenstelle\r\n");
        foreach (var p in postings)
        {
            var amount = Math.Abs(p.Amount);
            sb.Append(string.Join(';',
                p.Date.ToString("dd.MM.yyyy", Invariant),
                Field(p.Document),
                p.DebitAccount,
                p.CreditAccount,
                Number(amount),
                p.Amount >= 0 ? "S" : "H",
                p.TaxPercent == 0 ? "" : Number(p.TaxPercent),
                p.TaxPercent == 0 ? "" : Number(amount - TaxRates.Net(amount, p.TaxPercent)),
                Field(p.Text),
                Field(p.CostCenter ?? "")));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Schweiz (CHF): neutrale CSV für den Buchungsimport von Abacus und bexio, eine Zeile je Buchung mit Soll, Haben, Betrag und MWST.
    /// Beträge sind immer positiv; Stornos tauschen Soll und Haben. Spalten werden beim ersten Import einmal zugeordnet.
    /// </summary>
    internal static string Swiss(IReadOnlyList<AccountingPosting> postings, AccountingSettings s)
    {
        var codes = ParseTaxKeys(s.SwissTaxCodes);
        var sb = new StringBuilder("Datum;Beleg;Soll;Haben;Betrag;Währung;MWST-Code;MWST-Satz;MWST-Betrag;Text;Kostenstelle\r\n");
        foreach (var p in postings)
        {
            var (debit, credit) = p.Amount >= 0 ? (p.DebitAccount, p.CreditAccount) : (p.CreditAccount, p.DebitAccount);
            var amount = Math.Abs(p.Amount);
            sb.Append(string.Join(';',
                p.Date.ToString("dd.MM.yyyy", Invariant),
                Field(p.Document),
                debit,
                credit,
                amount.ToString("0.00", Invariant),
                p.Currency,
                p.TaxPercent == 0 ? "" : Field(codes.GetValueOrDefault(p.TaxPercent, "")),
                p.TaxPercent == 0 ? "" : p.TaxPercent.ToString("0.0#", Invariant),
                p.TaxPercent == 0 ? "" : (amount - TaxRates.Net(amount, p.TaxPercent)).ToString("0.00", Invariant),
                Field(p.Text),
                Field(p.CostCenter ?? "")));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Field(string value) =>
        value.AsSpan().IndexOfAny(";\"\r\n") >= 0 ? Quote(value.ReplaceLineEndings(" ")) : value;

    /// <summary>„4000=3400;4800=3200“ als Zuordnung Erlöskonto des Unternehmens → Konto in der Buchhaltung.</summary>
    internal static Dictionary<string, string> ParseAccountMap(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', StringSplitOptions.TrimEntries))
            .Where(pair => pair is [{ Length: > 0 }, { Length: > 0 }])
            .GroupBy(pair => pair[0], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last()[1], StringComparer.Ordinal);

    internal static Dictionary<decimal, string> ParseTaxKeys(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('='))
            .Where(parts => parts.Length == 2 && decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Number, Invariant, out _))
            .GroupBy(parts => decimal.Parse(parts[0].Replace(',', '.'), Invariant))
            .ToDictionary(g => g.Key, g => g.Last()[1].Trim());

    private static string Cut(string value, int length) => value.Length <= length ? value : value[..length];

    private static string Number(decimal value) => value.ToString("0.00", Invariant).Replace('.', ',');

    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>Spalten 1–38 des Buchungsstapels (bis KOST2); weitere Spalten sind optional und bleiben weg.</summary>
    private static readonly string[] DatevColumns =
    [
        "Umsatz (ohne Soll/Haben-Kz)", "Soll/Haben-Kennzeichen", "WKZ Umsatz", "Kurs", "Basis-Umsatz", "WKZ Basis-Umsatz",
        "Konto", "Gegenkonto (ohne BU-Schlüssel)", "BU-Schlüssel", "Belegdatum", "Belegfeld 1", "Belegfeld 2", "Skonto", "Buchungstext",
        "Postensperre", "Diverse Adressnummer", "Geschäftspartnerbank", "Sachverhalt", "Zinssperre", "Beleglink",
        "Beleginfo - Art 1", "Beleginfo - Inhalt 1", "Beleginfo - Art 2", "Beleginfo - Inhalt 2", "Beleginfo - Art 3", "Beleginfo - Inhalt 3",
        "Beleginfo - Art 4", "Beleginfo - Inhalt 4", "Beleginfo - Art 5", "Beleginfo - Inhalt 5", "Beleginfo - Art 6", "Beleginfo - Inhalt 6",
        "Beleginfo - Art 7", "Beleginfo - Inhalt 7", "Beleginfo - Art 8", "Beleginfo - Inhalt 8", "KOST1 - Kostenstelle", "KOST2 - Kostenstelle",
    ];
}

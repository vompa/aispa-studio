using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos.Fiscal;

/// <summary>
/// Export nach DSFinV-K (Digitale Schnittstelle der Finanzverwaltung für Kassensysteme) für die Kassen-Nachschau und Außenprüfung in
/// Deutschland: je Tagesabschluss (Z-Nummer) Stammdaten, Einzelaufzeichnungen und Kassenabschluss als CSV mit <c>index.xml</c>, gepackt als ZIP.
/// Enthalten sind die Kerntabellen; Bediener, Agenturumsätze und Einlagen/Entnahmen als eigene Geschäftsvorfälle fehlen noch.
/// Gutscheineinlösungen stehen vereinfacht als Zahlart „Unbar/Gutschein“. Gegen eine Prüfsoftware ist der Export noch nicht validiert.
/// </summary>
internal sealed class DsfinvkExport(AispaDbContext db)
{
    public const string TaxonomyVersion = "2.3";

    public async Task<(byte[] Content, string FileName)?> ZipAsync(Guid registerId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
        if (register is null)
        {
            return null;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == register.LocationId, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);

        // Z-Nummern zählen alle Abschlüsse der Kasse; exportiert werden die, deren Buchungstag im Zeitraum liegt.
        var closings = (await db.DayClosings.AsNoTracking().Where(c => c.RegisterId == registerId).OrderBy(c => c.Until).ToListAsync(ct))
            .Select((c, i) => (Closing: c, Number: i + 1, Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.Until, zone).DateTime)))
            .Where(z => z.Day >= from && z.Day <= until)
            .ToList();
        var tables = Tables();
        foreach (var (closing, number, day) in closings)
        {
            var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments)
                .Where(r => r.RegisterId == registerId && r.IssuedAt >= closing.From && r.IssuedAt < closing.Until)
                .Where(r => r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void)
                .OrderBy(r => r.Number)
                .AsSplitQuery()
                .ToListAsync(ct);
            Add(tables, register, location, zone, closing, number, day, receipts);
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var table in tables)
            {
                await using var entry = zip.CreateEntry(table.File).Open();
                await entry.WriteAsync(Encoding.UTF8.GetBytes(table.Csv()), ct);
            }

            await using var index = zip.CreateEntry("index.xml").Open();
            await index.WriteAsync(Encoding.UTF8.GetBytes(Index(tables, register)), ct);
        }

        return (buffer.ToArray(), $"DSFinV-K_{register.Code}_{from:yyyyMMdd}-{until:yyyyMMdd}.zip");
    }

    private static void Add(List<Table> t, CashRegister register, Location location, TimeZoneInfo zone,
        DayClosing closing, int z, DateOnly day, List<Receipt> receipts)
    {
        var created = Local(closing.Until, zone);
        object[] Key() => [register.Code, created, z];
        var header = register.ReceiptHeader.Split('\n', StringSplitOptions.TrimEntries);
        var lines = receipts.SelectMany(r => r.Lines.Select(l => (Receipt: r, Line: l))).ToList();
        var payments = receipts.SelectMany(r => r.Payments.Select(p => (Receipt: r, Payment: p))).ToList();

        Row(t, "cashpointclosing.csv", [.. Key(), day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), TaxonomyVersion,
            receipts.FirstOrDefault()?.Number.ToString(CultureInfo.InvariantCulture) ?? "", receipts.LastOrDefault()?.Number.ToString(CultureInfo.InvariantCulture) ?? "",
            header.ElementAtOrDefault(0) ?? "", header.ElementAtOrDefault(1) ?? "", "", "", "DE", "", "",
            payments.Sum(p => p.Payment.Amount), payments.Where(p => p.Payment.Method == PaymentMethod.Cash).Sum(p => p.Payment.Amount)]);
        Row(t, "location.csv", [.. Key(), location.Name, header.ElementAtOrDefault(1) ?? "", "", "", "DE", ""]);
        Row(t, "cashregister.csv", [.. Key(), register.Code, "AISPA", "AISPA Studio", "AISPA", "1.0", "EUR", 0]);
        Row(t, "slaves.csv", [.. Key(), register.Code, register.Code]);
        Row(t, "pa.csv", [.. Key(), "", "", "", "", "", "", "", ""]);
        foreach (var vat in Vat)
        {
            Row(t, "vat.csv", [.. Key(), vat.Key, vat.Percent, vat.Name]);
        }

        // TSE-Stammdaten aus dem ersten signierten Beleg: Algorithmus, Zeitformat und öffentlicher Schlüssel stehen im QR-Code.
        if (receipts.Select(r => r.FiscalCode?.Split(';')).FirstOrDefault(p => p is { Length: 12 } && p[0] == "V0") is { } qr)
        {
            Row(t, "tse.csv", [.. Key(), 1, register.CertificateSerial ?? "", qr[8], qr[9], "UTF-8", qr[11], "", ""]);
        }

        foreach (var r in receipts)
        {
            var start = Local(r.IssuedAt, zone);
            Row(t, "transactions.csv", [.. Key(), r.Number, r.Number, "Beleg", "", register.Code, r.Kind == ReceiptKind.Void ? 1 : 0, start, start,
                r.CreatedByUserId ?? "", "", r.Total, "", r.CustomerId?.ToString() ?? "", "", "", "", "", "", "", r.VoidReason ?? ""]);
            foreach (var g in r.Lines.GroupBy(l => VatKey(l.TaxPercent)))
            {
                var gross = g.Sum(l => l.Amount);
                var net = TaxRates.Net(gross, g.First().TaxPercent);
                Row(t, "transactions_vat.csv", [.. Key(), r.Number, g.Key, gross, net, gross - net]);
            }

            foreach (var p in r.Payments.GroupBy(p => (Type: PaymentType(p.Method), Name: PaymentMethods.Label(p.Method))))
            {
                Row(t, "datapayment.csv", [.. Key(), r.Number, p.Key.Type, p.Key.Name, "EUR", p.Sum(x => x.Amount), p.Sum(x => x.Amount)]);
            }

            if (Tse(r.FiscalCode) is { } signature)
            {
                Row(t, "transactions_tse.csv", [.. Key(), r.Number, 1, signature.Transaction, signature.Start, signature.End,
                    KassenSichVFiscalization.ProcessType, signature.Counter, signature.Signature, ""]);
            }
            else
            {
                Row(t, "transactions_tse.csv", [.. Key(), r.Number, 1, "", "", "", KassenSichVFiscalization.ProcessType, "", "",
                    r.SignatureDeviceFailed ? "TSE ausgefallen" : "keine Signatur"]);
            }
        }

        foreach (var (r, l) in lines)
        {
            var type = l.RevenueAccountId == PosModel.MultiPurposeVouchers.Id ? "MehrzweckgutscheinKauf" : "Umsatz";
            var net = TaxRates.Net(l.Amount, l.TaxPercent);
            Row(t, "lines.csv", [.. Key(), r.Number, l.Position, "", l.Description, register.Code, type, "", 0, r.Kind == ReceiptKind.Void ? 1 : 0,
                "", "", "", "", "", l.Quantity, 1, "Stück", l.Quantity == 0 ? 0 : Math.Round(l.Amount / l.Quantity, 2)]);
            Row(t, "lines_vat.csv", [.. Key(), r.Number, l.Position, VatKey(l.TaxPercent), l.Amount, net, l.Amount - net]);
        }

        foreach (var g in lines.GroupBy(x => (Type: x.Line.RevenueAccountId == PosModel.MultiPurposeVouchers.Id ? "MehrzweckgutscheinKauf" : "Umsatz",
                     Key: VatKey(x.Line.TaxPercent))))
        {
            var gross = g.Sum(x => x.Line.Amount);
            var net = TaxRates.NetSum(g.Select(x => x.Line), l => l.Amount, l => l.TaxPercent);
            Row(t, "businesscases.csv", [.. Key(), g.Key.Type, "", 0, g.Key.Key, gross, net, gross - net]);
        }

        foreach (var g in payments.GroupBy(p => (Type: PaymentType(p.Payment.Method), Name: PaymentMethods.Label(p.Payment.Method))))
        {
            Row(t, "payment.csv", [.. Key(), g.Key.Type, g.Key.Name, g.Sum(p => p.Payment.Amount)]);
        }

        Row(t, "cash_per_currency.csv", [.. Key(), "EUR", payments.Where(p => p.Payment.Method == PaymentMethod.Cash).Sum(p => p.Payment.Amount)]);
    }

    /// <summary>Steuerschlüssel nach DSFinV-K: 1 = 19 %, 2 = 7 %, 3 = 10,7 %, 4 = 5,5 %, 5 = nicht steuerbar.</summary>
    private static readonly (int Key, decimal Percent, string Name)[] Vat =
    [
        (1, 19m, "Regelsteuersatz"), (2, 7m, "Ermäßigter Steuersatz"), (3, 10.7m, "Durchschnittsatz (§ 24 Abs. 1 Nr. 3 UStG)"),
        (4, 5.5m, "Durchschnittsatz (§ 24 Abs. 1 Nr. 1 UStG)"), (5, 0m, "Nicht Steuerbar"),
    ];

    private static int VatKey(decimal percent) => Vat.FirstOrDefault(v => v.Percent == percent) is { Key: > 0 } v ? v.Key : 5;

    private static string PaymentType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Bar",
        PaymentMethod.InstantTransfer => "ElZahlungsdienstleister",
        _ => "Unbar",
    };

    /// <summary>TSE-Daten aus dem QR-Code nach DSFinV-K (V0;Kasse;Vorgangsart;Daten;TA-Nr;Zähler;Start;Ende;Algorithmus;Zeitformat;Signatur;Schlüssel).</summary>
    private static (string Transaction, string Counter, string Start, string End, string Signature)? Tse(string? code) =>
        code?.Split(';') is { Length: 12 } p && p[0] == "V0" ? (p[4], p[5], p[6], p[7], p[10]) : null;

    private static string Local(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static void Row(List<Table> tables, string file, object[] values)
    {
        var table = tables.Single(t => t.File == file);
        if (values.Length != table.Columns.Length)
        {
            throw new InvalidOperationException($"{file}: {values.Length} Werte für {table.Columns.Length} Spalten.");
        }

        table.Rows.Add(values);
    }

    private sealed record Table(string File, string Name, string[] Columns, string[] Numeric)
    {
        public List<object[]> Rows { get; } = [];

        public string Csv()
        {
            var text = new StringBuilder().AppendJoin(';', Columns).Append("\r\n");
            foreach (var row in Rows)
            {
                text.AppendJoin(';', row.Select(Format)).Append("\r\n");
            }

            return text.ToString();
        }

        private static string Format(object value) => value switch
        {
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            int or long => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            _ => $"\"{Convert.ToString(value, CultureInfo.InvariantCulture)!.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
        };
    }

    private static string Index(List<Table> tables, CashRegister register) =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("DataSet", null, "gdpdu-01-09-2004.dtd", null),
            new XElement("DataSet",
                new XElement("Version", "1.0"),
                new XElement("DataSupplier", new XElement("Name", "AISPA Studio"), new XElement("Location", register.Code),
                    new XElement("Comment", $"DSFinV-K {TaxonomyVersion}")),
                new XElement("Media", new XElement("Name", "DSFinV-K"),
                    tables.Select(t => new XElement("Table",
                        new XElement("URL", t.File), new XElement("Name", t.Name),
                        new XElement("DecimalSymbol", "."), new XElement("DigitGroupingSymbol", ","),
                        new XElement("VariableLength",
                            new XElement("ColumnDelimiter", ";"), new XElement("RecordDelimiter", "\r\n"), new XElement("TextEncapsulator", "\""),
                            t.Columns.Select(c => new XElement("VariableColumn", new XElement("Name", c),
                                t.Numeric.Contains(c) ? new XElement("Numeric", new XElement("Accuracy", c is "Z_NR" or "BON_ID" or "BON_NR" or "POS_ZEILE" ? "0" : "2"))
                                    : new XElement("AlphaNumeric"))))))))).ToString();

    private static List<Table> Tables()
    {
        string[] key = ["Z_KASSE_ID", "Z_ERSTELLUNG", "Z_NR"];
        Table T(string file, string name, string[] columns, params string[] numeric) => new(file, name, [.. key, .. columns], ["Z_NR", .. numeric]);
        return
        [
            T("cashpointclosing.csv", "Stamm_Abschluss", ["Z_BUCHUNGSTAG", "TAXONOMIE_VERSION", "Z_START_ID", "Z_ENDE_ID", "NAME", "STRASSE", "PLZ", "ORT",
                "LAND", "STNR", "USTID", "Z_SE_ZAHLUNGEN", "Z_SE_BARZAHLUNGEN"], "Z_SE_ZAHLUNGEN", "Z_SE_BARZAHLUNGEN"),
            T("location.csv", "Stamm_Orte", ["LOC_NAME", "LOC_STRASSE", "LOC_PLZ", "LOC_ORT", "LOC_LAND", "LOC_USTID"]),
            T("cashregister.csv", "Stamm_Kassen", ["KASSE_SERIENNR", "KASSE_BRAND", "KASSE_MODELL", "KASSE_SW_BRAND", "KASSE_SW_VERSION",
                "KASSE_BASISWAEH_CODE", "KEINE_UST_ZUORDNUNG"]),
            T("slaves.csv", "Stamm_Terminals", ["TERMINAL_ID", "TERMINAL_KASSE_SERIENNR"]),
            T("pa.csv", "Stamm_Agenturen", ["AGENTUR_ID", "AGENTUR_NAME", "AGENTUR_STRASSE", "AGENTUR_PLZ", "AGENTUR_ORT", "AGENTUR_LAND", "AGENTUR_STNR",
                "AGENTUR_USTID"]),
            T("vat.csv", "Stamm_USt", ["UST_SCHLUESSEL", "UST_SATZ", "UST_BESCHR"], "UST_SATZ"),
            T("tse.csv", "Stamm_TSE", ["TSE_ID", "TSE_SERIAL", "TSE_SIG_ALGO", "TSE_ZEITFORMAT", "TSE_PD_ENCODING", "TSE_PUBLIC_KEY", "TSE_ZERTIFIKAT_I",
                "TSE_ZERTIFIKAT_II"]),
            T("transactions.csv", "Bonkopf", ["BON_ID", "BON_NR", "BON_TYP", "BON_NAME", "TERMINAL_ID", "BON_STORNO", "BON_START", "BON_ENDE", "BEDIENER_ID",
                "BEDIENER_NAME", "UMS_BRUTTO", "KUNDE_NAME", "KUNDE_ID", "KUNDE_TYP", "KUNDE_STRASSE", "KUNDE_PLZ", "KUNDE_ORT", "KUNDE_LAND", "KUNDE_USTID",
                "BON_NOTIZ"], "BON_ID", "UMS_BRUTTO"),
            T("transactions_vat.csv", "Bonkopf_USt", ["BON_ID", "UST_SCHLUESSEL", "BON_BRUTTO", "BON_NETTO", "BON_UST"], "BON_ID", "BON_BRUTTO", "BON_NETTO", "BON_UST"),
            T("datapayment.csv", "Bonkopf_Zahlarten", ["BON_ID", "ZAHLART_TYP", "ZAHLART_NAME", "ZAHLWAEH_CODE", "ZAHLWAEH_BETRAG", "BASISWAEH_BETRAG"],
                "BON_ID", "ZAHLWAEH_BETRAG", "BASISWAEH_BETRAG"),
            T("transactions_tse.csv", "TSE_Transaktionen", ["BON_ID", "TSE_ID", "TSE_TANR", "TSE_TA_START", "TSE_TA_ENDE", "TSE_TA_VORGANGSART",
                "TSE_TA_SIGZ", "TSE_TA_SIG", "TSE_TA_FEHLER"], "BON_ID"),
            T("lines.csv", "Bonpos", ["BON_ID", "POS_ZEILE", "GUTSCHEIN_NR", "ARTIKELTEXT", "POS_TERMINAL_ID", "GV_TYP", "GV_NAME", "INHAUS", "P_STORNO",
                "AGENTUR_ID", "ART_NR", "GTIN", "WARENGR_ID", "WARENGR", "MENGE", "FAKTOR", "EINHEIT", "STK_BR"], "BON_ID", "POS_ZEILE", "MENGE", "FAKTOR", "STK_BR"),
            T("lines_vat.csv", "Bonpos_USt", ["BON_ID", "POS_ZEILE", "UST_SCHLUESSEL", "POS_BRUTTO", "POS_NETTO", "POS_UST"],
                "BON_ID", "POS_ZEILE", "POS_BRUTTO", "POS_NETTO", "POS_UST"),
            T("businesscases.csv", "Z_GV_Typ", ["GV_TYP", "GV_NAME", "AGENTUR_ID", "UST_SCHLUESSEL", "Z_UMS_BRUTTO", "Z_UMS_NETTO", "Z_UST"],
                "Z_UMS_BRUTTO", "Z_UMS_NETTO", "Z_UST"),
            T("payment.csv", "Z_Zahlart", ["ZAHLART_TYP", "ZAHLART_NAME", "Z_ZAHLART_BETRAG"], "Z_ZAHLART_BETRAG"),
            T("cash_per_currency.csv", "Z_Waehrungen", ["ZAHLART_WAEH", "ZAHLART_BETRAG_WAEH"], "ZAHLART_BETRAG_WAEH"),
        ];
    }
}

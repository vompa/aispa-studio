using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Exports;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Management-Bericht als PDF (T-095, Paket D2), A4 hoch nach docs/gestaltung/dokumente.md. Liest nur das <see cref="ManagementReportDocument"/> und rechnet nichts:
/// Seite 1 Titel, Zeitraum, KI-Kurzfassung mit Hinweis (wenn gewählt), Warnungen und die Kennzahlen je Gruppe mit Vergleich und Veränderung; danach der Verlauf als
/// waagrechte Balken und die übrigen Abschnitte als Tabellen in der Reihenfolge des Berichtskerns. Ab fünf Spalten stehen die Zeilen in der kleinen Schrift; damit passt
/// jede Tabelle des Berichts (höchstens sieben Spalten) auf A4 hoch.
/// </summary>
internal static class ManagementReportPdf
{
    public const string TrendName = "Verlauf";
    public const string NoRows = "Keine Einträge im Zeitraum.";

    private const int CompactFrom = 5;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static byte[] Build(ManagementReportDocument document)
    {
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, document.Header)
            .Title(Title(document))
            .Paragraph($"{document.Period}, verglichen mit {document.Comparison}", muted: true);
        foreach (var ai in document.Texts.Where(t => t.AiNotice is not null))
        {
            pdf.Heading(ai.Heading);
            ai.Lines.ToList().ForEach(line => pdf.Paragraph(line));
            pdf.Small(ai.AiNotice!);
        }

        foreach (var alerts in document.Texts.Where(t => t.Heading == ManagementReportDocument.AlertsName))
        {
            pdf.Heading(alerts.Heading);
            alerts.Lines.ToList().ForEach(line => pdf.Paragraph(line));
        }

        var overview = document.Sections.FirstOrDefault(s => s.Name == ManagementReportDocument.OverviewName);
        if (overview is not null)
        {
            KpiGroups(pdf, overview);
        }

        foreach (var section in document.Sections.Where(s => s != overview))
        {
            if (section.Name == TrendName)
            {
                Trend(pdf, section);
            }
            else
            {
                Table(pdf, section);
            }
        }

        foreach (var notes in document.Texts.Where(t => t.AiNotice is null && t.Heading != ManagementReportDocument.AlertsName))
        {
            pdf.Heading(notes.Heading);
            notes.Lines.ToList().ForEach(line => pdf.Paragraph(line, muted: true));
        }

        return pdf.Build();
    }

    /// <summary>„Management-Bericht-Wien-2027-03-01-bis-2027-03-07.pdf“.</summary>
    public static string FileName(ManagementReportDocument document, ManagementReportDto report) =>
        DocumentFiles.PdfName(document.Title, LocationName(document), Iso(report.From), "bis", Iso(report.Until));

    /// <summary>Vorbelegung für „Per Mail senden“: die eigene Adresse (leer, wenn das Konto keine hat), Betreff mit Standort und Zeitraum, kurzer Text mit Gruß.</summary>
    public static DocumentMailDraft Draft(ManagementReportDocument document, string? ownAddress) => new(
        ownAddress,
        $"{Title(document)}, {document.Period}",
        $"Guten Tag,\n\nim Anhang steht der {Title(document)} für den Zeitraum {document.Period}.{DocumentMails.Closing(document.Header.SpaName)}");

    internal static string Title(ManagementReportDocument document) =>
        LocationName(document) is { Length: > 0 } location ? $"{document.Title} {location}" : document.Title;

    internal static string LocationName(ManagementReportDocument document) => document.Header.LocationLines.Count > 0 ? document.Header.LocationLines[0] : "";

    /// <summary>
    /// Kennzahlen je Gruppe (Spalte „Gruppe“ der Übersicht) als eigene Tabelle: Kennzahl, Einheit, Wert, Vergleich, Veränderung absolut (bei Quoten in Prozentpunkten)
    /// und in Prozent. Euro steht als „EUR“ in der Spalte Einheit, nicht hinter jeder Zahl.
    /// </summary>
    private static void KpiGroups(DocumentBuilder pdf, ExportTable overview)
    {
        var columns = overview.Columns;
        DocumentColumn[] heads =
        [
            new(columns[0].Name, 3), new(columns[3].Name, 0.8), new(columns[1].Name, 1.2, DocumentAlign.Right), new(columns[2].Name, 1.2, DocumentAlign.Right),
            new(columns[4].Name, 1.4, DocumentAlign.Right), new(columns[5].Name, 1.1, DocumentAlign.Right),
        ];
        foreach (var group in overview.Rows.GroupBy(r => r[6] as string ?? "Kennzahlen"))
        {
            pdf.Heading(group.Key);
            pdf.Table(heads, group.Select(r => new DocumentRow(
            [
                Text(r[0]), Unit(r[3] as string), Plain(r[1]), Plain(r[2]), Signed(r[4]), Signed(r[5]),
            ])), compact: true);
        }
    }

    /// <summary>Der Verlauf als waagrechte Balken, ein Block je Spalte mit Balken-Hinweis (Umsatz netto, Auslastung Personal); je Zeile ein Tag oder eine Woche.</summary>
    private static void Trend(DocumentBuilder pdf, ExportTable trend)
    {
        pdf.Heading(trend.Name);
        if (trend.Rows.Count == 0)
        {
            pdf.Paragraph(NoRows, muted: true);
            return;
        }

        var weekly = trend.Rows.Any(r => r[0] is DateOnly from && r[1] is DateOnly until && from != until);
        for (var index = 0; index < trend.Columns.Count; index++)
        {
            var column = trend.Columns[index];
            if (column.Hint != ColumnHint.Bar)
            {
                continue;
            }

            pdf.Bars(weekly ? "Woche" : "Tag", Header(column), trend.Rows.Select(r => new DocumentBar(
                Period(r[0], r[1]), Value(r[index]) ?? 0, Format(r[index], column.Kind))), column.Kind == ColumnKind.Percent ? 100m : null);
        }
    }

    private static void Table(DocumentBuilder pdf, ExportTable section)
    {
        pdf.Heading(section.Name);
        if (section.Rows.Count == 0)
        {
            pdf.Paragraph(NoRows, muted: true);
            return;
        }

        var columns = section.Columns.Select((c, i) => new DocumentColumn(Header(c), i == 0 ? 2.2 : Weight(Header(c)),
            c.Kind is ColumnKind.Text or ColumnKind.Date ? DocumentAlign.Left : DocumentAlign.Right)).ToList();
        pdf.Table(columns, section.Rows.Select(r => new DocumentRow([.. section.Columns.Select((c, i) => Format(r[i], c.Kind))])),
            compact: section.Columns.Count >= CompactFrom);
    }

    /// <summary>Spaltenkopf; Beträge nennen die Währung als Wort („Umsatz brutto EUR“).</summary>
    internal static string Header(ExportColumn column) => column.Kind == ColumnKind.Money ? $"{column.Name} EUR" : column.Name;

    /// <summary>Breite einer Spalte nach dem längsten Wort ihres Kopfs, damit „Behandlungsstunden“ in der kleinen Schrift nicht mitten im Wort bricht.</summary>
    internal static double Weight(string header) => Math.Max(1, header.Split(' ').Max(word => word.Length) / 10.0);

    internal static string Unit(string? unit) => unit switch
    {
        "€" => "EUR",
        null => "",
        _ => unit,
    };

    internal static string Text(object? value) => value as string ?? "";

    /// <summary>Ein Wert in seiner Art: Betrag mit zwei Stellen, Prozent mit einer Stelle und Zeichen, Datum de-AT; leer als „–“.</summary>
    internal static string Format(object? value, ColumnKind kind) => value switch
    {
        null => "–",
        ExportCell cell => Format(cell.Value, cell.Kind),
        string text => Unit(text),
        DateOnly day => DocumentBuilder.Date(day),
        bool yes => yes ? "ja" : "nein",
        _ when Value(value) is { } number => kind switch
        {
            ColumnKind.Money => DocumentBuilder.Money(number),
            ColumnKind.Percent => $"{Math.Round(number, 1).ToString("0.0", German)} %",
            _ => DocumentBuilder.Number(number),
        },
        _ => Convert.ToString(value, German) ?? "",
    };

    /// <summary>Zahl ohne Einheit (die steht in der Spalte Einheit): Beträge mit zwei Stellen, Quoten mit einer.</summary>
    internal static string Plain(object? value) => value switch
    {
        null => "–",
        ExportCell { Kind: ColumnKind.Money } cell when Value(cell.Value) is { } money => DocumentBuilder.Money(money),
        ExportCell { Kind: ColumnKind.Percent } cell when Value(cell.Value) is { } rate => Math.Round(rate, 1).ToString("0.0", German),
        ExportCell cell => Plain(cell.Value),
        _ when Value(value) is { } number => DocumentBuilder.Number(number),
        _ => Convert.ToString(value, German) ?? "",
    };

    /// <summary>Veränderung mit Vorzeichen: „+1.234,50“, „-3,0“ (Bindestrich, weil die Schrift in WinAnsi kein Minuszeichen hat); ohne Vergleich leer.</summary>
    internal static string Signed(object? value)
    {
        if (value is null)
        {
            return "";
        }

        var number = Value(value is ExportCell cell ? cell.Value : value) ?? 0;
        var text = Plain(value is ExportCell c ? c with { Value = Math.Abs(number) } : Math.Abs(number));
        return number switch
        {
            > 0 => $"+{text}",
            < 0 => $"-{text}",
            _ => text,
        };
    }

    internal static decimal? Value(object? value) => value switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        double f => (decimal)f,
        ExportCell cell => Value(cell.Value),
        _ => null,
    };

    internal static string Period(object? from, object? until) => (from, until) switch
    {
        (DateOnly f, DateOnly u) when f == u => f.ToString("ddd dd.MM.", German),
        (DateOnly f, DateOnly u) => $"{f.ToString("dd.MM.", German)}–{u.ToString("dd.MM.", German)}",
        _ => "",
    };

    internal static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

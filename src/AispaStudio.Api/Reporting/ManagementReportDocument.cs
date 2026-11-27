using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Exports;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>Ein Textblock des Berichts: Überschrift, Zeilen und, bei KI-Text, der Hinweis, der daneben stehen muss.</summary>
internal sealed record ReportTextBlock(string Heading, IReadOnlyList<string> Lines, string? AiNotice = null);

/// <summary>
/// Inhalt des Management-Berichts als Dokument, einmal gebaut und von allen Ausgaben (Excel-Arbeitsmappe, PDF, Word) nur gelesen; keine Ausgabe rechnet selbst.
/// <c>Facts</c> ist der Kopfblock (Betrieb, Standort, Zeitraum, Vergleich, Erstellt), <c>Sections</c> die Tabellen in der Reihenfolge des Berichts
/// (Blattname = Überschrift), <c>Texts</c> die Hinweise (Warnungen, Vergleich mit anderen Spas, KI-Auswertung mit Hinweis).
/// Geldbeträge fehlen schon im <see cref="ManagementReportDto"/> ohne „Berichte mit Beträgen“; dann fehlen hier auch die Spalten und Tabellen, die nur Beträge zeigen.
/// </summary>
internal sealed record ManagementReportDocument(
    DocumentHeader Header,
    string Title,
    string Period,
    string Comparison,
    IReadOnlyList<KeyValuePair<string, string>> Facts,
    IReadOnlyList<ExportTable> Sections,
    IReadOnlyList<ReportTextBlock> Texts)
{
    public const string OverviewName = "Übersicht";
    public const string GuestsName = "Gäste";
    public const string NotesName = "Hinweise";
    public const string AlertsName = "Warnungen";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <param name="guests">Gästenamen (beste und abwandernde Gäste) nur mit dem Recht der Kundenliste; sonst fehlt das Blatt.</param>
    /// <param name="benchmark">Vergleich mit anderen Spas, wenn das Modul eingeschaltet ist.</param>
    /// <param name="aiSummary">KI-Auswertung, wenn gewählt und vorhanden; steht nur mit <paramref name="aiNotice"/> im Dokument.</param>
    /// <param name="analysis">Die übernommene KI-Auswertung („Mit KI auswerten“, T-096 E2) mit Hinweis, wenn die Person sie in den Bericht übernommen hat.</param>
    public static ManagementReportDocument Build(ManagementReportDto report, DocumentHeader header, string compare, bool guests,
        BenchmarkReport? benchmark = null, string? aiSummary = null, string? aiNotice = null, ReportTextBlock? analysis = null)
    {
        var amounts = !report.AmountsHidden;
        var compareLabel = compare switch
        {
            ManagementReports.CompareModes.Month => "Vormonat",
            ManagementReports.CompareModes.Year => "Vorjahr",
            _ => "Vorperiode",
        };
        var period = $"{Day(report.From)} bis {Day(report.Until)}";
        var comparison = report.CompareFrom is { } cf && report.CompareUntil is { } cu ? $"{compareLabel} {Day(cf)} bis {Day(cu)}" : compareLabel;
        var facts = new List<KeyValuePair<string, string>>
        {
            new("Betrieb", header.SpaName),
            new("Standort", header.LocationLines.Count > 0 ? header.LocationLines[0] : ""),
            new("Zeitraum", period),
            new("Vergleich", comparison),
            new("Erstellt am", DocumentBuilder.DateTime(header.CreatedAt, header.Zone)),
            new("Erstellt von", header.CreatedBy ?? "–"),
        };

        var sections = new List<ExportTable> { Overview(report, compareLabel), Trend(report, amounts) };
        if (amounts)
        {
            sections.Add(new ExportTable("Erlöskonten",
                [new("Erlöskonto", ColumnKind.Text), new("Kostenstelle", ColumnKind.Text), new("Umsatz brutto", ColumnKind.Money, ColumnHint.Bar), new("Umsatz netto", ColumnKind.Money)],
                [.. report.RevenueByAccount.Select(r => new object?[] { r.Label, r.CostCenter, r.Gross, r.Net })]));
            sections.Add(new ExportTable("Zahlungsarten",
                [new("Zahlungsart", ColumnKind.Text), new("Umsatz", ColumnKind.Money, ColumnHint.Bar)],
                [.. report.RevenueByPayment.Select(r => new object?[] { r.Label, r.Gross })]));
        }

        sections.Add(Employees(report, amounts));
        sections.Add(Treatments(report, amounts));
        sections.Add(Promotions(report, amounts));
        sections.Add(new ExportTable("Räume",
            [new("Raum", ColumnKind.Text), new("Auslastung", ColumnKind.Percent, ColumnHint.Bar), new("Termine", ColumnKind.Number)],
            [.. (report.Rooms ?? []).Select(r => new object?[] { r.Name, (decimal)r.Utilization, r.Appointments })]));
        sections.Add(new ExportTable("Wochentage",
            [new("Wochentag", ColumnKind.Text), new("Vormittag", ColumnKind.Percent), new("Mittag", ColumnKind.Percent), new("Nachmittag", ColumnKind.Percent), new("Abend", ColumnKind.Percent)],
            [.. (report.LoadByWeekday ?? []).Select(w => new object?[] { German.DateTimeFormat.GetDayName(w.Day), w.Morning, w.Midday, w.Afternoon, w.Evening })]));
        sections.Add(new ExportTable("Kanäle",
            [new("Kanal", ColumnKind.Text), new("Termine", ColumnKind.Number, ColumnHint.Bar)],
            [.. (report.Channels ?? []).Select(c => new object?[] { c.Label, c.Appointments })]));
        sections.Add(Forecast(report, amounts));
        if (guests)
        {
            sections.Add(Guests(report, amounts));
        }

        if (benchmark is { Metrics.Count: > 0 })
        {
            sections.Add(new ExportTable("Vergleich mit Spas",
                [new("Kennzahl", ColumnKind.Text), new("Eigener Wert", ColumnKind.Number), new("Unteres Viertel", ColumnKind.Number), new("Mitte", ColumnKind.Number),
                    new("Oberes Viertel", ColumnKind.Number), new("Einheit", ColumnKind.Text)],
                [.. benchmark.Metrics.Select(m => new object?[] { m.Label, Typed(m.Own, m.Unit), Typed(m.P25, m.Unit), Typed(m.Median, m.Unit), Typed(m.P75, m.Unit), m.Unit })]));
        }

        var texts = new List<ReportTextBlock>();
        if (report.Alerts.Count > 0)
        {
            texts.Add(new ReportTextBlock(AlertsName, report.Alerts));
        }

        if (benchmark is not null)
        {
            texts.Add(new ReportTextBlock($"Vergleich mit anderen Spas: {benchmark.PeerGroup}", benchmark.Notes));
        }

        if (!string.IsNullOrWhiteSpace(aiSummary) && !string.IsNullOrWhiteSpace(aiNotice))
        {
            texts.Add(new ReportTextBlock("KI-Auswertung", [aiSummary], aiNotice));
        }

        if (analysis is { AiNotice: not null })
        {
            texts.Add(analysis);
        }

        return new ManagementReportDocument(header, "Management-Bericht", period, comparison, facts, sections, texts);
    }

    /// <summary>Die Hinweise als Tabelle (Art, Text) für die Arbeitsmappe; der KI-Hinweis steht als eigene Zeile direkt unter dem KI-Text.</summary>
    public ExportTable NotesTable() => new(NotesName,
        [new("Art", ColumnKind.Text), new("Text", ColumnKind.Text)],
        [.. Texts.SelectMany(t => t.Lines.Select(line => new object?[] { t.Heading, line })
            .Concat(t.AiNotice is { } notice ? [new object?[] { t.Heading, notice }] : []))]);

    /// <summary>
    /// Kennzahlen: die ersten vier Spalten wie der bisherige Export (Kennzahl, Wert, Vergleich, Einheit), dazu Veränderung absolut (bei Quoten in Prozentpunkten)
    /// und in Prozent (nur bei Werten, die keine Quoten sind, und nur mit einem Vergleichswert ungleich 0) und die Gruppe.
    /// </summary>
    private static ExportTable Overview(ManagementReportDto report, string compareLabel) => new(OverviewName,
        [
            new("Kennzahl", ColumnKind.Text), new("Wert", ColumnKind.Number), new(compareLabel, ColumnKind.Number), new("Einheit", ColumnKind.Text),
            new("Veränderung (bei % in Prozentpunkten)", ColumnKind.Number, ColumnHint.Change), new("Veränderung in %", ColumnKind.Percent), new("Gruppe", ColumnKind.Text),
        ],
        [.. report.Kpis.Select(k => new object?[]
        {
            k.LabelWithNote(), Typed(k.Value, k.Unit), Typed(k.Previous, k.Unit), k.Unit,
            k.Previous is { } previous ? Typed(k.Value - previous, k.Unit == "%" ? "" : k.Unit) : null,
            k.Previous is { } p && p != 0 && k.Unit != "%" ? Math.Round((k.Value - p) / Math.Abs(p) * 100m, 1) : null,
            k.Group,
        })]);

    private static ExportTable Trend(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn> { new("Von", ColumnKind.Date), new("Bis", ColumnKind.Date) };
        if (amounts)
        {
            columns.Add(new("Umsatz netto ohne Gutscheinverkauf", ColumnKind.Money, ColumnHint.Bar));
        }

        columns.AddRange([new("Behandlungen", ColumnKind.Number), new("Auslastung Personal", ColumnKind.Percent, ColumnHint.Bar)]);
        return new ExportTable("Verlauf", columns, [.. (report.Trend ?? []).Select(t => Row(amounts,
            [t.From, t.Until], [t.RevenueNet], [t.Appointments, t.StaffUtilization]))]);
    }

    private static ExportTable Employees(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn>
        {
            new("Name", ColumnKind.Text), new("Dienststunden", ColumnKind.Number), new("Behandlungsstunden", ColumnKind.Number),
            new("Auslastung", ColumnKind.Percent, ColumnHint.Bar), new("Termine", ColumnKind.Number),
        };
        if (amounts)
        {
            columns.Add(new("Umsatz je Behandlungsstunde netto", ColumnKind.Money));
        }

        return new ExportTable("Personal", columns, [.. report.Employees.Select(e => Row(amounts,
            [e.Name, e.ShiftHours, e.TreatmentHours, (decimal)e.Utilization, e.Appointments], [e.RevenuePerHour], []))]);
    }

    private static ExportTable Treatments(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn> { new("Behandlung", ColumnKind.Text), new("Termine", ColumnKind.Number, amounts ? ColumnHint.None : ColumnHint.Bar) };
        if (amounts)
        {
            columns.AddRange([new("Umsatz netto", ColumnKind.Money, ColumnHint.Bar), new("Marge je Termin", ColumnKind.Money)]);
        }

        return new ExportTable("Behandlungen", columns, [.. report.Treatments.Select(t => Row(amounts,
            [t.Name, t.Appointments], [t.NetRevenue, t.MarginPerAppointment], []))]);
    }

    private static ExportTable Promotions(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn> { new("Aktion", ColumnKind.Text), new("Positionen", ColumnKind.Number), new("Gäste", ColumnKind.Number), new("Davon neu", ColumnKind.Number) };
        if (amounts)
        {
            columns.AddRange([new("Rabatt", ColumnKind.Money), new("Umsatz brutto", ColumnKind.Money, ColumnHint.Bar)]);
        }

        return new ExportTable("Aktionen", columns, [.. (report.Promotions ?? []).Select(p => Row(amounts,
            [p.Name, p.Positions, p.Guests, p.NewGuests], [p.Discount, p.Revenue], []))]);
    }

    private static ExportTable Forecast(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn> { new("Von", ColumnKind.Date), new("Bis", ColumnKind.Date), new("Termine", ColumnKind.Number) };
        if (amounts)
        {
            columns.Add(new("Gebuchter Umsatz brutto", ColumnKind.Money, ColumnHint.Bar));
        }

        columns.Add(new("Gebuchte Auslastung Personal", ColumnKind.Percent, ColumnHint.Bar));
        return new ExportTable("Vorschau", columns, [.. (report.Forecast ?? []).Select(w => Row(amounts,
            [w.From, w.Until, w.Appointments], [w.BookedRevenue], [w.StaffUtilization]))]);
    }

    /// <summary>Beste und abwandernde Gäste in einer Tabelle, unterschieden in der ersten Spalte, damit der Autofilter beide trennt.</summary>
    private static ExportTable Guests(ManagementReportDto report, bool amounts)
    {
        var columns = new List<ExportColumn> { new("Liste", ColumnKind.Text), new("Name", ColumnKind.Text), new("Besuche", ColumnKind.Number), new("Letzter Besuch", ColumnKind.Date) };
        if (amounts)
        {
            columns.Add(new("Umsatz brutto", ColumnKind.Money, ColumnHint.Bar));
        }

        var top = (report.TopGuests ?? []).Select(g => Row(amounts, ["Beste Gäste", g.Name, g.Visits, null], [g.Spent], []));
        var lapsing = (report.LapsingGuests ?? []).Select(g => Row(amounts, ["Abwandernde Stammgäste", g.Name, g.Visits, g.LastVisit], [null], []));
        return new ExportTable(GuestsName, columns, [.. top, .. lapsing]);
    }

    /// <summary>Eine Zeile: Werte vor den Beträgen, die Beträge nur mit dem Recht „Berichte mit Beträgen“, Werte danach.</summary>
    private static object?[] Row(bool amounts, object?[] before, object?[] money, object?[] after) => [.. before, .. amounts ? money : [], .. after];

    /// <summary>Euro als Betrag, Prozent als Prozent, alles andere als Zahl; die Einheit kommt aus der Kennzahl.</summary>
    private static object? Typed(decimal? value, string unit) => value is not { } number ? null : unit switch
    {
        "€" => new ExportCell(number, ColumnKind.Money),
        "%" => new ExportCell(number, ColumnKind.Percent),
        _ => number,
    };

    private static string Day(DateOnly date) => date.ToString("d.M.yyyy", German);
}

/// <summary>Was die Ausgaben des Berichts außer den Zahlen brauchen: Kopf des Dokuments und, wenn das Modul eingeschaltet ist, den Vergleich mit anderen Spas.</summary>
internal sealed class ManagementDocumentSources(DocumentHeaders headers, Benchmark benchmark, ModuleSwitches modules, TenantContext tenants, TenantLocks locks)
{
    public DocumentHeaders Headers => headers;

    /// <summary>Vergleich für denselben Zeitraum; null, wenn das Modul aus ist oder die Abo-Stufe es nicht enthält.</summary>
    public async Task<BenchmarkReport?> BenchmarkAsync(ManagementReportDto report, bool amounts, CancellationToken ct)
    {
        var plan = tenants.TenantId is { } tenantId ? await locks.PlanAsync(tenantId, ct) : (SubscriptionPlan?)null;
        return modules.IsOn(AppModule.Benchmark, plan) ? await benchmark.CompareAsync(report.LocationId, report.From, report.Until, amounts, ct) : null;
    }
}

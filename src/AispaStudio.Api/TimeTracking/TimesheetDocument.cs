using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.TimeTracking;

/// <summary>
/// Stundenliste einer Person für einen Monat als PDF (PDF-Ausgaben F, ADR 0091): je Tag mit Dienst oder Zeiten Kommen (erster Beginn), Gehen (letztes Ende), Pausen (Lücken zwischen den Abschnitten),
/// Ist, Soll und Saldo, unten die Monatssumme und Unterschriftenzeilen für die Person und die Leitung. Die Zahlen sind die der Zeiten-Maske (<see cref="TimesheetDay"/>), Stunden stehen dezimal.
/// </summary>
internal static class TimesheetDocument
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public sealed record Data(string PersonName, DateOnly Month, TimesheetRow Row, IReadOnlyDictionary<Guid, TimeZoneInfo> Zones);

    public static string FileName(Data data) => DocumentFiles.PdfName("Stundenliste", data.PersonName, data.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture));

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Title($"Stundenliste {data.Month.ToString("MMMM yyyy", German)}");
        pdf.Paragraph(data.PersonName, bold: true);
        var days = data.Row.Days.Where(d => d.PlannedHours > 0 || d.Records.Count > 0).ToList();
        pdf.Table(
            [new("Datum", 2), new("Kommen", 1), new("Gehen", 1), new("Pause", 1.2, DocumentAlign.Right), new("Ist", 1, DocumentAlign.Right), new("Soll", 1, DocumentAlign.Right), new("Saldo", 1, DocumentAlign.Right)],
            days.Select(d => Day(d, data)),
            DocumentRow.Of("Monatssumme", "", "", "", Hours(data.Row.ActualHours), Hours(data.Row.PlannedHours), Signed(data.Row.ActualHours - data.Row.PlannedHours)));
        pdf.Small("Zeiten in Stunden; Tage ohne Dienst und ohne Zeiten sind nicht aufgeführt.");
        pdf.Space(24);
        pdf.SignatureLines($"Unterschrift {data.PersonName}", "Unterschrift Leitung");
        return pdf.Build();
    }

    private static DocumentRow Day(TimesheetDay day, Data data)
    {
        var records = day.Records.OrderBy(r => r.Start).ToList();
        var first = records.FirstOrDefault();
        var last = records.LastOrDefault();
        return DocumentRow.Of(
            day.Date.ToString("ddd dd.MM.yyyy", German),
            first is null ? "" : Time(first.Start, first, data),
            last is null ? "" : last.End is { } end ? Time(end, last, data) : "läuft",
            day.BreakMinutes > 0 ? $"{day.BreakMinutes} min" : "",
            records.Count == 0 ? "" : Hours(day.ActualHours),
            Hours(day.PlannedHours),
            Signed(day.ActualHours - day.PlannedHours));
    }

    private static string Time(DateTimeOffset at, TimeRecordDto record, Data data) =>
        DocumentBuilder.Time(at, data.Zones.GetValueOrDefault(record.LocationId, TimeZoneInfo.Utc));

    private static string Hours(double value) => ((decimal)Math.Round(value, 2)).ToString("#,##0.##", German);

    private static string Signed(double value) => ((decimal)Math.Round(value, 2)).ToString("+#,##0.##;-#,##0.##;0", German);
}

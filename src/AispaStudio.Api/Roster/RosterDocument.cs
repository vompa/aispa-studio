using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Dienstplan-Aushang als PDF (PDF-Ausgaben F, ADR 0091): A4 quer, eine Zeile je Person und eine Spalte je Tag der Woche mit Diensten (Zeit, bei einem anderen Standort mit dessen Namen), Abwesenheiten
/// (grau hinterlegt) und der Notiz des Tages, dazu die Wochenstunden. Es gilt der veröffentlichte Stand; Entwürfe stehen nicht darauf. Die Kopfzeile nennt, wann der Plan zuletzt veröffentlicht wurde.
/// </summary>
internal static class RosterDocument
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Die Zelle einer Person an einem Tag: die sichtbaren Einträge des Plans mit den Namen der Standorte und die Notiz.</summary>
    public sealed record Cell(DayPlan Plan, IReadOnlyDictionary<Guid, string> LocationNames);

    public sealed record Row(string Name, IReadOnlyList<Cell> Days);

    public sealed record Data(Guid LocationId, string LocationName, DateOnly From, IReadOnlyList<Row> Rows, DateTimeOffset? PublishedAt);

    public static string FileName(Data data) => DocumentFiles.PdfName("Dienstplan", data.LocationName, data.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Der Montag der Woche, in der <paramref name="date"/> liegt.</summary>
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var until = data.From.AddDays(6);
        var pdf = new DocumentBuilder(DocumentFormat.A4Landscape, header);
        pdf.Title($"Dienstplan {data.From.ToString("dd.MM.", German)} – {DocumentBuilder.Date(until)}");
        pdf.Paragraph(data.PublishedAt is { } at ? $"{data.LocationName} · Veröffentlicht am {DocumentBuilder.DateTime(at, header.Zone)}" : data.LocationName, muted: true);
        if (data.Rows.Count == 0)
        {
            pdf.Paragraph("An diesem Standort ist niemand eingeteilt.");
            return pdf.Build();
        }

        pdf.Table(
            [new("Person", 1.5), .. Enumerable.Range(0, 7).Select(i => new DocumentColumn(data.From.AddDays(i).ToString("ddd dd.MM.", German), 1.4)), new("Stunden", 0.9, DocumentAlign.Right)],
            data.Rows.Select(row => ToRow(row, data.LocationId)));
        return pdf.Build();
    }

    private static DocumentRow ToRow(Row row, Guid locationId)
    {
        var cells = new List<string> { row.Name };
        var absences = new List<int>();
        foreach (var day in row.Days)
        {
            var plan = day.Plan;
            cells.Add(Text(day, locationId));
            if (plan.Entries.Count > 0 && plan.Entries.All(e => e.Kind != RosterKind.Work))
            {
                absences.Add(cells.Count - 1);
            }
        }

        cells.Add(DocumentBuilder.Number((decimal)Math.Round(row.Days.Sum(d => d.Plan.Hours), 2)));
        return new DocumentRow(cells, MutedCells: absences);
    }

    private static string Text(Cell cell, Guid locationId)
    {
        var lines = cell.Plan.Entries.Select(e => e.Kind == RosterKind.Work ? Shift(e, cell.LocationNames, locationId) : Absence(e.Kind)).ToList();
        if (!string.IsNullOrWhiteSpace(cell.Plan.Note))
        {
            lines.Add(cell.Plan.Note.Trim());
        }

        return lines.Count == 0 ? "–" : string.Join('\n', lines);
    }

    private static string Shift(RosterEntry entry, IReadOnlyDictionary<Guid, string> names, Guid locationId)
    {
        var time = $"{entry.Start:HH:mm}–{entry.End:HH:mm}";
        return entry.LocationId is { } id && id != locationId && names.TryGetValue(id, out var name) ? $"{time} {name}" : time;
    }

    private static string Absence(RosterKind kind) => kind switch
    {
        RosterKind.Vacation => "Urlaub",
        RosterKind.Sick => "Krank",
        RosterKind.DayOff => "Frei",
        RosterKind.Training => "Schulung",
        _ => kind.ToString(),
    };
}

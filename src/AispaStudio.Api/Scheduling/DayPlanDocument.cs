using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Tagesplan eines Standorts als PDF (PDF-Ausgaben E, ADR 0092): A4 quer, eine Spalte je Person oder je Raum. Die Tabelle hat eine Zeile je Beginn; in der Zelle steht der Termin mit
/// Zeit, Gast, Behandlung und Status, Pausen und Sperrzeiten stehen grau hinterlegt. Stornierte Termine fehlen. Die Fußnote nennt, wie viele Personen auf der Warteliste stehen.
/// </summary>
internal static class DayPlanDocument
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public sealed record Column(Guid Id, string Name);

    /// <summary>Ein Eintrag der Zelle: ein Termin (<paramref name="IsBlock"/> falsch) oder eine Sperrzeit mit ihrem Grund.</summary>
    public sealed record Entry(Guid ColumnId, DateTimeOffset Start, DateTimeOffset End, bool IsBlock, string Guest, string Title, AppointmentStatus Status, string? Detail);

    public sealed record Data(string LocationName, DateOnly Date, DayPlanColumns View, IReadOnlyList<Column> Columns, IReadOnlyList<Entry> Entries, int Waiting);

    public static string FileName(Data data) => DocumentFiles.PdfName("Tagesplan", data.LocationName, data.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var zone = header.Zone;
        var pdf = new DocumentBuilder(PaperFormat.A4Landscape, header);
        pdf.Title($"Tagesplan {data.Date.ToString("dddd, dd.MM.yyyy", German)}");
        var appointments = data.Entries.Count(e => !e.IsBlock);
        pdf.Paragraph($"{(data.View == DayPlanColumns.Rooms ? "Nach Räumen" : "Nach Team")} · {appointments} {(appointments == 1 ? "Termin" : "Termine")}", muted: true);
        if (data.Entries.Count == 0 || data.Columns.Count == 0)
        {
            pdf.Paragraph("Keine Termine an diesem Tag.");
        }
        else
        {
            pdf.Table([new("Zeit", 0.7), .. data.Columns.Select(c => new DocumentColumn(c.Name, 2))], Rows(data, zone));
        }

        pdf.Paragraph(WaitlistNote(data.Waiting), muted: true);
        return pdf.Build();
    }

    /// <summary>„Niemand auf der Warteliste“, „1 Person auf der Warteliste“, „3 Personen auf der Warteliste“.</summary>
    public static string WaitlistNote(int waiting) => waiting switch
    {
        0 => "Niemand auf der Warteliste",
        1 => "1 Person auf der Warteliste",
        _ => $"{waiting} Personen auf der Warteliste",
    };

    private static IEnumerable<DocumentRow> Rows(Data data, TimeZoneInfo zone) =>
        data.Entries.GroupBy(e => DocumentBuilder.Time(e.Start, zone)).OrderBy(g => g.Min(e => e.Start)).Select(group =>
        {
            var cells = new List<string> { group.Key };
            var muted = new List<int>();
            foreach (var column in data.Columns)
            {
                var entries = group.Where(e => e.ColumnId == column.Id).OrderBy(e => e.Start).ToList();
                cells.Add(string.Join("\n\n", entries.Select(e => Text(e, zone))));
                if (entries.Count > 0 && entries.All(e => e.IsBlock))
                {
                    muted.Add(cells.Count - 1);
                }
            }

            return new DocumentRow(cells, MutedCells: muted);
        });

    private static string Text(Entry entry, TimeZoneInfo zone)
    {
        var time = $"{DocumentBuilder.Time(entry.Start, zone)}–{DocumentBuilder.Time(entry.End, zone)}";
        if (entry.IsBlock)
        {
            return $"{time}\n{entry.Title}";
        }

        var treatment = entry.Detail is null ? entry.Title : $"{entry.Title} · {entry.Detail}";
        return $"{time} · {CustomerDisclosure.StatusLabel(entry.Status)}\n{entry.Guest}\n{treatment}";
    }
}

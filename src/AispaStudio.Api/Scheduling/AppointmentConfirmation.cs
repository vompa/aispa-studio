using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Terminbestätigung als PDF (PDF-Ausgaben C, ADR 0092): der Gast, alle Teile der Buchung (Paar, Spa-Tag) mit Uhrzeit, Dauer, Person (nur Vorname) und Preis,
/// der Standort mit Adresse, die Stornofrist des Standorts und der Absatz „Was mitbringen“ aus den Behandlungen.
/// </summary>
internal static class AppointmentConfirmation
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Ein Teil der Buchung, wie die Bestätigung ihn zeigt.</summary>
    public sealed record Part(Appointment Appointment, string Guest, string EmployeeFirstName);

    /// <summary>Alles, was die Bestätigung braucht: die Teile (der angefragte Termin zuerst in seiner Reihenfolge), Standort, Währung und die Regeln des Standorts.</summary>
    public sealed record Booking(IReadOnlyList<Part> Parts, Location Location, LocationOnlineRules Rules, TimeZoneInfo Zone);

    public static string FileName(Booking booking, Guid appointmentId)
    {
        var main = RequestedPart(booking, appointmentId);
        return DocumentFiles.PdfName("Terminbestätigung", DocumentBuilder.Date(main.Appointment.Start, booking.Zone));
    }

    public static DocumentMailDraft Draft(Booking booking, Guid appointmentId, string spaName, string? customerEmail)
    {
        var main = RequestedPart(booking, appointmentId).Appointment;
        var day = DocumentBuilder.Date(main.Start, booking.Zone);
        return new DocumentMailDraft(
            customerEmail,
            $"Ihre Terminbestätigung: {main.Treatment.Name} am {day}",
            $"Guten Tag,\n\nanbei erhalten Sie die Bestätigung Ihres Termins am {day} um {DocumentBuilder.Time(main.Start, booking.Zone)} Uhr als PDF.{DocumentMails.Closing(spaName)}");
    }

    public static byte[] Build(Booking booking, Guid appointmentId, DocumentHeader header)
    {
        var main = RequestedPart(booking, appointmentId);
        var parts = booking.Parts.OrderBy(p => p.Appointment.Start).ToList();
        var currency = booking.Location.Currency;
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, header);
        pdf.Title("Terminbestätigung");
        pdf.KeyValues([("Gast", main.Guest), ("Tag", Day(parts[0].Appointment.Start, booking.Zone))]);
        pdf.Heading(parts.Count > 1 ? "Ihre Termine" : "Ihr Termin");
        var withGuest = parts.Select(p => p.Guest).Distinct(StringComparer.Ordinal).Count() > 1;
        pdf.Table(Columns(withGuest, currency), parts.Select(p => Row(p, withGuest, booking.Zone)), TotalRow(parts, withGuest));
        pdf.Heading("Wo");
        pdf.KeyValues([(booking.Location.Name, string.Join('\n', Address(booking.Location)))]);
        if (Cancellation(booking, parts[0].Appointment.Start) is { } cancellation)
        {
            pdf.Heading("Absagen");
            pdf.Paragraph(cancellation);
        }

        if (Hints(parts) is { Count: > 0 } hints)
        {
            pdf.Heading("Was mitbringen");
            foreach (var hint in hints)
            {
                pdf.Paragraph(hint);
            }
        }

        return pdf.Build();
    }

    private static Part RequestedPart(Booking booking, Guid appointmentId) => booking.Parts.First(p => p.Appointment.Id == appointmentId);

    private static string Day(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("dddd, dd.MM.yyyy", German);

    private static DocumentColumn[] Columns(bool withGuest, string currency) =>
    [
        new("Zeit", 1),
        new("Behandlung", 3),
        .. withGuest ? [new DocumentColumn("Gast", 2)] : Array.Empty<DocumentColumn>(),
        new("Dauer", 1, DocumentAlign.Right),
        new("Mit", 1.5),
        new($"Preis {currency}", 1.5, DocumentAlign.Right),
    ];

    private static DocumentRow Row(Part part, bool withGuest, TimeZoneInfo zone)
    {
        var a = part.Appointment;
        var cells = new List<string> { DocumentBuilder.Time(a.Start, zone), a.Treatment.Name };
        if (withGuest)
        {
            cells.Add(part.Guest);
        }

        cells.AddRange([$"{(a.End - a.Start).TotalMinutes:0} Min.", part.EmployeeFirstName, DocumentBuilder.Money(a.Price)]);
        return new DocumentRow(cells);
    }

    private static DocumentRow? TotalRow(List<Part> parts, bool withGuest) =>
        parts.Count < 2 ? null : new DocumentRow([.. Enumerable.Repeat("", withGuest ? 4 : 3), "Summe", DocumentBuilder.Money(parts.Sum(p => p.Appointment.Price))], Bold: true);

    private static IEnumerable<string> Address(Location location)
    {
        if (!string.IsNullOrWhiteSpace(location.Street))
        {
            yield return location.Street.Trim();
        }

        var town = string.Join(' ', new[] { location.PostalCode, location.City }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (town.Length > 0)
        {
            yield return town;
        }

        if (!string.IsNullOrWhiteSpace(location.Directions))
        {
            yield return location.Directions.Trim();
        }
    }

    /// <summary>Stornofrist und Hinweis des Standorts (Online-Regeln); ohne Frist und ohne Hinweis steht nichts da.</summary>
    private static string? Cancellation(Booking booking, DateTimeOffset start)
    {
        var lines = new List<string>();
        if (booking.Rules.CancellationDeadlineHours > 0)
        {
            var until = start.AddHours(-booking.Rules.CancellationDeadlineHours);
            lines.Add($"Sie können kostenlos absagen oder verschieben bis {DocumentBuilder.DateTime(until, booking.Zone)} Uhr ({booking.Rules.CancellationDeadlineHours} Stunden vor dem Termin).");
        }

        if (!string.IsNullOrWhiteSpace(booking.Rules.CancellationPolicy))
        {
            lines.Add(booking.Rules.CancellationPolicy.Trim());
        }

        return lines.Count == 0 ? null : string.Join(' ', lines);
    }

    /// <summary>Hinweise der Behandlungen an die Gäste, je Behandlung einmal; bei mehreren Behandlungen mit deren Namen.</summary>
    private static List<string> Hints(IReadOnlyList<Part> parts)
    {
        var treatments = parts.Select(p => p.Appointment.Treatment).DistinctBy(t => t.Id).Where(t => !string.IsNullOrWhiteSpace(t.GuestHint)).ToList();
        var named = parts.Select(p => p.Appointment.TreatmentId).Distinct().Count() > 1;
        return [.. treatments.Select(t => named ? $"{t.Name}: {t.GuestHint!.Trim()}" : t.GuestHint!.Trim())];
    }
}

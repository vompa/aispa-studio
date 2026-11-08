using System.Globalization;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Online;

/// <summary>Ein Termin als .ics für den eigenen Kalender des Gastes (RFC 5545): Zeit in UTC, Behandlung als Titel, Standort mit Adresse.</summary>
internal static class AppointmentCalendarFile
{
    public const string ContentType = "text/calendar; charset=utf-8";
    public const string FileName = "termin.ics";

    /// <summary>Höchstlänge einer Zeile ohne Zeilenende in Bytes (RFC 5545, 3.1).</summary>
    private const int MaxLineBytes = 75;

    public static string Create(OnlineAppointment appointment, DateTimeOffset now)
    {
        var ics = new StringBuilder();
        void Line(string text) => ics.Append(Fold(text)).Append("\r\n");
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//AISPA Studio//Kundenbereich//DE");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("BEGIN:VEVENT");
        Line($"UID:{appointment.Id:N}@aispa.studio");
        Line($"DTSTAMP:{Stamp(now)}");
        Line($"DTSTART:{Stamp(appointment.Start)}");
        Line($"DTEND:{Stamp(appointment.End)}");
        Line($"SUMMARY:{Escape(appointment.TreatmentName)}");
        Line($"LOCATION:{Escape(Address(appointment))}");
        Line("END:VEVENT");
        Line("END:VCALENDAR");
        return ics.ToString();
    }

    private static string Address(OnlineAppointment appointment)
    {
        var contact = appointment.LocationContact;
        var city = string.Join(' ', new[] { contact.PostalCode, contact.City }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.Join(", ", new[] { appointment.LocationName, contact.Street, city }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string Stamp(DateTimeOffset t) => t.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Faltet nach Bytes und trennt nie innerhalb eines Zeichens, sonst zerfallen Umlaute im Kalender.</summary>
    private static string Fold(string line)
    {
        var folded = new StringBuilder();
        var bytes = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxLineBytes)
            {
                folded.Append("\r\n ");
                bytes = 1;
            }

            folded.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }

        return folded.ToString();
    }
}

using System.Globalization;
using System.Text;

namespace AispaStudio.Api.Exports;

/// <summary>Ein Kontakt für die vCard: Name mit Titeln, Rufname, Kontaktdaten, Postanschrift und der Vermerk aus der Kundenkarte (nie Wünsche oder Gesundheit).</summary>
internal sealed record VCardContact(
    Guid Id,
    string? TitlesBefore,
    string FirstName,
    string LastName,
    string? PreferredName,
    string? TitlesAfter,
    string? Email,
    string? Phone,
    DateOnly? BirthDate,
    string? Street,
    string? PostalCode,
    string? City,
    string? Country,
    string? Note,
    DateTimeOffset Revision);

/// <summary>Schreibt Kontakte als vCard 4.0 (RFC 6350): eine Datei für einen oder viele Kontakte, Zeilen nach 75 Oktetten umgebrochen, CRLF.</summary>
internal static class VCardWriter
{
    public const string ProductId = "-//AISPA Studio//DE";

    public static byte[] Write(IEnumerable<VCardContact> contacts)
    {
        var lines = new List<string>();
        foreach (var contact in contacts)
        {
            lines.Add("BEGIN:VCARD");
            lines.Add("VERSION:4.0");
            lines.Add($"PRODID:{ProductId}");
            lines.Add($"FN:{TextLines.Escape(FullName(contact))}");
            lines.Add($"N:{TextLines.Escape(contact.LastName)};{TextLines.Escape(contact.FirstName)};;{TextLines.Escape(contact.TitlesBefore)};{TextLines.Escape(contact.TitlesAfter)}");
            if (!string.IsNullOrWhiteSpace(contact.PreferredName))
            {
                lines.Add($"NICKNAME:{TextLines.Escape(contact.PreferredName)}");
            }

            if (!string.IsNullOrWhiteSpace(contact.Phone))
            {
                lines.Add(Phone(contact.Phone));
            }

            if (!string.IsNullOrWhiteSpace(contact.Email))
            {
                lines.Add($"EMAIL:{TextLines.Escape(contact.Email)}");
            }

            if (contact.BirthDate is { } birthday)
            {
                lines.Add($"BDAY:{birthday.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            }

            if (!string.IsNullOrWhiteSpace(contact.Street) || !string.IsNullOrWhiteSpace(contact.City) || !string.IsNullOrWhiteSpace(contact.PostalCode))
            {
                lines.Add($"ADR;TYPE=home:;;{TextLines.Escape(contact.Street)};{TextLines.Escape(contact.City)};;{TextLines.Escape(contact.PostalCode)};{TextLines.Escape(contact.Country)}");
            }

            if (!string.IsNullOrWhiteSpace(contact.Note))
            {
                lines.Add($"NOTE:{TextLines.Escape(contact.Note)}");
            }

            lines.Add($"UID:urn:uuid:{contact.Id}");
            lines.Add($"REV:{contact.Revision.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}");
            lines.Add("END:VCARD");
        }

        return TextLines.ToBytes(lines);
    }

    private static string FullName(VCardContact contact) =>
        string.Join(' ', new[] { contact.TitlesBefore, contact.FirstName, contact.LastName, contact.TitlesAfter }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>Als <c>tel:</c>-URI, wenn die Nummer nur aus Ziffern (und führendem Plus) besteht; sonst als Text, wie sie getippt wurde.</summary>
    private static string Phone(string phone)
    {
        var digits = new string([.. phone.Where(c => char.IsAsciiDigit(c) || c == '+')]);
        return digits.Length > 0 && digits.IndexOf('+', StringComparison.Ordinal) <= 0 && digits.Count(c => c == '+') <= 1
            ? $"TEL;TYPE=cell;VALUE=uri:tel:{digits}"
            : $"TEL;TYPE=cell:{TextLines.Escape(phone)}";
    }
}

/// <summary>Gemeinsames für vCard und iCal: Escapen von Textwerten, Umbruch nach 75 Oktetten (Fortsetzung mit Leerzeichen), CRLF, UTF-8.</summary>
internal static class TextLines
{
    private const int MaxOctets = 75;

    /// <summary>Rückstrich, Strichpunkt, Komma und Zeilenumbruch nach RFC 6350 und RFC 5545.</summary>
    public static string Escape(string? text) => text is null
        ? ""
        : text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\n", StringComparison.Ordinal);

    public static byte[] ToBytes(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            foreach (var part in Fold(line))
            {
                builder.Append(part).Append("\r\n");
            }
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Teilt eine Zeile so, dass jede Teilzeile samt führendem Leerzeichen höchstens 75 Oktette hat; nie mitten in einem Zeichen.</summary>
    private static IEnumerable<string> Fold(string line)
    {
        var budget = MaxOctets;
        var start = 0;
        var octets = 0;
        var first = true;
        for (var i = 0; i < line.Length; i++)
        {
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(i, char.IsHighSurrogate(line[i]) ? 2 : 1));
            if (octets + size > budget)
            {
                yield return (first ? "" : " ") + line[start..i];
                first = false;
                start = i;
                octets = 0;
                budget = MaxOctets - 1;
            }

            octets += size;
            if (char.IsHighSurrogate(line[i]))
            {
                i++;
            }
        }

        yield return (first ? "" : " ") + line[start..];
    }
}

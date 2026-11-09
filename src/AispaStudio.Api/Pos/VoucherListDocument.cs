using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Gutscheinliste eines Standorts als PDF (PDF-Ausgaben, Zusatz; ADR 0091): A4 quer, eine Zeile je Gutschein mit Code, Art, Ausstellung, Ausgangs- und Restwert, Gültigkeit, Status, Käufer, Empfänger,
/// Verkaufskanal und Versandvermerk; darunter die Summe der offenen Restwerte, die Verbindlichkeit für die Buchhaltung. Offen ist, was aktiv und nicht abgelaufen ist; ein abgelaufener Gutschein mit Rest
/// steht gesondert, bis jemand ihn ausbucht. Felder, die es in den Daten nicht gibt (Empfänger bei Kassa-Verkäufen, Ablaufdatum bei Wertgutscheinen), zeigt die Liste als „–“.
/// </summary>
internal static class VoucherListDocument
{
    /// <summary>Zustand eines Gutscheins für die Liste: „abgelaufen“ ist ein aktiver Gutschein, dessen Datum vorbei ist (die Datenbank kennt nur aktiv, eingelöst und storniert).</summary>
    public enum State
    {
        Active,
        Redeemed,
        Expired,
        Voided,
    }

    /// <summary>Auswahl der Liste; leere Felder schränken nicht ein. <see cref="Until"/> ist der letzte Tag und zählt mit.</summary>
    public sealed record Filter(DateOnly? From = null, DateOnly? Until = null, State? Status = null, bool Expiring = false, bool NotSent = false);

    public sealed record Entry(Voucher Voucher, string? TreatmentName, string? Buyer, string? Recipient, bool Online, IReadOnlyList<SentDocument> Sent);

    public sealed record Data(string LocationName, string Currency, Filter Filter, IReadOnlyList<Entry> Entries);

    public const int ExpiringDays = 30;

    public static string FileName(string locationName, DateOnly today) => DocumentFiles.PdfName("Gutscheinliste", locationName, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static State StateOf(Voucher voucher, DateOnly today) => voucher.Status switch
    {
        VoucherStatus.Voided => State.Voided,
        VoucherStatus.Redeemed => State.Redeemed,
        _ when voucher.ValidUntil is { } until && until < today => State.Expired,
        _ => State.Active,
    };

    /// <summary>
    /// Liest die Abfrage von Hand (unbekannt ist ein Fehler, kein stilles Weglassen): Daten als <c>yyyy-MM-dd</c>, Status als <c>active</c>, <c>redeemed</c>, <c>expired</c> oder <c>voided</c>,
    /// Schalter als <c>true</c> oder <c>false</c>. Null und die Fehlermeldungen je Feld, wenn etwas nicht passt.
    /// </summary>
    public static (Filter? Filter, Dictionary<string, string[]>? Errors) ParseFilter(string? from, string? until, string? status, string? expiring, string? notSent)
    {
        var errors = new Dictionary<string, string[]>();
        var fromDate = Date("from", from, errors);
        var untilDate = Date("until", until, errors);
        if (fromDate > untilDate)
        {
            errors["until"] = ["Das Ende des Zeitraums liegt vor dem Anfang."];
        }

        State? state = null;
        if (!string.IsNullOrEmpty(status))
        {
            state = status switch
            {
                "active" => State.Active,
                "redeemed" => State.Redeemed,
                "expired" => State.Expired,
                "voided" => State.Voided,
                _ => null,
            };
            if (state is null)
            {
                errors["status"] = ["Bitte active, redeemed, expired oder voided angeben."];
            }
        }

        var filter = new Filter(fromDate, untilDate, state, Switch("expiring", expiring, errors), Switch("notSent", notSent, errors));
        return errors.Count == 0 ? (filter, null) : (null, errors);
    }

    /// <summary>Die Gutscheine, die zur Auswahl passen, ältester zuerst.</summary>
    public static IReadOnlyList<Entry> Select(IEnumerable<Entry> entries, Filter filter, DateOnly today, TimeZoneInfo zone) =>
    [
        .. entries.Where(e => Matches(e, filter, today, zone)).OrderBy(e => e.Voucher.CreatedAt).ThenBy(e => e.Voucher.Code, StringComparer.Ordinal),
    ];

    private static bool Matches(Entry entry, Filter filter, DateOnly today, TimeZoneInfo zone)
    {
        var voucher = entry.Voucher;
        var issued = LocalTime.LocalDate(voucher.CreatedAt, zone);
        var state = StateOf(voucher, today);
        return (filter.From is not { } from || issued >= from)
            && (filter.Until is not { } until || issued <= until)
            && (filter.Status is not { } wanted || state == wanted)
            && (!filter.Expiring || (state == State.Active && voucher.ValidUntil is { } ends && ends <= today.AddDays(ExpiringDays)))
            && (!filter.NotSent || entry.Sent.Count == 0);
    }

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var today = LocalTime.LocalDate(header.CreatedAt, header.Zone);
        var pdf = new DocumentBuilder(DocumentFormat.A4Landscape, header);
        pdf.Title("Gutscheinliste");
        pdf.Paragraph($"{data.LocationName} · Stand {DocumentBuilder.DateTime(header.CreatedAt, header.Zone)} · {Describe(data.Filter)}", muted: true);
        if (data.Entries.Count == 0)
        {
            pdf.Paragraph("Keine Gutscheine für diese Auswahl.");
        }
        else
        {
            // Elf Spalten passen nur in der kleinen Schrift (8 pt); Gewichte so, dass Code, Art, Daten, Beträge und Versandvermerk nicht umbrechen
            // („WWWW-WWWW-WWWW“, „10 von 10 offen“, „Übergeben am 09.11.2026“) und die Namen den Rest bekommen. Währung steht im Spaltenkopf (Gestaltung: dokumente.md).
            pdf.Table(
                [
                    new("Code", 2.75), new("Art", 1.65), new("Ausgestellt", 1.4), new($"Ausgangswert\n{data.Currency}", 1.65, DocumentAlign.Right), new($"Restwert\n{data.Currency}", 1.2, DocumentAlign.Right),
                    new("Gültig bis", 1.3, DocumentAlign.Center), new("Status", 1.45), new("Käufer", 2.3), new("Empfänger", 2.3), new("Kanal", 0.85), new("Versand", 2.75),
                ],
                data.Entries.Select(e => Row(e, today, header.Zone)),
                compact: true);
        }

        pdf.Space(6);
        pdf.Rule();
        pdf.KeyValues(Totals(data, today), keepWithNext: true);
        pdf.Small("Offen sind die aktiven, nicht abgelaufenen Gutscheine; bei einem Paket zählt der anteilige Restwert. Abgelaufene stehen gesondert, bis sie ausgebucht sind.");
        return pdf.Build();
    }

    private static DocumentRow Row(Entry entry, DateOnly today, TimeZoneInfo zone)
    {
        var voucher = entry.Voucher;
        return DocumentRow.Of(
            voucher.Code,
            Kind(entry),
            DocumentBuilder.Date(voucher.CreatedAt, zone),
            DocumentBuilder.Money(voucher.InitialValue),
            DocumentBuilder.Money(voucher.Balance),
            voucher.ValidUntil is { } until ? DocumentBuilder.Date(until) : Dash,
            StateText(StateOf(voucher, today)),
            entry.Buyer ?? Dash,
            entry.Recipient ?? Dash,
            entry.Online ? "Online" : "Kassa",
            Dispatch(entry.Sent, zone));
    }

    private const string Dash = "–";

    private static string Kind(Entry entry)
    {
        var voucher = entry.Voucher;
        var treatment = entry.TreatmentName is { } name ? $"\n{name}" : "";
        if (voucher.Units is { } units)
        {
            return $"Paket {units} {(units == 1 ? "Behandlung" : "Behandlungen")}{treatment}\n{voucher.UnitsLeft ?? 0} von {units} offen";
        }

        return voucher.Kind == VoucherKind.SinglePurpose ? $"Behandlung{treatment}" : "Wert";
    }

    private static string StateText(State state) => state switch
    {
        State.Active => "aktiv",
        State.Redeemed => "aufgebraucht",
        State.Expired => "abgelaufen",
        _ => "storniert",
    };

    /// <summary>Je Weg der letzte Vermerk: Mail mit Datum, Adresse und Zahl der Sendungen, Druck, Übergabe; ohne Vermerk „–“.</summary>
    private static string Dispatch(IReadOnlyList<SentDocument> sent, TimeZoneInfo zone)
    {
        var lines = new List<string>();
        if (sent.Where(s => s.Channel == SentDocument.MailChannel).ToList() is { Count: > 0 } mails)
        {
            var last = mails[^1];
            lines.Add($"Mail am {DocumentBuilder.Date(last.SentAt, zone)}");
            lines.Add($"an {last.To}");
            if (mails.Count > 1)
            {
                lines.Add($"({mails.Count}× gesendet)");
            }
        }

        AddLast(lines, sent, SentDocument.PrintChannel, "Gedruckt am", zone);
        AddLast(lines, sent, SentDocument.HandoverChannel, "Übergeben am", zone);
        return lines.Count == 0 ? Dash : string.Join('\n', lines);
    }

    private static void AddLast(List<string> lines, IReadOnlyList<SentDocument> sent, string channel, string label, TimeZoneInfo zone)
    {
        if (sent.LastOrDefault(s => s.Channel == channel) is { } last)
        {
            lines.Add($"{label} {DocumentBuilder.Date(last.SentAt, zone)}");
        }
    }

    private static IEnumerable<(string, string)> Totals(Data data, DateOnly today)
    {
        var open = data.Entries.Where(e => StateOf(e.Voucher, today) == State.Active).ToList();
        var expired = data.Entries.Where(e => StateOf(e.Voucher, today) == State.Expired && e.Voucher.Balance > 0).ToList();
        yield return ("Offene Restwerte (Verbindlichkeit)", $"{data.Currency} {DocumentBuilder.Money(open.Sum(e => e.Voucher.Balance))} aus {open.Count} aktiv{(open.Count == 1 ? "em Gutschein" : "en Gutscheinen")}");
        if (expired.Count > 0)
        {
            yield return ("Abgelaufen, noch nicht ausgebucht", $"{data.Currency} {DocumentBuilder.Money(expired.Sum(e => e.Voucher.Balance))} aus {expired.Count} {(expired.Count == 1 ? "Gutschein" : "Gutscheinen")}");
        }
    }

    /// <summary>Die Auswahl in einem Satz für die Kopfzeile: „Ausgestellt 01.10.2026 bis 31.10.2026 · Status: aktiv · läuft in 30 Tagen ab“, ohne Auswahl „alle Gutscheine“.</summary>
    private static string Describe(Filter filter)
    {
        var parts = new List<string>();
        if (filter.From is not null || filter.Until is not null)
        {
            parts.Add(filter switch
            {
                { From: { } from, Until: { } until } => $"Ausgestellt {DocumentBuilder.Date(from)} bis {DocumentBuilder.Date(until)}",
                { From: { } from } => $"Ausgestellt ab {DocumentBuilder.Date(from)}",
                _ => $"Ausgestellt bis {DocumentBuilder.Date(filter.Until!.Value)}",
            });
        }

        if (filter.Status is { } status)
        {
            parts.Add($"Status: {StateText(status)}");
        }

        if (filter.Expiring)
        {
            parts.Add($"läuft in {ExpiringDays} Tagen ab");
        }

        if (filter.NotSent)
        {
            parts.Add("noch nicht versendet");
        }

        return parts.Count == 0 ? "alle Gutscheine" : string.Join(" · ", parts);
    }

    private static DateOnly? Date(string name, string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors[name] = ["Bitte ein Datum als jjjj-mm-tt angeben."];
        return null;
    }

    private static bool Switch(string name, string? value, Dictionary<string, string[]> errors)
    {
        switch (value)
        {
            case null or "" or "false":
                return false;
            case "true":
                return true;
            default:
                errors[name] = ["Bitte true oder false angeben."];
                return false;
        }
    }
}

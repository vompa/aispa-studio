using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Datenschutz-Auskunft nach Art. 15 DSGVO als PDF (PDF-Ausgaben C, ADR 0091): Stammdaten, Kontakt, Wünsche und Vermerke, alle Termine mit Status, Zahlungen, Gutscheine
/// auf den Namen, Online-Konto, die Gesundheitsangaben und der Verlauf der Änderungen. Die Anamnese steht nur drin, wenn die aufrufende Person sie in der App sehen darf
/// (<see cref="AccessRight.ViewHealthData"/>); sonst steht ein fester Satz, dass sie vorhanden ist und die Leitung Auskunft gibt.
/// </summary>
internal static class CustomerDisclosure
{
    public const string HealthWithheld = "Gesundheitsangaben: vorhanden, Auskunft durch die Leitung";

    public sealed record Visit(DateTimeOffset Start, string Treatment, string Location, AppointmentStatus Status, decimal Price, string Currency);

    public sealed record Payment(DateTimeOffset IssuedAt, string Number, string Kind, decimal Total, string Way, string Currency);

    public sealed record VoucherLine(string Code, string Kind, decimal InitialValue, decimal Balance, VoucherStatus Status, int? UnitsLeft, DateOnly? ValidUntil, string Currency);

    /// <summary>Gesundheitsangaben: ob ein Bogen vorliegt, und der entschlüsselte Bogen nur für Personen mit dem Recht (sonst null).</summary>
    public sealed record Health(bool Present, AnamnesisRecordDto? Record);

    public sealed record Data(
        Customer Customer,
        IReadOnlyList<string> OtherEmails,
        CustomerPreference? Preferences,
        IReadOnlyList<string> Flags,
        IReadOnlyList<Visit> Visits,
        IReadOnlyList<Payment> Payments,
        IReadOnlyList<VoucherLine> Vouchers,
        bool HasAccount,
        Health Health,
        IReadOnlyList<HistoryEntry> History);

    public static string FileName(Customer customer) => DocumentFiles.PdfName("Auskunft", customer.LegalName);

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var customer = data.Customer;
        var zone = header.Zone;
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Title("Auskunft über gespeicherte Daten");
        pdf.Paragraph($"Auskunft nach Art. 15 DSGVO für {customer.LegalName}. Sie zeigt alle Daten, die {header.SpaName} zu dieser Person gespeichert hat.", muted: true);
        pdf.Heading("Stammdaten");
        pdf.KeyValues(Master(customer, zone));
        pdf.Heading("Kontakt");
        pdf.KeyValues(Contact(data.Customer, data.OtherEmails));
        pdf.Heading("Wünsche und Vermerke");
        if (Wishes(data.Customer, data.Preferences, data.Flags) is { Count: > 0 } wishes)
        {
            pdf.KeyValues(wishes);
        }
        else
        {
            pdf.Paragraph("Keine gespeichert.");
        }

        Visits(pdf, data.Visits, zone);
        Payments(pdf, data.Payments, zone);
        Vouchers(pdf, data.Vouchers);
        pdf.Heading("Online-Konto");
        pdf.Paragraph(data.HasAccount ? "Ja, es gibt ein Online-Konto zu dieser Person." : "Nein, es gibt kein Online-Konto zu dieser Person.");
        HealthSection(pdf, data.Health, zone);
        pdf.Heading("Verlauf der Änderungen");
        if (data.History.Count == 0)
        {
            pdf.Paragraph("Keine Änderungen vermerkt.");
        }
        else
        {
            pdf.Table([new("Wann", 2), new("Änderung", 6)], data.History.Select(h => DocumentRow.Of(DocumentBuilder.DateTime(h.At, zone), Printable(h.Text))));
        }

        return pdf.Build();
    }

    private static List<(string, string)> Master(Customer customer, TimeZoneInfo zone)
    {
        var rows = new List<(string, string)> { ("Name", customer.LegalName) };
        Add(rows, "Rufname", customer.PreferredName);
        Add(rows, "Anrede", customer.Salutation switch
        {
            CustomerSalutation.Female => "Frau",
            CustomerSalutation.Male => "Herr",
            CustomerSalutation.Diverse => "Divers",
            _ => null,
        });
        Add(rows, "Pronomen", customer.Pronouns);
        Add(rows, "Geburtsdatum", customer.BirthDate is { } birth ? DocumentBuilder.Date(birth) : null);
        Add(rows, "Anschrift", Join('\n', customer.Street, Join(' ', customer.PostalCode, customer.City), customer.Country));
        Add(rows, "Sprache (Kürzel)", customer.Language);
        Add(rows, "Rechnungsempfänger", Join('\n', customer.BillingName, customer.BillingAddress, customer.BillingVatId is null ? null : $"UID {customer.BillingVatId}"));
        Add(rows, "Stammrabatt", customer.DiscountPercent is { } discount ? $"{DocumentBuilder.Number(discount)} %" : null);
        Add(rows, "Gespeichert seit", customer.CreatedAt == default ? null : DocumentBuilder.Date(customer.CreatedAt, zone));
        return rows;
    }

    internal static List<(string, string)> Contact(Customer customer, IReadOnlyList<string> otherEmails)
    {
        var rows = new List<(string, string)>();
        Add(rows, "E-Mail", Join('\n', [customer.Email, .. otherEmails]));
        Add(rows, "Telefon", customer.Phone);
        if (rows.Count == 0)
        {
            rows.Add(("Kontakt", "Keine Kontaktdaten gespeichert."));
        }

        return rows;
    }

    internal static List<(string, string)> Wishes(Customer customer, CustomerPreference? preferences, IReadOnlyList<string> flags)
    {
        var rows = new List<(string, string)>();
        Add(rows, "Notiz", customer.Note);
        Add(rows, "Vermerk der Rezeption", customer.ReceptionNote);
        if (preferences is { } p)
        {
            Add(rows, "Druck", p.Pressure);
            Add(rows, "Öl oder Duft", p.Scent);
            Add(rows, "Raumtemperatur", p.RoomTemperature);
            Add(rows, "Musik", p.Music);
            Add(rows, "Ruhe", p.PrefersQuiet switch { true => "Ja, lieber ruhig", false => "Nein", null => null });
            Add(rows, "Schwerpunkte", p.FocusAreas);
            Add(rows, "Aussparen", p.AvoidAreas);
            Add(rows, "Sonstiges", p.Other);
        }

        Add(rows, "Kennzeichen", Join('\n', [.. flags]));
        return rows;
    }

    private static void Visits(DocumentBuilder pdf, IReadOnlyList<Visit> visits, TimeZoneInfo zone)
    {
        pdf.Heading("Termine");
        if (visits.Count == 0)
        {
            pdf.Paragraph("Keine Termine.");
            return;
        }

        pdf.Table(
            [new("Wann", 2), new("Behandlung", 3), new("Standort", 2), new("Status", 2), new("Preis", 1.5, DocumentAlign.Right)],
            visits.OrderByDescending(v => v.Start).Select(v => DocumentRow.Of(
                DocumentBuilder.DateTime(v.Start, zone), v.Treatment, v.Location, StatusLabel(v.Status), $"{DocumentBuilder.Money(v.Price)} {v.Currency}")));
    }

    private static void Payments(DocumentBuilder pdf, IReadOnlyList<Payment> payments, TimeZoneInfo zone)
    {
        pdf.Heading("Zahlungen");
        if (payments.Count == 0)
        {
            pdf.Paragraph("Keine Zahlungen.");
            return;
        }

        pdf.Table(
            [new("Wann", 2), new("Beleg", 3), new("Zahlung", 2), new("Betrag", 1.5, DocumentAlign.Right)],
            payments.OrderByDescending(p => p.IssuedAt).Select(p => DocumentRow.Of(
                DocumentBuilder.DateTime(p.IssuedAt, zone), $"{p.Kind} Nr. {p.Number}", p.Way, $"{DocumentBuilder.Money(p.Total)} {p.Currency}")));
    }

    private static void Vouchers(DocumentBuilder pdf, IReadOnlyList<VoucherLine> vouchers)
    {
        pdf.Heading("Gutscheine auf den Namen");
        if (vouchers.Count == 0)
        {
            pdf.Paragraph("Keine Gutscheine.");
            return;
        }

        pdf.Table(
            [new("Code", 3), new("Art", 2), new("Status", 1.5), new("Gültig bis", 1.5), new("Rest", 2, DocumentAlign.Right)],
            vouchers.Select(v => DocumentRow.Of(
                v.Code, v.UnitsLeft is null ? "Gutschein" : "Paket", VoucherStatusLabel(v.Status), v.ValidUntil is { } until ? DocumentBuilder.Date(until) : "–",
                v.UnitsLeft is { } left ? $"{left} Behandlungen" : $"{DocumentBuilder.Money(v.Balance)} {v.Currency}")));
    }

    private static void HealthSection(DocumentBuilder pdf, Health health, TimeZoneInfo zone)
    {
        pdf.Heading("Gesundheitsangaben");
        if (!health.Present)
        {
            pdf.Paragraph("Keine Gesundheitsangaben gespeichert.");
        }
        else if (health.Record is not { } record)
        {
            pdf.Paragraph(HealthWithheld);
        }
        else
        {
            var questions = AnamnesisForm.Definition("de").Questions.ToDictionary(q => q.Key);
            pdf.Paragraph($"Anamnesebogen vom {DocumentBuilder.Date(record.SubmittedAt, zone)} ({record.Channel}), unterschrieben von {record.Signature}.", muted: true);
            pdf.Table(
                [new("Frage", 5), new("Antwort", 3)],
                record.Answers.Select(a => DocumentRow.Of(
                    questions.TryGetValue(a.Key, out var question) ? question.Label : a.Key,
                    string.Join(" – ", new[] { a.Yes switch { true => "Ja", false => "Nein", null => null }, a.Details }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } text ? text : "–")));
        }
    }

    /// <summary>Der Pfeil im Verlauf („alt → neu“) fehlt in der Schrift der Dokumente (WinAnsi); er steht als „nach“.</summary>
    private static string Printable(string text) => text.Replace("→", "nach", StringComparison.Ordinal);

    public static string StatusLabel(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Booked => "Gebucht",
        AppointmentStatus.CheckedIn => "Eingecheckt",
        AppointmentStatus.Completed => "Erledigt",
        AppointmentStatus.NoShow => "Nicht erschienen",
        _ => "Storniert",
    };

    public static string FlagLabel(CustomerFlagKind kind) => kind switch
    {
        CustomerFlagKind.MoreTime => "Braucht mehr Zeit",
        CustomerFlagKind.InformManagement => "Leitung informieren",
        CustomerFlagKind.NotWithMe => "Nicht bei einer bestimmten Person",
        _ => "Nicht mehr buchen",
    };

    private static string VoucherStatusLabel(VoucherStatus status) => status switch
    {
        VoucherStatus.Active => "Gültig",
        VoucherStatus.Redeemed => "Eingelöst",
        _ => "Ungültig",
    };

    internal static void Add(List<(string, string)> rows, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add((key, value.Trim()));
        }
    }

    internal static string? Join(char separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim())) is { Length: > 0 } text ? text : null;
}

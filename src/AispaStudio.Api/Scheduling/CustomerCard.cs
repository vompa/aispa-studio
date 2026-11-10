using AispaStudio.Api.Documents;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Stammblatt einer Kundin oder eines Kunden als PDF (PDF-Ausgaben E, ADR 0092): Kontakt und Anschrift, Wünsche und Vermerke, die nächsten Termine und die letzten fünf Besuche.
/// Gesundheitsdaten stehen nie darin; gibt es einen Anamnesebogen, steht nur der feste Satz <see cref="CustomerDisclosure.HealthWithheld"/>.
/// </summary>
internal static class CustomerCard
{
    /// <summary>So viele Termine und Besuche zeigt das Stammblatt höchstens.</summary>
    public const int Shown = 5;

    public sealed record Visit(DateTimeOffset Start, string Treatment, string Person, string Location, AppointmentStatus Status);

    public sealed record Data(
        Customer Customer,
        IReadOnlyList<string> OtherEmails,
        CustomerPreference? Preferences,
        IReadOnlyList<string> Flags,
        IReadOnlyList<Visit> Upcoming,
        IReadOnlyList<Visit> Past,
        bool HasHealthData);

    public static string FileName(Customer customer) => DocumentFiles.PdfName("Stammblatt", customer.LegalName);

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var customer = data.Customer;
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, header);
        pdf.Title($"Stammblatt {customer.LegalName}");
        pdf.Heading("Kontakt");
        var contact = CustomerDisclosure.Contact(customer, data.OtherEmails);
        CustomerDisclosure.Add(contact, "Anschrift", CustomerDisclosure.Join('\n', customer.Street, CustomerDisclosure.Join(' ', customer.PostalCode, customer.City), customer.Country));
        pdf.KeyValues(contact);
        pdf.Heading("Wünsche und Vermerke");
        if (CustomerDisclosure.Wishes(customer, data.Preferences, data.Flags) is { Count: > 0 } wishes)
        {
            pdf.KeyValues(wishes);
        }
        else
        {
            pdf.Paragraph("Keine gespeichert.");
        }

        if (data.HasHealthData)
        {
            pdf.Paragraph(CustomerDisclosure.HealthWithheld, muted: true);
        }

        pdf.Heading("Nächste Termine");
        Table(pdf, data.Upcoming, header.Zone, $"{data.Upcoming.Count} {(data.Upcoming.Count == 1 ? "Termin" : "Termine")} geplant", "Keine Termine geplant.");
        pdf.Heading("Letzte Besuche");
        Table(pdf, data.Past, header.Zone, null, "Noch keine Besuche.");
        return pdf.Build();
    }

    private static void Table(DocumentBuilder pdf, IReadOnlyList<Visit> visits, TimeZoneInfo zone, string? summary, string none)
    {
        if (visits.Count == 0)
        {
            pdf.Paragraph(none);
            return;
        }

        if (summary is not null)
        {
            pdf.Paragraph(summary, muted: true);
        }

        pdf.Table(
            [new("Wann", 2), new("Behandlung", 3), new("Mit", 1.5), new("Standort", 2), new("Status", 1.5)],
            visits.Select(v => DocumentRow.Of(DocumentBuilder.DateTime(v.Start, zone), v.Treatment, v.Person, v.Location, CustomerDisclosure.StatusLabel(v.Status))));
    }
}

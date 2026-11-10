using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Kundenliste als Excel, CSV, Portal-CSV und vCard sowie die Newsletter-Liste (Export/Import, Aufgabe B): dieselbe Suche wie die Kundenliste, nie Gesundheitsdaten, Wünsche,
/// Rabatt oder Rechnungsdaten; der Vermerk der Rezeption nur für Inhaber und Manager. Jede Datei steht im Export-Protokoll.
/// </summary>
internal static class CustomerExports
{
    private const string LeadershipOnly = "Die Newsletter-Liste sehen nur Inhaber und Manager.";

    public static IEndpointRouteBuilder MapCustomerExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/export", async Task<Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>> (
                string? format, string? search, string? list, AispaDbContext db, AccessService accessService, ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                if (ExportFiles.Parse(format) is not { } kind)
                {
                    return Problem("format", ExportFiles.UnknownFormatMessage);
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var newsletter = string.Equals(list, "newsletter", StringComparison.OrdinalIgnoreCase);
                if (newsletter && !access.IsOwnerLevel)
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(LeadershipOnly);
                }

                if (newsletter && kind is not (ExportFormat.Csv or ExportFormat.Xlsx))
                {
                    return Problem("format", "Die Newsletter-Liste gibt es als CSV oder Excel.");
                }

                if (kind == ExportFormat.Ics)
                {
                    return Problem("format", "Die Kundenliste gibt es als Excel, CSV, Portal-CSV oder vCard.");
                }

                var query = CustomerEndpoints.Search(db.Customers.AsNoTracking().Where(c => c.DeletedAt == null), db, search);
                if (newsletter)
                {
                    query = query.Where(c => db.MarketingConsents.Any(m => m.CustomerId == c.Id && m.State == MarketingConsentState.Confirmed));
                }

                if (await query.CountAsync(ct) > TableExport.MaxRows)
                {
                    return Problem("format", TableExport.TooLargeMessage);
                }

                var customers = await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync(ct);
                var zone = await ZoneAsync(db, ct);
                var today = LocalTime.LocalDate(time.GetUtcNow(), zone);
                var filter = string.IsNullOrWhiteSpace(search) ? null : $"Suche „{search.Trim()}“";
                var consents = await db.MarketingConsents.AsNoTracking().ToDictionaryAsync(m => m.CustomerId, ct);

                if (newsletter)
                {
                    var table = NewsletterTable(customers, consents);
                    await log.RecordAsync("Newsletter-Liste", kind, table.Rows.Count, filter, ct);
                    return File(table, kind, ExportFiles.Name("Newsletter", kind, today));
                }

                switch (kind)
                {
                    case ExportFormat.Vcf:
                        var cards = customers.Select(c => Contact(c, access.IsOwnerLevel, time.GetUtcNow())).ToList();
                        await log.RecordAsync("Kundenliste", kind, cards.Count, filter, ct);
                        return TypedResults.File(VCardWriter.Write(cards), ExportFiles.ContentType(kind), ExportFiles.Name("Kontakte", kind, today));
                    case ExportFormat.Portal:
                        var portal = PortalTable(customers, consents);
                        await log.RecordAsync("Kundenliste", kind, portal.Rows.Count, filter, ct);
                        return TypedResults.File(TableExport.ToCsv(portal, ','), ExportFiles.ContentType(kind), ExportFiles.Name("Kunden Portal", kind, today));
                    default:
                        var visits = await VisitsAsync(db, ct);
                        var customersTable = CustomerTable(customers, consents, visits, zone, access.IsOwnerLevel);
                        await log.RecordAsync("Kundenliste", kind, customersTable.Rows.Count, filter, ct);
                        return File(customersTable, kind, ExportFiles.Name("Kunden", kind, today));
                }
            })
            .WithName("ExportCustomers")
            .WithSummary("Exportiert die gefilterte Kundenliste als Excel, CSV, Portal-CSV (englische Spalten, Komma) oder vCard; mit list=newsletter nur Gäste mit bestätigter "
                + "Werbe-Einwilligung (nur Inhaber und Manager). Nie Gesundheitsdaten, Wünsche, Rabatt oder Rechnungsdaten; der Vermerk nur für die Leitung. Höchstens 50 000 Zeilen.");

        group.MapGet("/{customerId:guid}/vcard", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid customerId, AispaDbContext db, AccessService accessService, ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.DeletedAt == null, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                await log.RecordAsync("Kontakt", ExportFormat.Vcf, 1, customer.LegalName, ct);
                return TypedResults.File(VCardWriter.Write([Contact(customer, access.IsOwnerLevel, time.GetUtcNow())]), ExportFiles.ContentType(ExportFormat.Vcf),
                    ExportFiles.Name($"Kontakt {customer.LastName}", ExportFormat.Vcf));
            })
            .WithName("ExportCustomerVCard")
            .WithSummary("Ein Gast als vCard 4.0 für Outlook, Apple und Google; der Vermerk der Rezeption steht nur für Inhaber und Manager in NOTE.");

        return routes;
    }

    /// <summary>„Ja“ (bestätigt), „Nein“ (abgelehnt oder widerrufen) oder „offen“ (nicht gefragt oder Bestätigung per Mail ausstehend).</summary>
    private static string ConsentWord(MarketingConsent? consent) => consent?.State switch
    {
        MarketingConsentState.Confirmed => "Ja",
        MarketingConsentState.Refused or MarketingConsentState.Withdrawn => "Nein",
        _ => "offen",
    };

    /// <summary>Seit wann „Ja“ oder „Nein“ gilt; bei „offen“ leer.</summary>
    private static DateTimeOffset? ConsentDay(MarketingConsent? consent) => consent?.State switch
    {
        MarketingConsentState.Confirmed => consent.ConfirmedAt,
        MarketingConsentState.Refused or MarketingConsentState.Withdrawn => consent.WithdrawnAt,
        _ => null,
    };

    private static ValidationProblem Problem(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static FileContentHttpResult File(ExportTable table, ExportFormat format, string name) =>
        TypedResults.File(format == ExportFormat.Xlsx ? TableExport.ToExcel(table) : TableExport.ToCsv(table), ExportFiles.ContentType(format), name);

    /// <summary>Zeitzone des ersten Standorts; ohne Standort UTC.</summary>
    private static async Task<TimeZoneInfo> ZoneAsync(AispaDbContext db, CancellationToken ct) =>
        await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is { } id && LocalTime.IsKnownZone(id)
            ? LocalTime.Zone(id)
            : TimeZoneInfo.Utc;

    /// <summary>Abgeschlossene Besuche je Gast: Anzahl und letzter Beginn.</summary>
    private static async Task<Dictionary<Guid, (int Count, DateTimeOffset Last)>> VisitsAsync(AispaDbContext db, CancellationToken ct) =>
        (await db.Appointments.AsNoTracking().Where(a => a.Status == AppointmentStatus.Completed)
            .GroupBy(a => a.CustomerId).Select(g => new { g.Key, Count = g.Count(), Last = g.Max(a => a.Start) }).ToListAsync(ct))
        .ToDictionary(v => v.Key, v => (v.Count, v.Last));

    private static ExportTable CustomerTable(List<Customer> customers, Dictionary<Guid, MarketingConsent> consents,
        Dictionary<Guid, (int Count, DateTimeOffset Last)> visits, TimeZoneInfo zone, bool leadership)
    {
        var columns = new List<ExportColumn>
        {
            new("Anrede", ColumnKind.Text), new("Titel", ColumnKind.Text), new("Vorname", ColumnKind.Text), new("Nachname", ColumnKind.Text), new("Rufname", ColumnKind.Text),
            new("E-Mail", ColumnKind.Text), new("Telefon", ColumnKind.Text), new("Geburtstag", ColumnKind.Date), new("Sprache", ColumnKind.Text),
            new("Straße", ColumnKind.Text), new("PLZ", ColumnKind.Text), new("Ort", ColumnKind.Text), new("Land", ColumnKind.Text),
            new("Werbe-Einwilligung", ColumnKind.Text), new("Einwilligung am", ColumnKind.Date), new("Angelegt am", ColumnKind.Date), new("Kanal", ColumnKind.Text),
            new("Letzter Besuch", ColumnKind.Date), new("Besuche", ColumnKind.Number),
        };
        if (leadership)
        {
            columns.Add(new("Vermerk", ColumnKind.Text));
        }

        var rows = customers.Select(c =>
        {
            var consent = consents.GetValueOrDefault(c.Id);
            var visit = visits.TryGetValue(c.Id, out var v) ? v : default;
            var row = new List<object?>
            {
                Salutation(c.Salutation), Titles(c), c.FirstName, c.LastName, c.PreferredName, c.Email, c.Phone, c.BirthDate, Language(c.Language),
                c.Street, c.PostalCode, c.City, c.Country,
                ConsentWord(consent), ConsentDay(consent) is { } at ? LocalTime.LocalDate(at, zone) : null, LocalTime.LocalDate(c.CreatedAt, zone), Channel(c.CreatedFromChannel),
                visit.Count > 0 ? LocalTime.LocalDate(visit.Last, zone) : null, visit.Count,
            };
            if (leadership)
            {
                row.Add(c.ReceptionNote);
            }

            return row.ToArray();
        }).ToList();
        return new("Kunden", columns, rows);
    }

    /// <summary>Englische Spalten, wie Buchungsportale sie beim Import zuordnen; keine Vorlage eines Portals ist öffentlich belegt, deshalb allgemeine Köpfe und ISO-Datum.</summary>
    private static ExportTable PortalTable(List<Customer> customers, Dictionary<Guid, MarketingConsent> consents) => new("Customers",
    [
        new("First name", ColumnKind.Text), new("Last name", ColumnKind.Text), new("Email", ColumnKind.Text), new("Mobile", ColumnKind.Text),
        new("Date of birth", ColumnKind.Text), new("Notes", ColumnKind.Text), new("Marketing consent", ColumnKind.Text),
    ],
    customers.Select(c => new object?[]
    {
        c.FirstName, c.LastName, c.Email, c.Phone, c.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "",
        consents.GetValueOrDefault(c.Id)?.State == MarketingConsentState.Confirmed ? "yes" : "no",
    }).ToList());

    /// <summary>Für Brevo und Mailchimp: Adresse der Einwilligung (nicht die aktuelle Kundenadresse), Name, Sprache, Anrede.</summary>
    private static ExportTable NewsletterTable(List<Customer> customers, Dictionary<Guid, MarketingConsent> consents) => new("Newsletter",
        [new("E-Mail", ColumnKind.Text), new("Vorname", ColumnKind.Text), new("Nachname", ColumnKind.Text), new("Sprache", ColumnKind.Text), new("Anrede", ColumnKind.Text)],
        customers.Select(c => new object?[] { consents[c.Id].Email, c.FirstName, c.LastName, Language(c.Language), Salutation(c.Salutation) }).ToList());

    private static VCardContact Contact(Customer c, bool leadership, DateTimeOffset revision) =>
        new(c.Id, c.TitlesBefore, c.FirstName, c.LastName, c.PreferredName, c.TitlesAfter, c.Email, c.Phone, c.BirthDate, c.Street, c.PostalCode, c.City, c.Country,
            leadership ? c.ReceptionNote : null, revision);

    private static string Salutation(CustomerSalutation salutation) => salutation switch
    {
        CustomerSalutation.Female => "Frau",
        CustomerSalutation.Male => "Herr",
        _ => "",
    };

    private static string? Titles(Customer c) => string.Join(' ', new[] { c.TitlesBefore, c.TitlesAfter }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } titles ? titles : null;

    private static string Language(string? code) => GuestLanguages.Name(code) ?? "Deutsch";

    private static string Channel(BookingChannel? channel) => channel switch
    {
        BookingChannel.Email => "E-Mail",
        BookingChannel.Treatwell => "Treatwell",
        BookingChannel.Sms => "SMS",
        BookingChannel.WhatsApp => "WhatsApp",
        BookingChannel.Phone => "Telefon",
        _ => "Rezeption",
    };
}

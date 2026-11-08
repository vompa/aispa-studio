using AispaStudio.Api.BusinessInfo;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Zehn erfundene Einträge der Betriebsinformationen für das Beispiel-Spa (ADR 0074): zwei Versicherungen (die Betriebshaftpflicht nur für die Leitung), Installateur und
/// Saunatechniker (nur Salzburg), Wäscherei als Lieferant, Buchhaltung, Lohnverrechnung und Steuerberatung (nur Leitung), ein Notdienst für Wasserschäden und eine
/// Vertrauensperson (nur Leitung). Firmen, Namen und Nummern sind frei erfunden, die Nummern im Beispielformat +43 1 234 56 NN. Behörden und Elektriker fehlen mit Absicht,
/// damit der Assistent auch „Dazu ist nichts hinterlegt“ zeigen kann.
/// </summary>
internal static class DemoBusinessContacts
{
    public const int Count = 10;

    public static async Task AddAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var salzburg = await db.Locations.AsNoTracking().OrderBy(l => l.Name).FirstAsync(l => l.Name.StartsWith("Salzburg"), ct);

        BusinessContact New(BusinessContactCategory category, string designation, string company, string? person, string phone, string? responsibility,
            BusinessContactVisibility visibility = BusinessContactVisibility.Team, Guid? onlyAt = null)
        {
            var contact = new BusinessContact
            {
                Category = category,
                Designation = designation,
                Company = company,
                ContactPerson = person,
                Phone = phone,
                Responsibility = responsibility,
                AllLocations = onlyAt is null,
                Visibility = visibility,
                CreatedAt = now.AddDays(-30),
            };
            if (onlyAt is { } locationId)
            {
                contact.Locations.Add(new BusinessContactLocation { BusinessContactId = contact.Id, LocationId = locationId });
            }

            return contact;
        }

        var liability = New(BusinessContactCategory.Insurance, "Betriebshaftpflicht", "Alpenschutz Versicherungs-AG", "Elisabeth Polz", "+43 1 234 56 01",
            "Haftpflichtschäden, Schäden an Gästen", BusinessContactVisibility.Management);
        liability.Insurer = "Alpenschutz Versicherungs-AG";
        liability.PolicyNumber = "POL-2026-0042";
        liability.Coverage = "Betriebshaftpflicht bis 3 Mio. EUR, Selbstbehalt 500 EUR";
        liability.ClaimsHotline = "+43 1 234 56 90";
        liability.Email = "schaden@alpenschutz.example";
        liability.Notes = "Schaden innerhalb von 48 Stunden melden.";

        var inventory = New(BusinessContactCategory.Insurance, "Inventar- und Einbruchversicherung", "Donau Sicherheit Versicherung", null, "+43 1 234 56 02", "Einbruch, Brand, Leitungswasserschäden");
        inventory.Insurer = "Donau Sicherheit Versicherung";
        inventory.PolicyNumber = "INV-77-1203";
        inventory.ClaimsHotline = "+43 1 234 56 91";

        var plumber = New(BusinessContactCategory.Craftsman, "Installateur", "Haustechnik Sommer GmbH", "Franz Sommer", "+43 1 234 56 10", "Wasserschaden, Heizung, Warmwasser");
        plumber.Mobile = "+43 664 234 56 10";
        plumber.Email = "office@haustechnik-sommer.example";
        plumber.CustomerNumber = "K-2041";

        var sauna = New(BusinessContactCategory.Craftsman, "Saunatechniker", "Saunabau Alpin GmbH", "Gerhard Kogler", "+43 662 234 56 11", "Saunaofen, Dampfbad, Lüftung",
            onlyAt: salzburg.Id);
        sauna.Email = "service@saunabau-alpin.example";

        var laundry = New(BusinessContactCategory.Supplier, "Wäscherei", "Textilservice Frisch OG", "Monika Frisch", "+43 1 234 56 20", "Handtücher, Bademäntel; Lieferung dienstags");
        laundry.CustomerNumber = "WS-1042";
        laundry.Email = "bestellung@textilservice-frisch.example";

        var accounting = New(BusinessContactCategory.Accounting, "Buchhaltung", "Berger & Partner Buchhaltung", "Bernd Berger", "+43 1 234 56 30", "Belege, Monatsabschluss, UVA",
            BusinessContactVisibility.Management);
        accounting.Email = "buchhaltung@berger-partner.example";

        var payroll = New(BusinessContactCategory.Payroll, "Lohnverrechnung", "Lohnbüro Hofer", "Petra Hofer", "+43 1 234 56 31", "Gehälter, Lohnzettel, An- und Abmeldungen",
            BusinessContactVisibility.Management);
        payroll.Email = "lohn@lohnbuero-hofer.example";

        var tax = New(BusinessContactCategory.TaxAdvisor, "Steuerberatung", "Steuerberatung Dr. Auer & Kollegen", "Dr. Sabine Auer", "+43 1 234 56 32", "Jahresabschluss, Steuererklärung, Betriebsprüfung",
            BusinessContactVisibility.Management);
        tax.Email = "kanzlei@auer-steuer.example";

        var emergency = New(BusinessContactCategory.Emergency, "Wasser-Notdienst", "Rohrbruch Express", null, "+43 1 234 56 99", "Wasserschaden, Rohrbruch, Rückstau");
        emergency.Notes = "Rund um die Uhr erreichbar.";

        var trusted = New(BusinessContactCategory.TrustedPerson, "Vertrauensperson", "Unternehmensberatung Kerschbaumer", "Mag. Helga Kerschbaumer", "+43 1 234 56 40",
            "Konflikte im Team, Beschwerden von Gästen", BusinessContactVisibility.Management);
        trusted.Notes = "Außenstehende Ansprechperson, auf Wunsch vertraulich.";

        db.BusinessContacts.AddRange(liability, inventory, plumber, sauna, laundry, accounting, payroll, tax, emergency, trusted);
        await db.SaveChangesAsync(ct);
    }
}

using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Design;

/// <summary>
/// Beispieldaten der Kundenliste für die Vorschau (Namen wie im Beispiel-Spa): eine Stammkundin mit Titel, Adresse und Terminen,
/// dazu Grenzfälle für das Layout (ohne Mailadresse, langer Doppelname). Termine liegen relativ zu heute, damit die Karte nie veraltet.
/// </summary>
internal static class DesignCustomers
{
    private static readonly LocationDto Vienna = new(Guid.NewGuid(), "Wien Innere Stadt", "Europe/Vienna", "EUR", []);
    private static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [], []);
    private static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [], []);

    public static readonly CustomerDto[] All =
    [
        new(Guid.NewGuid(), "Brigitte", "Müller", "b.mueller@example.at", "+43 664 123 45 67", new(1968, 10, 21), "Stammkundin, mag Lavendelöl",
            Salutation: CustomerSalutation.Female, TitlesBefore: "Mag.", Address: new CustomerAddress("Neubaugasse 12/4", "1070", "Wien", "AT")),
        new(Guid.NewGuid(), "Bernhard", "Müllner", null, "+43 676 555 01 02", null, null, Salutation: CustomerSalutation.Male),
        new(Guid.NewGuid(), "Johanna-Theresia", "Schwarzenberger-Hohenwart", "j.schwarzenberger@example.at", null, null, null,
            Salutation: CustomerSalutation.Female),
        new(Guid.NewGuid(), "Selin", "Yılmaz", "selin.yilmaz@example.com", "+43 699 100 20 30", new(1991, 4, 2), null, Language: "en"),
        new(Guid.NewGuid(), "Thomas", "Gruber", "t.gruber@example.at", "+43 650 987 65 43", null, "Kommt meist mit Partnerin"),
    ];

    /// <summary>Alle Antworten, die das Öffnen der Kundenkarte von <paramref name="customer"/> abruft.</summary>
    public static DesignApi Api(CustomerDto customer, DateTimeOffset now)
    {
        var id = customer.Id;
        return new DesignApi()
            .On($"/api/customers/{id}/appointments", Appointments(customer, now))
            .On($"/api/customers/{id}/insights", new CustomerInsights(
                12, 6, DateOnly.FromDateTime(now.AddDays(-21).Date), 35, 2, "6 Besuche in zwölf Monaten", [], [],
                [new(Lena.Id, Lena.DisplayName, 8, null, null), new(Marco.Id, Marco.DisplayName, 4, null, null)], null, [], true, null))
            .On($"/api/reports/customers/{id}/preferences", new CustomerPreferences("mittel", "Lavendel", "warm", null, true, "Nacken", null, null))
            .On($"/api/customers/{id}/flags", new List<CustomerFlagDto>())
            .On($"/api/customers/{id}/flags/suggestions", new List<CustomerFlagSuggestion>())
            .On($"/api/customers/{id}/emails", new List<CustomerEmailDto>())
            .On($"/api/customers/{id}/marketing-consent", new MarketingConsentDto(MarketingConsentState.None, customer.Email, null, null, null, null, null))
            .On($"/api/customers/{id}/booking-preferences/suggestion", new CustomerPreferenceSuggestion(0, null, null, 0, null, 0, null, null, null))
            .On("/api/employees", new List<EmployeeDto> { Lena, Marco })
            .On("/api/locations", new List<LocationDto> { Vienna });
    }

    private static List<AppointmentDto> Appointments(CustomerDto customer, DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.Date, now.Offset);
        return
        [
            Visit(customer, today.AddDays(-42).AddHours(10), "Klassische Massage", 50, 89m, Lena, AppointmentStatus.Completed),
            Visit(customer, today.AddDays(-21).AddHours(14), "Hot-Stone-Massage", 80, 129m, Lena, AppointmentStatus.Completed),
            Visit(customer, today.AddDays(-7).AddHours(16), "Gesichtsbehandlung Classic", 60, 95m, Marco, AppointmentStatus.Cancelled),
            Visit(customer, today.AddDays(9).AddHours(11), "Aromaöl-Massage", 60, 98m, Lena, AppointmentStatus.Booked),
        ];
    }

    private static AppointmentDto Visit(CustomerDto customer, DateTimeOffset start, string treatment, int minutes, decimal price,
        EmployeeDto employee, AppointmentStatus status)
    {
        var end = start.AddMinutes(minutes);
        return new(Guid.NewGuid(), Vienna.Id, Guid.NewGuid(), treatment, customer.Id, customer.InHouseName, employee.Id, Guid.NewGuid(), null,
            start, end, start, end, status, price, null, start.AddDays(-3));
    }
}

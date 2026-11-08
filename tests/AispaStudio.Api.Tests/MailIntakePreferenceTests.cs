using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Terminannahme: ohne genannte Person gilt die bevorzugte des Gasts.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task Ohne_Wunschperson_bucht_die_Terminannahme_bei_der_bevorzugten_Person()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var preferred = await spa.Spa.CreateEmployeeAsync($"Bea {spa.Code}", [spa.Spa.Massage.Id], spa.Location.Id, Ct);
        var customer = await TestSpa.ReadAsync<CustomerDto>(await spa.Spa.Client.PostAsJsonAsync("/api/customers",
            new CustomerInput("Eva", "Sommer", spa.Guest, null, null, null), TestSpa.Json, Ct), Ct);
        // Bea hat schon mehr zu tun; ohne Vorliebe würde der Planer Anna nehmen.
        await TestSpa.ReadAsync<AppointmentDto>(await spa.Spa.BookAsync(TestSpa.At(14), Ct, preferred.Id, treatment: spa.Treatment, location: spa.Location.Id), Ct);
        await spa.Spa.PutAsync<CustomerDto>($"/api/customers/{customer.Id}/booking-preferences", new CustomerBookingPreferences(preferred.Id), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Terminanfrage",
            $"Hallo,\nich hätte gerne eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.\n\nLiebe Grüße\nEva Sommer", Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        var appointment = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct);
        Assert.Equal(preferred.Id, appointment.EmployeeId);
        Assert.Equal(customer.Id, appointment.CustomerId);
    }

    [Fact]
    public async Task Bei_Nicht_mehr_buchen_entscheidet_die_Rezeption_und_ausgeschlossene_Personen_kommen_nicht_vor()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await TestSpa.ReadAsync<CustomerDto>(await spa.Spa.Client.PostAsJsonAsync("/api/customers",
            new CustomerInput("Eva", "Sommer", spa.Guest, null, null, null), TestSpa.Json, Ct), Ct);
        var flags = $"/api/customers/{customer.Id}/flags";
        var other = await spa.Spa.CreateEmployeeAsync($"Bea {spa.Code}", [spa.Spa.Massage.Id], spa.Location.Id, Ct);
        await spa.Spa.PostAsync<List<CustomerFlagDto>>(flags, new CustomerFlagInput(CustomerFlagKind.NotWithMe, other.Id), Ct);

        var booked = await spa.ReceiveAsync(spa.Guest, "Terminanfrage",
            $"Hallo,\nich hätte gerne eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name} bei Bea {spa.Code}.\n\nLiebe Grüße\nEva Sommer", Ct);
        Assert.Equal(InboundMailStatus.Booked, booked.Status);
        Assert.NotEqual(other.Id, (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.AppointmentId}", Ct)).EmployeeId);

        await spa.Spa.PostAsync<List<CustomerFlagDto>>(flags, new CustomerFlagInput(CustomerFlagKind.DoNotBook, null, "Mehrfach nicht erschienen"), Ct);
        var held = await spa.ReceiveAsync(spa.Guest, "Noch ein Termin",
            $"Hallo,\nich hätte gerne eine {spa.Treatment.Name} am 02.03.2027 um 10 Uhr in {spa.Location.Name}.\n\nLiebe Grüße\nEva Sommer", Ct);
        Assert.Equal(InboundMailStatus.NeedsReview, held.Status);
        Assert.Null(held.AppointmentId);
        Assert.Empty(held.Proposals);
        Assert.Contains("Nicht mehr buchen", held.Note, StringComparison.Ordinal);
    }
}

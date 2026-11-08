using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Kennzeichen eines Gastes: wer was setzen darf und was sie beim Planen bewirken.</summary>
public sealed class CustomerFlagTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Braucht_mehr_Zeit_gibt_zehn_Minuten_Puffer_und_steht_im_Kalender()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/flags";
        var without = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var flags = await spa.PostAsync<List<CustomerFlagDto>>(path, new CustomerFlagInput(CustomerFlagKind.MoreTime), Ct);
        var with = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id);

        Assert.Equal(CustomerFlagKind.MoreTime, Assert.Single(flags).Kind);
        Assert.Equal(TimeSpan.FromMinutes(10), (with.BlockedUntil - with.End) - (without.BlockedUntil - without.End));
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal([CustomerFlagKind.MoreTime], Assert.Single(day.Guests!).Flags);

        // Zweimal setzen ergibt ein Kennzeichen; aufheben nimmt den Puffer wieder weg.
        Assert.Single(await spa.PostAsync<List<CustomerFlagDto>>(path, new CustomerFlagInput(CustomerFlagKind.MoreTime), Ct));
        var removed = await spa.Client.DeleteAsync($"{path}/{flags[0].Id}", Ct);
        Assert.Empty(await TestSpa.ReadAsync<List<CustomerFlagDto>>(removed, Ct));
        var again = await spa.BookOkAsync(TestSpa.At(15), Ct, spa.Anna.Id);
        Assert.Equal(without.BlockedUntil - without.End, again.BlockedUntil - again.End);
    }

    [Fact]
    public async Task Nicht_bei_mir_setzt_die_Therapeutin_selbst_und_der_Planer_nimmt_jemand_anderen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/flags";
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        using var ben = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Ben.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var forOther = await ben.PostAsJsonAsync(path, new CustomerFlagInput(CustomerFlagKind.NotWithMe, spa.Anna.Id), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forOther.StatusCode);
        var set = await anna.PostAsJsonAsync(path, new CustomerFlagInput(CustomerFlagKind.NotWithMe, null, "Chemie passt nicht"), TestSpa.Json, Ct);
        var flag = Assert.Single(await TestSpa.ReadAsync<List<CustomerFlagDto>>(set, Ct));
        Assert.Equal((spa.Anna.Id, "Anna", "Chemie passt nicht", true), (flag.EmployeeId, flag.EmployeeName, flag.Reason, flag.MayRemove));

        // Den Grund sehen nur die Person selbst und die Leitung.
        var atDesk = Assert.Single(await TestSpa.ReadAsync<List<CustomerFlagDto>>(await reception.GetAsync(path, Ct), Ct));
        Assert.Equal((null, false), (atDesk.Reason, atDesk.MayRemove));
        Assert.Equal("Chemie passt nicht", Assert.Single(await spa.GetAsync<List<CustomerFlagDto>>(path, Ct)).Reason);

        // Ausdrücklich Anna: abgelehnt; ohne Wunsch: Ben. Freie Zeiten für diesen Gast nur bei Ben.
        var explicitAnna = await spa.Client.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(10), spa.Anna.Id, null, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, explicitAnna.StatusCode);
        Assert.Contains("Nicht bei mir", await explicitAnna.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(spa.Ben.Id, (await spa.BookOkAsync(TestSpa.At(10), Ct)).EmployeeId);
        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}&customerId={spa.Customer.Id}", Ct);
        Assert.All(slots, s => Assert.Equal(spa.Ben.Id, s.EmployeeId));
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Null(Assert.Single(day.Guests!).Flags);

        Assert.Equal(HttpStatusCode.Forbidden, (await ben.DeleteAsync($"{path}/{flag.Id}", Ct)).StatusCode);
        Assert.Empty(await TestSpa.ReadAsync<List<CustomerFlagDto>>(await anna.DeleteAsync($"{path}/{flag.Id}", Ct), Ct));
    }

    [Fact]
    public async Task Nicht_mehr_buchen_und_Leitung_informieren_nur_durch_die_Leitung_mit_Grund()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/flags";
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await reception.PostAsJsonAsync(path, new CustomerFlagInput(CustomerFlagKind.DoNotBook, null, "offene Rechnungen"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reception.PostAsJsonAsync(path, new CustomerFlagInput(CustomerFlagKind.InformManagement), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await spa.Client.PostAsJsonAsync(path, new CustomerFlagInput(CustomerFlagKind.DoNotBook), TestSpa.Json, Ct)).StatusCode);

        await spa.PostAsync<List<CustomerFlagDto>>(path, new CustomerFlagInput(CustomerFlagKind.DoNotBook, null, "Zwei offene Rechnungen"), Ct);
        var flags = await spa.PostAsync<List<CustomerFlagDto>>(path, new CustomerFlagInput(CustomerFlagKind.InformManagement), Ct);
        Assert.Equal([CustomerFlagKind.InformManagement, CustomerFlagKind.DoNotBook], flags.Select(f => f.Kind));

        // Die Rezeption sieht den Grund, darf aber nicht aufheben; buchen kann sie trotzdem.
        var atDesk = await TestSpa.ReadAsync<List<CustomerFlagDto>>(await reception.GetAsync(path, Ct), Ct);
        Assert.Equal(("Zwei offene Rechnungen", false), (atDesk[1].Reason, atDesk[1].MayRemove));
        Assert.Equal(HttpStatusCode.Created, (await reception.PostAsJsonAsync("/api/appointments",
            new AppointmentRequest(spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(10), null, null, null, null), TestSpa.Json, Ct)).StatusCode);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal([CustomerFlagKind.InformManagement, CustomerFlagKind.DoNotBook], Assert.Single(day.Guests!).Flags);
    }
}

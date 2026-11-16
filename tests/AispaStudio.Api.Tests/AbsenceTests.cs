using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Ausfall-Assistent: Ersatz für die Termine einer ausgefallenen Person, erklärt, übernehmbar, Gäste werden informiert.</summary>
public sealed class AbsenceTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<AbsenceProposal> ProposeAsync(TestSpa spa, Guid employee) =>
        spa.PostAsync<AbsenceProposal>($"/api/locations/{spa.Location.Id}/absence/proposal", new AbsenceProposalRequest(employee, TestSpa.Monday), Ct);

    [Fact]
    public async Task Schlaegt_gleiche_Zeit_bei_Kollegin_vor_sonst_naechste_freie_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var early = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);
        var late = await spa.BookOkAsync(TestSpa.At(11, 30), Ct, employee: spa.Anna.Id, room: spa.RoomB.Id);
        await spa.BookOkAsync(TestSpa.At(11, 30), Ct, employee: spa.Ben.Id, room: spa.RoomA.Id);

        var proposal = await ProposeAsync(spa, spa.Anna.Id);

        Assert.Equal([early.Id, late.Id], proposal.Items.Select(i => i.Appointment.Id));
        var first = proposal.Items[0];
        Assert.Equal(AbsenceOption.SameTime, first.Option);
        Assert.Equal(spa.Ben.Id, first.EmployeeId);
        Assert.Equal(spa.RoomA.Id, first.RoomId);
        Assert.Equal("Ben hat die Qualifikation und ist um 10:00 frei; Raum Raum A bleibt.", first.Explanation);
        // Um 11:30 Uhr ist Ben selbst belegt, und um 10 Uhr hat er schon Annas ersten Termin übernommen.
        var second = proposal.Items[1];
        Assert.Equal(AbsenceOption.OtherTime, second.Option);
        Assert.Equal(spa.Ben.Id, second.EmployeeId);
        Assert.True(second.Start >= TestSpa.At(12, 30));
        Assert.StartsWith("Um 11:30 ist niemand frei. Nächste freie Zeit:", second.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_qualifizierten_Ersatz_bleibt_nur_der_Anruf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);

        var item = Assert.Single((await ProposeAsync(spa, spa.Anna.Id)).Items);

        Assert.Equal(AbsenceOption.None, item.Option);
        Assert.Null(item.Start);
        Assert.Contains("Bitte den Gast kontaktieren", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uebernehmen_verschiebt_und_informiert_den_Gast()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"gast-{Guid.NewGuid():N}@example.at";
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Maria", "Gast", email, null, null, null), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, customer: guest.Id), Ct);
        var item = Assert.Single((await ProposeAsync(spa, spa.Anna.Id)).Items);

        var result = await spa.PostAsync<AbsenceApplyResult>($"/api/locations/{spa.Location.Id}/absence/apply",
            new AbsenceApplyRequest([new AbsenceMove(appointment.Id, item.Start!.Value, item.EmployeeId!.Value, item.RoomId!.Value)], NotifyCustomers: true), Ct);
        var empty = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/absence/apply",
            new AbsenceApplyRequest([], NotifyCustomers: false), TestSpa.Json, Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Änderung Ihres Termins", StringComparison.Ordinal), Ct);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(spa.Ben.Id, Assert.Single(result.Moved).EmployeeId);
        Assert.Empty(result.Failed);
        Assert.Equal(1, result.Notified);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("Sie werden von Ben behandelt", mail.Text, StringComparison.Ordinal);
        Assert.Equal(spa.Ben.Id, calendar.Appointments.Single(a => a.Id == appointment.Id).EmployeeId);
    }

    [Fact]
    public async Task Ohne_Ersatz_hier_schlaegt_er_einen_anderen_Standort_vor_und_verlegt_erst_nach_Uebernehmen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var baden = await spa.CreateLocationAsync("Baden", [DayOfWeek.Monday], Ct);
        var roomC = await spa.PostAsync<RoomDto>($"/api/locations/{baden.Id}/rooms", new RoomInput("Raum C", 0), Ct);
        var clara = await spa.CreateEmployeeAsync("Clara", [spa.Massage.Id], baden.Id, Ct);
        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Massage überall", 50, 5, 10, 79m, [spa.Massage.Id], [spa.RoomA.Id, roomC.Id], []), Ct);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);
        var email = $"gast-{Guid.NewGuid():N}@example.at";
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Maria", "Gast", email, null, null, null), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(
            await spa.BookAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, treatment: treatment, customer: guest.Id), Ct);

        // Wer nur Wien sieht, bekommt keinen Vorschlag für Baden.
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var onlyHere = await (await reception.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/absence/proposal",
            new AbsenceProposalRequest(spa.Anna.Id, TestSpa.Monday), TestSpa.Json, Ct)).Content.ReadFromJsonAsync<AbsenceProposal>(TestSpa.Json, Ct);
        Assert.Equal(AbsenceOption.None, Assert.Single(onlyHere!.Items).Option);
        var sneaky = await reception.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/absence/apply",
            new AbsenceApplyRequest([new AbsenceMove(appointment.Id, TestSpa.At(10), clara.Id, roomC.Id, baden.Id)], NotifyCustomers: false), TestSpa.Json, Ct);
        Assert.Equal("Für diesen Standort fehlt der Zugang.", Assert.Single((await sneaky.Content.ReadFromJsonAsync<AbsenceApplyResult>(TestSpa.Json, Ct))!.Failed).Message);

        var item = Assert.Single((await ProposeAsync(spa, spa.Anna.Id)).Items);
        Assert.Equal(AbsenceOption.OtherLocation, item.Option);
        Assert.Equal(baden.Id, item.LocationId);
        Assert.Equal("Baden", item.LocationName);
        Assert.Equal(TestSpa.At(10), item.Start);
        Assert.Equal(clara.Id, item.EmployeeId);
        Assert.Equal(roomC.Id, item.RoomId);
        Assert.Equal("In Wien ist an diesem Tag niemand frei. In Baden geht es zur gleichen Zeit um 10:00 bei Clara in Raum C; nur nach Rücksprache mit dem Gast übernehmen.", item.Explanation);

        var result = await spa.PostAsync<AbsenceApplyResult>($"/api/locations/{spa.Location.Id}/absence/apply",
            new AbsenceApplyRequest([new AbsenceMove(appointment.Id, item.Start!.Value, item.EmployeeId!.Value, item.RoomId!.Value, item.LocationId)], NotifyCustomers: true), Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Änderung Ihres Termins", StringComparison.Ordinal), Ct);
        var wien = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var inBaden = await spa.GetAsync<CalendarDay>($"/api/locations/{baden.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Empty(result.Failed);
        Assert.Equal(baden.Id, Assert.Single(result.Moved).LocationId);
        Assert.DoesNotContain(wien.Appointments, a => a.Id == appointment.Id);
        Assert.Equal(clara.Id, inBaden.Appointments.Single(a => a.Id == appointment.Id).EmployeeId);
        Assert.Equal(appointment.Price, inBaden.Appointments.Single(a => a.Id == appointment.Id).Price);
        Assert.Contains("nicht in Wien, sondern in Baden statt", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Therapeutin_darf_nicht_umverteilen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var response = await anna.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/absence/proposal",
            new AbsenceProposalRequest(spa.Anna.Id, TestSpa.Monday), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gaeste_ohne_Ersatz_bekommen_eine_Nachricht_und_der_Termin_bleibt_stehen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suffix = Guid.NewGuid().ToString("N");
        var phoneOnly = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Paul", $"Telefon{suffix}", null, $"+43 660 {Random.Shared.Next(1_000_000, 9_999_999)}", null, null), Ct);
        var silent = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ina", $"Ohne{suffix}", null, null, null, null), Ct);
        var withMail = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var withPhone = await spa.BookOkAsync(TestSpa.At(12), Ct, employee: spa.Anna.Id, customer: phoneOnly.Id);
        var withNothing = await spa.BookOkAsync(TestSpa.At(14), Ct, employee: spa.Anna.Id, customer: silent.Id);
        var cancelled = await spa.BookOkAsync(TestSpa.At(15, 30), Ct, employee: spa.Anna.Id);
        (await spa.Client.PostAsJsonAsync($"/api/appointments/{cancelled.Id}/cancel", new { }, TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var result = await spa.PostAsync<AbsenceNotifyResult>($"/api/locations/{spa.Location.Id}/absence/notify",
            new AbsenceNotifyRequest([withMail.Id, withPhone.Id, withNothing.Id, cancelled.Id, Guid.NewGuid()]), Ct);

        Assert.Contains(withMail.Id, result.Notified);
        var mail = await fixture.Mailbox.WaitForAsync(spa.Customer.Email!, m => m.Subject.StartsWith("Ihr Termin am 01.03.2027", StringComparison.Ordinal), Ct);
        Assert.Contains("kann Ihr Termin „Massage“ am 01.03.2027 um 10:00 Uhr nicht wie geplant stattfinden", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Wir melden uns bei Ihnen", mail.Text, StringComparison.Ordinal);
        // Ohne Mailadresse und ohne Nummer bleibt nur der Anruf; abgesagte und fremde Termine werden nicht angeschrieben.
        Assert.Contains(result.Failed, f => f.AppointmentId == withNothing.Id && f.Message.Contains("anrufen", StringComparison.Ordinal));
        Assert.Contains(result.Failed, f => f.AppointmentId == cancelled.Id);
        Assert.Equal(5, result.Notified.Count + result.Failed.Count);
        // Nichts wird storniert oder verschoben.
        var after = await spa.GetAsync<AppointmentDto>($"/api/appointments/{withMail.Id}", Ct);
        Assert.Equal((AppointmentStatus.Booked, withMail.Start), (after.Status, after.Start));
    }

    [Fact]
    public async Task Rezeption_eines_anderen_Standorts_benachrichtigt_hier_niemanden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var other = await spa.CreateLocationAsync($"Anderswo {Guid.NewGuid():N}", [DayOfWeek.Monday], Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);

        var response = await reception.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/absence/notify", new AbsenceNotifyRequest([appointment.Id]), TestSpa.Json, Ct);

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }
}

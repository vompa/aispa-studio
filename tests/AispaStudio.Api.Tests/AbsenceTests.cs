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
}

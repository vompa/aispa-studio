using System.Net;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Kalenderdatei einer Person für den privaten Kalender (Export/Import, Aufgabe C): Dienste und Termine, ohne Namen der Gäste; nur die Leitung oder die Person selbst.</summary>
public sealed class EmployeeIcsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    private async Task<(TestSpa Spa, AppointmentDto Appointment)> SpaWithShiftAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{TestSpa.Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Work, spa.Location.Id, new TimeOnly(9, 0), new TimeOnly(17, 0))], null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(12), Ct, employee: spa.Ben.Id);
        return (spa, appointment);
    }

    [Fact]
    public async Task Dienste_und_Termine_ohne_Namen_der_Gaeste()
    {
        var (spa, appointment) = await SpaWithShiftAsync();

        using var response = await spa.Client.GetAsync($"/api/employees/{spa.Ben.Id}/ics?from=2027-03-01&until=2027-03-07", Ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        var text = Encoding.UTF8.GetString(bytes);
        SampleFiles.Save("Kalender_Ben.ics", bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Kalender_Ben.ics", (response.Content.Headers.ContentDisposition?.FileNameStar ?? "").Trim('"'));
        Assert.Equal(2, Count(text, "BEGIN:VEVENT"));
        Assert.Contains("SUMMARY:Dienst Wien\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DTSTART;TZID=Europe/Vienna:20270301T090000\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DTEND;TZID=Europe/Vienna:20270301T170000\r\n", text, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Massage (Raum ", text, StringComparison.Ordinal);
        Assert.Contains($"UID:{appointment.Id}@aispa-studio\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain(spa.Customer.LastName, text, StringComparison.Ordinal);
        Assert.DoesNotContain("VALARM", text, StringComparison.Ordinal);

        using var shifts = await spa.Client.GetAsync($"/api/employees/{spa.Ben.Id}/ics?from=2027-03-01&until=2027-03-07&include=shifts", Ct);
        var onlyShifts = await shifts.Content.ReadAsStringAsync(Ct);
        Assert.Equal(1, Count(onlyShifts, "BEGIN:VEVENT"));
        Assert.Contains("SUMMARY:Dienst Wien\r\n", onlyShifts, StringComparison.Ordinal);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Contains(history, h => h.Text == "Export Kalender einer Person (iCal, 2 Zeilen, Ben) von Test Person");
    }

    [Fact]
    public async Task Nur_die_Leitung_oder_die_Person_selbst()
    {
        var (spa, _) = await SpaWithShiftAsync();
        const string Path = "ics?from=2027-03-01&until=2027-03-07";
        using var ben = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Ben.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.OK, (await ben.GetAsync($"/api/employees/{spa.Ben.Id}/{Path}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ben.GetAsync($"/api/employees/{spa.Anna.Id}/{Path}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/employees/{spa.Ben.Id}/{Path}", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("from=2027-03-07&until=2027-03-01")]
    [InlineData("from=2027-03-01&until=2028-03-07")]
    [InlineData("from=2027-03-01&until=2027-03-07&include=urlaub")]
    public async Task Ungueltiger_Zeitraum_oder_Inhalt_ergeben_einen_Hinweis(string query)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        using var response = await spa.Client.GetAsync($"/api/employees/{spa.Ben.Id}/ics?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Kalenderdatei eines Termins für den Gast (Export/Import, Aufgabe C): Zeitzone als TZID, Erinnerung 24 Stunden vorher, stabile UID, eine Buchung mit mehreren Teilen als mehrere Einträge.</summary>
public sealed class AppointmentIcsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string FileName(HttpResponseMessage response) =>
        (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "").Trim('"');

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    [Fact]
    public async Task Ein_Termin_mit_Zeitzone_Erinnerung_und_stabiler_UID()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        using var response = await spa.Client.GetAsync($"/api/appointments/{appointment.Id}/ics", Ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        var text = Encoding.UTF8.GetString(bytes);
        SampleFiles.Save(FileName(response), bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Termin_2027-03-01_1000.ics", FileName(response));
        Assert.Equal(1, Count(text, "BEGIN:VEVENT"));
        Assert.Contains($"UID:{appointment.Id}@aispa-studio\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DTSTART;TZID=Europe/Vienna:20270301T100000\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DTEND;TZID=Europe/Vienna:20270301T105000\r\n", text, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VTIMEZONE\r\nTZID:Europe/Vienna\r\n", text, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Massage\r\n", text, StringComparison.Ordinal);
        Assert.Contains("LOCATION:Wien\r\n", text, StringComparison.Ordinal);
        Assert.Contains("TRIGGER:-PT24H\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Anna", text, StringComparison.Ordinal);

        using var again = await spa.Client.GetAsync($"/api/appointments/{appointment.Id}/ics", Ct);
        Assert.Contains($"UID:{appointment.Id}@aispa-studio\r\n", await again.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Contains(history, h => h.Text == "Export Termin (iCal, 1 Zeile, Massage am 1.3.2027) von Test Person");
    }

    [Fact]
    public async Task Eine_Buchung_mit_zwei_Teilen_ergibt_zwei_Eintraege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var massage = spa.MassageTreatment.Id;
        var response = await spa.Client.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(spa.Location.Id, TestSpa.At(10),
            [new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, spa.Customer.Id, 1)], 0), TestSpa.Json, Ct);
        var group = await TestSpa.ReadAsync<GroupBookingResult>(response, Ct);

        using var ics = await spa.Client.GetAsync($"/api/appointments/{group.Appointments[1].Id}/ics", Ct);
        var text = await ics.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, ics.StatusCode);
        Assert.Equal(2, Count(text, "BEGIN:VEVENT"));
        Assert.All(group.Appointments, a => Assert.Contains($"UID:{a.Id}@aispa-studio\r\n", text, StringComparison.Ordinal));
        Assert.Equal(2, Count(text, "TRIGGER:-PT24H"));
    }

    [Fact]
    public async Task Unbekannter_Termin_ergibt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        using var response = await spa.Client.GetAsync($"/api/appointments/{Guid.NewGuid()}/ics", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

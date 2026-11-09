using System.Net;
using System.Text;
using AispaStudio.Contracts;
using ClosedXML.Excel;

namespace AispaStudio.Api.Tests;

/// <summary>Terminliste eines Standorts als Excel und CSV (Export/Import, Aufgabe C): Zeitraum, Person, Raum und Status als Filter, Notiz nur für die Leitung, Eintrag im Verlauf.</summary>
public sealed class AppointmentExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Tuesday = TestSpa.Monday.AddDays(1);

    private static string FileName(HttpResponseMessage response) =>
        (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "").Trim('"');

    private static async Task<byte[]> SavedAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        SampleFiles.Save(FileName(response), bytes);
        return bytes;
    }

    private async Task<TestSpa> SpaWithAppointmentsAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var anna = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);
        _ = await spa.BookOkAsync(TestSpa.At(12), Ct, employee: spa.Ben.Id, room: spa.RoomB.Id);
        _ = await spa.BookOkAsync(TestSpa.At(10, date: Tuesday), Ct, employee: spa.Ben.Id, room: spa.RoomB.Id);
        _ = await spa.PutAsync<AppointmentDto>($"/api/appointments/{anna.Id}/note", new AppointmentNoteRequest("Lavendelöl"), Ct);
        return spa;
    }

    [Fact]
    public async Task Excel_eines_Tages_mit_Notiz_fuer_die_Leitung_und_Eintrag_im_Verlauf()
    {
        var spa = await SpaWithAppointmentsAsync();

        using var response = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/appointments/export?from=2027-03-01&until=2027-03-01&format=xlsx", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Termine_2027-03-01.xlsx", FileName(response));
        using var workbook = new XLWorkbook(new MemoryStream(await SavedAsync(response)));
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal(["Tag", "Beginn", "Ende", "Gast", "Behandlung", "Person", "Raum", "Status", "Preis", "Kanal", "Notiz"],
            sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
        var row = sheet.Row(2);
        Assert.Equal(new DateTime(2027, 3, 1), row.Cell(1).GetDateTime());
        Assert.Equal(new TimeSpan(10, 0, 0), row.Cell(2).GetTimeSpan());
        Assert.Equal(new TimeSpan(10, 50, 0), row.Cell(3).GetTimeSpan());
        Assert.Equal($"{spa.Customer.FirstName} {spa.Customer.LastName}", row.Cell(4).GetString());
        Assert.Equal(("Massage", "Anna", "Raum A", "Gebucht"), (row.Cell(5).GetString(), row.Cell(6).GetString(), row.Cell(7).GetString(), row.Cell(8).GetString()));
        Assert.Equal(79, row.Cell(9).GetDouble());
        Assert.Equal(("Rezeption", "Lavendelöl"), (row.Cell(10).GetString(), row.Cell(11).GetString()));
        Assert.Equal("Ben", sheet.Row(3).Cell(6).GetString());

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal("Export Terminliste (Excel, 2 Zeilen, 1.3.2027 bis 1.3.2027) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task CSV_fuer_die_Rezeption_ohne_Notiz_mit_Filter_Person()
    {
        var spa = await SpaWithAppointmentsAsync();
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        using var response = await reception.GetAsync(
            $"/api/locations/{spa.Location.Id}/appointments/export?from=2027-03-01&until=2027-03-02&employeeId={spa.Ben.Id}&format=csv", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await SavedAsync(response);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var guest = $"{spa.Customer.FirstName} {spa.Customer.LastName}";
        Assert.Equal(["Tag;Beginn;Ende;Gast;Behandlung;Person;Raum;Status;Preis;Kanal",
            $"01.03.2027;12:00;12:50;{guest};Massage;Ben;Raum B;Gebucht;79,00;Rezeption",
            $"02.03.2027;10:00;10:50;{guest};Massage;Ben;Raum B;Gebucht;79,00;Rezeption"], lines);
    }

    [Fact]
    public async Task Filter_Raum_und_Status_grenzen_ein()
    {
        var spa = await SpaWithAppointmentsAsync();
        var path = $"/api/locations/{spa.Location.Id}/appointments/export?from=2027-03-01&until=2027-03-07&format=csv";

        Assert.Equal(2, await RowsAsync(spa, $"{path}&roomId={spa.RoomB.Id}"));
        Assert.Equal(3, await RowsAsync(spa, $"{path}&status=booked"));
        Assert.Equal(0, await RowsAsync(spa, $"{path}&status=noshow"));
    }

    private static async Task<int> RowsAsync(TestSpa spa, string path)
    {
        using var response = await spa.Client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Encoding.UTF8.GetString((await response.Content.ReadAsByteArrayAsync(Ct))[3..]);
        return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }

    [Theory]
    [InlineData("from=2027-03-02&until=2027-03-01&format=xlsx")]
    [InlineData("until=2027-03-01&format=xlsx")]
    [InlineData("from=2026-01-01&until=2027-03-01&format=xlsx")]
    [InlineData("from=2027-03-01&until=2027-03-01&format=vcf")]
    [InlineData("from=2027-03-01&until=2027-03-01&format=xlsx&status=offen")]
    public async Task Ungueltiger_Zeitraum_Status_oder_Format_ergeben_einen_Hinweis(string query)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        using var response = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/appointments/export?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

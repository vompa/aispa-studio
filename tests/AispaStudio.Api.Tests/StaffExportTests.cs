using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Listen des Personals als Excel (Export/Import, Aufgabe D): Stundenliste je Person und Monat, Abwesenheiten eines Zeitraums und Dienstplan der Woche mit Tagen
/// als Zeilen und Personen als Spalten; nur für die Leitung, ein fremder Mandant sieht nichts. Anna und Ben arbeiten laut Wochenmuster montags 9 bis 17 Uhr.
/// </summary>
public sealed class StaffExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Montag in der Vergangenheit, damit Zeiten erfasst werden dürfen.</summary>
    private static readonly DateOnly Past = new(2026, 6, 1);

    private static string Base(TestSpa spa) => $"/api/locations/{spa.Location.Id}/staff/export";

    private static async Task PlanAsync(TestSpa spa, Guid employeeId, DateOnly date, RosterEntry entry) =>
        _ = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employeeId}/{date:yyyy-MM-dd}", new RosterDayInput([entry], null), Ct);

    [Fact]
    public async Task Stundenliste_eines_Monats_mit_Soll_Ist_und_Differenz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var added = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/time-records",
            new TimeRecordInput(spa.Ben.Id, TestSpa.At(9, 0, Past), TestSpa.At(17, 30, Past), "Stempeln vergessen"), TestSpa.Json, Ct);
        added.EnsureSuccessStatusCode();

        using var response = await spa.Client.GetAsync($"{Base(spa)}?list=hours&month=2026-06&employeeId={spa.Ben.Id}&format=xlsx", Ct);
        var sheet = await ExportResponses.SheetAsync(response, Ct);

        Assert.Equal("Stundenliste_2026-06-01.xlsx", ExportResponses.FileName(response));
        Assert.Equal(["Person", "Tag", "Soll (Stunden)", "Ist (Stunden)", "Pause (Minuten)", "Differenz (Stunden)"], ExportResponses.Headers(sheet));
        // Fünf Montage im Juni 2026, jeder mit 8 Stunden Soll; gestempelt nur am 1. Juni.
        Assert.Equal(6, sheet.LastRowUsed()!.RowNumber());
        var first = sheet.Row(2);
        Assert.Equal(("Ben", new DateTime(2026, 6, 1), 8d, 8.5d, 0.5d),
            (first.Cell(1).GetString(), first.Cell(2).GetDateTime(), first.Cell(3).GetDouble(), first.Cell(4).GetDouble(), first.Cell(6).GetDouble()));
        Assert.Equal((new DateTime(2026, 6, 8), 0d, -8d), (sheet.Row(3).Cell(2).GetDateTime(), sheet.Row(3).Cell(4).GetDouble(), sheet.Row(3).Cell(6).GetDouble()));

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal("Export Stundenliste (Excel, 5 Zeilen, Juni 2026) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Abwesenheiten_als_Spannen_und_Dienstplan_der_Woche_mit_Tagen_als_Zeilen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var vacation = new RosterEntry(RosterKind.Vacation, null, null, null);
        await PlanAsync(spa, spa.Anna.Id, TestSpa.Monday.AddDays(1), vacation);
        await PlanAsync(spa, spa.Anna.Id, TestSpa.Monday.AddDays(2), vacation);
        await PlanAsync(spa, spa.Ben.Id, TestSpa.Monday, new RosterEntry(RosterKind.Work, spa.Location.Id, new TimeOnly(10, 0), new TimeOnly(14, 0)));
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Tuesday], Ct);
        await PlanAsync(spa, spa.Ben.Id, TestSpa.Monday.AddDays(1), new RosterEntry(RosterKind.Work, graz.Id, new TimeOnly(10, 0), new TimeOnly(14, 0)));

        using var absences = await spa.Client.GetAsync($"{Base(spa)}?list=absences&from=2027-03-01&until=2027-03-31&format=xlsx", Ct);
        using var roster = await spa.Client.GetAsync($"{Base(spa)}?list=roster&from=2027-03-01&format=xlsx", Ct);
        var absenceSheet = await ExportResponses.SheetAsync(absences, Ct);
        var rosterSheet = await ExportResponses.SheetAsync(roster, Ct);

        Assert.Equal(["Person", "Art", "Von", "Bis", "Tage"], ExportResponses.Headers(absenceSheet));
        Assert.Equal(2, absenceSheet.LastRowUsed()!.RowNumber());
        var row = absenceSheet.Row(2);
        Assert.Equal(("Anna", "Urlaub", new DateTime(2027, 3, 2), new DateTime(2027, 3, 3), 2d),
            (row.Cell(1).GetString(), row.Cell(2).GetString(), row.Cell(3).GetDateTime(), row.Cell(4).GetDateTime(), row.Cell(5).GetDouble()));

        Assert.Equal("Dienstplan_2027-03-01.xlsx", ExportResponses.FileName(roster));
        Assert.Equal(["Tag", "Anna", "Ben", "Besetzung"], ExportResponses.Headers(rosterSheet));
        var (monday, tuesday, wednesday, sunday, hours) = (rosterSheet.Row(2), rosterSheet.Row(3), rosterSheet.Row(4), rosterSheet.Row(8), rosterSheet.Row(9));
        Assert.Equal(("Mo. 1.3.2027", "9:00–17:00", "10:00–14:00", 2d), (monday.Cell(1).GetString(), monday.Cell(2).GetString(), monday.Cell(3).GetString(), monday.Cell(4).GetDouble()));
        // Urlaub und ein Dienst in Graz zählen nicht zur Besetzung in Wien; der Dienst in Graz trägt den Standort in der Zelle.
        Assert.Equal(("Di. 2.3.2027", "Urlaub", "10:00–14:00 Graz", 0d), (tuesday.Cell(1).GetString(), tuesday.Cell(2).GetString(), tuesday.Cell(3).GetString(), tuesday.Cell(4).GetDouble()));
        Assert.Equal(("Mi. 3.3.2027", "Urlaub", ""), (wednesday.Cell(1).GetString(), wednesday.Cell(2).GetString(), wednesday.Cell(3).GetString()));
        Assert.Equal(("So. 7.3.2027", "Stunden", 8d, 8d), (sunday.Cell(1).GetString(), hours.Cell(1).GetString(), hours.Cell(2).GetDouble(), hours.Cell(3).GetDouble()));
        Assert.Equal(9, rosterSheet.LastRowUsed()!.RowNumber());

        using var csv = await spa.Client.GetAsync($"{Base(spa)}?list=roster&from=2027-03-01&format=csv", Ct);
        var lines = (await csv.Content.ReadAsStringAsync(Ct)).TrimStart('\uFEFF').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Tag;Anna;Ben;Besetzung", "Mo. 1.3.2027;9:00–17:00;10:00–14:00;2", "Di. 2.3.2027;Urlaub;10:00–14:00 Graz;0"], lines[..3]);
        Assert.Equal("Stunden;8;8;", lines[^1]);
    }

    [Fact]
    public async Task Nur_die_Leitung_und_nur_der_eigene_Mandant()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var stranger = await TestUsers.ClientAsync(fixture, Ct);
        var path = $"{Base(spa)}?list=roster&from=2027-03-01&format=xlsx";

        using var forManager = await manager.GetAsync(path, Ct);
        using var forStranger = await stranger.GetAsync(path, Ct);
        using var noMonth = await spa.Client.GetAsync($"{Base(spa)}?list=hours&format=xlsx", Ct);
        using var noWeek = await spa.Client.GetAsync($"{Base(spa)}?list=roster&format=csv", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forManager.StatusCode);
        Assert.Contains(forStranger.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (noMonth.StatusCode, noWeek.StatusCode));
    }
}

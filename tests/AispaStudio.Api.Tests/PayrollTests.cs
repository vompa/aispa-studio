using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Api.Payroll;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lohnexport: Monatswerte aus dem Dienstplan mit Sonntags-, Feiertags- und Nachtstunden, Abwesenheiten in Tagen.</summary>
public sealed class PayrollTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Feiertage_und_Nachtstunden()
    {
        Assert.Equal(new DateOnly(2027, 3, 28), PublicHolidays.EasterSunday(2027));
        Assert.Equal(new DateOnly(2026, 4, 5), PublicHolidays.EasterSunday(2026));
        Assert.True(PublicHolidays.IsHoliday(FiscalCountry.AT, new(2027, 3, 29)));
        Assert.True(PublicHolidays.IsHoliday(FiscalCountry.AT, new(2027, 12, 8)));
        Assert.False(PublicHolidays.IsHoliday(FiscalCountry.DE, new(2027, 12, 8)));
        Assert.True(PublicHolidays.IsHoliday(FiscalCountry.DE, new(2027, 3, 26)));

        Assert.Equal(1.5m, PayrollExport.NightHours(new(18, 0), new(23, 30), new(22, 0), new(6, 0)));
        Assert.Equal(1m, PayrollExport.NightHours(new(5, 0), new(9, 0), new(22, 0), new(6, 0)));
        Assert.Equal(0m, PayrollExport.NightHours(new(9, 0), new(17, 0), new(22, 0), new(6, 0)));
    }

    [Fact]
    public async Task Monatswerte_aus_dem_Dienstplan_gehen_an_DATEV_und_BMD()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = spa.Location.Id;
        // März 2027: Anna arbeitet Mo 9–17 Uhr (1., 8., 15., 22. und Ostermontag 29.), dazu Sonntag 7. von 18 bis 23 Uhr.
        await Plan(spa, spa.Anna.Id, new(2027, 3, 7), new RosterEntry(RosterKind.Work, location, new(18, 0), new(23, 0)));
        await Plan(spa, spa.Anna.Id, new(2027, 3, 8), new RosterEntry(RosterKind.Vacation, null, null, null));
        await Plan(spa, spa.Anna.Id, new(2027, 3, 15), new RosterEntry(RosterKind.Sick, null, null, null));
        await spa.PutAsync<PayrollEmployeeDto>($"/api/payroll/employees/{spa.Anna.Id}", new PersonnelNumberInput("1001"), Ct);

        var preview = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2027&month=3", Ct);

        var anna = preview.Lines.Where(l => l.EmployeeId == spa.Anna.Id).ToDictionary(l => l.Item, l => l.Quantity);
        Assert.Equal(29m, anna[PayrollItem.Hours]);
        Assert.Equal(5m, anna[PayrollItem.SundayHours]);
        Assert.Equal(8m, anna[PayrollItem.HolidayHours]);
        Assert.Equal(1m, anna[PayrollItem.NightHours]);
        Assert.Equal(1m, anna[PayrollItem.VacationDays]);
        Assert.Equal(1m, anna[PayrollItem.SickDays]);
        Assert.Equal(40m, preview.Lines.Single(l => l.EmployeeId == spa.Ben.Id && l.Item == PayrollItem.Hours).Quantity);
        Assert.Contains(preview.Warnings, w => w.StartsWith("Ben: keine Personalnummer", StringComparison.Ordinal));

        var lodas = await DownloadAsync(spa, PayrollFormat.DatevLodas);
        Assert.Contains("Ziel=LODAS", lodas, StringComparison.Ordinal);
        Assert.Contains("1;1001;01.03.2027;29,00;1;100;", lodas, StringComparison.Ordinal);
        Assert.Contains("1;1001;01.03.2027;1,00;2;200;", lodas, StringComparison.Ordinal);
        Assert.DoesNotContain("40,00", lodas, StringComparison.Ordinal);
        var bmd = await DownloadAsync(spa, PayrollFormat.BmdLohn);
        Assert.Contains("1001;120;8,00;Std;03.2027;Anna", bmd, StringComparison.Ordinal);
    }

    [Fact]
    public void Landesfeiertage_nach_Bundesland_und_Kanton()
    {
        // Ostersonntag 2027 ist der 28. März: Fronleichnam am 27. Mai.
        Assert.Contains(new DateOnly(2027, 5, 27), PublicHolidays.For(FiscalCountry.DE, "DE-BY", 2027));
        Assert.Contains(new DateOnly(2027, 11, 1), PublicHolidays.For(FiscalCountry.DE, "DE-NW", 2027));
        Assert.DoesNotContain(new DateOnly(2027, 5, 27), PublicHolidays.For(FiscalCountry.DE, "DE-BE", 2027));
        Assert.Contains(new DateOnly(2027, 3, 8), PublicHolidays.For(FiscalCountry.DE, "DE-BE", 2027));
        Assert.Contains(new DateOnly(2027, 10, 31), PublicHolidays.For(FiscalCountry.DE, "DE-HH", 2027));
        Assert.DoesNotContain(new DateOnly(2027, 10, 31), PublicHolidays.For(FiscalCountry.DE, null, 2027));
        Assert.Contains(new DateOnly(2026, 11, 18), PublicHolidays.For(FiscalCountry.DE, "DE-SN", 2026));
        Assert.Contains(new DateOnly(2027, 11, 17), PublicHolidays.For(FiscalCountry.DE, "DE-SN", 2027));
        Assert.Equal(13, PublicHolidays.For(FiscalCountry.AT, "AT-9", 2027).Count);
        Assert.Contains(new DateOnly(2027, 3, 26), PublicHolidays.For(FiscalCountry.CH, "CH-ZH", 2027));
        Assert.Contains(new DateOnly(2027, 1, 2), PublicHolidays.For(FiscalCountry.CH, "CH-BE", 2027));
        Assert.Equal([new DateOnly(2027, 8, 1)], PublicHolidays.For(FiscalCountry.CH, null, 2027));

        // Die Region bestimmt das Land, auch wenn die Lohneinstellungen ein anderes nennen.
        Assert.Contains(new DateOnly(2027, 3, 26), PublicHolidays.For(FiscalCountry.AT, "DE-BY", 2027));
        Assert.DoesNotContain(new DateOnly(2027, 12, 8), PublicHolidays.For(FiscalCountry.AT, "DE-BY", 2027));
    }

    [Fact]
    public async Task Standort_mit_Bundesland_bestimmt_die_Feiertage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var location = spa.Location;
        await Plan(spa, spa.Anna.Id, new(2027, 5, 27), new RosterEntry(RosterKind.Work, location.Id, new(9, 0), new(17, 0)));
        await spa.PutAsync<PayrollEmployeeDto>($"/api/payroll/employees/{spa.Anna.Id}", new PersonnelNumberInput("1001"), Ct);
        var before = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2027&month=5", Ct);
        Assert.Contains(before.Warnings, w => w.Contains("kein Bundesland oder Kanton", StringComparison.Ordinal));

        var invalid = await spa.Client.PutAsJsonAsync($"/api/locations/{location.Id}",
            new LocationInput(location.Name, location.TimeZone, location.Currency, location.OpeningHours, "DE-XX"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var updated = await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}",
            new LocationInput(location.Name, location.TimeZone, location.Currency, location.OpeningHours, "DE-BY"), Ct);
        Assert.Equal("DE-BY", updated.Region);

        var holidays = await spa.GetAsync<List<PublicHolidayDto>>($"/api/locations/{location.Id}/holidays?year=2027", Ct);
        Assert.Contains(new PublicHolidayDto(new(2027, 5, 27), "Fronleichnam"), holidays);
        Assert.Contains(new PublicHolidayDto(new(2027, 10, 3), "Tag der Deutschen Einheit"), holidays);
        Assert.Contains(await spa.GetAsync<List<HolidayRegionDto>>("/api/holidays/regions", Ct), r => r.Code == "CH-ZH");

        // Fronleichnam am Donnerstag zählt jetzt als Feiertag (Ostersonntag 2027: 28. März).
        var after = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2027&month=5", Ct);
        Assert.DoesNotContain(after.Warnings, w => w.Contains("kein Bundesland oder Kanton", StringComparison.Ordinal));
        Assert.Contains(after.Lines, l => l.EmployeeId == spa.Anna.Id && l.Item == PayrollItem.HolidayHours && l.Quantity >= 8m);
    }

    [Fact]
    public async Task Lohnexport_nur_fuer_Inhaber()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        var response = await manager.GetAsync("/api/payroll/preview?year=2027&month=3", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<RosterDayResult> Plan(TestSpa spa, Guid employee, DateOnly date, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employee}/{date:yyyy-MM-dd}", new RosterDayInput(entries, null), Ct);

    private static async Task<string> DownloadAsync(TestSpa spa, PayrollFormat format)
    {
        var response = await spa.Client.GetAsync($"/api/payroll/export?format={format}&year=2027&month=3", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetString(await response.Content.ReadAsByteArrayAsync(Ct));
    }
}

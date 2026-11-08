using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Controls;
using Avalonia.Headless;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 100 (Ä191, Ä250, ADR 0084): Lohn im Bereich „Team“ (Ansicht „Lohn“, Reiter „Lohn“ der Personenkarte) mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly byte[] PayrollFileBytes = [(byte)'D', (byte)'N', 0x0D, 0x0A];

    /// <summary>Wie <see cref="RosterApi"/>, mit den Lohn-Aufrufen für den Vormonat der Tests (September 2026).</summary>
    private static StubApi PayrollApi()
    {
        var (api, _) = RosterApi();
        var lena = RosterLena;
        var preview = new PayrollPreviewDto(2026, 9, FiscalCountry.AT,
            [new PayrollLine(lena.Id, lena.DisplayName, "4711", PayrollItem.Hours, 16m, "h", "100"), new PayrollLine(lena.Id, lena.DisplayName, "4711", PayrollItem.VacationDays, 2m, "Tage", "200")], []);
        return api
            .On("GET /api/payroll/settings", new PayrollSettingsDto(FiscalCountry.AT, "1234567", "12345", new TimeOnly(22, 0), new TimeOnly(6, 0), "100", "110", "120", "130", "200", "210", "220"))
            .On("GET /api/payroll/employees", new List<PayrollEmployeeDto> { new(lena.Id, lena.DisplayName, true, "4711") })
            .On("GET /api/payroll/preview?year=2026&month=9", preview)
            .OnBytes("GET /api/payroll/export?format=BmdLohn&year=2026&month=9", PayrollFileBytes)
            .OnJson($"PUT /api/payroll/employees/{lena.Id}", body =>
                new PayrollEmployeeDto(lena.Id, lena.DisplayName, true, JsonSerializer.Deserialize<PersonnelNumberInput>(body!, ApiClient.Json)!.PersonnelNumber));
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Lohn_exportiert_den_Monat_ueber_den_Speicherdialog(string theme)
    {
        var api = PayrollApi();
        return Render(theme, "verwaltung-team-lohn", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            var files = new FakeFileSaver();
            await ClickSection(window, "Lohn");
            Assert.True(team.ShowsPayroll);
            await Pump(Settling);
            Assert.True(team.Payroll!.IsLoaded);
            team.Payroll.Files = files;

            await ClickButton(window, VisibleNamed<Button>(window, "PayrollExportBmdButton"));
            await Pump(Settling);

            var (name, content) = Assert.Single(files.Saved);
            Assert.Equal("lohn-BmdLohn-2026-09.csv", name);
            Assert.Equal(PayrollFileBytes, content);
            Assert.True(Shows(window, "lohn-BmdLohn-2026-09.csv gespeichert."));
            Assert.True(Shows(window, "Stunden gesamt"));
            Assert.True(Shows(window, "16,00 h"));
            Assert.True(Shows(window, "Einstellungen der Lohnverrechnung"));
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "Footer").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_die_Karte_von_Lena_When_die_Personalnummer_getippt_und_Strg_S_gedrueckt_wird_Then_geht_sie_an_die_API()
    {
        var api = PayrollApi();
        return Render("hell", "verwaltung-team-person-lohn", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Lohn");
            await Pump(Settling);
            Assert.True(team.Card!.Payroll!.IsLoaded);
            Assert.True(Shows(window, "Stunden gesamt"));

            var field = VisibleNamed<TextBox>(window, "PersonnelNumberField");
            field.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, field);
            await Pump();
            field.SelectAll();
            window.KeyTextInput("4712");
            await Pump();
            PressSave(window);
            await Pump(Settling);

            var sent = JsonSerializer.Deserialize<PersonnelNumberInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/payroll/employees/{RosterLena.Id}").Body!, ApiClient.Json)!;
            Assert.Equal("4712", sent.PersonnelNumber);
            Assert.False(team.HasUnsavedChanges);
            return team;
        }, api);
    }
}

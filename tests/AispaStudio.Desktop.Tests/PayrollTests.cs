using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 100 (Ä191, Ä250, ADR 0084): Lohn im Bereich „Team“ – der Reiter „Lohn“ der Personenkarte (Personalnummer, Monatswerte der Person) und die Ansicht „Lohn“ mit Vorschau,
/// Export als Datei und Einstellungen. Die Mengen, die Prüfung der Personalnummer und das Dateiformat rechnet und prüft die API (<c>PayrollTests</c>); hier zählt, was die App von ihr
/// nimmt, schickt und sagt.
/// </summary>
public sealed class PayrollTests
{
    private const string October = "2026-10";
    private const string BmdExport = "GET /api/payroll/export?format=BmdLohn&year=2026&month=10";
    private const string OctoberPreview = "GET /api/payroll/preview?year=2026&month=10";

    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly EmployeeDto Marco = RosterSample.Marco;

    /// <summary>Kopf und eine Zeile einer BMD-Datei in Windows-1252: „ü“ ist das Byte 0xFC, Zeilenende CR LF. Fest eingetragen, nicht aus dem Code gerechnet.</summary>
    private static readonly byte[] WebExportBytes =
    [
        (byte)'D', (byte)'N', (byte)';', (byte)'L', (byte)'A', (byte)';', (byte)'M', (byte)'e', (byte)'n', (byte)'g', (byte)'e', 0x0D, 0x0A,
        (byte)'4', (byte)'7', (byte)'1', (byte)'1', (byte)';', (byte)'1', (byte)'0', (byte)'0', (byte)';', (byte)'1', (byte)'6', (byte)',', (byte)'0', (byte)'0', (byte)';', (byte)'F', 0xFC, (byte)'r', 0x0D, 0x0A,
    ];

    private static PayrollSettingsDto Settings(string? hours = "100") =>
        new(FiscalCountry.AT, "1234567", "12345", new TimeOnly(22, 0), new TimeOnly(6, 0), hours, "110", "120", "130", "200", "210", "220");

    private static PayrollLine Line(EmployeeDto person, PayrollItem item, decimal quantity, string unit, string? number, string? wageType) =>
        new(person.Id, person.DisplayName, number, item, quantity, unit, wageType);

    private static PayrollPreviewDto Preview(params PayrollLine[] lines) => new(2026, 10, FiscalCountry.AT, lines, []);

    private static StubApi Api(PayrollPreviewDto? preview = null, PayrollSettingsDto? settings = null)
    {
        var (api, _) = RosterTests.Setup();
        preview ??= Preview(
            Line(Lena, PayrollItem.Hours, 16m, "h", "4711", "100"),
            Line(Lena, PayrollItem.VacationDays, 2m, "Tage", "4711", "200"),
            Line(Marco, PayrollItem.Hours, 8m, "h", null, "100"));
        api.On("GET /api/payroll/settings", settings ?? Settings())
            .On("GET /api/payroll/employees", new List<PayrollEmployeeDto>
            {
                new(Lena.Id, Lena.DisplayName, true, "4711"),
                new(Marco.Id, Marco.DisplayName, true, null),
            })
            .On(OctoberPreview, preview)
            .On("GET /api/payroll/preview?year=2027&month=2", preview with { Year = 2027, Month = 2 })
            .OnBytes(BmdExport, WebExportBytes);
        return api;
    }

    private static async Task<PayrollViewModel> Payroll(StubApi api, CurrentUser? user = null, IFileSaver? files = null)
    {
        var team = await RosterTests.Loaded(api, user);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PayrollMode));
        var payroll = team.Payroll!;
        payroll.Files = files;
        await RosterTests.Until(() => payroll.IsLoaded);
        return payroll;
    }

    private static void ChooseOctober(PayrollViewModel payroll) => payroll.SelectedMonth = payroll.Months.Single(m => m.Key == October);

    private static async Task<PersonPayrollViewModel> CardPayroll(StubApi api, CurrentUser? user = null)
    {
        var team = await RosterTests.Loaded(api, user);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var payroll = team.Card!.Payroll!;
        await RosterTests.Until(() => payroll.IsLoaded);
        return payroll;
    }

    private static CurrentUser Reader() =>
        RosterTests.WithRights(StaffRole.Reception, AccessRight.ManageCatalog, AccessRight.ViewStaffRecords);

    // ---------- Export ----------

    [Fact]
    public async Task Given_der_Oktober_When_die_Inhaberin_den_Lohn_Export_speichert_Then_ist_die_Datei_Byte_fuer_Byte_gleich_dem_bisherigen_Web_Export()
    {
        var api = Api();
        var files = new FakeFileSaver();
        var payroll = await Payroll(api, files: files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == BmdExport);
        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal("lohn-BmdLohn-2026-10.csv", name);
        Assert.Equal(WebExportBytes, content);
        Assert.Equal("lohn-BmdLohn-2026-10.csv gespeichert.", payroll.Message);
        Assert.Null(payroll.Error);
    }

    [Fact]
    public async Task Given_alle_drei_Formate_When_exportiert_wird_Then_fragt_jedes_die_Adresse_mit_seinem_Format_und_Monat()
    {
        var api = Api().OnBytes("GET /api/payroll/export?format=DatevLodas&year=2026&month=10", [1])
            .OnBytes("GET /api/payroll/export?format=DatevLohnUndGehalt&year=2026&month=10", [2]);
        var files = new FakeFileSaver();
        var payroll = await Payroll(api, files: files);
        ChooseOctober(payroll);

        await payroll.ExportLodasCommand.ExecuteAsync(null);
        await payroll.ExportLohnUndGehaltCommand.ExecuteAsync(null);

        Assert.Equal(["lohn-DatevLodas-2026-10.csv", "lohn-DatevLohnUndGehalt-2026-10.csv"], files.Saved.Select(s => s.Name));
        Assert.Equal([1, 2], files.Saved.Select(s => (int)s.Content[0]));
    }

    [Fact]
    public async Task Given_der_Speicherdialog_wird_abgebrochen_When_exportiert_wird_Then_keine_Datei_und_keine_Meldung()
    {
        var files = new FakeFileSaver { Cancels = true };
        var payroll = await Payroll(Api(), files: files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.Empty(files.Saved);
        Assert.Null(payroll.Message);
        Assert.Null(payroll.Error);
    }

    [Fact]
    public async Task Given_keine_Zeile_mit_Personalnummer_und_Lohnart_When_exportiert_wird_Then_steht_der_Weg_statt_einer_leeren_Datei()
    {
        var api = Api(Preview(Line(Marco, PayrollItem.Hours, 8m, "h", null, "100")));
        var files = new FakeFileSaver();
        var payroll = await Payroll(api, files: files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.Empty(files.Saved);
        Assert.DoesNotContain(api.Calls, c => c.Route == BmdExport);
        Assert.Equal(
            "Der Export wurde nicht erstellt, weil keine Zeile übergeben werden könnte. Es fehlt die Personalnummer von Marco Berger. Sie steht auf der Karte der Person im Reiter „Lohn“. Danach den Export erneut starten.",
            payroll.Error);
    }

    [Fact]
    public async Task Given_ein_Monat_ohne_Dienste_When_exportiert_wird_Then_sagt_die_Maske_es_und_erzeugt_keine_Datei()
    {
        var api = Api(Preview());
        var files = new FakeFileSaver();
        var payroll = await Payroll(api, files: files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.Empty(files.Saved);
        Assert.StartsWith("Der Export wurde nicht erstellt: Im Monat gibt es keine Dienste", payroll.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_der_Server_lehnt_den_Export_ab_When_exportiert_wird_Then_steht_sein_Text_in_der_Maske()
    {
        var api = Api().On(BmdExport, new { title = "Der Export ist gerade nicht möglich." }, HttpStatusCode.InternalServerError);
        var files = new FakeFileSaver();
        var payroll = await Payroll(api, files: files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.Empty(files.Saved);
        Assert.False(string.IsNullOrEmpty(payroll.Error));
    }

    // ---------- Vorschau ----------

    [Fact]
    public async Task Given_der_Oktober_When_die_Monatswerte_angezeigt_werden_Then_zeigt_die_Maske_Zeilen_mit_Menge_Einheit_und_Lohnart()
    {
        var payroll = await Payroll(Api());
        ChooseOctober(payroll);

        await payroll.ShowPreviewCommand.ExecuteAsync(null);

        Assert.Equal(3, payroll.Rows.Count);
        Assert.Equal(new PayrollRow("Lena Huber", "4711", "Stunden gesamt", "16,00 h", "100"), payroll.Rows[0]);
        Assert.Equal(new PayrollRow("Lena Huber", "4711", "Urlaub", "2,00 Tage", "200"), payroll.Rows[1]);
        Assert.Equal("", payroll.Rows[2].PersonnelNumber);
        Assert.Contains("ohne Lohnart", new PayrollRow("A", "1", "Urlaub", "1,00 Tage", "").AutomationName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Warnungen_der_API_When_die_Monatswerte_angezeigt_werden_Then_stehen_sie_ueber_der_Tabelle()
    {
        var api = Api(Preview(Line(Lena, PayrollItem.Hours, 8m, "h", "4711", "100")) with { Warnings = ["Marco Berger hat keine Personalnummer."] });
        var payroll = await Payroll(api);
        ChooseOctober(payroll);

        await payroll.ShowPreviewCommand.ExecuteAsync(null);

        Assert.Equal(["Marco Berger hat keine Personalnummer."], payroll.Warnings);
        Assert.True(payroll.HasWarnings);
    }

    [Fact]
    public async Task Given_die_Maske_wird_geoeffnet_Then_ist_der_Vormonat_gewaehlt()
    {
        var api = Api();
        var payroll = await Payroll(api);

        Assert.Equal("Februar 2027", payroll.SelectedMonth.Label);
        Assert.Equal(24, payroll.Months.Count);
        Assert.Equal("Oktober 2026", payroll.Months.Single(m => m.Key == October).Label);
    }

    // ---------- Einstellungen ----------

    [Fact]
    public async Task Given_geaenderte_Lohnart_When_gespeichert_wird_Then_sendet_die_Maske_alle_Einstellungen_an_die_API()
    {
        var api = Api().OnJson("PUT /api/payroll/settings", body => JsonSerializer.Deserialize<PayrollSettingsDto>(body!, ApiClient.Json)!);
        var payroll = await Payroll(api);
        payroll.HoursWageType = "150";
        Assert.True(payroll.HasUnsavedChanges);

        Assert.True(await payroll.SaveChangesAsync());

        var sent = JsonSerializer.Deserialize<PayrollSettingsDto>(Assert.Single(api.Calls, c => c.Route == "PUT /api/payroll/settings").Body!, ApiClient.Json)!;
        Assert.Equal("150", sent.HoursWageType);
        Assert.Equal("110", sent.SundayWageType);
        Assert.Equal("1234567", sent.DatevConsultantNumber);
        Assert.Equal(new TimeOnly(22, 0), sent.NightStart);
        Assert.False(payroll.HasUnsavedChanges);
        Assert.Equal("Einstellungen gespeichert.", payroll.StatusText);
    }

    [Fact]
    public async Task Given_eine_ungueltige_Nachtzeit_When_gespeichert_wird_Then_meldet_das_Feld_den_Fehler_und_nichts_wird_gesendet()
    {
        var api = Api();
        var payroll = await Payroll(api);
        payroll.NightStartText = "abends";

        Assert.False(await payroll.SaveChangesAsync());

        Assert.Equal("Trage die Uhrzeit wie 22:00 ein.", payroll.NightStartError);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_API_lehnt_die_Einstellungen_ab_When_gespeichert_wird_Then_steht_ihre_Meldung_in_der_Maske_und_die_Eingaben_bleiben()
    {
        var api = Api().On("PUT /api/payroll/settings", new { title = "Beraternummer: bis zu 7 Ziffern." }, HttpStatusCode.BadRequest);
        var payroll = await Payroll(api);
        payroll.ConsultantNumber = "123456789";

        Assert.False(await payroll.SaveChangesAsync());

        Assert.Contains("Beraternummer", payroll.SettingsError, StringComparison.Ordinal);
        Assert.Equal("123456789", payroll.ConsultantNumber);
        Assert.True(payroll.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_geaenderte_Einstellungen_When_verworfen_wird_Then_steht_wieder_der_gespeicherte_Stand_da()
    {
        var payroll = await Payroll(Api());
        payroll.HoursWageType = "150";

        payroll.Discard();

        Assert.Equal("100", payroll.HoursWageType);
        Assert.False(payroll.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_das_Backoffice_ohne_Recht_zum_Aendern_When_die_Maske_offen_ist_Then_kann_sie_lesen_und_exportieren_aber_nicht_aendern()
    {
        var files = new FakeFileSaver();
        var payroll = await Payroll(Api(), RosterTests.WithRights(StaffRole.Reception, AccessRight.ViewStaffRecords), files);
        ChooseOctober(payroll);

        await payroll.ExportBmdCommand.ExecuteAsync(null);

        Assert.False(payroll.IsEditable);
        Assert.Single(files.Saved);
    }

    // ---------- Reiter „Lohn“ der Personenkarte ----------

    [Fact]
    public async Task Given_die_Karte_einer_Person_When_der_Reiter_Lohn_gewaehlt_wird_Then_stehen_Personalnummer_und_nur_ihre_Monatswerte_da()
    {
        var api = Api();
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var card = team.Card!;
        Assert.Equal(PersonCardViewModel.PayrollTab, card.SelectedTab);
        Assert.True(card.ShowsPayroll);
        Assert.Contains(card.Tabs, t => t.Id == PersonCardViewModel.PayrollTab && t.Label == "Lohn");
        var payroll = card.Payroll!;
        await RosterTests.Until(() => payroll.IsLoaded);

        payroll.SelectedMonth = payroll.Months.Single(m => m.Key == October);
        await RosterTests.Until(() => payroll.Rows.Count > 0);

        Assert.Equal("4711", payroll.PersonnelNumber);
        Assert.Equal(["Stunden gesamt", "Urlaub"], payroll.Rows.Select(r => r.Item));
        Assert.All(payroll.Rows, r => Assert.Equal("Lena Huber", r.Person));
    }

    [Fact]
    public async Task Given_eine_geaenderte_Personalnummer_When_die_Karte_gespeichert_wird_Then_geht_sie_an_die_API_und_die_Karte_ist_sauber()
    {
        var api = Api().OnJson($"PUT /api/payroll/employees/{Lena.Id}", body =>
        {
            var input = JsonSerializer.Deserialize<PersonnelNumberInput>(body!, ApiClient.Json)!;
            return new PayrollEmployeeDto(Lena.Id, Lena.DisplayName, true, input.PersonnelNumber);
        });
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var card = team.Card!;
        await RosterTests.Until(() => card.Payroll!.IsLoaded);
        card.Payroll!.PersonnelNumber = " 4712 ";
        Assert.True(card.HasUnsavedChanges);
        Assert.True(card.HasSaveBar);

        Assert.True(await card.SaveChangesAsync());

        var sent = JsonSerializer.Deserialize<PersonnelNumberInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/payroll/employees/{Lena.Id}").Body!, ApiClient.Json)!;
        Assert.Equal("4712", sent.PersonnelNumber);
        Assert.False(card.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", card.StatusText);
    }

    [Fact]
    public async Task Given_eine_leere_Personalnummer_When_gespeichert_wird_Then_sendet_die_Karte_null_und_entfernt_sie()
    {
        var api = Api().OnJson($"PUT /api/payroll/employees/{Lena.Id}", _ => new PayrollEmployeeDto(Lena.Id, Lena.DisplayName, true, null));
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var card = team.Card!;
        await RosterTests.Until(() => card.Payroll!.IsLoaded);
        card.Payroll!.PersonnelNumber = "  ";

        Assert.True(await card.SaveChangesAsync());

        Assert.Null(JsonSerializer.Deserialize<PersonnelNumberInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/payroll/employees/{Lena.Id}").Body!, ApiClient.Json)!.PersonnelNumber);
    }

    [Fact]
    public async Task Given_die_API_kennt_die_Personalnummer_schon_When_die_Karte_gespeichert_wird_Then_bleibt_die_Karte_im_Reiter_Lohn_und_nennt_den_Grund()
    {
        var api = Api().On($"PUT /api/payroll/employees/{Lena.Id}",
            new { title = "Fehler", errors = new Dictionary<string, string[]> { ["PersonnelNumber"] = ["Diese Personalnummer hat schon jemand."] } }, HttpStatusCode.BadRequest);
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var card = team.Card!;
        await RosterTests.Until(() => card.Payroll!.IsLoaded);
        card.SelectTab(PersonCardViewModel.DetailsTab);
        card.Payroll!.PersonnelNumber = "4712";

        Assert.False(await card.SaveChangesAsync());

        Assert.Equal(PersonCardViewModel.PayrollTab, card.SelectedTab);
        Assert.Contains("Diese Personalnummer hat schon jemand.", card.Error, StringComparison.Ordinal);
        Assert.True(card.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ungespeicherte_Personalnummer_When_die_Karte_verworfen_wird_Then_steht_die_alte_Nummer_da()
    {
        var payroll = await CardPayroll(Api());
        payroll.PersonnelNumber = "9999";

        payroll.Discard();

        Assert.Equal("4711", payroll.PersonnelNumber);
        Assert.False(payroll.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_die_Nummern_lassen_sich_nicht_laden_When_der_Reiter_Lohn_offen_ist_Then_bleibt_das_Feld_gesperrt_und_es_gibt_Erneut_laden()
    {
        var api = Api().On("GET /api/payroll/employees", new { title = "kaputt" }, HttpStatusCode.InternalServerError);
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);
        var payroll = team.Card!.Payroll!;
        await RosterTests.Until(() => payroll.HasLoadError);

        Assert.False(payroll.IsLoaded);
        Assert.False(payroll.CanEditNumber);
        Assert.False(payroll.HasUnsavedChanges);
        Assert.NotNull(payroll.ReloadCommand);
    }

    [Fact]
    public async Task Given_nur_Leserecht_auf_Personalunterlagen_When_die_Karte_offen_ist_Then_ist_die_Personalnummer_nur_lesbar()
    {
        var payroll = await CardPayroll(Api(), Reader());

        Assert.False(payroll.IsEditable);
        Assert.Equal("4711", payroll.PersonnelNumber);
    }

    [Fact]
    public async Task Given_kein_Recht_auf_den_Lohn_When_die_Karte_offen_ist_Then_gibt_es_weder_Reiter_noch_Ansicht()
    {
        var team = await RosterTests.Loaded(Api(), RosterTests.WithRights(StaffRole.Manager, AccessRight.ManageCatalog));
        team.Open(Lena.Id, PersonCardViewModel.PayrollTab, null);

        Assert.Null(team.Card!.Payroll);
        Assert.DoesNotContain(team.Card.Tabs, t => t.Id == PersonCardViewModel.PayrollTab);
        Assert.DoesNotContain(team.Modes, m => m.Id == TeamViewModel.PayrollMode);
        Assert.Null(team.Payroll);
    }

    [Fact]
    public async Task Given_ein_Sprung_in_den_Lohn_When_Team_offen_ist_Then_wechselt_die_Leiste_zur_Ansicht_Lohn()
    {
        var team = await RosterTests.Loaded(Api());

        team.Open(null, TeamViewModel.PayrollMode, null);
        await RosterTests.Until(() => team.ShowsPayroll);

        Assert.Equal("Lohn", team.Modes.Single(m => m.Id == TeamViewModel.PayrollMode).Label);
        Assert.True(team.ShowsPayrollView);
    }

    [Fact]
    public async Task Given_ungespeicherte_Einstellungen_When_die_Ansicht_gewechselt_wird_Then_fragt_das_Team_vorher()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var api = Api();
        var team = await RosterTests.Loaded(api, dialogs: dialogs);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PayrollMode));
        await RosterTests.Until(() => team.Payroll!.IsLoaded);
        team.Payroll!.HoursWageType = "150";

        Assert.False(await team.SwitchToAsync(TeamViewModel.PeopleMode));

        Assert.Equal(TeamViewModel.PayrollMode, team.Mode);
        Assert.Equal(["Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }
}

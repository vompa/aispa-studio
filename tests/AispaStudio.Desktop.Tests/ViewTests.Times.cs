using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 99 (Ä191, Ä250, ADR 0084): Zeiten im Bereich „Team“ (Ansicht „Zeiten“, Reiter der Personenkarte) mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly DateOnly TimesTuesday = SpaSample.Day.AddDays(1);
    private static readonly DateOnly TimesWednesday = SpaSample.Day.AddDays(2);

    /// <summary>Wie <see cref="RosterApi"/>, mit zwei Abschnitten für Lena am Dienstag (einer selbst gestempelt, einer nachgetragen) und dem Hinweis der API.</summary>
    private static (StubApi Api, RosterSample Roster, TimeRecordDto Manual) TimesApi()
    {
        var (api, roster) = RosterApi();
        roster.AddRecord(RosterLena, TimesTuesday, new TimeOnly(9, 0), new TimeOnly(12, 30));
        var manual = roster.AddRecord(RosterLena, TimesTuesday, new TimeOnly(13, 0), new TimeOnly(17, 30), TimeRecordSource.Manual);
        return (api, roster, manual);
    }

    private static T VisibleNamed<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(c => c.Name == name && c.IsEffectivelyVisible);

    /// <summary>Team öffnen und die Ansicht „Zeiten“ anklicken.</summary>
    private static async Task<TeamViewModel> OpenTimes(Window window, MainViewModel main)
    {
        var team = await OpenTeam(window, main);
        await ClickSection(window, "Zeiten");
        Assert.True(team.ShowsTimes);
        await Pump(Settling);
        Assert.True(team.Times!.IsLoaded);
        return team;
    }

    private static Button TimesCellButton(Window window, string person, string day) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("tcell") && b.IsEffectivelyVisible
            && (AutomationProperties.GetName(b) ?? "").StartsWith($"{person}, {day}", StringComparison.Ordinal));

    private static async Task ClickTimesCell(Window window, string person, string day)
    {
        var cell = TimesCellButton(window, person, day);
        cell.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, cell);
        await Pump(Settling);
    }

    private static async Task TypeIntoVisibleTime(Window window, string name, string text)
    {
        var box = window.GetVisualDescendants().OfType<TimeField>().First(f => f.IsEffectivelyVisible && AutomationProperties.GetName(f) == name);
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        await Pump();
    }

    private static async Task TypeIntoReason(Window window, string text)
    {
        var box = VisibleNamed<TextBox>(window, "TimeReason");
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        await Pump();
    }

    /// <summary>Ein Reiter der Personenkarte; „Zeiten“ gibt es auch als Ansicht des Teams, deshalb nur in der Leiste der Karte suchen.</summary>
    private static async Task ClickCardTab(Window window, string label)
    {
        var tab = Named<Border>(window, "CardTabs").GetVisualDescendants().OfType<TabButton>().First(t => t.Content as string == label);
        ClickCenter(window, tab);
        await Pump(Settling);
    }

    private static TimeRecordInput RecordBody(StubApi api, string route) =>
        JsonSerializer.Deserialize<TimeRecordInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Zeiten_zeigt_die_Woche_mit_Soll_Ist_und_dem_Umschalter(string theme)
    {
        var (api, roster, _) = TimesApi();
        roster.TimeWarning = "Pause kürzer als 30 Minuten.";
        return Render(theme, "verwaltung-team-zeiten", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");

            Assert.Equal(OwnerModes, window.GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => t.Content as string).Where(OwnerModes.Contains));
            Assert.True(Shows(window, "Zeiten ab Mo. 19.10."));
            Assert.True(Shows(window, "8 / 8"));
            Assert.True(Shows(window, "Soll 8 h · Ist 8 h · Pause 30 min"));
            Assert.True(Shows(window, "09:00–12:30"));
            Assert.True(Shows(window, "13:00–17:30"));
            Assert.True(Shows(window, "Pause kürzer als 30 Minuten."));
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "Footer").IsEffectivelyVisible);
            Assert.Equal("Lena Huber, Di. 20.10.", VisibleNamed<TextBlock>(window, "TimeDayHeading").Text);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Abschnitt_When_die_Managerin_ohne_Grund_Strg_S_drueckt_Then_steht_die_Meldung_am_Feld_und_es_geht_kein_Aufruf_raus()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-grund", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");
            var change = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Abschnitt 09:00–12:30 ändern");
            await ClickButton(window, change);

            await TypeIntoVisibleTime(window, "Ende", "13:00");
            PressSave(window);
            await Pump(Settling);

            Assert.True(Shows(window, "Grund angeben."));
            Assert.DoesNotContain(api.Calls, c => c.Route.Contains("time-records", StringComparison.Ordinal) && !c.Route.StartsWith("GET ", StringComparison.Ordinal));
            Assert.True(team.HasUnsavedChanges);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Tag_ohne_Zeiten_When_die_Managerin_22_bis_2_Uhr_mit_Grund_eintippt_und_Strg_S_drueckt_Then_endet_der_Abschnitt_am_Folgetag()
    {
        var (api, roster, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-folgetag", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Mi.");
            Assert.True(VisibleNamed<Control>(window, "TimeEditor").IsEffectivelyVisible);

            await TypeIntoVisibleTime(window, "Beginn", "22:00");
            await TypeIntoVisibleTime(window, "Ende", "02:00");
            Assert.True(Shows(window, "Endet am Folgetag."));
            await TypeIntoReason(window, "Nachtdienst");
            Assert.True(team.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var input = RecordBody(api, $"POST /api/locations/{SpaSample.LocationId}/time-records");
            Assert.Equal(RosterLena.Id, input.EmployeeId);
            Assert.Equal("Nachtdienst", input.Reason);
            Assert.Equal(TimeSpan.FromHours(4), input.End - input.Start);
            Assert.Equal(TimesWednesday, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(input.Start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime));
            Assert.False(team.HasUnsavedChanges);
            Assert.True(Shows(window, "22:00–02:00"));
            Assert.Single(roster.Records, r => r.End - r.Start == TimeSpan.FromHours(4));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Abschnitt_mit_Grund_When_die_Managerin_Entfernen_klickt_und_bestaetigt_Then_geht_DELETE_mit_dem_Grund_raus()
    {
        var (api, _, manual) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-entfernen", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");
            var change = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Abschnitt 13:00–17:30 ändern");
            await ClickButton(window, change);
            await TypeIntoReason(window, "doppelt gestempelt");

            await ClickButton(window, VisibleNamed<Button>(window, "RemoveTimeButton"));

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Abschnitt entfernen?", dialog.Title);
            Assert.Contains("13:00–17:30", dialog.Message, StringComparison.Ordinal);
            dialog.ConfirmCommand.Execute(null);
            await Pump(Settling);

            var call = Assert.Single(api.Calls, c => c.Route == $"DELETE /api/locations/{SpaSample.LocationId}/time-records/{manual.Id}");
            Assert.Equal("doppelt gestempelt", JsonSerializer.Deserialize<TimeRecordDeletion>(call.Body!, ApiClient.Json)!.Reason);
            Assert.False(Shows(window, "13:00–17:30"));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_nachgetragener_Abschnitt_When_die_Managerin_Verlauf_klickt_Then_steht_das_Protokoll_unter_dem_Abschnitt()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-verlauf", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Di.");
            var history = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Verlauf von Abschnitt 13:00–17:30");

            await ClickButton(window, history);

            Assert.True(Shows(window, "Marco Berger nachgetragen: Stempeln vergessen"));
            return team;
        }, api);
    }

    private static CurrentUser Backoffice() => new("u3", "backoffice@demo.aispa.test", "Bea Hofer", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Backoffice, true, []), null, [AccessRight.ViewCalendar, AccessRight.ViewStaffRecords]);

    [Fact]
    public Task Given_Backoffice_mit_ViewStaffRecords_When_sie_die_Zeiten_oeffnet_Then_sind_sie_nur_lesbar_aber_mit_Verlauf()
    {
        var (api, _, _) = TimesApi();
        api.On("GET /api/auth/me", Backoffice());
        return Render("hell", "verwaltung-team-zeiten-lesen", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickSection(window, "Zeiten");
            await Pump(Settling);
            Assert.True(team.ShowsTimes);
            await ClickTimesCell(window, "Lena Huber", "Di.");

            Assert.True(Shows(window, "09:00–12:30"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string is "Ändern" or "Zeit nachtragen");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "TimeEditor" && c.IsEffectivelyVisible);
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Verlauf");
            Assert.False(team.Times!.IsEditable);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Verwaltung_When_der_Sprung_zur_Zeiterfassung_kommt_Then_steht_Team_in_der_Ansicht_Zeiten()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-sprung", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);

            shell.AdminPage.Show("time");
            await Pump(Settling);

            Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
            var team = shell.AdminPage.Team!;
            Assert.True(team.ShowsTimes);
            Assert.True(VisibleNamed<Control>(window, "TimesScroll").IsEffectivelyVisible);
            Assert.True(Shows(window, "Zeiten ab Mo. 19.10."));
            Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Zeiten");
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Zeiten_When_auf_den_Namen_geklickt_wird_Then_oeffnet_die_Karte_im_Reiter_Zeiten()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-name", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            var name = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("link") && b.IsEffectivelyVisible && b.Content as string == "Lena Huber");

            await ClickButton(window, name);
            await Pump(Settling);

            Assert.True(team.ShowsPeople);
            Assert.Equal("Lena Huber", team.Card?.Title);
            Assert.Equal(PersonCardViewModel.TimesTab, team.Card?.SelectedTab);
            Assert.True(Named<Control>(window, "TimesSection").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Lena_gewaehlt_When_der_Reiter_Zeiten_geklickt_wird_Then_zeigt_die_Karte_nur_ihre_Woche()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-karte-zeiten", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await Pump(Settling);
            var card = team.Card!;
            Assert.True(card.Times!.IsLoaded);

            Assert.True(Named<Control>(window, "TimesSection").IsEffectivelyVisible);
            Assert.True(Shows(window, "8 / 8"));
            Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{SpaSample.LocationId}/timesheet?from=2026-10-19&employeeId={RosterLena.Id}");
            Assert.False(card.HasSaveBar);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_ungespeicherter_Abschnitt_When_auf_Personen_geklickt_wird_Then_fragt_die_Maske()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-zeiten-frage", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickTimesCell(window, "Lena Huber", "Mi.");
            await TypeIntoReason(window, "Nachtrag");

            await ClickSection(window, "Personen");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Contains("Zeiten", dialog.Message, StringComparison.Ordinal);
            Assert.True(team.ShowsTimes);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_ungespeicherter_Abschnitt_auf_der_Karte_When_eine_andere_Person_angeklickt_wird_Then_fragt_die_Maske()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-team-karte-zeiten-frage", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await ClickTimesCell(window, "Lena Huber", "Mi.");
            await TypeIntoReason(window, "Nachtrag");

            await ClickRow(window, "Marco Berger");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Contains("Lena Huber", dialog.Message, StringComparison.Ordinal);
            Assert.Equal("Lena Huber", team.Card?.Title);
            return team;
        }, api);
    }
}

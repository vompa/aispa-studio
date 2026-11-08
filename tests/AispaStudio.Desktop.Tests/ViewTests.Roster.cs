using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 99 (Ä191, Ä250, ADR 0084): Wochenplan, Tageseditor, Reiter der Personenkarte und Anträge im Bereich „Team“, mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] OwnerModes = ["Personen", "Wochenplan", "Zeiten", "Anträge"];

    private static readonly DateOnly RosterTuesday = SpaSample.Day.AddDays(1);

    /// <summary>Team wie <see cref="TeamApi"/> mit dem Stand des Dienstplans zum Prüfen (Lena arbeitet dienstags 9–17 Uhr).</summary>
    private static (StubApi Api, RosterSample Roster) RosterApi()
    {
        var api = TeamApi();
        return (api, new RosterSample(api, SpaSample.Vienna, SpaSample.Day, RosterLena, SpaSample.Marco));
    }

    /// <summary>Team öffnen und die Ansicht „Wochenplan“ anklicken.</summary>
    private static async Task<TeamViewModel> OpenWeekPlan(Window window, MainViewModel main)
    {
        var team = await OpenTeam(window, main);
        await ClickSection(window, "Wochenplan");
        Assert.True(team.ShowsRoster);
        await Pump(Settling);
        Assert.True(team.Plan!.IsLoaded);
        return team;
    }

    private static Button RosterCellButton(Window window, string person, string day) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("rcell") && b.IsEffectivelyVisible
            && (AutomationProperties.GetName(b) ?? "").StartsWith($"{person}, {day}", StringComparison.Ordinal));

    private static async Task ClickCell(Window window, string person, string day)
    {
        var cell = RosterCellButton(window, person, day);
        cell.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, cell);
        await Pump(Settling);
    }

    private static async Task ClickButton(Window window, Control button)
    {
        button.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, button);
        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Wochenplan_zeigt_die_Woche_mit_Lena_und_den_Umschalter(string theme)
    {
        var (api, _) = RosterWeekApi();
        return Render(theme, "verwaltung-team-wochenplan", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            Assert.Equal(OwnerModes, window.GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => t.Content as string).Where(OwnerModes.Contains));
            Assert.True(Shows(window, "Woche ab Mo. 19.10."));
            Assert.True(Shows(window, "09:00–17:00"));
            Assert.Contains("Lena Huber, Di. 20.10.", AutomationProperties.GetName(RosterCellButton(window, "Lena Huber", "Di.")), StringComparison.Ordinal);
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "Footer").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Lena_Dienstag_9_bis_17_When_die_Managerin_12_bis_20_eintippt_und_Strg_S_drueckt_Then_geht_genau_dieser_Dienst_an_die_API()
    {
        var (api, roster) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-tag", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            Assert.True(Named<Control>(window, "DayEditor").IsEffectivelyVisible);
            Assert.Equal("Lena Huber, Di. 20.10.", Named<TextBlock>(window, "DayHeading").Text);

            await TypeIntoTime(window, "Dienst 1 Beginn", "12:00");
            await TypeIntoTime(window, "Dienst 1 Ende", "20:00");
            await ClickButton(window, Named<CheckBox>(window, "ImmediateBox"));
            Assert.True(team.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/locations/{SpaSample.LocationId}/roster/{RosterLena.Id}/{RosterTuesday:yyyy-MM-dd}");
            var body = JsonSerializer.Deserialize<RosterDayInput>(put.Body!, ApiClient.Json)!;
            Assert.Equal(new RosterEntry(RosterKind.Work, SpaSample.LocationId, new TimeOnly(12, 0), new TimeOnly(20, 0)), Assert.Single(body.Entries));
            Assert.False(body.Draft);
            Assert.True(Shows(window, RosterDayEditorViewModel.Saved));
            Assert.False(team.HasUnsavedChanges);
            Assert.NotNull(roster.PlannedCell(RosterLena, RosterTuesday));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Urlaub_mit_drei_gebuchten_Terminen_When_die_Managerin_klickt_und_speichert_Then_warnt_die_Maske_mit_der_Zahl_3()
    {
        var (api, roster) = RosterApi();
        roster.Outside = 3;
        return Render("hell", "verwaltung-team-wochenplan-warnung", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");

            await ClickSection(window, "Urlaub");
            await ClickButton(window, Named<CheckBox>(window, "ImmediateBox"));
            PressSave(window);
            await Pump(Settling);

            Assert.True(Shows(window, "3 Termine liegen jetzt außerhalb des Dienstes."));
            Assert.True(Shows(window, "Gast 1"));
            Assert.True(Named<Button>(window, "SuggestButton").IsEffectivelyVisible);
            var put = Assert.Single(api.Calls, c => c.Route.StartsWith("PUT /api/locations/", StringComparison.Ordinal));
            Assert.Equal(RosterKind.Vacation, Assert.Single(JsonSerializer.Deserialize<RosterDayInput>(put.Body!, ApiClient.Json)!.Entries).Kind);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Lena_gewaehlt_When_der_Reiter_Dienstplan_und_Abwesenheit_geklickt_wird_Then_zeigt_die_Karte_nur_ihre_Woche()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-karte-dienstplan", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickSection(window, "Dienstplan und Abwesenheit");
            await Pump(Settling);
            var card = team.Card!;
            Assert.True(card.Roster!.IsLoaded);

            Assert.True(Named<Control>(window, "RosterSection").IsEffectivelyVisible);
            Assert.True(Shows(window, "09:00–17:00"));
            Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{SpaSample.LocationId}/roster?from=2026-10-19&employeeId={RosterLena.Id}");
            Assert.False(card.HasSaveBar);
            await ClickCell(window, "Lena Huber", "Di.");
            Assert.True(Named<Control>(window, "DayEditor").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Wochenplan_When_auf_den_Namen_geklickt_wird_Then_oeffnet_die_Karte_im_Reiter_Dienstplan()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-name", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            var name = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("link") && b.IsEffectivelyVisible && b.Content as string == "Lena Huber");

            await ClickButton(window, name);

            Assert.True(team.ShowsPeople);
            Assert.Equal("Lena Huber", team.Card?.Title);
            Assert.Equal(PersonCardViewModel.RosterTab, team.Card?.SelectedTab);
            Assert.True(Named<Control>(window, "RosterSection").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_offener_Tag_mit_Aenderung_When_auf_Personen_geklickt_wird_Then_fragt_die_Maske()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-frage", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            await TypeInto(window, "DayNote", "Dienst getauscht mit Marco");

            await ClickSection(window, "Personen");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.True(team.ShowsRoster);
            Assert.Contains("Dienstplan", dialog.Message, StringComparison.Ordinal);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Antraege_When_genehmigt_mit_Terminen_Then_fragt_die_Maske_mit_der_Zahl_und_POST_approve()
    {
        var (api, _) = RosterApi();
        var request = RosterSample.Request(RosterLena, RosterKind.Vacation, 8, 12, affected: 3, note: "Familienfeier");
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request }).On($"POST /api/absence-requests/{request.Id}/approve", null);
        return Render("hell", "verwaltung-team-antraege", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickSection(window, "Anträge");
            Assert.True(Shows(window, "Urlaub, 8.3.2027 bis 12.3.2027"));
            Assert.True(Shows(window, "In dieser Zeit liegen 3 Termine."));

            var approve = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Genehmigen");
            await ClickButton(window, approve);
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Abwesenheit genehmigen?", dialog.Title);
            Assert.Contains("3 Termine von Lena Huber", dialog.Message, StringComparison.Ordinal);
            dialog.ConfirmCommand.Execute(null);
            await Pump(Settling);

            Assert.Single(api.Calls, c => c.Route == $"POST /api/absence-requests/{request.Id}/approve");
            Assert.True(Shows(window, "Es gibt keine offenen Anträge."));
            return team;
        }, api);
    }

    [Fact]
    public Task Given_keine_Antrags_Route_When_die_Inhaberin_Antraege_klickt_Then_steht_der_Leerzustand_da()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-antraege-leer", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickSection(window, "Anträge");

            Assert.True(Shows(window, "Es gibt keine offenen Anträge."));
            Assert.False(team.Requests!.HasLoadError);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_Rezeption_When_sie_Team_oeffnet_Then_steht_nur_der_Wochenplan_da_und_die_Zellen_sind_nicht_klickbar()
    {
        var (api, _) = RosterApi();
        api.On("GET /api/auth/me", Reception());
        return Render("hell", "verwaltung-team-wochenplan-rezeption", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await Pump(Settling);

            Assert.True(team.ShowsRoster);
            Assert.False(Named<Control>(window, "TeamModes").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            Assert.True(Shows(window, "09:00–17:00"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("rcell") && b.IsEffectivelyVisible);
            var cell = window.GetVisualDescendants().OfType<ContentControl>().First(c => c.Classes.Contains("rcell") && c is not Button && c.IsEffectivelyVisible
                && (AutomationProperties.GetName(c) ?? "").StartsWith("Lena Huber, Di.", StringComparison.Ordinal));
            Assert.Equal(1, cell.Opacity);
            Assert.False(cell.Focusable);
            var peer = ControlAutomationPeer.CreatePeerForElement(cell);
            Assert.StartsWith("Lena Huber, Di. 20.10.: 09:00–17:00", peer.GetName(), StringComparison.Ordinal);
            Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
            ClickCenter(window, cell);
            await Pump(Settling);
            Assert.Null(team.Plan!.Day);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("link") && b.IsEffectivelyVisible && b.Content as string == "Lena Huber");
            // Die eigenen Erinnerungen lädt der Kalender für jede Person (Slice 117); Personaldaten anderer bleiben ungeladen.
            Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/staff", StringComparison.Ordinal)
                && !c.Route.StartsWith("GET /api/staff-duties/reminders", StringComparison.Ordinal));
            return team;
        }, api);
    }
}

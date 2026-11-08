using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 111 (Ä252): Reiter „Regelungen und Vollmachten“, „Wer darf was?“ und die Warnung im Tageseditor mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static Button ButtonNamed(Window window, string content) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == content);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Regelungen_erteilt_eine_Vollmacht_und_zeigt_Kennzeichen_und_abgelaufene_Eintraege(string theme)
    {
        var (api, _) = RosterApi();
        var path = $"/api/employees/{RosterLena.Id}/regulations";
        var powers = new List<StaffPowerDto> { new(Guid.NewGuid(), RosterLena.Id, StaffPowerKind.Safe, "Tresor", new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30), "Katrin", true) };
        api.OnJson($"GET {path}", _ => new StaffRegulationsDto(
            [new StaffNoteDto(Guid.NewGuid(), RosterLena.Id, "Spricht Italienisch", StaffNoteVisibility.Team, true, null, null, false)],
            [new StaffRuleDto(Guid.NewGuid(), RosterLena.Id, StaffRuleKind.NoAppointmentsAfter, "Keine Termine nach 16:00", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), new TimeOnly(16, 0), null, [], "Kinderbetreuung", false)],
            powers.ToList()));
        api.OnJson($"POST {path}/powers", body =>
        {
            var input = JsonSerializer.Deserialize<StaffPowerInput>(body!, ApiClient.Json)!;
            var power = new StaffPowerDto(Guid.NewGuid(), RosterLena.Id, input.Kind, "Kassa-Storno", input.ValidFrom, input.ValidUntil, "Katrin", false);
            powers.Add(power);
            return power;
        });
        api.On("GET /api/staff-regulations/flags", new List<StaffFlagsDto> { new(RosterLena.Id, ["Spricht Italienisch"], ["Keine Termine nach 16:00"], []) });
        return Render(theme, "verwaltung-team-person-regelungen", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            Assert.True(Shows(window, "Sonderregelung: Keine Termine nach 16:00"));
            await ClickCardTab(window, "Regelungen und Vollmachten");
            await Pump(Settling);
            Assert.True(Shows(window, "Tresor (abgelaufen)"));
            Assert.True(Shows(window, "Grund: Kinderbetreuung"));

            await ClickButton(window, VisibleNamed<Button>(window, "AddPowerButton"));
            await Pump(Settling);
            Assert.Equal(VisibleNamed<ComboBox>(window, "PowerKindField"), TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());
            PressSave(window);
            await Pump(Settling);

            Assert.Equal(StaffPowerKind.CashVoid, powers[^1].Kind);
            Assert.False(team.Card!.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            return team;
        }, api);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Wer_darf_was_zeigt_je_Vollmacht_wer_und_bis_wann(string theme)
    {
        var (api, _) = RosterApi();
        api.On("GET /api/staff-regulations/powers", new List<PowerOverviewRowDto>
        {
            new(StaffPowerKind.CashVoid, "Kassa-Storno", true, [new(RosterLena.Id, "Lena", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Katrin", false)]),
            new(StaffPowerKind.Safe, "Tresor", false, [new(RosterLena.Id, "Lena", new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30), "Katrin", true)]),
            new(StaffPowerKind.Keys, "Schlüssel", false, []),
        });
        return Render(theme, "verwaltung-team-wer-darf-was", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickSection(window, "Wer darf was?");
            await Pump(Settling);

            Assert.True(Named<Control>(window, "PowerOverview").IsEffectivelyVisible);
            Assert.True(Shows(window, "Lena, bis 31.12.2026, erteilt von Katrin"));
            Assert.True(Shows(window, "Lena, abgelaufen am 30.9.2026, erteilt von Katrin"));
            Assert.True(Shows(window, "Niemand"));
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_die_Regel_When_die_Managerin_im_Tageseditor_speichert_Then_warnt_die_Maske_und_der_Klick_auf_Trotzdem_speichern_schickt_die_Bestaetigung()
    {
        var (api, _) = RosterApi();
        var route = $"PUT /api/locations/{SpaSample.LocationId}/roster/{RosterLena.Id}/{RosterTuesday:yyyy-MM-dd}";
        api.On(route, new BookingRejected("Lena Huber hat eine Sonderregelung: keine Termine nach 16:00.", [], StaffRule: true), HttpStatusCode.Conflict);
        return Render("hell", "verwaltung-team-wochenplan-sonderregelung", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            await TypeIntoTime(window, "Dienst 1 Ende", "18:00");
            PressSave(window);
            await Pump(Settling);

            Assert.True(Named<Control>(window, "RuleWarningInfo").IsEffectivelyVisible);
            Assert.True(Shows(window, "Lena Huber hat eine Sonderregelung: keine Termine nach 16:00."));
            Assert.Single(api.Calls, c => c.Route == route);
            Assert.False(JsonSerializer.Deserialize<RosterDayInput>(api.Calls.Last(c => c.Route == route).Body!, ApiClient.Json)!.RulesConfirmed);

            await ClickButton(window, ButtonNamed(window, "Trotzdem speichern"));

            Assert.Equal(2, api.Calls.Count(c => c.Route == route));
            Assert.True(JsonSerializer.Deserialize<RosterDayInput>(api.Calls.Last(c => c.Route == route).Body!, ApiClient.Json)!.RulesConfirmed);
            return team;
        }, api);
    }
}

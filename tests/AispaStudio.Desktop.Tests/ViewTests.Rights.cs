using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Masken-Überarbeitung P11: Tabelle „Wer darf was, wer ist zuständig?“ als Inhaberin, mit echtem Klick; Bilder fürs Handbuch und den PR.</summary>
public sealed partial class ViewTests
{
    private static StubApi RightsApi()
    {
        var (api, _) = RosterApi();
        return api
            .On("GET /api/staff-regulations/powers", new List<PowerOverviewRowDto>
            {
                new(StaffPowerKind.CashVoid, "Kassa-Storno", true, [new(RosterLena.Id, "Lena Huber", new DateOnly(2026, 1, 1), null, "Katrin", false) { EntryId = Guid.NewGuid() }])
                {
                    Group = "Kassa und Geld", GroupOrder = 1, Hint = "Belege stornieren", Effect = "Schaltet frei", IsUnstaffed = false,
                },
                new(StaffPowerKind.CashClosing, "Kassa-Abschluss", false, [new(RosterLena.Id, "Lena Huber", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Katrin", false) { EntryId = Guid.NewGuid() }])
                {
                    Group = "Kassa und Geld", GroupOrder = 1, Hint = "Tagesabschluss", Effect = "Nur Aushang", IsUnstaffed = false,
                },
                new(StaffPowerKind.Safe, "Tresor", false, []) { Group = "Kassa und Geld", GroupOrder = 1, Hint = "Tresor öffnen und befüllen", Effect = "Nur Aushang", IsUnstaffed = true },
                new(StaffPowerKind.AcceptDeliveries, "Lieferungen annehmen", false, []) { Group = "Lager", GroupOrder = 2, Hint = "Lieferungen entgegennehmen", Effect = "Nur Aushang", IsUnstaffed = true },
                new(StaffPowerKind.TriggerOrders, "Bestellungen auslösen", true, [new(SpaSample.Marco.Id, "Marco Berger", new DateOnly(2026, 1, 1), null, "Katrin", false) { EntryId = Guid.NewGuid() }])
                {
                    Group = "Lager", GroupOrder = 2, Hint = "Bestellungen an Lieferanten senden", Effect = "Schaltet frei", IsUnstaffed = false,
                },
                new(StaffPowerKind.DeputyManagement, "Vertretung der Leitung", false, [])
                {
                    Group = "Sicherheit und Vertretung", GroupOrder = 3, Hint = "Vertritt die Leitung", Effect = "Nur Aushang", IsUnstaffed = true,
                },
            })
            .On("GET /api/staff-duties/overview", new List<DutyOverviewRowDto>
            {
                new(DutyFirstAid.Id, DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs",
                    [new(Guid.NewGuid(), RosterLena.Id, "Lena Huber", null, null, new DateOnly(2026, 1, 1), null, false), new(Guid.NewGuid(), SpaSample.Marco.Id, "Marco Berger", null, null, new DateOnly(2026, 1, 1), null, false)])
                {
                    Group = "Sicherheit und Vertretung", GroupOrder = 3, Hint = "Verlangt: Erste-Hilfe-Kurs", Effect = "Warnt im Dienstplan", IsUnstaffed = false,
                },
            })
            .On("GET /api/staff-duties/types", new List<DutyTypeDto>())
            .On("GET /api/staff-duties/expiring", new List<ExpiringCertificateDto>());
    }

    private static Button ButtonByName(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);

    private static async Task<TeamViewModel> OpenRights(Window window, MainViewModel main)
    {
        var team = await OpenTeam(window, main);
        await ClickSection(window, "Rechte und Zuständigkeiten");
        await Pump(Settling);
        Assert.True(Named<Control>(window, "PowerOverview").IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
        return team;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Wer_darf_was_zeigt_eine_Tabelle_nach_Themen_mit_Personen_in_der_Zeile(string theme) =>
        Render(theme, "verwaltung-team-wer-darf-was", async (window, main) =>
        {
            var team = await OpenRights(window, main);

            Assert.True(Shows(window, "Wer darf was, wer ist zuständig?"));
            Assert.True(Shows(window, "Kassa und Geld"));
            Assert.True(Shows(window, "Sicherheit und Vertretung"));
            Assert.True(Shows(window, "Belege stornieren"));
            Assert.True(Shows(window, "Alle 7"));
            Assert.True(Shows(window, "Unbesetzt 3"));
            Assert.True(Shows(window, "Schaltet etwas frei 2"));
            Assert.True(Shows(window, "Niemand eingetragen"));
            Assert.False(Shows(window, "Prüft die API zusätzlich zur Rolle"));
            Assert.NotNull(ButtonByName(window, "Person für Tresor eintragen"));
            return team;
        }, RightsApi());

    [Theory]
    [InlineData(1100, 680, "schmal")]
    [InlineData(1440, 900, "breit")]
    public Task Verwaltung_Team_Wer_darf_was_bleibt_bei_jeder_Fensterbreite_im_Fenster(double width, double height, string name) =>
        Render("hell", $"verwaltung-team-wer-darf-was-{name}", async (window, main) =>
        {
            await SetSize(window, width, height);
            var team = await OpenRights(window, main);

            var groups = Named<ItemsControl>(window, "RightsGroups");
            Assert.True(groups.TranslatePoint(new Point(groups.Bounds.Width, 0), window)!.Value.X <= window.Bounds.Width + 0.5, "Die Tabelle ragt über den Fensterrand");
            Assert.True(groups.Bounds.Width >= 640, $"Tabelle {groups.Bounds.Width:0} px");
            return team;
        }, RightsApi());

    [Fact]
    public Task Verwaltung_Team_Wer_darf_was_Filter_Unbesetzt_zeigt_nur_die_Zeilen_ohne_Person() =>
        Render("hell", "verwaltung-team-wer-darf-was-unbesetzt", async (window, main) =>
        {
            var team = await OpenRights(window, main);
            await ClickButton(window, ButtonByName(window, "Unbesetzt 3"));
            await Pump(Settling);

            Assert.True(Shows(window, "Tresor"));
            Assert.False(Shows(window, "Belege stornieren"));
            Assert.Equal(3, team.Powers!.Groups.Sum(g => g.Rows.Count));
            return team;
        }, RightsApi());

    [Fact]
    public Task Verwaltung_Team_Wer_darf_was_Plus_Person_oeffnet_die_Auswahl_und_traegt_die_Person_ueber_den_Endpunkt_der_Karte_ein()
    {
        var api = RightsApi();
        return Render("hell", "verwaltung-team-wer-darf-was-plus-person", async (window, main) =>
        {
            var team = await OpenRights(window, main);
            foreach (var id in team.Entries.Select(e => e.EmployeeId).OfType<Guid>())
            {
                api.On($"POST /api/employees/{id}/regulations/powers", new StaffPowerDto(Guid.NewGuid(), id, StaffPowerKind.Safe, "Tresor", new DateOnly(2026, 11, 17), null, "Katrin", false));
            }

            var tresor = team.Powers!.Groups.SelectMany(g => g.Rows).Single(r => r.Name == "Tresor");
            ClickCenter(window, ButtonByName(window, "Person für Tresor eintragen"));
            await Pump(Settling);

            var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible).ToList();
            Assert.Equal(tresor.Candidates.Select(c => c.Name), items.Select(i => i.Header));
            SaveShot(window, "verwaltung-team-wer-darf-was-plus-person", "hell");
            ClickCenter(window, items[0]);
            await Pump(Settling);

            Assert.Single(api.Calls, c => c.Route.StartsWith("POST /api/employees/", StringComparison.Ordinal) && c.Route.EndsWith("/regulations/powers", StringComparison.Ordinal));
            await RosterTests.Until(() => team.Powers.Toast.Text == "Eingetragen");
            return team;
        }, api);
    }
}

using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 98 (Ä191, Ä250, ADR 0084): Bereich „Team“ mit Personenkarte, mit echter Eingabe (Klick, Tippen, Strg+S) als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] CardTabs = ["Stammdaten", "Zugang und Rolle", "Qualifikationen", "Dienstplan und Abwesenheit", "Zeiten", "Lohn", "Personalakte", "Regelungen und Vollmachten", "Zuständigkeiten", "Verlauf"];

    /// <summary>Lena arbeitet dienstags 9–17 Uhr in Wien; ihre Woche und die von Marco liefert der Dienstplan-Stub (Slice 99).</summary>
    private static readonly EmployeeDto RosterLena = SpaSample.Lena with { WorkingHours = [new WorkingHours(SpaSample.LocationId, DayOfWeek.Tuesday, new(9, 0), new(17, 0))] };

    /// <summary>
    /// Behandlungen wie <see cref="TreatmentApi"/> (Lena hat Hot Stone) plus Team: Lena mit Zugang als Therapeutin, Marco als Standortleitung, Elif eingeladen,
    /// Johanna ausgeschieden.
    /// </summary>
    private static StubApi TeamApi()
    {
        var employees = SpaSample.Resources.Employees.Select(e => e == SpaSample.Lena ? e with { QualificationIds = [HotStoneQualification.Id] } : e).ToList();
        var lena = employees.Single(e => e.Id == SpaSample.Lena.Id);
        var api = TreatmentApi();
        _ = new RosterSample(api, SpaSample.Vienna, SpaSample.Day, RosterLena, SpaSample.Marco);
        return TeamSample.AddTeam(api, employees, [SpaSample.Johanna],
            [
                TeamSample.Access("u-lena", lena, "", StaffRole.Therapist, [SpaSample.LocationId]),
                TeamSample.Access("u-marco", SpaSample.Marco, "", StaffRole.Manager, [SpaSample.LocationId]),
            ],
            [TeamSample.Invitation(SpaSample.Elif.DisplayName, "elif@spa.test", StaffRole.Therapist, [SpaSample.LocationId], SpaSample.Elif.Id)]);
    }

    /// <summary>Verwaltung öffnen und „Team“ anklicken.</summary>
    private static async Task<TeamViewModel> OpenTeam(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        await OpenMask(window, shell, "Team");
        var team = shell.AdminPage.Team!;
        Assert.Same(team, shell.AdminPage.Content);
        Assert.True(team.IsLoaded);
        return team;
    }

    private static async Task ClickRow(Window window, string title)
    {
        var row = RowIn(window, "TeamList", title);
        row.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, row);
        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Inhaberin_klickt_Lena_und_findet_alle_vier_Reiter_auf_derselben_Karte(string theme) => Render(theme, "verwaltung-team", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        var card = team.Card!;
        Assert.Equal("Lena Huber", card.Title);
        Assert.Equal(CardTabs, Named<Border>(window, "CardTabs").GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => t.Content as string));

        var sections = new Dictionary<string, string>
        {
            ["Zugang und Rolle"] = "AccessSection",
            ["Qualifikationen"] = "QualificationsSection",
            ["Dienstplan und Abwesenheit"] = "RosterSection",
            ["Zeiten"] = "TimesSection",
            ["Regelungen und Vollmachten"] = "RegulationsHost",
            ["Zuständigkeiten"] = "DutiesHost",
            ["Verlauf"] = "HistorySectionPanel",
            ["Stammdaten"] = "DetailsSection",
        };
        foreach (var (label, section) in sections)
        {
            await ClickCardTab(window, label);
            Assert.Same(card, team.Card);
            Assert.True(Named<Control>(window, section).IsEffectivelyVisible, label);
            Assert.All(sections.Values.Where(s => s != section), other => Assert.False(window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == other && c.IsEffectivelyVisible), other));
        }

        Assert.Equal("Lena Huber", Named<TextBox>(window, "PersonNameField").Text);
        Assert.True(Shows(window, "Therapeut:in · aktiv"));
        return team;
    }, TeamApi());

    [Fact]
    public Task Verwaltung_Team_Einladen_per_Tippen_und_Klick_zeigt_die_Person_als_eingeladen()
    {
        var api = TeamApi();
        return Render("hell", "verwaltung-team-einladen", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Person einladen");
            Assert.True(team.Card?.Access?.IsInvite);
            Assert.False(Named<Control>(window, "CardTitle").IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "CardTabs").IsEffectivelyVisible);

            await TypeInto(window, "InviteNameField", "Nina Koller");
            await TypeInto(window, "InviteEmailField", "nina@spa.test");
            var invite = Named<Button>(window, "InviteButton");
            invite.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, invite);
            await Pump(Settling);

            var post = Assert.Single(api.Calls, c => c.Route == "POST /api/staff/invitations");
            Assert.Equal("nina@spa.test", JsonSerializer.Deserialize<InvitationInput>(post.Body!, ApiClient.Json)!.Email);
            Assert.True(Shows(window, "Einladung an nina@spa.test geschickt."));
            Assert.Contains($"einladung={TeamSample.Token}", Named<TextBox>(window, "InviteLinkField").Text, StringComparison.Ordinal);
            Assert.True(Shows(window, "7 Tage gültig, einmal verwendbar"));
            Assert.Equal("Einladungslink kopieren", Named<Button>(window, "CopyLinkButton").Content);
            var row = RowIn(window, "TeamList", "Nina Koller");
            // Ohne Wahl gilt die Vorgabe des Web-Formulars: Rezeption.
            Assert.Contains(row.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Rezeption · eingeladen");

            // Die Maus vom Knopf nehmen: sein Hinweis läge im Handbuchbild über der Bestätigung.
            window.MouseMove(new Avalonia.Point(1, 1));
            await Pump(Settling);
            return team;
        }, api);
    }

    [Fact]
    public Task Verwaltung_Team_Strg_S_speichert_die_Stammdaten()
    {
        var api = TeamApi();
        return Render("hell", "verwaltung-team-gespeichert", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");

            await TypeInto(window, "PersonNameField", "Lena Maria Huber");
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{SpaSample.Lena.Id}");
            Assert.Equal("Lena Maria Huber", JsonSerializer.Deserialize<EmployeeInput>(put.Body!, ApiClient.Json)!.DisplayName);
            Assert.False(team.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            Assert.Equal("Lena Maria Huber", team.SelectedEntry?.Title);
            return team;
        }, api);
    }

    [Fact]
    public Task Verwaltung_Team_Wer_kann_das_Klick_auf_Lena_oeffnet_den_Reiter_Qualifikationen() =>
        Render("hell", "verwaltung-team-wer", async (window, main) =>
        {
            var treatments = await OpenTreatments(window, main);
            await ClickSection(window, "Qualifikationen & Räume");
            var chip = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("chip") && b.Content as string == "Lena Huber");
            chip.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, chip);
            await Pump(Settling);

            var shell = (ShellViewModel)main.Content!;
            Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
            Assert.Equal("Lena Huber", shell.AdminPage.Team?.Card?.Title);
            Assert.True(Named<Control>(window, "QualificationsSection").IsEffectivelyVisible);
            return treatments;
        }, TeamApi());

    /// <summary>Jede Zeile der Liste (Person mit Zugang, Leitung, Einladung, Ausgeschiedene, Neu) und jeder Reiter der Karte.</summary>
    private static async Task VisitTeam(Window window, TeamViewModel team, Func<string, Task> visit)
    {
        await ClickRow(window, "Lena Huber");
        foreach (var tab in CardTabs)
        {
            await ClickCardTab(window, tab);
            await visit($"Lena, {tab}");
        }

        // Slice 99: der Tag im Reiter der Karte, dann Wochenplan (mit offenem Tag) und Anträge.
        await ClickCardTab(window, "Dienstplan und Abwesenheit");
        await ClickCell(window, "Lena Huber", "Di.");
        await visit("Lena, Dienstplan mit offenem Tag");
        await ClickCardTab(window, "Zeiten");
        await ClickTimesCell(window, "Lena Huber", "Di.");
        await visit("Lena, Zeiten mit offenem Tag");
        foreach (var mode in new[] { "Wochenplan", "Zeiten", "Anträge" })
        {
            await ClickSection(window, mode);
            await visit(mode);
            if (mode == "Wochenplan")
            {
                await ClickCell(window, "Lena Huber", "Di.");
                await visit("Wochenplan, Tag offen");
            }

            if (mode == "Zeiten")
            {
                await ClickTimesCell(window, "Lena Huber", "Di.");
                await visit("Zeiten, Tag offen");
            }
        }

        await ClickSection(window, "Personen");
        foreach (var title in new[] { "Marco Berger", "Elif Demir", "Person anlegen", "Person einladen" })
        {
            await ClickRow(window, title);
            await visit(title);
        }

        team.ShowFormer = true;
        await Pump(Settling);
        await ClickRow(window, "Johanna Lechner");
        await visit("Ausgeschieden");
    }

    [Fact]
    public Task Jede_Seite_Team_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen_und_erreicht_jedes_mit_Tab() =>
        Render("hell", "namen-team", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            var problems = new List<string>();
            await VisitTeam(window, team, async state =>
            {
                var view = window.GetVisualDescendants().OfType<TeamView>().Single();
                foreach (var control in view.GetVisualDescendants().OfType<Control>()
                    .Where(c => c is Button or ToggleButton or CheckBox or TextBox or ComboBox or ListBoxItem))
                {
                    if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                    {
                        problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                    }
                }

                problems.AddRange(await TabProblems(window, $"Team, {state}"));
            });
            Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
            return team;
        }, TeamApi());

    [Fact]
    public Task Team_melden_keine_Bindungsfehler() => Render("hell", "bindungen-team", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            TeamViewModel? team = null;
            await AssertNoBindingErrors(log, "Team öffnen", async () => team = await OpenTeam(window, main));
            await VisitTeam(window, team!, state => AssertNoBindingErrors(log, state, () => Task.CompletedTask));
            await AssertNoBindingErrors(log, "Anderer Bereich", async () =>
            {
                var shell = (ShellViewModel)main.Content!;
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                await Pump(Settling);
            });
            await AssertNoBindingErrors(log, "Zum Kalender", async () =>
            {
                var shell = (ShellViewModel)main.Content!;
                shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
                await Pump(Settling);
            });
            log.AssertClean();
            return team!;
        });
        return true;
    }, TeamApi());

    [Fact]
    public Task Verwaltung_Team_Abgelehnte_Rolle_steht_am_Feld_und_verschwindet_mit_der_naechsten_Wahl() =>
        Render("hell", "verwaltung-team-rolle", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Marco Berger");
            await ClickSection(window, "Zugang und Rolle");
            var role = Named<ComboBox>(window, "RoleField");
            var field = role.GetVisualAncestors().OfType<FormField>().First();
            role.BringIntoView();
            await Pump(Settling);

            await OpenAsync(window, role);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Pump(Settling);
            Assert.Equal("Inhaber", (role.SelectedItem as RoleChoice)?.Label);

            PressSave(window);
            await Pump(Settling);

            Assert.Equal(TeamSample.RejectRole, field.Error);
            Assert.True(Shows(window, TeamSample.RejectRole));
            Assert.Equal(TeamSample.RejectRole, team.Error);

            await OpenAsync(window, role);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Pump(Settling);
            Assert.Null(field.Error);
            return team;
        }, TeamApi().On("PUT /api/staff/u-marco", new { title = "Eingabe ungültig", errors = new Dictionary<string, string[]> { ["Role"] = [TeamSample.RejectRole] } },
            System.Net.HttpStatusCode.BadRequest));

    [Fact]
    public Task Verwaltung_Team_Angefangene_Einladung_fragt_beim_Klick_auf_eine_andere_Zeile() =>
        Render("hell", "verwaltung-team-formular", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Person einladen");
            await TypeInto(window, "InviteEmailField", "nina@spa.test");

            await ClickRow(window, "Lena Huber");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Equal("Person einladen", team.Card?.Title);
            return team;
        }, TeamApi());

    [Fact]
    public Task Verwaltung_Team_Aenderung_und_Suche_fragt_beim_Klick_auf_eine_andere_Zeile() =>
        Render("hell", "verwaltung-team-suche", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await TypeInto(window, "PersonNameField", "Lena Maria Huber");
            await TypeInto(window, "TeamSearch", "marco");
            await Pump(Settling);
            Assert.Null(team.SelectedEntry);

            await ClickRow(window, "Marco Berger");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Equal("Lena Maria Huber", team.Card?.Details?.Name);
            return team;
        }, TeamApi());
}

using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-102 (Brief 5.32): Die Besetzung über den Tag steht im Wochenplan des Teams unter den Spalten des Plans, kennzeichnet eine Lücke nie nur mit Farbe und fehlt auf der Personenkarte.</summary>
public sealed partial class ViewTests
{
    /// <summary>Lena dienstags 9–17, Marco dienstags 10–18; der Standort ist Montag bis Samstag von 8 bis 19 Uhr offen, „heute“ ist Montag.</summary>
    private static (StubApi Api, RosterSample Roster) StaffingApi()
    {
        var (api, roster) = RosterApi();
        roster.Plan(RosterLena, RosterTuesday, false, new RosterEntry(RosterKind.Work, SpaSample.Vienna.Id, new(9, 0), new(17, 0)));
        roster.Plan(SpaSample.Marco, RosterTuesday, false, new RosterEntry(RosterKind.Work, SpaSample.Vienna.Id, new(10, 0), new(18, 0)));
        roster.OpenHours = (8, 19);
        return (api, roster);
    }

    private static Border StaffingCell(Window window, string name) =>
        window.GetVisualDescendants().OfType<Border>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);

    private static bool HasStaffingSection(Window window) =>
        window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "RosterStaffing" && c.IsEffectivelyVisible);

    // Rot, wenn die Lücke nur noch Farbe trägt (Warndreieck oder Zahl fehlt), die geschlossene Stunde eine Zahl bekommt oder die Matrix nicht mehr unter den Spalten des Plans liegt.
    [Fact]
    public Task Wochenplan_zeigt_die_Besetzung_je_Stunde_buendig_unter_den_Spalten_und_kennzeichnet_die_Luecke_mit_Zeichen()
    {
        var (api, _) = StaffingApi();
        return Render("hell", "verwaltung-team-wochenplan-besetzung", async (window, main) =>
        {
            await SetSize(window, 1440, 900);
            var team = await OpenWeekPlan(window, main);

            var matrix = VisibleNamed<StackPanel>(window, "RosterStaffingMatrix");
            Assert.Equal("Besetzung über den Tag", AutomationProperties.GetName(matrix));

            var gap = StaffingCell(window, "Di. 20.10., 8 bis 9 Uhr: niemand im Dienst");
            Assert.Contains(gap.GetVisualDescendants().OfType<Icon>(), i => i.IsEffectivelyVisible);
            Assert.Contains(gap.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "0");

            var closed = StaffingCell(window, "So. 25.10., 9 bis 10 Uhr: geschlossen");
            Assert.DoesNotContain(closed.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text));
            Assert.Equal(1, StaffingCell(window, "Di. 20.10., 11 bis 12 Uhr: 2 Personen im Dienst").GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "2"));

            // Bündig: die erste Tagesspalte der Matrix beginnt dort, wo der Streifen von heute (Montag) in der Tabelle darüber beginnt (Zelle mit Rand 1).
            var stripe = VisibleNamed<Border>(window, "TodayStripe");
            var monday = StaffingCell(window, "Mo. 19.10., 8 bis 9 Uhr: niemand im Dienst");
            Assert.InRange(Math.Abs(PositionIn(window, monday).X - 1 - PositionIn(window, stripe).X), 0, 1);
            Assert.InRange(Math.Abs(monday.Bounds.Width + 2 - stripe.Bounds.Width), 0, 1);
            return team;
        }, api);
    }

    // Rot, wenn die Karte einer Person die Matrix zeigt (auch wenn der Server Zahlen schickt).
    [Fact]
    public Task Personenkarte_zeigt_die_Besetzung_ueber_den_Tag_nie()
    {
        var (api, _) = StaffingApi();
        return Render("hell", "verwaltung-team-karte-dienstplan-ohne-besetzung", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Dienstplan und Abwesenheit");
            await Pump(Settling);

            Assert.True(team.Card!.Roster!.IsLoaded);
            Assert.True(Named<Control>(window, "RosterSection").IsEffectivelyVisible);
            Assert.False(HasStaffingSection(window));
            return team;
        }, api);
    }

    // Rot, wenn bei Diensten und fehlenden Öffnungszeiten weder Satz noch Weg zur Einstellung erscheint oder trotzdem eine leere Matrix steht.
    [Fact]
    public Task Wochenplan_nennt_bei_der_Besetzung_ohne_Oeffnungszeiten_den_Weg_zur_Einstellung_statt_einer_leeren_Matrix()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-besetzung-leer", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            var hint = VisibleNamed<TextBlock>(window, "RosterStaffingHint");
            Assert.Contains("Öffnungszeiten", hint.Text, StringComparison.Ordinal);
            Assert.Contains("Standorte & Räume", hint.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "RosterStaffingMatrix" && c.IsEffectivelyVisible);
            return team;
        }, api);
    }
}

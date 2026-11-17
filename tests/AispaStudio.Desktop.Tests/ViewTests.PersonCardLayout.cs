using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>P5 (ADR 0098, Nachtrag): Teamliste mit festen Knöpfen und Kennzeichen, Personenkarte mit festem Kopf, Karten, Schaltern und Chips, als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static bool IsInside(Control inner, Control outer) => outer.IsVisualAncestorOf(inner);

    /// <summary>Alle sichtbaren Eingabefelder und Knöpfe im Rollbereich bleiben links vom Ende des sichtbaren Bereichs, ohne dass die Bildlaufleiste sie anschneidet.</summary>
    private static void AssertNothingClipped(Window window, ScrollViewer scroll, string where)
    {
        var right = LeftOf(window, scroll) + scroll.Viewport.Width;
        foreach (var control in scroll.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && !c.GetVisualAncestors().OfType<ScrollViewer>().TakeWhile(s => s != scroll).Any() && (c is TextBox or ComboBox or Button or ToggleButton or ToggleSwitch or TimeField or DateField)))
        {
            var edge = LeftOf(window, control) + control.Bounds.Width;
            Assert.True(edge <= right + 1, $"{where}: {control.GetType().Name} {control.Name} ({(control as ContentControl)?.Content}, {control.DataContext?.GetType().Name}) endet bei {edge:0}, sichtbar bis {right:0}");
        }
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Personenkarte_Kopf_und_Schiene_stehen_fest_und_nur_die_Bereiche_rollen(string theme) => Render(theme, "verwaltung-team-profil", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Profil");
        var scroll = Named<ScrollViewer>(window, "CardScroll");
        var title = Named<TextBlock>(window, "CardTitle");
        var tabs = Named<Border>(window, "CardTabs");
        var menu = Named<MoreMenuButton>(window, "CardMenu");
        Assert.False(IsInside(title, scroll));
        Assert.False(IsInside(tabs, scroll));
        Assert.True(menu.IsEffectivelyVisible);
        Assert.True(Named<Control>(window, "CardAvatar").IsEffectivelyVisible);
        Assert.Equal("Lena Huber", title.Text);
        Assert.True(Shows(window, "Zugang aktiv"));

        var before = TopOf(window, title);
        scroll.ScrollToEnd();
        await Pump(Settling);
        Assert.Equal(before, TopOf(window, title), 1);
        scroll.ScrollToHome();
        await Pump(Settling);
        return team;
    }, TeamApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Teamliste_hat_Person_hinzufuegen_fest_ueber_der_Liste_und_Kennzeichen_nur_bei_Abweichung(string theme) => Render(theme, "verwaltung-team-liste", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        var add = Named<Button>(window, "AddPersonButton");
        var invite = Named<Button>(window, "NewAccessButton");
        var list = Named<ListBox>(window, "TeamList");
        Assert.True(add.IsEffectivelyVisible);
        Assert.True(invite.IsEffectivelyVisible);
        Assert.Contains("primary", add.Classes);
        Assert.True(TopOf(window, add) < TopOf(window, invite));
        Assert.True(TopOf(window, invite) < TopOf(window, Named<Control>(window, "TeamSearch")));
        Assert.True(TopOf(window, Named<Control>(window, "TeamSearch")) < TopOf(window, list));

        // Die festen Knöpfe sind keine Zeilen der Liste.
        var rows = list.GetVisualDescendants().OfType<ListBoxItem>().Select(i => i.DataContext).OfType<TeamEntry>().ToList();
        Assert.DoesNotContain(rows, r => r.IsNew);

        // Lena ist normal angemeldet: kein Kennzeichen; Elif ist eingeladen: eines.
        var lena = RowIn(window, "TeamList", "Lena Huber");
        Assert.DoesNotContain(lena.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("pill") && b.IsEffectivelyVisible);
        var elif = RowIn(window, "TeamList", "Elif Demir");
        Assert.Contains(elif.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Eingeladen" && t.IsEffectivelyVisible);

        await ClickRow(window, "Lena Huber");
        ClickCenter(window, add);
        await Pump(Settling);
        Assert.Equal("Neue Person", team.Card?.Title);
        Assert.Null(team.ListSelection);
        Assert.True(Named<TextBox>(window, "PersonNameField").IsEffectivelyVisible);
        return team;
    }, TeamApi());

    [Fact]
    public Task Personenkarte_Profil_nutzt_Schalter_und_Chips_statt_Haekchen() => Render("hell", "verwaltung-team-profil-schalter", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Profil");

        var active = Named<ToggleSwitch>(window, "ActiveField");
        Assert.True(active.IsEffectivelyVisible);
        Assert.Equal(true, active.IsChecked);
        Assert.Equal("Zugang aktiv", active.Content);

        var qualifications = Named<ItemsControl>(window, "PersonQualificationsField");
        var chips = qualifications.GetVisualDescendants().OfType<ToggleButton>().Where(b => b.Classes.Contains("choicechip")).ToList();
        Assert.NotEmpty(chips);
        Assert.Contains(chips, c => c.IsChecked == true);

        // Im Profil steht kein Kontrollkästchen mehr; Befristung und Zuordnung sind unter „Weitere Angaben“ eingeklappt.
        var profile = Named<Control>(window, "ProfileColumns");
        Assert.DoesNotContain(profile.GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible);
        Assert.DoesNotContain(Named<Control>(window, "QualificationsHost").GetVisualDescendants().OfType<CheckBox>(), c => c.IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "UntilField" && c.IsEffectivelyVisible);
        ClickCenter(window, Named<ToggleButton>(window, "MoreToggle"));
        await Pump(Settling);
        Assert.True(Named<Control>(window, "UntilField").IsEffectivelyVisible);
        return team;
    }, TeamApi());

    [Theory]
    [InlineData(1440, "hell")]
    [InlineData(1100, "hell")]
    [InlineData(1440, "dunkel")]
    [InlineData(1100, "dunkel")]
    public Task Personenkarte_Karten_stehen_gleich_breit_nebeneinander_oder_untereinander_und_nichts_wird_angeschnitten(int width, string theme) =>
        Render(theme, $"verwaltung-team-karte-{width}-ende", async (window, main) =>
        {
            await SetSize(window, width, width == 1100 ? 680 : 900);
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardArea(window, "Profil");
            var scroll = Named<ScrollViewer>(window, "CardScroll");
            var details = Named<Control>(window, "DetailsBlock");
            var access = Named<Control>(window, "AccessHost");
            TestContext.Current.TestOutputHelper?.WriteLine($"{width}: Karte {scroll.Viewport.Width:0}, Stammdaten {details.Bounds.Width:0} bei {LeftOf(window, details):0}/{TopOf(window, details):0}, Zugang {access.Bounds.Width:0} bei {LeftOf(window, access):0}/{TopOf(window, access):0}");
            if (TopOf(window, details) == TopOf(window, access))
            {
                Assert.Equal(details.Bounds.Width, access.Bounds.Width, 1);
            }
            else
            {
                Assert.True(width < 1440, "bei 1440 stehen beide nebeneinander");
                Assert.Equal(details.Bounds.Width, access.Bounds.Width, 1);
            }

            AssertNothingClipped(window, scroll, $"Profil {width}");
            SaveShot(window, $"verwaltung-team-profil-{width}", theme);
            scroll.ScrollToEnd();
            await Pump(Settling);
            AssertNothingClipped(window, scroll, $"Profil {width}, unten");
            SaveShot(window, $"verwaltung-team-profil-{width}-unten", theme);

            await ClickCardArea(window, "Arbeitszeit");
            await Pump(Settling);
            var hours = Named<Control>(window, "WorkingHoursSection");
            hours.BringIntoView();
            await Pump(Settling);
            Assert.True(hours.IsEffectivelyVisible);
            Assert.Equal("Frei: Montag, Mittwoch, Donnerstag, Freitag, Samstag, Sonntag", Named<TextBlock>(window, "FreeDaysText").Text);
            SaveShot(window, $"verwaltung-team-arbeitszeit-{width}", theme);
            AssertNothingClipped(window, scroll, $"Arbeitszeit {width}");
            return team;
        }, TeamApi());

    [Fact]
    public Task Personenkarte_Regelarbeitszeit_aendern_speichert_mit_der_Leiste_der_Karte() => Render("hell", "verwaltung-team-regelarbeitszeit", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Arbeitszeit");
        Named<Control>(window, "WorkingHoursSection").BringIntoView();
        await Pump(Settling);
        var add = Named<Button>(window, "AddHoursButton");
        ClickCenter(window, add);
        await Pump(Settling);

        Assert.True(team.HasUnsavedChanges);
        Assert.True(Named<Control>(window, "Footer").IsEffectivelyVisible);
        Assert.DoesNotContain("Mittwoch", Named<TextBlock>(window, "FreeDaysText").Text, StringComparison.Ordinal);
        return team;
    }, TeamApi());
}

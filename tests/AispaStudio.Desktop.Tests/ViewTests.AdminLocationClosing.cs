using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0099, Punkt 10b: „Standort anlegen“ fest über der Liste, Menü „⋯“ im Kopf der Karte mit „Schließen ab Datum …“ und „Löschen …“, die Zeile mit dem Datum und der Hinweis eines geschlossenen Standorts.</summary>
public sealed partial class ViewTests
{
    private static readonly DateOnly ClosingDay = new(2027, 4, 1);

    [Theory]
    [InlineData("hell", 1440)]
    [InlineData("dunkel", 1440)]
    [InlineData("hell", 1100)]
    public Task Standorte_zeigen_Anlegen_ueber_der_Liste_und_schliessen_ab_Datum_im_Menue(string theme, int width) =>
        Render(theme, $"verwaltung-standorte-schliessen-{width}", async (window, main) =>
        {
            window.MinWidth = 0;
            window.Width = width;
            var shell = await OpenLocations(window, main);
            var locations = shell.AdminPage.Locations!;

            // Given „Standort anlegen“ steht fest über der Liste, nicht darin
            var create = Named<Button>(window, "NewLocationButton");
            var list = Named<ListBox>(window, "LocationList");
            Assert.True(create.IsEffectivelyVisible);
            Assert.True(BottomOf(window, create) <= TopOf(window, list) + 0.5);
            Assert.DoesNotContain(locations.Choices, c => c.Id is null);

            // When das Menü „⋯“ im Kopf der Karte aufgeht
            var more = Named<MoreMenuButton>(window, "LocationMoreMenu");
            Assert.True(more.IsEffectivelyVisible);
            ClickCenter(window, more);
            await Pump(Settling);
            var items = Assert.IsType<MenuFlyout>(more.Flyout).Items.OfType<MenuItem>().ToList();

            // Then stehen „Schließen ab Datum …“ und „Löschen …“ darin, und das erste öffnet die Zeile mit dem Datum
            Assert.Equal(["Schließen ab Datum …", "Löschen …"], items.Select(i => i.Header));
            items[0].Command!.Execute(null);
            more.Flyout!.Hide();
            await Pump(Settling);
            var panel = Named<Border>(window, "ClosePanel");
            Assert.True(panel.IsEffectivelyVisible);
            Assert.True(Named<DateField>(window, "CloseDateField").IsEffectivelyVisible);

            // Und nach „Schließen“ zeigen Hinweis und Liste, ab wann der Standort geschlossen ist
            locations.CloseDate = ClosingDay;
            await locations.ConfirmCloseCommand.ExecuteAsync(null);
            await Pump(Settling);
            Assert.False(panel.IsEffectivelyVisible);
            Assert.True(Named<InfoBar>(window, "ClosedBar").IsEffectivelyVisible);
            Assert.Equal("geschlossen ab 01.04.2027", locations.Choices[0].Note);
            return shell.AdminPage;
        }, LocationsApi().On($"POST /api/locations/{SpaSample.LocationId}/close", SpaSample.Vienna with { ClosedFrom = ClosingDay }));

    [Theory]
    [InlineData("hell", 1440)]
    [InlineData("dunkel", 1440)]
    [InlineData("hell", 1100)]
    public Task Firmendaten_zeigen_oben_die_Karte_Betrieb_einrichten_mit_Fortschritt_und_springen_zur_Maske(string theme, int width) =>
        Render(theme, $"verwaltung-firmendaten-einrichten-{width}", async (window, main) =>
        {
            window.MinWidth = 0;
            window.Width = width;
            var shell = await OpenAdminPage(window, main);
            var company = shell.AdminPage.Company!;

            // Given vier von sechs Schritten erledigt: die Karte steht oben, vor den Feldern der Firmendaten
            var card = Named<Border>(window, "SetupCard");
            Assert.True(card.IsEffectivelyVisible);
            Assert.Equal("4 von 6 Schritten erledigt", Named<TextBlock>(window, "SetupSummaryText").Text);
            Assert.True(BottomOf(window, card) <= TopOf(window, Named<TextBox>(window, "NameField")) + 0.5);

            // When der offene Schritt „Kassa einrichten“ gewählt wird, Then springt die Verwaltung zur Maske Kassa
            company.OpenSetupStepCommand.Execute(company.SetupSteps.Single(s => s.Step.Key == "register"));
            await Pump(Settling);
            Assert.Equal("Kassa", shell.AdminPage.SelectedArea?.Title);
            shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Firmendaten");
            await Pump(Settling);
            return shell.AdminPage;
        }, CompanyApi().On("GET /api/setup/progress", new SetupProgressDto(
        [
            new("location", "Standort anlegen", "Name, Zeitzone und Anschrift des Spas.", true, "masterData", "locations"),
            new("hours", "Öffnungszeiten eintragen", "Wann Gäste buchen können.", true, "masterData", "locations"),
            new("rooms", "Räume anlegen", "Behandlungsräume und Bereiche wie die Sauna.", true, "masterData", "locations"),
            new("treatments", "Behandlungen anlegen", "Was das Spa anbietet, mit Dauer und Preis.", true, "masterData", "treatments"),
            new("people", "Team anlegen", "Wer behandelt und wer an der Rezeption arbeitet.", false, "masterData", "team"),
            new("register", "Kassa einrichten", "Kassa-ID, Belegkopf und Land für Belege.", false, "masterData", "kassa"),
        ])));
}

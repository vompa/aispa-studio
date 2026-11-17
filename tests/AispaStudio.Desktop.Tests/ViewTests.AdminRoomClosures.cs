using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 95 (Ä207), seit ADR 0098 in der Liste der Seite „Schließzeiten“: die Raumsperre im Fenster: Zeile und Formular in der Seitenleiste, „Einzelner Raum“ beim Anlegen, Sprung aus dem
/// Kalender-Menü, Löschen über die Rückfrage. Die Daten stehen in <c>ViewTests.AdminClosures.cs</c> (<c>ClosuresApi</c>).
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Raumsperren(string theme) => Render(theme, "verwaltung-raumsperren", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Raumsperren");

        // Die Raumsperre steht mit dem Kennzeichen „Raum“ in der Liste; ihr Formular öffnet in der Seitenleiste.
        Assert.Equal("Raum Zeder, Wartung", page.Upcoming[0].Title);
        Assert.Equal("1 Termin offen", page.Upcoming[0].AppointmentsText);
        await ClickRow(window, "UpcomingList", 0);
        Assert.Same(page.RoomEditor, page.Editor);
        Assert.Equal("Lüftung wird gewartet", Named<TextBox>(window, "ReasonField").Text);
        Assert.True(Shows(window, "1 Termin liegt im gesperrten Raum"));
        Assert.True(Shows(window, "Aktiv"));
        Assert.False(Named<ComboBox>(window, "RoomField").IsEnabled);
        Assert.False(Named<StackPanel>(window, "NewKindChoice").IsEffectivelyVisible);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Raumsperre_anlegen_ueber_Klick_und_Speichern()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-raumsperren-anlegen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            var page = shell.AdminPage.ClosuresPage!;
            var closures = page.RoomEditor;

            // „Neue Schließzeit“, dann oben in der Leiste „Einzelner Raum“ wählen.
            ClickCenter(window, Named<Button>(window, "NewButton"));
            await Pump(Settling);
            ClickCenter(window, Named<RadioButton>(window, "NewKindRoom"));
            await Pump(Settling);
            await Pump(Settling);
            Assert.True(closures.IsCreating);
            Assert.Same(closures, page.Editor);
            Assert.True(Shows(window, "Neue Raumsperre"));

            await TypeInto(window, "FromDateField", "07.12.2026");
            await TypeInto(window, "UntilDateField", "08.12.2026");
            await TypeInto(window, "ReasonField", "Wasserschaden");
            PressSave(window);
            await Pump(Settling);

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {RoomClosureRoute()}");
            var input = JsonSerializer.Deserialize<RoomClosureInput>(post.Body!, ApiClient.Json)!;
            Assert.Equal((new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 8), "Wasserschaden", RoomClosureKind.Maintenance), (input.FromDate, input.UntilDate, input.Reason, input.Kind));
            Assert.False(closures.HasUnsavedChanges);
            Assert.Equal("Raumsperre gespeichert", page.Toast);
            Assert.True(Shows(window, "Raumsperre gespeichert"));
            Assert.Contains(page.Upcoming, r => r.IsRoom && r.Reason == "Wasserschaden");
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Raum_sperren_im_Kalender_oeffnet_die_Seite_mit_dem_Raum() => Render("hell", "verwaltung-raumsperren-kalender", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        calendar.ByRoom = true;
        await Pump(Settling);

        var header = window.GetVisualDescendants().OfType<Grid>().First(g => g.DataContext is CalendarColumn { Id: var id } && id == SpaSample.Cedar.Id && g.Classes.Contains("filterable") && g.IsEffectivelyVisible);
        var at = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);
        var entry = window.GetVisualDescendants().OfType<MenuItem>().First(i => i.Header is string text ? text == "Raum sperren …" : HeaderOf(i) == "Raum sperren …");
        ClickCenter(window, entry);
        await Pump(Settling);
        await Pump(Settling);

        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        var page = shell.AdminPage.ClosuresPage!;
        var closures = page.RoomEditor;
        Assert.True(closures.IsCreating);
        Assert.Equal(SpaSample.Cedar.Id, closures.SelectedRoom?.Id);
        Assert.True(Shows(window, "Neue Raumsperre"));
        Assert.True(Named<SidePanel>(window, "EditPanel").IsVisible);
        Assert.True(Named<RadioButton>(window, "NewKindRoom").IsChecked);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Raumsperre_loeschen_fragt_nach_und_sendet_DELETE()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-raumsperren-loeschen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            await ClickRow(window, "UpcomingList", 0);

            await ClickScrolled(window, "DeleteButton");
            Assert.Equal("Raumsperre löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"DELETE {RoomClosureRoute("/" + MaintenanceId)}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"DELETE {RoomClosureRoute("/" + MaintenanceId)}"));
            Assert.Equal("Raumsperre gelöscht", shell.AdminPage.ClosuresPage!.Toast);
            Assert.True(Shows(window, "Raumsperre gelöscht"));
            Assert.DoesNotContain(shell.AdminPage.ClosuresPage.Upcoming, r => r.IsRoom);
            return shell.AdminPage;
        }, api);
    }
}

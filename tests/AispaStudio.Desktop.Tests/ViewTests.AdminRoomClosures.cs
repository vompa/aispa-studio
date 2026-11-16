using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä207): native Maske „Raumsperren“ im Fenster: Eingabe per Maus und Tastatur, Sprung aus dem Kalender-Menü, Löschen über die Rückfrage, Scans.</summary>
public sealed partial class ViewTests
{
    private static readonly Guid MaintenanceId = Guid.Parse("00000000-0000-0000-0000-0000000c2051");

    private static readonly RoomClosureDto CedarMaintenance = new(MaintenanceId, SpaSample.LocationId, SpaSample.Cedar.Id, SpaSample.Cedar.Name, new(2026, 12, 24), null, new(2026, 12, 26), null,
        SpaSample.At(2), SpaSample.At(2).AddDays(3), RoomClosureKind.Maintenance, "Lüftung wird gewartet", ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), 1);

    private static string RoomClosureRoute(string tail = "") => $"/api/locations/{SpaSample.LocationId}/room-closures{tail}";

    private static StubApi RoomClosuresApi(StubApi? api = null) => (api ?? Api())
        .On($"GET {RoomClosureRoute()}", new List<RoomClosureDto> { CedarMaintenance })
        .OnJson($"POST {RoomClosureRoute()}", body =>
        {
            var input = JsonSerializer.Deserialize<RoomClosureInput>(body!, ApiClient.Json)!;
            var room = SpaSample.Resources.Rooms.Single(r => r.Id == input.RoomId);
            return new RoomClosureDto(Guid.NewGuid(), SpaSample.LocationId, room.Id, room.Name, input.FromDate, input.FromTime, input.UntilDate, input.UntilTime, SpaSample.At(2),
                SpaSample.At(2).AddDays(3), input.Kind, input.Reason, ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), 0);
        })
        .On($"DELETE {RoomClosureRoute("/" + MaintenanceId)}", null, System.Net.HttpStatusCode.NoContent);

    private static async Task<ShellViewModel> OpenRoomClosures(Window window, MainViewModel main)
    {
        // ADR 0098: Die Raumsperren sind der zweite Abschnitt der Seite „Schließzeiten“; das alte Sprungziel öffnet die Seite und rollt zum Abschnitt.
        var shell = await OpenAdminPage(window, main);
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Raumsperren");
        shell.AdminPage.Show("masterData", "roomClosures");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.ClosuresPage, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.RoomClosures!.IsLoaded);
        AssertRoomClosuresInView(window);
        return shell;
    }

    /// <summary>Der Abschnitt „Raumsperren“ beginnt im sichtbaren Bereich der Seite.</summary>
    private static void AssertRoomClosuresInView(Window window)
    {
        var scroll = Named<ScrollViewer>(window, "PageScroll");
        Assert.InRange(TopOf(window, RoomClosuresPart(window)), TopOf(window, scroll) - 0.5, BottomOf(window, scroll) - 40);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Raumsperren(string theme) => Render(theme, "verwaltung-raumsperren", async (window, main) =>
    {
        var shell = await OpenRoomClosures(window, main);
        var closures = shell.AdminPage.RoomClosures!;
        Assert.Equal(["Raum Zeder, Wartung"], closures.Rows.Select(r => r.Title));
        Assert.True(Shows(window, "1 Termin liegt im gesperrten Raum"));
        Assert.Equal("Lüftung wird gewartet", Named<TextBox>(window, "ReasonField").Text);

        // Mit Schließzeiten darüber liegt der Abschnitt unter dem ersten Bildschirm: Das Sprungziel hat an seinen Anfang gerollt.
        var page = Named<ScrollViewer>(window, "PageScroll");
        var section = Named<Control>(window, "RoomClosuresSection");
        Assert.True(page.Offset.Y > 0);
        Assert.True(Math.Abs(TopOf(window, page) - TopOf(window, section)) <= 1 || page.Offset.Y >= page.Extent.Height - page.Viewport.Height - 1,
            $"Abschnitt bei {TopOf(window, section):F0}, Seite bei {TopOf(window, page):F0}, gerollt {page.Offset.Y:F0} von {page.Extent.Height - page.Viewport.Height:F0}, Abschnitt hoch {section.Bounds.Height:F0}, sichtbar {page.Viewport.Height:F0}");
        return shell.AdminPage;
    }, RoomClosuresApi(ClosuresApi()));

    [Fact]
    public Task Raumsperre_anlegen_ueber_Klick_und_Speichern()
    {
        var api = RoomClosuresApi();
        return Render("hell", "verwaltung-raumsperren-anlegen", async (window, main) =>
        {
            var shell = await OpenRoomClosures(window, main);
            var closures = shell.AdminPage.RoomClosures!;

            var part = RoomClosuresPart(window);
            ClickCenter(window, Named<Button>(part, "NewButton"));
            await Pump(Settling);
            Assert.True(closures.IsCreating);
            await TypeInto(window, Named<TextBox>(part, "FromDateField"), "07.12.2026");
            await TypeInto(window, Named<TextBox>(part, "UntilDateField"), "08.12.2026");
            await TypeInto(window, "ReasonField", "Wasserschaden");
            PressSave(window);
            await Pump(Settling);

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {RoomClosureRoute()}");
            var input = JsonSerializer.Deserialize<RoomClosureInput>(post.Body!, ApiClient.Json)!;
            Assert.Equal((new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 8), "Wasserschaden", RoomClosureKind.Maintenance), (input.FromDate, input.UntilDate, input.Reason, input.Kind));
            Assert.False(closures.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Raum_sperren_im_Kalender_oeffnet_die_Maske_mit_dem_Raum() => Render("hell", "verwaltung-raumsperren-kalender", async (window, main) =>
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
        var closures = shell.AdminPage.RoomClosures!;
        Assert.True(closures.IsCreating);
        Assert.Equal(SpaSample.Cedar.Id, closures.SelectedRoom?.Id);
        Assert.True(Shows(window, "Neue Raumsperre"));
        Assert.Equal(ClosuresPageViewModel.RoomClosuresSection, shell.AdminPage.ClosuresPage!.FocusedSection);
        AssertRoomClosuresInView(window);
        return shell.AdminPage;
    }, RoomClosuresApi());

    [Fact]
    public Task Raumsperre_loeschen_fragt_nach_und_sendet_DELETE()
    {
        var api = RoomClosuresApi();
        return Render("hell", "verwaltung-raumsperren-loeschen", async (window, main) =>
        {
            var shell = await OpenRoomClosures(window, main);

            await ClickScrolled(window, Named<Control>(RoomClosuresPart(window), "DeleteButton"));
            Assert.Equal("Raumsperre löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"DELETE {RoomClosureRoute("/" + MaintenanceId)}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"DELETE {RoomClosureRoute("/" + MaintenanceId)}"));
            Assert.True(Shows(window, "Gelöscht."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Rezeptionistin_sieht_keine_Raumsperren() => Render("hell", "verwaltung-rezeption-raumsperren", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.RoomClosures);
        Assert.False(window.GetVisualDescendants().OfType<RoomClosuresView>().Any());
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Raumsperren_melden_keine_Bindungsfehler() => Render("hell", "bindungen-raumsperren", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Raumsperren", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Schließzeiten");
                return Task.CompletedTask;
            });
            var closures = shell.AdminPage.RoomClosures!;
            await AssertNoBindingErrors(log, "Neue Raumsperre", async () => await closures.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Eingabe", () => { closures.Period.IsPartial = true; closures.Period.FromTime = "13:00"; closures.Reason = "x"; closures.Kind = RoomClosureKind.Damage; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Verworfen", () => { closures.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Beendete zeigen", () => { closures.ShowEnded = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Standort", () => { closures.SelectedLocation = closures.Locations[1]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, RoomClosuresApi());

    [Fact]
    public Task Jede_Seite_Raumsperren_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-raumsperren", async (window, main) =>
    {
        var shell = await OpenRoomClosures(window, main);
        var closures = shell.AdminPage.RoomClosures!;
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        void Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }
        }

        Scan("Raumsperre");
        closures.Period.IsPartial = true;
        await Pump(Settling);
        Scan("Teil-Tage");
        closures.Discard();
        await closures.NewCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Neue Raumsperre");
        closures.ShowEnded = true;
        await Pump(Settling);
        Scan("Beendet");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, RoomClosuresApi());
}

using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä205): native Maske „Standorte &amp; Räume“ im Fenster: Eingabe per Maus und Tastatur, Strg+S, Rückfrage beim Verlassen, Sprungziele.</summary>
public sealed partial class ViewTests
{
    private static readonly RoomInput NewRoomInput = new("Ruheraum Nord", 5, 4, false, null);

    /// <summary>Speichern gibt zurück, was ankam, wie die API.</summary>
    private static StubApi LocationsApi(StubApi? api = null) => (api ?? Api())
        .On($"GET /api/locations/{SpaSample.LocationId}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, "Bitte kommen Sie 10 Minuten früher.", false, 20, 48, true, 0, "Absage bis 24 Stunden vorher ist kostenlos."))
        .On($"GET /api/rooms/{SpaSample.Lavender.Id}/start-checklist", new List<RoomStartItemDto> { new(Guid.NewGuid(), "Fenster öffnen"), new(Guid.NewGuid(), "Duftöl auffüllen") })
        .OnJson($"PUT /api/locations/{SpaSample.LocationId}", body =>
        {
            var input = JsonSerializer.Deserialize<LocationInput>(body!, ApiClient.Json)!;
            return new LocationDto(SpaSample.LocationId, input.Name, input.TimeZone, input.Currency, input.OpeningHours, input.Region, input.Contact, input.Company);
        })
        .OnJson($"POST /api/locations/{SpaSample.LocationId}/rooms", body =>
        {
            var input = JsonSerializer.Deserialize<RoomInput>(body!, ApiClient.Json)!;
            return new RoomDto(Guid.NewGuid(), SpaSample.LocationId, input.Name, input.CleaningMinutes, input.Places ?? 1, input.IsArea ?? false, input.AdmissionArticleId);
        });

    private static async Task<ShellViewModel> OpenLocations(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Standorte & Räume");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Locations, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Locations!.IsLoaded);
        return shell;
    }

    private static TimeField TimeFieldNamed(Window window, string name) =>
        window.GetVisualDescendants().OfType<TimeField>().First(f => AutomationProperties.GetName(f) == name);

    private static async Task TypeIntoTime(Window window, string name, string text)
    {
        var box = TimeFieldNamed(window, name);
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        await Pump();
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Standorte(string theme) => Render(theme, "verwaltung-standorte", async (window, main) =>
    {
        var shell = await OpenLocations(window, main);
        Assert.Equal("Wien Innere Stadt", Named<TextBox>(window, "NameField").Text);
        Assert.Equal(["Wien Innere Stadt", "Salzburg", "Standort anlegen"], shell.AdminPage.Locations!.Choices.Select(c => c.Title));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "RoomNameField" && t.Text == "Raum Lavendel");
        return shell.AdminPage;
    }, LocationsApi());

    [Fact]
    public Task Samstag_umstellen_und_Strg_S_speichert_den_Standort()
    {
        var api = LocationsApi();
        return Render("hell", "verwaltung-standorte-samstag", async (window, main) =>
        {
            var shell = await OpenLocations(window, main);
            var locations = shell.AdminPage.Locations!;
            var saturday = locations.Days.Single(d => d.Day == DayOfWeek.Saturday);
            Assert.False(saturday.IsOpen);

            var open = window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "DayOpen" && c.DataContext == saturday);
            open.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, open);
            await Pump();
            Assert.True(saturday.IsOpen);
            await TypeIntoTime(window, "Samstag öffnet", "9");
            await TypeIntoTime(window, "Samstag schließt", "14");
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/locations/{SpaSample.LocationId}");
            Assert.Equal(
                [new OpeningHours(DayOfWeek.Monday, new(9, 0), new(20, 0)), new OpeningHours(DayOfWeek.Saturday, new(9, 0), new(14, 0))],
                JsonSerializer.Deserialize<LocationInput>(put.Body!, ApiClient.Json)!.OpeningHours);
            Assert.False(locations.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Ungespeicherter_Standort_beim_Wechsel_zum_Kalender_fragt()
    {
        var api = LocationsApi();
        return Render("hell", "verwaltung-standorte-rueckfrage", async (window, main) =>
        {
            var shell = await OpenLocations(window, main);
            var locations = shell.AdminPage.Locations!;
            await TypeInto(window, "NameField", "Wien Mitte");

            ClickCenter(window, NavRow(window, "Kalender"));
            await Pump(Settling);
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Contains("Standorte & Räume", dialog.Message, StringComparison.Ordinal);
            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);

            // Verwerfen: wechselt zum Kalender, der Standort steht wieder auf dem gespeicherten Stand.
            ClickCenter(window, DialogButton(window, "AlternativeButton"));
            await Pump(Settling);
            Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
            Assert.Equal("Wien Innere Stadt", locations.Name);
            Assert.Equal(0, CallsTo(api, $"PUT /api/locations/{SpaSample.LocationId}"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Ungespeicherter_Standort_beim_Wechsel_zum_anderen_Standort_fragt_und_Abbrechen_haelt_die_Eingabe() =>
        Render("hell", "verwaltung-standorte-wechsel", async (window, main) =>
        {
            var shell = await OpenLocations(window, main);
            var locations = shell.AdminPage.Locations!;
            await TypeInto(window, "NameField", "Wien Mitte");

            var list = Named<ListBox>(window, "LocationList");
            var salzburg = list.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.DataContext is LocationChoice { Title: "Salzburg" });
            ClickCenter(window, salzburg);
            await Pump(Settling);
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);

            Press(window, PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);
            Assert.Equal("Wien Innere Stadt", locations.SelectedChoice?.Title);
            Assert.Equal("Wien Mitte", locations.Name);
            Assert.Equal("Wien Mitte", Named<TextBox>(window, "NameField").Text);
            return shell.AdminPage;
        }, LocationsApi());

    [Fact]
    public Task Neuen_Raum_eintippen_und_Hinzufuegen_klicken_sendet_den_Raum_und_zeigt_ihn_in_der_Liste()
    {
        var api = LocationsApi();
        return Render("hell", "verwaltung-standorte-raum", async (window, main) =>
        {
            var shell = await OpenLocations(window, main);
            var locations = shell.AdminPage.Locations!;
            var newRow = Named<ContentControl>(window, "NewRoomRow");
            var name = newRow.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "RoomNameField");
            name.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, name);
            await Pump();
            window.KeyTextInput("Ruheraum Nord");
            await Pump();
            var add = newRow.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Hinzufügen");
            Assert.True(add.IsEffectivelyVisible);
            ClickCenter(window, add);
            await Pump(Settling);

            var post = Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{SpaSample.LocationId}/rooms");
            Assert.Equal(NewRoomInput with { Places = 1, CleaningMinutes = 10 }, JsonSerializer.Deserialize<RoomInput>(post.Body!, ApiClient.Json));
            Assert.Contains(locations.Rooms, r => r.Name == "Ruheraum Nord");
            Assert.Equal("", locations.NewRoom!.Name);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Kalender_Raum_bearbeiten_oeffnet_Standorte_mit_dem_Fokus_in_der_Zeile_des_Raums() =>
        Render("hell", "verwaltung-standorte-raum-fokus", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;

            shell.ShowAdmin("masterData", "locations", SpaSample.Cedar.Id);
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal("Standorte & Räume", shell.AdminPage.SelectedArea?.Title);
            var focused = Assert.IsType<TextBox>(Focused(window));
            Assert.Equal("RoomNameField", focused.Name);
            Assert.Equal("Raum Zeder", focused.Text);
            return shell.AdminPage;
        }, LocationsApi());

    [Fact]
    public Task Rezeptionistin_sieht_keine_Standorte_und_Raeume() => Render("hell", "verwaltung-rezeption-standorte", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.Locations);
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Standorte & Räume");
        Assert.False(window.GetVisualDescendants().OfType<LocationsView>().Any());
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Standorte_und_Raeume_melden_keine_Bindungsfehler() => Render("hell", "bindungen-standorte", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Standorte", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Standorte & Räume");
                return Task.CompletedTask;
            });
            var locations = shell.AdminPage.Locations!;
            await AssertNoBindingErrors(log, "Anderer Standort", () => { locations.SelectedChoice = locations.Choices.Single(c => c.Title == "Salzburg"); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Standort anlegen", () => { locations.SelectedChoice = locations.Choices.Single(c => c.Id is null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Zurück zum ersten Standort", () => { locations.SelectedChoice = locations.Choices[0]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neuer Raum eingegeben", () => { locations.NewRoom!.Name = "Ruheraum Nord"; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                locations.NewRoom!.Name = "";
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, LocationsApi());

    [Fact]
    public Task Jede_Seite_Standorte_und_Raeume_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-standorte", async (window, main) =>
    {
        var shell = await OpenLocations(window, main);
        var locations = shell.AdminPage.Locations!;
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        void Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }
        }

        Scan("Standort");
        locations.NewRoom!.Name = "Ruheraum Nord";
        locations.Rooms[0].Name = "Raum Lavendel 2";
        await Pump(Settling);
        Scan("Zeilen geändert");
        locations.SelectedChoice = locations.Choices.Single(c => c.Id is null);
        await Pump(Settling);
        Scan("Standort anlegen");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, LocationsApi());
}

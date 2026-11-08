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

/// <summary>R05 Slice 95 (Ä191): native Maske „Kalender-Abos“ im Fenster: Eingabe per Maus und Tastatur, Strg+S, Abrufen, Importieren, Entscheidungen und Löschen, Scans.</summary>
public sealed partial class ViewTests
{
    private static readonly Guid AdminHolidayId = Guid.Parse("00000000-0000-0000-0000-0000000c4051");
    private static readonly Guid AdminSchoolId = Guid.Parse("00000000-0000-0000-0000-0000000c4052");

    private static CalendarSubscriptionDto AdminSubscription(Guid id, string name, SubscriptionKind kind, int conflicts = 0, bool preset = false) =>
        new(id, name, "https://calendar.example.org/" + id, kind, "AT", null, true, preset, true, SpaSample.At(9), SubscriptionFetchResult.Ok, null, null, 136, new DateOnly(2026, 1, 1),
            new DateOnly(2028, 12, 26), conflicts);

    private static string SubscriptionRoute(string tail = "") => $"/api/calendar-subscriptions{tail}";

    private static StubApi AdminSubscriptionsApi(StubApi? api = null)
    {
        var list = new List<CalendarSubscriptionDto>
        {
            AdminSubscription(AdminHolidayId, "Feiertage Wien", SubscriptionKind.PublicHoliday, conflicts: 1),
            AdminSubscription(AdminSchoolId, "Schulferien Wien", SubscriptionKind.SchoolHoliday),
        };
        return (api ?? Api())
            .On($"GET {SubscriptionRoute()}", list)
            .On("GET /api/holidays/regions", new List<HolidayRegionDto> { new("AT-9", FiscalCountry.AT, "Wien"), new("AT-5", FiscalCountry.AT, "Salzburg") })
            .On($"GET {SubscriptionRoute("/sources")}", new List<SubscriptionSource>
            {
                new("at-feiertage", "Feiertage Österreich", "https://example.org/at.ics", SubscriptionKind.PublicHoliday, "AT", "Offizielle Liste.", new DateOnly(2026, 2, 1)),
            })
            .On($"GET {SubscriptionRoute("/" + AdminHolidayId + "/conflicts")}", new List<SubscriptionConflictDto>
            {
                new(new DateOnly(2026, 12, 8), "Mariä Empfängnis", SubscriptionConflictKind.OnlySubscription, null, ["Wien"], null),
            })
            .On($"GET {SubscriptionRoute("/" + AdminSchoolId + "/conflicts")}", new List<SubscriptionConflictDto>())
            .On($"PUT {SubscriptionRoute("/" + AdminHolidayId + "/decisions")}", null, System.Net.HttpStatusCode.NoContent)
            .On($"POST {SubscriptionRoute("/" + AdminHolidayId + "/fetch")}", new { }, System.Net.HttpStatusCode.Accepted)
            .OnJson($"POST {SubscriptionRoute("/" + AdminHolidayId + "/import")}", _ => new SubscriptionRefreshResult(4, 0, 140, "4 Tage neu, 0 entfernt.", SubscriptionFetchResult.Ok))
            .OnJson($"POST {SubscriptionRoute()}", body =>
            {
                var input = JsonSerializer.Deserialize<CalendarSubscriptionInput>(body!, ApiClient.Json)!;
                var created = AdminSubscription(Guid.NewGuid(), input.Name, input.Kind) with { Url = input.Url, Region = input.Region };
                list.Add(created);
                return created;
            }, System.Net.HttpStatusCode.Created)
            .OnJson($"PUT {SubscriptionRoute("/" + AdminHolidayId)}", body =>
            {
                var input = JsonSerializer.Deserialize<CalendarSubscriptionInput>(body!, ApiClient.Json)!;
                var changed = list[0] with { Name = input.Name, Url = input.Url, IsActive = input.IsActive };
                list[0] = changed;
                return changed;
            })
            .On($"DELETE {SubscriptionRoute("/" + AdminHolidayId)}", null, System.Net.HttpStatusCode.NoContent);
    }

    private static async Task<ShellViewModel> OpenAdminSubscriptions(Window window, MainViewModel main, FakeFilePicker? files = null)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.Subscriptions!.Files = files;
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Kalender-Abos");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Subscriptions, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Subscriptions!.IsLoaded);
        return shell;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Kalender_Abos(string theme) => Render(theme, "verwaltung-kalender-abos", async (window, main) =>
    {
        var shell = await OpenAdminSubscriptions(window, main);
        var subscriptions = shell.AdminPage.Subscriptions!;
        Assert.Equal(["Feiertage Wien", "Schulferien Wien"], subscriptions.Rows.Select(r => r.Title));
        Assert.Equal("Feiertage Wien", Named<TextBox>(window, "NameField").Text);
        Assert.True(Shows(window, "Mariä Empfängnis"));
        Assert.True(Shows(window, "Stand heute · 136 Tage"));
        return shell.AdminPage;
    }, AdminSubscriptionsApi());

    private static async Task TypeIntoSuggest(Window window, string field, string text)
    {
        var box = Named<AutoCompleteBox>(window, field);
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump();
        box.GetVisualDescendants().OfType<TextBox>().Single().SelectAll();
        window.KeyTextInput(text);
        await Pump(Settling);
    }

    [Fact]
    public Task Region_zeigt_Vorschlaege_und_uebernimmt_einen_per_Tastatur_aber_Freitext_bleibt()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-region", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);
            var subscriptions = shell.AdminPage.Subscriptions!;
            var box = Named<AutoCompleteBox>(window, "RegionField");

            await TypeIntoSuggest(window, "RegionField", "AT-");
            Assert.True(box.IsDropDownOpen);

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Pump(Settling);
            Assert.Equal("AT-9", subscriptions.Region);

            await TypeIntoSuggest(window, "RegionField", "xx");
            Assert.Contains("xx", subscriptions.Region, StringComparison.Ordinal);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Abo_anlegen_ueber_Klick_und_Strg_S()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-anlegen", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);
            var subscriptions = shell.AdminPage.Subscriptions!;

            ClickCenter(window, Named<Button>(window, "NewButton"));
            await Pump(Settling);
            Assert.True(subscriptions.IsCreating);
            await TypeInto(window, "NameField", "Feiertage Salzburg");
            await TypeInto(window, "UrlField", "https://example.org/sbg.ics");
            await TypeIntoSuggest(window, "RegionField", "at-5");
            PressSave(window);
            await Pump(Settling);

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {SubscriptionRoute()}");
            var input = JsonSerializer.Deserialize<CalendarSubscriptionInput>(post.Body!, ApiClient.Json)!;
            Assert.Equal(("Feiertage Salzburg", "https://example.org/sbg.ics", "AT-5", SubscriptionKind.PublicHoliday), (input.Name, input.Url, input.Region, input.Kind));
            Assert.False(subscriptions.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Abo_aendern_und_Strg_S_sendet_PUT()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-aendern", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);
            var subscriptions = shell.AdminPage.Subscriptions!;

            await TypeInto(window, "NameField", "Feiertage Österreich");
            Assert.True(subscriptions.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT {SubscriptionRoute("/" + AdminHolidayId)}");
            Assert.Equal("Feiertage Österreich", JsonSerializer.Deserialize<CalendarSubscriptionInput>(put.Body!, ApiClient.Json)!.Name);
            Assert.False(subscriptions.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Name_leer_und_Strg_S_zeigt_den_Fehler_am_Feld()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-fehler", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);

            await TypeInto(window, "NameField", " ");
            PressSave(window);
            await Pump(Settling);

            Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
            Assert.True(Shows(window, "Name mit höchstens 120 Zeichen angeben."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Jetzt_abrufen_ueber_Klick_sendet_POST_fetch()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-abrufen", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);

            await ClickScrolled(window, "FetchButton");

            Assert.Single(api.Calls, c => c.Route == $"POST {SubscriptionRoute("/" + AdminHolidayId + "/fetch")}");
            Assert.True(Shows(window, "Der Abruf läuft im Hintergrund."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Datei_importieren_ueber_Klick_sendet_den_Inhalt_der_gewaehlten_Datei()
    {
        var api = AdminSubscriptionsApi();
        var files = new FakeFilePicker { File = new PickedFile("ferien.csv", 20, "Datum;Titel\n2026-12-08;Feiertag") };
        return Render("hell", "verwaltung-kalender-abos-import", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main, files);

            await ClickScrolled(window, "ImportButton");

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {SubscriptionRoute("/" + AdminHolidayId + "/import")}");
            Assert.Equal(new SubscriptionImportInput("ferien.csv", "Datum;Titel\n2026-12-08;Feiertag"), JsonSerializer.Deserialize<SubscriptionImportInput>(post.Body!, ApiClient.Json));
            Assert.True(Shows(window, "4 Tage neu, 0 entfernt."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Widerspruch_uebernehmen_ueber_Klick_sendet_die_Entscheidung()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-widerspruch", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);
            var accept = window.GetVisualDescendants().OfType<CheckBox>().First(c => (AutomationProperties.GetName(c) ?? "").StartsWith("Übernehmen,", StringComparison.Ordinal));
            accept.BringIntoView();
            await Pump(Settling);

            ClickCenter(window, accept);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == $"PUT {SubscriptionRoute("/" + AdminHolidayId + "/decisions")}");
            Assert.Equal(new SubscriptionDecisionInput(new DateOnly(2026, 12, 8), SubscriptionDecision.Accepted), JsonSerializer.Deserialize<SubscriptionDecisionInput>(put.Body!, ApiClient.Json));
            Assert.True(Shows(window, "Tag übernommen"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Kontrollkaestchen_springen_nach_einem_API_Fehler_ueber_Klick_zurueck()
    {
        var api = AdminSubscriptionsApi()
            .On($"PUT {SubscriptionRoute("/" + AdminHolidayId + "/show")}", null, System.Net.HttpStatusCode.InternalServerError)
            .On($"PUT {SubscriptionRoute("/" + AdminHolidayId + "/decisions")}", null, System.Net.HttpStatusCode.InternalServerError);
        return Render("hell", "verwaltung-kalender-abos-fehler-kaestchen", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);
            var show = window.GetVisualDescendants().OfType<CheckBox>().First(c => c.Name == "ShowCheck");
            show.BringIntoView();
            await Pump(Settling);
            Assert.True(show.IsChecked, "show.IsChecked");

            ClickCenter(window, show);
            await Pump(Settling);

            Assert.Contains(api.Calls, c => c.Route == $"PUT {SubscriptionRoute("/" + AdminHolidayId + "/show")}");
            Assert.True(show.IsChecked, "show.IsChecked");
            Assert.True(shell.AdminPage.Subscriptions!.ShowInCalendar, "vm");

            var accept = window.GetVisualDescendants().OfType<CheckBox>().First(c => (AutomationProperties.GetName(c) ?? "").StartsWith("Übernehmen,", StringComparison.Ordinal));
            accept.BringIntoView();
            await Pump(Settling);
            Assert.False(accept.IsChecked);

            ClickCenter(window, accept);
            await Pump(Settling);

            Assert.False(accept.IsChecked);
            Assert.True(shell.AdminPage.Subscriptions!.HasMessage);
            Assert.Equal(InfoBarSeverity.Error, shell.AdminPage.Subscriptions.MessageSeverity);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Abo_loeschen_fragt_nach_und_sendet_DELETE()
    {
        var api = AdminSubscriptionsApi();
        return Render("hell", "verwaltung-kalender-abos-loeschen", async (window, main) =>
        {
            var shell = await OpenAdminSubscriptions(window, main);

            await ClickScrolled(window, "DeleteButton");
            Assert.Equal("Abo löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Single(api.Calls, c => c.Route == $"DELETE {SubscriptionRoute("/" + AdminHolidayId)}");
            Assert.True(Shows(window, "Gelöscht."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Rezeptionistin_sieht_keine_Kalender_Abos() => Render("hell", "verwaltung-rezeption-kalender-abos", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.Subscriptions);
        Assert.False(window.GetVisualDescendants().OfType<SubscriptionsView>().Any());
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Kalender_Abos_melden_keine_Bindungsfehler() => Render("hell", "bindungen-kalender-abos", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Kalender-Abos", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Kalender-Abos");
                return Task.CompletedTask;
            });
            var subscriptions = shell.AdminPage.Subscriptions!;
            await AssertNoBindingErrors(log, "Neues Abo", async () => await subscriptions.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Eingabe", () =>
            {
                subscriptions.SelectedSource = subscriptions.Sources[1];
                subscriptions.Region = "AT-5";
                subscriptions.IsActive = false;
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Verworfen", () => { subscriptions.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderes Abo", () => { subscriptions.SelectedRow = subscriptions.Rows[1]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, AdminSubscriptionsApi());

    [Fact]
    public Task Jede_Seite_Kalender_Abos_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-kalender-abos", async (window, main) =>
    {
        var shell = await OpenAdminSubscriptions(window, main);
        var subscriptions = shell.AdminPage.Subscriptions!;
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

        Scan("Feiertags-Abo mit Widerspruch");
        subscriptions.SelectedRow = subscriptions.Rows[1];
        await Pump(Settling);
        await Pump(Settling);
        Scan("Schulferien-Abo");
        await subscriptions.NewCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Neues Abo");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, AdminSubscriptionsApi());
}

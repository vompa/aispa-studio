using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 93 (Ä191, ADR 0084): native Verwaltung mit Firmendaten, Speichern mit Strg+S, Rückfrage beim Verlassen und Verlauf.</summary>
public sealed partial class ViewTests
{
    private static readonly TenantDto SpaTenant = new(Guid.Parse("0190f5a3-0000-7000-8000-000000000093"), "Day-Spa Wien", "AT",
        CompanyProfile.Empty with { LegalName = "Day-Spa Wien GmbH", Street = "Ringstraße 1", PostalCode = "1010", City = "Wien", Email = "office@day-spa-wien.at" });

    private static readonly List<HistoryEntry> CompanyHistory =
        [new(SpaSample.At(11, 39), "Geändert von Katrin Aigner: Name Day-Spa Wien → Day-Spa am Ring")];

    /// <summary>Speichern gibt zurück, was ankam, wie die API.</summary>
    private static StubApi CompanyApi(StubApi? api = null) => (api ?? Api())
        .OnJson("PUT /api/tenant", body =>
        {
            var input = JsonSerializer.Deserialize<TenantInput>(body!, ApiClient.Json)!;
            return new TenantDto(SpaTenant.Id, input.Name, input.Country, input.Company);
        });

    private static ListBoxItem NavRow(Window window, string title) =>
        window.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.DataContext is NavItem item && item.Title == title);

    private static async Task<ShellViewModel> OpenAdminPage(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        await Pump();
        ClickCenter(window, NavRow(window, "Verwaltung"));
        await Pump(Settling);
        var shell = (ShellViewModel)main.Content!;
        Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
        return shell;
    }

    private static async Task TypeInto(Window window, string field, string text)
    {
        var box = Named<TextBox>(window, field);
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        await Pump();
    }

    private static bool Shows(Window window, string text) =>
        window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text?.Contains(text, StringComparison.Ordinal) == true);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Firmendaten(string theme) => Render(theme, "verwaltung-firmendaten", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Equal("Day-Spa Wien", Named<TextBox>(window, "NameField").Text);
        Assert.Equal("Day-Spa Wien GmbH", Named<TextBox>(window, "LegalNameField").Text);
        return shell.AdminPage;
    });

    [Fact]
    public Task Given_Inhaberin_aendert_den_Firmennamen_When_Strg_S_Then_gespeichert_und_der_Verlauf_zeigt_die_Aenderung()
    {
        var api = CompanyApi();
        return Render("hell", "verwaltung-firmendaten-gespeichert", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var company = shell.AdminPage.Company!;

            await TypeInto(window, "NameField", "Day-Spa am Ring");
            Assert.True(company.HasUnsavedChanges);
            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/tenant");
            Assert.Equal("Day-Spa am Ring", JsonSerializer.Deserialize<TenantInput>(put.Body!, ApiClient.Json)!.Name);
            Assert.False(company.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));

            var history = Named<Control>(window, "HistoryHeader");
            history.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, history);
            await Pump(Settling);
            Assert.True(Shows(window, "Name Day-Spa Wien → Day-Spa am Ring"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_ungespeicherte_Firmendaten_When_die_Inhaberin_zum_Kalender_wechselt_Then_fragt_die_App_Aenderungen_speichern()
    {
        var api = CompanyApi();
        return Render("hell", "verwaltung-rueckfrage", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var company = shell.AdminPage.Company!;
            await TypeInto(window, "NameField", "Day-Spa am Ring");

            ClickCenter(window, NavRow(window, "Kalender"));
            await Pump(Settling);
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Änderungen speichern?", dialog.Title);
            Assert.Equal(("Speichern", "Verwerfen", "Abbrechen"), (dialog.ConfirmText, dialog.AlternativeText, dialog.CancelText));
            Assert.False(dialog.IsDestructive);
            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
            SaveShot(window, "verwaltung-rueckfrage-dialog", "hell");

            // Abbrechen (Esc): bleibt in der Verwaltung, der Text bleibt.
            Press(window, PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);
            Assert.False(main.DialogHost.HasDialog);
            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
            Assert.Equal("Day-Spa am Ring", company.Name);
            // Die Navigationsleiste zeigt wieder die Verwaltung, nicht den angeklickten Kalender.
            Assert.Same(shell.AdminPage, ((NavItem)window.GetVisualDescendants().OfType<ListBox>().First(l => l.Classes.Contains("nav")).SelectedItem!).Page);

            // Verwerfen: wechselt zum Kalender, die Firmendaten stehen wieder auf dem gespeicherten Stand.
            ClickCenter(window, NavRow(window, "Kalender"));
            await Pump(Settling);
            ClickCenter(window, DialogButton(window, "AlternativeButton"));
            await Pump(Settling);
            Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
            Assert.Equal("Day-Spa Wien", company.Name);
            Assert.Equal(0, CallsTo(api, "PUT /api/tenant"));

            // Speichern: sendet und wechselt.
            ClickCenter(window, NavRow(window, "Verwaltung"));
            await Pump(Settling);
            await TypeInto(window, "NameField", "Day-Spa am Ring");
            ClickCenter(window, NavRow(window, "Kalender"));
            await Pump(Settling);
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, "PUT /api/tenant"));
            Assert.Same(shell.Calendar, shell.SelectedPage?.Page);
            Assert.False(company.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Name_des_Spas_leer_When_Strg_S_Then_nichts_gesendet_und_Fehler_am_Feld_mit_Fokus()
    {
        var api = CompanyApi();
        return Render("hell", "verwaltung-firmendaten-fehler", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var name = Named<TextBox>(window, "NameField");
            ClickCenter(window, name);
            name.SelectAll();
            Press(window, PhysicalKey.Delete, RawInputModifiers.None);
            await Pump();
            ClickCenter(window, Named<TextBox>(window, "CityField"));
            await Pump();

            PressSave(window);
            await Pump(Settling);

            Assert.Equal(0, CallsTo(api, "PUT /api/tenant"));
            Assert.Equal("Der Name des Spas fehlt. Trage ihn ein, wie Gäste und Team ihn sehen.", shell.AdminPage.Company!.NameError);
            Assert.True(Shows(window, "Der Name des Spas fehlt."));
            Assert.Same(name, Focused(window));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_geaenderte_Firmendaten_When_Verwerfen_geklickt_Then_steht_der_gespeicherte_Stand_wieder_da() =>
        Render("hell", "verwaltung-firmendaten-verwerfen", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            await TypeInto(window, "CityField", "Salzburg");
            Assert.True(shell.AdminPage.Company!.HasUnsavedChanges);

            ClickCenter(window, Named<Button>(window, "DiscardButton"));
            await Pump(Settling);

            Assert.Equal("Wien", Named<TextBox>(window, "CityField").Text);
            Assert.False(shell.AdminPage.Company.HasUnsavedChanges);
            return shell.AdminPage;
        }, CompanyApi());

    [Fact]
    public Task Given_Website_zum_Einbetten_eingetragen_When_Strg_S_Then_speichert_die_App_auch_die_Einbettung()
    {
        var api = CompanyApi().OnJson("PUT /api/tenant/embedding", body => JsonSerializer.Deserialize<EmbeddingSettings>(body!, ApiClient.Json)!);
        return Render("hell", "verwaltung-firmendaten-einbetten", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            await TypeInto(window, "EmbedField", "https://www.day-spa-wien.at");
            Assert.True(shell.AdminPage.Company!.HasUnsavedChanges);

            PressSave(window);
            await Pump(Settling);

            var put = Assert.Single(api.Calls, c => c.Route == "PUT /api/tenant/embedding");
            Assert.Equal(["https://www.day-spa-wien.at"], JsonSerializer.Deserialize<EmbeddingSettings>(put.Body!, ApiClient.Json)!.AllowedOrigins);
            Assert.False(shell.AdminPage.Company.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Assistent_bietet_Website_eintragen_an_When_geklickt_Then_native_Firmendaten_mit_Fokus_im_Feld_Website() =>
        Render("hell", "verwaltung-firmendaten-website", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;

            shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Website eintragen", "masterData", "company", Field: "company.website"));
            await Pump(Settling);

            Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
            Assert.Equal("Firmendaten", shell.AdminPage.SelectedArea?.Title);
            Assert.Same(Named<TextBox>(window, "WebsiteField"), Focused(window));
            return shell.AdminPage;
        });

    [Fact]
    public Task Given_Rezeptionistin_When_sie_die_Verwaltung_oeffnet_Then_steht_Firmendaten_nicht_im_Balken() =>
        Render("hell", "verwaltung-rezeption", async (window, main) =>
        {
            var shell = await OpenAdminPage(window, main);
            var titles = GroupStrip(window).Children.OfType<TabButton>().Select(Avalonia.Automation.AutomationProperties.GetName).ToList();
            Assert.DoesNotContain("Betrieb", titles);
            Assert.Contains("Lager", titles);
            Assert.False(window.GetVisualDescendants().OfType<CompanyView>().Any());
            return shell.AdminPage;
        }, ReceptionApi());
}

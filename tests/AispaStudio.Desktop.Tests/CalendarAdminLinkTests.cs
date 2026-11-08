using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Vor-/Nachbereitung und Raum aus dem Kalender in der Verwaltung bearbeiten.</summary>
public sealed class CalendarAdminLinkTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static CurrentUser Manager() => TestData.Reception() with { Rights = [.. TestData.Reception().Rights, AccessRight.ManageCatalog, AccessRight.ManageLocation] };

    private static async Task<(CalendarViewModel Calendar, List<(string Page, string? Tab, Guid? Id)> Requests)> OpenAsync(CurrentUser user, bool withAdmin = true)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(TestData.Appointment(10, TestData.Anna)));
        var calendar = new CalendarViewModel(api.Client(), user, null, new FixedTime());
        var requests = new List<(string, string?, Guid?)>();
        if (withAdmin)
        {
            calendar.AdminRequested = (page, tab, id) => requests.Add((page, tab, id));
        }

        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        return (calendar, requests);
    }

    [Fact]
    public async Task Rechtsklick_auf_den_Streifen_und_den_Termin_bietet_Vor_und_Nachbereitung_zum_Bearbeiten()
    {
        var (calendar, requests) = await OpenAsync(Manager());
        var item = calendar.Columns[0].Items.Single();
        var stripe = calendar.Columns[0].Stripes[0];

        var stripeMenu = calendar.ActionsForStripe(stripe);
        Assert.Equal(["Zeiten dieses Termins ändern …", "Behandlungszeiten bearbeiten …"], stripeMenu.Select(a => a.Header));
        var menu = stripeMenu[1];
        Assert.Contains(calendar.ActionsFor(item), a => a.Header == "Behandlungszeiten bearbeiten …");

        menu.Run!();
        Assert.Equal([("masterData", "treatments", (Guid?)TestData.MassageTreatment.Id)], requests);
    }

    [Fact]
    public async Task Ohne_Recht_auf_den_Katalog_oder_ohne_Verwaltung_gibt_es_den_Eintrag_nicht()
    {
        var (reception, _) = await OpenAsync(TestData.Reception());
        // „ändern“ gilt nur für diesen Termin und braucht das Buchungsrecht, nicht die Verwaltung.
        Assert.Equal(["Zeiten dieses Termins ändern …"], reception.ActionsForStripe(reception.Columns[0].Stripes[0]).Select(a => a.Header));
        Assert.DoesNotContain(reception.ActionsFor(reception.Columns[0].Items.Single()), a => a.Header == "Behandlungszeiten bearbeiten …");

        var (withoutAdmin, _) = await OpenAsync(Manager(), withAdmin: false);
        Assert.Equal(["Zeiten dieses Termins ändern …"], withoutAdmin.ActionsForStripe(withoutAdmin.Columns[0].Stripes[0]).Select(a => a.Header));
    }

    [Fact]
    public async Task Die_Arbeitsflaeche_wechselt_zur_Verwaltung_und_gibt_das_Ziel_mit()
    {
        var shell = new ShellViewModel(new StubApi().Client(), Manager(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        await Task.CompletedTask;

        shell.Calendar.AdminRequested!("masterData", "treatments", Guid.Empty);

        Assert.Same(shell.AdminPage, shell.SelectedPage!.Page);
        Assert.Equal("Behandlungen", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Treatments, shell.AdminPage.Content);
    }
}

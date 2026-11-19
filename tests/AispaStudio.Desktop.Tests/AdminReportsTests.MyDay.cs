using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Mein Tag“ (Masken-Überarbeitung P9): Kopf der Karte, Statuszeile je Termin, Gerät zum Abhaken und Vorlieben nur einmal.</summary>
public sealed partial class AdminReportsTests
{
    private static async Task<TreatmentCardRow> OpenLavaCard(StubApi? api = null)
    {
        api ??= AddReports(new StubApi());
        api.On(DayPlanRoute, LavaDay());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        await reports.DayPlan.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);
        return Assert.Single(reports.DayPlan.Cards);
    }

    [Fact]
    public async Task Mein_Tag_Kopf_der_Karte_nennt_Uhrzeit_und_Namen_und_Dauer_und_Raum_als_Kennzeichen()
    {
        var card = await OpenLavaCard();

        Assert.Equal("10:00 Lava-Hot-Stone-Massage", card.Title);
        Assert.Equal("60 Min.", card.DurationText);
        Assert.Equal("Raum 1", card.RoomName);
    }

    [Fact]
    public async Task Mein_Tag_Statuszeile_nennt_die_offenen_Schritte_samt_Geraet_und_wird_bei_Haken_zu_Bereit()
    {
        var api = AddReports(new StubApi())
            .On($"PUT /api/reports/appointments/{AppointmentId}/preparation/{StepId}", new { })
            .On($"PUT /api/reports/appointments/{AppointmentId}/equipment-prepared", new { });
        var card = await OpenLavaCard(api);

        Assert.Equal("erste bis 09:30", card.StatusText);
        Assert.False(card.IsReady);

        card.Todo[0].Done = true;
        await card.Todo[0].ToggleCommand.ExecuteAsync(null);
        Assert.Equal("1 Vorbereitung offen", card.StatusText);

        card.Todo[1].Done = true;
        await card.Todo[1].ToggleCommand.ExecuteAsync(null);
        Assert.Equal("Bereit", card.StatusText);
        Assert.True(card.IsReady);
        Assert.Equal($"PUT /api/reports/appointments/{AppointmentId}/equipment-prepared", api.Calls[^1].Route);
    }

    [Fact]
    public async Task Mein_Tag_zeigt_Vorlieben_unter_dem_Vornamen_und_nicht_mehr_als_Vorbereitung()
    {
        var card = await OpenLavaCard();

        Assert.Equal("Das wünscht sich Anna", card.WishesHeading);
        Assert.Equal(["Druck: fest", "Duft: Lavendel", "Fokus: Nacken", "Wenig Gespräch"], card.Wishes);
        Assert.Equal(["Letzte Notiz (09.10., Lisa): Nacken verspannt"], card.TodoNotes);
        Assert.Equal(["Steine aufheizen", "Steinwärmer bereitstellen"], card.Todo.Select(t => t.Text));
    }

    [Fact]
    public async Task Mein_Tag_Status_der_Seite_zaehlt_offene_Vorbereitungen_mit()
    {
        var api = AddReports(new StubApi()).On(DayPlanRoute, LavaDay());
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        await reports.DayPlan.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);

        Assert.Equal("2 Vorbereitungen offen, 1 Hinweis zum Tag", reports.DayPlan.StatusText);
        Assert.False(reports.DayPlan.StatusIsOk);
    }
}

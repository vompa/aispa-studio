using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Hinweis in der Storno-Rückfrage (L3): Liegt die Absage innerhalb der Stornofrist, nennt die Rückfrage Frist und Gebühr laut Regel des Standorts
/// und belegt den Grund mit „Kurzfristig an der Rezeption storniert“ vor; außerhalb der Frist oder ohne Regel bleibt alles wie bisher.
/// </summary>
public sealed class AppointmentCancelNoticeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    /// <summary>Montag 07:00 Ortszeit; der Termin um 10:00 steht bevor.</summary>
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(7);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<(AppointmentViewModel Panel, FakeDialogs Dialogs)> OpenAsync(CardRequestOffer offer, CurrentUser? user = null)
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment))
            .On($"GET /api/appointments/{appointment.Id}/card-guarantee/offer", offer);
        var dialogs = new FakeDialogs();
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime()) { Dialogs = dialogs };
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;
        await panel.Loads;
        return (panel, dialogs);
    }

    /// <summary>Angebot des Servers: Frist 24 Stunden vor dem Termin um 10:00, also Sonntag 10:00 und damit schon vorbei.</summary>
    private static CardRequestOffer Offer(int feePercent, int deadlineHoursBefore = 24) =>
        new(false, 39.50m, "EUR", feePercent, "Für den Gast ist keine E-Mail-Adresse hinterlegt.", TestData.At(10).AddHours(-deadlineHoursBefore));

    [Fact]
    public async Task Innerhalb_der_Frist_nennt_die_Rueckfrage_Frist_und_Gebuehr_und_belegt_den_Grund_vor()
    {
        var (panel, dialogs) = await OpenAsync(Offer(50));

        await panel.AskCancelCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogs.Asked);
        Assert.Equal("Absage innerhalb der Stornofrist (bis 28.02. 10:00). Laut Regel fallen 50 % Ausfallgebühr an; belasten kann die Standortleitung.", request.Notice);
        Assert.Equal("Kurzfristig an der Rezeption storniert", request.InputText);
        Assert.Equal("Termin stornieren?", request.Title);
        dialogs.AssertRules();
    }

    [Fact]
    public async Task Mit_Kassenaufsicht_nennt_der_Hinweis_den_Weg_zum_Belasten()
    {
        var (panel, dialogs) = await OpenAsync(Offer(50), TestData.Owner());

        await panel.AskCancelCommand.ExecuteAsync(null);

        Assert.EndsWith("Laut Regel fallen 50 % Ausfallgebühr an. Belasten unter „Ausfallgebühr belasten …“ nach dem Stornieren.", Assert.Single(dialogs.Asked).Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Innerhalb_der_Frist_ohne_Gebuehr_nennt_die_Rueckfrage_nur_die_Frist()
    {
        var (panel, dialogs) = await OpenAsync(Offer(0));

        await panel.AskCancelCommand.ExecuteAsync(null);

        Assert.Equal("Absage innerhalb der Stornofrist (bis 28.02. 10:00).", Assert.Single(dialogs.Asked).Notice);
    }

    [Fact]
    public async Task Ausserhalb_der_Frist_gibt_es_keine_Zeile_und_den_gewohnten_Grund()
    {
        var (panel, dialogs) = await OpenAsync(Offer(50, deadlineHoursBefore: 1));

        await panel.AskCancelCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogs.Asked);
        Assert.Null(request.Notice);
        Assert.Equal("An der Rezeption storniert", request.InputText);
    }
}

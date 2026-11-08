using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 115 (Ä251): „Passt dazu“ im Termin-Panel. Das Panel fragt die Vorschläge nur für einen erledigten Termin ab und nur, wenn die Person kassieren darf;
/// ein Live-Echo baut das Panel neu auf, ohne den Hinweis neu zu laden. Klicks auf „Erledigt“ und „Mit kassieren“ prüft <c>ViewTests.Recommendations</c>.
/// </summary>
public sealed class AppointmentRecommendationTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(12);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly RecommendedArticleDto Oil = new(Guid.NewGuid(), "Massageöl Lavendel 100 ml", 24.90m, 4, "Stück");

    private static string RecommendationsRoute(AppointmentDto appointment) => $"GET /api/appointments/{appointment.Id}/recommendations";

    /// <summary>Darf den Kalender sehen und den Status setzen, aber nicht buchen und kassieren.</summary>
    private static CurrentUser Therapist() =>
        new("u4", "anna@aispa.test", "Anna", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Therapist, true, []), TestData.Anna.Id,
            [AccessRight.ViewCalendar, AccessRight.ChangeStatus]);

    private static async Task<CalendarViewModel> CalendarWith(StubApi api, AppointmentDto appointment, CurrentUser user)
    {
        api.On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment));
        var calendar = new CalendarViewModel(api.Client(), user, null, new FixedTime())
        {
            CheckoutRequested = _ => Task.CompletedTask,
            CheckoutWithArticleRequested = (_, _) => Task.CompletedTask,
        };
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        return calendar;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Given_ein_erledigter_Termin_ohne_Recht_zu_kassieren_When_das_Panel_offen_ist_Then_kein_Hinweis()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var api = new StubApi().On(RecommendationsRoute(appointment), new List<RecommendedArticleDto> { Oil });
        var calendar = await CalendarWith(api, appointment, Therapist());

        calendar.OpenAppointment(appointment);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(calendar.Selected!.HasRecommendations);
        Assert.DoesNotContain(api.Calls, c => c.Route == RecommendationsRoute(appointment));
    }

    [Fact]
    public async Task Given_ein_erledigter_Termin_mit_Vorschlag_When_das_Panel_offen_ist_Then_nennt_der_Hinweis_Name_Preis_und_Regal()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var api = new StubApi().On(RecommendationsRoute(appointment), new List<RecommendedArticleDto> { Oil });
        var calendar = await CalendarWith(api, appointment, TestData.Owner());

        calendar.OpenAppointment(appointment);
        await Until(() => calendar.Selected!.HasRecommendations);

        var hint = Assert.Single(calendar.Selected!.Recommendations);
        Assert.Equal(("Massageöl Lavendel 100 ml · 24,90 € · 4 am Regal", "Massageöl Lavendel 100 ml mit kassieren"), (hint.Text, hint.CheckoutName));
    }

    [Fact]
    public async Task Given_ein_erledigter_Termin_ohne_Vorschlag_When_das_Panel_offen_ist_Then_kein_Hinweis()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var api = new StubApi().On(RecommendationsRoute(appointment), new List<RecommendedArticleDto>());
        var calendar = await CalendarWith(api, appointment, TestData.Owner());

        calendar.OpenAppointment(appointment);
        await Until(() => api.Calls.Any(c => c.Route == RecommendationsRoute(appointment)));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(calendar.Selected!.HasRecommendations);
    }

    [Fact]
    public async Task Given_der_Hinweis_steht_When_ein_Live_Echo_das_Panel_neu_aufbaut_Then_bleibt_er_ohne_neue_Abfrage()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var api = new StubApi().On(RecommendationsRoute(appointment), new List<RecommendedArticleDto> { Oil });
        var calendar = await CalendarWith(api, appointment, TestData.Owner());
        calendar.OpenAppointment(appointment);
        await Until(() => calendar.Selected!.HasRecommendations);
        var before = calendar.Selected;

        calendar.ApplyLive(appointment with { UpdatedAt = TestData.At(11) });

        Assert.NotSame(before, calendar.Selected);
        Assert.True(calendar.Selected!.HasRecommendations);
        Assert.Single(api.Calls, c => c.Route == RecommendationsRoute(appointment));
    }
}

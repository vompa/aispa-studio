using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// „Betrieb einrichten“ nach ADR 0104, Entscheidung 6: ein Schritt ist erst erledigt, wenn er für das Buchen taugt, und nennt sonst, was fehlt;
/// die Bereitschaftsprüfung sucht über den Planer den ersten freien Platz in den nächsten 14 Tagen oder nennt den Grund.
/// </summary>
public sealed class SetupProgressTests(ApiFixture fixture)
{
    private const string Path = "/api/setup/progress";

    private const string WithReadiness = Path + "?readiness=true";

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DayOfWeek[] EveryDay = Enum.GetValues<DayOfWeek>();

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body) =>
        await TestSpa.ReadAsync<T>(await client.PostAsJsonAsync(path, body, TestSpa.Json, Ct), Ct);

    private static async Task<SetupProgressDto> ProgressAsync(HttpClient client) => await TestSpa.ReadAsync<SetupProgressDto>(await client.GetAsync(WithReadiness, Ct), Ct);

    /// <summary>Ein Standort, der jeden Tag geöffnet hat, mit einem Raum und einer Behandlung, die <paramref name="qualifications"/> braucht.</summary>
    private static async Task<LocationDto> LocationWithTreatmentAsync(HttpClient owner, Guid[] qualifications)
    {
        var location = await PostAsync<LocationDto>(owner, "/api/locations",
            new LocationInput("Wien", "Europe/Vienna", "EUR", [.. EveryDay.Select(d => new OpeningHours(d, new(9, 0), new(20, 0)))]));
        var room = await PostAsync<RoomDto>(owner, $"/api/locations/{location.Id}/rooms", new RoomInput("Raum A", 0));
        _ = await PostAsync<TreatmentDto>(owner, "/api/treatments", new TreatmentInput("Massage", 50, 0, 0, 79m, qualifications, [room.Id], []));
        return location;
    }

    private static SetupStepDto Step(SetupProgressDto progress, string key) => progress.Steps.Single(s => s.Key == key);

    [Fact]
    public async Task Given_eine_Person_ohne_Arbeitszeiten_When_der_Stand_gelesen_wird_Then_ist_Team_offen_mit_Satz_und_niemand_hat_Dienst()
    {
        using var owner = await TestUsers.ClientAsync(fixture, Ct);
        _ = await LocationWithTreatmentAsync(owner, []);
        _ = await PostAsync<EmployeeDto>(owner, "/api/employees", new EmployeeInput("Anna", "#2f6f5e", [], []));

        var progress = await ProgressAsync(owner);

        var people = Step(progress, "people");
        Assert.False(people.Done);
        Assert.Equal(SetupProgress.NoHours, people.Missing);
        Assert.False(progress.Readiness!.Ready);
        Assert.Equal(SetupReadiness.NoDuty, progress.Readiness.Reason);
    }

    [Fact]
    public async Task Given_eine_Behandlung_mit_einer_Qualifikation_die_niemand_hat_When_der_Stand_gelesen_wird_Then_nicht_bereit_und_der_Grund_nennt_sie()
    {
        using var owner = await TestUsers.ClientAsync(fixture, Ct);
        var hotStone = await PostAsync<QualificationDto>(owner, "/api/qualifications", new QualificationInput($"Hot Stone {Guid.NewGuid():N}"));
        var location = await LocationWithTreatmentAsync(owner, [hotStone.Id]);
        _ = await PostAsync<EmployeeDto>(owner, "/api/employees",
            new EmployeeInput("Anna", "#2f6f5e", [], [.. EveryDay.Select(d => new WorkingHours(location.Id, d, new(9, 0), new(17, 0)))]));

        var progress = await ProgressAsync(owner);

        Assert.False(Step(progress, "people").Done);
        Assert.False(progress.Readiness!.Ready);
        Assert.Null(progress.Readiness.FirstSlot);
        Assert.Equal(SetupReadiness.NoQualification("Massage", [hotStone.Name]), progress.Readiness.Reason);
    }

    [Fact]
    public async Task Given_ein_eingerichteter_Betrieb_When_der_Stand_gelesen_wird_Then_bereit_mit_dem_ersten_freien_Platz_in_14_Tagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var before = DateTimeOffset.UtcNow;

        var readiness = (await spa.GetAsync<SetupProgressDto>(WithReadiness, Ct)).Readiness!;

        Assert.True(readiness.Ready);
        Assert.Null(readiness.Reason);
        var slot = readiness.FirstSlot!;
        Assert.Equal((spa.Location.Id, spa.MassageTreatment.Id), (slot.LocationId, slot.TreatmentId));
        Assert.InRange(slot.Start, before.AddMinutes(-1), before.AddDays(15));
    }

    [Fact]
    public async Task Given_ein_leerer_Betrieb_When_der_Stand_gelesen_wird_Then_nicht_bereit_wegen_der_Oeffnungszeiten()
    {
        using var owner = await TestUsers.ClientAsync(fixture, Ct);

        var readiness = (await ProgressAsync(owner)).Readiness!;

        Assert.False(readiness.Ready);
        Assert.Equal(SetupReadiness.NoOpeningHours, readiness.Reason);
    }

    [Fact]
    public async Task Given_eine_Behandlung_ohne_Preis_When_der_Stand_gelesen_wird_Then_bleibt_der_Schritt_erledigt_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Fußbad", 20, 0, 0, 0m, [], [spa.RoomA.Id], []), Ct);

        var progress = await spa.GetAsync<SetupProgressDto>(Path, Ct);

        var treatments = Step(progress, "treatments");
        Assert.True(treatments.Done);
        Assert.Null(treatments.Missing);
        Assert.Equal(SetupProgress.MissingPrices(1), treatments.Note);
        Assert.Null(progress.Readiness);
    }

    [Fact]
    public async Task Given_ein_geschlossener_Standort_mit_Behandlungen_und_ein_offener_When_die_Bereitschaft_geprueft_wird_Then_bereit_am_offenen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // „Baden“ steht vor „Wien“ und hätte mit seinen Behandlungen alle durchsuchten Paare belegt.
        var baden = await spa.CreateLocationAsync("Baden", [.. EveryDay], Ct);
        var room = await spa.PostAsync<RoomDto>($"/api/locations/{baden.Id}/rooms", new RoomInput("Raum Baden", 0), Ct);
        foreach (var name in new[] { "Bad A", "Bad B", "Bad C" })
        {
            _ = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput(name, 50, 0, 0, 79m, [spa.Massage.Id], [room.Id], []), Ct);
        }

        _ = await spa.PostAsync<EmployeeDto>("/api/employees",
            new EmployeeInput("Cleo", "#2f6f5e", [spa.Massage.Id], [.. EveryDay.Select(d => new WorkingHours(baden.Id, d, new(9, 0), new(17, 0)))]), Ct);
        _ = await spa.PostAsync<LocationDto>($"/api/locations/{baden.Id}/close", new LocationCloseInput(Today), Ct);

        var readiness = (await spa.GetAsync<SetupProgressDto>(WithReadiness, Ct)).Readiness!;

        Assert.True(readiness.Ready);
        Assert.Equal(spa.Location.Id, readiness.FirstSlot!.LocationId);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_ueber_die_naechsten_Tage_When_die_Bereitschaft_geprueft_wird_Then_der_erste_freie_Platz_danach()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.PostAsync<EmployeeDto>("/api/employees",
            new EmployeeInput("Cleo", "#2f6f5e", [spa.Massage.Id], [.. EveryDay.Select(d => new WorkingHours(spa.Location.Id, d, new(9, 0), new(17, 0)))]), Ct);
        // Länger als die erlaubten Suchen: jeder geschlossene Tag, der eine Suche kostete, ließe am Ende „keine Zeit frei“.
        var lastClosed = Today.AddDays(SetupReadiness.MaxContextLoads);
        _ = await spa.PostAsync<ClosureDto>($"/api/locations/{spa.Location.Id}/closures",
            new ClosureInput(Today, null, lastClosed, null, ClosureKind.Vacation, null, null), Ct);

        var readiness = (await spa.GetAsync<SetupProgressDto>(WithReadiness, Ct)).Readiness!;

        Assert.True(readiness.Ready, readiness.Reason);
        Assert.True(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readiness.FirstSlot!.Start, Vienna).DateTime) > lastClosed);
    }
}

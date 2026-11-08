using System.Net.Http.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Hilfen für die Tests der Antwortgeber (ADR 0072): eine Frage über die API stellen (mit Rechten und Zeit der Wirklichkeit) oder direkt an die Antwortgeber
/// mit festem Zeitpunkt und gewählten Rechten, damit „heute“, „jetzt“ und Zeiträume im Test feststehen.
/// </summary>
internal sealed class AnswerKit(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Mittwoch, 3. März 2027, 11:00 Uhr in Wien (10:00 UTC); der Montag davor ist der Tag der Testtermine (<see cref="TestSpa.Monday"/>).</summary>
    public static readonly DateTimeOffset Now = new(2027, 3, 3, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Frage an den Assistenten über die API, mit dem Konto des Clients.</summary>
    public static async Task<AssistantAnswer> AskAsync(HttpClient client, string question, Guid? location)
    {
        var response = await client.PostAsJsonAsync("/api/assistant", new AssistantQuestion(question, location), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssistantAnswer>(TestSpa.Json, Ct))!;
    }

    /// <summary>Block der Antwort; die Antwort muss einen haben.</summary>
    public static async Task<AssistantBlock> BlockAsync(HttpClient client, string question, Guid? location)
    {
        var answer = await AskAsync(client, question, location);
        return answer.Block ?? throw new InvalidOperationException($"Keine Betriebsantwort auf „{question}“: {answer.Text}");
    }

    /// <summary>
    /// Frage direkt an die Antwortgeber, mit dem Zugang <paramref name="role"/> an allen Standorten. Die Absicht erkennen die Regeln; <paramref name="now"/> ist „jetzt“.
    /// </summary>
    public async Task<AssistantBlock> AnswerAsync(TestSpa spa, string question, DateTimeOffset? now = null, StaffRole role = StaffRole.Owner, bool amountReports = false,
        Guid? employeeId = null, Guid? locationId = null, bool plainReports = true)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == (locationId ?? spa.Location.Id), Ct);
        var at = now ?? Now;
        var zone = LocalTime.Zone(location.TimeZone);
        var today = LocalTime.LocalDate(at, zone);
        var intent = BusinessIntents.Detect(question, today) ?? throw new InvalidOperationException($"Keine Betriebsfrage: {question}");
        var access = new UserAccess("tester", role, true, new HashSet<Guid>(), employeeId, amountReports, plainReports);
        return await scope.ServiceProvider.GetRequiredService<BusinessAnswers>()
            .AnswerAsync(BusinessIntents.Question(intent, question, today)!, new AnswerContext(access, location, zone, at, today), Ct);
    }

    /// <summary>
    /// Frage an den ganzen Assistenten (<see cref="RoleAssistant"/>) mit festem „jetzt“ (<see cref="Now"/>): ohne <paramref name="model"/> mit den Regeln, mit einem Modell so,
    /// wie er mit Claude läuft. Ohne <paramref name="locations"/> sieht der Zugang alle Standorte; der Standort der Frage ist der des Tests.
    /// </summary>
    public async Task<AssistantAnswer> StaffAsync(TestSpa spa, AssistantQuestion question, IAiJson? model = null, StaffRole role = StaffRole.Owner,
        IReadOnlySet<Guid>? locations = null, DateTimeOffset? now = null, Guid? employeeId = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        IAssistantClassifier classifier = model is null ? new RuleBasedAssistantClassifier() : new ModelAssistantClassifier(model);
        IAnswerPhraser phraser = model is null ? new NoAnswerPhraser() : new ModelAnswerPhraser(model, NullLogger<ModelAnswerPhraser>.Instance);
        var assistant = ActivatorUtilities.CreateInstance<RoleAssistant>(scope.ServiceProvider, classifier, phraser, new FixedTime(now ?? Now));
        var access = new UserAccess("tester", role, locations is null, locations ?? new HashSet<Guid>(), employeeId);
        return await assistant.AskStaffAsync(access, question with { LocationId = question.LocationId ?? spa.Location.Id }, Ct);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Ein Zugang mit der Rolle im Spa des Tests (über eine Einladung, wie im Betrieb); optional mit dem Recht „Berichte mit Beträgen“.</summary>
    public async Task<HttpClient> JoinAsync(TestSpa spa, StaffRole role, bool amountReports = false, bool allLocations = true, Guid? employee = null, bool plainReports = true)
    {
        var email = $"{role}-{Guid.NewGuid():N}@aispa.test".ToLowerInvariant();
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, $"{role} Person", role, allLocations, allLocations ? [] : [spa.Location.Id], employee, null, amountReports, plainReports), Ct);
        using var anonymous = fixture.CreateClient();
        var accepted = await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        accepted.EnsureSuccessStatusCode();
        return fixture.CreateClient(string.Join("; ", accepted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    /// <summary>Standort mit Öffnungszeiten, Region und Kontakt, für Feiertage und Kontaktfragen.</summary>
    public static async Task<LocationDto> LocationAsync(TestSpa spa, string name, string? region, LocationContact? contact = null, bool withHours = true,
        string timeZone = "Europe/Vienna", string currency = "EUR")
    {
        IReadOnlyList<OpeningHours> hours = withHours
            ? [.. Enum.GetValues<DayOfWeek>().Where(d => d != DayOfWeek.Sunday).Select(d => new OpeningHours(d, new(9, 0), new(20, 0)))]
            : [];
        return await spa.PostAsync<LocationDto>("/api/locations", new LocationInput(name, timeZone, currency, hours, region, contact), Ct);
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gesundheitshinweise auf der Behandlungskarte (ADR 0013, Nachtrag 19.11.2026): sehen darf sie, wer den Gast behandelt, die Rezeption und die
/// Standortleitung, nicht Inhaber und Manager; jeder Abruf von Gesundheitsdaten steht im Protokoll, das nur die Leitung liest.
/// </summary>
public sealed class HealthHintTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task SubmitAnamnesisAsync(TestSpa spa, Guid customerId)
    {
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{customerId}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = spa.Fixture.CreateClient();
        var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
        List<AnamnesisAnswer> answers =
        [
            new("allergies", true, "Nüsse"), new("skin", false, "steht nicht auf der Karte"), new("circulation", false, null),
            new("pregnancy", true, null), new("medication", false, null), new("surgery", false, null), new("other", null, "Bitte wenig Druck am Rücken"),
        ];
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Eva Muster", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
    }

    private static Task<HttpResponseMessage> HintsAsync(HttpClient client, Guid appointmentId) =>
        client.GetAsync($"/api/reports/appointments/{appointmentId}/health", Ct);

    [Fact]
    public async Task Behandelnde_Person_Rezeption_und_Standortleitung_sehen_die_Hinweise_jeder_Abruf_steht_im_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await SubmitAnamnesisAsync(spa, spa.Customer.Id);
        Guid[] here = [spa.Location.Id];
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, here, spa.Anna.Id);
        using var ben = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, here, spa.Ben.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, here);
        using var lead = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, here);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.GeneralManager, here);

        // Der eigene Tagesplan sagt nur, dass es Hinweise gibt.
        var plan = await anna.GetFromJsonAsync<DayPlanDto>($"/api/reports/locations/{spa.Location.Id}/day-plan?date={TestSpa.Monday:yyyy-MM-dd}", TestSpa.Json, Ct);
        Assert.True(Assert.Single(plan!.Cards).HasHealthHints);

        var hints = await TestSpa.ReadAsync<TreatmentHealthHints>(await HintsAsync(anna, appointment.Id), Ct);
        Assert.Equal(["Allergien: Nüsse", "Schwanger/stillend", "Sonstiges: Bitte wenig Druck am Rücken"],
            hints.Hints.Select(h => h.Details is null ? h.Label : $"{h.Label}: {h.Details}"));
        Assert.NotNull(hints.SubmittedAt);
        Assert.Contains("protokolliert", hints.Notice, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await HintsAsync(reception, appointment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await HintsAsync(lead, appointment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await HintsAsync(ben, appointment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await HintsAsync(manager, appointment.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await HintsAsync(spa.Client, appointment.Id)).StatusCode);

        // Den Bogen im Termin liest die Inhaberin weiter; auch das steht im Protokoll, mit dem Termin.
        Assert.Equal(HttpStatusCode.OK,
            (await spa.Client.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis?appointmentId={appointment.Id}", Ct)).StatusCode);

        var accesses = await spa.GetAsync<List<HealthAccessEntry>>($"/api/customers/{spa.Customer.Id}/anamnesis/accesses", Ct);
        Assert.Equal(
            [HealthAccessPurpose.Anamnesis, HealthAccessPurpose.TreatmentCard, HealthAccessPurpose.TreatmentCard, HealthAccessPurpose.TreatmentCard],
            accesses.Select(a => a.Purpose));
        Assert.All(accesses, a => Assert.Equal((spa.MassageTreatment.Name, (DateTimeOffset?)appointment.Start), (a.Treatment, a.AppointmentStart)));
        Assert.All(accesses, a => Assert.False(string.IsNullOrWhiteSpace(a.Person)));

        // Das Protokoll liest nur die Leitung.
        Assert.Equal(HttpStatusCode.OK, (await lead.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis/accesses", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis/accesses", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await anna.GetAsync($"/api/customers/{spa.Customer.Id}/anamnesis/accesses", Ct)).StatusCode);
    }

    [Fact]
    public async Task Ohne_Bogen_keine_Hinweise_und_kein_Protokolleintrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var plan = await anna.GetFromJsonAsync<DayPlanDto>($"/api/reports/locations/{spa.Location.Id}/day-plan?date={TestSpa.Monday:yyyy-MM-dd}", TestSpa.Json, Ct);
        Assert.False(Assert.Single(plan!.Cards).HasHealthHints);
        var hints = await TestSpa.ReadAsync<TreatmentHealthHints>(await HintsAsync(anna, appointment.Id), Ct);
        Assert.Equal((null, 0), (hints.SubmittedAt, hints.Hints.Count));
        Assert.Empty(await spa.GetAsync<List<HealthAccessEntry>>($"/api/customers/{spa.Customer.Id}/anamnesis/accesses", Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await HintsAsync(anna, Guid.NewGuid())).StatusCode);
    }
}

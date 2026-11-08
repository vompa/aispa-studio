using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>„Anamnese prüfen“ beim Buchen: nur ja oder nein, nie der Inhalt des Bogens.</summary>
public sealed class AnamnesisCheckTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static List<AnamnesisAnswer> Answers(bool skin = false, string? other = null) =>
    [
        new("allergies", false, null),
        new("skin", skin, skin ? "Sonnenbrand am Rücken" : null),
        new("circulation", false, null),
        new("pregnancy", false, null),
        new("medication", false, null),
        new("surgery", false, null),
        new("other", null, other),
    ];

    private static async Task SubmitAsync(TestSpa spa, Guid customerId, List<AnamnesisAnswer> answers)
    {
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{customerId}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = spa.Fixture.CreateClient();
        var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Eva Muster", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
    }

    private static Task<AnamnesisCheck> CheckAsync(TestSpa spa, Guid customerId, Guid treatmentId) =>
        spa.GetAsync<AnamnesisCheck>($"/api/customers/{customerId}/anamnesis/check?treatmentId={treatmentId}", Ct);

    [Fact]
    public async Task Hinweis_nur_bei_markierter_Behandlung_und_Angaben_im_Bogen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var hotStone = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Hot Stone", 60, 0, 0, 99m, [spa.Massage.Id], [spa.RoomA.Id], [], CheckAnamnesis: true), Ct);
        var other = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", "Ohne", null, null, null, null), Ct);
        var third = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", "Text", null, null, null, null), Ct);

        var withoutRecord = await CheckAsync(spa, spa.Customer.Id, hotStone.Id);
        await SubmitAsync(spa, spa.Customer.Id, Answers(skin: true));
        await SubmitAsync(spa, other.Id, Answers());
        await SubmitAsync(spa, third.Id, Answers(other: "Bitte wenig Druck im Nacken"));

        Assert.True(hotStone.CheckAnamnesis);
        Assert.False(withoutRecord.Check);
        Assert.True((await CheckAsync(spa, spa.Customer.Id, hotStone.Id)).Check);
        Assert.False((await CheckAsync(spa, spa.Customer.Id, spa.MassageTreatment.Id)).Check);
        Assert.False((await CheckAsync(spa, other.Id, hotStone.Id)).Check);
        Assert.True((await CheckAsync(spa, third.Id, hotStone.Id)).Check);
        var unknown = await spa.Client.GetAsync($"/api/customers/{Guid.NewGuid()}/anamnesis/check?treatmentId={hotStone.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Neuer_Bogen_ohne_Angaben_nimmt_den_Hinweis_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var peeling = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Peeling", 30, 0, 0, 49m, [spa.Massage.Id], [spa.RoomA.Id], [], CheckAnamnesis: true), Ct);
        await SubmitAsync(spa, spa.Customer.Id, Answers(skin: true));
        await SubmitAsync(spa, spa.Customer.Id, Answers());

        Assert.False((await CheckAsync(spa, spa.Customer.Id, peeling.Id)).Check);
    }
}

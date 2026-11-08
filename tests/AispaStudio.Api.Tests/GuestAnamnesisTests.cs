using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Stand des Anamnesebogens im Assistenten (Ä317): ob und wann ein Bogen da ist, sieht, wer den Kalender sieht; Hinweise daraus (Allergie, Schwangerschaft …) nur, wer
/// Gesundheitsdaten sehen darf, und nie mit den Freitexten des Bogens.
/// </summary>
public sealed class GuestAnamnesisTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> SteinerAsync(TestSpa spa) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Christian", "Steiner", null, null, null, null, Salutation: CustomerSalutation.Male), Ct);

    /// <summary>Der Gast füllt den Bogen am Tablet aus: Allergie gegen Lavendelöl, sonst alles mit Nein.</summary>
    private async Task SubmitWithAllergyAsync(TestSpa spa, CustomerDto guest)
    {
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{guest.Id}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = fixture.CreateClient();
        var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
        List<AnamnesisAnswer> answers =
        [
            new("allergies", true, "Lavendelöl"), new("skin", false, null), new("circulation", false, null), new("pregnancy", false, null),
            new("medication", false, null), new("surgery", false, null), new("other", null, null),
        ];
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Christian Steiner", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
    }

    [Fact]
    public async Task Ohne_Fragebogen_sagt_die_Antwort_kein_Fragebogen_hinterlegt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SteinerAsync(spa);

        var block = await _kit.AnswerAsync(spa, "hat herr steiner die anamnese ausgefüllt", role: StaffRole.Reception);

        Assert.Equal("Christian Steiner: Kein Fragebogen hinterlegt.", block.Text);
    }

    [Theory]
    [InlineData(StaffRole.Reception, AssistantBlockKind.Answer, true)]
    [InlineData(StaffRole.Backoffice, AssistantBlockKind.Denied, false)]
    public async Task Allergie_sieht_nur_wer_Gesundheitsdaten_sehen_darf(StaffRole role, AssistantBlockKind kind, bool allergy)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await SteinerAsync(spa);
        await SubmitWithAllergyAsync(spa, steiner);

        var block = await _kit.AnswerAsync(spa, "hat herr steiner die anamnese ausgefüllt", role: role);

        Assert.Equal((kind, allergy), (block.Kind, block.Text.Contains("Allergie", StringComparison.Ordinal)));
        Assert.DoesNotContain("Lavendel", block.Text, StringComparison.Ordinal);
    }
}

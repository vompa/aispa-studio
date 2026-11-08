using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Stornofrist und Ausfallgebühr für den Hinweis im Storno-Dialog der Rezeption (L3): Das Kartenangebot und die Hinterlegung eines Termins
/// nennen, bis wann der Gast ohne Gebühr absagt (Beginn minus Frist des Standorts); ohne Frist bleibt der Wert leer.
/// </summary>
public sealed class CancellationTermsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<LocationOnlineRules> SetRulesAsync(TestSpa spa, int deadlineHours, int feePercent) =>
        spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules",
            new LocationOnlineRules(true, 60, 365, deadlineHours, 15, null, false, feePercent), Ct);

    [Fact]
    public async Task Angebot_und_Hinterlegung_nennen_Frist_und_Gebuehr_laut_Regel_des_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SetRulesAsync(spa, 24, 50);
        var appointment = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);

        var offer = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{appointment.Id}/card-guarantee/offer", Ct);
        var requested = await spa.PostAsync<CardGuaranteeStaffDto>($"/api/appointments/{appointment.Id}/card-guarantee/request", new { }, Ct);
        var staff = await spa.GetAsync<CardGuaranteeStaffDto>($"/api/appointments/{appointment.Id}/card-guarantee", Ct);

        Assert.Equal((TestSpa.At(14).AddHours(-24), 50), (offer.CancellationDeadlineAt, offer.FeePercent));
        Assert.Equal(TestSpa.At(14).AddHours(-24), requested.CancellationDeadlineAt);
        Assert.Equal((TestSpa.At(14).AddHours(-24), 50), (staff.CancellationDeadlineAt, staff.FeePercent));
    }

    [Fact]
    public async Task Ohne_Frist_und_ohne_Gebuehr_bleibt_das_Angebot_leer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SetRulesAsync(spa, 0, 0);
        var appointment = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);

        var offer = await spa.GetAsync<CardRequestOffer>($"/api/appointments/{appointment.Id}/card-guarantee/offer", Ct);

        Assert.Null(offer.CancellationDeadlineAt);
        Assert.Equal(0, offer.FeePercent);
        Assert.False(offer.Possible);
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>„Mein Tag“ (Masken-Überarbeitung P9): Name ohne doppelte Dauer und das Gerät als abhakbarer Schritt der Vorbereitung.</summary>
public sealed class MyDayCardTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Lava-Hot-Stone-Massage (60 Min.)", "Lava-Hot-Stone-Massage")]
    [InlineData("Lava-Hot-Stone-Massage (90 Min.)", "Lava-Hot-Stone-Massage")]
    [InlineData("Klassische Massage", "Klassische Massage")]
    [InlineData("Massage (Rücken) (45 Min.) ", "Massage (Rücken)")]
    public void Der_Anzeigename_der_Karte_traegt_die_Dauer_nicht_doppelt(string name, string expected) =>
        Assert.Equal(expected, AispaStudio.Api.Reporting.TreatmentCards.DisplayNameOf(name));

    [Fact]
    public async Task Das_Geraet_ist_ein_abhakbarer_Schritt_der_Vorbereitung_und_der_Haken_bleibt_gespeichert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var heater = await spa.PostAsync<EquipmentDto>($"/api/locations/{spa.Location.Id}/equipment", new EquipmentInput("Steinwärmer"), Ct);
        var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Hot Stone (60 Min.)", 60, 0, 0, 99m, [spa.Massage.Id], [spa.RoomA.Id], [heater.Id]), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, treatment: treatment), Ct);

        var before = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{appointment.Id}/card", Ct);
        var check = await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{appointment.Id}/equipment-prepared", new PreparationCheck(true), TestSpa.Json, Ct);
        var after = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{appointment.Id}/card", Ct);
        var undo = await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{appointment.Id}/equipment-prepared", new PreparationCheck(false), TestSpa.Json, Ct);
        var undone = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{appointment.Id}/card", Ct);

        Assert.Equal("Hot Stone", before.DisplayName);
        var item = Assert.Single(before.Todo, t => t.IsEquipment == true);
        Assert.Equal(("Steinwärmer bereitstellen", false), (item.Text, item.Done));
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
        Assert.True(Assert.Single(after.Todo, t => t.IsEquipment == true).Done);
        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);
        Assert.False(Assert.Single(undone.Todo, t => t.IsEquipment == true).Done);
    }

    [Fact]
    public async Task Ohne_Geraet_im_Termin_ist_der_Haken_nicht_moeglich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var response = await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{appointment.Id}/equipment-prepared", new PreparationCheck(true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Freie Zeiten werden danach bewertet, ob sie im Dienst der Person eine unbrauchbare Lücke lassen.</summary>
public sealed class GapFitTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<List<AvailableSlot>> AnnaAsync(TestSpa spa, int step = 15) =>
        await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}"
            + $"&employeeId={spa.Anna.Id}&stepMinutes={step}", Ct);

    [Fact]
    public async Task Bewertet_Luecken_nach_der_kuerzesten_Behandlung()
    {
        // Massage bindet 65 Minuten (5 Vorbereitung, 50 Behandlung, 10 Nachbereitung); Termin 10:00 bindet Anna 9:55–11:00.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var slots = await AnnaAsync(spa);
        AvailableSlot At(int hour, int minute = 0) => slots.Single(s => s.Start == TestSpa.At(hour, minute));

        Assert.Equal(SlotFit.Snug, At(11, 5).Fit);
        Assert.Equal("Schließt bei Anna direkt an den Termin davor an.", At(11, 5).FitReason);
        Assert.Equal(SlotFit.LeavesGap, At(11, 30).Fit);
        Assert.Contains("25 Minuten davor", At(11, 30).FitReason, StringComparison.Ordinal);
        Assert.Equal(SlotFit.Neutral, At(12, 15).Fit);
        Assert.Equal(SlotFit.Snug, At(16).Fit);
        Assert.Equal("Endet bei Anna mit dem Dienst.", At(16).FitReason);
    }

    [Fact]
    public async Task Schlaegt_lueckenlose_Zeiten_ausserhalb_des_Rasters_nur_passend_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, employee: spa.Anna.Id);

        var slots = await AnnaAsync(spa, step: 30);
        var starts = slots.Select(s => s.Start).ToList();

        // 11:05 schließt an den Termin um 10:00 an, 12:55 endet samt Nachbereitung genau vor dem Termin um 14:00 (gebunden ab 13:55).
        Assert.Contains(TestSpa.At(11, 5), starts);
        Assert.Equal("Endet bei Anna direkt vor dem nächsten Termin.", slots.Single(s => s.Start == TestSpa.At(12, 55)).FitReason);
        Assert.All(slots.Where(s => s.Start.Minute % 30 != 0), s => Assert.Equal(SlotFit.Snug, s.Fit));
        Assert.Equal(starts.Order(), starts);
        Assert.Equal(starts.Distinct().Count(), starts.Count);
    }

    [Fact]
    public async Task Kuerzere_Behandlung_macht_kleine_Luecken_nutzbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Fußbad", 20, 0, 0, 25m, [], [spa.RoomA.Id], []), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var slots = await AnnaAsync(spa);

        // Kürzeste Behandlung jetzt 20 Minuten: 25 Minuten Lücke sind wieder nutzbar.
        Assert.Equal(SlotFit.Neutral, slots.Single(s => s.Start == TestSpa.At(11, 30)).Fit);
        Assert.Equal(SlotFit.LeavesGap, slots.Single(s => s.Start == TestSpa.At(11, 15)).Fit);
    }
}

using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// „Kein geeigneter Raum ist zu dieser Zeit frei“ (Ä79, Slice 72): der Raumplaner meldet es nur, wenn wirklich jeder passende Raum belegt ist,
/// und nennt dann die Räume mit ihrer Belegung und die nächste freie Zeit. Nachgestellt wird eine Behandlung von 90 Minuten um 11:45
/// (Anti-Aging); die Testpersonen arbeiten nur montags, der Wochentag ist für den Planer ohne Belang.
/// </summary>
public sealed class RoomAvailabilityTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TreatmentDto> AntiAgingAsync(TestSpa spa, params Guid[] rooms) =>
        spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Anti-Aging", 90, 5, 10, 149m, [spa.Massage.Id], rooms, []), Ct);

    [Fact]
    public async Task Given_zwei_freie_Behandlungsraeume_When_gebucht_Then_meldet_der_Planer_nicht_kein_Raum_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var antiAging = await AntiAgingAsync(spa, spa.RoomA.Id, spa.RoomB.Id);

        var response = await spa.BookAsync(TestSpa.At(11, 45), Ct, employee: spa.Anna.Id, treatment: antiAging);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Raum_belegt_und_einer_frei_When_gebucht_Then_bekommt_der_Termin_den_freien()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var antiAging = await AntiAgingAsync(spa, spa.RoomA.Id, spa.RoomB.Id);
        _ = await spa.BookOkAsync(TestSpa.At(11), Ct, employee: spa.Ben.Id, room: spa.RoomA.Id);

        var booked = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(11, 45), Ct, employee: spa.Anna.Id, treatment: antiAging), Ct);

        Assert.Equal(spa.RoomB.Id, booked.RoomId);
    }

    [Fact]
    public async Task Given_der_einzige_Raum_ist_belegt_When_gebucht_Then_nennt_der_Konflikt_Raum_Belegung_und_naechste_freie_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var antiAging = await AntiAgingAsync(spa, spa.RoomA.Id);
        // Belegung des Raums: Beginn 11:00, davor 5 Min. Vorbereitung, 50 Min. Behandlung, 10 Min. Nachbereitung, 15 Min. Raumpflege: Raum bis 12:15.
        var occupying = await spa.BookOkAsync(TestSpa.At(11), Ct, employee: spa.Ben.Id, room: spa.RoomA.Id);

        var response = await spa.BookAsync(TestSpa.At(11, 45), Ct, employee: spa.Anna.Id, treatment: antiAging);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        var busy = Assert.Single(rejected.Conflicts, c => c.Kind == ResourceKind.Room && c.ResourceId == spa.RoomA.Id);
        Assert.Equal((occupying.Id, "Raum A"), (busy.AppointmentId, busy.ResourceName));
        Assert.NotNull(busy.From);
        var summary = Assert.Single(rejected.Conflicts, c => c.ResourceName == "Räume");
        Assert.Equal("Kein geeigneter Raum ist zu dieser Zeit frei", summary.Reason);
        // Der Raum ist bis 12:15 belegt, die Behandlung braucht davor 5 Minuten Vorbereitung: frühester Beginn 12:20.
        Assert.Equal(TestSpa.At(12, 20), summary.NextFree);
    }
}

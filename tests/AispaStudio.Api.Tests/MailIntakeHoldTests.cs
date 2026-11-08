using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Vormerkung aus dem Posteingang (ADR 0083): vormerken und vorschlagen, Antwort des Gastes, bestätigen, freigeben.</summary>
public sealed partial class MailIntakeTests
{
    private const string HeldText = "halten wir Ihnen diesen Termin frei";

    /// <summary>Anfrage für Montag 10:00, die nicht frei ist: die Terminannahme schlägt Zeiten vor.</summary>
    private static async Task<(MailSpa Spa, InboundMailDto Mail)> ProposedAsync(ApiFixture fixture, CancellationToken ct)
    {
        var spa = await MailSpa.CreateAsync(fixture, ct);
        await spa.BookAsync(TestSpa.At(10), ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Massage",
            $"Guten Tag, gibt es am 1.3.2027 um 10:00 einen Termin für {spa.Treatment.Name} im {spa.Location.Name}? Danke\nLukas Berg", ct);
        Assert.Equal(InboundMailStatus.Proposed, mail.Status);
        return (spa, mail);
    }

    private static async Task<InboundMailDto> HoldAsync(MailSpa spa, InboundMailDto mail, AvailableSlot slot, CancellationToken ct)
    {
        var response = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/hold",
            new HoldTimeRequest(spa.Location.Id, spa.Treatment.Id, slot.Start, slot.EmployeeId, null, null), TestSpa.Json, ct);
        return await TestSpa.ReadAsync<InboundMailDto>(response, ct);
    }

    [Fact]
    public async Task Vormerken_belegt_die_Zeit_schlaegt_sie_vor_und_Bestaetigen_bucht_fest()
    {
        var (spa, mail) = await ProposedAsync(fixture, Ct);
        var slot = mail.Proposals[0];

        var held = await HoldAsync(spa, mail, slot, Ct);
        Assert.Equal(InboundMailStatus.Proposed, held.Status);
        Assert.Equal(slot.Start, held.HeldStart);
        Assert.Empty(held.Proposals);
        var appointmentId = held.AppointmentId!.Value;

        var appointment = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{appointmentId}", Ct);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.NotNull(appointment.HeldSince);
        Assert.Equal(mail.Id, appointment.HoldMailId);
        Assert.Null(appointment.HoldAnsweredAt);
        // Termin in mehr als zwei Tagen: Erinnerung zum Tagesstart des Vortags; Sonntag ist geschlossen, dann 8:00.
        Assert.Equal(TestSpa.At(8, date: TestSpa.Monday.AddDays(-1)), appointment.HoldRemindAt);

        // Die Zeit ist belegt: dieselbe Person zur selben Zeit lässt sich nicht noch einmal buchen.
        var again = await spa.Spa.BookAsync(slot.Start, Ct, employee: slot.EmployeeId, treatment: spa.Treatment, location: spa.Location.Id);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Der Entwurf schlägt dem Gast die Zeit vor; gesendet wird erst nach Prüfung.
        var drafted = await spa.WaitAsync(mail.Id, m => m.ReplyBody?.Contains(HeldText, StringComparison.Ordinal) == true, Ct);
        Assert.Equal(ReplyStatus.Draft, drafted.ReplyStatus);
        await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);

        // Der Gast antwortet: keine neuen Zeiten, die Nachricht zeigt die Vormerkung, die Erinnerung entfällt.
        var answer = await spa.ReceiveAsync(spa.Guest, "Re: Massage", "Ja, das passt mir gut.\n\nLiebe Grüße\nLukas Berg", Ct, received: DateTimeOffset.UtcNow);
        Assert.Equal(mail.Id, answer.PreviousMailId);
        Assert.Equal(InboundMailStatus.NeedsReview, answer.Status);
        Assert.Equal(appointmentId, answer.AppointmentId);
        Assert.Equal(slot.Start, answer.HeldStart);
        Assert.Empty(answer.Proposals);
        Assert.NotNull((await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{appointmentId}", Ct)).HoldAnsweredAt);

        // Bestätigen: fest gebucht, die Antwort an den Gast wird als Bestätigung neu entworfen.
        var confirmed = await TestSpa.ReadAsync<AppointmentDto>(await spa.Spa.Client.PostAsync($"/api/appointments/{appointmentId}/hold/confirm", null, Ct), Ct);
        Assert.Null(confirmed.HeldSince);
        Assert.Null(confirmed.HoldRemindAt);
        Assert.Equal(AppointmentStatus.Booked, confirmed.Status);
        var booked = await spa.WaitAsync(answer.Id, m => m.ReplyBody?.Contains("eingetragen", StringComparison.Ordinal) == true, Ct);
        Assert.Equal(InboundMailStatus.Booked, booked.Status);
        Assert.Null(booked.HeldStart);

        // Ein zweites Bestätigen geht nicht: der Termin ist nicht mehr vorgemerkt.
        var twice = await spa.Spa.Client.PostAsync($"/api/appointments/{appointmentId}/hold/confirm", null, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
    }

    [Fact]
    public async Task Freigeben_storniert_still_und_die_Anfrage_steht_wieder_auf_Pruefen()
    {
        var (spa, mail) = await ProposedAsync(fixture, Ct);
        var held = await HoldAsync(spa, mail, mail.Proposals[0], Ct);
        var appointmentId = held.AppointmentId!.Value;

        var released = await TestSpa.ReadAsync<AppointmentDto>(await spa.Spa.Client.PostAsync($"/api/appointments/{appointmentId}/hold/release", null, Ct), Ct);
        Assert.Equal(AppointmentStatus.Cancelled, released.Status);
        Assert.Null(released.HeldSince);

        var open = await spa.Spa.GetAsync<InboundMailDto>($"/api/inbox/{mail.Id}", Ct);
        Assert.Equal(InboundMailStatus.NeedsReview, open.Status);
        Assert.Null(open.AppointmentId);
        Assert.Null(open.HeldStart);
        Assert.Contains("freigegeben", open.Note, StringComparison.Ordinal);

        // Die Zeit ist wieder frei.
        var again = await spa.Spa.BookAsync(mail.Proposals[0].Start, Ct, employee: mail.Proposals[0].EmployeeId, treatment: spa.Treatment, location: spa.Location.Id);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);

        // Ein gewöhnlicher Termin lässt sich nicht „freigeben“.
        var refused = await spa.Spa.Client.PostAsync($"/api/appointments/{appointmentId}/hold/release", null, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Vormerken_geht_nur_einmal_je_Anfrage()
    {
        var (spa, mail) = await ProposedAsync(fixture, Ct);
        await HoldAsync(spa, mail, mail.Proposals[0], Ct);

        var second = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/hold",
            new HoldTimeRequest(spa.Location.Id, spa.Treatment.Id, mail.Proposals[1].Start, mail.Proposals[1].EmployeeId, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}

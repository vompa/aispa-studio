using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Posteingang: Übersetzung für die Rezeption und KI-Überarbeitung des Entwurfs, beides über die Queue (KI-Attrappen).</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task Fremdsprachige_Nachricht_und_Entwurf_werden_ins_Deutsche_uebersetzt_deutsche_nicht()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Booking",
            $"Hello, I would like to book a {spa.Treatment.Name} at {spa.Location.Name} on Monday between 13 and 16 h.\n\nBest regards\nJane Doe", Ct);
        var translated = await spa.WaitAsync(mail.Id, m => m.Translation == MessageTranslationState.Done && m.ReplyTranslation is not null, Ct);
        Assert.Equal("[DE] Booking", translated.TranslatedSubject);
        Assert.StartsWith("[DE] Hello, I would like", translated.TranslatedBody, StringComparison.Ordinal);
        Assert.StartsWith("Hello, I would like", translated.Body, StringComparison.Ordinal);
        Assert.StartsWith("[DE] Hello Jane Doe,", translated.ReplyTranslation, StringComparison.Ordinal);

        // Weicht eine Zahl ab, gilt die Übersetzung als unsicher.
        var wrong = await spa.ReceiveAsync($"x{spa.Guest}", "Booking",
            $"Hello, a {spa.Treatment.Name} at {spa.Location.Name} on Monday at 14 h please. FALSCHE ZAHL\nJohn Doe", Ct);
        await spa.WaitAsync(wrong.Id, m => m.Translation == MessageTranslationState.Mismatch, Ct);

        var german = await spa.ReceiveAsync($"y{spa.Guest}", "Termin",
            $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.\nEva Sommer", Ct);
        Assert.Equal(MessageTranslationState.None, german.Translation);
        Assert.Null(german.TranslatedBody);
    }

    [Fact]
    public async Task Entwurf_wird_ueberarbeitet_erfundene_Zahlen_verworfen_und_zuruecknehmen_geht()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Termin",
            $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.\nEva Sommer", Ct);
        Assert.True(mail.CanRevise);
        Assert.False(mail.CanUndoReply);
        var original = mail.ReplyBody!;

        var revise = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/reply/revise",
            new ReplyRevisionInput(ReplyRevisionKind.Friendlier), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Accepted, revise.StatusCode);
        var friendly = await spa.WaitAsync(mail.Id, m => !m.ReplyRevising, Ct);
        Assert.Equal($"Herzlich willkommen! {original}", friendly.ReplyBody);
        Assert.True(friendly.CanUndoReply);
        Assert.Null(friendly.ReplyNotice);

        // Eine neue Zahl (hier eine Parkplatznummer) kommt nicht aus dem Vorschlag: die Überarbeitung wird verworfen.
        await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/reply/revise",
            new ReplyRevisionInput(ReplyRevisionKind.Custom, "bitte Parkplatz mit Zahl nennen"), TestSpa.Json, Ct);
        var rejected = await spa.WaitAsync(mail.Id, m => !m.ReplyRevising && m.ReplyNotice is not null, Ct);
        Assert.Equal(friendly.ReplyBody, rejected.ReplyBody);
        Assert.Contains("verworfen", rejected.ReplyNotice, StringComparison.Ordinal);

        var missing = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/reply/revise",
            new ReplyRevisionInput(ReplyRevisionKind.Custom, " "), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var undone = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/reply/undo", new { }, Ct);
        Assert.Equal(original, undone.ReplyBody);
        Assert.False(undone.CanUndoReply);
        var nothing = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/reply/undo", null, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
    }
}

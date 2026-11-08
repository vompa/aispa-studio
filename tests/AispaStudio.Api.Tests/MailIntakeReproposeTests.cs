using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Posteingang: „Neu vorschlagen“ der Rezeption und Antworten des Gasts auf einen Vorschlag.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task Neu_vorschlagen_laesst_gezeigte_Zeiten_aus_und_nimmt_den_Hinweis_auf()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await spa.BookAsync(TestSpa.At(10), Ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Massage",
            $"Guten Tag, gibt es am 1.3.2027 um 10:00 einen Termin für {spa.Treatment.Name} im {spa.Location.Name}? Danke\nLukas Berg", Ct);
        Assert.Equal(InboundMailStatus.Proposed, mail.Status);
        Assert.Equal(1, mail.ProposalRound);
        Assert.True(mail.CanRepropose);
        var first = mail.Proposals.Select(p => p.Start).ToList();

        var response = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/repropose", new ReproposeInput("lieber am Nachmittag"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var again = await spa.WaitAsync(mail.Id, m => !m.Reproposing, Ct);

        Assert.Equal(InboundMailStatus.Proposed, again.Status);
        Assert.Equal(2, again.ProposalRound);
        Assert.Equal("lieber am Nachmittag", again.WishNote);
        Assert.Equal("de", again.Language);
        Assert.Equal(first.Order(), again.RejectedStarts!.Order());
        Assert.NotEmpty(again.Proposals);
        Assert.All(again.Proposals, p => Assert.InRange(p.Start, TestSpa.At(12), TestSpa.At(18)));
        Assert.All(again.Proposals, p => Assert.DoesNotContain(first, r => Math.Abs((r - p.Start).TotalMinutes) < 15));
        Assert.Contains("freie Zeiten", again.ReplyBody, StringComparison.Ordinal);

        // Ohne Hinweis: noch einmal andere Zeiten, alle bisherigen bleiben ausgelassen.
        await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/repropose", new ReproposeInput(), TestSpa.Json, Ct);
        var third = await spa.WaitAsync(mail.Id, m => !m.Reproposing && m.ProposalRound == 3, Ct);
        Assert.Equal(first.Count + again.Proposals.Count, third.RejectedStarts!.Count);
        Assert.All(third.Proposals, p => Assert.DoesNotContain(third.RejectedStarts!, r => Math.Abs((r - p.Start).TotalMinutes) < 15));

        var tooLong = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{mail.Id}/repropose", new ReproposeInput(new string('x', 301)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        // Eine Frage ist keine Terminanfrage.
        var question = await spa.ReceiveAsync($"q{spa.Guest}", "Frage", "Habt ihr Parkplätze?", Ct);
        Assert.False(question.CanRepropose);
        var refused = await spa.Spa.Client.PostAsJsonAsync($"/api/inbox/{question.Id}/repropose", new ReproposeInput(), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Antwort_des_Gasts_auf_den_Vorschlag_wird_verknuepft_und_schlaegt_neu_vor()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await spa.BookAsync(TestSpa.At(10), Ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Massage",
            $"Guten Tag, gibt es am 1.3.2027 um 10:00 einen Termin für {spa.Treatment.Name} im {spa.Location.Name}? Danke\nLukas Berg", Ct);
        await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);

        // Eine Nachricht eines anderen Absenders gehört nicht dazu.
        var other = await spa.ReceiveAsync($"o{spa.Guest}", "Re: Massage", "Das passt leider nicht, lieber am Nachmittag.", Ct,
            received: DateTimeOffset.UtcNow);
        Assert.Null(other.PreviousMailId);

        var answer = await spa.ReceiveAsync(spa.Guest.ToUpperInvariant(), "Re: Massage",
            "Das passt leider nicht, lieber am Nachmittag.\n\nLiebe Grüße\nLukas Berg", Ct, received: DateTimeOffset.UtcNow);

        Assert.Equal(mail.Id, answer.PreviousMailId);
        Assert.True(answer.Status == InboundMailStatus.Proposed, $"{answer.Status}: {answer.Note} {answer.Summary}");
        Assert.Equal(spa.Treatment.Id, answer.TreatmentId);
        Assert.Equal(2, answer.ProposalRound);
        Assert.Equal(mail.Proposals.Select(p => p.Start).Order(), answer.RejectedStarts!.Order());
        Assert.All(answer.Proposals, p => Assert.InRange(p.Start, TestSpa.At(12), TestSpa.At(18)));
        Assert.All(answer.Proposals, p => Assert.DoesNotContain(mail.Proposals, r => Math.Abs((r.Start - p.Start).TotalMinutes) < 15));

        var closed = await spa.Spa.GetAsync<InboundMailDto>($"/api/inbox/{mail.Id}", Ct);
        Assert.Equal(InboundMailStatus.Ignored, closed.Status);
        Assert.Contains("geantwortet", closed.Note, StringComparison.Ordinal);
    }
}

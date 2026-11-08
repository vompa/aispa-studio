using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.MailIntake;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Eigenes Postfach je Spa und das Spa über die angeschriebene Nummer.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task Eigenes_Postfach_landet_beim_eigenen_Spa_und_das_Passwort_bleibt_verborgen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var other = await MailSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        var input = new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993, MailboxSecurity.SslOnConnect,
            address, null, null, null, null, null);

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Spa.Client.GetAsync("/api/intake/mailbox", Ct)).StatusCode);
        var missing = await spa.Spa.Client.PutAsJsonAsync("/api/intake/mailbox", input, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var saved = await spa.Spa.Client.PutAsJsonAsync("/api/intake/mailbox", input with { Secret = "geheim-123" }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.DoesNotContain("geheim-123", await saved.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        var dto = await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct);
        Assert.True(dto.HasSecret);
        Assert.Equal("INBOX", dto.Folder);

        // Ändern ohne Passwort behält das gespeicherte.
        await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", input with { Folder = "Termine" }, Ct);

        var messageId = $"<{Guid.NewGuid():N}@gast.example>";
        fixture.SpaMailboxes.Inbox(address).Deliver(new IncomingMail(messageId, spa.Guest, "Eva Sommer", address, "Termin",
            $"Bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.", Received, true));
        await Poller().PollSpaMailboxesAsync(Ct);

        var mail = await spa.WaitForSenderAsync(spa.Guest, Ct);
        Assert.DoesNotContain(await other.Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", Ct), m => m.Id == mail.Id);
        var settings = fixture.SpaMailboxes.SettingsOf(address)!;
        Assert.Equal("geheim-123", settings.Secret);
        Assert.Equal("Termine", settings.Folder);
        Assert.Null((await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastError);

        // Abruf schlägt fehl: das Spa sieht den Grund.
        fixture.SpaMailboxes.Failing[address] = "Anmeldung abgelehnt.";
        await Poller().PollSpaMailboxesAsync(Ct);
        Assert.Equal("Anmeldung abgelehnt.", (await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastError);
        fixture.SpaMailboxes.Failing.TryRemove(address, out _);

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Spa.Client.DeleteAsync("/api/intake/mailbox", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Spa.Client.GetAsync("/api/intake/mailbox", Ct)).StatusCode);
    }

    [Fact]
    public async Task SMS_ohne_Spa_in_der_Adresse_findet_das_Spa_ueber_die_angeschriebene_Nummer()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var other = await MailSpa.CreateAsync(fixture, Ct);
        var spaNumber = NewPhone();
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var created = await operatorClient.PostAsJsonAsync("/api/operator/routes",
            new IntakeRouteInput(spa.Spa.TenantId, BookingChannel.Sms, spaNumber.Replace("+43", "0043 ", StringComparison.Ordinal), null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Dieselbe Nummer für ein anderes Spa geht nicht; ohne Schlüssel gar nichts.
        var taken = await operatorClient.PostAsJsonAsync("/api/operator/routes",
            new IntakeRouteInput(other.Spa.TenantId, BookingChannel.Sms, spaNumber, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await anonymous.GetAsync("/api/operator/routes", Ct)).StatusCode);

        Assert.Equal(spaNumber, Assert.Single(await spa.Spa.GetAsync<List<IntakeRouteDto>>("/api/intake/routes", Ct)).Address);
        Assert.Empty(await other.Spa.GetAsync<List<IntakeRouteDto>>("/api/intake/routes", Ct));

        var phone = NewPhone();
        var form = new Dictionary<string, string>
        {
            ["MessageSid"] = $"SM{Guid.NewGuid():N}",
            ["From"] = phone,
            ["To"] = spaNumber,
            ["Body"] = $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}. Eva Sommer",
        };
        using var response = await PostTwilioAsync(anonymous, form, Sign(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.DoesNotContain(await other.Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", Ct), m => m.From == phone);

        // Die Antwort kommt von der Nummer, an die der Gast geschrieben hat.
        Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct)).StatusCode);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
        Assert.Equal(spaNumber, Assert.Single(fixture.Texts.To(phone)).From);

        var route = Assert.Single(await operatorClient.GetFromJsonAsync<List<OperatorIntakeRouteDto>>("/api/operator/routes", TestSpa.Json, Ct)
            ?? [], r => r.Address == spaNumber);
        Assert.Equal(spa.Spa.TenantId, route.TenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await operatorClient.DeleteAsync($"/api/operator/routes/{route.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Antworten_gehen_aus_dem_eigenen_Postfach_und_Fehler_beim_Versand_sieht_das_Spa()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        var input = new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993, MailboxSecurity.SslOnConnect,
            address, null, null, null, null, "geheim-123", SendReplies: true);

        var noServer = await spa.Spa.Client.PutAsJsonAsync("/api/intake/mailbox", input, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noServer.StatusCode);
        Assert.Contains("smtpHost", await noServer.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
        var dto = await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", input with { SmtpHost = "smtp.spa.example" }, Ct);
        Assert.True(dto.SendReplies);
        Assert.Equal(587, dto.SmtpPort);

        var (mail, messageId) = await ReceiveThroughAsync(spa, address);
        Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct)).StatusCode);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);

        var (settings, sent) = Assert.Single(fixture.SpaMailboxes.Sent, s => s.Mail.To == spa.Guest);
        Assert.Equal("smtp.spa.example", settings.SmtpHost);
        Assert.Equal(MailboxSecurity.StartTls, settings.SmtpSecurity);
        Assert.Equal("geheim-123", settings.Secret);
        Assert.Equal(messageId, sent.InReplyTo);
        Assert.False(string.IsNullOrWhiteSpace(sent.FromName));
        Assert.Contains("KI", sent.Text, StringComparison.Ordinal);
        Assert.Empty(fixture.Mailbox.To(spa.Guest));

        // Der Server lehnt ab: das Spa sieht den Grund beim Postfach.
        fixture.SpaMailboxes.FailingSend[address] = "Anmeldung am SMTP-Server abgelehnt.";
        try
        {
            var (second, _) = await ReceiveThroughAsync(spa, address);
            Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync($"/api/inbox/{second.Id}/send", null, Ct)).StatusCode);
            await WaitUntilAsync(async () =>
                (await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastSendError == "Anmeldung am SMTP-Server abgelehnt.");
        }
        finally
        {
            fixture.SpaMailboxes.FailingSend.TryRemove(address, out _);
        }
    }

    [Fact]
    public async Task Fehlt_nur_die_Kopie_in_Gesendet_gilt_die_Antwort_als_gesendet_und_das_Spa_sieht_den_Hinweis()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        var input = new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993, MailboxSecurity.SslOnConnect,
            address, null, null, null, null, "geheim-123", SendReplies: true, SmtpHost: "smtp.spa.example");
        var dto = await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", input, Ct);
        Assert.True(dto.SaveSentCopy);
        Assert.Null(dto.SentFolder);

        dto = await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", input with { SaveSentCopy = false, SentFolder = " INBOX.Gesendet " }, Ct);
        Assert.False(dto.SaveSentCopy);
        Assert.Equal("INBOX.Gesendet", dto.SentFolder);
        await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", input, Ct);

        fixture.SpaMailboxes.FailingCopy[address] = "Ordner nicht gefunden.";
        try
        {
            var (mail, _) = await ReceiveThroughAsync(spa, address);
            Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct)).StatusCode);
            await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
            await WaitUntilAsync(async () => (await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastSendError
                is { } error && error.StartsWith("Gesendet, aber keine Kopie", StringComparison.Ordinal));
            Assert.Single(fixture.SpaMailboxes.Sent, s => s.Mail.To == spa.Guest);
        }
        finally
        {
            fixture.SpaMailboxes.FailingCopy.TryRemove(address, out _);
        }
    }

    [Fact]
    public async Task Ohne_Versand_aus_dem_Postfach_antwortet_der_Gast_trotzdem_an_das_Postfach_des_Spas()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993,
            MailboxSecurity.SslOnConnect, address, null, null, null, null, "geheim-123"), Ct);

        var (mail, _) = await ReceiveThroughAsync(spa, address);
        Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct)).StatusCode);

        var sent = await fixture.Mailbox.WaitForAsync(spa.Guest, _ => true, Ct);
        Assert.Equal(address, sent.ReplyTo);
        Assert.False(string.IsNullOrWhiteSpace(sent.FromName));
        Assert.DoesNotContain(fixture.SpaMailboxes.Sent, s => s.Mail.To == spa.Guest);
    }

    [Fact]
    public async Task Jetzt_pruefen_ruft_das_Postfach_ab_und_prueft_die_Anmeldung_zum_Versand()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Spa.Client.PostAsync("/api/intake/mailbox/check", null, Ct)).StatusCode);

        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993,
            MailboxSecurity.SslOnConnect, address, null, null, null, null, "geheim-123", SendReplies: true, SmtpHost: "smtp.spa.example"), Ct);
        fixture.SpaMailboxes.Inbox(address).Deliver(new IncomingMail($"<{Guid.NewGuid():N}@gast.example>", spa.Guest, "Eva Sommer", address,
            "Termin", $"Bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.", Received, true));
        fixture.SpaMailboxes.FailingSend[address] = "SMTP-Anmeldung abgelehnt.";
        try
        {
            Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync("/api/intake/mailbox/check", null, Ct)).StatusCode);
            await WaitUntilAsync(async () =>
                (await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastSendError == "SMTP-Anmeldung abgelehnt.");
            await spa.WaitForSenderAsync(spa.Guest, Ct);
        }
        finally
        {
            fixture.SpaMailboxes.FailingSend.TryRemove(address, out _);
        }

        Assert.Equal(HttpStatusCode.Accepted, (await spa.Spa.Client.PostAsync("/api/intake/mailbox/check", null, Ct)).StatusCode);
        await WaitUntilAsync(async () => (await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastSendError is null);
        Assert.Null((await spa.Spa.GetAsync<SpaMailboxDto>("/api/intake/mailbox", Ct)).LastError);
    }

    [Fact]
    public async Task Betreiber_sieht_alle_Spas_mit_Postfach_und_Nummern()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        await spa.Spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993,
            MailboxSecurity.SslOnConnect, address, null, null, null, null, "geheim-123"), Ct);
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var number = NewPhone();
        Assert.Equal(HttpStatusCode.Created, (await operatorClient.PostAsJsonAsync("/api/operator/routes",
            new IntakeRouteInput(spa.Spa.TenantId, BookingChannel.WhatsApp, number, "1234567890"), TestSpa.Json, Ct)).StatusCode);

        var spas = await operatorClient.GetFromJsonAsync<List<OperatorSpaDto>>("/api/operator/spas", TestSpa.Json, Ct) ?? [];
        var entry = Assert.Single(spas, s => s.Id == spa.Spa.TenantId);
        Assert.Equal(address, entry.MailboxAddress);
        Assert.True(entry.MailboxEnabled);
        Assert.True(entry.Locations >= 1);
        var route = Assert.Single(entry.Routes);
        Assert.Equal((BookingChannel.WhatsApp, number, "1234567890"), (route.Channel, route.Address, route.SenderId));

        // Ein Team-Konto kommt ohne Betreiber-Schlüssel nicht an die Liste.
        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Spa.Client.GetAsync("/api/operator/spas", Ct)).StatusCode);
    }

    [Fact]
    public async Task Nur_Inhaber_richten_das_Postfach_ein()
    {
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/intake/mailbox", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/intake/routes", Ct)).StatusCode);
    }

    /// <summary>Eine Anfrage über das eigene Postfach des Spas einwerfen, abrufen und warten, bis sie verarbeitet ist.</summary>
    private async Task<(InboundMailDto Mail, string MessageId)> ReceiveThroughAsync(MailSpa spa, string address)
    {
        var messageId = $"<{Guid.NewGuid():N}@gast.example>";
        var known = (await spa.Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", Ct)).Select(m => m.Id).ToHashSet();
        fixture.SpaMailboxes.Inbox(address).Deliver(new IncomingMail(messageId, spa.Guest, "Eva Sommer", address, "Termin",
            $"Bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.", Received, true));
        await Poller().PollSpaMailboxesAsync(Ct);
        InboundMailSummary? found = null;
        await WaitUntilAsync(async () =>
        {
            found = (await spa.Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", Ct)).FirstOrDefault(m => !known.Contains(m.Id));
            return found is not null;
        });
        return (await spa.WaitAsync(found!.Id, m => m.Status != InboundMailStatus.Received && m.ReplyStatus == ReplyStatus.Draft, Ct), messageId);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> done)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!await done())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException("Bedingung nicht erfüllt.");
            }

            await Task.Delay(100, Ct);
        }
    }

    private MailboxPoller Poller() => fixture.Factory.Services.GetRequiredService<MailboxPoller>();
}

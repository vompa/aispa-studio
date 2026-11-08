using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>SMS, WhatsApp und Anrufe: über die Webhooks der Anbieter bis zur gesendeten Kurznachricht.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task SMS_ueber_Twilio_wird_gebucht_und_kurz_per_SMS_beantwortet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        var form = new Dictionary<string, string>
        {
            ["MessageSid"] = $"SM{Guid.NewGuid():N}",
            ["From"] = phone,
            ["To"] = "+43676800100",
            ["Body"] = $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}. Eva Sommer",
        };

        using var anonymous = fixture.CreateClient();
        using var response = await PostTwilioAsync(anonymous, form, Sign(form), spa.Spa.TenantId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<Response/>", await response.Content.ReadAsStringAsync(Ct));

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(BookingChannel.Sms, mail.Channel);
        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Equal(phone, mail.ReplyTo);
        Assert.DoesNotContain("Herzliche Grüße", mail.ReplyBody, StringComparison.Ordinal);
        Assert.Contains("eingetragen", mail.ReplyBody, StringComparison.Ordinal);
        var customer = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{mail.CustomerId}", Ct);
        Assert.Equal(phone, customer.Phone);
        Assert.Equal(AppointmentSource.Sms, (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct)).Source);
        Assert.Null(customer.Email);

        // Dieselbe Nachricht noch einmal (Twilio wiederholt bei Zeitüberschreitung): nur einmal im Posteingang.
        using var again = await PostTwilioAsync(anonymous, form, Sign(form), spa.Spa.TenantId);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var send = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
        var sms = Assert.Single(fixture.Texts.To(phone));
        Assert.Equal(BookingChannel.Sms, sms.Channel);
        Assert.StartsWith(mail.ReplyBody!, sms.Text, StringComparison.Ordinal);
        Assert.EndsWith(ReplyLanguages.ShortDisclosure("de"), sms.Text, StringComparison.Ordinal);
        Assert.Single(await spa.Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", Ct), m => m.From == phone);
    }

    [Fact]
    public async Task Webhooks_ohne_gueltige_Signatur_werden_abgewiesen()
    {
        using var anonymous = fixture.CreateClient();
        var form = new Dictionary<string, string> { ["MessageSid"] = "SM1", ["From"] = NewPhone(), ["Body"] = "Termin?" };
        using var twilio = await PostTwilioAsync(anonymous, form, Convert.ToBase64String(new byte[20]));
        Assert.Equal(HttpStatusCode.Forbidden, twilio.StatusCode);

        using var whatsApp = new StringContent("{}", Encoding.UTF8, "application/json");
        whatsApp.Headers.Add("X-Hub-Signature-256", "sha256=00");
        Assert.Equal(HttpStatusCode.Forbidden, (await anonymous.PostAsync("/api/hooks/whatsapp", whatsApp, Ct)).StatusCode);

        using var voice = new StringContent("Termin bitte", Encoding.UTF8, "text/plain");
        voice.Headers.Add("X-Aispa-Voice-Key", "falsch");
        Assert.Equal(HttpStatusCode.Forbidden, (await anonymous.PostAsync($"/api/hooks/voice/recordings?from={Uri.EscapeDataString(NewPhone())}", voice, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/hooks/whatsapp?hub.mode=subscribe&hub.verify_token=falsch&hub.challenge=1", Ct)).StatusCode);
        Assert.Equal("42", await anonymous.GetStringAsync("/api/hooks/whatsapp?hub.mode=subscribe&hub.verify_token=verify-me&hub.challenge=42", Ct));
    }

    [Fact]
    public async Task WhatsApp_Nachricht_findet_den_Gast_ueber_die_Telefonnummer()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        // Im Kundenstamm in anderer Schreibweise als von WhatsApp geliefert.
        var known = await spa.Spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Lena", "Gruber", null, $"0{phone[3..6]} {phone[6..]}", null, null), Ct);
        var payload = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "1",
                    changes = new object[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                metadata = new { display_phone_number = "43676800100" },
                                contacts = new[] { new { wa_id = phone.TrimStart('+'), profile = new { name = "Lena" } } },
                                messages = new[]
                                {
                                    new
                                    {
                                        from = phone.TrimStart('+'),
                                        id = $"wamid.{Guid.NewGuid():N}",
                                        timestamp = "1803000000",
                                        type = "text",
                                        text = new { body = $"Hi, {spa.Treatment.Name} am 01.03.2027 um 11 Uhr in {spa.Location.Name}?" },
                                    },
                                },
                            },
                        },
                        new { field = "messages", value = new { statuses = new[] { new { id = "x", status = "read" } } } },
                    },
                },
            },
        });

        using var anonymous = fixture.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256",
            "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiFixture.MetaSecret), Encoding.UTF8.GetBytes(payload))));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsync($"/api/hooks/whatsapp?spa={spa.Spa.TenantId}", content, Ct)).StatusCode);

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(BookingChannel.WhatsApp, mail.Channel);
        Assert.Equal("Lena", mail.FromName);
        Assert.Equal(known.Id, mail.CustomerId);
        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.StartsWith("Guten Tag Lena Gruber,", mail.ReplyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anruf_wird_als_Abschrift_angenommen_und_per_SMS_beantwortet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        using var anonymous = fixture.CreateClient();
        using var recording = new StringContent(
            $"Grüß Gott, hier Maria Huber. Ich hätte gern eine {spa.Treatment.Name} am 01.03.2027 um 15 Uhr in {spa.Location.Name}.",
            Encoding.UTF8, "text/plain");
        recording.Headers.Add("X-Aispa-Voice-Key", ApiFixture.VoiceKey);
        var response = await anonymous.PostAsync(
            $"/api/hooks/voice/recordings?from={Uri.EscapeDataString(phone)}&callId={Guid.NewGuid():N}&language=de&spa={spa.Spa.TenantId}", recording, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(BookingChannel.Phone, mail.Channel);
        Assert.Equal(InboundMailStatus.Booked, mail.Status);

        await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
        Assert.Equal(BookingChannel.Sms, Assert.Single(fixture.Texts.To(phone)).Channel);
    }

    [Fact]
    public async Task Anruf_ueber_Twilio_bekommt_Ansage_wird_abgeschrieben_gebucht_und_per_SMS_beantwortet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        var callSid = $"CA{Guid.NewGuid():N}";
        using var anonymous = fixture.CreateClient();

        // 1. Der Anruf kommt an: Ansage mit Spa-Name, Hinweis auf Aufnahme und KI, dann Aufnahme.
        var spaName = (await spa.Spa.GetAsync<CurrentUser>("/api/auth/me", Ct)).Access!.TenantName;
        var call = new Dictionary<string, string> { ["CallSid"] = callSid, ["From"] = phone, ["To"] = "+43676800100", ["CallStatus"] = "ringing" };
        var voiceUrl = $"/api/hooks/twilio/voice?spa={spa.Spa.TenantId}";
        using var greeting = await PostTwilioAsync(anonymous, voiceUrl, call, Sign($"https://aispa.test{voiceUrl}", call));
        Assert.Equal(HttpStatusCode.OK, greeting.StatusCode);
        var twiml = await greeting.Content.ReadAsStringAsync(Ct);
        Assert.Contains($"Grüß Gott bei {spaName}.", twiml, StringComparison.Ordinal);
        Assert.Contains("aufgenommen und von unserer KI-Terminannahme ausgewertet", twiml, StringComparison.Ordinal);
        Assert.Contains("recorded and handled by our AI booking assistant", twiml, StringComparison.Ordinal);
        Assert.Contains($"<Record action=\"https://aispa.test/api/hooks/twilio/voice/recording?spa={spa.Spa.TenantId}\"", twiml, StringComparison.Ordinal);

        // Ohne gültige Signatur keine Ansage.
        using var forged = await PostTwilioAsync(anonymous, voiceUrl, call, Sign("https://andere.test/api/hooks/twilio/voice", call));
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);

        // 2. Die Aufnahme ist fertig: Twilio nennt die Adresse, die Queue holt und schreibt sie ab.
        var recordingUrl = $"https://api.twilio.com/2010-04-01/Accounts/ACtest/Recordings/RE{Guid.NewGuid():N}";
        FakeTwilioRecordings.Spoken[recordingUrl] =
            $"Grüß Gott, hier Maria Huber. Ich hätte gern eine {spa.Treatment.Name} am 01.03.2027 um 15 Uhr in {spa.Location.Name}.";
        var recorded = new Dictionary<string, string>
        {
            ["CallSid"] = callSid,
            ["From"] = phone,
            ["To"] = "+43676800100",
            ["RecordingUrl"] = recordingUrl,
            ["RecordingSid"] = recordingUrl[^34..],
            ["RecordingDuration"] = "9",
        };
        var recordingPath = $"/api/hooks/twilio/voice/recording?spa={spa.Spa.TenantId}";
        using var goodbye = await PostTwilioAsync(anonymous, recordingPath, recorded, Sign($"https://aispa.test{recordingPath}", recorded));
        Assert.Equal(HttpStatusCode.OK, goodbye.StatusCode);
        Assert.Contains("Sie bekommen in Kürze eine SMS", await goodbye.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(BookingChannel.Phone, mail.Channel);
        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Contains("Maria Huber", mail.Body, StringComparison.Ordinal);
        Assert.Contains(recordingUrl, FakeTwilioRecordings.Deleted);

        // 3. Die Antwort geht nach Freigabe per SMS an den Anrufer.
        await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
        Assert.Equal(BookingChannel.Sms, Assert.Single(fixture.Texts.To(phone)).Channel);

        // Aufgelegt vor dem Ton: nur Hangup, nichts im Posteingang.
        var empty = new Dictionary<string, string> { ["CallSid"] = $"CA{Guid.NewGuid():N}", ["From"] = NewPhone(), ["To"] = "+43676800100" };
        using var hangup = await PostTwilioAsync(anonymous, recordingPath, empty, Sign($"https://aispa.test{recordingPath}", empty));
        Assert.Equal("<Response><Hangup /></Response>", (await hangup.Content.ReadAsStringAsync(Ct)).Split('\n')[^1]);
    }

    [Fact]
    public async Task Anruf_ohne_Abschrift_landet_mit_Aufnahme_im_Posteingang_und_bleibt_bei_Twilio()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        using var anonymous = fixture.CreateClient();
        var recordingUrl = $"https://api.twilio.com/2010-04-01/Accounts/ACtest/Recordings/RE{Guid.NewGuid():N}";
        var recorded = new Dictionary<string, string> { ["CallSid"] = $"CA{Guid.NewGuid():N}", ["From"] = phone, ["To"] = "+43676800100", ["RecordingUrl"] = recordingUrl };
        var recordingPath = $"/api/hooks/twilio/voice/recording?spa={spa.Spa.TenantId}";
        using var response = await PostTwilioAsync(anonymous, recordingPath, recorded, Sign($"https://aispa.test{recordingPath}", recorded));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(BookingChannel.Phone, mail.Channel);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Contains("konnte nicht abgeschrieben werden", mail.Body, StringComparison.Ordinal);
        Assert.Contains(recordingUrl, mail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(recordingUrl, FakeTwilioRecordings.Deleted);
    }

    [Theory]
    [InlineData("whatsapp:+43 660 1234567", "+436601234567")]
    [InlineData("436601234567", "+436601234567")]
    [InlineData("0043-660-1234567", "+436601234567")]
    [InlineData("tel:+41 79 123 45 67", "+41791234567")]
    [InlineData("anonymous", "")]
    public void Telefonnummern_werden_einheitlich(string raw, string normalized) =>
        Assert.Equal(normalized, PhoneNumbers.Normalize(raw));

    [Fact]
    public void Twilio_Signatur_haengt_an_Adresse_und_allen_Feldern()
    {
        var form = new Dictionary<string, string> { ["From"] = "+43660", ["Body"] = "Hallo", ["MessageSid"] = "SM1" };
        var signature = Sign(form);
        Assert.True(TwilioWebhook.IsValid(ApiFixture.TwilioToken, TwilioUrl, form, signature));
        Assert.False(TwilioWebhook.IsValid(ApiFixture.TwilioToken, "https://andere.test/api/hooks/twilio/messages", form, signature));
        Assert.False(TwilioWebhook.IsValid(ApiFixture.TwilioToken, TwilioUrl, new Dictionary<string, string>(form) { ["Body"] = "Storno" }, signature));
        Assert.False(TwilioWebhook.IsValid(ApiFixture.TwilioToken, TwilioUrl, form, "kein-base64"));
    }

    private const string TwilioUrl = "https://aispa.test/api/hooks/twilio/messages";

    private static string NewPhone() => $"+43660{Random.Shared.Next(1_000_000, 9_999_999)}";

    /// <summary>Wie Twilio signiert: URL und die nach Namen sortierten Felder, HMAC-SHA1 mit dem Auth-Token.</summary>
    private static string Sign(Dictionary<string, string> form) => Sign(TwilioUrl, form);

    private static string Sign(string url, Dictionary<string, string> form)
    {
        var data = url + string.Concat(form.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Key + f.Value));
#pragma warning disable CA5350 // Von Twilio vorgegeben.
        return Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(ApiFixture.TwilioToken), Encoding.UTF8.GetBytes(data)));
#pragma warning restore CA5350
    }

    /// <param name="spa">Mandant aus der eingetragenen Webhook-Adresse (?spa=), wie bei einem Spa mit eigener Nummer.</param>
    private static Task<HttpResponseMessage> PostTwilioAsync(HttpClient client, Dictionary<string, string> form, string signature, Guid? spa = null) =>
        PostTwilioAsync(client, $"/api/hooks/twilio/messages{(spa is null ? "" : $"?spa={spa}")}", form, signature);

    private static async Task<HttpResponseMessage> PostTwilioAsync(HttpClient client, string path, Dictionary<string, string> form, string signature)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add("X-Twilio-Signature", signature);
        return await client.SendAsync(request, Ct);
    }
}

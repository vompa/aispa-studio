using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>
/// Webhooks der Anbieter für SMS, WhatsApp und Anrufe. Ohne Anmeldung, aber jeder Aufruf muss die Signatur bzw. den
/// Schlüssel des Anbieters tragen; ohne Konfiguration antworten sie mit 404. Eingehende Nachrichten gehen wie Mails über
/// <see cref="MailIngestion"/> in die Queue, der Webhook antwortet sofort.
/// </summary>
internal static class ChannelHookEndpoints
{
    public static IEndpointRouteBuilder MapChannelHookEndpoints(this IEndpointRouteBuilder routes)
    {
        var hooks = routes.MapGroup("/api/hooks").AllowAnonymous().ExcludeFromDescription();

        hooks.MapPost("/twilio/messages", async (HttpRequest request, IOptions<TwilioOptions> options, MailIngestion ingestion,
            TimeProvider time, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.IsConfigured)
            {
                return Results.NotFound();
            }

            var form = (await request.ReadFormAsync(ct)).ToDictionary(f => f.Key, f => f.Value.ToString());
            var url = o.WebhookUrl ?? $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
            if (!TwilioWebhook.IsValid(o.AuthToken!, url, form, request.Headers["X-Twilio-Signature"]))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (TwilioWebhook.ToIncoming(form, time.GetUtcNow()) is { } message)
            {
                await ingestion.IngestAsync(message, ct);
            }

            // Leere TwiML-Antwort: die eigentliche Antwort kommt später über die Queue, nach Freigabe.
            return Results.Content("<Response/>", "text/xml");
        });

        // Anruf über Twilio: Ansage, dann Aufnahme. Das Spa steht in ?spa= oder gehört zur angerufenen Nummer.
        hooks.MapPost("/twilio/voice", async (HttpRequest request, IOptions<TwilioOptions> options, IOptions<VoiceOptions> voice,
            AispaDbContext db, TenantContext tenants, IOptions<MailIntakeOptions> intake, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.IsConfigured)
            {
                return Results.NotFound();
            }

            var form = (await request.ReadFormAsync(ct)).ToDictionary(f => f.Key, f => f.Value.ToString());
            if (!TwilioWebhook.IsValid(o.AuthToken!, TwilioVoice.PublicUrl(o, request), form, request.Headers["X-Twilio-Signature"]))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var tenantId = tenants.TenantId
                ?? (await IntakeRoutes.FindAsync(db, BookingChannel.Phone, form.GetValueOrDefault("To") ?? "", ct))?.TenantId;
            var name = tenantId is { } id
                ? await db.Tenants.IgnoreQueryFilters().Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(ct)
                : null;
            var action = $"{TwilioVoice.Base(o, request)}{TwilioVoice.RecordingPath}{request.QueryString}";
            return Results.Content(TwilioVoice.Greeting(name ?? intake.Value.SpaName, action, voice.Value.MaxRecordingSeconds), "text/xml");
        });

        // Aufnahme fertig: Twilio nennt die Adresse der Aufnahme; holen und abschreiben erledigt die Queue.
        hooks.MapPost("/twilio/voice/recording", async (HttpRequest request, IOptions<TwilioOptions> options, TenantContext tenants,
            IMessageBus bus, TimeProvider time, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.IsConfigured)
            {
                return Results.NotFound();
            }

            var form = (await request.ReadFormAsync(ct)).ToDictionary(f => f.Key, f => f.Value.ToString());
            if (!TwilioWebhook.IsValid(o.AuthToken!, TwilioVoice.PublicUrl(o, request), form, request.Headers["X-Twilio-Signature"]))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var caller = PhoneNumbers.Normalize(form.GetValueOrDefault("From") ?? "");
            if (form.GetValueOrDefault("RecordingUrl") is not { Length: > 0 } url || form.GetValueOrDefault("CallSid") is not { Length: > 0 } callSid
                || caller.Length <= 4)
            {
                // Aufgelegt vor dem Ton oder anonym: nichts zu verarbeiten.
                return Results.Content(TwilioVoice.HangUp(), "text/xml");
            }

            await bus.PublishAsync(new TwilioRecordingReceived(
                tenants.TenantId, callSid, caller, PhoneNumbers.Normalize(form.GetValueOrDefault("To") ?? ""), url,
                form.GetValueOrDefault("RecordingSid"), time.GetUtcNow()));
            return Results.Content(TwilioVoice.Goodbye(), "text/xml");
        });

        hooks.MapGet("/whatsapp", (HttpRequest request, IOptions<MetaWhatsAppOptions> options) =>
        {
            var o = options.Value;
            var token = request.Query["hub.verify_token"].ToString();
            return o.IsConfigured && request.Query["hub.mode"] == "subscribe"
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(o.VerifyToken!))
                ? Results.Text(request.Query["hub.challenge"].ToString())
                : Results.NotFound();
        });

        hooks.MapPost("/whatsapp", async (HttpRequest request, IOptions<MetaWhatsAppOptions> options, MailIngestion ingestion, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.IsConfigured)
            {
                return Results.NotFound();
            }

            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            if (!WhatsAppCloudWebhook.IsValid(o.AppSecret!, buffer.GetBuffer().AsSpan(0, (int)buffer.Length), request.Headers["X-Hub-Signature-256"]))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            JsonElement payload;
            try
            {
                payload = JsonSerializer.Deserialize<JsonElement>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            foreach (var message in WhatsAppCloudWebhook.ToIncoming(payload))
            {
                await ingestion.IngestAsync(message, ct);
            }

            return Results.Ok();
        });

        hooks.MapPost("/voice/recordings", async (HttpRequest request, string? from, string? to, string? callId, string? language,
            IOptions<VoiceOptions> options, ISpeechToText speech, MailIngestion ingestion, TimeProvider time, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.IsConfigured)
            {
                return Results.NotFound();
            }

            var key = request.Headers["X-Aispa-Voice-Key"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(o.ApiKey!)))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(from) || PhoneNumbers.Normalize(from) is not { Length: > 4 } caller)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["from"] = ["Nummer des Anrufers fehlt."] });
            }

            if (request.ContentLength is { } length && length > o.MaxRecordingBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var transcript = await speech.TranscribeAsync(request.Body, request.ContentType ?? "application/octet-stream", language, ct);
            var id = await ingestion.IngestAsync(new IncomingMail(
                $"<call:{(string.IsNullOrWhiteSpace(callId) ? Guid.NewGuid().ToString("N") : callId.Trim())}>",
                caller,
                null,
                PhoneNumbers.Normalize(to ?? ""),
                "Anruf",
                Mailboxes.MimeMail.Trim(transcript.Text),
                time.GetUtcNow(),
                SenderVerified: true,
                BookingChannel.Phone), ct);
            return Results.Accepted(value: new { id });
        });

        return routes;
    }
}

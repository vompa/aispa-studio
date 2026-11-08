using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>Posteingang der Terminannahme für die Rezeption.</summary>
internal static class MailIntakeEndpoints
{
    public static IEndpointRouteBuilder MapMailIntakeEndpoints(this IEndpointRouteBuilder routes, IWebHostEnvironment env)
    {
        var group = routes.MapGroup("/api/inbox").WithTags("Terminannahme").RequirePermission(AccessRight.Book);

        group.MapGet("/", async (InboundMailStatus? status, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var visible = access.LocationIds.ToList();
                // Mails ohne erkannten Standort sieht jede Person mit Buchungsrecht; sonst nur die eigenen Standorte.
                var query = db.InboundMails.AsNoTracking()
                    .Where(m => access.AllLocations || m.LocationId == null || visible.Contains(m.LocationId.Value));
                if (status is { } wanted)
                {
                    query = query.Where(m => m.Status == wanted);
                }

                return await query.OrderByDescending(m => m.ReceivedAt).Take(100)
                    .Select(m => new InboundMailSummary(m.Id, m.Channel, m.From, m.FromName, m.Subject, m.ReceivedAt,
                        m.Status, m.Kind, m.ReplyStatus, m.Summary, m.AppointmentId))
                    .ToListAsync(ct);
            })
            .WithName("ListInboundMails")
            .WithSummary("Eingegangene Mails der eigenen Standorte, neueste zuerst (höchstens 100), optional nach Bearbeitungsstand gefiltert.");

        group.MapGet("/{mailId:guid}", async Task<Results<Ok<InboundMailDto>, NotFound>> (
                Guid mailId, AispaDbContext db, IReplyReviser reviser, CancellationToken ct) =>
                await db.InboundMails.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mailId, ct) is { } mail
                    ? TypedResults.Ok(ToDto(mail, reviser.Available))
                    : TypedResults.NotFound())
            .WithName("GetInboundMail");

        group.MapPost("/{mailId:guid}/accept", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem, Conflict<BookingRejected>>> (
                Guid mailId, AcceptProposalRequest request, AispaDbContext db, BookingService booking, CustomerMatcher matcher, IMessageBus bus,
                IReplyReviser reviser, TimeProvider time, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(mail.AppointmentId is null || mail.Status == InboundMailStatus.NeedsReview, "status", "Für diese Mail ist schon ein Termin eingetragen.");
                errors.Check(mail.LocationId is not null, nameof(mail.LocationId), "Standort unbekannt.");
                errors.Check(mail.TreatmentId is not null, nameof(mail.TreatmentId), "Behandlung unbekannt.");
                var claimed = mail.Channel == BookingChannel.Treatwell && mail.PlatformReference is { } treatwellReference
                    && await db.ChannelBookings.AnyAsync(b => b.Channel == BookingChannel.Treatwell && b.ExternalId == treatwellReference && b.AppointmentId != null, ct);
                errors.Check(!claimed, "status", "Für diese Treatwell-Buchung gibt es schon einen Termin.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (await CustomerForBookingAsync(mail, db, matcher, time, ct) is not { } customerId)
                {
                    return WhoIsThisFirst();
                }

                var outcome = await booking.BookAsync(new AppointmentRequest(
                    mail.LocationId!.Value, mail.TreatmentId!.Value, customerId, request.Start,
                    request.EmployeeId, request.RoomId, request.EquipmentId,
                    mail.Channel == BookingChannel.Treatwell && mail.PlatformReference is { } reference
                        ? $"Treatwell-Buchung {reference}"
                        : $"Per {MailIntakeProcessor.ChannelName(mail.Channel)} angefragt (KI-Terminannahme)"), ct,
                    source: MailIntakeProcessor.Source(mail.Channel),
                    platform: mail.Channel == BookingChannel.Treatwell ? new PlatformBooking(mail.PlatformPayment, mail.PrepaidAmount, AcceptPending: true) : null);
                switch (outcome)
                {
                    case BookingOutcome.Success success:
                        // Nach einer Kollision hat der BookingService den Change-Tracker geleert; frisch laden.
                        mail = await db.InboundMails.FirstAsync(m => m.Id == mailId, ct);
                        if (mail is { Channel: BookingChannel.Treatwell, PlatformReference: { } claim })
                        {
                            // Die Rezeption trägt die Treatwell-Buchung selbst ein: ab jetzt finden Änderung und Absage den Termin.
                            db.ChannelBookings.Add(new ChannelBooking
                            {
                                Channel = BookingChannel.Treatwell,
                                ExternalId = claim,
                                AppointmentId = success.Appointment.Id,
                                InboundMailId = mail.Id,
                                CreatedAt = time.GetUtcNow(),
                            });
                            mail.AcceptInPlatformPending = true;
                        }

                        mail.Status = InboundMailStatus.Booked;
                        mail.CustomerId = customerId;
                        mail.CustomerConfirmed = true;
                        mail.CustomerCandidates = [];
                        mail.AppointmentId = success.Appointment.Id;
                        mail.Proposals = [];
                        mail.Note = null;
                        await db.SaveChangesAsync(ct);
                        await bus.PublishAsync(new ReplyDraftRequested(mail.Id));
                        return TypedResults.Ok(ToDto(mail, reviser.Available));
                    case BookingOutcome.Rejected rejected:
                        return TypedResults.Conflict(rejected.Reason);
                    case BookingOutcome.NotFound notFound:
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["mail"] = [$"{notFound.What} nicht gefunden."] });
                    default:
                        throw new InvalidOperationException($"Unerwartetes Ergebnis {outcome}.");
                }
            })
            .WithName("AcceptInboundMailProposal")
            .WithSummary("Bucht eine vorgeschlagene (oder andere) Zeit für die Mail; die Bestätigung an den Gast wird neu entworfen.");

        group.MapPost("/{mailId:guid}/hold", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem, Conflict<BookingRejected>>> (
                Guid mailId, HoldTimeRequest request, AispaDbContext db, BookingService booking, CustomerMatcher matcher, IMessageBus bus,
                IReplyReviser reviser, TimeProvider time, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(mail.AppointmentId is null, "status", "Für diese Anfrage ist schon ein Termin eingetragen.");
                errors.Check(mail.Channel != BookingChannel.Treatwell, "status", "Treatwell-Buchungen werden in Treatwell bestätigt, nicht vorgemerkt.");
                errors.Check(mail.ReplyStatus != ReplyStatus.Sending, "status", "Die Antwort wird gerade gesendet.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (await CustomerForBookingAsync(mail, db, matcher, time, ct) is not { } customerId)
                {
                    return WhoIsThisFirst();
                }

                var outcome = await booking.BookAsync(new AppointmentRequest(
                    request.LocationId, request.TreatmentId, customerId, request.Start, request.EmployeeId, request.RoomId, request.EquipmentId,
                    $"Vorgemerkt aus dem Posteingang ({MailIntakeProcessor.ChannelName(mail.Channel)})"), ct, source: MailIntakeProcessor.Source(mail.Channel));
                switch (outcome)
                {
                    case BookingOutcome.Success success:
                        await booking.MarkHeldAsync(success.Appointment.Id, mailId, ct);
                        // Nach dem Buchen ist der Change-Tracker womöglich geleert; frisch laden.
                        mail = await db.InboundMails.FirstAsync(m => m.Id == mailId, ct);
                        mail.Status = InboundMailStatus.Proposed;
                        mail.AppointmentId = success.Appointment.Id;
                        mail.HeldStart = success.Appointment.Start;
                        mail.LocationId = request.LocationId;
                        mail.TreatmentId = request.TreatmentId;
                        mail.Proposals = [];
                        mail.Note = null;
                        // Eine schon gesendete Rückfrage bleibt im Verlauf; der Vorschlag ist eine neue Antwort.
                        if (mail.ReplyStatus == ReplyStatus.Sent)
                        {
                            mail.ReplyStatus = ReplyStatus.Draft;
                        }

                        await db.SaveChangesAsync(ct);
                        await bus.PublishAsync(new ReplyDraftRequested(mail.Id));
                        return TypedResults.Ok(ToDto(mail, reviser.Available));
                    case BookingOutcome.Rejected rejected:
                        return TypedResults.Conflict(rejected.Reason);
                    case BookingOutcome.NotFound notFound:
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["mail"] = [$"{notFound.What} nicht gefunden."] });
                    case BookingOutcome.Invalid invalid:
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] });
                    default:
                        throw new InvalidOperationException($"Unerwartetes Ergebnis {outcome}.");
                }
            })
            .WithName("HoldInboundMailTime")
            .WithSummary("Merkt eine freie Zeit für den Gast der Anfrage vor (ADR 0083): der Termin belegt den Kalender, bis die Rezeption bestätigt oder freigibt; "
                + "der Antwortentwurf schlägt dem Gast die Zeit vor.");

        group.MapPut("/{mailId:guid}/reply", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, ReplyDraftInput input, AispaDbContext db, MailIntakeProcessor processor, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Require(nameof(input.Subject), input.Subject);
                errors.Require(nameof(input.Body), input.Body);
                errors.Check(mail.ReplyTo is not null, "replyTo", "Für diese Mail gibt es keinen Empfänger.");
                errors.Check(mail.ReplyStatus is ReplyStatus.None or ReplyStatus.Draft, "replyStatus", "Die Antwort ist schon gesendet.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                mail.ReplySubject = input.Subject.Trim();
                mail.ReplyBody = input.Body.Trim();
                mail.ReplyStatus = ReplyStatus.Draft;
                await db.SaveChangesAsync(ct);
                await processor.QueueTranslationAsync(mail, ct);
                return TypedResults.Ok(ToDto(mail, reviser.Available));
            })
            .WithName("UpdateInboundMailReply");

        group.MapPost("/{mailId:guid}/reply/revise", async Task<Results<Accepted<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, ReplyRevisionInput input, AispaDbContext db, IMessageBus bus, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                var instruction = input.Instruction?.Trim() is { Length: > 0 } text ? text : null;
                var errors = new Errors();
                errors.Check(reviser.Available, "kind", "Überarbeiten braucht die KI; ohne API-Schlüssel ist es nicht verfügbar.");
                errors.Check(mail.ReplyStatus == ReplyStatus.Draft && !string.IsNullOrWhiteSpace(mail.ReplyBody), "replyStatus",
                    "Es gibt keinen Entwurf zum Überarbeiten.");
                errors.Check(input.Kind != ReplyRevisionKind.Custom || instruction is not null, "instruction", "Bitte sagen, was geändert werden soll.");
                errors.Check(instruction is null || instruction.Length <= 300, "instruction", "Höchstens 300 Zeichen.");
                errors.Check(!mail.ReplyRevising, "replyStatus", "Die KI überarbeitet den Entwurf schon.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                mail.ReplyRevising = true;
                mail.ReplyNotice = null;
                await db.SaveChangesAsync(ct);
                await bus.PublishAsync(new ReplyRevisionRequested(mail.Id, input.Kind, input.Kind == ReplyRevisionKind.Custom ? instruction : null));
                return TypedResults.Accepted($"/api/inbox/{mail.Id}", ToDto(mail, reviser.Available));
            })
            .WithName("ReviseInboundMailReply")
            .WithSummary("Lässt die KI den Entwurf überarbeiten (freundlicher, kürzer, förmlicher, du, Entschuldigung oder eigener Hinweis). "
                + "Läuft über die Queue; eine Fassung mit neuen Zahlen wird verworfen.");

        group.MapPost("/{mailId:guid}/reply/undo", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AispaDbContext db, MailIntakeProcessor processor, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                if (mail.ReplyStatus != ReplyStatus.Draft || mail.ReplyRevising || mail.ReplyHistory.Count == 0)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["replyStatus"] = ["Es gibt keine frühere Fassung."] });
                }

                mail.ReplyBody = mail.ReplyHistory[^1];
                mail.ReplyHistory = mail.ReplyHistory[..^1];
                mail.ReplyNotice = null;
                await db.SaveChangesAsync(ct);
                await processor.QueueTranslationAsync(mail, ct);
                return TypedResults.Ok(ToDto(mail, reviser.Available));
            })
            .WithName("UndoInboundMailReplyRevision")
            .WithSummary("Holt die Fassung des Entwurfs vor der letzten KI-Überarbeitung zurück.");

        group.MapPost("/{mailId:guid}/repropose", async Task<Results<Accepted<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, ReproposeInput input, AispaDbContext db, IMessageBus bus, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                var hint = input.Hint?.Trim() is { Length: > 0 } text ? text : null;
                var errors = new Errors();
                errors.Check(CanRepropose(mail), "status", "Neu vorschlagen geht nur bei einer Terminanfrage ohne Termin, solange keine Antwort gesendet wird.");
                errors.Check(hint is null || hint.Length <= 300, "hint", "Höchstens 300 Zeichen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                mail.Reproposing = true;
                await db.SaveChangesAsync(ct);
                await bus.PublishAsync(new ReproposeRequested(mail.Id, hint));
                return TypedResults.Accepted($"/api/inbox/{mail.Id}", ToDto(mail, reviser.Available));
            })
            .WithName("ReproposeInboundMail")
            .WithSummary("Rechnet neue Vorschläge: die bisherigen gelten als abgelehnt, ein Hinweis („lieber am Abend“) ergänzt den Wunsch. "
                + "Läuft über die Queue und entwirft die Antwort neu.");

        group.MapPost("/{mailId:guid}/send", async Task<Results<Accepted<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AispaDbContext db, MailIntakeProcessor processor, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                if (mail.ReplyStatus != ReplyStatus.Draft || mail.ReplyTo is null || string.IsNullOrWhiteSpace(mail.ReplyBody))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["replyStatus"] = ["Es gibt keinen Entwurf zum Senden."] });
                }

                await processor.QueueSendAsync(mail, ct);
                return TypedResults.Accepted($"/api/inbox/{mail.Id}", ToDto(mail, reviser.Available));
            })
            .WithName("SendInboundMailReply")
            .WithSummary("Gibt den Antwortentwurf frei. Gesendet wird über die Queue, mit KI-Hinweis unter dem Text.");

        group.MapPost("/{mailId:guid}/platform-accepted", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AispaDbContext db, BookingService booking, IReplyReviser reviser, CancellationToken ct) =>
            {
                if (await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct) is not { } mail)
                {
                    return TypedResults.NotFound();
                }

                if (mail.Channel != BookingChannel.Treatwell)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Das gibt es nur für Treatwell-Buchungen."] });
                }

                if (mail.AppointmentId is { } appointmentId)
                {
                    await booking.ConfirmPlatformAcceptedAsync(appointmentId, ct);
                    db.ChangeTracker.Clear();
                    mail = await db.InboundMails.FirstAsync(m => m.Id == mailId, ct);
                }
                else
                {
                    mail.AcceptInPlatformPending = false;
                    await db.SaveChangesAsync(ct);
                }

                return TypedResults.Ok(ToDto(mail, reviser.Available));
            })
            .WithName("ConfirmInboundMailPlatformAccepted")
            .WithSummary("Die Rezeption hat die Treatwell-Buchung in Treatwell angenommen; der Hinweis „In Treatwell noch annehmen“ verschwindet in der Mail und am Termin.");

        group.MapPost("/{mailId:guid}/dismiss", async Task<Results<Ok<InboundMailDto>, NotFound>> (
                Guid mailId, AispaDbContext db, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                mail.Status = InboundMailStatus.Ignored;
                mail.Note = "Von der Rezeption erledigt.";
                if (mail.ReplyStatus == ReplyStatus.Draft)
                {
                    mail.ReplyStatus = ReplyStatus.None;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(mail, reviser.Available));
            })
            .WithName("DismissInboundMail")
            .WithSummary("Als erledigt markieren, ohne Termin und ohne Antwort.");

        group.MapPost("/{mailId:guid}/cancel-appointment", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem, Conflict<BookingRejected>>> (
                Guid mailId, AispaDbContext db, AccessService accessService, BookingService booking, IMessageBus bus, IReplyReviser reviser,
                CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                if (!CanCancelAppointment(mail))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Zu dieser Mail gibt es keinen Termin zum Stornieren."] });
                }

                // Der Termin kann an einem anderen Standort liegen als die Mail; storniert wird nur an eigenen Standorten.
                var access = (await accessService.CurrentAsync(ct))!;
                var appointmentId = mail.AppointmentId!.Value;
                var location = await db.Appointments.Where(a => a.Id == appointmentId).Select(a => (Guid?)a.LocationId).FirstOrDefaultAsync(ct);
                if (location is null || !(access.AllLocations || access.LocationIds.Contains(location.Value)))
                {
                    return TypedResults.NotFound();
                }

                var outcome = await booking.CancelAsync(appointmentId, $"Abgesagt per {MailIntakeProcessor.ChannelName(mail.Channel)} (KI-Terminannahme)", ct);
                switch (outcome)
                {
                    case BookingOutcome.Success:
                        mail = await db.InboundMails.FirstAsync(m => m.Id == mailId, ct);
                        mail.Status = InboundMailStatus.Cancelled;
                        mail.Note = null;
                        await db.SaveChangesAsync(ct);
                        await bus.PublishAsync(new ReplyDraftRequested(mail.Id));
                        return TypedResults.Ok(ToDto(mail, reviser.Available));
                    case BookingOutcome.Rejected rejected:
                        return TypedResults.Conflict(rejected.Reason);
                    default:
                        return TypedResults.NotFound();
                }
            })
            .WithName("CancelInboundMailAppointment")
            .WithSummary("Storniert den Termin, den der Gast in seiner Nachricht absagt; die Antwort an den Gast wird neu entworfen.");

        group.MapPost("/{mailId:guid}/retry", async Task<Results<Accepted<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AispaDbContext db, IMessageBus bus, IReplyReviser reviser, CancellationToken ct) =>
            {
                var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
                if (mail is null)
                {
                    return TypedResults.NotFound();
                }

                if (mail.Status != InboundMailStatus.Failed)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Nur fehlgeschlagene Mails werden erneut verarbeitet."] });
                }

                mail.Status = InboundMailStatus.Received;
                mail.Note = null;
                await db.SaveChangesAsync(ct);
                await bus.PublishAsync(new InboundMailReceived(mail.Id));
                return TypedResults.Accepted($"/api/inbox/{mail.Id}", ToDto(mail, reviser.Available));
            })
            .WithName("RetryInboundMail");

        routes.MapGet("/api/locations/{locationId:guid}/appointment-holds", async Task<Ok<List<AppointmentHoldDto>>> (
                Guid locationId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var now = time.GetUtcNow();
                var open = await db.Appointments.AsNoTracking()
                    .Where(a => a.LocationId == locationId && a.HeldSince != null && a.HoldAnsweredAt == null && a.HoldRemindAt != null
                        && a.Status == AppointmentStatus.Booked && a.Start > now)
                    .OrderBy(a => a.HoldRemindAt).ThenBy(a => a.Id)
                    .Select(a => new AppointmentHoldDto(a.Id, a.HoldMailId, a.Customer.InHouseName, a.Treatment.Name, a.Start, a.HoldRemindAt!.Value))
                    .ToListAsync(ct);
                return TypedResults.Ok(open);
            })
            .WithTags("Terminannahme")
            .RequirePermission(AccessRight.Book)
            .WithName("ListAppointmentHolds")
            .WithSummary("Offene Vormerkungen des Standorts ohne Antwort des Gastes, mit dem Zeitpunkt, ab dem der Kalender erinnert (ADR 0083).");

        var holds = routes.MapGroup("/api/appointments/{appointmentId:guid}/hold").WithTags("Terminannahme").RequirePermission(AccessRight.Book);

        holds.MapPost("/confirm", async Task<Results<Ok<AppointmentDto>, NotFound, ValidationProblem, Conflict<BookingRejected>>> (
                Guid appointmentId, BookingService booking, IMessageBus bus, CancellationToken ct) =>
            {
                var (outcome, redraft) = await booking.ConfirmHoldAsync(appointmentId, ct);
                foreach (var id in redraft)
                {
                    await bus.PublishAsync(new ReplyDraftRequested(id));
                }

                return HoldResult(outcome);
            })
            .WithName("ConfirmAppointmentHold")
            .WithSummary("Bestätigt eine Vormerkung (ADR 0083): der Termin ist fest gebucht, die Bestätigung an den Gast wird neu entworfen.");

        holds.MapPost("/release", async Task<Results<Ok<AppointmentDto>, NotFound, ValidationProblem, Conflict<BookingRejected>>> (
                Guid appointmentId, BookingService booking, CancellationToken ct) =>
                HoldResult(await booking.ReleaseHoldAsync(appointmentId, ct)))
            .WithName("ReleaseAppointmentHold")
            .WithSummary("Gibt eine Vormerkung frei (ADR 0083): der Termin wird ohne Nachricht an den Gast storniert, die Anfrage steht wieder auf „Prüfen“.");

        if (env.IsDevelopment())
        {
            // Test-Postfach: Mails einwerfen (gesendete Antworten stehen in der Entwicklung im Log). Nur in der Entwicklung, nicht im OpenAPI-Dokument.
            var dev = routes.MapGroup("/api/inbox/dev").RequirePermission(AccessRight.Book).ExcludeFromDescription();

            dev.MapPost("/mails", async (TestMailInput input, MailIngestion ingestion, TimeProvider time, CancellationToken ct) =>
            {
                var id = await ingestion.IngestAsync(new IncomingMail(
                    input.MessageId ?? $"<{Guid.NewGuid():N}@test.aispa>",
                    input.From, input.FromName, input.To ?? "termine@aispa.test", input.Subject, input.Body,
                    input.ReceivedAt ?? time.GetUtcNow(), input.SenderVerified ?? true), ct);
                return TypedResults.Ok(new { id });
            });

            dev.MapPost("/messages", async (TestMessageInput input, MailIngestion ingestion, TimeProvider time, CancellationToken ct) =>
            {
                var channel = input.Channel is BookingChannel.Sms or BookingChannel.WhatsApp or BookingChannel.Phone ? input.Channel : BookingChannel.Sms;
                var id = await ingestion.IngestAsync(new IncomingMail(
                    $"<{Guid.NewGuid():N}@test.aispa>", PhoneNumbers.Normalize(input.From), input.FromName, "+43100000000",
                    MailIntakeProcessor.ChannelName(channel), input.Text, time.GetUtcNow(), SenderVerified: true, channel), ct);
                return TypedResults.Ok(new { id });
            });
        }

        group.MapInboxCustomerEndpoints();

        routes.MapChannelHookEndpoints();
        routes.MapIntakeEndpoints();
        routes.MapSenderRuleEndpoints();
        return routes;
    }

    private static Results<Ok<AppointmentDto>, NotFound, ValidationProblem, Conflict<BookingRejected>> HoldResult(BookingOutcome outcome) => outcome switch
    {
        BookingOutcome.Success success => TypedResults.Ok(success.Appointment),
        BookingOutcome.NotFound => TypedResults.NotFound(),
        BookingOutcome.Invalid invalid => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] }),
        BookingOutcome.Rejected rejected => TypedResults.Conflict(rejected.Reason),
        _ => throw new InvalidOperationException($"Unerwartetes Ergebnis {outcome}."),
    };

    private static ValidationProblem WhoIsThisFirst() => TypedResults.ValidationProblem(new Dictionary<string, string[]>
    {
        ["customer"] = ["Wer ist das? Bitte zuerst einen Vorschlag zuordnen oder den Gast bewusst neu anlegen; erst dann lässt sich der Termin eintragen."],
    });

    /// <summary>
    /// Der Gast für einen Termin aus der Anfrage: bestätigt, sicher erkannt oder neu angelegt. Gibt es Kandidaten, nicht still neu anlegen, sondern erst
    /// zuordnen lassen (ADR 0061); dann <c>null</c>, und die Kandidaten stehen an der Mail.
    /// </summary>
    private static async Task<Guid?> CustomerForBookingAsync(InboundMail mail, AispaDbContext db, CustomerMatcher matcher, TimeProvider time, CancellationToken ct)
    {
        if (mail is { CustomerConfirmed: true, CustomerId: { } confirmed })
        {
            return confirmed;
        }

        var match = await matcher.MatchAsync(MailIntakeProcessor.MatchRequest(mail), ct);
        if (match.Certain is null && match.Candidates.Count > 0)
        {
            mail.CustomerCandidates = [.. match.Candidates];
            await db.SaveChangesAsync(ct);
            return null;
        }

        var customerId = match.Certain?.Id ?? await CreateCustomerAsync(mail, db, time, ct);
        mail.CustomerId = customerId;
        mail.CustomerConfirmed = true;
        mail.CustomerCandidates = [];
        await db.SaveChangesAsync(ct);
        return customerId;
    }

    private static async Task<Guid> CreateCustomerAsync(InboundMail mail, AispaDbContext db, TimeProvider time, CancellationToken ct)
    {
        var customer = MailIntakeProcessor.NewCustomer(mail, time.GetUtcNow());
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer.Id;
    }

    /// <param name="canRevise">Ob es die KI-Überarbeitung gibt (mit API-Schlüssel).</param>
    public static InboundMailDto ToDto(InboundMail m, bool canRevise = false) => new(
        m.Id, m.Channel, m.From, m.FromName, m.Subject, m.Body, m.ReceivedAt, m.Status, m.Kind, m.Language, m.Summary, m.Note,
        m.LocationId, m.TreatmentId, m.CustomerId, m.AppointmentId, m.Proposals, m.ReplyStatus, m.ReplyTo, m.ReplySubject, m.ReplyBody,
        m.ReplyTo is null ? null
            : MailIntakeProcessor.IsShortMessage(m.Channel) ? ReplyLanguages.ShortDisclosure(m.Language ?? "de")
            : ReplyLanguages.Disclosure(m.Language ?? "de"),
        m.ProcessedAt, m.RepliedAt,
        m.TranslationState, m.TranslatedSubject, m.TranslatedBody,
        m.ReplyBody is { } reply && m.ReplyTranslationOf == MailIntakeProcessor.Fingerprint(reply) ? m.ReplyTranslation : null,
        m.ReplyRevising, m.ReplyStatus == ReplyStatus.Draft && m.ReplyHistory.Count > 0 && !m.ReplyRevising, m.ReplyNotice,
        canRevise && m.ReplyStatus == ReplyStatus.Draft,
        m.PreviousMailId, m.RejectedStarts, m.ProposalRound, m.WishNote, m.Reproposing, CanRepropose(m), CanCancelAppointment(m),
        m.CustomerCandidates, m.CustomerConfirmed,
        m.PlatformReference, m.PlatformPayment, m.PrepaidAmount, m.AcceptInPlatformPending,
        m is { Channel: BookingChannel.Treatwell, Kind: InboundMailKind.TreatwellBooking, Status: InboundMailStatus.Booked, AppointmentId: not null, CustomerConfirmed: false },
        m.SenderRulePattern, m.SuggestedSalutation, m.SuggestedTitlesBefore, m.HeldStart);

    /// <summary>Terminanfrage ohne Termin, nichts läuft gerade; auch nachdem der Vorschlag gesendet ist (der Gast ruft an).</summary>
    private static bool CanRepropose(InboundMail m) =>
        m.Kind == InboundMailKind.BookingRequest && m.Status is InboundMailStatus.Proposed or InboundMailStatus.NeedsReview
        && m.AppointmentId is null && m.ReplyStatus != ReplyStatus.Sending && !m.Reproposing && !m.ReplyRevising;

    /// <summary>Absage, zu der die Terminannahme den nächsten Termin des Gasts gefunden hat; noch nicht storniert.</summary>
    private static bool CanCancelAppointment(InboundMail m) =>
        m.Kind == InboundMailKind.Cancellation && m.Status == InboundMailStatus.NeedsReview && m.AppointmentId is not null && m.CustomerConfirmed
        && m.ReplyStatus != ReplyStatus.Sending && !m.ReplyRevising;

    /// <summary>SMS, WhatsApp oder Anruf (als Abschrift) für die Entwicklung, ohne Anbieter.</summary>
    internal sealed record TestMessageInput(BookingChannel Channel, string From, string? FromName, string Text);

    /// <summary>Mail für das Test-Postfach. Ohne Angabe gilt der Absender als bestätigt.</summary>
    internal sealed record TestMailInput(
        string From,
        string? FromName,
        string Subject,
        string Body,
        string? To,
        string? MessageId,
        DateTimeOffset? ReceivedAt,
        bool? SenderVerified);
}

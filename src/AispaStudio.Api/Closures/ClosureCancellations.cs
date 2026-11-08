using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Termine einer Schließzeit absagen und die Gäste benachrichtigen (ADR 0073). Nichts geschieht von selbst: die Rückfrage (<see cref="PreviewAsync"/>) zeigt die
/// offenen Termine und Wartelisten-Einträge und den Vorschlag für den Text, <see cref="CancelAsync"/> sagt die gewählten Termine ab (Status „storniert“,
/// <see cref="Appointment.CancelledByBusiness"/>, kein Ausfallhonorar), retourniert eine geleistete Anzahlung über die Kassa (<see cref="DepositReturns"/>) und stellt je Termin und
/// je Wartelisten-Eintrag genau eine Nachricht in die Queue (<see cref="SendClosureNotice"/>), in der Sprache des Gastes (Ä46). Wer bucht, darf auch absagen (<see cref="AccessRight.Book"/>).
/// </summary>
internal sealed class ClosureCancellations(
    AispaDbContext db,
    OnlineDbContext online,
    ClosureService closures,
    BookingService booking,
    DepositReturns depositReturns,
    Deposits deposits,
    IMessageBus bus,
    TenantContext tenants,
    IOptions<OnlineBookingOptions> onlineOptions,
    IOptions<TextChannelOptions> textChannels,
    IReplyReviser reviser,
    TimeProvider time)
{
    /// <summary>Die Vorlage, die das Team bearbeitet, ist deutsch; die übrigen Sprachen bekommen ihre Standardvorlage.</summary>
    private const string German = "de";

    // ---------- Rückfrage ----------

    public async Task<ClosureCancellationPreview?> PreviewAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        if (await closures.ZoneAsync(locationId, ct) is not { } zone || await closures.FindByIdAsync(locationId, closureId, ct) is not { } closure)
        {
            return null;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == locationId, ct);
        var now = time.GetUtcNow();
        var appointments = await ClosureService.OpenAppointmentsAsync(db, closure, zone, now, ct);
        var settings = await ReminderSettingsStore.LoadAsync(db, ct);
        var ids = appointments.Select(a => a.Id).ToList();
        var refundable = await DepositAmountsAsync(ids, ct);
        var employeeIds = appointments.Select(a => a.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.InHouseName, ct);
        var today = LocalTime.LocalDate(now, zone);
        var (subject, text) = ClosureText.CancellationTemplate(closure.Kind, closure.GuestText,
            ClosureText.Period(closure.FromDate, closure.FromTime, closure.UntilDate, closure.UntilTime, today), location.Name);
        return new ClosureCancellationPreview(
            await closures.ToDtoAsync(closure, zone, ct),
            [.. appointments.Select(a => new ClosureAppointmentDto(a.Id, a.Customer.InHouseName, a.Treatment.Name, a.Start, a.End,
                employees.GetValueOrDefault(a.EmployeeId, "Person"), a.Status, ChannelFor(settings, a.Customer), refundable.ContainsKey(a.Id),
                a.PlatformPayment == PlatformPayment.Prepaid && a.PrepaidAmount > 0, GuestLanguage.Resolve(a.Customer), refundable.TryGetValue(a.Id, out var deposit) ? deposit : null))],
            subject, text, reviser.Available,
            await WaitlistDtosAsync(closure, zone, now, settings, ct));
    }

    /// <summary>Protokoll: alle Absagen dieser Schließzeit, die jüngsten zuerst.</summary>
    public async Task<List<ClosureNoticeDto>?> NoticesAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        if (await closures.FindByIdAsync(locationId, closureId, ct) is null)
        {
            return null;
        }

        var notices = await db.ClosureNotices.AsNoTracking().Where(n => n.ClosureId == closureId).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync(ct);
        return await ToDtosAsync(notices, ct);
    }

    // ---------- Absagen ----------

    /// <summary>
    /// Sagt die gewählten Termine ab und benachrichtigt die gewählten Wartelisten-Einträge. <c>(null, errors)</c>: die Anfrage ist ungültig (nichts wurde abgesagt); <c>(null, null)</c>:
    /// Schließzeit nicht gefunden. Ein Termin, der sich inzwischen geändert hat, bleibt offen und zählt als <c>Failed</c>.
    /// </summary>
    public async Task<(CancelClosureAppointmentsResult? Result, Errors? Errors)> CancelAsync(
        Guid locationId, Guid closureId, CancelClosureAppointmentsRequest request, UserAccess access, CancellationToken ct)
    {
        if (await closures.ZoneAsync(locationId, ct) is not { } zone || await closures.FindByIdAsync(locationId, closureId, ct) is not { } closure)
        {
            return (null, null);
        }

        var ids = (request.AppointmentIds ?? []).Distinct().ToList();
        var waitlistIds = (request.WaitlistEntryIds ?? []).Distinct().ToList();
        var subject = request.Subject?.Trim() ?? "";
        var text = request.Text?.Trim() ?? "";
        var errors = new Errors();
        errors.Check(ids.Count + waitlistIds.Count > 0, nameof(request.AppointmentIds), "Bitte mindestens einen Termin oder Eintrag der Warteliste wählen.");
        if (ids.Count > 0)
        {
            errors.Check(subject.Length is > 0 and <= ClosureModel.SubjectLength, nameof(request.Subject), $"Bitte einen Betreff eingeben (höchstens {ClosureModel.SubjectLength} Zeichen).");
            errors.Check(text.Length is > 0 and <= ClosureModel.TextLength, nameof(request.Text), $"Bitte einen Text eingeben (höchstens {ClosureModel.TextLength} Zeichen).");
        }

        if (errors.Any)
        {
            return (null, errors);
        }

        var now = time.GetUtcNow();
        var open = (await ClosureService.OpenAppointmentsAsync(db, closure, zone, now, ct)).ToDictionary(a => a.Id);
        errors.Check(ids.All(open.ContainsKey), nameof(request.AppointmentIds), "Ein gewählter Termin liegt nicht in der Schließzeit oder ist nicht mehr offen.");
        var waiting = (await ClosureService.OpenWaitlistAsync(db, online, closure, zone, now, ct)).ToDictionary(w => w.Id);
        errors.Check(waitlistIds.All(waiting.ContainsKey), nameof(request.WaitlistEntryIds), "Ein gewählter Eintrag der Warteliste liegt nicht in der Schließzeit oder ist schon benachrichtigt.");
        if (errors.Any)
        {
            return (null, errors);
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == locationId, ct);
        var settings = await ReminderSettingsStore.LoadAsync(db, ct);
        var name = await db.Users.AsNoTracking().Where(u => u.Id == access.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) is { Length: > 0 } display ? display : "Unbekannt";
        var today = LocalTime.LocalDate(now, zone);
        var reason = $"Schließzeit ({ClosureText.Title(closure.Kind)}): {ClosureText.Period(closure.FromDate, closure.FromTime, closure.UntilDate, closure.UntilTime, today)}";
        var link = request.OfferRebooking ? RebookingLink() : null;
        var texts = new Templates(closure, location, today, subject, text);

        var created = new List<ClosureNotice>();
        var (failed, refunded, refundsFailed) = (0, 0, 0);
        foreach (var appointment in ids.Select(id => open[id]))
        {
            if (await booking.CancelAsync(appointment.Id, reason, ct, byBusiness: true) is not BookingOutcome.Success)
            {
                failed++;
                continue;
            }

            var refund = await RefundAsync(appointment, reason, access.UserId, ct);
            refunded += refund.Amount is null ? 0 : 1;
            refundsFailed += refund.Error is null ? 0 : 1;
            created.Add(await AddNoticeAsync(AppointmentNotice(request, appointment, location, zone, settings, texts, link, refund, access, name), ct));
        }

        foreach (var entry in waitlistIds.Select(id => waiting[id]))
        {
            // Ein Eintrag ohne Kundendatensatz gibt es praktisch nicht; er bliebe offen und stünde in der nächsten Rückfrage wieder da.
            if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entry.CustomerId, ct) is not { } customer)
            {
                continue;
            }

            var treatment = await db.Treatments.AsNoTracking().Where(t => t.Id == entry.TreatmentId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "";
            created.Add(await AddNoticeAsync(WaitlistNotice(entry, customer, treatment, location, texts, settings, access, name), ct));
        }

        foreach (var notice in created.Where(n => n.Channel != ClosureChannel.None))
        {
            await DispatchAsync(notice);
        }

        var dtos = await ToDtosAsync(created, ct);
        return (new CancelClosureAppointmentsResult(created.Count(n => n.AppointmentId is not null), created.Count(n => n.State == ClosureNoticeState.Queued),
            created.Count(n => n.State == ClosureNoticeState.Skipped), failed, dtos, created.Count(n => n.WaitlistEntryId is not null), refunded, refundsFailed), null);
    }

    /// <summary>
    /// Lässt die KI den Text umformulieren (nur auf Knopfdruck, ADR 0073). Das Modell behält die Sprache des Textes (<see cref="ReviseClosureTextRequest.Language"/>, sonst Deutsch),
    /// Zahlen dürfen nicht dazukommen und die Platzhalter müssen bleiben; sonst bleibt der Text, wie er ist. <c>(null, message)</c>: die KI ist nicht verfügbar oder die Fassung
    /// wurde verworfen.
    /// </summary>
    public async Task<(string? Text, string? Problem)> ReviseAsync(ReviseClosureTextRequest request, CancellationToken ct)
    {
        if (!reviser.Available)
        {
            return (null, "Umformulieren braucht die KI; ohne API-Schlüssel ist es nicht verfügbar.");
        }

        // Ein eigener Hinweis an die KI (Custom) gibt es hier nicht: die Knöpfe nennen die Richtung.
        if (request.Kind == ReplyRevisionKind.Custom || !Enum.IsDefined(request.Kind))
        {
            return (null, "Bitte eine der Vorgaben wählen (freundlicher, kürzer, förmlicher, persönlicher, mit Entschuldigung).");
        }

        var text = request.Text?.Trim() ?? "";
        if (text.Length is 0 or > ClosureModel.TextLength)
        {
            return (null, $"Bitte einen Text eingeben (höchstens {ClosureModel.TextLength} Zeichen).");
        }

        var revised = (await reviser.ReviseAsync(text, string.IsNullOrWhiteSpace(request.Language) ? German : GuestLanguage.Normalize(request.Language), request.Kind, null, ct)).Trim();
        if (!Numbers.OnlyFrom(text, revised) || ClosureText.Placeholders.Any(p => text.Contains(p, StringComparison.Ordinal) && !revised.Contains(p, StringComparison.Ordinal))
            || revised.Length > ClosureModel.TextLength)
        {
            return (null, "Die KI hat den Text so verändert, dass Angaben oder Platzhalter fehlen oder neu sind. Der Text bleibt, wie er war.");
        }

        return (revised, null);
    }

    // ---------- Wartezeit: jetzt senden, nicht senden ----------

    public const string NoticeTooLate = "Die Nachricht ist schon gesendet oder zurückgenommen.";

    /// <summary>Sofort in die Queue, oder für die Sendezeit geplant: die Wartezeit des Standorts lässt dem Team Zeit, ein Versehen zurückzunehmen (Ä95).</summary>
    private async Task DispatchAsync(ClosureNotice notice)
    {
        if (notice.ScheduledFor is { } at && at > time.GetUtcNow())
        {
            await bus.ScheduleAsync(new SendClosureNotice(notice.Id), at);
        }
        else
        {
            await bus.PublishAsync(new SendClosureNotice(notice.Id));
        }
    }

    /// <summary>„Jetzt senden“: die wartende Nachricht geht gleich raus. Die geplante Queue-Nachricht findet sie später schon gesendet vor und tut nichts.</summary>
    public async Task<(ClosureNoticeDto? Notice, NoticeChange Change)> SendNowAsync(Guid locationId, Guid closureId, Guid noticeId, CancellationToken ct)
    {
        var (notice, change) = await WaitingNoticeAsync(locationId, closureId, noticeId, ct);
        if (notice is null)
        {
            return (null, change);
        }

        notice.ScheduledFor = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new SendClosureNotice(notice.Id));
        return ((await ToDtosAsync([notice], ct))[0], NoticeChange.Done);
    }

    /// <summary>„Nicht senden“: die wartende Nachricht geht nie raus. Der Termin bleibt abgesagt; ein Rückgängig dafür ist eine eigene Funktion.</summary>
    public async Task<(ClosureNoticeDto? Notice, NoticeChange Change)> WithdrawAsync(Guid locationId, Guid closureId, Guid noticeId, CancellationToken ct)
    {
        var (notice, change) = await WaitingNoticeAsync(locationId, closureId, noticeId, ct);
        if (notice is null)
        {
            return (null, change);
        }

        notice.State = ClosureNoticeState.Cancelled;
        notice.Error = "Vom Team nicht gesendet.";
        await db.SaveChangesAsync(ct);
        return ((await ToDtosAsync([notice], ct))[0], NoticeChange.Done);
    }

    /// <summary>Die Nachricht, die noch auf ihre Sendezeit wartet; sonst <see cref="NoticeChange.NotFound"/> oder <see cref="NoticeChange.TooLate"/> (gesendet, zurückgenommen, schon in Zustellung).</summary>
    private async Task<(ClosureNotice? Notice, NoticeChange Change)> WaitingNoticeAsync(Guid locationId, Guid closureId, Guid noticeId, CancellationToken ct)
    {
        if (await closures.FindByIdAsync(locationId, closureId, ct) is null
            || await db.ClosureNotices.FirstOrDefaultAsync(n => n.Id == noticeId && n.ClosureId == closureId, ct) is not { } notice)
        {
            return (null, NoticeChange.NotFound);
        }

        var waiting = notice.State == ClosureNoticeState.Queued && notice.ScheduledFor > time.GetUtcNow();
        return waiting ? (notice, NoticeChange.Done) : (null, NoticeChange.TooLate);
    }

    // ---------- Nachrichten ----------

    /// <summary>Betreff und Text der Absage je Sprache: Deutsch ist die bearbeitete Vorlage, die anderen Sprachen die Standardvorlage. Je Sprache einmal berechnet.</summary>
    private sealed class Templates(Closure closure, Location location, DateOnly today, string germanSubject, string germanText)
    {
        private readonly Dictionary<string, (string Subject, string Text)> _cancellation = [];

        public Guid ClosureId => closure.Id;

        public string Period(string language) => ClosureText.Period(closure.FromDate, closure.FromTime, closure.UntilDate, closure.UntilTime, today, language);

        public (string Subject, string Text) Cancellation(string language)
        {
            if (!_cancellation.TryGetValue(language, out var template))
            {
                _cancellation[language] = template = language == German
                    ? (germanSubject, germanText)
                    : ClosureText.CancellationTemplate(closure.Kind, closure.GuestText, Period(language), location.Name, language);
            }

            return template;
        }

        public (string Subject, string Text) Waitlist(WaitlistEntry entry, string greeting, string treatment, string language) =>
            ClosureText.WaitlistNotice(closure.Kind, closure.GuestText, Period(language),
                ClosureText.Period(entry.From, null, entry.Until, null, today, language), greeting, treatment, location.Name, language);
    }

    /// <summary>Wie eine Anzahlung retourniert wurde: Betrag und Storno-Beleg, oder warum es nicht ging; beides leer, wenn es keine Anzahlung gab.</summary>
    private sealed record Refund(decimal? Amount, string? Currency, Guid? ReceiptId, string? Error, string? Way = null, bool Cash = false);

    /// <summary>
    /// Die Anzahlung eines abgesagten Termins geht zurück (Ä47): Storno-Beleg über die Kassa, Grund ist die Schließzeit. Eine noch nicht bezahlte Online-Anzahlung wird nur abgebrochen;
    /// eine bezahlte, deren Beleg noch nicht da ist, erstattet die Queue, sobald er da ist (<see cref="ReceiptOnlineDepositHandler"/>). Geht das Storno nicht (z. B. Kassa außer Betrieb),
    /// steht der Grund im Protokoll, die Nachricht nennt keine Rückzahlung und die Rezeption erstattet an der Kassa.
    /// </summary>
    private async Task<Refund> RefundAsync(Appointment appointment, string reason, string userId, CancellationToken ct)
    {
        var result = await depositReturns.ReturnAsync(appointment.Id, $"{reason}; Anzahlung retourniert", userId, ct);
        await deposits.RefundAfterCancellationAsync(appointment.Id, ct, RefundOnlineDepositHandler.BusinessReason);
        if (result.Value is { } returned)
        {
            return new Refund(returned.Amount, await CurrencyAsync(appointment.LocationId, ct), returned.VoidReceipt.Id, null, returned.Way, returned.Cash);
        }

        // „Keine offene Anzahlung“ ist der Normalfall: dann gibt es nichts zu retournieren.
        return result.Error is { Kind: PosErrorKind.NotFound } ? new Refund(null, null, null, null) : new Refund(null, null, null, result.Error!.Message);
    }

    private async Task<string> CurrencyAsync(Guid locationId, CancellationToken ct) =>
        await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.Currency).FirstAsync(ct);

    private ClosureNotice AppointmentNotice(CancelClosureAppointmentsRequest request, Appointment appointment, Location location, TimeZoneInfo zone, ReminderSettings settings,
        Templates texts, string? link, Refund refund, UserAccess access, string name)
    {
        var language = GuestLanguage.Resolve(appointment.Customer);
        var channel = ChannelFor(settings, appointment.Customer);
        var greeting = GuestName.Of(appointment.Customer, location.TimeZone).For(language);
        var when = ReminderTexts.When(appointment.Start, zone, language);
        var (subject, template) = texts.Cancellation(language);
        // Der Hinweis auf die KI gehört nur an Nachrichten, die die KI umformuliert hat: das ist die bearbeitete deutsche Vorlage.
        var aiWritten = request.AiWritten && language == German;
        var body = ClosureText.Fill(template, greeting, appointment.Treatment.Name, when);
        if (refund is { Amount: { } amount, Currency: { } currency })
        {
            body = ClosureText.WithBeforeClosing(body, ClosureText.RefundSentence(amount, currency, language, refund.Cash));
        }

        if (link is not null)
        {
            body += $"\n\n{ClosureText.RebookingSentence(link, language)}";
        }

        if (aiWritten)
        {
            body += $"\n\n{ReplyLanguages.Disclosure(language)}";
        }

        return new ClosureNotice
        {
            ClosureId = texts.ClosureId,
            AppointmentId = appointment.Id,
            Language = language,
            Channel = channel,
            State = StateOf(channel),
            Error = ErrorOf(channel),
            Subject = channel == ClosureChannel.Email ? ClosureText.Fill(subject, greeting, appointment.Treatment.Name, when) : null,
            Text = body,
            ScheduledFor = ScheduleOf(channel, appointment.Start, location),
            AiWritten = aiWritten,
            OfferedRebooking = request.OfferRebooking,
            RefundAmount = refund.Amount,
            RefundReceiptId = refund.ReceiptId,
            RefundWay = refund.Way,
            RefundError = refund.Error,
            SentByUserId = access.UserId,
            SentByName = name,
            CreatedAt = time.GetUtcNow(),
        };
    }

    private ClosureNotice WaitlistNotice(WaitlistEntry entry, Customer customer, string treatment, Location location, Templates texts, ReminderSettings settings, UserAccess access, string name)
    {
        var language = GuestLanguage.Normalize(entry.Language);
        var channel = ChannelFor(settings, customer);
        var (subject, body) = texts.Waitlist(entry, GuestName.Of(customer, location.TimeZone).For(language), treatment, language);
        return new ClosureNotice
        {
            ClosureId = texts.ClosureId,
            WaitlistEntryId = entry.Id,
            Language = language,
            Channel = channel,
            State = StateOf(channel),
            Error = ErrorOf(channel),
            Subject = channel == ClosureChannel.Email ? subject : null,
            Text = body,
            ScheduledFor = ScheduleOf(channel, null, location),
            SentByUserId = access.UserId,
            SentByName = name,
            CreatedAt = time.GetUtcNow(),
        };
    }

    private DateTimeOffset? ScheduleOf(ClosureChannel channel, DateTimeOffset? appointmentStart, Location location) =>
        channel == ClosureChannel.None ? null : GuestMessageTiming.SendAt(time.GetUtcNow(), appointmentStart, location.GuestMessageDelayMinutes);

    private static ClosureNoticeState StateOf(ClosureChannel channel) => channel == ClosureChannel.None ? ClosureNoticeState.Skipped : ClosureNoticeState.Queued;

    private static string? ErrorOf(ClosureChannel channel) => channel == ClosureChannel.None ? "Keine Mailadresse und keine Telefonnummer." : null;

    private async Task<ClosureNotice> AddNoticeAsync(ClosureNotice notice, CancellationToken ct)
    {
        db.ClosureNotices.Add(notice);
        await db.SaveChangesAsync(ct);
        return notice;
    }

    // ---------- Absage zurücknehmen (Ä95) ----------

    /// <summary>Stand der Nachricht zu einer Absage: wartet noch (geht beim Zurücknehmen nie raus), ist raus oder gibt es nicht; dazu, ob eine Anzahlung mit der Absage retourniert wurde.</summary>
    public async Task<AppointmentRestoration> RestorationAsync(Guid appointmentId, CancellationToken ct)
    {
        // Nur eine Absage des Betriebs hat eine Nachricht, die zur aktuellen Stornierung gehört; eine von Hand stornierte Terminzeile (auch nach einem früheren Zurücknehmen) hat keine.
        if (!await db.Appointments.AsNoTracking().AnyAsync(a => a.Id == appointmentId && a.CancelledByBusiness, ct))
        {
            return new AppointmentRestoration(CancellationNoticeState.None, false);
        }

        var notices = await db.ClosureNotices.AsNoTracking().Where(n => n.AppointmentId == appointmentId && !n.IsCorrection).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        var now = time.GetUtcNow();
        var state = notices.FirstOrDefault() switch
        {
            { State: ClosureNoticeState.Queued } n when n.ScheduledFor > now => CancellationNoticeState.Waiting,
            { State: ClosureNoticeState.Queued or ClosureNoticeState.Sent } => CancellationNoticeState.Sent,
            _ => CancellationNoticeState.None,
        };
        return new AppointmentRestoration(state, notices.Any(n => n.RefundAmount is not null));
    }

    /// <summary>
    /// Der Termin ist wieder gebucht: eine wartende Absage geht nie raus. Ist sie schon raus und das Team wünscht es, geht eine Korrektur sofort in die Queue
    /// (gleicher Kanal und gleiche Sprache wie die Absage).
    /// </summary>
    public async Task AfterRestoreAsync(AppointmentDto restored, CancellationNoticeState state, bool sendCorrection, CancellationToken ct)
    {
        if (state == CancellationNoticeState.None)
        {
            return;
        }

        var notices = await db.ClosureNotices.Where(n => n.AppointmentId == restored.Id && !n.IsCorrection).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        var now = time.GetUtcNow();
        foreach (var waiting in notices.Where(n => n.State == ClosureNoticeState.Queued && n.ScheduledFor > now))
        {
            waiting.State = ClosureNoticeState.Cancelled;
            waiting.Error = "Termin wieder gebucht, Nachricht nicht gesendet.";
        }

        // „Gesendet“ schließt eine fällige, noch nicht zugestellte Nachricht ein: der Gast bekommt die Absage dann noch, die Korrektur folgt ihr.
        var sent = notices.FirstOrDefault(n => n.State is ClosureNoticeState.Sent or ClosureNoticeState.Queued);
        var correction = sendCorrection && state == CancellationNoticeState.Sent && sent is { Channel: not ClosureChannel.None } ? await CorrectionNoticeAsync(sent, restored, now, ct) : null;
        await db.SaveChangesAsync(ct);
        if (correction is not null)
        {
            await bus.PublishAsync(new SendClosureNotice(correction.Id));
        }
    }

    private async Task<ClosureNotice> CorrectionNoticeAsync(ClosureNotice sent, AppointmentDto restored, DateTimeOffset now, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment).FirstAsync(a => a.Id == restored.Id, ct);
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var zone = LocalTime.Zone(location.TimeZone);
        var greeting = GuestName.Of(appointment.Customer, location.TimeZone).For(sent.Language);
        var (subject, text) = ClosureText.Correction(greeting, appointment.Treatment.Name, ReminderTexts.When(appointment.Start, zone, sent.Language), location.Name, sent.Language);
        var notice = new ClosureNotice
        {
            ClosureId = sent.ClosureId,
            AppointmentId = appointment.Id,
            Language = sent.Language,
            Channel = sent.Channel,
            Subject = sent.Channel == ClosureChannel.Email ? subject : null,
            Text = text,
            ScheduledFor = now,
            IsCorrection = true,
            SentByUserId = sent.SentByUserId,
            SentByName = sent.SentByName,
            CreatedAt = now,
        };
        db.ClosureNotices.Add(notice);
        return notice;
    }

    // ---------- Hilfen ----------

    private string RebookingLink() =>
        $"{onlineOptions.Value.PublicUrl.TrimEnd('/')}/buchen/?{OnlineTenant.Query}={tenants.TenantId}#/";

    /// <summary>
    /// Der Kanal für die Absage: der bevorzugte der Erinnerungen, sonst der, für den Kontaktdaten da sind (WhatsApp braucht eine Vorlage und gilt hier als SMS).
    /// <see cref="ClosureChannel.None"/>, wenn der Gast weder Mailadresse noch Telefonnummer hat.
    /// </summary>
    private ClosureChannel ChannelFor(ReminderSettings settings, Customer customer)
    {
        var email = !string.IsNullOrWhiteSpace(customer.Email);
        var sms = !string.IsNullOrEmpty(PhoneNumbers.Normalize(customer.Phone ?? "")) && !string.IsNullOrWhiteSpace(textChannels.Value.SmsProvider);
        return settings.Channel switch
        {
            ReminderChannel.WhatsApp or ReminderChannel.Sms when sms => ClosureChannel.Sms,
            _ when email => ClosureChannel.Email,
            _ when sms => ClosureChannel.Sms,
            _ => ClosureChannel.None,
        };
    }

    /// <summary>
    /// Die Anzahlung je Termin, die mit der Absage zurückgeht: die offenen Anzahlungen der Kassa (bezahlt, nicht verrechnet, Beleg nicht storniert) und die online bezahlten, deren
    /// Anzahlungsbeleg noch entsteht.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> DepositAmountsAsync(IReadOnlyCollection<Guid> appointmentIds, CancellationToken ct)
    {
        var booked = await db.AppointmentDeposits.AsNoTracking()
            .Where(d => appointmentIds.Contains(d.AppointmentId) && d.VoidedAt == null && d.SettledOnReceiptId == null)
            .GroupBy(d => d.AppointmentId).Select(g => new { Id = g.Key, Amount = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(g => g.Id, g => g.Amount, ct);
        var paid = await online.Deposits.AsNoTracking().Where(d => appointmentIds.Contains(d.AppointmentId) && d.Status == OnlineDepositStatus.Paid)
            .ToDictionaryAsync(d => d.AppointmentId, d => d.Amount, ct);
        foreach (var (id, amount) in paid)
        {
            booked.TryAdd(id, amount);
        }

        return booked;
    }

    private async Task<List<ClosureWaitlistDto>> WaitlistDtosAsync(Closure closure, TimeZoneInfo zone, DateTimeOffset now, ReminderSettings settings, CancellationToken ct)
    {
        var entries = await ClosureService.OpenWaitlistAsync(db, online, closure, zone, now, ct);
        var customerIds = entries.Select(e => e.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var treatmentIds = entries.Select(e => e.TreatmentId).Distinct().ToList();
        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        return [.. entries.Where(e => customers.ContainsKey(e.CustomerId)).Select(e => new ClosureWaitlistDto(e.Id, customers[e.CustomerId].InHouseName,
            treatments.GetValueOrDefault(e.TreatmentId, ""), e.From, e.Until, e.DayPart, ChannelFor(settings, customers[e.CustomerId]), GuestLanguage.Normalize(e.Language)))];
    }

    private async Task<List<ClosureNoticeDto>> ToDtosAsync(IReadOnlyCollection<ClosureNotice> notices, CancellationToken ct)
    {
        var appointmentIds = notices.Where(n => n.AppointmentId is not null).Select(n => n.AppointmentId!.Value).ToList();
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment).Where(a => appointmentIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var entryIds = notices.Where(n => n.WaitlistEntryId is not null).Select(n => n.WaitlistEntryId!.Value).ToList();
        var entries = await online.WaitlistEntries.AsNoTracking().Where(w => entryIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var customerIds = entries.Values.Select(e => e.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var treatmentIds = entries.Values.Select(e => e.TreatmentId).Distinct().ToList();
        var treatments = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        return [.. notices.Select(n =>
        {
            if (n.AppointmentId is { } id)
            {
                var a = appointments[id];
                return new ClosureNoticeDto(n.Id, n.ClosureId, n.AppointmentId, a.Customer.InHouseName, a.Treatment.Name, a.Start, n.Channel, n.State, n.Error, n.Subject, n.Text,
                    n.AiWritten, n.OfferedRebooking, n.SentByName, n.CreatedAt, n.SentAt, n.Language, null, null, null, n.RefundAmount, n.RefundReceiptId, n.RefundError, n.RefundWay, n.ScheduledFor);
            }

            var entry = entries[n.WaitlistEntryId!.Value];
            return new ClosureNoticeDto(n.Id, n.ClosureId, null, customers.TryGetValue(entry.CustomerId, out var customer) ? customer.InHouseName : "", treatments.GetValueOrDefault(entry.TreatmentId, ""),
                null, n.Channel, n.State, n.Error, n.Subject, n.Text, n.AiWritten, n.OfferedRebooking, n.SentByName, n.CreatedAt, n.SentAt, n.Language, entry.Id, entry.From, entry.Until, ScheduledFor: n.ScheduledFor);
        })];
    }
}

/// <summary>Ausgang von „Jetzt senden“ und „Nicht senden“: erledigt, Nachricht nicht gefunden oder dafür zu spät (schon gesendet oder zurückgenommen).</summary>
public enum NoticeChange
{
    Done,
    NotFound,
    TooLate,
}

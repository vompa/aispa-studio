using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>Einstellungen der Terminannahme (Abschnitt <c>MailIntake</c>).</summary>
public sealed class MailIntakeOptions
{
    /// <summary>Spa (Mandant), dem das konfigurierte Postfach gehört. Leer: das einzige Spa, solange es nur eines gibt.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Ist die Wunschzeit genau angegeben und frei, wird der Termin gleich eingetragen; sonst nur vorgeschlagen.</summary>
    public bool AutoBook { get; set; } = true;

    /// <summary>Antworten ohne Prüfung durch die Rezeption senden. Aus, bis das Team den Antworten vertraut.</summary>
    public bool AutoSend { get; set; }

    public int PollSeconds { get; set; } = 30;

    /// <summary>Name des Spas in der Grußformel.</summary>
    public string SpaName { get; set; } = "Ihr Spa-Team";

    public int MaxProposals { get; set; } = 3;

    /// <summary>So viele Tage ab dem Wunschtermin wird nach Alternativen gesucht.</summary>
    public int SearchDays { get; set; } = 14;

    /// <summary>Frühester Termin, den die Terminannahme vergibt, gerechnet ab Eingang der Mail.</summary>
    public int MinLeadMinutes { get; set; } = 60;
}

/// <summary>
/// Verarbeitet eingegangene Mails: versteht sie mit der KI, sucht freie Zeiten im Kalender, trägt ein oder schlägt vor
/// und entwirft die Antwort. Welche Absender Buchungsplattformen sind, bestimmen die Absender-Regeln des Spas (ADR 0062); Treatwell-Buchungen werden
/// gelesen (KI mit JSON-Schema, sonst Regeln) und sofort als Termin eingetragen.
/// Läuft in der Queue (<see cref="MailIntakeHandler"/>), nie im HTTP-Aufruf.
/// </summary>
internal sealed partial class MailIntakeProcessor(
    AispaDbContext db,
    BookingService booking,
    CustomerMatcher matcher,
    SenderRuleStore senderRules,
    TreatwellReader reader,
    IBookingAssistant assistant,
    IQuestionAnswerer answerer,
    IMessageTranslator translator,
    IReplyReviser reviser,
    SpaFacts facts,
    SpaMailSender sender,
    ITextMessageSender texts,
    IMessageBus bus,
    IOptions<MailIntakeOptions> options,
    TimeProvider time,
    ILogger<MailIntakeProcessor> log)
{
    private const string UniqueViolation = "23505";
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    private MailIntakeOptions Options => options.Value;

    /// <summary>Absender-Regel „nur als Vorschlag“ für eine andere Plattform: die Terminannahme liest die Mail, trägt aber nichts selbst ein.</summary>
    private bool _suggestOnly;

    public async Task ProcessAsync(Guid mailId, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is null || mail.Status != InboundMailStatus.Received)
        {
            return;
        }

        // Was die Terminannahme bucht, verschiebt oder storniert, steht im Verlauf mit dem Kanal der Nachricht (ADR 0082).
        using var actor = ChangeActor.Use(ChangeChannelOf(mail.Channel));
        try
        {
            // Absender-Regeln gelten für Mails; SMS, WhatsApp und Anrufe haben keinen Absender, den eine Plattform nutzt.
            var rule = mail.Channel is BookingChannel.Email or BookingChannel.Treatwell ? await senderRules.FindAsync(mail.From, ct) : null;
            mail.SenderRulePattern = rule?.Pattern;
            if (rule is { Handling: SenderHandling.Ignore })
            {
                mail.Status = InboundMailStatus.Ignored;
                mail.Note = $"Absender-Regel „{rule.Pattern}“: ignorieren. Die Mail bleibt sichtbar, wird aber nicht von der KI bearbeitet; es gibt keinen Antwortentwurf.";
            }
            else if (mail.Channel == BookingChannel.Treatwell || rule is { Platform: SenderPlatform.Treatwell })
            {
                // Auch bei erneuter Verarbeitung (Wiederholen, Zuordnung des Kunden): der Kanal ist dann schon gesetzt.
                await ProcessTreatwellAsync(mail, rule, ct);
            }
            else if (rule is { Platform: SenderPlatform.Other })
            {
                await ProcessOtherPlatformAsync(mail, rule, ct);
            }
            else
            {
                await ProcessRequestAsync(mail, ct);
            }

            mail.ProcessedAt = time.GetUtcNow();
            await SaveMailAsync(mail, ct);
            await QueueTranslationAsync(mail, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Mail {MailId} konnte nicht verarbeitet werden.", mailId);
            db.ChangeTracker.Clear();
            await db.InboundMails.Where(m => m.Id == mailId).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, InboundMailStatus.Failed)
                .SetProperty(m => m.Note, Truncate($"Verarbeitung fehlgeschlagen: {ex.Message}", 2000))
                .SetProperty(m => m.ProcessedAt, time.GetUtcNow()), ct);
            return;
        }

        if (Options.AutoSend && mail.ReplyStatus == ReplyStatus.Draft
            && mail.Status is InboundMailStatus.Booked or InboundMailStatus.Proposed)
        {
            await QueueSendAsync(mail, ct);
        }
    }

    /// <summary>
    /// Die Rezeption hat den Kunden bestätigt: Hinweis zu Absage oder Verschiebung mit dem nächsten Termin des Kunden, Vorschläge
    /// freigeben (aus „Wer ist das?“ wird „Vorschlag“) und „Nicht mehr buchen“ beachten. Bis hierhin hat die Nachricht nichts aus der
    /// Kundenkarte erfahren.
    /// </summary>
    public async Task ApplyCustomerAsync(InboundMail mail, CancellationToken ct)
    {
        var customer = mail is { CustomerConfirmed: true, CustomerId: { } id }
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct)
            : null;
        if (customer is not null)
        {
            mail.Note = WithoutWhoIsThis(mail.Note);
            if (mail.Kind is InboundMailKind.Cancellation or InboundMailKind.Reschedule)
            {
                mail.AppointmentId = null;
                mail.Note = await ForwardNoteAsync(mail, customer, ct);
            }
            else if (mail is { Kind: InboundMailKind.BookingRequest, AppointmentId: null })
            {
                if ((await CustomerRules.LoadAsync(db, customer.Id, ct)).DoNotBook)
                {
                    mail.Status = InboundMailStatus.NeedsReview;
                    mail.Note = DoNotBookNote;
                    mail.Proposals = [];
                    await DraftAsync(mail, ReplyCase.Forwarded, await GuestNameAsync(mail, customer, ct), null, null, null, [], [], ct);
                }
                else if (mail is { Status: InboundMailStatus.NeedsReview, Proposals.Count: > 0 })
                {
                    mail.Status = InboundMailStatus.Proposed;
                }
            }
        }

        await SaveMailAsync(mail, ct);
    }

    /// <summary>
    /// Zuordnung gelöst: der Kunde ist ausgeschlossen, die Kandidaten werden ohne ihn neu gesucht, und die Nachricht wartet wieder auf eine
    /// Entscheidung der Rezeption.
    /// </summary>
    public async Task ReleaseCustomerAsync(InboundMail mail, CancellationToken ct)
    {
        var released = mail.CustomerId;
        mail.CustomerId = null;
        mail.CustomerConfirmed = false;
        var match = await matcher.MatchAsync(MatchRequest(mail) with { ExcludeCustomerId = released }, ct);
        mail.CustomerCandidates = match.Certain is null ? [.. match.Candidates] : [.. match.Everyone];
        if (mail.Kind is InboundMailKind.Cancellation or InboundMailKind.Reschedule)
        {
            mail.AppointmentId = null;
            mail.Note = await ForwardNoteAsync(mail, null, ct);
        }
        else if (mail is { Kind: InboundMailKind.BookingRequest, AppointmentId: null } && mail.CustomerCandidates.Count > 0)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = WhoIsThisNote;
        }

        await SaveMailAsync(mail, ct);
    }

    private static string? WithoutWhoIsThis(string? note) =>
        note?.Replace($" {WhoIsThisNote}", "", StringComparison.Ordinal).Replace(WhoIsThisNote, "", StringComparison.Ordinal).Trim() is { Length: > 0 } rest ? rest : null;

    internal const string DoNotBookNote = "Beim Gast ist „Nicht mehr buchen“ gesetzt. Die Terminannahme bucht nicht und schlägt nichts vor; bitte selbst entscheiden.";

    /// <summary>Nach dem Buchen eines Vorschlags oder dem Storno einer Absage durch die Rezeption: Antwort neu entwerfen.</summary>
    public async Task RedraftAsync(Guid mailId, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail?.AppointmentId is not { } appointmentId || mail.ReplyStatus is ReplyStatus.Sending or ReplyStatus.Sent)
        {
            return;
        }

        if (!mail.CustomerConfirmed)
        {
            // Ohne bestätigte Zuordnung kein Entwurf, der einen Termin aus der Kundenkarte nennt (ADR 0061).
            return;
        }

        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .FirstAsync(a => a.Id == appointmentId, ct);
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var zone = LocalTime.Zone(location.TimeZone);
        var replyCase = mail.Status == InboundMailStatus.Cancelled ? ReplyCase.Cancelled
            : appointment.HeldSince is not null ? ReplyCase.Held
            : ReplyCase.Booked;
        await DraftAsync(mail, replyCase, await GuestNameAsync(mail, appointment.Customer, ct), appointment.Treatment.Name, location.Name,
            TimeZoneInfo.ConvertTime(appointment.Start, zone).DateTime, [], [], ct);
        await SaveMailAsync(mail, ct);
        await QueueTranslationAsync(mail, ct);
    }

    /// <summary>Nachricht in einer anderen Sprache als Deutsch: Übersetzung für die Rezeption anstoßen (Nachricht und Entwurf).</summary>
    public async Task QueueTranslationAsync(InboundMail mail, CancellationToken ct)
    {
        if (!IsForeign(mail))
        {
            return;
        }

        if (!translator.Available)
        {
            mail.TranslationState = MessageTranslationState.Unavailable;
            await SaveMailAsync(mail, ct);
            return;
        }

        if (mail.TranslationState == MessageTranslationState.None)
        {
            mail.TranslationState = MessageTranslationState.Pending;
            await SaveMailAsync(mail, ct);
        }

        await bus.PublishAsync(new MailTranslationRequested(mail.Id));
    }

    public static bool IsForeign(InboundMail mail) => mail.Language is { } language && language != "de";

    /// <summary>
    /// Übersetzt die Nachricht (einmal) und den aktuellen Entwurf (nach jeder Änderung) ins Deutsche. Weichen Zahlen zwischen
    /// Original und Übersetzung ab, gilt die Übersetzung als unsicher und der Posteingang zeigt das Original.
    /// </summary>
    public async Task TranslateAsync(Guid mailId, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is null || !IsForeign(mail) || !translator.Available)
        {
            return;
        }

        try
        {
            if (mail.TranslatedBody is null)
            {
                var translated = await translator.ToGermanAsync(mail.Subject, mail.Body, ct);
                mail.TranslatedSubject = Truncate(translated.Subject, 1000);
                mail.TranslatedBody = translated.Body;
                mail.TranslationState = Numbers.Same($"{mail.Subject}\n{mail.Body}", $"{translated.Subject}\n{translated.Body}")
                    ? MessageTranslationState.Done
                    : MessageTranslationState.Mismatch;
            }

            if (mail.ReplyBody is { Length: > 0 } reply && mail.ReplyTranslationOf != Fingerprint(reply))
            {
                var translated = await translator.ToGermanAsync(mail.ReplySubject ?? "", reply, ct);
                mail.ReplyTranslation = translated.Body;
                mail.ReplyTranslationOf = Fingerprint(reply);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Mail {MailId} konnte nicht übersetzt werden.", mailId);
            if (mail.TranslatedBody is null)
            {
                mail.TranslationState = MessageTranslationState.Unavailable;
            }
        }

        await SaveMailAsync(mail, ct);
    }

    /// <summary>
    /// KI-Überarbeitung des Entwurfs. Enthält sie eine Zahl, die nicht schon im Entwurf steht, wird sie verworfen (keine erfundenen
    /// Zeiten oder Preise). Die vorige Fassung kommt in den Verlauf und lässt sich zurückholen.
    /// </summary>
    public async Task ReviseAsync(Guid mailId, ReplyRevisionKind kind, string? instruction, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is null)
        {
            return;
        }

        var changed = false;
        if (mail is { ReplyStatus: ReplyStatus.Draft, ReplyBody: { Length: > 0 } draft })
        {
            try
            {
                var revised = await reviser.ReviseAsync(draft, mail.Language ?? "de", kind, instruction, ct);
                if (revised.Length == 0 || !Numbers.OnlyFrom(draft, revised))
                {
                    mail.ReplyNotice = "Überarbeitung verworfen: Die KI hat eine Zahl (Datum, Uhrzeit, Preis) geändert oder ergänzt. Der Entwurf ist unverändert.";
                }
                else
                {
                    mail.ReplyHistory = [.. mail.ReplyHistory.TakeLast(MaxReplyHistory - 1), draft];
                    mail.ReplyBody = revised;
                    mail.ReplyNotice = null;
                    changed = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Entwurf zu Mail {MailId} konnte nicht überarbeitet werden.", mailId);
                mail.ReplyNotice = "Überarbeitung fehlgeschlagen. Der Entwurf ist unverändert.";
            }
        }

        mail.ReplyRevising = false;
        await SaveMailAsync(mail, ct);
        if (changed)
        {
            await QueueTranslationAsync(mail, ct);
        }
    }

    /// <summary>So viele frühere Fassungen des Entwurfs bleiben zum Zurückholen.</summary>
    public const int MaxReplyHistory = 10;

    public static string Fingerprint(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Antwort senden; der KI-Hinweis wird hier angehängt und lässt sich so nicht weglassen.</summary>
    public async Task SendAsync(Guid mailId, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is not { ReplyStatus: ReplyStatus.Sending, ReplyTo: { } to })
        {
            return;
        }

        if (IsShortMessage(mail.Channel))
        {
            // Anrufe werden per SMS beantwortet. Gehört die angeschriebene Nummer dem Spa, kommt die Antwort von dort.
            var channel = mail.Channel == BookingChannel.WhatsApp ? BookingChannel.WhatsApp : BookingChannel.Sms;
            var own = await IntakeRoutes.FindAsync(db, channel, mail.To, ct);
            await texts.SendAsync(new OutgoingText(
                channel,
                to,
                $"{mail.ReplyBody}\n{ReplyLanguages.ShortDisclosure(mail.Language ?? "de")}",
                From: own?.Route.Address,
                SenderId: own?.Route.Channel == channel ? own?.Route.SenderId : null), ct);
        }
        else
        {
            await sender.SendAsync(new OutgoingMail(
                to,
                mail.ReplySubject ?? $"Re: {mail.Subject}",
                $"{mail.ReplyBody}\n\n-- \n{ReplyLanguages.Disclosure(mail.Language ?? "de")}",
                InReplyTo: mail.MessageId), ct);
        }

        mail.ReplyStatus = ReplyStatus.Sent;
        mail.RepliedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task QueueSendAsync(InboundMail mail, CancellationToken ct)
    {
        mail.ReplyStatus = ReplyStatus.Sending;
        if (mail.ReplyNotice == UnsentNotice)
        {
            mail.ReplyNotice = null;
        }

        await SaveMailAsync(mail, ct);
        await bus.PublishAsync(new ReplySendRequested(mail.Id));
    }

    /// <summary>Hinweis am Entwurf, wenn der Versand endgültig gescheitert ist.</summary>
    public const string UnsentNotice = "Nicht gesendet: Der Versand ist mehrmals gescheitert. Der Entwurf ist unverändert, Sie können ihn erneut senden.";

    /// <summary>
    /// Versand endgültig gescheitert (Rollen Api und Worker, <see cref="MailIntakeFailurePolicy"/>): Die Antwort bleibt nicht bei
    /// „Wird gesendet“ hängen, sondern wird wieder Entwurf mit Hinweis, damit das Team erneut senden kann.
    /// </summary>
    public async Task ReturnUnsentReplyAsync(Guid mailId, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is not { ReplyStatus: ReplyStatus.Sending })
        {
            return;
        }

        mail.ReplyStatus = ReplyStatus.Draft;
        mail.ReplyNotice = UnsentNotice;
        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessRequestAsync(InboundMail mail, CancellationToken ct)
    {
        mail.ReplyTo = mail.From;

        // Antwort auf einen gesendeten Vorschlag („passt nicht, lieber am Abend“): mit der Anfrage verknüpfen, deren Wunsch
        // weiterführen und die schon vorgeschlagenen Zeiten auslassen.
        var previous = await FindPreviousRequestAsync(mail, ct);
        if (previous is not null)
        {
            mail.PreviousMailId = previous.Id;
            mail.RejectedStarts = [.. previous.RejectedStarts.Union(previous.Proposals.Select(p => p.Start))];
            mail.ProposalRound = previous.ProposalRound;
            await db.InboundMails.Where(m => m.Id == previous.Id && m.Status == InboundMailStatus.Proposed).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, InboundMailStatus.Ignored)
                .SetProperty(m => m.Note, "Der Gast hat auf den Vorschlag geantwortet; weiter in seiner neuen Nachricht."), ct);

            if (await AnswerToHoldAsync(mail, previous, ct))
            {
                return;
            }
        }

        // „Re: Massage“ wiederholt nur den alten Betreff und soll den Wunsch von vorher nicht überschreiben.
        var subject = previous is not null && BareSubject(mail.Subject).Equals(BareSubject(previous.Subject), StringComparison.OrdinalIgnoreCase)
            ? ""
            : mail.Subject;
        await ProposeAsync(mail, subject, mail.Body, previous is null ? null : Earlier(previous), previous, fromReception: false,
            mail.ReceivedAt, ct);
    }

    /// <summary>
    /// Antwort des Gastes auf eine vorgemerkte Zeit (ADR 0083): keine neuen Zeiten suchen, sondern die Nachricht mit der Vormerkung zur Entscheidung
    /// an die Rezeption geben (bestätigen oder freigeben); die Erinnerung „Gast hat sich nicht gemeldet“ entfällt.
    /// </summary>
    private async Task<bool> AnswerToHoldAsync(InboundMail mail, InboundMail previous, CancellationToken ct)
    {
        if (previous.AppointmentId is not { } heldId
            || !await db.Appointments.AsNoTracking().AnyAsync(a => a.Id == heldId && a.HeldSince != null && a.Status == AppointmentStatus.Booked, ct))
        {
            return false;
        }

        mail.AppointmentId = heldId;
        mail.HeldStart = previous.HeldStart;
        mail.CustomerId = previous.CustomerId;
        mail.CustomerConfirmed = previous.CustomerConfirmed;
        mail.LocationId = previous.LocationId;
        mail.TreatmentId = previous.TreatmentId;
        mail.Language ??= previous.Language;
        mail.Status = InboundMailStatus.NeedsReview;
        mail.Summary = "Antwort auf die vorgemerkte Zeit.";
        await booking.NoteHoldAnsweredAsync(heldId, mail.ReceivedAt, ct);
        await SaveMailAsync(mail, ct);
        await QueueTranslationAsync(mail, ct);
        return true;
    }

    /// <summary>Betreff ohne „Re:“, „AW:“, „Fwd:“ und Ähnliches.</summary>
    internal static string BareSubject(string subject) => ReplyPrefix().Replace(subject, "").Trim();

    [GeneratedRegex(@"^(\s*(re|aw|wg|fw|fwd|r|rif|tr|sv)\s*(\[\d+\])?\s*:\s*)+", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefix();

    /// <summary>Wie lange nach dem Senden eines Vorschlags eine Nachricht desselben Absenders als Antwort darauf gilt.</summary>
    private static readonly TimeSpan AnswerWindow = TimeSpan.FromDays(14);

    private async Task<InboundMail?> FindPreviousRequestAsync(InboundMail mail, CancellationToken ct)
    {
        if (mail.PreviousMailId is { } known)
        {
            // Erneute Verarbeitung (retry): die Verknüpfung steht schon.
            return await db.InboundMails.AsNoTracking().FirstOrDefaultAsync(m => m.Id == known, ct);
        }

        var from = mail.From.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        var since = mail.ReceivedAt - AnswerWindow;
        return await db.InboundMails.AsNoTracking()
            .Where(m => m.Id != mail.Id && EF.Functions.ILike(m.From, from) && m.Status == InboundMailStatus.Proposed
                && m.ReplyStatus == ReplyStatus.Sent && m.RepliedAt >= since && m.RepliedAt <= mail.ReceivedAt)
            .OrderByDescending(m => m.RepliedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Der bisherige Wunsch als Zusammenhang für die KI: frühere Nachricht und Hinweise der Rezeption.</summary>
    private static string Earlier(InboundMail mail) =>
        $"Betreff: {mail.Subject}\n{mail.Body}" + (mail.WishNote is null ? "" : $"\nHinweis der Rezeption: {mail.WishNote}");

    /// <summary>
    /// „Neu vorschlagen“ der Rezeption: die gezeigten Zeiten gelten als abgelehnt, ein Hinweis ergänzt den Wunsch. Gebucht wird
    /// nie automatisch, die Rezeption entscheidet.
    /// </summary>
    public async Task ReproposeAsync(Guid mailId, string? hint, CancellationToken ct)
    {
        var mail = await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct);
        if (mail is null || !mail.Reproposing)
        {
            return;
        }

        using var actor = ChangeActor.Use(ChangeChannelOf(mail.Channel));
        try
        {
            mail.RejectedStarts = [.. mail.RejectedStarts.Union(mail.Proposals.Select(p => p.Start))];
            mail.Proposals = [];
            if (!string.IsNullOrWhiteSpace(hint))
            {
                mail.WishNote = Truncate(mail.WishNote is null ? hint.Trim() : $"{mail.WishNote}; {hint.Trim()}", 1000);
            }

            var previous = mail.PreviousMailId is { } id ? await db.InboundMails.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct) : null;
            var earlier = (previous is null ? "" : Earlier(previous) + "\n") + $"Betreff: {mail.Subject}\n{mail.Body}";
            if (mail.WishNote is { } note)
            {
                await ProposeAsync(mail, "Hinweis der Rezeption", note, earlier, previous, fromReception: true, time.GetUtcNow(), ct);
            }
            else
            {
                await ProposeAsync(mail, mail.Subject, mail.Body, previous is null ? null : Earlier(previous), previous, fromReception: true,
                    time.GetUtcNow(), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Neu vorschlagen für Mail {MailId} fehlgeschlagen.", mailId);
            db.ChangeTracker.Clear();
            mail = await db.InboundMails.FirstAsync(m => m.Id == mailId, ct);
            mail.Note = Truncate($"Neu vorschlagen fehlgeschlagen: {ex.Message}", 2000);
        }

        mail.Reproposing = false;
        await SaveMailAsync(mail, ct);
        await QueueTranslationAsync(mail, ct);
    }

    /// <summary>
    /// Wunsch verstehen und Zeiten vorschlagen (oder buchen). <paramref name="earlier"/> ist der bisherige Wunsch, den der Text
    /// ergänzt; <paramref name="previous"/> die Anfrage, deren Behandlung, Standort und Gast gelten, wenn der Text sie nicht nennt.
    /// </summary>
    private async Task ProposeAsync(InboundMail mail, string subject, string body, string? earlier, InboundMail? previous,
        bool fromReception, DateTimeOffset now, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        var treatments = await db.Treatments.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);

        var analysis = await assistant.AnalyzeAsync(new MailAnalysisRequest(
            mail.From, mail.FromName, subject, body, now,
            locations.FirstOrDefault()?.TimeZone ?? "Europe/Vienna",
            [.. treatments.Select(t => new NamedOption(t.Id, t.Name))],
            [.. locations.Select(l => new NamedOption(l.Id, l.Name))],
            [.. employees.Select(e => new NamedOption(e.Id, EmployeeNames.Recognizable(e.DisplayName, e.PreferredName)))],
            earlier), ct);

        // Nur Ids übernehmen, die es gibt: die KI darf nichts erfinden.
        var context = fromReception ? mail : previous;
        var treatment = treatments.FirstOrDefault(t => t.Id == analysis.TreatmentId) ?? treatments.FirstOrDefault(t => t.Id == context?.TreatmentId);
        var location = locations.FirstOrDefault(l => l.Id == analysis.LocationId) ?? locations.FirstOrDefault(l => l.Id == context?.LocationId)
            ?? (locations.Count == 1 ? locations[0] : null);
        var employeeId = employees.Any(e => e.Id == analysis.EmployeeId) ? analysis.EmployeeId : null;
        var intent = context is not null && analysis.Intent is MailIntent.Other ? MailIntent.BookingRequest
            : fromReception ? MailIntent.BookingRequest
            : analysis.Intent;

        mail.Summary = Truncate(analysis.Summary, 2000);
        if (!fromReception || mail.Language is null)
        {
            // Ein Hinweis der Rezeption ist deutsch und ohne Signatur: Sprache und Name des Gasts bleiben, wie erkannt.
            mail.Language = GuestLanguage.Normalize(analysis.Language);
            mail.Kind = intent switch
            {
                MailIntent.BookingRequest => InboundMailKind.BookingRequest,
                MailIntent.Cancellation => InboundMailKind.Cancellation,
                MailIntent.Reschedule => InboundMailKind.Reschedule,
                MailIntent.Question => InboundMailKind.Question,
                _ => InboundMailKind.Other,
            };
            mail.GuestFirstName = Truncate(WithoutTitles(analysis.FirstName), 100) ?? previous?.GuestFirstName;
            mail.GuestLastName = Truncate(WithoutTitles(analysis.LastName), 100) ?? previous?.GuestLastName;
            mail.GuestPhone = Truncate(analysis.Phone, 50) ?? previous?.GuestPhone;
            mail.SuggestedSalutation = analysis.Salutation ?? previous?.SuggestedSalutation;
            mail.SuggestedTitlesBefore = Truncate(analysis.TitlesBefore ?? PersonNames.LeadingTitles(mail.FromName), 100) ?? previous?.SuggestedTitlesBefore;
        }

        mail.TreatmentId = treatment?.Id;
        mail.LocationId = location?.Id;

        var customer = await ResolveCustomerAsync(mail, previous, fromReception, ct);
        var guest = await GuestNameAsync(mail, customer, ct);
        var undecided = customer is null && mail.CustomerCandidates.Count > 0;

        // Nennt der Gast niemanden, gilt seine bevorzugte Person; ist sie nicht frei, sucht die Terminannahme bei allen.
        if (employeeId is null && customer?.PreferredEmployeeId is { } preferred && employees.Any(e => e.Id == preferred))
        {
            employeeId = preferred;
        }

        if (intent != MailIntent.BookingRequest)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = await ForwardNoteAsync(mail, customer, ct);
            await DraftAsync(mail, ReplyCase.Forwarded, guest, null, null, null, [], [], ct);
            if (mail.Kind == InboundMailKind.Question)
            {
                await SuggestAnswerAsync(mail, guest, ct);
            }

            return;
        }

        // Kennzeichen: bei „Nicht mehr buchen“ entscheidet die Rezeption; ausgeschlossene Personen schlägt die Terminannahme nicht vor.
        var rules = await CustomerRules.LoadAsync(db, customer?.Id, ct);
        if (rules.DoNotBook)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = DoNotBookNote;
            await DraftAsync(mail, ReplyCase.Forwarded, guest, null, null, null, [], [], ct);
            return;
        }

        if (employeeId is { } excluded && rules.ExcludedEmployees.Contains(excluded))
        {
            employeeId = null;
        }

        var missing = new List<string>();
        if (treatment is null)
        {
            missing.Add(MissingText(mail.Language ?? "de", "treatment", treatments.Select(t => t.Name)));
        }

        if (location is null)
        {
            missing.Add(MissingText(mail.Language ?? "de", "location", locations.Select(l => l.Name)));
        }

        if (treatment is null || location is null)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = (treatment is null ? "Behandlung nicht erkannt; Rückfrage an den Gast ist entworfen." : "Standort nicht erkannt; Rückfrage an den Gast ist entworfen.")
                + (undecided ? $" {WhoIsThisNote}" : "");
            await DraftAsync(mail, ReplyCase.NeedsDetails, guest, treatment?.Name, location?.Name, null, [], missing, ct);
            return;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var earliest = now.AddMinutes(Options.MinLeadMinutes);
        var search = await FindSlotsAsync(location.Id, treatment.Id, employeeId, analysis.Wishes, earliest, zone, mail.RejectedStarts, rules, ct);

        if (search.Exact is { } exact && Options.AutoBook && !fromReception && !undecided && !_suggestOnly)
        {
            // Ohne Kandidaten und ohne sicheren Treffer ist der Gast neu: anlegen wie bisher (ADR 0061).
            customer ??= await CreateCustomerAsync(mail, ct);
            mail.CustomerId = customer.Id;
            var outcome = await booking.BookAsync(new AppointmentRequest(
                location.Id, treatment.Id, customer.Id, exact.Start, exact.EmployeeId, exact.RoomId, exact.EquipmentId,
                $"Per {ChannelName(mail.Channel)} angefragt (KI-Terminannahme)"), ct, source: Source(mail.Channel));
            if (outcome is BookingOutcome.Success success)
            {
                mail.Status = InboundMailStatus.Booked;
                mail.AppointmentId = success.Appointment.Id;
                mail.Proposals = [];
                await DraftAsync(mail, ReplyCase.Booked, guest, treatment.Name, location.Name,
                    TimeZoneInfo.ConvertTime(exact.Start, zone).DateTime, [], [], ct);
                return;
            }

            // Inzwischen vergeben: wie ein belegter Wunsch behandeln und Alternativen anbieten.
            search = search with { Proposals = [.. search.Proposals.Where(p => p.Start != exact.Start)] };
        }
        else if (search.Exact is { } wanted)
        {
            search = search with { Proposals = [wanted, .. search.Proposals.Where(p => p.Start != wanted.Start)] };
        }

        var proposals = search.Proposals.Take(Options.MaxProposals).OrderBy(p => p.Start).ToList();
        mail.Proposals = proposals;
        if (proposals.Count == 0)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = $"In den nächsten {Options.SearchDays} Tagen ist für {treatment.Name} nichts frei." + (undecided ? $" {WhoIsThisNote}" : "");
            await DraftAsync(mail, ReplyCase.NoAvailability, guest, treatment.Name, location.Name, null, [], [], ct);
            return;
        }

        mail.Status = undecided ? InboundMailStatus.NeedsReview : InboundMailStatus.Proposed;
        mail.ProposalRound++;
        mail.Note = undecided ? WhoIsThisNote : null;
        await DraftAsync(mail, ReplyCase.Proposed, guest, treatment.Name, location.Name, null,
            [.. proposals.Select(p => TimeZoneInfo.ConvertTime(p.Start, zone).DateTime)], [], ct);
    }

    private sealed record SlotSearch(AvailableSlot? Exact, IReadOnlyList<AvailableSlot> Proposals);

    /// <summary>
    /// Sucht zuerst genau die Wunschzeit, dann freie Zeiten im Wunschzeitraum (nächste zur Wunschzeit zuerst),
    /// dann an den folgenden Tagen. Gibt es mit der gewünschten Person nichts, auch mit anderen.
    /// </summary>
    private async Task<SlotSearch> FindSlotsAsync(Guid locationId, Guid treatmentId, Guid? employeeId,
        IReadOnlyList<RequestedTime> wishes, DateTimeOffset earliest, TimeZoneInfo zone, IReadOnlyCollection<DateTimeOffset> rejected,
        CustomerRules rules, CancellationToken ct)
    {
        // Schon vorgeschlagene Zeiten kommen als Alternative nicht wieder, für keine Person (15 Minuten um den Beginn).
        bool Open(AvailableSlot slot) => !rejected.Any(r => Math.Abs((slot.Start - r).TotalMinutes) < 15);

        var result = await SearchAsync(employeeId);
        return result.Exact is null && result.Proposals.Count == 0 && employeeId is not null ? await SearchAsync(null) : result;

        async Task<SlotSearch> SearchAsync(Guid? employee)
        {
            AvailableSlot? exact = null;
            var proposals = new List<AvailableSlot>();
            foreach (var wish in wishes.OrderBy(w => w.Date))
            {
                var context = await booking.LoadContextAsync(locationId, treatmentId, wish.Date, null, ct);
                if (context is null)
                {
                    break;
                }

                var planner = new ResourcePlanner(rules.Apply(context));
                if (exact is null && wish is { From: { } at, Until: null })
                {
                    var start = LocalTime.ToUtc(wish.Date, at, zone);
                    if (start >= earliest && planner.Plan(start, employee).Plan is { } plan)
                    {
                        exact = new AvailableSlot(plan.Times.Start, plan.Times.End, plan.Employee.Id, plan.Room.Id, plan.Equipment?.Id);
                    }
                }

                var day = planner.FreeSlots(wish.Date, Step, employee).Where(s => s.Start >= earliest && Open(s)).DistinctBy(s => s.Start).ToList();
                var (from, until) = (wish.From ?? new TimeOnly(0, 0), wish.Until ?? wish.From?.AddHours(3) ?? new TimeOnly(23, 59));
                var target = LocalTime.ToUtc(wish.Date, wish.From ?? new TimeOnly(0, 0), zone);
                var inWindow = wish.From is null && wish.Until is null
                    ? day
                    : day.Where(s =>
                        {
                            var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Start, zone).DateTime);
                            return local >= from.AddHours(wish.Until is null ? -3 : 0) && local <= until;
                        }).ToList();
                proposals.AddRange(Spread(inWindow, target));
            }

            if (proposals.Count == 0)
            {
                // Nichts im Wunschzeitraum: ab dem ersten Wunschtag je Tag die erste freie Zeit.
                var firstDay = wishes.Count > 0 ? wishes.Min(w => w.Date) : LocalTime.LocalDate(earliest, zone);
                var window = wishes.FirstOrDefault(w => w.From is not null);
                for (var date = firstDay.AddDays(wishes.Count > 0 ? 1 : 0);
                     date <= firstDay.AddDays(Options.SearchDays) && proposals.Count < Options.MaxProposals;
                     date = date.AddDays(1))
                {
                    var context = await booking.LoadContextAsync(locationId, treatmentId, date, null, ct);
                    if (context is null)
                    {
                        break;
                    }

                    var day = new ResourcePlanner(rules.Apply(context)).FreeSlots(date, Step, employee).Where(s => s.Start >= earliest && Open(s)).ToList();
                    var preferred = window?.From is { } wanted
                        ? day.MinBy(s => Math.Abs((TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Start, zone).DateTime) - wanted).TotalMinutes))
                        : day.FirstOrDefault();
                    if (preferred is not null)
                    {
                        proposals.Add(preferred);
                    }
                }
            }

            return new SlotSearch(exact, [.. proposals.DistinctBy(p => p.Start)]);
        }
    }

    /// <summary>Freie Zeiten nahe an der Wunschzeit, mindestens eine halbe Stunde auseinander, damit die Auswahl etwas bringt.</summary>
    private List<AvailableSlot> Spread(IEnumerable<AvailableSlot> slots, DateTimeOffset target)
    {
        var picked = new List<AvailableSlot>();
        foreach (var slot in slots.OrderBy(s => Math.Abs((s.Start - target).TotalMinutes)))
        {
            if (picked.Count >= Options.MaxProposals)
            {
                break;
            }

            if (picked.All(p => Math.Abs((p.Start - slot.Start).TotalMinutes) >= 30))
            {
                picked.Add(slot);
            }
        }

        return picked;
    }

    /// <summary>
    /// Mail einer anderen Plattform laut Absender-Regel: wie eine Anfrage lesen (automatisch oder nur als Vorschlag), aber nie an die
    /// Plattform antworten.
    /// </summary>
    private async Task ProcessOtherPlatformAsync(InboundMail mail, SenderRule rule, CancellationToken ct)
    {
        _suggestOnly = rule.Handling == SenderHandling.Suggest;
        await ProcessRequestAsync(mail, ct);
        mail.ReplyStatus = ReplyStatus.None;
        mail.ReplyTo = null;
        mail.ReplySubject = null;
        mail.ReplyBody = null;
        var hint = $"Absender-Regel „{rule.Pattern}“{(rule.Note is { Length: > 0 } note ? $" ({note})" : "")}: "
            + (_suggestOnly ? "nur als Vorschlag, die Terminannahme trägt nichts selbst ein." : "wie eine Anfrage gelesen.")
            + " Eine Antwort an die Plattform gibt es nicht.";
        mail.Note = mail.Note is { Length: > 0 } existing ? Truncate($"{hint} {existing}", 2000) : hint;
    }

    /// <summary>
    /// Treatwell-Mail: lesen (KI, sonst Regeln), dann eine gültige Neubuchung sofort in den Kalender eintragen, auch wenn der Gast noch
    /// keinem Kunden sicher zugeordnet ist (ADR 0062). Kollidiert die Zeit, wird nichts eingetragen und die Rezeption entscheidet.
    /// </summary>
    private async Task ProcessTreatwellAsync(InboundMail mail, SenderRule? rule, CancellationToken ct)
    {
        mail.Channel = BookingChannel.Treatwell;
        mail.Language = "de";

        var homeZone = LocalTime.Zone(await db.Locations.AsNoTracking().OrderBy(l => l.Name).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) ?? "Europe/Vienna");
        var reading = await reader.ReadAsync(mail.Subject, mail.Body, mail.ReceivedAt, homeZone, ct);
        if (reading.Booking is not { } parsed)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = Truncate($"Treatwell-Mail nicht eindeutig gelesen. {reading.Problem} Bitte die Buchung selbst übernehmen.", 2000);
            return;
        }

        var prepaid = parsed.Payment == PlatformPayment.Prepaid ? parsed.Total : null;
        mail.Kind = parsed.Kind;
        mail.PlatformReference = Truncate(parsed.Reference, 200);
        mail.PlatformPayment = parsed.Payment;
        mail.PrepaidAmount = prepaid;
        mail.Summary = Truncate($"Treatwell {parsed.Reference}: {parsed.Service ?? "?"} am {parsed.Date:dd.MM.yyyy} {parsed.Time:HH\\:mm}, "
            + $"{parsed.Customer ?? "Gast"} · {PaymentText(parsed.Payment, prepaid)} · gelesen mit {reading.Method}", 2000);

        // Ohne bestätigten Absender könnte jeder Termine anlegen oder stornieren, indem er sich als Treatwell ausgibt.
        var handling = rule?.Handling ?? SenderHandling.Automatic;
        var holdBack = handling != SenderHandling.Automatic
            ? $"Absender-Regel „{rule!.Pattern}“: nur als Vorschlag. Die Buchung wurde nicht automatisch eingetragen."
            : !mail.SenderVerified
                ? "Absender nicht bestätigt (SPF/DKIM). Die Mail wurde nicht automatisch übernommen; bitte im Treatwell-Konto prüfen."
                : null;

        var existing = await db.ChannelBookings.FirstOrDefaultAsync(b => b.Channel == BookingChannel.Treatwell && b.ExternalId == parsed.Reference, ct);

        if (parsed.Kind == InboundMailKind.TreatwellCancellation)
        {
            if (existing?.AppointmentId is not { } toCancel)
            {
                mail.Status = InboundMailStatus.NeedsReview;
                mail.Note = $"Stornierung für Treatwell-Buchung {parsed.Reference}, die Buchung ist aber nicht im Kalender.";
                return;
            }

            mail.AppointmentId = toCancel;
            if (holdBack is not null)
            {
                mail.Status = InboundMailStatus.NeedsReview;
                mail.Note = $"Treatwell hat die Buchung {parsed.Reference} abgesagt. {holdBack} Bitte den Termin im Kalender prüfen und selbst stornieren.";
                return;
            }

            var cancelled = await booking.CancelAsync(toCancel, $"Storniert über Treatwell ({parsed.Reference})", ct);
            (mail.Status, mail.Note) = cancelled is BookingOutcome.Success
                ? (InboundMailStatus.Cancelled, null)
                : (InboundMailStatus.NeedsReview, "Stornierung konnte nicht eingetragen werden.");
            return;
        }

        if (parsed.Kind == InboundMailKind.TreatwellBooking && existing is not null)
        {
            mail.Status = InboundMailStatus.Ignored;
            mail.AppointmentId = existing.AppointmentId;
            mail.Note = $"Treatwell-Buchung {parsed.Reference} ist schon übernommen.";
            return;
        }

        if (parsed.ServiceCount > 1)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = $"Treatwell-Buchung {parsed.Reference} enthält {parsed.ServiceCount} Behandlungen. Mehrere Behandlungen in einer Buchung trägt die Terminannahme nicht selbst ein; "
                + "bitte die Termine selbst eintragen.";
            return;
        }

        var resolved = await ResolveTreatwellAsync(parsed, ct);
        if (resolved.Problem is { } problem)
        {
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = problem;
            mail.LocationId = resolved.Location?.Id;
            mail.TreatmentId = resolved.Treatment?.Id;
            return;
        }

        var (location, treatment, employeeId) = (resolved.Location!, resolved.Treatment!, resolved.EmployeeId);
        mail.LocationId = location.Id;
        mail.TreatmentId = treatment.Id;
        mail.GuestEmail = Truncate(parsed.Email?.Trim().ToLowerInvariant(), 320);
        mail.GuestPhone = Truncate(parsed.Phone?.Trim(), 50);
        (mail.GuestFirstName, mail.GuestLastName) = SplitName(parsed.Customer);
        var zone = LocalTime.Zone(location.TimeZone);
        var start = LocalTime.ToUtc(parsed.Date!.Value, parsed.Time!.Value, zone);

        if (holdBack is not null)
        {
            // Nur als Vorschlag: die gewünschte Zeit steht (wenn frei) ganz vorn, die Rezeption trägt sie mit einem Klick ein.
            var offer = await FindSlotsAsync(location.Id, treatment.Id, employeeId, [new RequestedTime(parsed.Date.Value, parsed.Time, null)],
                mail.ReceivedAt, zone, [], CustomerRules.None, ct);
            IEnumerable<AvailableSlot> slots = offer.Exact is { } exact ? [exact, .. offer.Proposals.Where(p => p.Start != exact.Start)] : offer.Proposals;
            mail.Proposals = [.. slots.Take(Options.MaxProposals).OrderBy(p => p.Start)];
            mail.Status = InboundMailStatus.NeedsReview;
            mail.Note = Truncate($"Treatwell-Buchung {parsed.Reference}: {holdBack}", 2000);
            return;
        }

        BookingOutcome outcome;
        TreatwellGuest? guest = null;
        if (existing?.AppointmentId is { } toMove)
        {
            // Treatwell hat den Termin verschoben: dieselbe Buchung bewegen statt neu anlegen.
            outcome = await booking.MoveAsync(toMove, new MoveAppointmentRequest(start, employeeId, null, null), ct);
            if (outcome is BookingOutcome.Rejected && employeeId is not null)
            {
                outcome = await booking.MoveAsync(toMove, new MoveAppointmentRequest(start, null, null, null), ct);
            }
        }
        else
        {
            guest = await ResolveTreatwellGuestAsync(mail, ct);
            mail.CustomerId = guest.Customer.Id;
            var request = new AppointmentRequest(location.Id, treatment.Id, guest.Customer.Id, start, employeeId, null, null,
                $"Treatwell-Buchung {parsed.Reference}");
            var platform = new PlatformBooking(parsed.Payment, prepaid, AcceptPending: true);
            outcome = await booking.BookAsync(request, ct, source: AppointmentSource.Treatwell, platform: platform);
            // Die Person auf Treatwell ist belegt: andere Person mit gleicher Qualifikation, anderer Raum.
            if (outcome is BookingOutcome.Rejected && employeeId is not null)
            {
                outcome = await booking.BookAsync(request with { EmployeeId = null }, ct, source: AppointmentSource.Treatwell, platform: platform);
            }
        }

        switch (outcome)
        {
            case BookingOutcome.Success success:
                mail.Status = InboundMailStatus.Booked;
                mail.AppointmentId = success.Appointment.Id;
                mail.CustomerId = success.Appointment.CustomerId;
                mail.AcceptInPlatformPending = existing is null;
                mail.Note = guest is { Provisional: true } ? ProvisionalNote : null;
                if (existing is null)
                {
                    await ClaimAsync(mail, parsed.Reference, success.Appointment.Id, ct);
                }

                break;

            case BookingOutcome.Rejected rejected:
                // Doppelbelegung: nichts eintragen. Die Buchung steht bei Treatwell schon fest, die Rezeption muss dort umbuchen oder absagen.
                await DropNewGuestAsync(mail, guest, ct);
                var offer = await FindSlotsAsync(location.Id, treatment.Id, null,
                    [new RequestedTime(parsed.Date.Value, parsed.Time, null)], mail.ReceivedAt, zone, [], CustomerRules.None, ct);
                mail.Proposals = [.. offer.Proposals.Take(Options.MaxProposals).OrderBy(p => p.Start)];
                mail.Status = InboundMailStatus.NeedsReview;
                mail.Note = Truncate(await CollisionNoteAsync(parsed, location.Id, treatment, start, rejected.Reason, zone, moved: existing is not null, ct), 2000);
                break;

            default:
                await DropNewGuestAsync(mail, guest, ct);
                mail.Status = InboundMailStatus.NeedsReview;
                mail.Note = $"Treatwell-Buchung {parsed.Reference} konnte nicht eingetragen werden ({outcome}).";
                break;
        }
    }

    internal const string ProvisionalNote = "Wer ist das? Der Gast ist vorläufig angelegt, der Termin steht schon im Kalender. Bitte einen Vorschlag zuordnen oder den Gast bewusst als neu bestätigen.";

    /// <summary>Wie der Gast bezahlt, für Zusammenfassung und Anzeige: „Vorab über Treatwell bezahlt: 35,00 €“ oder „Zahlt vor Ort“.</summary>
    internal static string PaymentText(PlatformPayment payment, decimal? prepaid) => payment switch
    {
        PlatformPayment.Prepaid => prepaid is { } amount ? $"Vorab über Treatwell bezahlt: {EuroText(amount)}" : "Vorab über Treatwell bezahlt",
        PlatformPayment.OnSite => "Zahlt vor Ort",
        _ => "Zahlung unbekannt",
    };

    internal static string EuroText(decimal amount) => $"{amount.ToString("N2", CultureInfo.GetCultureInfo("de-AT"))} €";

    /// <summary>„kollidiert mit …“: wer die Zeit belegt, damit die Rezeption in Treatwell umbuchen oder absagen kann.</summary>
    private async Task<string> CollisionNoteAsync(TreatwellBooking parsed, Guid locationId, Treatment treatment, DateTimeOffset start,
        BookingRejected rejected, TimeZoneInfo zone, bool moved, CancellationToken ct)
    {
        var end = start.AddMinutes(treatment.DurationMinutes);
        // Nennt der Planer bestimmte Personen, Räume oder Geräte, zählen deren Termine; sonst ist das ganze Team oder jeder Raum belegt.
        var resources = rejected.Conflicts.Select(c => c.ResourceId).ToList();
        var anyResource = resources.Count == 0 || resources.Contains(Guid.Empty);
        var blocking = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == locationId && a.Status != AppointmentStatus.Cancelled && a.BlockedFrom < end && start < a.BlockedUntil
                && (anyResource || resources.Contains(a.EmployeeId) || resources.Contains(a.RoomId) || (a.EquipmentId != null && resources.Contains(a.EquipmentId.Value))))
            .OrderBy(a => a.Start).Take(2).ToListAsync(ct);
        string Local(DateTimeOffset at, string format) => TimeZoneInfo.ConvertTime(at, zone).ToString(format, CultureInfo.InvariantCulture);
        var with = blocking.Count > 0
            ? string.Join(" und ", blocking.Select(a => $"dem Termin von {$"{a.Customer.FirstName} {a.Customer.LastName}".Trim()} ({a.Treatment.Name}, {Local(a.Start, "HH:mm")}–{Local(a.End, "HH:mm")})"))
            : rejected.Message;
        var what = moved
            ? $"Treatwell hat die Buchung {parsed.Reference} auf {Local(start, "dd.MM.yyyy HH:mm")} verschoben"
            : $"Treatwell-Buchung {parsed.Reference} ({Local(start, "dd.MM.yyyy HH:mm")})";
        return $"{what} kollidiert mit {with}. In Treatwell umbuchen oder absagen.";
    }

    private sealed record TreatwellGuest(Customer Customer, bool Created, bool Provisional);

    /// <summary>
    /// Kunde der Treatwell-Buchung: sicher erkannt, von der Rezeption bestätigt, neu angelegt (kein Kandidat) oder, solange Kandidaten offen sind,
    /// vorläufig angelegt mit „Zuordnung offen“ (der Termin blockt die Zeit trotzdem).
    /// </summary>
    private async Task<TreatwellGuest> ResolveTreatwellGuestAsync(InboundMail mail, CancellationToken ct)
    {
        if (mail is { CustomerConfirmed: true, CustomerId: { } confirmed }
            && await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == confirmed && c.DeletedAt == null, ct) is { } chosen)
        {
            return new TreatwellGuest(chosen, false, false);
        }

        var match = await matcher.MatchAsync(MatchRequest(mail), ct);
        if (match.Certain is { } certain)
        {
            mail.CustomerId = certain.Id;
            mail.CustomerConfirmed = true;
            mail.CustomerCandidates = [];
            return new TreatwellGuest(certain, false, false);
        }

        if (match.Candidates.Count > 0)
        {
            var provisional = NewCustomer(mail, time.GetUtcNow());
            provisional.AssignmentOpen = true;
            provisional.Note = "Aus Treatwell angelegt, Zuordnung offen";
            db.Customers.Add(provisional);
            await db.SaveChangesAsync(ct);
            mail.CustomerId = provisional.Id;
            mail.CustomerConfirmed = false;
            mail.CustomerCandidates = [.. match.Candidates];
            return new TreatwellGuest(provisional, true, true);
        }

        return new TreatwellGuest(await CreateCustomerAsync(mail, ct), true, false);
    }

    /// <summary>Die Buchung ist gescheitert: den eben dafür angelegten Gast wieder entfernen (er hat nichts anderes).</summary>
    private async Task DropNewGuestAsync(InboundMail mail, TreatwellGuest? guest, CancellationToken ct)
    {
        if (guest is not { Created: true })
        {
            return;
        }

        if (db.Entry(guest.Customer).State != EntityState.Detached)
        {
            db.Entry(guest.Customer).State = EntityState.Detached;
        }

        await db.Customers.Where(c => c.Id == guest.Customer.Id).ExecuteDeleteAsync(ct);
        mail.CustomerId = null;
        mail.CustomerConfirmed = false;
        mail.CustomerCandidates = [];
    }

    /// <summary>
    /// Die Rezeption hat den vorläufig angelegten Treatwell-Gast einem Kunden zugeordnet: die Termine dieser Buchung wandern auf ihn, der
    /// vorläufige Gast wird entfernt, sofern er sonst nichts hat (Termine, Rechnungen, Belege); sonst bleibt er stehen und die Nachricht sagt es.
    /// Ist <paramref name="chosenId"/> der vorläufige Gast selbst, gilt er danach als bewusst neu angelegt. Liefert einen Fehlertext oder <c>null</c>.
    /// </summary>
    public async Task<string?> RelinkTreatwellGuestAsync(InboundMail mail, Guid provisionalId, Guid chosenId, CancellationToken ct)
    {
        string? note = null;
        if (provisionalId != chosenId)
        {
            var appointmentIds = new List<Guid>();
            if (mail.AppointmentId is { } own)
            {
                appointmentIds.Add(own);
            }

            if (mail.PlatformReference is { } reference)
            {
                appointmentIds.AddRange((await db.ChannelBookings.AsNoTracking()
                    .Where(b => b.Channel == BookingChannel.Treatwell && b.ExternalId == reference && b.AppointmentId != null)
                    .Select(b => b.AppointmentId!.Value).ToListAsync(ct)).Except(appointmentIds));
            }

            foreach (var id in appointmentIds)
            {
                switch (await booking.ReassignCustomerAsync(id, chosenId, ct))
                {
                    case BookingOutcome.Rejected rejected:
                        return rejected.Reason.Message;
                    case BookingOutcome.NotFound notFound:
                        return $"{notFound.What} nicht gefunden.";
                }
            }

            var inUse = await db.Appointments.AnyAsync(a => a.CustomerId == provisionalId, ct)
                || await db.Bills.AnyAsync(b => b.CustomerId == provisionalId, ct)
                || await db.Receipts.AnyAsync(r => r.CustomerId == provisionalId, ct);
            if (!inUse)
            {
                try
                {
                    await db.Customers.Where(c => c.Id == provisionalId).ExecuteDeleteAsync(ct);
                }
                catch (Exception ex) when (ex is DbUpdateException or PostgresException)
                {
                    inUse = true;
                }
            }

            if (inUse)
            {
                await db.Customers.Where(c => c.Id == provisionalId).ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.AssignmentOpen, false)
                    .SetProperty(c => c.Note, "Aus Treatwell angelegt; die Buchung gehört zu einem anderen Kunden"), ct);
                note = "Der vorläufig angelegte Treatwell-Gast bleibt bestehen, weil er noch Termine oder Belege hat. Bitte in der Kundenliste prüfen.";
            }
        }
        else
        {
            await db.Customers.Where(c => c.Id == chosenId).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AssignmentOpen, false)
                .SetProperty(c => c.Note, "Aus Treatwell angelegt"), ct);
        }

        mail.CustomerId = chosenId;
        mail.CustomerConfirmed = true;
        mail.CustomerCandidates = [];
        mail.Note = note;
        await SaveMailAsync(mail, ct);
        return null;
    }

    private sealed record TreatwellTarget(Location? Location, Treatment? Treatment, Guid? EmployeeId, string? Problem);

    private async Task<TreatwellTarget> ResolveTreatwellAsync(TreatwellBooking parsed, CancellationToken ct)
    {
        var mappings = await db.ChannelMappings.AsNoTracking().Where(m => m.Channel == BookingChannel.Treatwell).ToListAsync(ct);
        Guid? Mapped(ChannelMappingKind kind, string? name) =>
            name is null ? null : mappings.FirstOrDefault(m => m.Kind == kind && m.NormalizedName == Normalize(name))?.TargetId;

        var locations = await db.Locations.AsNoTracking().ToListAsync(ct);
        var location = Mapped(ChannelMappingKind.Location, parsed.Venue) is { } locationId
            ? locations.FirstOrDefault(l => l.Id == locationId)
            : parsed.Venue is { } venue
                ? locations.Where(l => venue.Contains(l.Name, StringComparison.OrdinalIgnoreCase) || l.Name.Contains(venue, StringComparison.OrdinalIgnoreCase)).MaxBy(l => l.Name.Length)
                : null;
        location ??= locations.Count == 1 ? locations[0] : null;

        var treatments = await db.Treatments.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        var treatment = Mapped(ChannelMappingKind.Treatment, parsed.Service) is { } treatmentId
            ? treatments.FirstOrDefault(t => t.Id == treatmentId)
            : parsed.Service is { } service
                ? treatments.FirstOrDefault(t => Normalize(t.Name) == Normalize(service))
                  ?? treatments.Where(t => service.Contains(t.Name, StringComparison.OrdinalIgnoreCase)).MaxBy(t => t.Name.Length)
                : null;

        var employees = await db.Employees.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);
        var employeeId = Mapped(ChannelMappingKind.Employee, parsed.Staff)
            ?? (parsed.Staff is { } staff
                ? employees.FirstOrDefault(e => Normalize(e.DisplayName) == Normalize(staff)
                    || e.DisplayName.StartsWith($"{staff.Trim()} ", StringComparison.OrdinalIgnoreCase)
                    || (e.PreferredName is { } preferred && Normalize(preferred) == Normalize(staff)))?.Id
                : null);

        var problem =
            location is null ? $"Standort „{parsed.Venue}“ unbekannt. Unter Treatwell-Zuordnungen eintragen."
            : treatment is null ? $"Leistung „{parsed.Service}“ unbekannt. Unter Treatwell-Zuordnungen der passenden Behandlung zuordnen."
            : parsed.Date is null || parsed.Time is null ? "Datum oder Uhrzeit der Treatwell-Buchung nicht lesbar."
            : null;
        return new TreatwellTarget(location, treatment, employeeId, problem);
    }

    /// <summary>Merkt sich die Treatwell-Nummer; kommt dieselbe Buchung parallel zweimal, wird die zweite wieder storniert.</summary>
    private async Task ClaimAsync(InboundMail mail, string reference, Guid appointmentId, CancellationToken ct)
    {
        db.ChannelBookings.Add(new ChannelBooking
        {
            Channel = BookingChannel.Treatwell,
            ExternalId = reference,
            AppointmentId = appointmentId,
            InboundMailId = mail.Id,
            CreatedAt = time.GetUtcNow(),
        });
        try
        {
            await SaveMailAsync(mail, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            db.ChangeTracker.Clear();
            await booking.CancelAsync(appointmentId, $"Doppelt übernommene Treatwell-Buchung {reference}", ct);
            mail.Status = InboundMailStatus.Ignored;
            mail.Note = $"Treatwell-Buchung {reference} ist schon übernommen.";
        }
    }

    /// <summary>Kunde der Nachricht (nur sicher oder bestätigt) bestimmen und Kandidaten für die Rezeption an der Mail merken.</summary>
    private async Task<Customer?> ResolveCustomerAsync(InboundMail mail, InboundMail? previous, bool fromReception, CancellationToken ct)
    {
        // Schon bestätigt (Rezeption, Wiederholung nach einem Fehler) oder die Antwort auf eine bestätigte Anfrage desselben Absenders.
        var known = mail is { CustomerConfirmed: true, CustomerId: { } own } ? own
            : !fromReception && previous is { CustomerConfirmed: true, CustomerId: { } inherited } ? inherited
            : (Guid?)null;
        if (known is { } id && await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct) is { } existing)
        {
            mail.CustomerId = existing.Id;
            mail.CustomerConfirmed = true;
            mail.CustomerCandidates = [];
            return existing;
        }

        if (fromReception)
        {
            // „Neu vorschlagen“ ordnet nichts neu zu: die Vorschläge und eine gelöste Zuordnung bleiben, wie die Rezeption sie hinterlassen hat.
            mail.CustomerId = null;
            mail.CustomerConfirmed = false;
            return null;
        }

        var match = await matcher.MatchAsync(MatchRequest(mail), ct);
        mail.CustomerId = match.Certain?.Id;
        mail.CustomerConfirmed = match.Certain is not null;
        mail.CustomerCandidates = match.Certain is null ? [.. match.Candidates] : [];
        return match.Certain;
    }

    /// <summary>Was über den Absender bekannt ist: Adresse bzw. Nummer des Absenders, von der KI gelesene Nummer und Name.</summary>
    internal static CustomerMatchRequest MatchRequest(InboundMail mail)
    {
        var (first, last) = SplitName(mail.FromName);
        var shortMessage = IsShortMessage(mail.Channel);
        return new CustomerMatchRequest(
            shortMessage ? null : mail.Channel == BookingChannel.Treatwell ? mail.GuestEmail : mail.From,
            shortMessage ? mail.From : null,
            mail.GuestPhone,
            mail.GuestFirstName ?? first,
            mail.GuestLastName ?? last);
    }

    internal const string WhoIsThisNote = "Wer ist das? Der Absender ist keinem Kunden sicher zuzuordnen. Bitte einen Vorschlag zuordnen oder bewusst neu anlegen; gebucht wird erst danach.";

    private async Task<Customer> CreateCustomerAsync(InboundMail mail, CancellationToken ct)
    {
        var customer = NewCustomer(mail, time.GetUtcNow());
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        mail.CustomerId = customer.Id;
        mail.CustomerConfirmed = true;
        mail.CustomerCandidates = [];
        return customer;
    }

    /// <summary>Neuer Kunde aus der Anfrage: bei Mails mit Adresse, bei SMS, WhatsApp und Anruf mit Telefonnummer.</summary>
    internal static Customer NewCustomer(InboundMail mail, DateTimeOffset now)
    {
        var shortMessage = IsShortMessage(mail.Channel);
        var (first, last) = SplitName(mail.FromName);
        var treatwell = mail.Channel == BookingChannel.Treatwell;
        return new Customer
        {
            FirstName = mail.GuestFirstName ?? first ?? (treatwell ? "Treatwell-Gast" : shortMessage ? "Gast" : mail.From.Split('@')[0]),
            LastName = mail.GuestLastName ?? last ?? "",
            Email = shortMessage ? null : Truncate(treatwell ? mail.GuestEmail : mail.ReplyTo ?? mail.From, 256),
            Phone = shortMessage ? Truncate(mail.From, 50) : mail.GuestPhone,
            Note = $"Aus {ChannelName(mail.Channel)} angelegt",
            CreatedFromChannel = mail.Channel,
            CreatedAt = now,
        };
    }

    internal static bool IsShortMessage(BookingChannel channel) => channel is BookingChannel.Sms or BookingChannel.WhatsApp or BookingChannel.Phone;

    /// <summary>Quelle des Termins im Kalender: der Kanal, über den die Anfrage kam.</summary>
    internal static AppointmentSource Source(BookingChannel channel) => channel switch
    {
        BookingChannel.Sms => AppointmentSource.Sms,
        BookingChannel.WhatsApp => AppointmentSource.WhatsApp,
        BookingChannel.Phone => AppointmentSource.Phone,
        BookingChannel.Treatwell => AppointmentSource.Treatwell,
        _ => AppointmentSource.Email,
    };

    /// <summary>Kanal im Änderungsverlauf: der Kanal, über den die Anfrage kam.</summary>
    internal static ChangeChannel ChangeChannelOf(BookingChannel channel) => channel switch
    {
        BookingChannel.Sms => ChangeChannel.Sms,
        BookingChannel.WhatsApp => ChangeChannel.WhatsApp,
        BookingChannel.Phone => ChangeChannel.Phone,
        BookingChannel.Treatwell => ChangeChannel.Treatwell,
        _ => ChangeChannel.Mail,
    };

    internal static string ChannelName(BookingChannel channel) => channel switch
    {
        BookingChannel.Sms => "SMS",
        BookingChannel.WhatsApp => "WhatsApp",
        BookingChannel.Phone => "Anruf",
        BookingChannel.Treatwell => "Treatwell",
        _ => "Mail",
    };

    /// <summary>
    /// Für die Rezeption: Absage oder Verschiebung mit dem nächsten Termin des Gasts. Bei einer Absage merkt sich die Mail diesen
    /// Termin, damit die Rezeption ihn mit einem Klick storniert; storniert wird nie ohne sie.
    /// </summary>
    private async Task<string> ForwardNoteAsync(InboundMail mail, Customer? customer, CancellationToken ct)
    {
        var next = customer is null ? null : await db.Appointments.AsNoTracking().Include(a => a.Treatment)
            .Where(a => a.CustomerId == customer.Id && a.Status == AppointmentStatus.Booked && a.Start > time.GetUtcNow())
            .OrderBy(a => a.Start).FirstOrDefaultAsync(ct);
        var upcoming = customer is null && mail.CustomerCandidates.Count > 0
            ? WhoIsThisNote
            : "Kein kommender Termin gefunden.";
        if (next is not null)
        {
            var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == next.LocationId, ct);
            var start = TimeZoneInfo.ConvertTime(next.Start, LocalTime.Zone(location.TimeZone));
            upcoming = $"Nächster Termin: {next.Treatment.Name} am {start:dd.MM.yyyy} um {start:HH:mm} Uhr, {location.Name}.";
            if (mail.Kind == InboundMailKind.Cancellation)
            {
                mail.AppointmentId = next.Id;
                mail.LocationId ??= next.LocationId;
            }
        }

        return mail.Kind switch
        {
            InboundMailKind.Cancellation => $"Gast möchte absagen. {upcoming}",
            InboundMailKind.Reschedule => $"Gast möchte verschieben. {upcoming}",
            InboundMailKind.Question => "Frage des Gasts, bitte beantworten.",
            _ => "Kein Terminwunsch erkannt, bitte ansehen.",
        };
    }

    private async Task DraftAsync(InboundMail mail, ReplyCase replyCase, string guest, string? treatment, string? location,
        DateTime? booked, IReadOnlyList<DateTime> alternatives, IReadOnlyList<string> missing, CancellationToken ct)
    {
        if (mail.ReplyTo is null)
        {
            return;
        }

        var draft = await assistant.DraftReplyAsync(new ReplyRequest(
            mail.Language ?? "de", replyCase, guest, mail.Subject, mail.Body, Options.SpaName,
            treatment, location, booked, alternatives, missing, IsShortMessage(mail.Channel)), ct);
        mail.ReplySubject = Truncate(draft.Subject, 1000);
        mail.ReplyBody = draft.Body;
        mail.ReplyStatus = ReplyStatus.Draft;

        // Neuer Entwurf: frühere Fassungen gehörten zu anderen Zeiten und lassen sich nicht mehr zurückholen.
        mail.ReplyHistory = [];
        mail.ReplyNotice = null;
    }

    /// <summary>
    /// Frage des Gasts: Antwortvorschlag der KI aus den Angaben des Spas statt „Das Team meldet sich“. Bleibt zur Prüfung bei der
    /// Rezeption; enthält er eine Zahl, die weder in den Angaben noch in der Nachricht steht, gilt der bisherige Entwurf.
    /// </summary>
    private async Task SuggestAnswerAsync(InboundMail mail, string guest, CancellationToken ct)
    {
        if (!answerer.Enabled || mail.ReplyTo is null)
        {
            return;
        }

        try
        {
            var (spa, known) = await facts.DescribeAsync(mail.LocationId, ct);
            var answer = await answerer.AnswerAsync(new QuestionRequest(
                mail.Language ?? "de", guest, mail.Subject, mail.Body, spa, known, IsShortMessage(mail.Channel)), ct);
            if (answer is not { Answerable: true, Body: { Length: > 0 and <= MaxSuggestion } body }
                || !SpaFacts.OnlyKnownNumbers(body, $"{known}\n{mail.Subject}\n{mail.Body}"))
            {
                return;
            }

            mail.ReplyBody = body.Trim();
            mail.Note = SuggestedNote;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Kein Antwortvorschlag der KI für Mail {MailId}.", mail.Id);
        }
    }

    internal const string SuggestedNote = "Frage des Gasts. Die KI hat aus Öffnungszeiten, Behandlungen und Preisen eine Antwort vorgeschlagen: bitte prüfen und senden.";

    private const int MaxSuggestion = 4000;

    /// <summary>Der BookingService leert bei einer Kollision den Change-Tracker; dann die Mail wieder anhängen.</summary>
    private async Task SaveMailAsync(InboundMail mail, CancellationToken ct)
    {
        if (db.Entry(mail).State == EntityState.Detached)
        {
            db.InboundMails.Update(mail);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Wie der Gast in der Antwort angesprochen wird. Deutsch mit Anrede und Titel nach dem Land des Standorts der Anfrage („Frau Mag. Huber“,
    /// ohne Standort Österreich); sonst und ohne Kundendatensatz der Name aus der Mail. Nie der Rufname (ADR 0064).
    /// </summary>
    private async Task<string> GuestNameAsync(InboundMail mail, Customer? customer, CancellationToken ct)
    {
        if (customer is not null && (mail.Language ?? "de") == "de")
        {
            var timeZone = await db.Locations.Where(l => l.Id == mail.LocationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
            return Mail.GuestName.Of(customer, timeZone).GreetingName;
        }

        return customer is not null ? $"{customer.FirstName} {customer.LastName}".Trim()
            : $"{mail.GuestFirstName} {mail.GuestLastName}".Trim() is { Length: > 0 } name ? name
            : mail.FromName ?? "";
    }

    private static string MissingText(string language, string what, IEnumerable<string> options)
    {
        var list = string.Join(", ", options);
        return (language, what) switch
        {
            ("de", "treatment") => $"welche Behandlung Sie möchten ({list})",
            ("de", _) => $"an welchem Standort ({list})",
            ("fr", "treatment") => $"le soin souhaité ({list})",
            ("fr", _) => $"le lieu souhaité ({list})",
            ("it", "treatment") => $"il trattamento desiderato ({list})",
            ("it", _) => $"la sede desiderata ({list})",
            (_, "treatment") => $"which treatment you would like ({list})",
            _ => $"which location you prefer ({list})",
        };
    }

    internal static string Normalize(string name) => string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static (string? First, string? Last) SplitName(string? name)
    {
        var parts = PersonNames.WithoutTitles(name).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (string.Join(' ', parts[..^1]), parts[^1]),
        };
    }

    /// <summary>Name ohne Titel; leer wird null. Titel gehören nie in Vor- oder Nachnamen des Gasts (ADR 0064).</summary>
    private static string? WithoutTitles(string? name) => PersonNames.WithoutTitles(name) is { Length: > 0 } clean ? clean : null;

    private static string? Truncate(string? value, int length) => value is null || value.Length <= length ? value : value[..length];
}

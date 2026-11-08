using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>Queue: diese Absage wegen einer Schließzeit dem Gast zustellen (ADR 0073). Schlägt der Versand fehl, wiederholt die Queue ihn.</summary>
public sealed record SendClosureNotice(Guid NoticeId);

public static class SendClosureNoticeHandler
{
    public static async Task Handle(SendClosureNotice message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.ClosureNotices.Where(n => n.Id == message.NoticeId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<ClosureNoticeSender>().SendAsync(message.NoticeId, ct);
    }
}

/// <summary>
/// Stellt eine Absage oder die Benachrichtigung eines Wartelisten-Eintrags per Mail oder SMS zu und hält das Ergebnis im Protokoll fest. Der Text steht schon fest, hier wird
/// nichts mehr zusammengesetzt.
/// </summary>
internal sealed class ClosureNoticeSender(AispaDbContext db, OnlineDbContext online, SpaMailSender mails, ITextMessageSender texts, TimeProvider time)
{
    public async Task SendAsync(Guid noticeId, CancellationToken ct)
    {
        var notice = await db.ClosureNotices.FirstOrDefaultAsync(n => n.Id == noticeId, ct);
        if (notice is null || notice.State is ClosureNoticeState.Sent or ClosureNoticeState.Skipped or ClosureNoticeState.Cancelled)
        {
            return;
        }

        var customer = await CustomerOfAsync(notice, ct);
        try
        {
            switch (notice.Channel)
            {
                case ClosureChannel.Email when !string.IsNullOrWhiteSpace(customer.Email):
                    await mails.SendAsync(new OutgoingMail(customer.Email, notice.Subject ?? "Absage Ihres Termins", notice.Text), ct);
                    break;
                case ClosureChannel.Sms when !string.IsNullOrEmpty(PhoneNumbers.Normalize(customer.Phone ?? "")):
                    // Von der eigenen Nummer des Spas, damit eine Antwort des Gastes wieder beim richtigen Spa landet.
                    var sms = await IntakeRoutes.OwnAsync(db, BookingChannel.Sms, ct);
                    await texts.SendAsync(new OutgoingText(BookingChannel.Sms, PhoneNumbers.Normalize(customer.Phone!), ForSms(notice.Text), From: sms?.Address, SenderId: sms?.SenderId), ct);
                    break;
                default:
                    notice.State = ClosureNoticeState.Skipped;
                    notice.Error = "Keine Mailadresse und keine Telefonnummer.";
                    await db.SaveChangesAsync(ct);
                    return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or MailKit.Net.Smtp.SmtpCommandException
            or MailKit.Net.Smtp.SmtpProtocolException or IOException)
        {
            notice.State = ClosureNoticeState.Failed;
            notice.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        notice.State = ClosureNoticeState.Sent;
        notice.Error = null;
        notice.SentAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Der Gast der Nachricht: der des abgesagten Termins, bei der Warteliste der des Eintrags.</summary>
    private async Task<Customer> CustomerOfAsync(ClosureNotice notice, CancellationToken ct)
    {
        if (notice.AppointmentId is { } appointmentId)
        {
            return await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId).Select(a => a.Customer).FirstAsync(ct);
        }

        var customerId = await online.WaitlistEntries.AsNoTracking().Where(w => w.Id == notice.WaitlistEntryId).Select(w => w.CustomerId).FirstAsync(ct);
        return await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct);
    }

    /// <summary>Eine SMS ist ein Block: Leerzeilen fallen weg, damit sie kurz bleibt.</summary>
    internal static string ForSms(string text) => string.Join('\n', text.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0));
}

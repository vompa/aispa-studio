using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Gast anonymisieren und weich löschen: Eine Datenschutzvorgabe erlaubt es nur so, nie hart. Name, Kontakt, Notizen, Anamnese und Online-Zugang werden überschrieben
/// oder gelöscht, <see cref="Customer.DeletedAt"/> gesetzt. Termine, Rechnungszeilen, Gutscheine und Belege bleiben als „Gelöschter Gast“ (Belege 7 Jahre nach
/// § 132 BAO und RKSV, Ansprüche 3 Jahre nach § 1489 ABGB); die Belegkopie der Rechnungsanschrift (<c>ReceiptRecipients</c>) bleibt unverändert, weil Belege
/// unveränderlich sind. Der Änderungsverlauf behält seine Zeilen, verliert aber die Werte: das erledigt ein verzögerter Trigger beim Commit
/// (<c>aispa_history_anonymize</c>, <see cref="Sync.ChangeHistory"/>), auch auf der Gegenseite des Abgleichs.
/// <para>
/// Alles läuft in <b>einer</b> Transaktion des <see cref="AispaDbContext"/>. <c>OnlineDbContext</c> liegt in derselben Datenbank (nur andere Schemas), deshalb
/// geht der Online- und Gesundheitsteil als parametrisiertes SQL über dieselbe Verbindung; einen zweiten Kontext braucht es nicht. Der Baustein ist so
/// geschnitten, dass eine spätere Automatik nach Inaktivität ihn wiederverwendet.
/// </para>
/// </summary>
internal sealed class CustomerAnonymizer(AispaDbContext db, TimeProvider time)
{
    /// <summary>Der Platzhalter: Vor- und Nachname ergeben im gültigen Namen und im Namen im Haus „Gelöschter Gast“ (<see cref="PersonNames"/>).</summary>
    public const string PlaceholderFirstName = "Gelöschter";

    public const string PlaceholderLastName = "Gast";

    public const string AlreadyAnonymizedMessage = "Der Gast ist bereits anonymisiert.";

    /// <summary>Meldung der Buchung, der Kassa und der Zuordnung, wenn jemand einen anonymisierten Gast wählt.</summary>
    public const string NotSelectableMessage = "Der Gast ist anonymisiert und lässt sich nicht mehr wählen.";

    public abstract record Outcome
    {
        public sealed record Done : Outcome;

        public sealed record NotFound : Outcome;

        public sealed record AlreadyDone : Outcome;

        /// <summary>Noch offen: künftige Termine, offene Rechnungen und Online-Erstattungen, je mit Zahl („1 künftiger Termin“).</summary>
        public sealed record Blocked(IReadOnlyList<string> Open) : Outcome;
    }

    public async Task<Outcome> AnonymizeAsync(Guid customerId, CancellationToken ct) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async Task<Outcome> () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
            {
                return new Outcome.NotFound();
            }

            if (customer.DeletedAt is not null)
            {
                return new Outcome.AlreadyDone();
            }

            var now = time.GetUtcNow();
            if (await OpenItemsAsync(customerId, now, ct) is { Count: > 0 } open)
            {
                return new Outcome.Blocked(open);
            }

            var addresses = await MailAddressesAsync(customer, ct);
            await OverwriteAsync(customer, now, ct);
            await DeleteRowsAsync(customerId, ct);
            await ClearFreeTextAsync(customerId, ct);
            await ClearOnlineAndHealthAsync(customerId, addresses, ct);
            await transaction.CommitAsync(ct);
            return new Outcome.Done();
        });

    /// <summary>Künftige, nicht stornierte Termine (auch Besuche in Bereichen), offene Rechnungen und nicht abgeschlossene Online-Erstattungen; leer, wenn nichts mehr hängt.</summary>
    private async Task<List<string>> OpenItemsAsync(Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var upcoming = await db.Appointments.CountAsync(a => a.CustomerId == customerId && a.Status != AppointmentStatus.Cancelled && a.End > now, ct)
            + await db.AreaVisits.CountAsync(v => v.CustomerId == customerId && !v.IsCancelled && v.Until > now, ct);
        var bills = await db.Bills.CountAsync(b => b.CustomerId == customerId && b.Status == BillStatus.Open, ct);
        var refunded = OnlineRefundStatus.Refunded.ToString();
        var refunds = (await GuestRefundsAsync(customerId, ct)).Count(r => r.Status != refunded);
        var open = new List<string>();
        if (upcoming > 0)
        {
            open.Add(upcoming == 1 ? "1 künftiger Termin" : $"{upcoming} künftige Termine");
        }

        if (bills > 0)
        {
            open.Add(bills == 1 ? "1 offene Rechnung" : $"{bills} offene Rechnungen");
        }

        // Laufende oder fehlgeschlagene Online-Erstattungen: Geld ist noch offen, und die Bestätigung (auch nach dem erneuten Versuch) geht an die gespeicherte Adresse.
        if (refunds > 0)
        {
            open.Add(refunds == 1 ? "1 offene Rückerstattung" : $"{refunds} offene Rückerstattungen");
        }

        return open;
    }

    private sealed record GuestRefund(Guid Id, string Status);

    /// <summary>Online-Erstattungen zum Gast: über seine Belege oder über seine Gutscheinbestellungen, Anzahlungen und Kartenhinterlegungen.</summary>
    private Task<List<GuestRefund>> GuestRefundsAsync(Guid customerId, CancellationToken ct) =>
        db.Database.SqlQuery<GuestRefund>(
            $"""
            SELECT "Id", "Status" FROM online."Refunds"
             WHERE "ReceiptId" IN (SELECT "Id" FROM aispa."Receipts" WHERE "CustomerId" = {customerId})
                OR "SourceId" IN (SELECT "Id" FROM online."VoucherOrders" WHERE "CustomerId" = {customerId})
                OR "SourceId" IN (SELECT "Id" FROM online."Deposits" WHERE "CustomerId" = {customerId})
                OR "SourceId" IN (SELECT "Id" FROM online."CardGuarantees" WHERE "CustomerId" = {customerId})
            """).ToListAsync(ct);

    /// <summary>Mailadressen des Gastes (Hauptadresse, weitere, Online-Konten), klein geschrieben: Anmeldecodes im Kundenbereich hängen nur an der Adresse.</summary>
    private async Task<string[]> MailAddressesAsync(Customer customer, CancellationToken ct)
    {
        var addresses = await db.CustomerEmails.Where(e => e.CustomerId == customer.Id).Select(e => e.NormalizedAddress).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            addresses.Add(CustomerEmail.Normalize(customer.Email));
        }

        addresses.AddRange(await db.Database.SqlQuery<string>(
            $"""SELECT lower("Email") AS "Value" FROM online."CustomerAccounts" WHERE "CustomerId" = {customer.Id} AND "Email" IS NOT NULL""").ToListAsync(ct));
        return [.. addresses.Distinct()];
    }

    /// <summary>Alle Felder mit Personenbezug werden leer, der Name zum Platzhalter; <c>DeletedAt</c> ist die Löschmarke.</summary>
    private async Task OverwriteAsync(Customer customer, DateTimeOffset now, CancellationToken ct)
    {
        customer.FirstName = PlaceholderFirstName;
        customer.LastName = PlaceholderLastName;
        customer.PreferredName = null;
        customer.Salutation = CustomerSalutation.None;
        customer.Pronouns = null;
        customer.TitlesBefore = null;
        customer.TitlesAfter = null;
        customer.TitleInGreeting = null;
        customer.Street = null;
        customer.PostalCode = null;
        customer.City = null;
        customer.Country = null;
        customer.Email = null;
        customer.Phone = null;
        customer.BirthDate = null;
        customer.Note = null;
        customer.BillingName = null;
        customer.BillingAddress = null;
        customer.BillingVatId = null;
        customer.DiscountPercent = null;
        customer.DiscountScope = CustomerDiscountScope.All;
        customer.DiscountApprovedByUserId = null;
        customer.PreferredEmployeeId = null;
        customer.PreferredEmployeeFixed = false;
        customer.PreferredWeekdays = 0;
        customer.PreferredFrom = null;
        customer.PreferredUntil = null;
        customer.PreferredGender = null;
        customer.ReceptionNote = null;
        customer.Language = null;
        customer.AssignmentOpen = false;
        customer.DeletedAt = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Zeilen, die ganz verschwinden: weitere Mailadressen, Kennzeichen, Namensänderungen, Vorlieben, Einwilligung und Erinnerungen.</summary>
    private async Task DeleteRowsAsync(Guid customerId, CancellationToken ct)
    {
        _ = await db.CustomerEmails.Where(e => e.CustomerId == customerId).ExecuteDeleteAsync(ct);
        _ = await db.CustomerFlags.Where(f => f.CustomerId == customerId).ExecuteDeleteAsync(ct);
        _ = await db.CustomerNameChanges.Where(n => n.CustomerId == customerId).ExecuteDeleteAsync(ct);
        _ = await db.CustomerPreferences.Where(p => p.CustomerId == customerId).ExecuteDeleteAsync(ct);
        _ = await db.MarketingConsents.Where(c => c.CustomerId == customerId).ExecuteDeleteAsync(ct);
        _ = await db.RebookReminders.Where(r => r.CustomerId == customerId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Freitext an Terminen, Serien, Besuchen, Nachsorge, Anfragen und Absage-Nachrichten; die Zeilen selbst bleiben.</summary>
    private async Task ClearFreeTextAsync(Guid customerId, CancellationToken ct)
    {
        _ = await db.Appointments.Where(a => a.CustomerId == customerId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Note, (string?)null).SetProperty(a => a.CancelReason, (string?)null), ct);
        _ = await db.AppointmentSeries.Where(s => s.CustomerId == customerId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Problem, (string?)null), ct);
        _ = await db.AreaVisits.Where(v => v.CustomerId == customerId).ExecuteUpdateAsync(s => s.SetProperty(v => v.Note, (string?)null), ct);
        // Eine Nachsorge besteht nur aus dem Freitext; ohne ihn hat die Zeile keinen Inhalt mehr.
        _ = await db.AppointmentFollowUps.Where(f => db.Appointments.Any(a => a.Id == f.AppointmentId && a.CustomerId == customerId)).ExecuteDeleteAsync(ct);

        // Die Anfrage anderer Absender, die den Gast nur als Kandidaten nennt, verliert diesen Eintrag (Name steht im JSON).
        var pattern = $"%{customerId}%";
        _ = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE aispa."InboundMails" AS m SET "CustomerCandidates" = (
                SELECT coalesce(jsonb_agg(t.e ORDER BY t.ord), jsonb_build_array())
                  FROM jsonb_array_elements(m."CustomerCandidates") WITH ORDINALITY AS t(e, ord)
                 WHERE t.e::text NOT ILIKE {pattern})
             WHERE m."CustomerCandidates"::text ILIKE {pattern}
            """, ct);
        // Absender, Betreff und Text lassen sich nur beim Anlegen setzen (init): direkt in der Datenbank leeren.
        _ = await db.InboundMails.Where(m => m.CustomerId == customerId).ExecuteUpdateAsync(
            s => s.SetProperty(m => m.From, "").SetProperty(m => m.FromName, (string?)null).SetProperty(m => m.Subject, "").SetProperty(m => m.Body, ""), ct);
        foreach (var mail in await db.InboundMails.Where(m => m.CustomerId == customerId).ToListAsync(ct))
        {
            ClearMail(mail);
        }

        await db.SaveChangesAsync(ct);

        _ = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE aispa."ClosureNotices" SET "Subject" = NULL, "Text" = ''
             WHERE "AppointmentId" IN (SELECT "Id" FROM aispa."Appointments" WHERE "CustomerId" = {customerId})
                OR "WaitlistEntryId" IN (SELECT "Id" FROM online."WaitlistEntries" WHERE "CustomerId" = {customerId})
            """, ct);
    }

    /// <summary>Zusammenfassung, Notiz, Gastangaben, Antwort samt Übersetzung und Entwürfen; Status, Termin und Gast-Id bleiben.</summary>
    private static void ClearMail(InboundMail mail)
    {
        mail.Summary = null;
        mail.Note = null;
        mail.GuestFirstName = null;
        mail.GuestLastName = null;
        mail.GuestPhone = null;
        mail.GuestEmail = null;
        mail.SuggestedSalutation = null;
        mail.SuggestedTitlesBefore = null;
        mail.ReplyTo = null;
        mail.ReplySubject = null;
        mail.ReplyBody = null;
        mail.TranslatedSubject = null;
        mail.TranslatedBody = null;
        mail.ReplyTranslation = null;
        mail.ReplyTranslationOf = null;
        mail.ReplyHistory = [];
        mail.ReplyNotice = null;
        mail.WishNote = null;
        mail.CustomerCandidates = [];
    }

    /// <summary>
    /// Schemas „online“ und „health“: Konto samt Anmeldedaten (Kindtabellen fallen per Kaskade mit, die Kennung beim Anbieter verlässt auch das Änderungsprotokoll), Links, offene Warteliste, Anmeldecodes, Anamnese und Scans werden
    /// gelöscht; Adressen und Namen in Bestellungen, Anzahlungen, Rückerstattungen und Kartenhinterlegungen werden geleert.
    /// </summary>
    private async Task ClearOnlineAndHealthAsync(Guid customerId, string[] addresses, CancellationToken ct)
    {
        var waiting = WaitlistStatus.Waiting.ToString();
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM online."AccessLinks" WHERE "CustomerId" = {customerId}""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM online."WaitlistEntries" WHERE "CustomerId" = {customerId} AND "Status" = {waiting}""", ct);
        var providerKeys = await db.Database.SqlQuery<string>(
            $"""
            SELECT l."ProviderKey" AS "Value" FROM online."CustomerAccountLogins" l
              JOIN online."CustomerAccounts" a ON a."Id" = l."UserId" WHERE a."CustomerId" = {customerId}
            """).ToListAsync(ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM online."CustomerAccounts" WHERE "CustomerId" = {customerId}""", ct);
        // Das Änderungsprotokoll des Abgleichs trägt nur Schlüssel, bei externen Anmeldungen aber die Kennung beim Anbieter (ProviderKey). Die Gegenseite löscht die
        // Anmeldung über die Kaskade des Kontos mit; die Zeilen mit der Kennung würden bis zum Aufräumen des Protokolls liegen bleiben.
        _ = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM sync.changes WHERE table_schema = 'online' AND table_name = 'CustomerAccountLogins' AND row_key ->> 'ProviderKey' = ANY({providerKeys.ToArray()})""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM online."SignInCodes" WHERE lower("NormalizedEmail") = ANY({addresses})""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM health."AnamnesisScans" WHERE "CustomerId" = {customerId}""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM health."AnamnesisRecords" WHERE "CustomerId" = {customerId}""", ct);

        _ = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE online."VoucherOrders" SET "BuyerEmail" = '', "RecipientName" = NULL, "Message" = NULL WHERE "CustomerId" = {customerId}""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE online."Deposits" SET "Email" = '' WHERE "CustomerId" = {customerId}""", ct);
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE online."CardGuarantees" SET "Email" = '' WHERE "CustomerId" = {customerId}""", ct);
        var refundIds = (await GuestRefundsAsync(customerId, ct)).Select(r => r.Id).ToArray();
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE online."Refunds" SET "Email" = '' WHERE "Id" = ANY({refundIds})""", ct);
    }
}

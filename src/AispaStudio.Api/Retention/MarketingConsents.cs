using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Retention;

/// <summary>
/// Einwilligung in Erinnerungen an die nächste Behandlung mit Double-Opt-in (ADR 0040): das Team fragt an, der Gast bestätigt
/// über den Link in der Mail. Der Link öffnet nur eine Seite; bestätigt wird erst mit dem Knopf, weil Virenscanner Links
/// vorab aufrufen. Über denselben Link und den in jeder Erinnerung lässt sich jederzeit widerrufen.
/// </summary>
internal sealed class MarketingConsents(
    AispaDbContext db,
    OnlineDbContext online,
    AccessLinks links,
    OnlinePortal portal,
    IMessageBus bus,
    TenantContext tenants,
    TimeProvider time)
{
    /// <summary>Solange gilt der Link; er muss auch nach Jahren noch abmelden können.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(3 * 365);

    /// <summary>Höchstens eine Anfrage-Mail je Tag und Gast, damit niemand mit Anfragen überhäuft wird.</summary>
    public static readonly TimeSpan ResendAfter = TimeSpan.FromHours(24);

    public const string SourceTeam = "Team";

    /// <summary>Am Empfang erfasst (mündlich oder Unterschrift), ohne Double-Opt-in.</summary>
    public const string SourceReception = "Rezeption";

    /// <summary>Aus einer importierten Kundenliste, wenn die Datei die Einwilligung belegt.</summary>
    public const string SourceImport = "Import";

    public abstract record Outcome
    {
        public sealed record Done(MarketingConsentDto Consent) : Outcome;

        public sealed record NotFound : Outcome;

        public sealed record Refused(string Field, string Message) : Outcome;
    }

    public async Task<MarketingConsentDto?> ForCustomerAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.DeletedAt == null, ct))
        {
            return null;
        }

        return ToDto(await db.MarketingConsents.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId, ct));
    }

    /// <summary>Schickt dem Gast die Mail zum Bestätigen. Ist schon für dieselbe Adresse bestätigt, bleibt es dabei.</summary>
    public async Task<Outcome> RequestAsync(Guid customerId, string? language, string source, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.DeletedAt == null, ct);
        if (customer is null)
        {
            return new Outcome.NotFound();
        }

        var email = customer.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return new Outcome.Refused("email", "Beim Gast ist keine Mailadresse hinterlegt.");
        }

        var now = time.GetUtcNow();
        var consent = await db.MarketingConsents.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        var sameAddress = consent is not null && string.Equals(consent.Email, email, StringComparison.OrdinalIgnoreCase);
        if (sameAddress && consent!.State == MarketingConsentState.Confirmed)
        {
            return new Outcome.Done(ToDto(consent));
        }

        if (sameAddress && consent!.State == MarketingConsentState.Pending && now - consent.RequestedAt < ResendAfter)
        {
            return new Outcome.Refused("email", "Die Mail zum Bestätigen ist schon unterwegs. Erneut anfragen geht nach 24 Stunden.");
        }

        if (consent is null)
        {
            consent = new MarketingConsent { CustomerId = customerId, Email = email, Source = source };
            db.MarketingConsents.Add(consent);
        }

        consent.Email = email;
        consent.Source = source;
        consent.Language = GuestLanguage.Normalize(language);
        consent.State = MarketingConsentState.Pending;
        consent.RequestedAt = now;
        consent.ConfirmedAt = null;

        var token = links.Create(AccessLinkPurpose.MarketingConsent, customerId, null, now + LinkLifetime);
        await online.SaveChangesAsync(ct);
        await db.SaveChangesAsync(ct);

        var spa = await SpaNameAsync(ct);
        await bus.PublishGuestMailAsync(tenants, ConsentTexts.Request(email, GuestName.Of(customer, null), spa, portal.Url($"einwilligung/{token}"), consent.Language));
        return new Outcome.Done(ToDto(consent));
    }

    /// <summary>
    /// Was der Gast am Empfang gesagt hat (Export/Import, Aufgabe B): „Ja“ wird <see cref="MarketingConsentState.Confirmed"/> mit Quelle <see cref="SourceReception"/> (ohne Double-Opt-in,
    /// der Nachweis ist das Gespräch oder die Unterschrift), „Nein“ <see cref="MarketingConsentState.Refused"/>, „nicht gefragt“ setzt zurück. Eine schon per Mail bestätigte Einwilligung bleibt bei „Ja“.
    /// </summary>
    public async Task<Outcome> DecideAsync(Guid customerId, MarketingConsentState state, CancellationToken ct)
    {
        if (state is not (MarketingConsentState.Confirmed or MarketingConsentState.Refused or MarketingConsentState.None))
        {
            return new Outcome.Refused("state", "Erlaubt sind Ja, Nein oder nicht gefragt.");
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
        {
            return new Outcome.NotFound();
        }

        var now = time.GetUtcNow();
        var consent = await db.MarketingConsents.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (consent is null)
        {
            if (state == MarketingConsentState.None)
            {
                return new Outcome.Done(ToDto(null));
            }

            consent = new MarketingConsent { CustomerId = customerId, Email = customer.Email?.Trim() ?? "", Source = SourceReception, RequestedAt = now };
            db.MarketingConsents.Add(consent);
        }

        switch (state)
        {
            case MarketingConsentState.Confirmed when consent.State != MarketingConsentState.Confirmed:
                consent.State = MarketingConsentState.Confirmed;
                consent.Source = SourceReception;
                consent.ConfirmedAt = now;
                consent.WithdrawnAt = null;
                if (consent.Email.Length == 0 && customer.Email is { Length: > 0 } email)
                {
                    consent.Email = email.Trim();
                }

                break;
            case MarketingConsentState.Refused:
                consent.State = MarketingConsentState.Refused;
                consent.Source = SourceReception;
                consent.ConfirmedAt = null;
                consent.WithdrawnAt = now;
                break;
            case MarketingConsentState.None:
                consent.State = MarketingConsentState.None;
                consent.ConfirmedAt = null;
                consent.WithdrawnAt = null;
                break;
        }

        await db.SaveChangesAsync(ct);
        return new Outcome.Done(ToDto(consent));
    }

    /// <summary>Widerruf durch das Team, z. B. wenn der Gast es am Empfang sagt.</summary>
    public async Task<MarketingConsentDto?> WithdrawAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.DeletedAt == null, ct))
        {
            return null;
        }

        var consent = await db.MarketingConsents.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (consent is not null && consent.State != MarketingConsentState.Withdrawn)
        {
            consent.State = MarketingConsentState.Withdrawn;
            consent.WithdrawnAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        return ToDto(consent);
    }

    /// <summary>Neuer Abmeldelink für eine Erinnerung; gespeichert wird er mit dem nächsten <c>SaveChanges</c> des Kundenbereichs.</summary>
    public string UnsubscribeUrl(Guid customerId) =>
        portal.Url($"einwilligung/{links.Create(AccessLinkPurpose.MarketingConsent, customerId, null, time.GetUtcNow() + LinkLifetime)}");

    public async Task<OnlineConsentDto?> ForTokenAsync(string token, CancellationToken ct) =>
        await FindAsync(token, ct) is { } found ? await ToOnlineAsync(found.Consent, ct) : null;

    /// <summary>
    /// Bestätigen über den Link. Nur ein Link, der nach der letzten Anfrage verschickt wurde, bestätigt: ein alter Link an eine
    /// frühere Adresse darf die neue nicht freischalten.
    /// </summary>
    public async Task<OnlineConsentDto?> ConfirmAsync(string token, CancellationToken ct)
    {
        if (await FindAsync(token, ct) is not { } found)
        {
            return null;
        }

        var consent = found.Consent;
        if (consent.State != MarketingConsentState.Confirmed && found.Link.CreatedAt >= consent.RequestedAt)
        {
            consent.State = MarketingConsentState.Confirmed;
            consent.ConfirmedAt = time.GetUtcNow();
            consent.WithdrawnAt = null;
            await db.SaveChangesAsync(ct);
        }

        return await ToOnlineAsync(consent, ct);
    }

    /// <summary>Widerrufen über irgendeinen Link des Gastes, ohne Anmeldung und ohne Rückfrage.</summary>
    public async Task<OnlineConsentDto?> WithdrawAsync(string token, CancellationToken ct)
    {
        if (await FindAsync(token, ct) is not { } found)
        {
            return null;
        }

        var consent = found.Consent;
        if (consent.State != MarketingConsentState.Withdrawn)
        {
            consent.State = MarketingConsentState.Withdrawn;
            consent.WithdrawnAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        return await ToOnlineAsync(consent, ct);
    }

    private async Task<(AccessLink Link, MarketingConsent Consent)?> FindAsync(string token, CancellationToken ct)
    {
        if (await links.FindAsync(token, AccessLinkPurpose.MarketingConsent, ct) is not { } link)
        {
            return null;
        }

        var consent = await db.MarketingConsents.FirstOrDefaultAsync(c => c.CustomerId == link.CustomerId, ct);
        return consent is null ? null : (link, consent);
    }

    private async Task<OnlineConsentDto> ToOnlineAsync(MarketingConsent consent, CancellationToken ct)
    {
        var firstName = await db.Customers.Where(c => c.Id == consent.CustomerId).Select(c => c.FirstName).FirstOrDefaultAsync(ct) ?? "";
        return new OnlineConsentDto(await SpaNameAsync(ct), firstName, consent.State);
    }

    private async Task<string> SpaNameAsync(CancellationToken ct)
    {
        var tenantId = tenants.TenantId ?? throw new InvalidOperationException("Kein Spa gesetzt.");
        return await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstAsync(ct);
    }

    public static MarketingConsentDto ToDto(MarketingConsent? c) => c is null
        ? new MarketingConsentDto(MarketingConsentState.None, null, null, null, null, null, null)
        : new MarketingConsentDto(c.State, c.Email, c.Language, c.RequestedAt, c.ConfirmedAt, c.WithdrawnAt, c.Source);
}

/// <summary>Feste Texte der Anfrage-Mail (keine KI) in den Sprachen des Kundenbereichs.</summary>
internal static class ConsentTexts
{
    public static OutgoingMail Request(string to, GuestName guest, string spa, string link, string language) => language switch
    {
        "en" => new OutgoingMail(to, $"Please confirm: reminders from {spa}", $"""
            Hello {guest.FirstName},

            {spa} would like to remind you by email when your next treatment is due. For this we need your confirmation:

            {link}

            Without your confirmation we will not send you such reminders. You can withdraw your consent at any time using the same link or the link in every reminder.

            {spa}
            """),
        "fr" => new OutgoingMail(to, $"Merci de confirmer : rappels de {spa}", $"""
            Bonjour {guest.FirstName},

            {spa} souhaite vous rappeler par e-mail lorsque votre prochain soin approche. Pour cela, nous avons besoin de votre confirmation :

            {link}

            Sans confirmation, nous ne vous enverrons pas de tels rappels. Vous pouvez retirer votre consentement à tout moment via ce même lien ou celui de chaque rappel.

            {spa}
            """),
        "it" => new OutgoingMail(to, $"Conferma richiesta: promemoria da {spa}", $"""
            Buongiorno {guest.FirstName},

            {spa} desidera ricordarle via e-mail quando è il momento del prossimo trattamento. Per questo abbiamo bisogno della sua conferma:

            {link}

            Senza conferma non le invieremo promemoria di questo tipo. Può revocare il consenso in qualsiasi momento tramite lo stesso link o quello presente in ogni promemoria.

            {spa}
            """),
        _ => new OutgoingMail(to, $"Bitte bestätigen: Erinnerungen von {spa}", $"""
            Guten Tag {guest.GreetingName},

            {spa} möchte Sie per Mail erinnern, wenn Ihre nächste Behandlung fällig ist. Dafür brauchen wir Ihre Bestätigung:

            {link}

            Ohne Bestätigung schicken wir Ihnen keine solchen Erinnerungen. Sie können die Einwilligung jederzeit über denselben Link oder den Link in jeder Erinnerung widerrufen.

            {spa}
            """),
    };
}

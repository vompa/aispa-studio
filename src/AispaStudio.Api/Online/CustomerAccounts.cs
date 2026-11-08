using System.Net.Mail;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Kundenkonten anlegen, finden und mit dem gemeinsamen Kundenstamm verknüpfen.</summary>
internal sealed class CustomerAccounts(
    UserManager<CustomerAccount> users,
    OnlineDbContext online,
    AispaDbContext db,
    TimeProvider time)
{
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 &&
        MailAddress.TryCreate(email.Trim(), out var parsed) && parsed.Address == email.Trim();

    public string Normalize(string email) => users.NormalizeEmail(email.Trim());

    /// <summary>Konto zu einer bestätigten E-Mail-Adresse (Code oder Google/Apple); legt es beim ersten Mal an.</summary>
    public async Task<CustomerAccount> FindOrCreateAsync(string email, CancellationToken ct)
    {
        email = email.Trim();
        var account = await users.FindByEmailAsync(email);
        if (account is null)
        {
            account = new CustomerAccount { UserName = email, Email = email, EmailConfirmed = true, CreatedAt = time.GetUtcNow() };
            var created = await users.CreateAsync(account);
            if (!created.Succeeded)
            {
                // Gleichzeitige erste Anmeldung mit derselben Adresse: das andere Konto verwenden.
                account = await users.FindByEmailAsync(email)
                    ?? throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
            }
        }
        else if (!account.EmailConfirmed)
        {
            account.EmailConfirmed = true;
            await users.UpdateAsync(account);
        }

        await LinkExistingCustomerAsync(account, ct);
        return account;
    }

    /// <summary>
    /// Verknüpft das Konto mit dem Kunden, den die Rezeption unter dieser E-Mail-Adresse schon angelegt hat (der älteste, falls
    /// es mehrere gibt). Die Adresse ist bestätigt, deshalb darf das Konto dessen Termine sehen.
    /// </summary>
    public async Task LinkExistingCustomerAsync(CustomerAccount account, CancellationToken ct)
    {
        if (account.CustomerId is not null || account.Email is null)
        {
            return;
        }

        var customerId = await FindCustomerByEmailAsync(account.Email, ct);
        if (customerId is not null)
        {
            account.CustomerId = customerId;
            await users.UpdateAsync(account);
        }
    }

    public async Task<Guid?> FindCustomerByEmailAsync(string email, CancellationToken ct)
    {
        var pattern = Escape(email.Trim());
        return await db.Customers.AsNoTracking()
            .Where(c => c.Email != null && EF.Functions.ILike(c.Email, pattern))
            .OrderBy(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Legt einen Kunden im gemeinsamen Kundenstamm an (erste Online-Buchung, Profil).</summary>
    public async Task<Customer> CreateCustomerAsync(string firstName, string lastName, string email, string? phone, CancellationToken ct)
    {
        var customer = new Customer
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CreatedAt = time.GetUtcNow(),
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer;
    }

    /// <summary>
    /// Kunde für eine Online-Buchung oder einen Wartelisten-Eintrag. Mit Konto: der verknüpfte Kunde, sonst wird er aus
    /// <paramref name="contact"/> angelegt und verknüpft. Gast: die bestehende Kundenkartei mit dieser Adresse, damit die
    /// Rezeption eine Kundin nicht doppelt führt; der Gast sieht von dieser Kartei nichts.
    /// </summary>
    public async Task<(Guid CustomerId, string Email)> ResolveCustomerAsync(CustomerAccount? account, OnlineContact? contact, CancellationToken ct)
    {
        if (account is not null)
        {
            if (account.CustomerId is { } linked)
            {
                return (linked, account.Email!);
            }

            account.CustomerId = (await CreateCustomerAsync(contact!.FirstName, contact.LastName, account.Email!, contact.Phone, ct)).Id;
            await online.SaveChangesAsync(ct);
            return (account.CustomerId.Value, account.Email!);
        }

        var email = contact!.Email.Trim();
        var customerId = await FindCustomerByEmailAsync(email, ct)
            ?? (await CreateCustomerAsync(contact.FirstName, contact.LastName, email, contact.Phone, ct)).Id;
        return (customerId, email);
    }

    public async Task<CustomerSession?> SessionAsync(Guid accountId, CancellationToken ct)
    {
        var account = await online.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null)
        {
            return null;
        }

        var customer = account.CustomerId is { } id
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            : null;
        var passkeys = await online.UserPasskeys.CountAsync(p => p.UserId == accountId, ct);
        return new CustomerSession(account.Email ?? "", customer?.FirstName, customer?.LastName, customer?.Phone, customer is not null, passkeys);
    }

    public static void ValidateContact(Errors errors, OnlineContact? contact, bool requireEmail)
    {
        if (contact is null)
        {
            errors.Add(nameof(OnlineBookingRequest.Contact), "Bitte Name und E-Mail-Adresse angeben.");
            return;
        }

        errors.Require("contact.firstName", contact.FirstName);
        errors.Require("contact.lastName", contact.LastName);
        errors.Check((contact.FirstName?.Length ?? 0) <= 100 && (contact.LastName?.Length ?? 0) <= 100, "contact.lastName", "Höchstens 100 Zeichen.");
        if (requireEmail)
        {
            errors.Check(IsValidEmail(contact.Email), "contact.email", "Keine gültige E-Mail-Adresse.");
        }

        errors.Check((contact.Phone?.Length ?? 0) <= 40, "contact.phone", "Höchstens 40 Zeichen.");
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}

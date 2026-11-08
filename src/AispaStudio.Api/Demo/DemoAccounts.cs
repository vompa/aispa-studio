using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Testkonten für alle Rollen im Beispiel-Spa. Ohne Passkey: angemeldet wird in der Entwicklungsumgebung über
/// <c>/api/auth/dev/login</c> (Auswahl auf der Anmeldeseite); ein Passkey lässt sich danach wie gewohnt hinzufügen.
/// </summary>
internal static class DemoAccounts
{
    /// <summary>
    /// Unter der reservierten Domain .test (RFC 2606): Mails an diese Adressen erreichen nie jemanden. Eigene Subdomain, damit die
    /// Anmeldung ohne Passkey nur diese Konten trifft und nicht die Konten aus den Tests (…@aispa.test).
    /// </summary>
    public const string Domain = "demo.aispa.test";

    /// <summary>PIN für den Schnellwechsel am Empfangsgerät: alle Testkonten „1234“, Inhaberin und Manager „123456“ (Inhaber und Manager brauchen sechs Ziffern).</summary>
    public const string Pin = "1234";
    public const string OwnerPin = "123456";

    /// <summary><c>Days</c>: befristeter Zugang, endet so viele Tage nach dem Anlegen (Aushilfe).</summary>
    private sealed record Account(string DisplayName, string Mailbox, StaffRole Role, string[] Locations, int? Days = null);

    // Leitung und Empfang sind keine Behandelnden; Standorte über den Anfang des Namens („Wien“, „Salzburg“, „Graz“).
    private static readonly Account[] Office =
    [
        new("Katrin Aigner", "inhaberin", StaffRole.Owner, []),
        new("Susanne Leeb", "manager", StaffRole.GeneralManager, []),
        new("Gerhard Zehetner", "backoffice", StaffRole.Backoffice, []),
        new("Markus Steiner", "leitung.wien", StaffRole.Manager, ["Wien"]),
        new("Birgit Rainer", "leitung.salzburg", StaffRole.Manager, ["Salzburg", "Graz"]),
        new("Nina Kovač", "empfang.wien", StaffRole.Reception, ["Wien"]),
        new("Paul Brunner", "empfang.salzburg", StaffRole.Reception, ["Salzburg"]),
        new("Sarah Pölzl", "empfang.graz", StaffRole.Reception, ["Graz"]),
        new("Lisa Hofer", "aushilfe.wien", StaffRole.Reception, ["Wien"], Days: 60),
    ];

    public static string Email(string mailbox) => $"{mailbox}@{Domain}";

    /// <summary>Mailadresse eines Teammitglieds, z. B. „lena.huber@demo.aispa.test“.</summary>
    public static string EmployeeEmail(string displayName) => Email(string.Join('.', displayName.Split(' ').Select(DemoText.Ascii)));

    public static async Task AddAsync(IServiceProvider scoped, DateTimeOffset now, CancellationToken ct)
    {
        var db = scoped.GetRequiredService<AispaDbContext>();
        var users = scoped.GetRequiredService<UserManager<AispaUser>>();
        var locations = await db.Locations.AsNoTracking().ToListAsync(ct);
        var employees = await db.Employees.Include(e => e.WorkingHours).OrderBy(e => e.DisplayName).ToListAsync(ct);

        foreach (var account in Office)
        {
            var ids = locations.Where(l => account.Locations.Any(prefix => l.Name.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(l => l.Id).ToList();
            await AddUserAsync(db, users, account.DisplayName, Email(account.Mailbox), account.Role, ids, now, ct,
                account.Days is { } days ? now.AddDays(days) : null);
        }

        foreach (var employee in employees)
        {
            var ids = employee.WorkingHours.Select(w => w.LocationId).Distinct().ToList();
            employee.UserId = await AddUserAsync(db, users, employee.DisplayName, EmployeeEmail(employee.DisplayName),
                StaffRole.Therapist, ids, now, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<string> AddUserAsync(AispaDbContext db, UserManager<AispaUser> users, string name, string email,
        StaffRole role, List<Guid> locationIds, DateTimeOffset now, CancellationToken ct, DateTimeOffset? expiresAt = null)
    {
        var allLocations = role is StaffRole.Owner or StaffRole.GeneralManager or StaffRole.Backoffice;
        var user = new AispaUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Testkonto {email}: {string.Join(" ", created.Errors.Select(e => e.Description))}");
        }

        db.StaffAccess.Add(new StaffAccess
        {
            UserId = user.Id,
            Role = role,
            AllLocations = allLocations,
            LocationIds = allLocations ? [] : [.. locationIds],
            ExpiresAt = expiresAt,
            UpdatedAt = now,
        });
        db.Add(new StaffPin
        {
            UserId = user.Id,
            PinHash = users.PasswordHasher.HashPassword(user, Roles.IsOwnerLevel(role) ? OwnerPin : Pin),
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
}

using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Auth;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Access;

/// <summary>
/// Zugänge verwalten: Personen per Link einladen, Rolle und Standorte ändern, sperren.
/// Einladungen gehen als Mail an die eingeladene Adresse; der Link steht zusätzlich in der Antwort (zum Weitergeben per Messenger oder QR-Code).
/// </summary>
internal static class StaffEndpoints
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/staff").WithTags("Zugänge").RequirePermission(AccessRight.ManageStaff);

        group.MapGet("/", async (AccessService accessService, AispaDbContext db, UserManager<AispaUser> users, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var members = await (
                        from a in db.StaffAccess.AsNoTracking()
                        join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                        orderby u.DisplayName
                        select new { Access = a, User = u })
                    .ToListAsync(ct);
                var employees = await db.Employees.AsNoTracking().Where(e => e.UserId != null)
                    .ToDictionaryAsync(e => e.UserId!, e => e.Id, ct);
                var passkeys = await db.UserPasskeys.AsNoTracking().GroupBy(p => p.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

                return members
                    .Where(m => m.User.Id == access.UserId || access.CanManage(m.Access.Role, m.Access.AllLocations, m.Access.LocationIds))
                    .Select(m => new StaffMemberDto(m.User.Id, m.User.Email ?? "", m.User.DisplayName, m.Access.Role,
                        m.Access.AllLocations, m.Access.LocationIds, employees.TryGetValue(m.User.Id, out var e) ? e : null,
                        m.Access.IsActive, passkeys.GetValueOrDefault(m.User.Id), m.Access.ExpiresAt, m.User.UserName, m.User.MustChangePassword,
                        m.Access.AmountReports, m.Access.PlainReports))
                    .ToList();
            })
            .WithName("ListStaff")
            .WithSummary("Alle Zugänge, die die angemeldete Person verwalten darf (Manager und Standortleitung: nur ihre Standorte, keine Inhaber und Manager).");

        group.MapPut("/{userId}", async Task<Results<Ok<StaffMemberDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                string userId, StaffAccessInput input, AccessService accessService, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var target = await db.StaffAccess.FirstOrDefaultAsync(a => a.UserId == userId, ct);
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                if (target is null || user is null || !access.CanManage(target.Role, target.AllLocations, target.LocationIds))
                {
                    return TypedResults.NotFound();
                }

                // Wer sich selbst herabstuft oder sperrt, sperrt sich womöglich aus; den eigenen Zugang ändert deshalb jemand anderes.
                if (userId == access.UserId)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.",
                        detail: "Den eigenen Zugang ändert eine andere Person mit Verwaltungsrecht.");
                }

                var errors = await ValidateAsync(access, input.Role, input.AllLocations, input.LocationIds, input.EmployeeId, userId, db, ct);
                CheckExpiry(errors, input.Role, input.ExpiresAt, target.ExpiresAt, time.GetUtcNow());
                var amountReports = input.AmountReports && Roles.CanBeGranted(input.Role);
                CheckAmountReports(errors, access, amountReports, target.AmountReports);
                var plainReports = PlainReportsOf(input.Role, input.PlainReports);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                target.Role = input.Role;
                target.AmountReports = amountReports;
                target.PlainReports = plainReports;
                target.ExpiresAt = input.ExpiresAt;
                target.AllLocations = input.AllLocations;
                target.LocationIds = input.AllLocations ? [] : [.. input.LocationIds.Distinct()];
                target.IsActive = input.IsActive;
                target.UpdatedAt = time.GetUtcNow();
                await LinkEmployeeAsync(db, userId, input.EmployeeId, ct);
                await db.SaveChangesAsync(ct);

                var passkeys = await db.UserPasskeys.CountAsync(p => p.UserId == userId, ct);
                return TypedResults.Ok(new StaffMemberDto(userId, user.Email ?? "", user.DisplayName, target.Role, target.AllLocations,
                    target.LocationIds, input.EmployeeId, target.IsActive, passkeys, target.ExpiresAt, user.UserName, user.MustChangePassword,
                    target.AmountReports, target.PlainReports));
            })
            .WithName("UpdateStaffAccess")
            .WithSummary("Ändert Rolle, Standorte, Teamzuordnung, Befristung und das Recht „Berichte mit Beträgen“ (nur Inhaber und Manager) sowie bei Therapeut:innen „Auswertungen ohne Beträge“ "
                + "oder sperrt den Zugang (wirkt sofort).");

        // Einladungen per Link nur im Server-Betrieb; im Einzelplatz legt die Leitung Zugänge direkt an (MapStandaloneAccounts).
        var invitations = group.MapGroup("").RequireHosting(standalone: false);
        invitations.MapGet("/invitations", async (AccessService accessService, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var now = time.GetUtcNow();
                var open = await db.Invitations.AsNoTracking()
                    .Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync(ct);
                return open.Where(i => access.CanManage(i.Role, i.AllLocations, i.LocationIds)).Select(ToDto).ToList();
            })
            .WithName("ListInvitations")
            .WithSummary("Offene Einladungen (nicht angenommen, nicht zurückgezogen, nicht abgelaufen).");

        invitations.MapPost("/invitations", async Task<Results<Created<InvitationCreated>, ValidationProblem>> (
                InvitationInput input, AccessService accessService, AispaDbContext db, TenantContext tenants, TimeProvider time, IMessageBus bus,
                IOptions<OnlineBookingOptions> online, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                // Klein geschrieben gespeichert, damit „Anna@…“ und „anna@…“ dieselbe Einladung sind.
                var email = input.Email?.Trim().ToLowerInvariant() ?? "";
                var errors = await ValidateAsync(access, input.Role, input.AllLocations, input.LocationIds, input.EmployeeId, null, db, ct);
                errors.Require(nameof(input.DisplayName), input.DisplayName);
                errors.Check(email.Contains('@'), nameof(input.Email), "Bitte eine E-Mail-Adresse angeben.");
                CheckExpiry(errors, input.Role, input.AccessExpiresAt, null, time.GetUtcNow());
                var amountReports = input.AmountReports && Roles.CanBeGranted(input.Role);
                CheckAmountReports(errors, access, amountReports, false);
                var plainReports = PlainReportsOf(input.Role, input.PlainReports);
                var normalized = email.ToUpperInvariant();
                errors.Check(!await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct), nameof(input.Email),
                    "Für diese E-Mail gibt es schon ein Konto.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Eine neue Einladung ersetzt eine noch offene an dieselbe Adresse.
                var now = time.GetUtcNow();
                await db.Invitations
                    .Where(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.RevokedAt, now), ct);

                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var invitation = new Invitation
                {
                    TokenHash = Hash(token),
                    Email = email,
                    DisplayName = input.DisplayName.Trim(),
                    Role = input.Role,
                    AllLocations = input.AllLocations,
                    LocationIds = input.AllLocations ? [] : [.. input.LocationIds.Distinct()],
                    EmployeeId = input.EmployeeId,
                    AccessExpiresAt = input.AccessExpiresAt,
                    AmountReports = amountReports,
                    PlainReports = plainReports,
                    CreatedByUserId = access.UserId,
                    CreatedAt = now,
                    ExpiresAt = now + InvitationLifetime,
                };
                db.Invitations.Add(invitation);
                await db.SaveChangesAsync(ct);
                var link = $"{online.Value.PublicUrl.TrimEnd('/')}/anmelden/?einladung={Uri.EscapeDataString(token)}";
                await SendInvitationMailAsync(bus, db, tenants, invitation, link, ct);
                return TypedResults.Created($"/api/staff/invitations/{invitation.Id}", new InvitationCreated(ToDto(invitation), token, link));
            })
            .WithName("CreateInvitation")
            .WithSummary("Erstellt einen Einladungslink (7 Tage gültig, einmal verwendbar) und schickt die Einladung per Mail. Der Token steht nur in dieser Antwort.");

        invitations.MapDelete("/invitations/{invitationId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid invitationId, AccessService accessService, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.AcceptedAt == null && i.RevokedAt == null, ct);
                if (invitation is null || !access.CanManage(invitation.Role, invitation.AllLocations, invitation.LocationIds))
                {
                    return TypedResults.NotFound();
                }

                invitation.RevokedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("RevokeInvitation");

        MapAcceptance(routes);
        MapStandaloneAccounts(group);
        return routes;
    }

    /// <summary>Schickt die Einladung über die Queue (nie im HTTP-Aufruf) an die eingeladene Adresse.</summary>
    private static async Task SendInvitationMailAsync(
        IMessageBus bus, AispaDbContext db, TenantContext tenants, Invitation invitation, string link, CancellationToken ct)
    {
        // Tenant hat keinen Mandantenfilter: ohne Where stünde hier der Name irgendeines Spas.
        var spa = await db.Tenants.AsNoTracking().Where(t => t.Id == tenants.TenantId).Select(t => t.Name).FirstAsync(ct);
        await bus.PublishAsync(new SendMail(new OutgoingMail(invitation.Email,
            $"Einladung zu {spa}",
            $"""
            Hallo {invitation.DisplayName},

            du wurdest zu AISPA Studio bei {spa} eingeladen, als {RoleLabel(invitation.Role)}.

            Mit diesem Link legst du deinen Passkey an und meldest dich an:
            {link}

            Der Link gilt 7 Tage und nur einmal.
            """,
            FromName: spa)));
    }

    private static string RoleLabel(StaffRole role) => role switch
    {
        StaffRole.Owner => "Inhaber:in",
        StaffRole.GeneralManager => "Manager:in",
        StaffRole.Manager => "Standortleitung",
        StaffRole.Reception => "Rezeption",
        StaffRole.Therapist => "Therapeut:in",
        StaffRole.Backoffice => "Backoffice",
        _ => role.ToString(),
    };

    /// <summary>
    /// Einzelplatz-Betrieb (ADR 0041): Zugang mit Benutzername und Startpasswort anlegen und Passwort zurücksetzen.
    /// Das Startpasswort muss die Person bei der ersten Anmeldung ändern.
    /// </summary>
    private static void MapStandaloneAccounts(RouteGroupBuilder staff)
    {
        var group = staff.MapGroup("").RequireHosting(standalone: true);

        group.MapPost("/accounts", async Task<Results<Created<StaffMemberDto>, ValidationProblem>> (
                StaffAccountInput input, AccessService accessService, AispaDbContext db, UserManager<AispaUser> users, TimeProvider time,
                CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var errors = await ValidateAsync(access, input.Role, input.AllLocations, input.LocationIds, input.EmployeeId, null, db, ct);
                errors.Require(nameof(input.DisplayName), input.DisplayName);
                errors.Require(nameof(input.UserName), input.UserName);
                CheckExpiry(errors, input.Role, input.ExpiresAt, null, time.GetUtcNow());
                var amountReports = input.AmountReports && Roles.CanBeGranted(input.Role);
                CheckAmountReports(errors, access, amountReports, false);
                var plainReports = PlainReportsOf(input.Role, input.PlainReports);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var user = new AispaUser { UserName = input.UserName.Trim(), DisplayName = input.DisplayName.Trim(), MustChangePassword = true };
                var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    var created = await users.CreateAsync(user, input.Password ?? "");
                    if (!created.Succeeded)
                    {
                        return created;
                    }

                    db.StaffAccess.Add(new StaffAccess
                    {
                        UserId = user.Id,
                        Role = input.Role,
                        AllLocations = input.AllLocations,
                        LocationIds = input.AllLocations ? [] : [.. input.LocationIds.Distinct()],
                        ExpiresAt = input.Role == StaffRole.Owner ? null : input.ExpiresAt,
                        AmountReports = amountReports,
                        PlainReports = plainReports,
                        UpdatedAt = time.GetUtcNow(),
                    });
                    await LinkEmployeeAsync(db, user.Id, input.EmployeeId, ct);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return created;
                });
                if (!result.Succeeded)
                {
                    return StandaloneAuth.Problem(result);
                }

                return TypedResults.Created($"/api/staff/{user.Id}", new StaffMemberDto(user.Id, "", user.DisplayName, input.Role, input.AllLocations,
                    input.AllLocations ? [] : [.. input.LocationIds.Distinct()], input.EmployeeId, true, 0,
                    input.Role == StaffRole.Owner ? null : input.ExpiresAt, user.UserName, true, amountReports, plainReports));
            })
            .WithName("CreateStaffAccount")
            .WithSummary("Einzelplatz-Betrieb: legt einen Zugang mit Benutzername und Startpasswort an. Die Person ändert das Passwort bei der ersten Anmeldung.");

        group.MapPost("/{userId}/password", async Task<Results<NoContent, ValidationProblem, NotFound, ProblemHttpResult>> (
                string userId, PasswordReset reset, AccessService accessService, AispaDbContext db, UserManager<AispaUser> users, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var target = await db.StaffAccess.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
                var user = await users.FindByIdAsync(userId);
                if (target is null || user is null || !access.CanManage(target.Role, target.AllLocations, target.LocationIds))
                {
                    return TypedResults.NotFound();
                }

                if (userId == access.UserId)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.",
                        detail: "Das eigene Passwort änderst du über „Passwort ändern“.");
                }

                var result = await users.RemovePasswordAsync(user);
                if (result.Succeeded)
                {
                    result = await users.AddPasswordAsync(user, reset.NewPassword ?? "");
                }

                if (!result.Succeeded)
                {
                    return StandaloneAuth.Problem(result);
                }

                user.MustChangePassword = true;
                await users.UpdateAsync(user);
                // Sperre nach Fehlversuchen aufheben und laufende Anmeldungen beenden: das alte Passwort gilt nicht mehr.
                await users.ResetAccessFailedCountAsync(user);
                await users.SetLockoutEndDateAsync(user, null);
                await users.UpdateSecurityStampAsync(user);
                return TypedResults.NoContent();
            })
            .WithName("ResetStaffPassword")
            .WithSummary("Einzelplatz-Betrieb: neues Startpasswort für eine Person, die ihres vergessen hat; sie ändert es bei der nächsten Anmeldung.");
    }

    /// <summary>Ohne Anmeldung erreichbar: der Link selbst ist der Nachweis.</summary>
    private static void MapAcceptance(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth/invitations").WithTags("Anmeldung").RequireHosting(standalone: false);

        group.MapGet("/{token}", async Task<Results<Ok<InvitationPreview>, NotFound>> (
                string token, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var invitation = await FindOpenAsync(db, token, time, ct);
                if (invitation is null)
                {
                    return TypedResults.NotFound();
                }

                tenants.Use(db.TenantOf(invitation));

                var names = invitation.AllLocations
                    ? ["Alle Standorte"]
                    : await db.Locations.Where(l => invitation.LocationIds.Contains(l.Id)).OrderBy(l => l.Name).Select(l => l.Name).ToListAsync(ct);
                return TypedResults.Ok(new InvitationPreview(invitation.Email, invitation.DisplayName, invitation.Role, names, invitation.ExpiresAt));
            })
            .WithName("GetInvitation")
            .WithSummary("Zeigt, wofür eine Einladung gilt. 404, wenn sie abgelaufen, zurückgezogen oder schon angenommen ist.");

        group.MapPost("/{token}/accept", async Task<Results<Ok<CurrentUser>, NotFound, ValidationProblem>> (
                string token, AispaDbContext db, UserManager<AispaUser> users, SignInManager<AispaUser> signIn,
                AccessService accessService, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                var invitation = await FindOpenAsync(db, token, time, ct);
                if (invitation is null)
                {
                    return TypedResults.NotFound();
                }

                // Ohne Anmeldung gibt es noch keinen Mandanten; er kommt aus der Einladung.
                tenants.Use(db.TenantOf(invitation));

                // Konto, Zugang und „angenommen“ gemeinsam oder gar nicht; mit Wiederholungsstrategie nur über ExecuteAsync.
                var user = new AispaUser { UserName = invitation.Email, Email = invitation.Email, DisplayName = invitation.DisplayName };
                var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    var created = await users.CreateAsync(user);
                    if (!created.Succeeded)
                    {
                        return created;
                    }

                    var now = time.GetUtcNow();
                    db.StaffAccess.Add(new StaffAccess
                    {
                        UserId = user.Id,
                        Role = invitation.Role,
                        AllLocations = invitation.AllLocations,
                        LocationIds = invitation.LocationIds,
                        ExpiresAt = invitation.AccessExpiresAt,
                        AmountReports = invitation.AmountReports,
                        PlainReports = invitation.PlainReports,
                        UpdatedAt = now,
                    });
                    invitation.AcceptedAt = now;
                    invitation.AcceptedByUserId = user.Id;
                    await LinkEmployeeAsync(db, user.Id, invitation.EmployeeId, ct);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return created;
                });
                if (!result.Succeeded)
                {
                    return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                // Angemeldet, damit gleich im Anschluss der Passkey angelegt werden kann.
                await signIn.SignInAsync(user, isPersistent: false);
                return TypedResults.Ok(await accessService.ToCurrentUserAsync(user, users, ct));
            })
            .WithName("AcceptInvitation")
            .WithSummary("Legt das Konto an und meldet an. Danach im selben Schritt einen Passkey hinzufügen.");
    }

    /// <summary>Gemeinsame Prüfung für Einladung und Änderung: darf die Person diese Rolle an diesen Standorten vergeben?</summary>
    private static async Task<Errors> ValidateAsync(UserAccess access, StaffRole role, bool allLocations, IReadOnlyList<Guid>? locationIds,
        Guid? employeeId, string? userId, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        var ids = (locationIds ?? []).Distinct().ToList();
        errors.Check(Enum.IsDefined(role), nameof(StaffAccessInput.Role), "Unbekannte Rolle.");
        errors.Check(allLocations || ids.Count > 0, nameof(StaffAccessInput.LocationIds), "Mindestens einen Standort wählen.");
        errors.Check(allLocations || await db.Locations.CountAsync(l => ids.Contains(l.Id), ct) == ids.Count,
            nameof(StaffAccessInput.LocationIds), "Unbekannter Standort.");
        errors.Check(access.CanManage(role, allLocations, ids), nameof(StaffAccessInput.Role),
            access.IsOwner ? "Nicht erlaubt." : Roles.IsOwnerLevel(role)
                ? "Inhaber- und Manager-Zugänge legt nur der Inhaber an, ändert und entfernt sie."
                : "Du vergibst Rollen nur für deine eigenen Standorte.");
        errors.Check(role != StaffRole.Therapist || employeeId is not null, nameof(StaffAccessInput.EmployeeId),
            "Für Therapeut:innen bitte die Person im Team wählen, damit ihre eigenen Termine erkannt werden.");
        // Buchhaltung und Lohnverrechnung (Exporte, Lohn) gelten fürs ganze Unternehmen, nicht für einzelne Standorte.
        errors.Check(role != StaffRole.Backoffice || allLocations, nameof(StaffAccessInput.AllLocations),
            "Das Backoffice arbeitet für alle Standorte.");
        if (employeeId is { } id)
        {
            var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.IsActive, ct);
            errors.Check(employee is not null, nameof(StaffAccessInput.EmployeeId), "Unbekannte Person im Team.");
            errors.Check(employee?.UserId is null || employee.UserId == userId, nameof(StaffAccessInput.EmployeeId),
                "Diese Person im Team hat schon einen eigenen Zugang.");
        }

        return errors;
    }

    /// <summary>Das Recht „Berichte mit Beträgen“ vergeben und entziehen nur Inhaber und Manager: sonst könnte sich eine Standortleitung die Umsätze selbst freigeben.</summary>
    private static void CheckAmountReports(Errors errors, UserAccess access, bool requested, bool current) =>
        errors.Check(requested == current || access.IsOwnerLevel, nameof(StaffAccessInput.AmountReports),
            "Das Recht „Berichte mit Beträgen“ vergibt oder entzieht nur der Inhaber oder ein Manager.");

    /// <summary>
    /// „Auswertungen ohne Beträge“ im Assistenten lässt sich nur der Rolle Therapeut:in abschalten (Ä55); bei den anderen Rollen bleibt es an und wird nicht anders gespeichert. Wer den Zugang
    /// verwalten darf (Inhaber, Manager, Standortleitung für ihre Standorte), darf es auch umstellen: es gibt dabei keine Beträge freizugeben.
    /// </summary>
    private static bool PlainReportsOf(StaffRole role, bool requested) => role != StaffRole.Therapist || requested;

    private static async Task LinkEmployeeAsync(AispaDbContext db, string userId, Guid? employeeId, CancellationToken ct)
    {
        // Sofort ausgeführt, damit der eindeutige Index auf UserId beim Umhängen nicht kurz zwei Einträge sieht.
        await db.Employees.Where(e => e.UserId == userId && e.Id != employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, (string?)null), ct);

        if (employeeId is { } id && await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct) is { } employee)
        {
            employee.UserId = userId;
        }
    }

    private static Task<Invitation?> FindOpenAsync(AispaDbContext db, string token, TimeProvider time, CancellationToken ct)
    {
        var hash = Hash(token);
        var now = time.GetUtcNow();
        return db.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TokenHash == hash && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now, ct);
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static InvitationDto ToDto(Invitation i) =>
        new(i.Id, i.Email, i.DisplayName, i.Role, i.AllLocations, i.LocationIds, i.EmployeeId, i.CreatedAt, i.ExpiresAt, i.AccessExpiresAt, i.AmountReports, i.PlainReports);

    /// <summary>
    /// Befristung: Inhaber nie (sonst kann sich das Spa aussperren), sonst höchstens zwei Jahre. Ein neuer Zeitpunkt muss in der Zukunft liegen;
    /// eine schon abgelaufene Befristung darf unverändert gespeichert werden.
    /// </summary>
    private static void CheckExpiry(Errors errors, StaffRole role, DateTimeOffset? expiresAt, DateTimeOffset? current, DateTimeOffset now)
    {
        if (expiresAt is not { } at)
        {
            return;
        }

        errors.Check(role != StaffRole.Owner, nameof(StaffAccessInput.ExpiresAt), "Inhaber-Zugänge sind nicht befristet.");
        errors.Check(at == current || at > now, nameof(StaffAccessInput.ExpiresAt), "Das Ende der Befristung muss in der Zukunft liegen.");
        errors.Check(at <= now.AddYears(2), nameof(StaffAccessInput.ExpiresAt), "Höchstens zwei Jahre befristen; länger bitte unbefristet.");
    }
}

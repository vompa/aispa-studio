using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Kundenstamm, gemeinsam für alle Standorte.</summary>
internal static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/", async (string? search, AispaDbContext db, CancellationToken ct) =>
            {
                var query = db.Customers.AsNoTracking();
                // Jedes Suchwort muss in Vorname, Rufname, Nachname, E-Mail oder Telefon vorkommen: „anna muster“, „0664“ usw.
                // Titel („Dr.“, „MSc“) sind keine Suchwörter, damit „Dr. Huber“ die Kundin trotzdem findet.
                foreach (var term in PersonNames.WithoutTitles(search).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var pattern = $"%{term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
                    query = query.Where(c =>
                        EF.Functions.ILike(c.FirstName, pattern) ||
                        (c.PreferredName != null && EF.Functions.ILike(c.PreferredName, pattern)) ||
                        EF.Functions.ILike(c.LastName, pattern) ||
                        (c.Email != null && EF.Functions.ILike(c.Email, pattern)) ||
                        db.CustomerEmails.Any(e => e.CustomerId == c.Id && EF.Functions.ILike(e.Address, pattern)) ||
                        (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)));
                }

                return (await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).Take(25).ToListAsync(ct))
                    .Select(ToDto).ToList();
            })
            .WithName("SearchCustomers")
            .WithSummary("Sucht im gemeinsamen Kundenstamm (höchstens 25 Treffer).");

        group.MapPost("/duplicates", async (DuplicateCheckInput input, CustomerMatcher matcher, CancellationToken ct) =>
            {
                // Angaben im Rumpf statt in der Adresse: Name, Mail und Telefon gehören nicht in URLs und Protokolle.
                var match = await matcher.MatchAsync(new CustomerMatchRequest(Blank(input.Email), Blank(input.Phone), null,
                    Blank(input.FirstName), Blank(input.LastName), input.BirthDate), ct);
                return match.Everyone;
            })
            .WithName("FindCustomerDuplicates")
            .WithSummary("Mögliche Dubletten zu den Angaben eines neuen Kunden (höchstens 3, mit Stufe und Grund): gleiche Mailadresse oder Nummer, "
                + "gleicher oder ähnlicher Name. Dieselbe Erkennung wie bei eingehenden Nachrichten.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/{customerId:guid}", async Task<Results<Ok<CustomerDto>, NotFound>> (
                Guid customerId, AispaDbContext db, CancellationToken ct) =>
                await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct) is { } customer
                    ? TypedResults.Ok(ToDto(customer))
                    : TypedResults.NotFound())
            .WithName("GetCustomer");

        group.MapPost("/", async Task<Results<Created<CustomerDto>, ValidationProblem>> (
                CustomerInput input, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
            {
                // Pflicht ist nur ein Name, damit die Rezeption auch „Frau Huber“ am Telefon sofort anlegen kann.
                var firstName = input.FirstName?.Trim() ?? "";
                var lastName = input.LastName?.Trim() ?? "";
                var errors = new Errors();
                errors.Check(firstName.Length > 0 || lastName.Length > 0, nameof(input.LastName), "Vor- oder Nachname angeben.");
                errors.Check(Blank(input.Email) is not { } email || email.Contains('@', StringComparison.Ordinal), nameof(input.Email), "Keine gültige E-Mail-Adresse.");
                errors.Check(KnownLanguage(input.Language), nameof(input.Language), "Unbekannte Sprache.");
                CheckNameDetails(errors, input);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Wiederholtes Anlegen mit derselben Id (vorgemerkt ohne Verbindung, Abgleich nach Abbruch): bestehende Kundin zurückgeben.
                if (input.ClientId is { } clientId)
                {
                    if (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct) is { } existing)
                    {
                        return TypedResults.Created($"/api/customers/{existing.Id}", ToDto(existing));
                    }

                    if (clientId == Guid.Empty || await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id == clientId, ct))
                    {
                        errors.Check(false, nameof(input.ClientId), "Diese Id ist nicht verwendbar.");
                        return errors.Problem();
                    }
                }

                var customer = new Customer
                {
                    Id = input.ClientId ?? Guid.CreateVersion7(),
                    FirstName = firstName,
                    LastName = lastName,
                    Email = Blank(input.Email),
                    Phone = await StoredPhoneAsync(input.Phone, db, tenants, ct),
                    BirthDate = input.BirthDate,
                    Note = Blank(input.Note),
                    Language = GuestLanguages.Normalize(input.Language),
                    CreatedAt = time.GetUtcNow(),
                };
                ApplyNameDetails(customer, input);
                db.Customers.Add(customer);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/customers/{customer.Id}", ToDto(customer));
            })
            .WithName("CreateCustomer")
            .RequirePermission(AccessRight.Book);

        group.MapPut("/{customerId:guid}", async Task<Results<Ok<CustomerDto>, NotFound, ValidationProblem>> (
                Guid customerId, CustomerInput input, AispaDbContext db, TenantContext tenants, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var firstName = input.FirstName?.Trim() ?? "";
                var lastName = input.LastName?.Trim() ?? "";
                var errors = new Errors();
                errors.Check(firstName.Length > 0 || lastName.Length > 0, nameof(input.LastName), "Vor- oder Nachname angeben.");
                errors.Check(Blank(input.Email) is not { } email || email.Contains('@', StringComparison.Ordinal), nameof(input.Email), "Keine gültige E-Mail-Adresse.");
                errors.Check(KnownLanguage(input.Language), nameof(input.Language), "Unbekannte Sprache.");
                CheckNameDetails(errors, input);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Der gültige Name steht auf Belegen: jede Änderung wird protokolliert; ausgestellte Belege behalten den alten Namen.
                var newName = PersonNames.Legal(firstName, lastName);
                if (newName != customer.LegalName)
                {
                    db.CustomerNameChanges.Add(new CustomerNameChangeEntry
                    {
                        CustomerId = customer.Id,
                        OldName = customer.LegalName,
                        NewName = newName,
                        ChangedAt = time.GetUtcNow(),
                        ChangedByUserId = (await accessService.CurrentAsync(ct))?.UserId,
                    });
                }

                // Leere Felder löschen den gespeicherten Wert, wie im Formular angezeigt.
                customer.FirstName = firstName;
                customer.LastName = lastName;
                customer.Email = Blank(input.Email);
                customer.Phone = await StoredPhoneAsync(input.Phone, db, tenants, ct);
                customer.BirthDate = input.BirthDate;
                customer.Note = Blank(input.Note);
                customer.Language = GuestLanguages.Normalize(input.Language);
                ApplyNameDetails(customer, input);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(customer));
            })
            .WithName("UpdateCustomer")
            .WithSummary("Ändert Name, Anrede, Rufname, Titel, Postanschrift, Kontaktdaten, Geburtstag, Notiz und Sprache einer Kundin; leere Felder "
                + "löschen den Wert. Änderungen am gültigen Namen (Vor- und Nachname) werden protokolliert.")
            .RequirePermission(AccessRight.Book);

        group.MapPut("/{customerId:guid}/billing", async Task<Results<Ok<CustomerDto>, NotFound, ValidationProblem>> (
                Guid customerId, CustomerBilling? input, AispaDbContext db, CancellationToken ct) =>
            {
                if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var name = Blank(input?.Name);
                var address = Blank(input?.Address);
                var vatId = Blank(input?.VatId)?.ToUpperInvariant();
                var errors = new Errors();
                // Leerer Name und leere Anschrift löschen den gespeicherten Empfänger.
                if (name is not null || address is not null || vatId is not null)
                {
                    errors.Check(name is { Length: <= 200 }, nameof(CustomerBilling.Name), "Name oder Firma des Empfängers (höchstens 200 Zeichen).");
                    errors.Check(address is { Length: <= 300 }, nameof(CustomerBilling.Address), "Anschrift des Empfängers (höchstens 300 Zeichen).");
                    errors.Check(vatId is not { Length: > 20 }, nameof(CustomerBilling.VatId), "UID-Nummer höchstens 20 Zeichen.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                customer.BillingName = name;
                customer.BillingAddress = name is null ? null : address;
                customer.BillingVatId = name is null ? null : vatId;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(customer));
            })
            .WithName("SetCustomerBilling")
            .WithSummary("Speichert den Rechnungsempfänger einer Kundin (Firma, Anschrift, UID) oder löscht ihn mit leerem Inhalt.")
            .RequirePermission(AccessRight.Book);

        group.MapPut("/{customerId:guid}/discount", async Task<Results<Ok<CustomerDto>, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid customerId, CustomerDiscountInput input, AispaDbContext db, TenantContext tenants, AccessService access,
                UserManager<AispaUser> users, TimeProvider time, CancellationToken ct) =>
            {
                if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(input.Percent is >= 0 and < 100 && decimal.Round(input.Percent, 2) == input.Percent, nameof(input.Percent),
                    "Rabatt in Prozent zwischen 0 und 99,99.");
                errors.Check(Enum.IsDefined(input.Scope), nameof(input.Scope), "Unbekannter Geltungsbereich.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Bis zur Rabattgrenze des Spas (Standard 20 %) darf die Rezeption, darüber nur die Kassenaufsicht.
                // Darüber gibt die Leitung am selben Platz mit ihrer PIN frei.
                var limit = await db.Tenants.Where(t => t.Id == tenants.TenantId).Select(t => t.DiscountLimitPercent).FirstOrDefaultAsync(ct);
                string? approvedBy = null;
                if (input.Percent > limit && (await access.CurrentAsync(ct))?.Has(AccessRight.SuperviseCash) != true)
                {
                    if (input.Approval is null)
                    {
                        return TypedResults.Problem($"Rabatte über {PosService.PercentText(limit)} % gibt die Standortleitung oder die Inhaberin.",
                            statusCode: StatusCodes.Status403Forbidden);
                    }

                    (approvedBy, var error) = await QuickSwitch.ApproveAsync(input.Approval, null, db, users, time.GetUtcNow(), ct);
                    if (approvedBy is null)
                    {
                        return TypedResults.Problem(error, statusCode: StatusCodes.Status403Forbidden);
                    }
                }

                customer.DiscountPercent = input.Percent == 0 ? null : input.Percent;
                customer.DiscountScope = input.Percent == 0 ? CustomerDiscountScope.All : input.Scope;
                customer.DiscountApprovedByUserId = approvedBy;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(customer));
            })
            .WithName("SetCustomerDiscount")
            .WithSummary("Speichert den dauerhaften Rabatt einer Kundin in Prozent; die Kassa zieht ihn bei neuen Positionen ab. 0 nimmt ihn zurück. Über der Rabattgrenze des Spas nur mit Kassenaufsicht.")
            .RequirePermission(AccessRight.Book);

        group.MapPut("/{customerId:guid}/booking-preferences", async Task<Results<Ok<CustomerDto>, NotFound, ValidationProblem>> (
                Guid customerId, CustomerBookingPreferences input, AispaDbContext db, CancellationToken ct) =>
            {
                if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(input.EmployeeId is null || await db.Employees.AnyAsync(e => e.Id == input.EmployeeId, ct),
                    nameof(input.EmployeeId), "Diese Person gibt es nicht.");
                errors.Check(!input.EmployeeFixed || input.EmployeeId is not null, nameof(input.EmployeeFixed), "Fest geht nur mit einer Person.");
                errors.Check(input is not { From: { } from, Until: { } until } || from < until, nameof(input.Until), "„Bis“ muss nach „ab“ liegen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                customer.PreferredEmployeeId = input.EmployeeId;
                customer.PreferredEmployeeFixed = input.EmployeeFixed;
                customer.PreferredWeekdays = (input.Weekdays ?? []).Distinct().Aggregate(0, (mask, day) => mask | (1 << (int)day));
                customer.PreferredFrom = input.From;
                customer.PreferredUntil = input.Until;
                customer.PreferredGender = input.Gender;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(customer));
            })
            .WithName("SetCustomerBookingPreferences")
            .WithSummary("Speichert, wann und bei wem eine Kundin gebucht werden möchte: Therapeut:in (bevorzugt oder fest), Therapeutin oder Therapeut, Wochentage, Uhrzeiten. "
                + "Ein leerer Inhalt löscht es.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/{customerId:guid}/booking-preferences/suggestion", async Task<Results<Ok<CustomerPreferenceSuggestion>, NotFound>> (
                Guid customerId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct)
                    ? TypedResults.Ok(await CustomerPreferenceHistory.SuggestAsync(db, customerId, time.GetUtcNow(), ct))
                    : TypedResults.NotFound())
            .WithName("SuggestCustomerBookingPreferences")
            .WithSummary("Schlägt Vorlieben aus den erledigten Besuchen der letzten zwei Jahre vor (z. B. „4 von 5 Besuchen bei Lena“).");

        group.MapGet("/{customerId:guid}/insights", async Task<Results<Ok<CustomerInsights>, NotFound>> (
                Guid customerId, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
                await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct)
                    ? TypedResults.Ok(await CustomerInsightsService.GetAsync(db, (await accessService.CurrentAsync(ct))!, customerId, time.GetUtcNow(), ct))
                    : TypedResults.NotFound())
            .WithName("GetCustomerInsights")
            .WithSummary("Kennzahlen der Kundenkarte aus Kalender und Kassenbelegen: Besuche, üblicher Abstand, Sterne, nicht erschienen, späte Absagen, "
                + "Besuche je Person, Pakete. Umsatz nur mit „Zahlen sehen“, Rezeptionsvermerk nur mit dem Recht zu buchen.");

        group.MapGet("/{customerId:guid}/summary", async Task<Results<Ok<CustomerSummary>, NotFound>> (
                Guid customerId, AispaDbContext db, OnlineDbContext online, AccessService accessService, TimeProvider time, CancellationToken ct) =>
                await CustomerSummaryService.GetAsync(db, online, (await accessService.CurrentAsync(ct))!, customerId, time.GetUtcNow(), ct) is { } summary
                    ? TypedResults.Ok(summary)
                    : TypedResults.NotFound())
            .WithName("GetCustomerSummary")
            .WithSummary("Kundenüberblick für das Termin-Panel: Besuche, letzter Besuch, nicht erschienen, nächster Termin, Kundennotiz, die letzten Behandlungen, "
                + "Kontakt und der Stand der Anamnese (nie Inhalte). Offener Betrag und Pakete nur mit „Berichte mit Beträgen“.");

        group.MapPut("/{customerId:guid}/reception-note", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid customerId, CustomerReceptionNoteInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check((input.Text ?? "").Trim().Length <= 1000, nameof(input.Text), "Höchstens 1000 Zeichen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is not { } customer)
                {
                    return TypedResults.NotFound();
                }

                customer.ReceptionNote = Blank(input.Text);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("SetCustomerReceptionNote")
            .WithSummary("Rezeptionsvermerk: Organisatorisches wie „zahlt mit Firmenkarte“, nur für Rezeption und Leitung. Keine Gesundheitsdaten.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/{customerId:guid}/appointments", async (Guid customerId, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var visible = access.LocationIds.ToList();
                return (await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                        .Where(a => a.CustomerId == customerId && (access.AllLocations || visible.Contains(a.LocationId)))
                        .OrderByDescending(a => a.Start)
                        .Take(100)
                        .ToListAsync(ct))
                    .Select(a => BookingService.ToDto(a, a.Treatment.Name)).ToList();
            })
            .WithName("ListCustomerAppointments")
            .WithSummary("Termine eines Kunden an allen Standorten mit Zugang, neueste zuerst. Der Kundenstamm selbst ist standortübergreifend.");

        return routes;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Die Nummer im Format „+43 664 1234567“ (Vorwahl des Landes des Spas); was sich nicht als Nummer lesen lässt, bleibt wie getippt.</summary>
    private static async Task<string?> StoredPhoneAsync(string? typed, AispaDbContext db, TenantContext tenants, CancellationToken ct)
    {
        if (Blank(typed) is not { } phone)
        {
            return null;
        }

        var country = await db.Tenants.AsNoTracking().Where(t => t.Id == tenants.TenantId).Select(t => t.Country).FirstOrDefaultAsync(ct);
        return PhoneNumbers.Format(phone, country) is { Length: > 0 } formatted ? formatted : phone;
    }

    private static void CheckNameDetails(Errors errors, CustomerInput input)
    {
        errors.Check(Enum.IsDefined(input.Salutation), nameof(input.Salutation), "Unbekannte Anrede.");
        errors.Check((input.PreferredName ?? "").Trim().Length <= 100, nameof(input.PreferredName), "Rufname höchstens 100 Zeichen.");
        errors.Check((input.Pronouns ?? "").Trim().Length <= 40, nameof(input.Pronouns), "Pronomen höchstens 40 Zeichen.");
        errors.Check((PersonNames.NormalizeTitles(input.TitlesBefore, after: false) ?? "").Length <= 100, nameof(input.TitlesBefore), "Titel höchstens 100 Zeichen.");
        errors.Check((PersonNames.NormalizeTitles(input.TitlesAfter, after: true) ?? "").Length <= 100, nameof(input.TitlesAfter), "Titel höchstens 100 Zeichen.");
        errors.Check(Blank(input.Address?.Country) is not { } country || (country.Length == 2 && country.All(char.IsAsciiLetter)), "Address.Country",
            "Land als Kürzel mit zwei Buchstaben, z. B. AT.");
        errors.Check((input.Address?.Street ?? "").Trim().Length <= 200 && (input.Address?.City ?? "").Trim().Length <= 100
            && (input.Address?.PostalCode ?? "").Trim().Length <= 16, "Address", "Anschrift zu lang.");
    }

    /// <summary>Anrede, Rufname, Pronomen, Titel und Postanschrift; leere Felder löschen den Wert.</summary>
    private static void ApplyNameDetails(Customer customer, CustomerInput input)
    {
        customer.Salutation = input.Salutation;
        customer.PreferredName = Blank(input.PreferredName);
        customer.Pronouns = Blank(input.Pronouns);
        customer.TitlesBefore = PersonNames.NormalizeTitles(input.TitlesBefore, after: false);
        customer.TitlesAfter = PersonNames.NormalizeTitles(input.TitlesAfter, after: true);
        customer.TitleInGreeting = input.TitleInGreeting;
        customer.Street = Blank(input.Address?.Street);
        customer.PostalCode = Blank(input.Address?.PostalCode);
        customer.City = Blank(input.Address?.City);
        customer.Country = Blank(input.Address?.Country)?.ToUpperInvariant();
    }

    public static CustomerDto ToDto(Customer c) => new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.BirthDate, c.Note,
        c is { BillingName: { } name, BillingAddress: { } address } ? new CustomerBilling(name, address, c.BillingVatId) : null,
        c.DiscountPercent is { } percent ? new CustomerDiscount(percent, c.DiscountScope) : null,
        BookingPreferences(c), c.Language, c.CreatedFromChannel?.ToString(), c.AssignmentOpen,
        c.Salutation, c.PreferredName, c.TitlesBefore, c.TitlesAfter, c.TitleInGreeting,
        c is { Street: null, PostalCode: null, City: null, Country: null } ? null : new CustomerAddress(c.Street, c.PostalCode, c.City, c.Country),
        c.Pronouns);

    /// <summary>Leer, Deutsch oder eine der Sprachen aus <see cref="GuestLanguages"/>.</summary>
    public static bool KnownLanguage(string? code) =>
        Blank(code) is not { } c || c.Trim().Equals("de", StringComparison.OrdinalIgnoreCase) || GuestLanguages.Normalize(c) is not null;

    /// <summary>Null, solange nichts eingetragen ist.</summary>
    public static CustomerBookingPreferences? BookingPreferences(Customer c) =>
        c is { PreferredEmployeeId: null, PreferredWeekdays: 0, PreferredFrom: null, PreferredUntil: null, PreferredGender: null }
            ? null
            : new CustomerBookingPreferences(c.PreferredEmployeeId, c.PreferredEmployeeFixed,
                [.. Enum.GetValues<DayOfWeek>().Where(d => (c.PreferredWeekdays & (1 << (int)d)) != 0)], c.PreferredFrom, c.PreferredUntil,
                c.PreferredGender);
}

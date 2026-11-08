using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Öffentlicher Teil des Kundenbereichs: Angebot, freie Zeiten, Buchen (als Gast oder angemeldet), Termin per Link.</summary>
internal static class OnlineBookingEndpoints
{
    private const int MaxNoteLength = 500;

    public static IEndpointRouteBuilder MapOnlineBookingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/online").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        group.MapGet("/locations", async (AispaDbContext db, OnlineRules rules, CardGuarantees guarantees, Deposits deposits, ClosureService closures, CancellationToken ct) =>
            {
                var locations = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).OrderBy(l => l.Name).ToListAsync(ct);
                var byLocation = await rules.ForAsync([.. locations.Select(l => l.Id)], ct);
                var cards = await guarantees.AvailableAsync(ct);
                var result = new List<OnlineLocation>();
                foreach (var l in locations)
                {
                    result.Add(new OnlineLocation(l.Id, l.Name, l.TimeZone, l.Currency,
                        [.. l.OpeningHours.OrderBy(o => o.Day).ThenBy(o => o.Opens).Select(o => new OpeningHours(o.Day, o.Opens, o.Closes))],
                        byLocation[l.Id].BookingEnabled, byLocation[l.Id].GuestNote, byLocation[l.Id].CancellationDeadlineHours,
                        MasterDataEndpoints.ContactOf(l), CardGuarantees.EffectivePercent(cards, byLocation[l.Id]), byLocation[l.Id].NoShowFeeNewCustomersOnly,
                        await deposits.AvailableAsync(l.Id, ct) ? Deposits.EffectivePercent(byLocation[l.Id]) : 0,
                        await closures.OnlineAsync(l.Id, ct)));
                }

                return result;
            })
            .WithName("ListOnlineLocations")
            .WithSummary("Standorte für die Online-Buchung.");

        group.MapGet("/imprint", async Task<Results<Ok<OnlineImprint>, NotFound>> (AispaDbContext db, TenantContext tenants, CancellationToken ct) =>
            {
                var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenants.TenantId, ct);
                if (tenant is null)
                {
                    return TypedResults.NotFound();
                }

                var first = await db.Locations.AsNoTracking().OrderBy(l => l.Name).FirstOrDefaultAsync(ct);
                return TypedResults.Ok(CompanyProfiles.Imprint(tenant, first));
            })
            .WithName("GetOnlineImprint")
            .WithSummary("Impressum des Spas aus den Firmendaten (ohne Bankverbindung).");

        group.MapGet("/locations/{locationId:guid}/treatments", async Task<Results<Ok<List<OnlineTreatment>>, NotFound>> (
                Guid locationId, AispaDbContext db, OnlineRules rules, CardGuarantees guarantees, Deposits deposits, CancellationToken ct) =>
            {
                var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                var locationRules = await rules.ForAsync(locationId, ct);
                var cards = await guarantees.AvailableAsync(ct);
                var depositsAvailable = await deposits.AvailableAsync(locationId, ct);
                return TypedResults.Ok((await BookableTreatmentsAsync(db, locationId, ct))
                    .Select(t => new OnlineTreatment(t.Id, t.Name, t.DurationMinutes, t.PriceAt(locationId), location.Currency, t.Category,
                        CardGuarantees.EffectivePercent(cards, locationRules, t), depositsAvailable ? Deposits.EffectivePercent(locationRules, t) : 0)).ToList());
            })
            .WithName("ListOnlineTreatments")
            .WithSummary("Behandlungen, die an diesem Standort online buchbar sind (geeigneter Raum und qualifiziertes Team vorhanden).");

        group.MapGet("/locations/{locationId:guid}/availability", async Task<Results<Ok<OnlineAvailability>, NotFound>> (
                Guid locationId, Guid treatmentId, DateOnly? from, int? days,
                AispaDbContext db, OnlinePortal portal, CancellationToken ct) =>
            {
                var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
                var treatment = location is null ? null : (await BookableTreatmentsAsync(db, locationId, ct)).FirstOrDefault(t => t.Id == treatmentId);
                if (location is null || treatment is null)
                {
                    return TypedResults.NotFound();
                }

                var zone = LocalTime.Zone(location.TimeZone);
                var (first, last, earliest) = await portal.BookableRangeAsync(locationId, zone, ct);
                var start = from is { } f && f > first ? f : first;
                var result = new List<OnlineDay>();
                for (var date = start; date < start.AddDays(Math.Clamp(days ?? 7, 1, 14)) && date <= last; date = date.AddDays(1))
                {
                    if (await portal.FreeTimesAsync(locationId, treatmentId, date, earliest, ct) is not { } times)
                    {
                        return TypedResults.NotFound();
                    }

                    // Preis je Zeit nur, wenn die Behandlung Preise nach Tageszeit oder Wochentag hat.
                    result.Add(new OnlineDay(date, times,
                        treatment.PriceRules.Count == 0 ? null : [.. times.Select(t => treatment.PriceAt(locationId, t, zone))]));
                }

                return TypedResults.Ok(new OnlineAvailability(locationId, treatmentId, location.TimeZone, first, last, result));
            })
            .WithName("FindOnlineAvailability")
            .WithSummary("Freie Startzeiten für bis zu 14 Tage ab „from“, unter Beachtung von Vorlaufzeit und Buchungshorizont.");

        group.MapPost("/bookings", BookAsync)
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("BookOnline")
            .WithSummary("Bucht einen Termin als Gast oder mit Kundenkonto; die Bestätigung geht per Mail. Bei Überschneidung 409, 403 wenn dieser Gast online nicht buchen kann (Kennzeichen, No-Shows, zu viele offene Termine).");

        group.MapGet("/bookings/{token}", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, AccessLinks links, OnlinePortal portal, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                var appointment = link?.AppointmentId is { } id ? await portal.LoadAppointmentAsync(id, ct) : null;
                return appointment is null || appointment.CustomerId != link!.CustomerId
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(await portal.ToOnlineAsync(appointment, ct));
            })
            .WithName("GetOnlineBooking")
            .WithSummary("Termin über den Link aus der Bestätigungsmail.");

        group.MapPost("/bookings/{token}/cancel", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                string token, AccessLinks links, OnlinePortal portal, CancellationToken ct, bool acceptFee = false) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id
                    ? Respond(await portal.CancelAsync(id, link.CustomerId, acceptFee, ct))
                    : TypedResults.NotFound();
            })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("CancelOnlineBooking")
            .WithSummary("Storniert über den Link aus der Bestätigungsmail, bis zur Stornofrist; danach nur mit hinterlegter Karte und „acceptFee“.");

        group.MapGet("/bookings/{token}/calendar.ics", async Task<Results<FileContentHttpResult, NotFound>> (
                string token, AccessLinks links, OnlinePortal portal, TimeProvider time, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id ? await portal.CalendarFileAsync(id, link.CustomerId, time.GetUtcNow(), ct) : TypedResults.NotFound();
            })
            .WithName("GetOnlineBookingCalendarFile")
            .WithSummary("Termin als .ics für den eigenen Kalender, über den Link aus der Bestätigungsmail.");

        group.MapPost("/bookings/{token}/move", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                string token, OnlineMoveRequest request, AccessLinks links, OnlinePortal portal, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id
                    ? Respond(await portal.MoveAsync(id, link.CustomerId, request.Start, ct))
                    : TypedResults.NotFound();
            })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("MoveOnlineBooking")
            .WithSummary("Der Gast verschiebt über den Link aus der Bestätigungsmail auf eine freie Zeit, bis zur Stornofrist und höchstens zweimal.");

        group.MapPost("/bookings/{token}/confirm", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                string token, AccessLinks links, OnlinePortal portal, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id
                    ? Respond(await portal.ConfirmAttendanceAsync(id, link.CustomerId, ct))
                    : TypedResults.NotFound();
            })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("ConfirmOnlineBooking")
            .WithSummary("Der Gast sagt über den Link aus Bestätigung oder Erinnerung zu, dass er kommt; nur für bevorstehende Termine.");

        return routes;
    }

    private static async Task<Results<Created<OnlineBookingConfirmation>, ValidationProblem, NotFound, Conflict<BookingRejected>, ProblemHttpResult>> BookAsync(
        OnlineBookingRequest request, HttpContext http, AispaDbContext db, OnlineDbContext online,
        BookingService booking, OnlinePortal portal, CustomerAccounts accounts, OnlineRules rules, CardGuarantees guarantees, Deposits deposits,
        TimeProvider time, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocationId, ct);
        var treatment = location is null ? null : (await BookableTreatmentsAsync(db, request.LocationId, ct)).FirstOrDefault(t => t.Id == request.TreatmentId);
        if (location is null || treatment is null)
        {
            return TypedResults.NotFound();
        }

        var accountId = await CustomerAuth.TryGetAccountIdAsync(http);
        var account = accountId is { } aid ? await online.Users.FirstOrDefaultAsync(a => a.Id == aid, ct) : null;

        var errors = new Errors();
        errors.Check(request.AcceptPrivacy, nameof(request.AcceptPrivacy), "Bitte den Datenschutzhinweis bestätigen.");
        errors.Check((request.Note?.Length ?? 0) <= MaxNoteLength, nameof(request.Note), $"Höchstens {MaxNoteLength} Zeichen.");
        // Ohne ausdrückliche Zustimmung beim Buchen darf später keine Ausfallgebühr belastet werden. Gilt sie nur für neue Gäste,
        // zählt der schon bekannte Kunde (Konto oder E-Mail); wer noch unbekannt ist, ist neu.
        var locationRules = await rules.ForAsync(location.Id, ct);
        var knownCustomer = account?.CustomerId
            ?? (request.Contact?.Email is { } contactEmail ? await accounts.FindCustomerByEmailAsync(contactEmail.Trim(), ct) : null);
        // Eine Anzahlung geht der Karte vor; die Zustimmung gilt dann ihren Bedingungen.
        var depositPercent = knownCustomer is { } knownForDeposit
            ? await deposits.PercentForAsync(locationRules, treatment, location.Id, knownForDeposit, ct)
            : await deposits.PercentAtAsync(locationRules, treatment, location.Id, ct);
        var feePercent = depositPercent > 0 ? 0
            : knownCustomer is { } known
                ? await guarantees.PercentForAsync(locationRules, treatment, known, ct)
                : CardGuarantees.EffectivePercent(await guarantees.AvailableAsync(ct), locationRules, treatment);
        errors.Check(request.AcceptNoShowFee || (feePercent == 0 && depositPercent == 0), nameof(request.AcceptNoShowFee),
            depositPercent > 0 ? "Bitte den Bedingungen der Anzahlung zustimmen." : "Bitte der Ausfallgebühr zustimmen.");
        var (_, last, earliest) = await portal.BookableRangeAsync(location.Id, LocalTime.Zone(location.TimeZone), ct);
        var start = request.Start.ToUniversalTime();
        errors.Check(start >= earliest, nameof(request.Start), "Diese Zeit ist online nicht mehr buchbar.");
        errors.Check(LocalTime.LocalDate(start, LocalTime.Zone(location.TimeZone)) <= last, nameof(request.Start), "So weit im Voraus ist online noch nicht buchbar.");
        // Gutschein nur angeben: geprüft wird er hier, eingelöst an der Kassa (Beleg nach RKSV/KassenSichV), nicht online.
        var voucher = Blank(request.VoucherCode) is { } voucherCode
            ? await FindVoucherAsync(db, voucherCode, treatment.Id, LocalTime.LocalDate(start, LocalTime.Zone(location.TimeZone)), ct)
            : null;
        errors.Check(Blank(request.VoucherCode) is null || voucher is not null, nameof(request.VoucherCode),
            "Diesen Gutschein können wir für diese Behandlung nicht annehmen. Bitte prüfen Sie den Code.");
        if (account?.CustomerId is null)
        {
            // Gäste und neue Konten ohne Kundendaten: Name und Kontakt sind Pflicht; die E-Mail eines Kontos steht schon fest.
            CustomerAccounts.ValidateContact(errors, request.Contact, requireEmail: account is null);
        }

        if (errors.Any)
        {
            return errors.Problem();
        }

        // Erst prüfen, ob die Zeit frei ist, damit für eine abgelehnte Gastbuchung kein Kunde angelegt wird.
        var context = await booking.LoadContextAsync(
            request.LocationId, request.TreatmentId, LocalTime.LocalDate(start, LocalTime.Zone(location.TimeZone)), null, ct);
        if (context is null)
        {
            return TypedResults.NotFound();
        }

        // Kennzeichen des bekannten Gastes: bei „Nicht mehr buchen“ entscheidet die Rezeption, ohne dem Gast den Grund zu nennen.
        var customerRules = await CustomerRules.LoadAsync(db, knownCustomer, ct);
        if (customerRules.DoNotBook)
        {
            return Refused("Online ist diese Buchung leider nicht möglich. Bitte rufen Sie uns an, wir finden gern einen Termin.");
        }

        // Schutz vor No-Shows und gehorteten Terminen; auch hier ohne dem Gast den Grund im Einzelnen zu nennen.
        if (knownCustomer is { } guest)
        {
            var now = time.GetUtcNow();
            if (depositPercent == 0 && feePercent == 0 && await NoShowHistory.IsRepeatAsync(db, guest, now, ct))
            {
                return Refused("Online ist diese Buchung leider nicht möglich. Bitte rufen Sie uns an, wir finden gern einen Termin.");
            }

            if (await NoShowHistory.OpenOnlineBookingsAsync(db, guest, now, ct) >= NoShowHistory.MaxOpenOnlineBookings)
            {
                return Refused($"Sie haben schon {NoShowHistory.MaxOpenOnlineBookings} offene Termine online gebucht. Weitere Termine buchen wir gern telefonisch.");
            }
        }

        if (new ResourcePlanner(customerRules.Apply(context)).Plan(start) is { Plan: null } rejected)
        {
            return TypedResults.Conflict(Public(rejected.Message!));
        }

        var (customerId, email) = await accounts.ResolveCustomerAsync(account, request.Contact, ct);

        var outcome = await booking.BookAsync(new AppointmentRequest(
            request.LocationId, request.TreatmentId, customerId, start, null, null, null,
            voucher is null ? Blank(request.Note) : $"{Blank(request.Note)}\nGutschein {voucher} angegeben, an der Kassa einlösen.".TrimStart()), ct,
            source: AppointmentSource.Online);
        switch (outcome)
        {
            case BookingOutcome.Rejected r:
                return TypedResults.Conflict(Public(r.Reason.Message));
            case BookingOutcome.Success s:
                var confirmation = await portal.ConfirmAsync(s.Appointment.Id, account?.Id, email, GuestLanguage.Normalize(request.Language), ct);
                return TypedResults.Created($"/api/online/me/appointments/{s.Appointment.Id}", confirmation);
            default:
                return TypedResults.NotFound();
        }
    }

    /// <summary>
    /// Online angebotene Behandlungen: aktiv, mit geeignetem Raum am Standort und mit mindestens einer Person, die dort arbeitet
    /// und alle nötigen Qualifikationen hat. So bietet der Kundenbereich nichts an, wofür es nie freie Zeiten gäbe.
    /// </summary>
    /// <summary>Online buchbare Behandlungen: angebotene, solange der Standort die Online-Buchung nicht ausgeschaltet hat.</summary>
    public static async Task<List<Treatment>> BookableTreatmentsAsync(AispaDbContext db, Guid locationId, CancellationToken ct) =>
        await db.LocationOnlineSettings.AnyAsync(s => s.LocationId == locationId && !s.BookingEnabled, ct)
            ? []
            : await OfferedTreatmentsAsync(db, locationId, ct);

    /// <summary>Behandlungen mit geeignetem Raum und qualifiziertem Team am Standort (auch für Gutscheine).</summary>
    public static async Task<List<Treatment>> OfferedTreatmentsAsync(AispaDbContext db, Guid locationId, CancellationToken ct)
    {
        var treatments = await db.Treatments.AsNoTracking()
            .Include(t => t.RequiredQualifications).Include(t => t.LocationPrices).Include(t => t.PriceRules)
            .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
            .OrderBy(t => t.Name)
            .AsSplitQuery()
            .ToListAsync(ct);
        var qualificationSets = (await db.Employees.AsNoTracking().Include(e => e.Qualifications)
                .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == locationId))
                .ToListAsync(ct))
            .Select(e => e.Qualifications.Select(q => q.Id).ToHashSet())
            .ToList();
        return [.. treatments.Where(t => qualificationSets.Any(s => t.RequiredQualifications.All(q => s.Contains(q.Id))))];
    }

    public static Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult> Respond(PortalOutcome outcome) => outcome switch
    {
        PortalOutcome.Done d => TypedResults.Ok(d.Appointment),
        PortalOutcome.Refused r => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: r.Message),
        _ => TypedResults.NotFound(),
    };

    /// <summary>Ablehnung ohne interne Details: Kundinnen und Kunden sehen nicht, welche Person oder welcher Raum belegt ist.</summary>
    /// <summary>Code eines gültigen Gutscheins, der für die Behandlung am Tag des Termins gilt; sonst <c>null</c>.</summary>
    private static async Task<string?> FindVoucherAsync(AispaDbContext db, string code, Guid treatmentId, DateOnly day, CancellationToken ct)
    {
        var normalized = PosService.NormalizeCode(code);
        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == normalized, ct);
        return voucher is { Status: VoucherStatus.Active }
            && (voucher.Units is null ? voucher.Balance > 0 : voucher.UnitsLeft > 0)
            && (voucher.ValidUntil is not { } until || until >= day)
            && (voucher.TreatmentId is not { } only || only == treatmentId)
            ? voucher.Code
            : null;
    }

    private static BookingRejected Public(string message) => new(message, []);

    /// <summary>Online nicht buchbar für diesen Gast (403, nicht 409), damit der Kundenbereich nicht „Zeit vergeben“ meldet.</summary>
    private static ProblemHttpResult Refused(string message) => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: message);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

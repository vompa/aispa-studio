using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Terminbestätigung als PDF und per Mail (PDF-Ausgaben C, ADR 0092). Rechte wie das Lesen des Termins; den Termin prüft der Mandantenfilter, ein fremder ist „nicht gefunden“.
/// Nach dem Versand steht „Bestätigung per Mail an … gesendet von …“ im Verlauf des Termins (<see cref="AppointmentHistory"/>).
/// </summary>
internal static class AppointmentConfirmationEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentConfirmationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/appointments/{appointmentId:guid}/confirmation").WithTags("Kalender").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid appointmentId, ClaimsPrincipal user, AispaDbContext db, OnlineRules rules, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await LoadAsync(appointmentId, db, rules, ct) is not { } booking)
                {
                    return TypedResults.NotFound();
                }

                var pdf = AppointmentConfirmation.Build(booking, appointmentId, await headers.ForAsync(booking.Location.Id, user, ct));
                return DocumentFiles.Pdf(pdf, AppointmentConfirmation.FileName(booking, appointmentId));
            })
            .WithName("AppointmentConfirmationPdf")
            .WithSummary("Terminbestätigung als PDF: Gast, alle Teile der Buchung, Tag, Uhrzeit, Dauer, Person (Vorname), Standort mit Adresse, Preis, Stornofrist und „Was mitbringen“.");

        group.MapGet("/send", async Task<Results<Ok<DocumentMailDraft>, NotFound>> (
                Guid appointmentId, ClaimsPrincipal user, AispaDbContext db, OnlineRules rules, DocumentHeaders headers, CancellationToken ct) =>
                await PrepareAsync(appointmentId, user, db, rules, headers, ct) is { } prepared ? TypedResults.Ok(prepared.Draft) : TypedResults.NotFound())
            .WithName("AppointmentConfirmationMailDraft")
            .WithSummary("Vorbelegung für „Per Mail senden“: Adresse des Gastes aus der Kundenkarte, Betreff und Text.");

        group.MapPost("/send", async Task<Results<Accepted, NotFound, ValidationProblem>> (
                Guid appointmentId, SendDocumentRequest request, ClaimsPrincipal user, AispaDbContext db, OnlineRules rules, DocumentHeaders headers,
                SentDocuments sent, TenantContext tenants, IMessageBus bus, CancellationToken ct) =>
            {
                if (DocumentMails.Validate(request) is { } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                if (await PrepareAsync(appointmentId, user, db, rules, headers, ct) is not { } prepared)
                {
                    return TypedResults.NotFound();
                }

                var (subject, text) = DocumentMails.Resolve(prepared.Draft, request);
                var pdf = AppointmentConfirmation.Build(prepared.Booking, appointmentId, prepared.Header);
                await bus.PublishAsync(DocumentMails.Message(tenants.TenantId!.Value, request.To, subject, text, AppointmentConfirmation.FileName(prepared.Booking, appointmentId), pdf));
                await sent.RecordAsync(SentDocument.AppointmentConfirmationKind, appointmentId, request.To, subject, user, ct);
                return TypedResults.Accepted($"/api/appointments/{appointmentId}");
            })
            .WithName("SendAppointmentConfirmation")
            .WithSummary("Terminbestätigung als PDF per Mail an den Gast (über die Queue), mit Vermerk im Verlauf des Termins; ohne Betreff und Text gilt die Vorbelegung.");

        return routes;
    }

    private static async Task<PreparedConfirmation?> PrepareAsync(
        Guid appointmentId, ClaimsPrincipal user, AispaDbContext db, OnlineRules rules, DocumentHeaders headers, CancellationToken ct)
    {
        if (await LoadAsync(appointmentId, db, rules, ct) is not { } booking)
        {
            return null;
        }

        var header = await headers.ForAsync(booking.Location.Id, user, ct);
        var customerId = booking.Parts.First(p => p.Appointment.Id == appointmentId).Appointment.CustomerId;
        var email = await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Email).FirstOrDefaultAsync(ct);
        return new PreparedConfirmation(booking, header, AppointmentConfirmation.Draft(booking, appointmentId, header.SpaName, string.IsNullOrWhiteSpace(email) ? null : email));
    }

    /// <summary>Der Termin mit den anderen Teilen seiner Buchung (gleiche Gruppe am selben Standort, ohne stornierte) und den Regeln des Standorts; null, wenn es den Termin nicht gibt.</summary>
    private static async Task<AppointmentConfirmation.Booking?> LoadAsync(Guid appointmentId, AispaDbContext db, OnlineRules rules, CancellationToken ct)
    {
        var main = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (main is null)
        {
            return null;
        }

        var appointments = main.GroupId is { } groupId
            ? await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                .Where(a => a.GroupId == groupId && a.LocationId == main.LocationId && (a.Status != AppointmentStatus.Cancelled || a.Id == appointmentId)).ToListAsync(ct)
            : await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment).Where(a => a.Id == appointmentId).ToListAsync(ct);
        var employeeIds = appointments.Select(a => a.EmployeeId).Distinct().ToList();
        var employees = (await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).Select(e => new { e.Id, e.DisplayName, e.PreferredName }).ToListAsync(ct))
            .ToDictionary(e => e.Id, e => FirstName(e.DisplayName, e.PreferredName));
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == main.LocationId, ct);
        var parts = appointments.Select(a => new AppointmentConfirmation.Part(a, GuestName(a), employees.GetValueOrDefault(a.EmployeeId, ""))).ToList();
        return new AppointmentConfirmation.Booking(parts, location, await rules.ForAsync(location.Id, ct), LocalTime.Zone(location.TimeZone));
    }

    private static string GuestName(Appointment appointment) =>
        appointment.PlaceholderGuest is { } number ? $"Gast {number}" : appointment.Customer.LegalName;

    /// <summary>Vorname des Rufnamens (ADR 0064); nur der Vorname steht auf dem Dokument für den Gast.</summary>
    private static string FirstName(string displayName, string? preferredName) =>
        EmployeeNames.InHouse(displayName, preferredName).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    private sealed record PreparedConfirmation(AppointmentConfirmation.Booking Booking, DocumentHeader Header, DocumentMailDraft Draft);
}

using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.BusinessInfo;

/// <summary>
/// Betriebsinformationen (ADR 0074): lesen für jeden Zugang mit Kalender oder Berichten (der Server filtert Einträge der Stufe „Leitung“ für alle ohne das Recht
/// heraus), verwalten für Inhaber, Manager und Standortleitung.
/// </summary>
internal static class BusinessContactEndpoints
{
    public static IEndpointRouteBuilder MapBusinessContactEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/business-contacts").WithTags("Betriebsinformationen");

        group.MapGet("/", async (Guid? locationId, bool? includeArchived, BusinessContactService contacts, AccessService access, CancellationToken ct) =>
                await contacts.ListAsync(includeArchived == true, locationId, (await access.CurrentAsync(ct))!, ct))
            .WithName("ListBusinessContacts")
            .WithSummary("Betriebsinformationen: Versicherung, Handwerker, Buchhaltung und andere Kontakte mit Zuständigkeit. Einträge der Stufe „Leitung“ lesen nur Inhaber, Manager, Standortleitung und Backoffice; archivierte nur auf Wunsch und nur, wer verwalten darf.")
            .RequirePermission(AccessRight.ViewCalendar)
            // Das Backoffice hat keinen Kalender, liest aber alles für Buchhaltung und Lohn (ADR 0070, 0074).
            .AlsoAllow(AccessRight.ViewFinance, AccessRight.ViewCalendar);

        group.MapPost("/", async Task<Results<Created<BusinessContactDto>, ValidationProblem>> (
                BusinessContactInput input, BusinessContactService contacts, AccessService access, CancellationToken ct) =>
            {
                var (contact, errors) = await contacts.CreateAsync(input, (await access.CurrentAsync(ct))!, ct);
                return contact is null ? errors!.Problem() : TypedResults.Created($"/api/business-contacts/{contact.Id}", contact);
            })
            .WithName("CreateBusinessContact")
            .WithSummary("Legt einen Eintrag an. Keine Passwörter oder Zugangsdaten eintragen.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{contactId:guid}", async Task<Results<Ok<BusinessContactDto>, NotFound, ValidationProblem>> (
                Guid contactId, BusinessContactInput input, BusinessContactService contacts, AccessService access, CancellationToken ct) =>
            {
                var (contact, errors) = await contacts.UpdateAsync(contactId, input, (await access.CurrentAsync(ct))!, ct);
                return contact is not null ? TypedResults.Ok(contact) : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("UpdateBusinessContact")
            .WithSummary("Ändert einen Eintrag. Die Standortleitung ändert nur Einträge ihrer eigenen Standorte.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{contactId:guid}/archive", async Task<Results<Ok<BusinessContactDto>, NotFound>> (
                Guid contactId, BusinessContactService contacts, AccessService access, CancellationToken ct) =>
                await contacts.SetArchivedAsync(contactId, true, (await access.CurrentAsync(ct))!, ct) is { } contact ? TypedResults.Ok(contact) : TypedResults.NotFound())
            .WithName("ArchiveBusinessContact")
            .WithSummary("Archiviert einen Eintrag: er bleibt erhalten, steht aber nicht mehr in der Liste und der Assistent nennt ihn nicht mehr.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{contactId:guid}/unarchive", async Task<Results<Ok<BusinessContactDto>, NotFound>> (
                Guid contactId, BusinessContactService contacts, AccessService access, CancellationToken ct) =>
                await contacts.SetArchivedAsync(contactId, false, (await access.CurrentAsync(ct))!, ct) is { } contact ? TypedResults.Ok(contact) : TypedResults.NotFound())
            .WithName("UnarchiveBusinessContact")
            .WithSummary("Holt einen archivierten Eintrag zurück in die Liste.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }
}

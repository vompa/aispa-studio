using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.BusinessInfo;

/// <summary>
/// Betriebsinformationen lesen und verwalten (ADR 0074). Lesen darf jeder Zugang mit Kalender oder Berichten, gefiltert nach Stufe „Team“ oder „Leitung“ und nach
/// den Standorten des Zugangs; verwalten dürfen Inhaber, Manager und Standortleitung (<see cref="AccessRight.ManageLocation"/>), die Standortleitung nur Einträge
/// ihrer eigenen Standorte. Der Assistent liest über <see cref="ForLocationAsync"/> und wendet <see cref="BusinessContactRules"/> selbst an.
/// </summary>
internal sealed partial class BusinessContactService(AispaDbContext db, TimeProvider time)
{
    // ---------- Abfrage ----------

    /// <summary>
    /// Die Einträge, die diese Person lesen darf (Stufe, Standorte), nach Kategorie und Bezeichnung geordnet. Archivierte nur für Personen, die verwalten dürfen,
    /// und nur auf Wunsch. <paramref name="locationId"/> grenzt auf Einträge ein, die an diesem Standort gelten.
    /// </summary>
    public async Task<List<BusinessContactDto>> ListAsync(bool includeArchived, Guid? locationId, UserAccess access, CancellationToken ct)
    {
        var showArchived = includeArchived && access.Has(AccessRight.ManageLocation);
        var query = db.BusinessContacts.AsNoTracking().Include(c => c.Locations).AsSplitQuery();
        var contacts = await (showArchived ? query : query.Where(c => c.ArchivedAt == null)).ToListAsync(ct);
        return [.. contacts
            .Where(c => BusinessContactRules.CanRead(access, c) && BusinessContactRules.InScope(access, c)
                && (locationId is not { } place || BusinessContactRules.AppliesTo(c, place)))
            .OrderBy(c => c.Category).ThenBy(c => c.Designation, StringComparer.Ordinal).ThenBy(c => c.Company, StringComparer.Ordinal)
            .Select(c => ToDto(c, access))];
    }

    /// <summary>
    /// Alle nicht archivierten Einträge, die an diesem Standort gelten, mit ihren Standorten und ohne Prüfung der Stufe; der Aufrufer (der Assistent) filtert mit
    /// <see cref="BusinessContactRules.CanRead"/>, damit er „keine Berechtigung“ von „nichts hinterlegt“ unterscheiden kann.
    /// </summary>
    public async Task<List<BusinessContact>> ForLocationAsync(Guid locationId, CancellationToken ct) =>
        [.. (await db.BusinessContacts.AsNoTracking().Include(c => c.Locations).AsSplitQuery().Where(c => c.ArchivedAt == null).ToListAsync(ct))
            .Where(c => BusinessContactRules.AppliesTo(c, locationId))
            .OrderBy(c => c.Category).ThenBy(c => c.Designation, StringComparer.Ordinal)];

    // ---------- Verwaltung ----------

    public async Task<(BusinessContactDto? Contact, Errors? Errors)> CreateAsync(BusinessContactInput input, UserAccess access, CancellationToken ct)
    {
        var errors = await ValidateAsync(input, access, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        var contact = new BusinessContact { Designation = input.Designation.Trim(), CreatedAt = time.GetUtcNow() };
        Apply(contact, input);
        db.BusinessContacts.Add(contact);
        await db.SaveChangesAsync(ct);
        return (ToDto(contact, access), null);
    }

    /// <summary>Ändert einen Eintrag. <c>(null, null)</c>: gibt es nicht, oder die Person darf ihn nicht verwalten.</summary>
    public async Task<(BusinessContactDto? Contact, Errors? Errors)> UpdateAsync(Guid id, BusinessContactInput input, UserAccess access, CancellationToken ct)
    {
        var contact = await LoadAsync(id, ct);
        if (contact is null || !BusinessContactRules.CanManage(access, contact))
        {
            return (null, null);
        }

        var errors = await ValidateAsync(input, access, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        contact.Designation = input.Designation.Trim();
        db.BusinessContactLocations.RemoveRange(contact.Locations);
        contact.Locations.Clear();
        Apply(contact, input);
        await db.SaveChangesAsync(ct);
        return (ToDto(contact, access), null);
    }

    /// <summary>Archivieren oder zurückholen; der Eintrag bleibt erhalten. Null, wenn es ihn nicht gibt oder die Person ihn nicht verwalten darf.</summary>
    public async Task<BusinessContactDto?> SetArchivedAsync(Guid id, bool archived, UserAccess access, CancellationToken ct)
    {
        var contact = await LoadAsync(id, ct);
        if (contact is null || !BusinessContactRules.CanManage(access, contact))
        {
            return null;
        }

        contact.ArchivedAt = archived ? contact.ArchivedAt ?? time.GetUtcNow() : null;
        await db.SaveChangesAsync(ct);
        return ToDto(contact, access);
    }

    private Task<BusinessContact?> LoadAsync(Guid id, CancellationToken ct) =>
        db.BusinessContacts.Include(c => c.Locations).AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct);

    private async Task<Errors> ValidateAsync(BusinessContactInput input, UserAccess access, CancellationToken ct)
    {
        var errors = new Errors();
        var designation = input.Designation?.Trim() ?? "";
        errors.Check(designation.Length is > 0 and <= BusinessContactModel.DesignationLength, nameof(input.Designation),
            $"Bezeichnung mit höchstens {BusinessContactModel.DesignationLength} Zeichen angeben, z. B. „Installateur“.");
        errors.Check(Enum.IsDefined(input.Category), nameof(input.Category), "Kategorie wählen.");
        errors.Check(Enum.IsDefined(input.Visibility), nameof(input.Visibility), "Wählen, wer den Eintrag lesen darf.");

        Limit(errors, nameof(input.Company), input.Company, BusinessContactModel.NameLength);
        Limit(errors, nameof(input.ContactPerson), input.ContactPerson, BusinessContactModel.NameLength);
        Limit(errors, nameof(input.Address), input.Address, BusinessContactModel.TextLength);
        Limit(errors, nameof(input.Responsibility), input.Responsibility, BusinessContactModel.TextLength);
        Limit(errors, nameof(input.CustomerNumber), input.CustomerNumber, BusinessContactModel.NumberLength);
        Limit(errors, nameof(input.Notes), input.Notes, BusinessContactModel.NotesLength);
        CheckPhone(errors, nameof(input.Phone), input.Phone);
        CheckPhone(errors, nameof(input.Mobile), input.Mobile);
        if (Text(input.Email) is { } email)
        {
            errors.Check(email.Length <= BusinessContactModel.EmailLength && CustomerAccounts.IsValidEmail(email), nameof(input.Email), "Keine gültige E-Mail-Adresse.");
        }

        errors.Check(new[] { input.Company, input.ContactPerson, input.Phone, input.Mobile, input.Email }.Any(v => Text(v) is not null), nameof(input.Company),
            "Mindestens Firma, Ansprechperson, Telefon, Mobil oder E-Mail angeben.");

        var insurance = new[] { input.Insurer, input.PolicyNumber, input.Coverage, input.ClaimsHotline };
        if (input.Category == BusinessContactCategory.Insurance)
        {
            Limit(errors, nameof(input.Insurer), input.Insurer, BusinessContactModel.NameLength);
            Limit(errors, nameof(input.PolicyNumber), input.PolicyNumber, BusinessContactModel.NumberLength);
            Limit(errors, nameof(input.Coverage), input.Coverage, BusinessContactModel.TextLength);
            CheckPhone(errors, nameof(input.ClaimsHotline), input.ClaimsHotline);
        }
        else
        {
            errors.Check(insurance.All(v => Text(v) is null), nameof(input.Insurer), "Versicherer, Polizzennummer, Deckung und Schadenshotline gibt es nur bei der Kategorie „Versicherung“.");
        }

        var locationIds = (input.LocationIds ?? []).Distinct().ToList();
        if (input.AllLocations)
        {
            errors.Check(locationIds.Count == 0, nameof(input.LocationIds), "Bei „alle Standorte“ keine einzelnen Standorte wählen.");
            errors.Check(access.AllLocations, nameof(input.AllLocations), "Einträge für alle Standorte legt nur die Inhaberin oder der Inhaber an.");
        }
        else
        {
            errors.Check(locationIds.Count > 0, nameof(input.LocationIds), "Mindestens einen Standort wählen.");
            errors.Check(locationIds.All(access.CanSee), nameof(input.LocationIds), "Für einen dieser Standorte hast du keinen Zugang.");
            errors.Check(await db.Locations.CountAsync(l => locationIds.Contains(l.Id), ct) == locationIds.Count, nameof(input.LocationIds), "Standort nicht gefunden.");
        }

        return errors;
    }

    /// <summary>Übernimmt die Eingabe (ohne Bezeichnung, die der Aufrufer getrimmt setzt); leere Angaben werden zu <c>null</c>, Versicherungsfelder gibt es nur bei Versicherungen.</summary>
    private void Apply(BusinessContact contact, BusinessContactInput input)
    {
        var insurance = input.Category == BusinessContactCategory.Insurance;
        contact.Category = input.Category;
        contact.Company = Text(input.Company);
        contact.ContactPerson = Text(input.ContactPerson);
        contact.Phone = Text(input.Phone);
        contact.Mobile = Text(input.Mobile);
        contact.Email = Text(input.Email);
        contact.Address = Text(input.Address);
        contact.Responsibility = Text(input.Responsibility);
        contact.CustomerNumber = Text(input.CustomerNumber);
        contact.Insurer = insurance ? Text(input.Insurer) : null;
        contact.PolicyNumber = insurance ? Text(input.PolicyNumber) : null;
        contact.Coverage = insurance ? Text(input.Coverage) : null;
        contact.ClaimsHotline = insurance ? Text(input.ClaimsHotline) : null;
        contact.AllLocations = input.AllLocations;
        contact.Notes = Text(input.Notes);
        contact.Visibility = input.Visibility;
        if (!input.AllLocations)
        {
            foreach (var locationId in (input.LocationIds ?? []).Distinct())
            {
                var link = new BusinessContactLocation { BusinessContactId = contact.Id, LocationId = locationId };
                contact.Locations.Add(link);
                db.BusinessContactLocations.Add(link);
            }
        }
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Limit(Errors errors, string field, string? value, int max) =>
        errors.Check((Text(value)?.Length ?? 0) <= max, field, $"Höchstens {max} Zeichen.");

    private static void CheckPhone(Errors errors, string field, string? value)
    {
        if (Text(value) is not { } phone)
        {
            return;
        }

        errors.Check(phone.Length <= BusinessContactModel.PhoneLength && PhoneNumber().IsMatch(phone) && phone.Count(char.IsAsciiDigit) >= 3, field,
            "Keine gültige Telefonnummer. Erlaubt sind Ziffern, Leerzeichen und + ( ) / . -");
    }

    [GeneratedRegex(@"^\+?[0-9 ()/.\-]+$")]
    private static partial Regex PhoneNumber();

    private static BusinessContactDto ToDto(BusinessContact c, UserAccess access) =>
        new(c.Id, c.Category, c.Designation, c.Company, c.ContactPerson, c.Phone, c.Mobile, c.Email, c.Address, c.Responsibility, c.CustomerNumber, c.Insurer,
            c.PolicyNumber, c.Coverage, c.ClaimsHotline, c.AllLocations, [.. c.Locations.Select(l => l.LocationId).Order()], c.Notes, c.Visibility, c.ArchivedAt is not null,
            access.Has(AccessRight.ManageLocation) && BusinessContactRules.CanManage(access, c));
}

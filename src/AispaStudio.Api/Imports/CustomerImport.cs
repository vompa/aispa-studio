using AispaStudio.Api.Data;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Importing;

/// <summary>Eine geprüfte Zeile der Datei: neu, Dublette (mit Ziel im Bestand oder früherer Zeile der Datei) oder fehlerhaft (mit Grund).</summary>
public sealed record CustomerImportRow(
    ImportLine Line,
    ImportRowStatus Status,
    CustomerInput? Input,
    string? StoredPhone,
    bool Consent,
    string? Reason,
    Guid? ExistingCustomerId,
    int? EarlierRow);

/// <summary>
/// Kunden-Import aus Excel und CSV (Export/Import, Aufgabe E). Jede Zeile geht durch dieselben Prüfungen wie <c>POST /api/customers</c> (<see cref="CustomerEndpoints.CheckNew"/>),
/// Dubletten sind gleicher Nachname und gleiche Telefonnummer oder E-Mail-Adresse, im Bestand und innerhalb der Datei. „Ergänzen“ füllt nur leere Felder, nie überschreibt es.
/// Gespeichert wird in Blöcken zu 200 mit dem Kanal <see cref="ChangeChannel.Import"/>, damit jeder Gast „Angelegt beim Import von …“ im Verlauf trägt.
/// </summary>
public sealed class CustomerImport(AispaDbContext db, TimeProvider time)
{
    public const int MaxRows = 20_000;
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int BlockSize = 200;
    public const int PreviewRows = 20;

    private sealed record Known(Guid Id, string Name);

    private sealed record DuplicateKey(string Key, string Reason);

    /// <summary>Prüft alle Zeilen gegen die Zuordnung und den Bestand des Spas, ohne etwas zu ändern.</summary>
    public async Task<List<CustomerImportRow>> AnalyzeAsync(ImportSheet sheet, CustomerImportMapping mapping, CancellationToken ct)
    {
        var country = await db.Tenants.AsNoTracking().Where(t => t.Id == db.CurrentTenantId).Select(t => t.Country).FirstOrDefaultAsync(ct);
        var dateFormat = mapping.DateFormat == ImportDateFormat.Auto ? DetectedDateFormat(sheet, mapping) : mapping.DateFormat;
        var existing = await ExistingKeysAsync(country, ct);
        var inFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<CustomerImportRow>(sheet.Lines.Count);
        foreach (var line in sheet.Lines)
        {
            var (input, consent, error) = Read(line, mapping, dateFormat, country);
            if (input is null)
            {
                rows.Add(new CustomerImportRow(line, ImportRowStatus.Invalid, null, null, false, error, null, null));
                continue;
            }

            var phone = CustomerEndpoints.StoredPhone(input.Phone, country);
            var keys = Keys(input.LastName, phone, input.Email, country);
            var hit = keys.Select(k => existing.TryGetValue(k.Key, out var known) ? (k.Reason, Known: known) : (k.Reason, Known: null)).FirstOrDefault(m => m.Known is not null);
            var earlier = keys.Select(k => inFile.TryGetValue(k.Key, out var row) ? row : (int?)null).FirstOrDefault(r => r is not null);
            foreach (var key in keys)
            {
                inFile.TryAdd(key.Key, line.Number);
            }

            rows.Add(hit.Known is { } match
                ? new CustomerImportRow(line, ImportRowStatus.Duplicate, input, phone, consent, $"{hit.Reason} wie {match.Name}", match.Id, null)
                : earlier is { } first
                    ? new CustomerImportRow(line, ImportRowStatus.Duplicate, input, phone, consent, $"doppelt in der Datei (Zeile {first})", null, first)
                    : new CustomerImportRow(line, ImportRowStatus.New, input, phone, consent, null, null, null));
        }

        return rows;
    }

    /// <summary>Das Datumsformat, das die Spalte Geburtstag am besten liest; ohne zugeordnete Spalte TT.MM.JJJJ.</summary>
    public static ImportDateFormat DetectedDateFormat(ImportSheet sheet, CustomerImportMapping mapping) =>
        mapping.Columns.FirstOrDefault(c => c.Field == CustomerImportField.BirthDate) is { } birthday
            ? CustomerImportFields.Detect(sheet.Lines.Select(l => l.Cell(birthday.Column)))
            : ImportDateFormat.DayMonthYear;

    /// <summary>
    /// Führt den Import aus: neue Gäste anlegen, Dubletten nach <paramref name="decisions"/> überspringen (Vorgabe) oder ergänzen. Im Probelauf wird nur gezählt.
    /// Zuletzt steht eine Zeile im Import-Protokoll, gespeichert mit dem Kanal der Anfrage („von Lena“), nicht mit dem Kanal Import.
    /// </summary>
    public async Task<CustomerImportResult> RunAsync(IReadOnlyList<CustomerImportRow> rows, IReadOnlyDictionary<int, ImportDuplicateAction> decisions, bool dryRun,
        string fileName, string? userId, CancellationToken ct)
    {
        ImportDuplicateAction Decision(CustomerImportRow row) => decisions.TryGetValue(row.Line.Number, out var action) ? action : ImportDuplicateAction.Skip;

        var created = rows.Count(r => r.Status == ImportRowStatus.New);
        var merged = rows.Count(r => r.Status == ImportRowStatus.Duplicate && Decision(r) == ImportDuplicateAction.Merge);
        var skipped = rows.Count(r => r.Status == ImportRowStatus.Duplicate) - merged;
        var failed = rows.Count(r => r.Status == ImportRowStatus.Invalid);
        var result = new CustomerImportResult(dryRun, rows.Count, created, merged, skipped, failed);
        if (dryRun)
        {
            return result;
        }

        using (ChangeActor.Use(ChangeChannel.Import, userId))
        {
            await WriteAsync(rows, Decision, ct);
        }

        db.Imports.Add(new ImportEntry
        {
            Kind = "Kunden",
            FileName = fileName.Length > 200 ? fileName[..200] : fileName,
            RowCount = rows.Count,
            Created = created,
            Merged = merged,
            Skipped = skipped,
            Failed = failed,
            CreatedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task WriteAsync(IReadOnlyList<CustomerImportRow> rows, Func<CustomerImportRow, ImportDuplicateAction> decision, CancellationToken ct)
    {
        var mergeIds = rows.Where(r => r.Status == ImportRowStatus.Duplicate && decision(r) == ImportDuplicateAction.Merge && r.ExistingCustomerId is not null)
            .Select(r => r.ExistingCustomerId!.Value).Distinct().ToList();
        var targets = await db.Customers.Where(c => mergeIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var consented = (await db.MarketingConsents.Where(c => mergeIds.Contains(c.CustomerId)).Select(c => c.CustomerId).ToListAsync(ct)).ToHashSet();
        var createdByRow = new Dictionary<int, Customer>();
        var now = time.GetUtcNow();
        var pending = 0;
        foreach (var row in rows.Where(r => r.Status != ImportRowStatus.Invalid))
        {
            var target = Target(row, decision(row), targets, createdByRow, now);
            if (target is not null && row.Consent && target.Email is { Length: > 0 } email && consented.Add(target.Id))
            {
                db.MarketingConsents.Add(new MarketingConsent
                {
                    CustomerId = target.Id,
                    Email = email,
                    Language = target.Language ?? "de",
                    State = MarketingConsentState.Confirmed,
                    Source = MarketingConsents.SourceImport,
                    RequestedAt = now,
                    ConfirmedAt = now,
                });
            }

            if (++pending >= BlockSize)
            {
                await db.SaveChangesAsync(ct);
                pending = 0;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Der Gast, den die Zeile anlegt oder ergänzt; <c>null</c> bei einer übersprungenen Dublette.</summary>
    private Customer? Target(CustomerImportRow row, ImportDuplicateAction decision, Dictionary<Guid, Customer> targets, Dictionary<int, Customer> createdByRow, DateTimeOffset now)
    {
        if (row.Status == ImportRowStatus.New)
        {
            var customer = CustomerEndpoints.NewCustomer(row.Input!, row.StoredPhone, now);
            db.Customers.Add(customer);
            createdByRow[row.Line.Number] = customer;
            return customer;
        }

        if (decision != ImportDuplicateAction.Merge)
        {
            return null;
        }

        var target = row.ExistingCustomerId is { } id ? targets.GetValueOrDefault(id) : createdByRow.GetValueOrDefault(row.EarlierRow!.Value);
        if (target is not null)
        {
            FillEmpty(target, CustomerEndpoints.NewCustomer(row.Input!, row.StoredPhone, now));
        }

        return target;
    }

    /// <summary>Ergänzen: nur leere Felder füllen, nie überschreiben.</summary>
    private static void FillEmpty(Customer target, Customer fresh)
    {
        target.FirstName = Fill(target.FirstName, fresh.FirstName) ?? "";
        target.Email = Fill(target.Email, fresh.Email);
        target.Phone = Fill(target.Phone, fresh.Phone);
        target.BirthDate ??= fresh.BirthDate;
        target.Street = Fill(target.Street, fresh.Street);
        target.PostalCode = Fill(target.PostalCode, fresh.PostalCode);
        target.City = Fill(target.City, fresh.City);
        target.Country = Fill(target.Country, fresh.Country);
        if (target.Salutation == CustomerSalutation.None)
        {
            target.Salutation = fresh.Salutation;
        }
    }

    private static string? Fill(string? current, string? candidate) => string.IsNullOrWhiteSpace(current) ? candidate ?? current : current;

    /// <summary>Liest die Werte der Zeile nach der Zuordnung; ohne Eingabe mit dem Grund für die Vorschau.</summary>
    private static (CustomerInput? Input, bool Consent, string? Error) Read(ImportLine line, CustomerImportMapping mapping, ImportDateFormat dateFormat, string? country)
    {
        string Value(CustomerImportField field) =>
            mapping.Columns.FirstOrDefault(c => c.Field == field) is { } column ? line.Cell(column.Column) : "";

        var lastName = Value(CustomerImportField.LastName);
        if (lastName.Length == 0)
        {
            return (null, false, "Nachname fehlt");
        }

        DateOnly? birthDate = null;
        if (Value(CustomerImportField.BirthDate) is { Length: > 0 } birthday)
        {
            birthDate = CustomerImportFields.ParseDate(birthday, dateFormat);
            if (birthDate is null)
            {
                return (null, false, "Geburtstag kein Datum");
            }
        }

        var phone = Value(CustomerImportField.Phone);
        if (phone.Length > 0 && PhoneNumbers.ToE164(phone, country).Length == 0)
        {
            return (null, false, "Telefon unleserlich");
        }

        var street = Value(CustomerImportField.Street);
        var postalCode = Value(CustomerImportField.PostalCode);
        var city = Value(CustomerImportField.City);
        var countryCode = Value(CustomerImportField.Country);
        var address = street.Length + postalCode.Length + city.Length + countryCode.Length > 0
            ? new CustomerAddress(Blank(street), Blank(postalCode), Blank(city), Blank(countryCode))
            : null;
        var input = new CustomerInput(Value(CustomerImportField.FirstName), lastName, Blank(Value(CustomerImportField.Email)), Blank(phone), birthDate, null,
            Salutation: CustomerImportFields.Salutation(Value(CustomerImportField.Salutation)), Address: address);
        var errors = CustomerEndpoints.CheckNew(input);
        return errors.Any
            ? (null, false, errors.First)
            : (input, CustomerImportFields.IsYes(Value(CustomerImportField.MarketingConsent)), null);
    }

    private static string? Blank(string value) => value.Length == 0 ? null : value;

    private static List<DuplicateKey> Keys(string lastName, string? phone, string? email, string? country)
    {
        var name = lastName.Trim().ToUpperInvariant();
        var keys = new List<DuplicateKey>(2);
        if (PhoneNumbers.ToE164(phone, country) is { Length: > 0 } e164)
        {
            keys.Add(new DuplicateKey($"{name}|tel|{e164}", "gleicher Nachname und gleiche Telefonnummer"));
        }

        if (email?.Trim() is { Length: > 0 } mail)
        {
            keys.Add(new DuplicateKey($"{name}|mail|{mail.ToUpperInvariant()}", "gleicher Nachname und gleiche E-Mail-Adresse"));
        }

        return keys;
    }

    private async Task<Dictionary<string, Known>> ExistingKeysAsync(string? country, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.Phone != null || c.Email != null)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone, c.Email })
            .ToListAsync(ct);
        var keys = new Dictionary<string, Known>(StringComparer.Ordinal);
        foreach (var c in customers)
        {
            var known = new Known(c.Id, $"{c.FirstName} {c.LastName}".Trim());
            foreach (var key in Keys(c.LastName, c.Phone, c.Email, country))
            {
                keys.TryAdd(key.Key, known);
            }
        }

        return keys;
    }
}

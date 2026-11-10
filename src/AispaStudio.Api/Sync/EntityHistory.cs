using System.Globalization;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Verlauf von Kunden, Behandlungen, Dienstplan und Zugängen für Menschen (Slice 91, ADR 0082): liest <c>sync.history</c> und macht aus jedem Eintrag einen Satz
/// wie „Geändert von Lena: Telefon 0664 111 → 0664 222“. Tabellen im Schema <c>health</c> nennen nur Art und Person, nie Inhalte. Personen mit ihrem Anzeigenamen
/// zur Zeit des Lesens. Wer den Verlauf sehen darf, entscheiden die Endpunkte (<see cref="HistoryEndpoints"/>).
/// </summary>
public sealed class EntityHistory(AispaDbContext db)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly string[] CustomerTables = ["Customers", "CustomerEmails", "MarketingConsents"];
    private static readonly string[] TreatmentTables = ["Treatments", "TreatmentLocationPrices", "TreatmentPriceRules"];
    private static readonly string[] RosterTables = ["RosterDays", "RosterDayEntries"];
    private static readonly string[] StaffTables = ["StaffAccess"];
    private static readonly string[] CompanyTables = ["Tenants"];

    /// <summary>Kundendaten und Anamnese; die Termine des Gastes haben ihren eigenen Verlauf.</summary>
    public Task<List<HistoryEntry>> ForCustomerAsync(Guid customerId, CancellationToken ct) => ReadAsync(
        "customer_id = {0} AND (table_schema = 'health' OR table_name = ANY ({1}))", [customerId, CustomerTables], ct);

    public Task<List<HistoryEntry>> ForTreatmentAsync(Guid treatmentId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_name = ANY ({1})", [treatmentId.ToString(), TreatmentTables], ct);

    /// <summary>Dienstplan und Abwesenheiten (Urlaub, Krankenstand, Frei, Schulung) einer Person.</summary>
    public Task<List<HistoryEntry>> ForRosterAsync(Guid employeeId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_name = ANY ({1})", [employeeId.ToString(), RosterTables], ct);

    /// <summary>Zugang und Rolle eines Benutzers.</summary>
    public Task<List<HistoryEntry>> ForStaffAsync(string userId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_name = ANY ({1})", [userId, StaffTables], ct);

    /// <summary>Name, Land und Firmendaten des Spas (R05 Slice 93); andere Einstellungen in derselben Zeile (Rabattgrenze, Einbettung …) stehen hier nicht.</summary>
    public Task<List<HistoryEntry>> ForCompanyAsync(Guid tenantId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_name = ANY ({1})", [tenantId.ToString(), CompanyTables], ct);

    /// <summary>Personalakte einer Person (R05 Slice 110): nur Art, Person und Zeit, nie Inhalte.</summary>
    public Task<List<HistoryEntry>> ForHrFileAsync(Guid employeeId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_schema = 'hr' AND table_name = 'HrEntries'", [employeeId.ToString()], ct);

    /// <summary>Hinweise, Sonderregelungen und Vollmachten einer Person (R05 Slice 111): nur Art, Person und Zeit, nie Inhalte.</summary>
    public Task<List<HistoryEntry>> ForRegulationsAsync(Guid employeeId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_schema = 'hr' AND table_name = ANY ({1})", [employeeId.ToString(), RegulationTables], ct);

    private static readonly string[] RegulationTables = ["StaffNotes", "StaffRules", "StaffPowers"];

    /// <summary>Zuständigkeiten, Zertifikate und Scans einer Person (R05 Slice 117): nur Art, Person und Zeit, nie Inhalte.</summary>
    public Task<List<HistoryEntry>> ForDutiesAsync(Guid employeeId, CancellationToken ct) => ReadAsync(
        "subject = {0} AND table_schema = 'hr' AND table_name = ANY ({1})", [employeeId.ToString(), DutyTables], ct);

    private static readonly string[] DutyTables = ["StaffDuties", "StaffCertificates", "StaffCertificateScans"];

    /// <summary>Export-Protokoll des Spas (Export/Import, Aufgaben A und E): wer wann welche Liste in welchem Format herausgeholt und welche Datei importiert hat.</summary>
    public Task<List<HistoryEntry>> ForExportsAsync(Guid tenantId, CancellationToken ct) => ReadAsync(
        "tenant_id = {0} AND table_schema = 'aispa' AND table_name IN ('Exports', 'Imports')", [tenantId], ct);

    /// <summary>
    /// Wer die Einwilligung des Gastes zuletzt in den Zustand <paramref name="state"/> gebracht hat (Name der Person, Export/Import B): der Nachweis „am Empfang erfasst von …“
    /// kommt aus dem Verlauf, nicht aus einer eigenen Spalte. Ohne Person (Gast, Import, Job) oder ohne Eintrag null.
    /// </summary>
    public async Task<string?> ConsentRecorderAsync(Guid customerId, MarketingConsentState state, CancellationToken ct)
    {
        var userId = await db.Database.SqlQueryRaw<string?>("""
            SELECT user_id AS "Value"
              FROM sync.history
             WHERE customer_id = {0} AND table_name = 'MarketingConsents' AND context ->> 'State' = {1}
               AND (operation = 'I' OR changes -> 'State' IS NOT NULL)
             ORDER BY changed_at DESC, seq DESC
             LIMIT 1
            """, customerId, state.ToString()).ToListAsync(ct);
        return userId.FirstOrDefault() is { } id
            ? await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? HistoryActor.DeletedPerson
            : null;
    }

    private async Task<List<HistoryEntry>> ReadAsync(string where, object[] args, CancellationToken ct)
    {
        // Nur @where kommt als Text in die Abfrage, und zwar eine feste Bedingung der Methoden oben; alle Werte sind Parameter.
#pragma warning disable EF1002
        var rows = await db.Database.SqlQueryRaw<HistoryRow>($"""
            SELECT {HistoryRow.Columns}
              FROM sync.history h
             WHERE {where}
             ORDER BY h.changed_at DESC, h.seq DESC
            """, args).ToListAsync(ct);
#pragma warning restore EF1002

        var relevant = Relevant(rows).ToList();
        var described = await DescribeAsync(relevant, ct);
        return [.. relevant.Select((row, i) => new HistoryEntry(row.ChangedAt, described[i].Text))];
    }

    /// <summary>
    /// Zeilen, die im Verlauf stehen: ein Dienstplan-Tag, der mit seinen Einträgen in einem Zug angelegt wurde, steht nur mit den Einträgen da; das Spa hat neben den
    /// Firmendaten weitere Einstellungen, deren Änderungen nicht in den Verlauf der Firmendaten gehören. <see cref="ActivityFeed"/> wendet dieselben Regeln in SQL an.
    /// </summary>
    private static IEnumerable<HistoryRow> Relevant(List<HistoryRow> rows)
    {
        var entryMoments = rows.Where(r => r.Table == "RosterDayEntries").Select(r => r.ChangedAt).ToHashSet();
        return rows
            .Where(r => !(r.Table == "RosterDays" && r.Operation == "I" && entryMoments.Contains(r.ChangedAt)))
            .Where(r => !(r.Table == "Tenants" && r.Operation == "U" && !Changes(r.Changes).Keys.Any(Columns["Tenants"].ContainsKey)));
    }

    /// <summary>Die Spalten der Firmendaten, die im Verlauf stehen; andere Einstellungen in derselben Zeile zählen nicht.</summary>
    internal static string[] CompanyColumns => [.. Columns["Tenants"].Keys];

    /// <summary>Satz und Werte je Zeile, in der Reihenfolge der Zeilen; Personen, Standorte und Mitarbeitende mit ihrem Namen zur Zeit des Lesens.</summary>
    internal async Task<List<Described>> DescribeAsync(IReadOnlyList<HistoryRow> rows, CancellationToken ct)
    {
        var parsed = rows.Select(r => (Row: r, Changes: Changes(r.Changes), Context: Context(r.Context))).ToList();
        var userIds = rows.Select(r => r.UserId).OfType<string>().Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var ids = parsed.SelectMany(p => p.Changes.Values.SelectMany(v => new[] { v.Old, v.New }).Concat(p.Context.Values)).SelectMany(Guids).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        foreach (var location in await db.Locations.AsNoTracking().Where(l => ids.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToListAsync(ct))
        {
            names[location.Id] = location.Name;
        }

        var describer = new Describer(people, names);
        return [.. parsed.Select(p => new Described(describer.Describe(p.Row, p.Changes, p.Context), describer.Values(p.Row, p.Changes)))];
    }

    /// <summary>Ein Satz für Menschen und die geänderten Werte alt → neu (ohne Spalten, die nur als „geändert“ erscheinen).</summary>
    internal sealed record Described(string Text, IReadOnlyList<ActivityValue> Values);

    private static Dictionary<string, (JsonElement Old, JsonElement New)> Changes(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (p.Value[0].Clone(), p.Value[1].Clone()), StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonElement> Context(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
    }

    private static Guid? GuidOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;

    /// <summary>Die Ids in einem Wert: eine einzelne oder die Elemente einer Liste (Standorte eines Zugangs).</summary>
    private static IEnumerable<Guid> Guids(JsonElement value) => value.ValueKind == JsonValueKind.Array
        ? value.EnumerateArray().Select(GuidOf).OfType<Guid>()
        : GuidOf(value) is { } id ? [id] : [];

    private enum Kind { Text, Money, Bool, Date, Time, Minutes, Percent, Employee, Location, Role, Locations, Flag, Consent }

    /// <summary>Zustand der Werbe-Einwilligung in Worten des Empfangs.</summary>
    private static readonly Dictionary<string, string> ConsentWords = new(StringComparer.Ordinal)
    {
        [nameof(MarketingConsentState.None)] = "nicht gefragt",
        [nameof(MarketingConsentState.Pending)] = "angefragt",
        [nameof(MarketingConsentState.Confirmed)] = "ja",
        [nameof(MarketingConsentState.Withdrawn)] = "widerrufen",
        [nameof(MarketingConsentState.Refused)] = "nein",
    };

    /// <summary>Spalten, die im Verlauf stehen: Bezeichnung und Art der Anzeige. Andere Spalten (Schlüssel, Zeitstempel, Interna) bleiben weg; <c>Flag</c> nennt nur, dass sich etwas geändert hat.</summary>
    private static readonly Dictionary<string, Dictionary<string, (string Label, Kind Kind)>> Columns = new(StringComparer.Ordinal)
    {
        ["Customers"] = new(StringComparer.Ordinal)
        {
            ["FirstName"] = ("Vorname", Kind.Text),
            ["LastName"] = ("Nachname", Kind.Text),
            ["PreferredName"] = ("Rufname", Kind.Text),
            ["Salutation"] = ("Anrede", Kind.Flag),
            ["Pronouns"] = ("Pronomen", Kind.Text),
            ["TitlesBefore"] = ("Titel", Kind.Text),
            ["TitlesAfter"] = ("Titel nachgestellt", Kind.Text),
            ["TitleInGreeting"] = ("Titel in der Anrede", Kind.Flag),
            ["Street"] = ("Straße", Kind.Text),
            ["PostalCode"] = ("PLZ", Kind.Text),
            ["City"] = ("Ort", Kind.Text),
            ["Country"] = ("Land", Kind.Text),
            ["Email"] = ("E-Mail", Kind.Text),
            ["Phone"] = ("Telefon", Kind.Text),
            ["BirthDate"] = ("Geburtstag", Kind.Date),
            ["Note"] = ("Notiz", Kind.Flag),
            ["ReceptionNote"] = ("Notiz für die Rezeption", Kind.Flag),
            ["BillingName"] = ("Rechnungsempfänger", Kind.Text),
            ["BillingAddress"] = ("Rechnungsanschrift", Kind.Text),
            ["BillingVatId"] = ("UID", Kind.Text),
            ["DiscountPercent"] = ("Rabatt", Kind.Percent),
            ["DiscountScope"] = ("Rabatt gilt für", Kind.Flag),
            ["PreferredEmployeeId"] = ("Bevorzugte Person", Kind.Employee),
            ["PreferredEmployeeFixed"] = ("Bevorzugte Person fest", Kind.Bool),
            ["PreferredWeekdays"] = ("Bevorzugte Wochentage", Kind.Flag),
            ["PreferredFrom"] = ("Bevorzugt ab", Kind.Time),
            ["PreferredUntil"] = ("Bevorzugt bis", Kind.Time),
            ["PreferredGender"] = ("Wunsch Therapeut:in", Kind.Flag),
            ["Language"] = ("Sprache", Kind.Text),
        },
        ["CustomerEmails"] = new(StringComparer.Ordinal) { ["Address"] = ("E-Mail-Adresse", Kind.Text) },
        ["MarketingConsents"] = new(StringComparer.Ordinal) { ["State"] = ("Werbe-Einwilligung", Kind.Consent), ["Email"] = ("Adresse der Einwilligung", Kind.Text) },
        ["Treatments"] = new(StringComparer.Ordinal)
        {
            ["Name"] = ("Name", Kind.Text),
            ["DurationMinutes"] = ("Dauer", Kind.Minutes),
            ["PreparationMinutes"] = ("Vorbereitung", Kind.Minutes),
            ["FollowUpMinutes"] = ("Nachbereitung", Kind.Minutes),
            ["HandsOnMinutes"] = ("Zeit am Gast", Kind.Minutes),
            ["ProcessingMinutes"] = ("Einwirkzeit", Kind.Minutes),
            ["Price"] = ("Preis", Kind.Money),
            ["IsActive"] = ("Im Angebot", Kind.Bool),
            ["Category"] = ("Gruppe", Kind.Text),
            ["NoShowFeePercent"] = ("Ausfallgebühr", Kind.Percent),
            ["DepositPercent"] = ("Anzahlung", Kind.Percent),
            ["GuestHint"] = ("Hinweis an Gäste", Kind.Flag),
            ["CheckAnamnesis"] = ("Anamnese prüfen", Kind.Bool),
        },
        ["TreatmentLocationPrices"] = new(StringComparer.Ordinal) { ["Price"] = ("Preis", Kind.Money) },
        ["TreatmentPriceRules"] = new(StringComparer.Ordinal)
        {
            ["Name"] = ("Name", Kind.Text),
            ["Days"] = ("Wochentage", Kind.Flag),
            ["From"] = ("Von", Kind.Time),
            ["Until"] = ("Bis", Kind.Time),
            ["Price"] = ("Preis", Kind.Money),
            ["LocationId"] = ("Standort", Kind.Location),
        },
        ["RosterDays"] = new(StringComparer.Ordinal) { ["Note"] = ("Notiz", Kind.Flag) },
        ["StaffAccess"] = new(StringComparer.Ordinal)
        {
            ["Role"] = ("Rolle", Kind.Role),
            ["AllLocations"] = ("Alle Standorte", Kind.Bool),
            ["LocationIds"] = ("Standorte", Kind.Locations),
            ["IsActive"] = ("Zugang aktiv", Kind.Bool),
            ["ExpiresAt"] = ("Befristet bis", Kind.Date),
            ["AmountReports"] = ("Berichte mit Beträgen", Kind.Bool),
            ["PlainReports"] = ("Auswertungen ohne Beträge", Kind.Bool),
        },
        ["Tenants"] = new(StringComparer.Ordinal)
        {
            ["Name"] = ("Name", Kind.Text),
            ["Country"] = ("Land", Kind.Text),
            ["LegalName"] = ("Firmenwortlaut", Kind.Text),
            ["VatId"] = ("UID-Nummer", Kind.Text),
            ["CompanyRegisterNumber"] = ("Firmenbuchnummer", Kind.Text),
            ["CompanyRegisterCourt"] = ("Firmenbuchgericht", Kind.Text),
            ["Street"] = ("Straße", Kind.Text),
            ["PostalCode"] = ("PLZ", Kind.Text),
            ["City"] = ("Ort", Kind.Text),
            ["Phone"] = ("Telefon", Kind.Text),
            ["Email"] = ("E-Mail", Kind.Text),
            ["Website"] = ("Website", Kind.Text),
            ["AccountHolder"] = ("Kontoinhaber", Kind.Text),
            ["Iban"] = ("IBAN", Kind.Text),
            ["Bic"] = ("BIC", Kind.Text),
            ["BusinessPurpose"] = ("Unternehmensgegenstand", Kind.Text),
            ["SupervisoryAuthority"] = ("Aufsichtsbehörde", Kind.Text),
            ["ChamberAndRules"] = ("Kammer und Vorschriften", Kind.Text),
        },
    };

    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        [nameof(StaffRole.Owner)] = "Inhaber:in",
        [nameof(StaffRole.GeneralManager)] = "Manager:in",
        [nameof(StaffRole.Manager)] = "Standortleitung",
        [nameof(StaffRole.Reception)] = "Rezeption",
        [nameof(StaffRole.Therapist)] = "Therapeut:in",
        [nameof(StaffRole.Backoffice)] = "Backoffice",
    };

    private static readonly Dictionary<string, string> RosterKinds = new(StringComparer.Ordinal)
    {
        [nameof(RosterKind.Work)] = "Dienst",
        [nameof(RosterKind.Vacation)] = "Urlaub",
        [nameof(RosterKind.Sick)] = "Krankenstand",
        [nameof(RosterKind.DayOff)] = "Frei",
        [nameof(RosterKind.Training)] = "Schulung",
    };

    private static readonly Dictionary<string, string> RequestKinds = new(StringComparer.Ordinal)
    {
        [nameof(InboundMailKind.BookingRequest)] = "Buchungsanfrage",
        [nameof(InboundMailKind.Cancellation)] = "Absage",
        [nameof(InboundMailKind.Reschedule)] = "Verschiebewunsch",
        [nameof(InboundMailKind.Question)] = "Frage",
        [nameof(InboundMailKind.TreatwellBooking)] = "Treatwell-Buchung",
        [nameof(InboundMailKind.TreatwellCancellation)] = "Treatwell-Storno",
        [nameof(InboundMailKind.TreatwellChange)] = "Treatwell-Änderung",
    };

    private static readonly Dictionary<string, string> HrKinds = new(StringComparer.Ordinal)
    {
        [nameof(HrEntryKind.Conversation)] = "Gespräch",
        [nameof(HrEntryKind.Note)] = "Anmerkung",
        [nameof(HrEntryKind.Warning)] = "Verwarnung",
    };

    private sealed class Describer(Dictionary<string, string> people, Dictionary<Guid, string> names)
    {
        public string Describe(HistoryRow row, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context)
        {
            var by = HistoryActor.Text(HistoryActor.Channel(row.Channel), row.UserId, people);
            if (row.Schema == "health")
            {
                // Gesundheitsdaten: nur Art, Person und Zeit, nie Inhalte (Ruling Ä184).
                var subject = row.Table == "AnamnesisScans" ? "Anamnese-Scan" : "Anamnese";
                return $"{subject} {Verb(row.Operation)} {by}";
            }

            if (row.Schema == "hr")
            {
                // Personalakte: nur Art, Person und Zeit, nie Inhalte.
                var entry = row.Table switch
                {
                    "StaffNotes" => "Hinweis",
                    "StaffRules" => "Sonderregelung",
                    "StaffPowers" => "Vollmacht",
                    "StaffDuties" => "Zuständigkeit",
                    "StaffCertificates" => "Zertifikat",
                    "StaffCertificateScans" => "Scan",
                    "DutyTypes" => "Eintrag der Liste der Zuständigkeiten",
                    _ => context.TryGetValue("Kind", out var hrKind) && Text(hrKind) is { } key && HrKinds.TryGetValue(key, out var hrLabel) ? hrLabel : "Eintrag",
                };
                return HistoryActor.Channel(row.Channel) == ChangeChannel.Job ? $"{entry} automatisch {Verb(row.Operation)}" : $"{entry} {Verb(row.Operation)} {by}";
            }

            return row.Table switch
            {
                "RosterDayEntries" => RosterEntry(row.Operation, by, context),
                "RosterDays" => RosterDay(row.Operation, by, changes, context),
                "TreatmentLocationPrices" => PriceAtLocation(row.Operation, by, changes, context),
                "TreatmentPriceRules" => PriceRule(row.Operation, by, changes, context),
                "CustomerEmails" => Email(row.Operation, by, changes, context),
                "InboundMails" => Request(row.Operation, HistoryActor.Channel(row.Channel), by, changes, context),
                "Exports" => Export(by, context),
                "Imports" => Import(by, context),
                "MarketingConsents" => Consent(row.Operation, by, changes, context),
                _ => row.Operation switch
                {
                    "I" => row.Table == "StaffAccess" ? $"Zugang angelegt {by}{RoleSuffix(context)}" : $"Angelegt {by}",
                    "D" => row.Table == "StaffAccess" ? $"Zugang entfernt {by}" : $"Gelöscht {by}",
                    _ => $"Geändert {by}{ChangeList(row.Table, changes)}",
                },
            };
        }

        /// <summary>„Buchungsanfrage eingegangen (Mail)“, „Anfrage als erledigt markiert von Lena“, „Freie Zeiten vorgeschlagen von der KI“: Eingang und Entscheidung, nie Inhalt.</summary>
        private static string Request(
            string operation, ChangeChannel channel, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context)
        {
            if (operation == "I")
            {
                var kind = context.TryGetValue("Kind", out var k) && Text(k) is { } key && RequestKinds.TryGetValue(key, out var label) ? label : "Anfrage";
                var way = context.TryGetValue("Channel", out var c) && Enum.TryParse<BookingChannel>(Text(c), out var booking) ? booking : BookingChannel.Email;
                return $"{kind} eingegangen ({MailIntakeProcessor.ChannelName(way)})";
            }

            var status = changes.TryGetValue("Status", out var s) ? Text(s.New) : null;
            var reply = changes.TryGetValue("ReplyStatus", out var r) ? Text(r.New) : null;
            var (what, verb) = status switch
            {
                nameof(InboundMailStatus.Booked) => ("Anfrage", "gebucht"),
                nameof(InboundMailStatus.Proposed) => ("Freie Zeiten", "vorgeschlagen"),
                nameof(InboundMailStatus.Ignored) => ("Anfrage", "als erledigt markiert"),
                nameof(InboundMailStatus.Cancelled) => ("Termin zur Anfrage", "storniert"),
                _ when reply == nameof(ReplyStatus.Sending) => ("Antwort", "freigegeben"),
                _ => ("Anfrage", "geändert"),
            };
            return channel == ChangeChannel.Job ? $"{what} automatisch {verb}" : $"{what} {verb} {by}";
        }

        private static string Verb(string operation) => operation switch { "I" => "angelegt", "D" => "gelöscht", _ => "geändert" };

        private string RoleSuffix(Dictionary<string, JsonElement> context) =>
            context.TryGetValue("Role", out var role) && Text(role) is not null ? $": {Value(Kind.Role, role)}" : "";

        private string Email(string operation, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context) => operation switch
        {
            "I" => $"E-Mail-Adresse {Address(context)} hinzugefügt {by}",
            "D" => $"E-Mail-Adresse {Address(context)} entfernt {by}",
            _ => $"E-Mail-Adresse geändert {by}{ChangeList("CustomerEmails", changes)}",
        };

        /// <summary>„Export Kundenliste (Excel, 312 Zeilen, Suche „Huber“) von Lena“; Exporte werden nur angelegt, nie geändert.</summary>
        private static string Export(string by, Dictionary<string, JsonElement> context)
        {
            var kind = context.TryGetValue("Kind", out var k) ? Text(k) ?? "Liste" : "Liste";
            var format = context.TryGetValue("Format", out var f) && Text(f) is { } code && Enum.TryParse<ExportFormat>(code, out var parsed) ? Exports.ExportFiles.Label(parsed) : "Datei";
            var rows = context.TryGetValue("Rows", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : 0;
            var filter = context.TryGetValue("Filter", out var fi) && Text(fi) is { Length: > 0 } text ? $", {text}" : "";
            return $"Export {kind} ({format}, {rows} {(rows == 1 ? "Zeile" : "Zeilen")}{filter}) {by}";
        }

        /// <summary>„Import Kunden: 240 neu, 12 ergänzt, 3 übersprungen, 1 fehlerhaft (kunden.xlsx, 256 Zeilen) von Lena“; Fehler nur, wenn es welche gab.</summary>
        private static string Import(string by, Dictionary<string, JsonElement> context)
        {
            int Count(string key) => context.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
            var kind = context.TryGetValue("Kind", out var k) ? Text(k) ?? "Daten" : "Daten";
            var file = context.TryGetValue("File", out var f) && Text(f) is { Length: > 0 } name ? $"{name}, " : "";
            var failed = Count("Failed") > 0 ? $", {Count("Failed")} fehlerhaft" : "";
            var rows = Count("Rows");
            return $"Import {kind}: {Count("Created")} neu, {Count("Merged")} ergänzt, {Count("Skipped")} übersprungen{failed} ({file}{rows} {(rows == 1 ? "Zeile" : "Zeilen")}) {by}";
        }

        /// <summary>„Werbe-Einwilligung: ja von Lena“ beim ersten Eintrag; Änderungen als „Werbe-Einwilligung ja → nein“.</summary>
        private string Consent(string operation, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context) => operation switch
        {
            "I" => $"Werbe-Einwilligung: {(context.TryGetValue("State", out var s) ? Value(Kind.Consent, s) : "–")} {by}",
            "D" => $"Werbe-Einwilligung entfernt {by}",
            _ => $"Werbe-Einwilligung geändert {by}{ChangeList("MarketingConsents", changes)}",
        };

        private static string Address(Dictionary<string, JsonElement> context) => context.TryGetValue("Address", out var address) ? Text(address) ?? "–" : "–";

        private string PriceAtLocation(string operation, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context)
        {
            var place = context.TryGetValue("LocationId", out var id) ? Value(Kind.Location, id) : "–";
            var price = context.TryGetValue("Price", out var p) ? Value(Kind.Money, p) : "–";
            return operation switch
            {
                "I" => $"Preis am Standort {place} auf {price} gesetzt {by}",
                "D" => $"Preis am Standort {place} entfernt {by}",
                _ => $"Preis am Standort {place} geändert {by}{ChangeList("TreatmentLocationPrices", changes)}",
            };
        }

        private string PriceRule(string operation, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context)
        {
            var name = context.TryGetValue("Name", out var n) ? Text(n) ?? "–" : "–";
            var price = context.TryGetValue("Price", out var p) ? Value(Kind.Money, p) : "–";
            return operation switch
            {
                "I" => $"Preisregel „{name}“ angelegt {by}: {price}",
                "D" => $"Preisregel „{name}“ entfernt {by}",
                _ => $"Preisregel „{name}“ geändert {by}{ChangeList("TreatmentPriceRules", changes)}",
            };
        }

        private string RosterDay(string operation, string by, Dictionary<string, (JsonElement Old, JsonElement New)> changes, Dictionary<string, JsonElement> context)
        {
            var date = context.TryGetValue("Date", out var d) ? Value(Kind.Date, d) : "–";
            return operation switch
            {
                "I" => $"Dienstplan am {date} angelegt {by}",
                "D" => $"Dienstplan am {date} zurückgesetzt {by}",
                _ => $"Dienstplan am {date} geändert {by}{ChangeList("RosterDays", changes)}",
            };
        }

        private string RosterEntry(string operation, string by, Dictionary<string, JsonElement> context)
        {
            var kind = context.TryGetValue("Kind", out var k) && Text(k) is { } key && RosterKinds.TryGetValue(key, out var label) ? label : "Eintrag";
            var date = context.TryGetValue("Date", out var d) ? Value(Kind.Date, d) : "–";
            var times = context.TryGetValue("Start", out var s) && context.TryGetValue("End", out var e) && s.ValueKind == JsonValueKind.String && e.ValueKind == JsonValueKind.String
                ? $" {Value(Kind.Time, s)}–{Value(Kind.Time, e)}"
                : "";
            var place = context.TryGetValue("LocationId", out var l) && GuidOf(l) is not null ? $" in {Value(Kind.Location, l)}" : "";
            return $"{kind} am {date}{times}{place} {(operation == "D" ? "entfernt" : "eingetragen")} {by}";
        }

        /// <summary>„: Telefon 0664 111 → 0664 222; Notiz geändert“; leer, wenn keine bekannte Spalte betroffen ist.</summary>
        private string ChangeList(string table, Dictionary<string, (JsonElement Old, JsonElement New)> changes)
        {
            if (!Columns.TryGetValue(table, out var known))
            {
                return "";
            }

            var parts = changes.Where(c => known.ContainsKey(c.Key)).Select(c =>
            {
                var (label, kind) = known[c.Key];
                return kind == Kind.Flag ? $"{label} geändert" : $"{label} {Value(kind, c.Value.Old)} → {Value(kind, c.Value.New)}";
            }).ToList();
            return parts.Count == 0 ? "" : $": {string.Join("; ", parts)}";
        }

        /// <summary>Geänderte Spalten als Werte für die Verlaufsseite; Gesundheitsdaten und Personalakte tragen keine, ebenso Spalten, die nur „geändert“ heißen.</summary>
        public List<ActivityValue> Values(HistoryRow row, Dictionary<string, (JsonElement Old, JsonElement New)> changes)
        {
            if (row.Schema is "health" or "hr" || !Columns.TryGetValue(row.Table, out var known))
            {
                return [];
            }

            return [.. changes.Where(c => known.TryGetValue(c.Key, out var column) && column.Kind != Kind.Flag)
                .Select(c => new ActivityValue(known[c.Key].Label, Value(known[c.Key].Kind, c.Value.Old), Value(known[c.Key].Kind, c.Value.New)))];
        }

        private string Value(Kind kind, JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return "–";
            }

            switch (kind)
            {
                case Kind.Money when value.ValueKind == JsonValueKind.Number:
                    return $"{value.GetDecimal().ToString("N2", German)} €";
                case Kind.Minutes when value.ValueKind == JsonValueKind.Number:
                    return $"{value.GetInt32()} min";
                case Kind.Percent when value.ValueKind == JsonValueKind.Number:
                    return $"{value.GetDecimal().ToString("0.##", German)} %";
                case Kind.Bool:
                    return value.ValueKind == JsonValueKind.True ? "ja" : "nein";
                case Kind.Date when Text(value) is { Length: >= 10 } date && DateTime.TryParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                    return day.ToString("d.M.yyyy", German);
                case Kind.Time when Text(value) is { Length: >= 5 } time:
                    return time[..5];
                case Kind.Role:
                    return Text(value) is { } role && Roles.TryGetValue(role, out var roleName) ? roleName : Text(value) ?? "–";
                case Kind.Consent:
                    return Text(value) is { } state && ConsentWords.TryGetValue(state, out var word) ? word : Text(value) ?? "–";
                case Kind.Employee or Kind.Location:
                    return GuidOf(value) is { } id && names.TryGetValue(id, out var name) ? name : "–";
                case Kind.Locations:
                    var places = Guids(value).Select(id => names.GetValueOrDefault(id, "–")).ToList();
                    return places.Count == 0 ? "–" : string.Join(", ", places);
                default:
                    var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString();
                    return text.Length == 0 ? "–" : text.Length > 60 ? $"{text[..57]}…" : text;
            }
        }

        private static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}

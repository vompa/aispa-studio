using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>Wo ein Feld in der Verwaltung steht: am Standort, in den Firmendaten, bei einer Behandlung.</summary>
internal enum FieldScope
{
    Location,
    Company,
    Treatment,
}

/// <summary>
/// Eine Angabe, die das Spa pflegt und die der Assistent für Antworten braucht. <paramref name="Id"/> ist die Feldkennung (auch der Anker <c>data-field</c> in der Maske),
/// <paramref name="Name"/> der Name im Satz („Anreise“), <paramref name="Page"/> und <paramref name="Tab"/> die Verwaltungsseite, <paramref name="Where"/> der Ort in Worten,
/// <paramref name="Right"/> das Recht, das Feld zu ändern, <paramref name="FillLabel"/> die Beschriftung des Verweises bei „Dazu ist nichts hinterlegt“, <paramref name="Plural"/> ob der Name im Plural steht („Öffnungszeiten fehlen“).
/// </summary>
internal sealed record FieldMask(string Id, string Name, string Page, string? Tab, string Where, AccessRight Right, FieldScope Scope, string FillLabel, bool Plural = false);

/// <summary>Eine fehlende Angabe; bei Feldern einer Behandlung nennt <paramref name="Subject"/> ihren Namen und <paramref name="TargetId"/> ihre Kennung.</summary>
internal readonly record struct FieldGap(string Id, string? Subject = null, Guid? TargetId = null);

/// <summary>Die Feldkennungen; Verweise und Hinweise nehmen nur diese, nie von Hand gebaute Seiten und Reiter.</summary>
internal static class FieldIds
{
    public const string OpeningHours = "location.openingHours";
    public const string Region = "location.region";
    public const string Phone = "location.phone";
    public const string Email = "location.email";
    public const string Street = "location.street";
    public const string Directions = "location.directions";
    public const string CancellationPolicy = "location.cancellationPolicy";
    public const string Rooms = "location.rooms";
    public const string Areas = "location.areas";
    public const string Website = "company.website";
    public const string Price = "treatment.price";
    public const string Duration = "treatment.duration";
    public const string Promotions = "promotions.any";
    public const string BusinessContacts = "businessInfo.contacts";
}

/// <summary>
/// Die zentrale Tabelle Feldkennung → Maske (ADR 0072, Nachtrag Runde 5b): Verwaltungsroute mit Anker, Beschriftung des Orts und benötigtes Recht. Alle Verweise
/// der Antwortgeber („Jetzt ergänzen“, „Öffnungszeiten eintragen“) entstehen hier; ein Test verlangt, dass jede Feldkennung in der Tabelle steht.
/// </summary>
internal static class FieldMasks
{
    private const string Locations = "Stammdaten › Standorte › ";

    public static readonly IReadOnlyList<FieldMask> All =
    [
        new(FieldIds.OpeningHours, "Öffnungszeiten", "masterData", "locations", Locations + "Öffnungszeiten", AccessRight.ManageLocation, FieldScope.Location, "Öffnungszeiten eintragen", Plural: true),
        new(FieldIds.Region, "Bundesland oder Kanton", "masterData", "locations", Locations + "Bundesland / Kanton", AccessRight.ManageLocation, FieldScope.Location, "Bundesland oder Kanton eintragen"),
        new(FieldIds.Phone, "Telefon", "masterData", "locations", Locations + "Anschrift und Kontakt", AccessRight.ManageLocation, FieldScope.Location, "Kontaktdaten eintragen"),
        new(FieldIds.Email, "E-Mail", "masterData", "locations", Locations + "Anschrift und Kontakt", AccessRight.ManageLocation, FieldScope.Location, "Kontaktdaten eintragen"),
        new(FieldIds.Street, "Adresse", "masterData", "locations", Locations + "Anschrift und Kontakt", AccessRight.ManageLocation, FieldScope.Location, "Kontaktdaten eintragen"),
        new(FieldIds.Directions, "Anreise", "masterData", "locations", Locations + "Anreise und Parkplätze", AccessRight.ManageLocation, FieldScope.Location, "Anreise eintragen"),
        new(FieldIds.CancellationPolicy, "Stornoregel", "masterData", "locations", Locations + "Online-Buchung", AccessRight.ManageLocation, FieldScope.Location, "Stornoregel eintragen"),
        new(FieldIds.Rooms, "Räume", "masterData", "locations", Locations + "Räume", AccessRight.ManageLocation, FieldScope.Location, "Räume anlegen", Plural: true),
        new(FieldIds.Areas, "Bereiche", "masterData", "locations", Locations + "Räume", AccessRight.ManageLocation, FieldScope.Location, "Bereiche anlegen", Plural: true),
        new(FieldIds.Website, "Website", "masterData", "company", "Stammdaten › Firmendaten › Sitz und Kontakt", AccessRight.ManageOrganization, FieldScope.Company, "Website eintragen"),
        new(FieldIds.Price, "Preis", "masterData", "treatments", "Stammdaten › Behandlungen › Grunddaten", AccessRight.ManageCatalog, FieldScope.Treatment, "Preis eintragen"),
        new(FieldIds.Duration, "Dauer", "masterData", "treatments", "Stammdaten › Behandlungen › Grunddaten", AccessRight.ManageCatalog, FieldScope.Treatment, "Dauer eintragen"),
        new(FieldIds.Promotions, "Aktionen", "promotions", null, "Aktionen", AccessRight.ManageCatalog, FieldScope.Location, "Aktionen verwalten", Plural: true),
        new(FieldIds.BusinessContacts, "Betriebsinformationen", "businessInfo", null, "Betriebsinformationen", AccessRight.ManageLocation, FieldScope.Location, "Betriebsinformationen öffnen", Plural: true),
    ];

    public const string FillNow = "Jetzt ergänzen";

    public const string ManagementOnly = "Nur Leitung";

    public static FieldMask Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"Unbekannte Feldkennung {id}.", nameof(id));

    /// <summary>
    /// Der Verweis in die Maske am Feld; <c>null</c>, wenn die Person das Recht dazu nicht hat. <paramref name="label"/> ist ohne Angabe die Beschriftung der Tabelle
    /// („Anreise eintragen“).
    /// </summary>
    public static AssistantLink? Link(AnswerContext context, string fieldId, string? label = null, Guid? targetId = null)
    {
        var mask = Get(fieldId);
        return context.Has(mask.Right)
            ? new AssistantLink(AssistantLinkKind.Setting, label ?? mask.FillLabel, mask.Page, mask.Tab, context.Location.Id, TargetId: targetId, Field: fieldId)
            : null;
    }

    /// <summary>Der Verweis auf die Seite zum Ansehen, ohne Recht und ohne Feld (für Personen, die dort nur lesen, z. B. die Betriebsinformationen).</summary>
    public static AssistantLink View(AnswerContext context, string fieldId, string? label = null)
    {
        var mask = Get(fieldId);
        return new AssistantLink(AssistantLinkKind.Setting, label ?? mask.FillLabel, mask.Page, mask.Tab, context.Location.Id);
    }

    /// <summary>
    /// Der Abschnitt „Unvollständig“ zu den fehlenden Angaben: je Maske (und Behandlung) ein Satz, ein Verweis „Jetzt ergänzen“ am ersten fehlenden Feld oder, ohne Recht,
    /// der Hinweis „Nur Leitung“. <paramref name="except"/> ist der Verweis, den die Antwort schon trägt: Angaben aus derselben Maske stehen dann nicht noch einmal da.
    /// </summary>
    public static IReadOnlyList<AssistantIncomplete> Incomplete(AnswerContext context, IEnumerable<FieldGap> gaps, AssistantLink? except = null)
    {
        var covered = except?.Field is { } field ? Get(field) : null;
        return
        [
            .. gaps.Distinct()
                .Select(g => (Gap: g, Mask: Get(g.Id)))
                .Where(x => covered is null || x.Mask.Where != covered.Where)
                .GroupBy(x => (x.Mask.Where, x.Gap.Subject, x.Gap.TargetId))
                .Select(group =>
                {
                    var first = group.First();
                    var names = group.Select(x => x.Mask.Name).ToList();
                    var link = Link(context, first.Gap.Id, FillNow, first.Gap.TargetId);
                    return new AssistantIncomplete(
                        $"Unvollständig: {Joined(names)} {(names.Count == 1 && !first.Mask.Plural ? "fehlt" : "fehlen")} {WhereText(first.Mask.Scope, context, first.Gap.Subject)}.",
                        first.Mask.Where, [.. group.Select(x => x.Gap.Id)], link, link is null ? ManagementOnly : null);
                }),
        ];
    }

    private static string WhereText(FieldScope scope, AnswerContext context, string? subject) => scope switch
    {
        FieldScope.Company => "in den Firmendaten",
        FieldScope.Treatment => $"bei {subject}",
        _ => $"beim {context.Place}",
    };

    private static string Joined(List<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} und {names[^1]}";
}

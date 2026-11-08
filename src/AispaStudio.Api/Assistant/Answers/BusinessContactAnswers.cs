using AispaStudio.Api.BusinessInfo;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// „Wer ist unser Installateur?“, „Nummer der Versicherung“, „Wer ist für die Heizung zuständig?“, „Kontakt der Lohnverrechnung“, „Wen rufe ich bei Wasserschaden an?“:
/// die Betriebsinformationen des Standorts (<see cref="BusinessContactService"/>, ADR 0074). Gefunden wird nach Kategorie und nach Wörtern der Frage in Bezeichnung,
/// Firma, Ansprechperson und Zuständigkeit. Einträge der Stufe „Leitung“ nennt der Assistent nur Personen, die sie lesen dürfen; wer sie nicht lesen darf und sonst
/// nichts findet, bekommt „keine Berechtigung“, nicht „nichts hinterlegt“. Die Antwort wird nie von der KI umformuliert (<see cref="ModelAnswerPhraser"/>): Nummern
/// müssen stimmen, und die Kontaktdaten gehen nicht an ein Sprachmodell.
/// </summary>
internal sealed class BusinessContactAnswers(BusinessContactService contacts) : IAnswerGiver
{
    private const string Source = "Betriebsinformationen";

    public IReadOnlyCollection<BusinessTopic> Topics { get; } = [BusinessTopic.BusinessContacts];

    public async Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct)
    {
        var topic = question.Topic;
        var category = Enum.TryParse<BusinessContactCategory>(question.Detail, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : (BusinessContactCategory?)null;
        var words = BusinessContactSearch.Words(question.Subject ?? question.Text);
        var readLink = FieldMasks.View(context, FieldIds.BusinessContacts);
        if (category is null && words.Count == 0)
        {
            return Blocks.NotFound(topic, "Bitte nennen, wofür oder wen: zum Beispiel „Wer ist für die Heizung zuständig?“ oder „Nummer der Versicherung“.", Source) with { Link = readLink };
        }

        var found = BusinessContactSearch.Find(await contacts.ForLocationAsync(context.Location.Id, ct), category, words);
        var readable = found.Where(c => BusinessContactRules.CanRead(context.Access, c)).ToList();
        if (readable.Count > 0)
        {
            return Answer(topic, readable, readLink);
        }

        if (found.Count > 0)
        {
            return Blocks.Denied(topic, "Einträge der Stufe „Leitung“ sehen Inhaber, Manager, Standortleitung und Backoffice.");
        }

        var what = words.Count > 0 ? $"„{string.Join(' ', words.Select(w => w.Display))}“" : BusinessContactLabels.Of(category!.Value);
        var manage = context.Fill(FieldIds.BusinessContacts);
        return Blocks.NotStored(topic, what, Source, manage) with { Link = manage ?? readLink };
    }

    private static AssistantBlock Answer(BusinessTopic topic, List<BusinessContact> found, AssistantLink link)
    {
        if (found.Count == 1)
        {
            var contact = found[0];
            return Blocks.Answer(topic, Sentence(contact), Source, Blocks.Table(["Angabe", "Wert"], Details(contact)), link, Copy(contact));
        }

        var names = string.Join(", ", found.Take(5).Select(c => c.Designation)) + (found.Count > 5 ? " u. a." : "");
        var more = found.Count > Blocks.MaxRows ? $" Die ersten {Blocks.MaxRows} stehen in der Tabelle, alle auf der Seite Betriebsinformationen." : "";
        return Blocks.Answer(topic, $"Dazu sind {found.Count} Einträge hinterlegt: {names}.{more}", Source,
            Blocks.Table(["Bezeichnung", "Firma", "Ansprechperson", "Telefon"], found.Select(c => (IReadOnlyList<string>)
                [c.Designation, c.Company ?? "–", c.ContactPerson ?? "–", c.Phone ?? c.Mobile ?? "–"])),
            link, string.Join('\n', found.Take(Blocks.MaxRows).Select(OneLine)));
    }

    /// <summary>Der Satz zu einem Eintrag: Bezeichnung mit Firma und Ansprechperson, Erreichbarkeit, bei Versicherungen Hotline und Polizze, Zuständigkeit.</summary>
    private static string Sentence(BusinessContact c)
    {
        var parts = new List<string>
        {
            $"{c.Designation}{(c.Company is null ? "" : $": {c.Company}")}{(c.ContactPerson is null ? "" : $", Ansprechperson {c.ContactPerson}")}.",
        };
        var reach = Pairs(("Telefon", c.Phone), ("Mobil", c.Mobile), ("E-Mail", c.Email));
        if (reach.Count > 0)
        {
            parts.Add(string.Join("; ", reach) + ".");
        }

        var insurance = Pairs(("Schadenshotline", c.ClaimsHotline), ("Polizzennummer", c.PolicyNumber));
        if (insurance.Count > 0)
        {
            parts.Add(string.Join("; ", insurance) + ".");
        }

        if (c.Responsibility is not null)
        {
            parts.Add($"Zuständig für: {c.Responsibility}.");
        }

        return string.Join(' ', parts);
    }

    private static List<string> Pairs(params (string Label, string? Value)[] values) =>
        [.. values.Where(v => v.Value is not null).Select(v => $"{v.Label} {v.Value}")];

    /// <summary>Die Angaben als Tabelle, die wichtigsten zuerst (höchstens zehn Zeilen); alles steht im Kopiertext und auf der Seite.</summary>
    private static IEnumerable<IReadOnlyList<string>> Details(BusinessContact c) =>
        Fields(c).Where(f => f.Value is not null).Select(f => (IReadOnlyList<string>)[f.Label, f.Value!]);

    private static (string Label, string? Value)[] Fields(BusinessContact c) =>
    [
        ("Bezeichnung", c.Designation),
        c.Category == BusinessContactCategory.Insurance && c.Insurer is not null ? ("Versicherer", c.Insurer) : ("Firma", c.Company),
        ("Ansprechperson", c.ContactPerson),
        ("Telefon", c.Phone),
        ("Mobil", c.Mobile),
        ("E-Mail", c.Email),
        ("Schadenshotline", c.ClaimsHotline),
        ("Polizzennummer", c.PolicyNumber),
        ("Zuständig für", c.Responsibility),
        ("Deckung", c.Coverage),
        ("Kunden-/Vertragsnummer", c.CustomerNumber),
        ("Adresse", c.Address),
    ];

    /// <summary>Alle Angaben eines Eintrags zum Kopieren, eine Angabe je Zeile.</summary>
    private static string Copy(BusinessContact c) =>
        string.Join('\n', Fields(c).Where(f => f.Value is not null).Select(f => $"{f.Label}: {f.Value}"));

    private static string OneLine(BusinessContact c) =>
        string.Join(", ", new[] { c.Designation, c.Company, c.ContactPerson, c.Phone ?? c.Mobile }.Where(v => !string.IsNullOrWhiteSpace(v)));
}

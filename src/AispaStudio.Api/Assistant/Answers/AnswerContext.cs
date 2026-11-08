using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Was ein Antwortgeber über die Frage weiß: wer fragt (mit Rechten), an welchem Standort, in dessen Zeitzone und wann „jetzt“ ist.
/// Die Zeit steht hier statt in den Antwortgebern, damit Tests mit festem Zeitpunkt arbeiten können.
/// <paramref name="ChosenGuestId"/> ist der Gast, den die Person aus einer Auswahl gewählt hat; <paramref name="ReferredGuestId"/> der Gast, den „sie“, „er“ oder
/// „der Gast“ meint (offener Termin, offene Kundenkarte oder zuletzt genannter Gast, schon aufgelöst und nach Standortrechten geprüft).
/// <paramref name="ReferredAppointmentId"/> ist der offene Termin, wenn er an einem Standort liegt, den die Person sieht.
/// </summary>
internal sealed record AnswerContext(UserAccess Access, Location Location, TimeZoneInfo Zone, DateTimeOffset Now, DateOnly Today, Guid? ChosenGuestId = null,
    Guid? ReferredGuestId = null, Guid? ReferredAppointmentId = null)
{
    public static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public bool Has(AccessRight right) => Access.Has(right);

    /// <summary>Therapeut:innen (ohne Buchen und ohne Finanzen) sehen nur eigene Termine, wie im Tagesüberblick (Ä317).</summary>
    public bool OwnOnly => !Has(AccessRight.Book) && !Has(AccessRight.ViewFinance);

    /// <summary>Die eigene Person für <see cref="OwnOnly"/>; ohne Person im Team trifft der Filter keinen Termin.</summary>
    public Guid OwnEmployeeId => Access.EmployeeId ?? Guid.Empty;

    /// <summary>Der Standort in Worten für die Quelle, z. B. „Standort Wien Innere Stadt“.</summary>
    public string Place => $"Standort {Location.Name}";

    /// <summary>Der Standort im zweiten Fall, „des Standorts Wien Innere Stadt“ (nach „des“ eingesetzt).</summary>
    public string PlaceOf => $"Standorts {Location.Name}";

    /// <summary>Uhrzeit in Ortszeit des Standorts, „14:30“.</summary>
    public string Time(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).ToString("HH:mm", German);

    /// <summary>Betrag in der Währung des Standorts, „1.234,50 EUR“ (Schweiz mit Schweizer Schreibweise).</summary>
    public string Money(decimal amount) =>
        amount.ToString("#,##0.00", Location.Currency == "CHF" ? CultureInfo.GetCultureInfo("de-CH") : German) + " " + Location.Currency;

    /// <summary>Verweis auf das Feld einer Maske der Verwaltung (zentrale Tabelle <see cref="FieldMasks"/>); ohne das Recht, es zu ändern, gibt es keinen Verweis.</summary>
    public AssistantLink? Fill(string fieldId, string? label = null, Guid? targetId = null) => FieldMasks.Link(this, fieldId, label, targetId);

    /// <summary>Verweis auf eine ganze Seite der Verwaltung ohne bestimmtes Feld (Schließzeiten); ohne das Recht gibt es keinen Verweis.</summary>
    public AssistantLink? Setting(AccessRight needed, string label, string page, string? tab) =>
        Has(needed) ? new AssistantLink(AssistantLinkKind.Setting, label, page, tab, Location.Id) : null;

    /// <summary>„Im Bericht öffnen“ mit Standort und Zeitraum; nur für Personen, die den Bericht (Management) sehen.</summary>
    public AssistantLink? Report(DateOnly from, DateOnly until) =>
        Has(AccessRight.ViewFinance) ? new AssistantLink(AssistantLinkKind.Report, "Im Bericht öffnen", "reports", "management", Location.Id, from, until) : null;
}

/// <summary>
/// Beantwortet Fragen zu einem oder mehreren Themen aus den Daten des Spas (ADR 0072). Ein Antwortgeber nutzt nur Dienste, die es schon gibt, prüft die Rechte
/// selbst, erfindet nichts und nennt immer die Quelle. Die KI ruft ihn nicht auf und ändert seine Zahlen nicht.
/// </summary>
internal interface IAnswerGiver
{
    IReadOnlyCollection<BusinessTopic> Topics { get; }

    /// <summary>
    /// Die Angaben des Spas (Feldkennungen aus <see cref="FieldIds"/>), die diese Frage braucht. Ist eine leer, obwohl die Antwort möglich war, hängt <see cref="BusinessAnswers"/>
    /// den Abschnitt „Unvollständig“ an. Themen, die nichts aus den Einstellungen brauchen, haben keine.
    /// </summary>
    IReadOnlyCollection<string> Fields(BusinessQuestion question) => [];

    Task<AssistantBlock> AnswerAsync(BusinessQuestion question, AnswerContext context, CancellationToken ct);
}

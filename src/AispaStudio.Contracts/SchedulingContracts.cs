namespace AispaStudio.Contracts;

/// <summary>Art einer planbaren Ressource. Ein Termin belegt immer eine Person und einen Raum, optional ein Gerät.</summary>
public enum ResourceKind
{
    Employee,
    Room,
    Equipment,
}

/// <summary>Ablauf eines Termins. Nur <see cref="Cancelled"/> gibt die belegten Ressourcen wieder frei.</summary>
public enum AppointmentStatus
{
    Booked,
    CheckedIn,
    Completed,
    NoShow,
    Cancelled,
}

/// <summary>Öffnungszeit eines Standorts an einem Wochentag (Ortszeit des Standorts). Mehrere Einträge pro Tag sind möglich.</summary>
public sealed record OpeningHours(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

/// <summary>
/// Anschrift und Kontakt eines Standorts; erscheinen im Kundenbereich und in Mails an Gäste. <c>Directions</c> (Anreise und Parkplätze, höchstens 500 Zeichen)
/// ist ein Freitext, aus dem der Assistent auf „Wie kommt der Gast zu uns?“ antwortet.
/// </summary>
public sealed record LocationContact(string? Street, string? PostalCode, string? City, string? Phone, string? Email, string? Directions = null)
{
    public static readonly LocationContact Empty = new(null, null, null, null, null);
}

/// <summary>
/// Angaben zum Unternehmen hinter dem Standort für Belege und Rechnungen: Firmenwortlaut, UID-Nummer, Firmenbuch- bzw.
/// Handelsregisternummer und Gericht (z. B. „FN 123456a“, „Handelsgericht Wien“). Alles optional.
/// </summary>
public sealed record LocationCompany(string? LegalName, string? VatId, string? CompanyRegisterNumber, string? CompanyRegisterCourt)
{
    public static readonly LocationCompany Empty = new(null, null, null, null);
}

/// <summary>
/// Neuer Standort. <c>TimeZone</c> ist eine IANA-Zeitzone, z. B. „Europe/Vienna“; <c>Currency</c> z. B. „EUR“ oder „CHF“.
/// <c>Contact</c> weglassen lässt Anschrift und Kontakt unverändert, <c>Company</c> ebenso die Unternehmensangaben.
/// Regeln der Online-Buchung: <c>/online-rules</c>.
/// </summary>
/// <param name="Region">Bundesland oder Kanton nach ISO 3166-2 (z. B. „AT-9“, „DE-BY“, „CH-ZH“); bestimmt die Feiertage.</param>
public sealed record LocationInput(
    string Name,
    string TimeZone,
    string Currency,
    IReadOnlyList<OpeningHours> OpeningHours,
    string? Region = null,
    LocationContact? Contact = null,
    LocationCompany? Company = null);

/// <summary><c>Contact</c> und <c>Company</c> sind immer gesetzt; ohne gepflegte Angaben sind die Felder leer.</summary>
public sealed record LocationDto(
    Guid Id,
    string Name,
    string TimeZone,
    string Currency,
    IReadOnlyList<OpeningHours> OpeningHours,
    string? Region = null,
    LocationContact? Contact = null,
    LocationCompany? Company = null);

/// <summary>
/// Neuer Raum. <c>CleaningMinutes</c>: Raumpflege nach jedem Termin, in der der Raum gesperrt bleibt.
/// <c>Places</c>: Gäste derselben Buchung, die hier gleichzeitig behandelt werden können (2 für eine Paarsuite); weglassen lässt den Wert
/// unverändert, neu angelegt 1. <c>IsArea</c>: Gemeinschaftsbereich (Sauna, Ruheraum), <c>Places</c> ist dann die Kapazität für Eintritte;
/// weglassen lässt den Wert unverändert.
/// </summary>
/// <summary><c>AdmissionArticleId</c>: nur für Bereiche; der Kassa-Artikel, der je Person für einen Eintritt abgerechnet wird.</summary>
public sealed record RoomInput(string Name, int CleaningMinutes, int? Places = null, bool? IsArea = null, Guid? AdmissionArticleId = null);

public sealed record RoomDto(Guid Id, Guid LocationId, string Name, int CleaningMinutes, int Places = 1, bool IsArea = false, Guid? AdmissionArticleId = null);

public sealed record EquipmentInput(string Name);

public sealed record EquipmentDto(Guid Id, Guid LocationId, string Name);

public sealed record QualificationInput(string Name);

public sealed record QualificationDto(Guid Id, string Name);

/// <summary>Regelmäßige Arbeitszeit einer Person an einem Wochentag und Standort (Ortszeit des Standorts).</summary>
public sealed record WorkingHours(Guid LocationId, DayOfWeek Day, TimeOnly Start, TimeOnly End);

/// <summary>Neue Person im Team. <c>Color</c> ist die Farbe im Kalender, z. B. „#2f6f5e“.</summary>
/// <c>Languages</c>: Sprachen außer Deutsch, in denen die Person behandeln kann (Kürzel aus <see cref="GuestLanguages"/>).
/// <c>PreferredName</c>: Name im Haus, höchstens 40 Zeichen (siehe <see cref="EmployeeNames"/>).
public sealed record EmployeeInput(
    string DisplayName,
    string Color,
    IReadOnlyList<Guid> QualificationIds,
    IReadOnlyList<WorkingHours> WorkingHours,
    IReadOnlyList<string>? Languages = null,
    TherapistGender? Gender = null,
    string? PreferredName = null);

public sealed record EmployeeDto(
    Guid Id,
    string DisplayName,
    string Color,
    IReadOnlyList<Guid> QualificationIds,
    IReadOnlyList<WorkingHours> WorkingHours,
    IReadOnlyList<string>? Languages = null,
    TherapistGender? Gender = null,
    string? PreferredName = null);

/// <summary>Für den Wunsch „Therapeutin“ oder „Therapeut“: Angabe an der Person und Wunsch des Gastes.</summary>
public enum TherapistGender
{
    Female,
    Male,
}

/// <summary>
/// Sprachen für Gäste und Mitarbeitende als Kürzel nach ISO 639-1 mit deutschem Namen. Deutsch ist die Grundsprache und wird
/// nicht gespeichert: ein Gast ohne Sprache spricht Deutsch, eine Person ohne Sprachen spricht nur Deutsch.
/// </summary>
public static class GuestLanguages
{
    public static IReadOnlyList<(string Code, string Name)> All { get; } =
    [
        ("en", "Englisch"), ("it", "Italienisch"), ("fr", "Französisch"), ("es", "Spanisch"), ("nl", "Niederländisch"),
        ("cs", "Tschechisch"), ("sk", "Slowakisch"), ("hu", "Ungarisch"), ("sl", "Slowenisch"), ("hr", "Kroatisch"),
        ("pl", "Polnisch"), ("ro", "Rumänisch"), ("ru", "Russisch"), ("uk", "Ukrainisch"), ("tr", "Türkisch"),
        ("ar", "Arabisch"), ("zh", "Chinesisch"), ("ja", "Japanisch"),
    ];

    public static string? Name(string? code) => All.FirstOrDefault(l => l.Code == code).Name;

    /// <summary>Kleingeschriebenes Kürzel; „de“, leer und Unbekanntes ergeben <c>null</c>.</summary>
    public static string? Normalize(string? code) =>
        code?.Trim().ToLowerInvariant() is { } c && All.Any(l => l.Code == c) ? c : null;
}

/// <summary>
/// Neue Behandlung. Vor- und Nachbereitung sperren Person und Raum zusätzlich zur Behandlungsdauer.
/// <c>RoomIds</c>: geeignete Räume (einer davon wird belegt). <c>EquipmentIds</c>: geeignete Geräte; leer heißt, es wird kein Gerät gebraucht.
/// <c>Price</c> ist der Grundpreis; <c>LocationPrices</c> weicht an einzelnen Standorten davon ab (weglassen lässt sie unverändert).
/// </summary>
public sealed record TreatmentInput(
    string Name,
    int DurationMinutes,
    int PreparationMinutes,
    int FollowUpMinutes,
    decimal Price,
    IReadOnlyList<Guid> RequiredQualificationIds,
    IReadOnlyList<Guid> RoomIds,
    IReadOnlyList<Guid> EquipmentIds,
    string? Category = null,
    IReadOnlyList<TreatmentLocationPriceDto>? LocationPrices = null,
    int? NoShowFeePercent = null,
    int? DepositPercent = null,
    string? GuestHint = null,
    bool CheckAnamnesis = false,
    int HandsOnMinutes = 0,
    int ProcessingMinutes = 0);

/// <summary>Preis einer Behandlung an einem Standort, abweichend vom Grundpreis.</summary>
public sealed record TreatmentLocationPriceDto(Guid LocationId, decimal Price);

/// <summary>
/// Preis nach Wochentag und Tageszeit, z. B. „Abendpreis“ Mo–Fr 17–21 Uhr oder „Wochenende“ Sa–So. Maßgeblich ist der Beginn des Termins
/// in Ortszeit, <c>From</c> eingeschlossen, <c>Until</c> ausgeschlossen. <c>LocationId</c> leer: alle Standorte. Die erste passende
/// Regel gilt, Regeln eines Standorts vor denen für alle; ohne passende Regel gilt der Preis des Standorts.
/// Der Preis wird bei der Buchung festgehalten; Verschieben ändert ihn nicht.
/// </summary>
public sealed record TreatmentPriceRuleDto(string Name, IReadOnlyList<DayOfWeek> Days, TimeOnly From, TimeOnly Until, decimal Price, Guid? LocationId = null);

/// <summary>
/// <c>Price</c>: mit Standort abgefragt (z. B. in <see cref="LocationResources"/>) der dort geltende Preis, sonst der Grundpreis.
/// <c>BasePrice</c> ist immer der Grundpreis, <c>LocationPrices</c> die Abweichungen.
/// </summary>

public sealed record TreatmentDto(
    Guid Id,
    string Name,
    int DurationMinutes,
    int PreparationMinutes,
    int FollowUpMinutes,
    decimal Price,
    IReadOnlyList<Guid> RequiredQualificationIds,
    IReadOnlyList<Guid> RoomIds,
    IReadOnlyList<Guid> EquipmentIds,
    string? Category,
    decimal? BasePrice = null,
    IReadOnlyList<TreatmentLocationPriceDto>? LocationPrices = null,
    int? NoShowFeePercent = null,
    int? DepositPercent = null,
    string? GuestHint = null,
    bool CheckAnamnesis = false,
    int HandsOnMinutes = 0,
    int ProcessingMinutes = 0);

/// <summary>Alles, was ein Standort zum Planen braucht: Räume, Geräte, Personen mit Arbeitszeit dort und angebotene Behandlungen.</summary>
public sealed record LocationResources(
    LocationDto Location,
    IReadOnlyList<RoomDto> Rooms,
    IReadOnlyList<EquipmentDto> Equipment,
    IReadOnlyList<EmployeeDto> Employees,
    IReadOnlyList<TreatmentDto> Treatments,
    IReadOnlyList<QualificationDto> Qualifications);

/// <summary>
/// Kundin oder Kunde. Der Kundenstamm gilt für alle Standorte gemeinsam. Pflicht ist nur ein Name (Vor- oder Nachname),
/// alles andere lässt sich später ergänzen.
/// </summary>
/// <param name="ClientId">
/// Vom Arbeitsplatz vergebene Id, z. B. für eine ohne Verbindung angelegte Kundin, auf die vorgemerkte Termine schon verweisen.
/// Gibt es die Kundin schon, liefert der Server sie zurück statt sie doppelt anzulegen.
/// </param>
/// <param name="Language">Sprache des Gastes als Kürzel aus <see cref="GuestLanguages"/>; leer heißt Deutsch.</param>
/// <param name="PreferredName">Rufname im Haus (ersetzt im Kalender den Vornamen); Vor- und Nachname bleiben der gültige Name (ADR 0064).</param>
/// <param name="TitlesBefore">Vorangestellte Titel, mehrere mit Leerzeichen („Mag. Dr.“).</param>
/// <param name="TitlesAfter">Nachgestellte Titel, mehrere mit Komma („MSc, MBA“).</param>
/// <param name="TitleInGreeting">Titel in der Anrede: leer nach dem Land des Standorts, sonst ja oder nein.</param>
/// <param name="Pronouns">Pronomen, wenn der Gast sie nennt; freiwillig, nur für das Team sichtbar.</param>
public sealed record CustomerInput(string FirstName, string LastName, string? Email, string? Phone, DateOnly? BirthDate, string? Note,
    Guid? ClientId = null, string? Language = null, CustomerSalutation Salutation = CustomerSalutation.None, string? PreferredName = null,
    string? TitlesBefore = null, string? TitlesAfter = null, bool? TitleInGreeting = null, CustomerAddress? Address = null, string? Pronouns = null);

/// <param name="DeletedAt">Gesetzt, wenn der Gast anonymisiert wurde: Name, Kontakt und Notizen sind gelöscht, Termine und Belege bleiben als „Gelöschter Gast“.</param>
public sealed record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly? BirthDate,
    string? Note,
    CustomerBilling? Billing = null,
    CustomerDiscount? Discount = null,
    CustomerBookingPreferences? Booking = null,
    string? Language = null,
    string? CreatedFromChannel = null,
    bool AssignmentOpen = false,
    CustomerSalutation Salutation = CustomerSalutation.None,
    string? PreferredName = null,
    string? TitlesBefore = null,
    string? TitlesAfter = null,
    bool? TitleInGreeting = null,
    CustomerAddress? Address = null,
    string? Pronouns = null,
    DateTimeOffset? DeletedAt = null)
{
    /// <summary>Name im Haus (Rufname statt Vorname): Kalender, Suche, Kundenkarte, Kassa-Oberfläche.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string InHouseName => PersonNames.InHouse(FirstName, LastName, PreferredName);

    /// <summary>Gültiger Name für Rechnung, Belege und Nachrichten an den Gast.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string LegalName => PersonNames.Legal(FirstName, LastName);

    /// <summary>Rufname oder, ohne Rufname, der Vorname; für Listen „Huber, Steffi“.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string InHouseFirstName => string.IsNullOrWhiteSpace(PreferredName) ? FirstName : PreferredName.Trim();
}

/// <summary>
/// Wann und bei wem eine Kundin gebucht werden möchte: Therapeut:in (bevorzugt oder fest), Wochentage und Uhrzeiten. Vorlieben
/// zur Behandlung selbst (Druck, Duft, Musik) stehen in <see cref="CustomerPreferences"/>.
/// </summary>
public sealed record CustomerBookingPreferences(
    Guid? EmployeeId = null,
    bool EmployeeFixed = false,
    IReadOnlyList<DayOfWeek>? Weekdays = null,
    TimeOnly? From = null,
    TimeOnly? Until = null,
    TherapistGender? Gender = null);

/// <summary>Aus den erledigten Besuchen: bei wem und wann die Kundin meist kommt. Text ist leer, wenn es kein Muster gibt.</summary>
public sealed record CustomerPreferenceSuggestion(
    int Visits,
    Guid? EmployeeId,
    string? EmployeeName,
    int WithEmployee,
    DayOfWeek? Weekday,
    int OnWeekday,
    TimeOnly? From,
    TimeOnly? Until,
    string? Text);

/// <summary>
/// Kennzahlen der Kundenkarte, aus Kalender und Kassenbelegen (nie von der KI gerechnet). <c>Revenue</c> und der Umsatz je Person nur mit
/// dem Recht „Zahlen sehen“, <c>ReceptionNote</c> nur mit dem Recht zu buchen (<c>MaySeeReceptionNote</c>). <c>Stars</c> 0 bis 3, Grund in
/// <c>StarsText</c>. Späte Absage: innerhalb von 24 Stunden vor Beginn storniert. <c>Birthday</c>: der nächste Geburtstag (Ä45), ohne Geburtsdatum leer.
/// <c>Vouchers</c> (offene Wertgutscheine) und <c>Purchases</c> (letzte Käufe ohne Termin) sind Auskunft der Kassa und kommen wie im Assistenten nur mit dem
/// Recht zu buchen, sonst leer (null).
/// </summary>
public sealed record CustomerInsights(
    int Visits,
    int VisitsLastYear,
    DateOnly? LastVisit,
    int? IntervalDays,
    int Stars,
    string StarsText,
    IReadOnlyList<DateOnly> NoShows,
    IReadOnlyList<DateOnly> LateCancellations,
    IReadOnlyList<CustomerEmployeeVisits> Employees,
    IReadOnlyList<CustomerRevenue>? Revenue,
    IReadOnlyList<GuestPackage> Packages,
    bool MaySeeReceptionNote,
    string? ReceptionNote,
    IReadOnlyList<CustomerNameChange>? NameChanges = null,
    GuestBirthday? Birthday = null,
    IReadOnlyList<GuestVoucher>? Vouchers = null,
    IReadOnlyList<GuestPurchase>? Purchases = null);

/// <summary>
/// Kundenüberblick im Termin-Panel (Runde 5, Ä34): wenige Kennzahlen, die Kundennotiz, die letzten Behandlungen und der Kontakt, in einem Aufruf.
/// <c>Visits</c> sind erledigte Besuche, <c>NoShows</c> nicht erschienene Termine, <c>Next</c> der nächste gebuchte oder eingecheckte Termin,
/// <c>Recent</c> die letzten erledigten Behandlungen (höchstens fünf, neueste zuerst). <c>Note</c> ist die Kundennotiz („Weitere Angaben“), nicht der
/// Rezeptionsvermerk. <c>AnamnesisOnFile</c> nennt nur den Stand, nie Inhalte. <c>Open</c> (offener Betrag der Rechnungen und Pakete) fehlt ohne das Recht
/// „Berichte mit Beträgen“ und wenn es nichts Offenes gibt.
/// </summary>
public sealed record CustomerSummary(
    int Visits,
    DateOnly? LastVisit,
    int NoShows,
    CustomerNextAppointment? Next,
    IReadOnlyList<CustomerRecentVisit> Recent,
    string? Note,
    string? Phone,
    string? Email,
    bool AnamnesisOnFile,
    CustomerOpenItems? Open);

public sealed record CustomerNextAppointment(Guid AppointmentId, DateTimeOffset Start, string TreatmentName);

public sealed record CustomerRecentVisit(Guid AppointmentId, DateTimeOffset Start, string TreatmentName, string EmployeeName);

/// <summary>Offenes beim Gast: Rest nicht bezahlter Rechnungen (<c>OpenAmount</c>, in <c>Currency</c>) und Pakete mit Rest. Gutscheine mit Wert gehören keinem Gast.</summary>
public sealed record CustomerOpenItems(decimal OpenAmount, string Currency, IReadOnlyList<GuestPackage> Packages);

/// <summary>Änderung am gültigen Namen (Vor- oder Nachname), neueste zuerst; ausgestellte Belege behalten den alten Namen.</summary>
public sealed record CustomerNameChange(DateTimeOffset At, string? ChangedBy, string OldName, string NewName);

/// <summary>Erledigte Besuche bei einer Person; <c>Revenue</c> nur mit dem Recht „Zahlen sehen“.</summary>
public sealed record CustomerEmployeeVisits(Guid EmployeeId, string Name, int Visits, decimal? Revenue, string? Currency);

/// <summary>Umsatz aus den Kassenbelegen (Verkäufe minus Stornos) je Währung, gesamt und in den letzten zwölf Monaten.</summary>
public sealed record CustomerRevenue(string Currency, decimal Total, decimal LastYear);

/// <summary>
/// Kennzeichen eines Gastes mit Wirkung im Kalender. Neue Arten kommen ans Ende.
/// <c>MoreTime</c>: 10 Minuten Puffer nach jeder Behandlung. <c>InformManagement</c>: Hinweis an die Leitung beim Termin.
/// <c>NotWithMe</c>: Diese Person bekommt den Gast nicht mehr vorgeschlagen. <c>DoNotBook</c>: Online-Buchung und KI buchen nicht, die Rezeption entscheidet.
/// </summary>
public enum CustomerFlagKind
{
    MoreTime,
    InformManagement,
    NotWithMe,
    DoNotBook,
}

/// <summary>
/// Gesetztes Kennzeichen. <c>Reason</c> sieht bei „Nicht bei mir“ nur die Leitung und die Person selbst. <c>MayRemove</c>: ob die
/// angemeldete Person es aufheben darf.
/// </summary>
public sealed record CustomerFlagDto(
    Guid Id,
    CustomerFlagKind Kind,
    Guid? EmployeeId,
    string? EmployeeName,
    string? Reason,
    DateOnly Since,
    bool MayRemove);

/// <summary>Neues Kennzeichen; <c>EmployeeId</c> nur bei „Nicht bei mir“ (leer: die angemeldete Person), <c>Reason</c> Pflicht bei „Nicht mehr buchen“.</summary>
public sealed record CustomerFlagInput(CustomerFlagKind Kind, Guid? EmployeeId = null, string? Reason = null);

/// <summary>Rezeptionsvermerk: Organisatorisches, nur für Rezeption und Leitung. Leer löscht ihn.</summary>
public sealed record CustomerReceptionNoteInput(string? Text);

/// <summary>Gespeicherter Rechnungsempfänger einer Kundin, z. B. ihre Firma. Die Kassa belegt damit die Rechnung vor.</summary>
public sealed record CustomerBilling(string Name, string Address, string? VatId = null);

/// <summary>Wofür der Kundenrabatt gilt. Gutscheine werden immer zum vollen Wert verkauft.</summary>
public enum CustomerDiscountScope
{
    /// <summary>Behandlungen, Eintritte, Produkte und freie Positionen.</summary>
    All,

    /// <summary>Nur Behandlungen (Termine).</summary>
    Treatments,

    /// <summary>Nur Produkte (Artikel aus dem Sortiment).</summary>
    Products,
}

/// <summary>
/// Dauerhafter Rabatt einer Kundin in Prozent, z. B. für Stammkundschaft oder Mitarbeitende. Die Kassa zieht ihn bei jeder
/// neuen Position der Kundin ab und weist ihn auf dem Beleg aus; ein Prozentsatz von 0 nimmt ihn zurück.
/// </summary>
public sealed record CustomerDiscount(decimal Percent, CustomerDiscountScope Scope = CustomerDiscountScope.All);

/// <summary>
/// Antwort mit 409, wenn ein Gast noch nicht anonymisiert werden darf: <c>Open</c> nennt, was offen ist („1 künftiger Termin“, „2 offene Rechnungen“),
/// <c>Message</c> den Satz für die Oberfläche.
/// </summary>
public sealed record CustomerAnonymizeRejected(string Message, IReadOnlyList<string> Open);

/// <summary>Kundenrabatt festlegen; über der Rabattgrenze des Spas mit Freigabe per PIN durch Inhaber oder Standortleitung.</summary>
public sealed record CustomerDiscountInput(decimal Percent, CustomerDiscountScope Scope = CustomerDiscountScope.All, SupervisorApproval? Approval = null);

/// <summary>Sperrzeit einer Ressource ohne Termin, z. B. Urlaub, Schulung, Wartung eines Geräts.</summary>
public sealed record BlockInput(ResourceKind Kind, Guid ResourceId, DateTimeOffset From, DateTimeOffset Until, string Reason);

public sealed record BlockDto(Guid Id, ResourceKind Kind, Guid ResourceId, DateTimeOffset From, DateTimeOffset Until, string Reason);

/// <summary>Beginn und Ende einer Sperrzeit oder Pause ändern (Rand im Kalender gezogen); Person bzw. Raum und Grund bleiben.</summary>
public sealed record BlockChange(DateTimeOffset From, DateTimeOffset Until);

/// <summary>
/// Terminwunsch. Ohne Angabe von Person, Raum oder Gerät wählt der Server passende freie Ressourcen.
/// </summary>
/// <param name="ClientId">
/// Vom Arbeitsplatz vergebene Id, z. B. für offline vorgemerkte Buchungen. Gibt es den Termin schon, liefert der Server ihn
/// zurück statt doppelt zu buchen; so darf ein Arbeitsplatz eine Buchung nach einem Verbindungsabbruch gefahrlos wiederholen.
/// </param>
/// <param name="OutsideHoursConfirmed">
/// „Trotzdem buchen“ (Slice 85, ADR 0073, Nachtrag Runde R04): die Rezeption bucht ausdrücklich außerhalb der Öffnungszeit oder des Dienstes. Der Server beachtet es
/// nur bei Buchungen der Rezeption; Online-Buchung und KI-Terminannahme bekommen solche Zeiten nie.
/// </param>
/// <param name="RulesConfirmed">„Trotzdem buchen“ trotz einer Sonderregelung der Person (R05 Slice 111, E10); der Server beachtet es wie <paramref name="OutsideHoursConfirmed"/>.</param>
public sealed record AppointmentRequest(
    Guid LocationId,
    Guid TreatmentId,
    Guid CustomerId,
    DateTimeOffset Start,
    Guid? EmployeeId,
    Guid? RoomId,
    Guid? EquipmentId,
    string? Note,
    Guid? ClientId = null,
    bool OutsideHoursConfirmed = false,
    bool RulesConfirmed = false);

/// <summary>
/// Gebuchter Termin. <c>Start</c>–<c>End</c> ist die Behandlung, <c>BlockedFrom</c>–<c>BlockedUntil</c> die Zeit, in der die Person
/// (mit Vor- und Nachbereitung) gebunden ist. <c>UpdatedAt</c> ordnet Live-Updates: ältere Stände werden verworfen.
/// <c>GroupId</c> verbindet gemeinsam gebuchte Termine (Paar, Spa-Tag). <c>LateMinutes</c>: der Gast hat gemeldet, dass er so viele
/// Minuten später kommt. <c>PlaceholderGuest</c>: Gast ohne Namen in einer Gruppe; <c>CustomerName</c> ist dann die buchende Person.
/// <c>ConfirmedAt</c>: der Gast hat über den Link zugesagt. <c>PlatformPayment</c>/<c>PrepaidAmount</c>: bei Treatwell-Buchungen, wie der Gast
/// bezahlt (vorab über die Plattform mit Betrag, oder im Spa). <c>AcceptInPlatformPending</c>: die Buchung ist in Treatwell noch anzunehmen.
/// <c>CustomerAssignmentOpen</c>: der Gast ist vorläufig angelegt, die Rezeption ordnet ihn noch einem Kunden zu.
/// <c>Warnings</c>: Hinweise nur in der Antwort auf das Buchen (Lagergrenze gerissen: Bestand reicht nicht, Mindestmenge, nur noch wenige Behandlungen) und auf das
/// Einchecken (Tagesstart des Raums nicht abgeschlossen, Raumpflege davor nicht abgehakt); sie blockieren nichts und werden nicht gespeichert.
/// <c>Version</c>: Stand des Termins (Ä281); ändernde Anfragen schicken ihn als <c>ExpectedVersion</c> zurück, damit ein veralteter Stand nichts überschreibt.
/// <c>HeldSince</c>: aus dem Posteingang vorgemerkt, bis die Rezeption bestätigt oder freigibt (ADR 0083); <c>HoldRemindAt</c> sagt, ab wann der Kalender
/// „Gast hat sich nicht gemeldet“ zeigt, <c>HoldAnsweredAt</c>, wann der Gast geantwortet hat, <c>HoldMailId</c> die Anfrage dazu.
/// </summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid LocationId,
    Guid TreatmentId,
    string TreatmentName,
    Guid CustomerId,
    string CustomerName,
    Guid EmployeeId,
    Guid RoomId,
    Guid? EquipmentId,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset BlockedFrom,
    DateTimeOffset BlockedUntil,
    AppointmentStatus Status,
    decimal Price,
    string? Note,
    DateTimeOffset UpdatedAt,
    Guid? GroupId = null,
    Guid? SeriesId = null,
    AppointmentSource Source = AppointmentSource.Reception,
    int? LateMinutes = null,
    int? PlaceholderGuest = null,
    DateTimeOffset? ConfirmedAt = null,
    PlatformPayment PlatformPayment = PlatformPayment.None,
    decimal? PrepaidAmount = null,
    bool AcceptInPlatformPending = false,
    bool CustomerAssignmentOpen = false,
    IReadOnlyList<string>? Warnings = null,
    int PreparationMinutes = 0,
    int FollowUpMinutes = 0,
    int ExtraFollowUpMinutes = 0,
    int HandsOnMinutes = 0,
    int ProcessingMinutes = 0,
    DateTimeOffset? HeldSince = null,
    DateTimeOffset? HoldRemindAt = null,
    DateTimeOffset? HoldAnsweredAt = null,
    Guid? HoldMailId = null,
    DateTimeOffset? StartedAt = null,
    uint Version = 0);

/// <summary>Wer den Termin eingetragen hat: das Team selbst, der Gast online oder die KI-Terminannahme über einen Kanal.</summary>
public enum AppointmentSource
{
    Reception,
    Online,
    Email,
    Sms,
    WhatsApp,
    Phone,
    Treatwell,
}

/// <summary>Termin verschieben. Ohne Angabe bleiben Person, Raum und Gerät gleich, sofern sie zur neuen Zeit frei sind.</summary>
/// <summary>
/// Vorschlag, einen gebuchten Termin um wenige Minuten zu verschieben, damit bei der Person keine unbrauchbare Lücke bleibt
/// (kürzer als die kürzeste Behandlung des Standorts). Gleiche Person; Raum und Gerät bleiben, wenn sie frei sind.
/// </summary>
public sealed record GapSuggestion(
    Guid AppointmentId,
    Guid EmployeeId,
    string EmployeeName,
    string CustomerName,
    string TreatmentName,
    DateTimeOffset CurrentStart,
    DateTimeOffset SuggestedStart,
    int GapMinutes,
    string Reason);

/// <param name="ExpectedVersion">
/// <see cref="AppointmentDto.Version"/> des Stands, von dem aus geändert wird (Ä281). Weicht der Termin inzwischen ab, antwortet der Server mit 409 und
/// <see cref="BookingRejected.Current"/>; ohne Angabe ändert er wie bisher. Gilt ebenso für Zeiten, Notiz, Storno, Status und Zurücknehmen.
/// </param>
public sealed record MoveAppointmentRequest(DateTimeOffset Start, Guid? EmployeeId, Guid? RoomId, Guid? EquipmentId, uint? ExpectedVersion = null);

/// <summary>Der Gast kommt so viele Minuten später (1 bis 120); 0 nimmt die Meldung zurück.</summary>
public sealed record ReportLateRequest(int Minutes);

/// <summary>Vor- und Nachbereitung dieses einen Termins in Minuten (je 0 bis 120); der Puffer „Braucht mehr Zeit“ bleibt.</summary>
public sealed record AppointmentTimesRequest(int PreparationMinutes, int FollowUpMinutes, uint? ExpectedVersion = null);

/// <summary>Notiz zu diesem einen Termin (höchstens 500 Zeichen); leer löscht sie. Die Kundennotiz ist davon getrennt.</summary>
public sealed record AppointmentNoteRequest(string? Note, uint? ExpectedVersion = null);

public sealed record CancelAppointmentRequest(string? Reason, uint? ExpectedVersion = null);

/// <summary>
/// Stornierung zurücknehmen (Ä95): ohne <c>Start</c> an derselben Zeit mit derselben Person, demselben Raum und Gerät, sonst zum neuen Beginn mit freier Wahl wie beim Verschieben.
/// <c>SendCorrection</c>: der Gast hat die Absage schon bekommen und erhält eine Korrektur.
/// </summary>
public sealed record RestoreAppointmentRequest(DateTimeOffset? Start = null, bool SendCorrection = false, uint? ExpectedVersion = null);

/// <summary>Die Nachricht an den Gast zu einer Absage des Teams: keine, noch wartend oder schon gesendet.</summary>
public enum CancellationNoticeState
{
    None,
    Waiting,
    Sent,
}

/// <summary>
/// Was beim Zurücknehmen einer Stornierung zu beachten ist: Stand der Absage-Nachricht, eine retournierte Anzahlung, und ob eine neue Zeit nötig ist, weil der Termin
/// in der Vergangenheit liegt (<c>InPast</c>) oder Zeit, Person oder Raum inzwischen belegt sind (<c>Conflict</c>: die Belegungen, die das Zurücknehmen melden würde).
/// </summary>
public sealed record AppointmentRestoration(CancellationNoticeState Notice, bool DepositReturned, bool InPast = false, BookingRejected? Conflict = null);

/// <summary>Namen für einen Gast ohne Namen nachtragen; gilt für alle seine Termine in der Buchung.</summary>
public sealed record AssignGuestRequest(Guid CustomerId);

public sealed record AppointmentStatusChange(AppointmentStatus Status, uint? ExpectedVersion = null);

/// <summary>
/// Warum eine Ressource zur gewünschten Zeit nicht geht. <c>From</c> und <c>Until</c> nennen, wann sie belegt ist (UTC; die Oberfläche
/// zeigt Ortszeit), <c>AppointmentId</c> den Termin dahinter (zum Markieren), <c>IsBlock</c> eine Sperrzeit. Der Text <c>Reason</c> enthält keine Uhrzeiten.
/// <c>NextFree</c> (UTC): beim Konflikt „kein Raum frei“ der früheste Beginn, zu dem ein passender Raum frei wäre; sonst leer.
/// </summary>
public sealed record BookingConflict(ResourceKind Kind, Guid ResourceId, string ResourceName, string Reason,
    DateTimeOffset? From = null, DateTimeOffset? Until = null, Guid? AppointmentId = null, bool IsBlock = false, DateTimeOffset? NextFree = null);

/// <summary>
/// Antwort, wenn ein Termin nicht gebucht werden kann (HTTP 409). <c>OutsideHours</c>: die Rezeption könnte mit <see cref="AppointmentRequest.OutsideHoursConfirmed"/>
/// trotzdem buchen, weil nur die Öffnungszeit oder der Dienst im Weg steht (Slice 85).
/// </summary>
/// <param name="StaffRule">Die Änderung verstößt gegen eine Sonderregelung der Person (R05 Slice 111, E10): die Maske warnt, „Trotzdem“ schickt sie mit Bestätigung noch einmal.</param>
/// <param name="Current">Die Änderung kam von einem veralteten Stand (<c>ExpectedVersion</c> passt nicht, Ä281): der aktuelle Termin, den die Oberfläche statt des alten zeigt.</param>
public sealed record BookingRejected(string Message, IReadOnlyList<BookingConflict> Conflicts, bool OutsideHours = false, bool StaffRule = false,
    AppointmentDto? Current = null);

/// <summary>Wie gut eine freie Zeit in den Tag der Person passt.</summary>
public enum SlotFit
{
    /// <summary>Weder angrenzend noch störend.</summary>
    Neutral,

    /// <summary>Schließt ohne Lücke an einen Termin, eine Sperrzeit oder an Dienstbeginn bzw. -ende an und lässt keine zu kurze Lücke.</summary>
    Snug,

    /// <summary>Lässt davor oder danach eine Lücke, in die keine Behandlung des Standorts mehr passt.</summary>
    LeavesGap,
}

/// <summary>
/// Freie Zeit für eine Behandlung mit einer passenden Kombination aus Person, Raum und Gerät.
/// <c>Fit</c> und <c>FitReason</c> sagen, ob die Zeit eine unbrauchbare Lücke im Tag der Person hinterlässt, mit Begründung.
/// </summary>
public sealed record AvailableSlot(
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid EmployeeId,
    Guid RoomId,
    Guid? EquipmentId,
    SlotFit Fit = SlotFit.Neutral,
    string? FitReason = null);

/// <summary>Zeitspanne, z. B. Öffnungszeit oder Dienst einer Person an einem Tag.</summary>
public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

public sealed record EmployeeShift(Guid EmployeeId, DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Ein Kalendertag eines Standorts für die Rezeption: Öffnungszeit, Dienste, Termine und Sperrzeiten. <c>RoomCare</c>: Raumpflege-Aufgaben des Tages mit Zustand
/// (offen, erledigt, überfällig) und Zustand je Raum (Tagesstart-Kontrolle, Abnahmen) für die Anzeige im Kalender. <c>Closures</c>: Schließzeiten, die den Tag
/// schneiden (ADR 0073), als Band über allen Spalten. <c>ClosuresDue</c>: laufende und noch kommende Schließzeiten mit offenen Terminen oder Wartelisten-Einträgen,
/// gleich wie fern sie beginnt und welcher Tag gezeigt wird; die Meldung „Termine in der Schließzeit“ stützt sich darauf.
/// <c>RoomClosures</c>: Raumsperren, die den Tag schneiden (ADR 0076), schraffiert in der Spalte des Raums. <c>RoomClosuresDue</c>: laufende und kommende Raumsperren
/// mit offenen Terminen im Raum, gleich wie fern; die Meldung „Termine im gesperrten Raum“ stützt sich darauf.
/// </summary>
public sealed record CalendarDay(
    Guid LocationId,
    DateOnly Date,
    string TimeZone,
    IReadOnlyList<TimeRange> Opening,
    IReadOnlyList<EmployeeShift> Shifts,
    IReadOnlyList<AppointmentDto> Appointments,
    IReadOnlyList<BlockDto> Blocks,
    IReadOnlyList<CalendarGuest>? Guests = null,
    IReadOnlyList<CalendarAbsence>? Absences = null,
    IReadOnlyList<CalendarBreak>? Breaks = null,
    IReadOnlyList<CalendarNoteDto>? Notes = null,
    IReadOnlyList<CalendarPreparation>? Preparations = null,
    IReadOnlyList<SeriesInfo>? Series = null,
    IReadOnlyList<AreaVisitDto>? GroupAreas = null,
    RoomCareDay? RoomCare = null,
    IReadOnlyList<CalendarElsewhere>? Elsewhere = null,
    IReadOnlyList<ClosureBand>? Closures = null,
    IReadOnlyList<ClosureBand>? ClosuresDue = null,
    IReadOnlyList<CalendarDayFact>? Facts = null,
    IReadOnlyList<RoomClosureBand>? RoomClosures = null,
    IReadOnlyList<RoomClosureBand>? RoomClosuresDue = null,
    IReadOnlyList<Guid>? BilledAppointmentIds = null);

/// <summary>
/// Was der Kalender über einen Gast des Tages zeigt (Stammgast, Erstbesuch, Hinweis), gezählt über alle Standorte und nur vor diesem Tag.
/// Ein Besuch ist ein erledigter Termin; eine gemeinsame Buchung (Spa-Tag) zählt einmal. Gesundheitsdaten stehen hier nie.
/// </summary>
/// <remarks>
/// <c>Likes</c>: Vorlieben zur Behandlung in einer Zeile („Druck kräftig · Lavendel · wenig Gespräch“) für die Hover-Karte.
/// <c>Packages</c>: gekaufte Pakete mit offenem Rest (z. B. 10er-Block Massage).
/// <c>Flags</c>: gesetzte Kennzeichen außer „Nicht bei mir“ (das wirkt nur im Planer).
/// <c>Language</c>: Sprache des Gastes, wenn nicht Deutsch.
/// <c>Salutation</c>: Anrede für ♀/♂ neben dem Namen (der Name im Termin ist schon der Rufname).
/// <c>Birthday</c>: Geburtstag am Tag oder höchstens drei Tage davor oder danach (nie das Geburtsjahr), sonst leer.
/// </remarks>
public sealed record CalendarGuest(Guid CustomerId, int Visits, int VisitsLastYear, int NoShows, DateOnly? LastVisit, string? Note,
    CustomerBookingPreferences? Booking = null, string? Likes = null, IReadOnlyList<GuestPackage>? Packages = null,
    IReadOnlyList<CustomerFlagKind>? Flags = null, string? Language = null, CustomerSalutation Salutation = CustomerSalutation.None, GuestBirthday? Birthday = null);

/// <summary>
/// Geburtstag eines Gastes, vom Server gerechnet (Runde 5b, Ä45). <c>Date</c> ist der Tag des Geburtstags in dem Jahr, das zum Bezugstag gehört (im Kalender der Tag
/// im Fenster von drei Tagen davor bis danach, auf der Kundenkarte der nächste), <c>DaysAway</c> der Abstand zum Bezugstag (0 = heute, negativ = vorbei). Wer am 29. Februar
/// geboren ist, feiert in Jahren ohne Schalttag am 28. Februar. <c>Age</c> ist das Alter an <c>Date</c>, <c>AgeNow</c> das Alter am Bezugstag; beide fehlen, wenn das
/// Geburtsjahr nicht bekannt ist (Jahr 1).
/// </summary>
public sealed record GuestBirthday(DateOnly Date, int DaysAway, int? Age, int? AgeNow);

/// <summary>Gekauftes Paket eines Gastes: <c>Units</c> Behandlungen, davon <c>UnitsLeft</c> noch offen.</summary>
public sealed record GuestPackage(Guid TreatmentId, string TreatmentName, int Units, int UnitsLeft, DateOnly? ValidUntil);

/// <summary>
/// Offener Wertgutschein eines Gastes (Restwert, kein Paket). <c>CodeEnd</c> sind die letzten vier Zeichen des Codes zum Wiedererkennen; der ganze Code
/// bleibt in der Kassa. <c>TreatmentName</c> nur beim Behandlungsgutschein.
/// </summary>
public sealed record GuestVoucher(VoucherKind Kind, string CodeEnd, string? TreatmentName, decimal Balance, string Currency, DateOnly? ValidUntil);

/// <summary>Gekaufte Position ohne Termin (Artikel, Gutschein, Paket) auf einem nicht stornierten Beleg; <c>Date</c> am Kalendertag des Standorts.</summary>
public sealed record GuestPurchase(DateOnly Date, string Description, int Quantity, decimal Amount, string Currency);

/// <summary>
/// Wer an diesem Tag laut Dienstplan nicht da ist (Urlaub, Krankenstand, Schulung, frei), mit dem letzten Tag in Folge derselben Art
/// (höchstens 60 Tage voraus) und der Notiz aus dem Dienstplan; beim Krankenstand ohne Notiz, weil sie einen Befund nennen könnte.
/// </summary>
public sealed record CalendarAbsence(Guid EmployeeId, RosterKind Kind, DateOnly Until, string? Note);

/// <summary>
/// Person aus dem Team des Standorts, die an diesem Tag dort keinen Dienst hat, aber an einem anderen Standort arbeitet („Graz 10:00–18:00“).
/// Für „Alle Mitarbeitenden“ im Kalender: die Spalte zeigt, wo die Person ist. Die Zeiten sind in UTC.
/// </summary>
public sealed record CalendarElsewhere(Guid EmployeeId, string LocationName, DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Pause einer Person mit Dienst: wie viele Minuten das Arbeitszeitrecht des Landes verlangt, wie viele eingetragen sind (Sperrzeiten
/// „Pause“ und Lücken zwischen zwei Diensten) und eine Warnung, wenn zwischen den Terminen keine Zeit mehr für die fehlende Pause bleibt.
/// </summary>
public sealed record CalendarBreak(Guid EmployeeId, int RequiredMinutes, int PlannedMinutes, string? Warning);

/// <summary>Erster Vorbereitungsschritt einer Behandlung („Steine auf 55 °C aufheizen“), als Text im Vorbereitungsstreifen.</summary>
public sealed record CalendarPreparation(Guid TreatmentId, string Text);

/// <summary>
/// Notiz im Kalender. Ohne Ressource und Zeit eine Tagesnotiz für den Standort (Lieferung, Wartung), sonst ein Zettel in der Spalte
/// einer Person oder eines Raums zu einer Uhrzeit. Blockiert nichts und lässt sich abhaken.
/// </summary>
public sealed record CalendarNoteDto(Guid Id, DateOnly Date, string Text, ResourceKind? Kind, Guid? ResourceId, DateTimeOffset? At, bool Done,
    DateTimeOffset UpdatedAt, DateTimeOffset? Until = null);

/// <summary>
/// Neue Notiz. Spalte (Art und Id) und Uhrzeit nur gemeinsam; ohne beide gilt sie für den ganzen Tag am Standort.
/// <c>Until</c>: optionales Ende des Zettels, nur mit Uhrzeit und nach ihr.
/// </summary>
public sealed record CalendarNoteInput(DateOnly Date, string Text, ResourceKind? Kind = null, Guid? ResourceId = null, DateTimeOffset? At = null,
    DateTimeOffset? Until = null);

/// <summary>
/// Notiz ändern oder abhaken; leere Felder bleiben, wie sie sind. <c>At</c> verschiebt die Uhrzeit eines Zettels (nicht einer Tagesnotiz),
/// <c>Until</c> setzt das Ende (nach der Uhrzeit der Notiz).
/// </summary>
public sealed record CalendarNoteChange(string? Text = null, bool? Done = null, DateTimeOffset? Until = null, DateTimeOffset? At = null);

/// <summary>Bundesland oder Kanton, für das die Software die Feiertage kennt.</summary>
public sealed record HolidayRegionDto(string Code, FiscalCountry Country, string Name);

public sealed record PublicHolidayDto(DateOnly Date, string Name);

/// <summary>Was der Ausfall-Assistent für einen Termin vorschlägt.</summary>
public enum AbsenceOption
{
    /// <summary>Gleiche Zeit, andere Person (Raum möglichst gleich).</summary>
    SameTime,

    /// <summary>Andere Zeit am selben Tag, so nah wie möglich an der ursprünglichen.</summary>
    OtherTime,

    /// <summary>Kein Ersatz an diesem Tag; der Gast muss kontaktiert werden.</summary>
    None,

    /// <summary>Hier kein Ersatz, aber an einem anderen Standort desselben Spas; nur nach Rücksprache mit dem Gast übernehmen.</summary>
    OtherLocation,
}

/// <summary>Ausfall einer Person an einem Tag: für ihre Termine an diesem Standort Ersatz vorschlagen.</summary>
public sealed record AbsenceProposalRequest(Guid EmployeeId, DateOnly Date);

/// <summary>Vorschlag für einen Termin, mit Begründung in einem Satz.</summary>
public sealed record AbsenceProposalItem(
    AppointmentDto Appointment,
    AbsenceOption Option,
    DateTimeOffset? Start,
    Guid? EmployeeId,
    string? EmployeeName,
    Guid? RoomId,
    string? RoomName,
    string Explanation,
    Guid? LocationId = null,
    string? LocationName = null);

public sealed record AbsenceProposal(Guid EmployeeId, DateOnly Date, IReadOnlyList<AbsenceProposalItem> Items);

/// <summary>Ein übernommener Vorschlag (die Rezeption kann ihn vorher anpassen oder weglassen).</summary>
public sealed record AbsenceMove(Guid AppointmentId, DateTimeOffset Start, Guid EmployeeId, Guid RoomId, Guid? LocationId = null);

public sealed record AbsenceApplyRequest(IReadOnlyList<AbsenceMove> Moves, bool NotifyCustomers);

public sealed record AbsenceMoveFailure(Guid AppointmentId, string Message);

public sealed record AbsenceApplyResult(IReadOnlyList<AppointmentDto> Moved, IReadOnlyList<AbsenceMoveFailure> Failed, int Notified);

/// <summary>
/// Eine Behandlung in einer gemeinsamen Buchung. Behandlungen mit gleichem <c>Step</c> beginnen gleichzeitig (Paar), der nächste Schritt
/// beginnt, wenn alle Behandlungen des vorigen zu Ende sind (Spa-Tag). Ohne Person oder Raum wählt der Server.
/// </summary>
/// <param name="PlaceholderGuest">
/// Gast ohne Namen (Gruppe, Firmen-Event): Nummer des Gastes in der Buchung. <c>CustomerId</c> ist dann die buchende Person; der Name
/// wird später nachgetragen.
/// </param>
public sealed record GroupBookingItem(Guid TreatmentId, Guid CustomerId, int Step, Guid? EmployeeId = null, Guid? RoomId = null,
    int? PlaceholderGuest = null);

/// <summary>
/// Zeit in einem Bereich (Sauna, Ruheraum) als Teil des Spa-Tags: der Gast belegt im Schritt <c>Step</c> einen Platz für <c>Minutes</c>.
/// </summary>
public sealed record GroupAreaItem(Guid RoomId, Guid CustomerId, int Step, int Minutes);

/// <summary>
/// Paar- oder Spa-Tag-Buchung: alle Behandlungen werden gemeinsam gebucht oder keine. <c>GapMinutes</c>: Pause zwischen den Schritten.
/// <c>ClientId</c>: vom Arbeitsplatz vergebene Id der Buchung (ohne Verbindung vorgemerkt); dieselbe Id noch einmal liefert die schon
/// gebuchten Termine. <c>Areas</c>: Zeiten in Sauna oder Ruheraum als eigene Schritte. <c>FlexibleOrder</c>: der Planer darf die Schritte
/// umstellen und zwischen ihnen kurz warten lassen; er nimmt die Reihenfolge, mit der der Tag am frühesten endet (Baukasten).
/// </summary>
public sealed record GroupBookingRequest(
    Guid LocationId,
    DateTimeOffset Start,
    IReadOnlyList<GroupBookingItem> Items,
    int GapMinutes = 0,
    string? Note = null,
    Guid? ClientId = null,
    IReadOnlyList<GroupAreaItem>? Areas = null,
    bool FlexibleOrder = false);

/// <summary>Freie Startzeiten für eine gemeinsame Buchung an einem Tag suchen.</summary>
public sealed record GroupAvailabilityRequest(DateOnly Date, IReadOnlyList<GroupBookingItem> Items, int GapMinutes = 0,
    IReadOnlyList<GroupAreaItem>? Areas = null, bool FlexibleOrder = false);

/// <summary>Geplante Zeit in einem Bereich, in derselben Reihenfolge wie <c>Areas</c> der Anfrage.</summary>
public sealed record GroupSlotArea(DateTimeOffset From, DateTimeOffset Until, Guid RoomId, Guid CustomerId);

/// <summary>Geplante Behandlung einer gemeinsamen Buchung, in derselben Reihenfolge wie die Anfrage.</summary>
public sealed record GroupSlotItem(DateTimeOffset Start, DateTimeOffset End, Guid EmployeeId, Guid RoomId, Guid? EquipmentId);

/// <summary>
/// Startzeit, zu der alle Behandlungen passen. <c>SharedRoom</c>: gleichzeitige Gäste sind gemeinsam in einem Mehrplatzraum (Paarsuite).
/// </summary>
public sealed record GroupSlot(DateTimeOffset Start, DateTimeOffset End, bool SharedRoom, IReadOnlyList<GroupSlotItem> Items,
    IReadOnlyList<GroupSlotArea>? Areas = null);

/// <summary>Ganze gemeinsame Buchung auf einen neuen Beginn verschieben; Ablauf und Pause bleiben.</summary>
public sealed record MoveGroupRequest(DateTimeOffset Start);

public sealed record GroupBookingResult(Guid GroupId, IReadOnlyList<AppointmentDto> Appointments, IReadOnlyList<AreaVisitDto>? AreaVisits = null);

/// <summary>
/// Eintritt in einen Bereich (Sauna, Ruheraum) buchen. <c>Persons</c>: so viele Plätze werden belegt (z. B. Gast mit Begleitung).
/// <c>ClientId</c>: vom Arbeitsplatz vergebene Id (ohne Verbindung vorgemerkt); dieselbe Id noch einmal liefert den schon gebuchten Eintritt.
/// </summary>
public sealed record AreaVisitRequest(
    Guid LocationId,
    Guid RoomId,
    Guid CustomerId,
    DateTimeOffset From,
    DateTimeOffset Until,
    int Persons = 1,
    string? Note = null,
    Guid? ClientId = null);

public sealed record AreaVisitDto(
    Guid Id,
    Guid LocationId,
    Guid RoomId,
    Guid CustomerId,
    string CustomerName,
    int Persons,
    DateTimeOffset From,
    DateTimeOffset Until,
    bool IsCancelled,
    string? Note,
    Guid? GroupId = null,
    IReadOnlyList<string>? Warnings = null);

/// <summary>Hinweise an die Rezeption, die nichts sperren; der Server schickt sie in <c>Warnings</c>, die Windows-App zeigt denselben Text vorab in der Maske.</summary>
public static class BookingNotices
{
    /// <summary>Die Rezeption trägt an einem Schließtag oder einem Tag ohne Dienst ein (ADR 0073, Nachtrag Runde R03).</summary>
    public const string ClosedDay = "Der Standort hat an diesem Tag geschlossen.";

    /// <summary>Die Rezeption hat mit „Trotzdem buchen“ außerhalb der Öffnungszeit oder des Dienstes gebucht (Slice 85, ADR 0073, Nachtrag Runde R04).</summary>
    public const string OutsideHours = "Außerhalb der Öffnungszeit oder des Dienstes gebucht.";
}

/// <summary>Belegte Plätze eines Bereichs ab <c>Start</c> bis zum nächsten Eintrag.</summary>
public sealed record AreaLoad(DateTimeOffset Start, int Occupied);

/// <summary>Ein Bereich an einem Tag: Kapazität, Eintritte und Verlauf der Belegung (nur Änderungspunkte).</summary>
public sealed record AreaDay(Guid RoomId, string Name, int Places, IReadOnlyList<AreaVisitDto> Visits, IReadOnlyList<AreaLoad> Load);

/// <summary>Wie eine Serie weitergeht: im festen Abstand am selben Wochentag oder jeweils nach dem letzten Besuch.</summary>
public enum SeriesMode
{
    /// <summary>Alle N Wochen am selben Wochentag zur selben Zeit; alle Termine werden beim Anlegen gebucht.</summary>
    Weekly,

    /// <summary>N Wochen nach dem letzten erledigten Besuch; gebucht ist immer nur der nächste Termin.</summary>
    AfterLastVisit,
}

/// <summary>
/// Regel für wiederkehrende Termine. <c>IntervalWeeks</c> 1 bis 8. Ende über <c>Count</c> (weitere Termine, höchstens 52)
/// oder <c>Until</c> (letzter möglicher Tag, höchstens ein Jahr voraus); eines von beiden ist Pflicht.
/// </summary>
public sealed record SeriesInput(int IntervalWeeks, SeriesMode Mode = SeriesMode.Weekly, int? Count = null, DateOnly? Until = null);

/// <summary>Wie ein Termin der Serie zustande käme.</summary>
public enum SeriesOccurrenceState
{
    /// <summary>Gleiche Zeit, gleiche Person.</summary>
    Free,

    /// <summary>Gleiche Person, andere Uhrzeit am selben Tag.</summary>
    OtherTime,

    /// <summary>Gleiche Zeit, andere Person (nicht bei fester Therapeut:in).</summary>
    OtherEmployee,

    /// <summary>Andere Zeit und andere Person.</summary>
    OtherTimeAndEmployee,

    /// <summary>An diesem Tag ist nichts frei oder die Behandlung gibt es nicht mehr.</summary>
    Conflict,
}

/// <summary>
/// Ein Termin der Vorschau. <c>Start</c> und <c>EmployeeId</c> sind der Vorschlag (bei <c>Conflict</c> die gewünschte Zeit ohne Person);
/// <c>Problem</c> sagt, warum die gewünschte Zeit nicht geht. Ein Feiertag ändert nichts (ADR 0075, Nachtrag Ä50).
/// </summary>
public sealed record SeriesOccurrence(DateOnly Date, DateTimeOffset Start, Guid? EmployeeId, string? EmployeeName,
    SeriesOccurrenceState State, string? Problem);

/// <summary>Ein Termin, den die Rezeption aus der Vorschau übernimmt.</summary>
public sealed record SeriesPick(DateTimeOffset Start, Guid? EmployeeId);

/// <summary>Serie anlegen: Regel und die übernommenen Termine aus der Vorschau (ausgelassene fehlen einfach).</summary>
public sealed record SeriesCreate(SeriesInput Rule, IReadOnlyList<SeriesPick> Picks);

/// <summary>Ein Termin der Serie, der nicht ging, mit Grund.</summary>
public sealed record SeriesFailure(DateTimeOffset Start, string Reason);

/// <summary>
/// Serie im Kalender: Regel, alle nicht stornierten Termine (für „3 von 6“) und, bei „nach dem letzten Besuch“, warum der nächste
/// Termin nicht gebucht werden konnte. <c>Ended</c>: über „diesen und alle folgenden stornieren“ beendet.
/// </summary>
public sealed record SeriesInfo(Guid Id, int IntervalWeeks, SeriesMode Mode, int? Count, DateOnly? Until,
    IReadOnlyList<DateTimeOffset> Starts, string? Problem, bool Ended);

/// <summary>Ergebnis beim Anlegen oder Stornieren mehrerer Termine einer Serie.</summary>
public sealed record SeriesResult(SeriesInfo Series, IReadOnlyList<AppointmentDto> Changed, IReadOnlyList<SeriesFailure> Failed);

using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

// Fachmodell für Planung und Kalender. Alle Zeitpunkte in UTC; Öffnungs- und Arbeitszeiten in Ortszeit des Standorts.
// Medizinische Behandlungen (Version 2) passen über Qualifikationen und Ressourcen hinein, ohne das Modell zu ändern.

public sealed class Location
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    /// <summary>IANA-Zeitzone, z. B. „Europe/Vienna“.</summary>
    public required string TimeZone { get; set; }

    public required string Currency { get; set; }

    /// <summary>Bundesland oder Kanton nach ISO 3166-2, z. B. „DE-BY“; bestimmt die regionalen Feiertage.</summary>
    public string? Region { get; set; }

    public List<LocationOpeningHours> OpeningHours { get; init; } = [];

    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Anreise und Parkplätze in Worten, z. B. „U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß“; der Assistent antwortet daraus (ADR 0072).</summary>
    public string? Directions { get; set; }

    /// <summary>Firmenwortlaut des Unternehmens hinter dem Standort (bei mehreren Gesellschaften je Standort verschieden).</summary>
    public string? LegalName { get; set; }

    /// <summary>UID-Nummer, z. B. „ATU12345678“ oder „DE123456789“, „CHE-123.456.789 MWST“.</summary>
    public string? VatId { get; set; }

    /// <summary>Firmenbuch- bzw. Handelsregisternummer, z. B. „FN 123456a“ oder „HRB 12345“.</summary>
    public string? CompanyRegisterNumber { get; set; }

    public string? CompanyRegisterCourt { get; set; }

    /// <summary>Minuten, die eine Nachricht an den Gast nach einer Absage des Teams wartet (0 bis 60), damit ein Versehen noch zurückgenommen werden kann (Ä95).</summary>
    public int GuestMessageDelayMinutes { get; set; } = 10;
}

public sealed class LocationOpeningHours
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public DayOfWeek Day { get; init; }
    public TimeOnly Opens { get; init; }
    public TimeOnly Closes { get; init; }
}

public sealed class Room
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public required string Name { get; set; }

    /// <summary>Raumpflege nach jedem Termin; so lange bleibt der Raum zusätzlich gesperrt.</summary>
    public int CleaningMinutes { get; set; }

    /// <summary>Plätze für gleichzeitig behandelte Gäste derselben Buchung, z. B. 2 für eine Paarsuite; im Bereich die Kapazität.</summary>
    public int Places { get; set; } = 1;

    /// <summary>Gemeinschaftsbereich (Sauna, Ruheraum, Pool): Gäste teilen sich die Plätze, gebucht wird ein Eintritt statt einer Behandlung.</summary>
    public bool IsArea { get; set; }

    /// <summary>Bereich: Kassa-Artikel, der je Person für einen Eintritt abgerechnet wird (Preis, Steuersatz und Erlöskonto kommen vom Artikel).</summary>
    public Guid? AdmissionArticleId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class Equipment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Qualification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
}

public sealed class Employee
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string DisplayName { get; set; }

    /// <summary>Rufname („Name im Haus“, ADR 0064, Nachtrag): ersetzt im Haus den vollständigen Namen, nie in Lohn, Export, Zugängen und Einladungen.</summary>
    public string? PreferredName { get; set; }

    public required string Color { get; set; }

    /// <summary>Verknüpftes Anmeldekonto, sobald die Person eingeladen ist.</summary>
    public string? UserId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Sprachen außer Deutsch, in denen die Person behandelt (Kürzel).</summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>Für Gäste, die eine Therapeutin oder einen Therapeuten wünschen; ohne Angabe passt die Person zu jedem Wunsch.</summary>
    public TherapistGender? Gender { get; set; }

    public List<Qualification> Qualifications { get; init; } = [];
    public List<EmployeeWorkingHours> WorkingHours { get; init; } = [];

    /// <summary>Name im Haus (Rufname, sonst vollständiger Name); nicht für Lohn, Export, Zugänge und Einladungen.</summary>
    public string InHouseName => EmployeeNames.InHouse(DisplayName, PreferredName);
}

/// <summary>Regelmäßige Wochenarbeitszeit. Ein datumsgenauer Dienstplan ersetzt das später für einzelne Tage.</summary>
public sealed class EmployeeWorkingHours
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public Guid LocationId { get; init; }
    public DayOfWeek Day { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
}

public sealed class Treatment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public int DurationMinutes { get; set; }
    public int PreparationMinutes { get; set; }
    public int FollowUpMinutes { get; set; }

    /// <summary>Minuten am Gast bis zur Einwirkzeit (Packung, Maske); 0 ohne Einwirkzeit (Schritt 13).</summary>
    public int HandsOnMinutes { get; set; }

    /// <summary>Einwirkzeit: der Gast bleibt im Raum, die Person ist frei; 0 ohne Einwirkzeit.</summary>
    public int ProcessingMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Gruppe im Angebot, z. B. „Massagen“ oder „Gesicht“; frei wählbar, gleiche Schreibweise fasst zusammen.</summary>
    public string? Category { get; set; }

    /// <summary>Ausfallgebühr in Prozent des Preises nur für diese Behandlung; leer heißt Regel des Standorts, 0 heißt keine.</summary>
    public int? NoShowFeePercent { get; set; }

    /// <summary>Anzahlung beim Online-Buchen in Prozent des Preises (100 = Vorauszahlung); leer heißt Regel des Standorts, 0 heißt keine.</summary>
    public int? DepositPercent { get; set; }

    /// <summary>Hinweis an Gäste vor dieser Behandlung, z. B. „Bitte 15 Minuten früher kommen, Badekleidung mitbringen“; steht in Bestätigung und Erinnerung.</summary>
    public string? GuestHint { get; set; }

    /// <summary>„Anamnese prüfen“: beim Buchen eines Gastes mit Angaben im Anamnesebogen erscheint ein Hinweis, ohne Inhalt.</summary>
    public bool CheckAnamnesis { get; set; }

    /// <summary>Abweichende Preise an einzelnen Standorten; sonst gilt <see cref="Price"/>.</summary>
    public List<TreatmentLocationPrice> LocationPrices { get; init; } = [];

    /// <summary>Preis an diesem Standort. <see cref="LocationPrices"/> muss geladen sein.</summary>
    public decimal PriceAt(Guid locationId) => LocationPrices.FirstOrDefault(p => p.LocationId == locationId)?.Price ?? Price;

    /// <summary>Preise nach Wochentag und Tageszeit (z. B. Abend- oder Wochenendpreis); die erste passende Regel gilt.</summary>
    public List<TreatmentPriceRule> PriceRules { get; init; } = [];

    /// <summary>
    /// Preis für einen Termin, der zu <paramref name="start"/> beginnt: passende Regel des Standorts, sonst passende Regel für alle Standorte,
    /// sonst der Preis des Standorts. <see cref="LocationPrices"/> und <see cref="PriceRules"/> müssen geladen sein.
    /// </summary>
    public decimal PriceAt(Guid locationId, DateTimeOffset start, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(start, zone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        return PriceRules
            .Where(r => (r.LocationId == null || r.LocationId == locationId) && r.Applies(local.DayOfWeek, time))
            .OrderBy(r => r.LocationId == null).ThenBy(r => r.Position)
            .FirstOrDefault()?.Price ?? PriceAt(locationId);
    }

    /// <summary>Die Person braucht alle diese Qualifikationen.</summary>
    public List<Qualification> RequiredQualifications { get; init; } = [];

    /// <summary>Geeignete Räume; einer davon wird belegt. Die Behandlung wird an Standorten mit einem dieser Räume angeboten.</summary>
    public List<Room> Rooms { get; init; } = [];

    /// <summary>Geeignete Geräte; leer heißt, es wird kein Gerät gebraucht.</summary>
    public List<Equipment> Equipment { get; init; } = [];
}

/// <summary>Preis einer Behandlung an einem Standort, z. B. höher in der Innenstadt.</summary>
/// <summary>Preis einer Behandlung an bestimmten Wochentagen zwischen zwei Uhrzeiten (Ortszeit, Beginn des Termins zählt).</summary>
public sealed class TreatmentPriceRule
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TreatmentId { get; init; }

    /// <summary>Leer: gilt an allen Standorten.</summary>
    public Guid? LocationId { get; init; }

    public int Position { get; init; }
    public required string Name { get; init; }

    /// <summary>Wochentage als Bitmaske, Bit <c>1 &lt;&lt; (int)DayOfWeek</c>.</summary>
    public int Days { get; init; }

    public TimeOnly From { get; init; }
    public TimeOnly Until { get; init; }
    public decimal Price { get; init; }

    public bool Applies(DayOfWeek day, TimeOnly time) => (Days & (1 << (int)day)) != 0 && time >= From && time < Until;
}

public sealed class TreatmentLocationPrice
{
    public Guid TreatmentId { get; init; }
    public Guid LocationId { get; init; }
    public decimal Price { get; set; }
}

/// <summary>Kundenstamm, gemeinsam für alle Standorte. Gesundheitsdaten (Anamnese) kommen getrennt und verschlüsselt dazu.</summary>
public sealed class Customer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Vor- und Nachname, der gültige Name (ADR 0064); einer der beiden genügt, der andere ist dann leer.</summary>
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Rufname im Haus: ersetzt in Kalender, Suche und Kassa-Oberfläche den Vornamen, nie auf Belegen und in Nachrichten an den Gast.</summary>
    public string? PreferredName { get; set; }

    public CustomerSalutation Salutation { get; set; }

    /// <summary>Pronomen, wenn der Gast sie nennt („sie/ihr“, „they/them“); freiwillig, nur für das Team, nie in Nachrichten nach außen.</summary>
    public string? Pronouns { get; set; }

    /// <summary>Vorangestellte Titel mit Leerzeichen („Mag. Dr.“), nachgestellte mit Komma („MSc, MBA“).</summary>
    public string? TitlesBefore { get; set; }

    public string? TitlesAfter { get; set; }

    /// <summary>Titel in der Anrede: null nach dem Land des Standorts (<see cref="PersonNames.GreetingTitles"/>), sonst ja oder nein.</summary>
    public bool? TitleInGreeting { get; set; }

    /// <summary>Postanschrift des Gastes; der Rechnungsempfänger steht getrennt in <see cref="BillingAddress"/>.</summary>
    public string? Street { get; set; }

    public string? PostalCode { get; set; }
    public string? City { get; set; }

    /// <summary>ISO-Kürzel des Landes, z. B. „AT“.</summary>
    public string? Country { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Note { get; set; }

    /// <summary>Rechnungsempfänger, falls Rechnungen auf eine Firma oder eine andere Anschrift gehen; die Kassa belegt damit die Rechnung vor.</summary>
    public string? BillingName { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingVatId { get; set; }

    /// <summary>Dauerhafter Rabatt in Prozent; die Kassa zieht ihn bei neuen Positionen ab (<see cref="DiscountScope"/>).</summary>
    public decimal? DiscountPercent { get; set; }

    public CustomerDiscountScope DiscountScope { get; set; }

    /// <summary>Wer einen Rabatt über der Grenze des Spas per PIN freigegeben hat.</summary>
    public string? DiscountApprovedByUserId { get; set; }

    /// <summary>Bevorzugte Therapeut:in; mit <see cref="PreferredEmployeeFixed"/> fest (Termin wandert nicht stillschweigend zu jemand anderem).</summary>
    public Guid? PreferredEmployeeId { get; set; }

    public bool PreferredEmployeeFixed { get; set; }

    /// <summary>Bevorzugte Wochentage als Bitmaske (Bit 0 = Sonntag, wie <see cref="DayOfWeek"/>).</summary>
    public int PreferredWeekdays { get; set; }

    public TimeOnly? PreferredFrom { get; set; }
    public TimeOnly? PreferredUntil { get; set; }

    /// <summary>Wunsch: Therapeutin oder Therapeut.</summary>
    public TherapistGender? PreferredGender { get; set; }

    /// <summary>Organisatorisches („zahlt mit Firmenkarte“), nur für Rezeption und Leitung; anders als <see cref="Note"/> nicht im Kalender.</summary>
    public string? ReceptionNote { get; set; }

    /// <summary>Sprache des Gastes (Kürzel), wenn nicht Deutsch.</summary>
    public string? Language { get; set; }

    /// <summary>Gesetzt, wenn die Terminannahme den Kunden aus einer Nachricht angelegt hat (Kanal); die Kundenkarte zeigt es, damit die Rezeption prüft.</summary>
    public BookingChannel? CreatedFromChannel { get; set; }

    /// <summary>
    /// Vorläufig angelegt (Treatwell-Buchung, die schon im Kalender steht): die Rezeption ordnet den Gast noch einem Bestandskunden zu
    /// oder bestätigt ihn als neu (ADR 0062). Gilt nur, bis die Zuordnung entschieden ist.
    /// </summary>
    public bool AssignmentOpen { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gesetzt, wenn der Gast anonymisiert wurde (weich gelöscht): Name, Kontakt, Notizen, Anamnese und Online-Zugang sind weg, Termine und Belege bleiben
    /// als „Gelöschter Gast“. Such- und Auswahlstellen schließen solche Gäste aus; Termine, Belege, Berichte und der Verlauf lesen sie weiter.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Name im Haus (Rufname statt Vorname); nicht für Belege und Nachrichten an den Gast.</summary>
    public string InHouseName => PersonNames.InHouse(FirstName, LastName, PreferredName);

    /// <summary>Gültiger Name: Rechnungen, Belege, Gutscheine auf Namen, Buchhaltung, DSGVO, Anamnese, Nachrichten an den Gast.</summary>
    public string LegalName => PersonNames.Legal(FirstName, LastName);

    /// <summary>„Guten Tag Frau Mag. Huber“ nach dem Land des Standorts.</summary>
    public string Greeting(FiscalCountry country) => PersonNames.Greeting(Salutation, TitlesBefore, FirstName, LastName, country, TitleInGreeting);
}

/// <summary>Protokoll einer Änderung am gültigen Namen (ADR 0064); ausgestellte Belege behalten den Namen von damals.</summary>
public sealed class CustomerNameChangeEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public required string OldName { get; init; }
    public required string NewName { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string? ChangedByUserId { get; init; }
}

public sealed class Appointment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Ändert sich nur, wenn der Ausfall-Assistent den Termin mit Zustimmung des Gastes an einen anderen Standort verlegt.</summary>
    public Guid LocationId { get; set; }
    public Guid TreatmentId { get; init; }
    public Treatment Treatment { get; init; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Gemeinsam gebuchte Termine (Paar, Spa-Tag) tragen dieselbe Gruppe.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Wiederkehrender Termin: gehört zu dieser Serie.</summary>
    public Guid? SeriesId { get; set; }

    /// <summary>Wer den Termin eingetragen hat; im Kalender steht die Quelle, wenn es nicht das Team war.</summary>
    public AppointmentSource Source { get; init; }

    /// <summary>Gemeldete Verspätung in Minuten; fällt weg, sobald der Termin verschoben wird.</summary>
    public int? LateMinutes { get; set; }

    /// <summary>Gast ohne Namen in einer Gruppe (Nummer in der Buchung); <see cref="CustomerId"/> ist bis zum Nachtragen die buchende Person.</summary>
    public int? PlaceholderGuest { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid? EquipmentId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public DateTimeOffset BlockedFrom { get; set; }
    public DateTimeOffset BlockedUntil { get; set; }

    /// <summary>Vorbereitung vor dem Termin; gilt für diesen Termin (Schritt 12), unabhängig von späteren Änderungen an der Behandlung.</summary>
    public int PreparationMinutes { get; set; }

    /// <summary>Nachbereitung nach dem Termin, ohne den Puffer.</summary>
    public int FollowUpMinutes { get; set; }

    /// <summary>Puffer aus dem Kennzeichen „Braucht mehr Zeit“ nach der Nachbereitung.</summary>
    public int ExtraFollowUpMinutes { get; set; }

    /// <summary>Minuten am Gast bis zur Einwirkzeit; Kopie der Behandlung beim Buchen (Schritt 13).</summary>
    public int HandsOnMinutes { get; set; }

    /// <summary>Einwirkzeit: Raum belegt, Person frei; Kopie der Behandlung beim Buchen.</summary>
    public int ProcessingMinutes { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    /// <summary>Preis zum Zeitpunkt der Buchung; spätere Preisänderungen ändern bestehende Termine nicht.</summary>
    public decimal Price { get; init; }

    public string? Note { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Der Betrieb hat den Termin abgesagt (Schließzeit, ADR 0073): „storniert durch Betrieb“. Dann gibt es nie eine Ausfallgebühr und keine einbehaltene Anzahlung.</summary>
    public bool CancelledByBusiness { get; set; }

    /// <summary>Treatwell-Buchung: vorab über die Plattform bezahlt, im Spa zu zahlen oder unbekannt/keine Plattformbuchung.</summary>
    public PlatformPayment PlatformPayment { get; set; }

    /// <summary>Betrag, den der Gast vorab über Treatwell bezahlt hat (höchstens der Preis); die Kassa verrechnet ihn als Zahlungsart „Über Treatwell bezahlt“.</summary>
    public decimal? PrepaidAmount { get; set; }

    /// <summary>Die Rezeption hat die Buchung in Treatwell noch nicht angenommen (erst dann gilt sie dort); „Erledigt“ setzt es zurück.</summary>
    public bool AcceptInPlatformPending { get; set; }

    /// <summary>Wann der Gast über den Link in Bestätigung oder Erinnerung zugesagt hat; Verschieben auf eine andere Zeit setzt es zurück.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Aus dem Posteingang vorgemerkt (ADR 0083): belegt wie gebucht, bis die Rezeption bestätigt oder freigibt.</summary>
    public DateTimeOffset? HeldSince { get; set; }

    /// <summary>Ab wann der Kalender „Gast hat sich nicht gemeldet“ zeigt (<see cref="HoldReminder"/>).</summary>
    public DateTimeOffset? HoldRemindAt { get; set; }

    /// <summary>Wann der Gast auf den Vorschlag geantwortet hat; dann gibt es keine Erinnerung mehr.</summary>
    public DateTimeOffset? HoldAnsweredAt { get; set; }

    /// <summary>Die Anfrage im Posteingang, aus der die Vormerkung stammt.</summary>
    public Guid? HoldMailId { get; set; }

    /// <summary>Wann die Person die Behandlung als begonnen gemeldet hat (Handy-App, Slice 106); eingecheckt und begonnen heißt: läuft.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Wie oft der Gast den Termin selbst online verschoben hat; begrenzt durch <c>OnlineBooking:MaxGuestMoves</c>.</summary>
    public int GuestMoves { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistische Sperre (PostgreSQL xmin): gleichzeitige Änderungen am selben Termin fallen auf.</summary>
    public uint Version { get; init; }

    public List<ResourceBooking> Bookings { get; init; } = [];
}

/// <summary>
/// Belegung einer Ressource in einem Zeitraum: durch einen Termin oder als Sperrzeit (Urlaub, Wartung).
/// Eine Ausschlussbedingung in PostgreSQL verhindert, dass sich zwei Belegungen derselben Ressource überschneiden,
/// auch wenn zwei Personen an der Rezeption gleichzeitig buchen.
/// </summary>
public sealed class ResourceBooking
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public ResourceKind Kind { get; init; }
    public Guid ResourceId { get; init; }

    /// <summary>Platz in einem Mehrplatzraum; die Ausschlussbedingung gilt je Ressource und Platz. Sonst immer 0.</summary>
    public int Slot { get; init; }

    public DateTimeOffset From { get; init; }
    public DateTimeOffset Until { get; init; }
    public Guid? AppointmentId { get; init; }

    /// <summary>Eintritt in einen Bereich (Sauna, Ruheraum); belegt je Person einen Platz.</summary>
    public Guid? AreaVisitId { get; init; }

    /// <summary>Grund einer Sperrzeit; bei Terminen leer.</summary>
    public string? Reason { get; init; }
}

/// <summary>Eintritt in einen Gemeinschaftsbereich (Sauna, Ruheraum) für eine oder mehrere Personen, ohne Behandlung und ohne Person im Team.</summary>
public sealed class AreaVisit
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public Guid RoomId { get; init; }
    public Guid CustomerId { get; init; }
    public Customer Customer { get; init; } = null!;
    public int Persons { get; init; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset Until { get; set; }
    public bool IsCancelled { get; set; }

    /// <summary>Teil eines Spa-Tags: dieselbe Gruppe wie die Termine der Buchung.</summary>
    public Guid? GroupId { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ResourceBooking> Bookings { get; init; } = [];
}

/// <summary>
/// Notiz im Kalender: ohne Ressource und Zeit für den ganzen Tag am Standort, sonst als Zettel in einer Spalte zu einer Uhrzeit.
/// Belegt nichts; die Planung kennt sie nicht.
/// </summary>
public sealed class CalendarNote
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public DateOnly Date { get; init; }
    public ResourceKind? Kind { get; init; }
    public Guid? ResourceId { get; init; }

    /// <summary>Uhrzeit des Zettels; lässt sich beim Bearbeiten verschieben (Tagesnotizen haben keine).</summary>
    public DateTimeOffset? At { get; set; }

    /// <summary>Ende des Zettels; ohne Ende steht er nur zu seiner Uhrzeit.</summary>
    public DateTimeOffset? Until { get; set; }

    public required string Text { get; set; }
    public bool Done { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Regel für wiederkehrende Termine eines Gastes. Die Termine selbst sind gewöhnliche Termine mit <see cref="Appointment.SeriesId"/>;
/// <c>StartTime</c> und <c>EmployeeId</c> sind Uhrzeit und Person für weitere Termine (bei „nach dem letzten Besuch“).
/// </summary>
public sealed class AppointmentSeries
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid TreatmentId { get; init; }
    public int IntervalWeeks { get; set; }
    public SeriesMode Mode { get; set; }
    public int? Count { get; set; }
    public DateOnly? Until { get; set; }
    public TimeOnly StartTime { get; set; }
    public Guid EmployeeId { get; set; }

    /// <summary>Warum der nächste Termin nicht gebucht werden konnte (nur „nach dem letzten Besuch“).</summary>
    public string? Problem { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

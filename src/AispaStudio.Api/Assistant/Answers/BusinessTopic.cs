using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Worüber eine Betriebsfrage geht (ADR 0072). Zu jedem Thema gibt es genau einen Antwortgeber (<see cref="IAnswerGiver"/>), der die Antwort
/// aus den Daten des Spas zusammensetzt; die KI erkennt höchstens das Thema.
/// </summary>
public enum BusinessTopic
{
    /// <summary>Öffnungszeiten an einem Tag (heute, morgen, Samstag, 1. November) oder die ganze Woche.</summary>
    OpeningHours,

    /// <summary>Telefon, Adresse und Mail des Standorts.</summary>
    Contact,

    /// <summary>Gesetzliche Feiertage nach Land und Region des Standorts.</summary>
    Holiday,

    /// <summary>Schulferien aus aktiven Kalender-Abos (ADR 0075): „Wann sind Ferien?“, „Sind am 28.10. Ferien?“.</summary>
    SchoolHolidays,

    /// <summary>Anreise und Parkplätze (Freitext am Standort).</summary>
    Directions,

    /// <summary>Stornoregel aus den Buchungseinstellungen.</summary>
    CancellationRule,

    /// <summary>Wer arbeitet an einem Tag.</summary>
    WhoWorks,

    /// <summary>Ist eine bestimmte Person an einem Tag oder in einer Woche im Dienst, im Urlaub oder krank.</summary>
    PersonAvailable,

    /// <summary>Wie viele Termine hat der Standort noch heute oder an einem Tag.</summary>
    RemainingAppointments,

    /// <summary>Wie voll ist ein Tag (Auslastung laut Dienstplan).</summary>
    DayLoad,

    /// <summary>Welche Räume sind jetzt frei.</summary>
    RoomFree,

    /// <summary>Wie voll ist ein Gemeinschaftsbereich (Sauna, Ruheraum).</summary>
    AreaLoad,

    /// <summary>Preis und Dauer einer Behandlung.</summary>
    TreatmentInfo,

    /// <summary>Wer bietet eine Behandlung an (Qualifikationen).</summary>
    WhoOffers,

    /// <summary>Wann war ein Gast zuletzt da.</summary>
    GuestLastVisit,

    /// <summary>Hat ein Gast in einem Zeitraum einen Termin.</summary>
    GuestNextVisit,

    /// <summary>Restwert eines Gutscheins.</summary>
    VoucherBalance,

    /// <summary>Laufende und kommende Aktionen und Rabatte.</summary>
    Promotions,

    /// <summary>Häufigste Behandlungen eines Zeitraums.</summary>
    TopTreatments,

    /// <summary>Beste Gäste eines Zeitraums, nach Besuchen oder (mit Recht) nach Ausgaben.</summary>
    TopGuests,

    /// <summary>Welche Person hat die meisten Termine.</summary>
    TopTherapist,

    /// <summary>Auslastung je Person und je Raum.</summary>
    Utilization,

    /// <summary>Umsatz je Behandlung (nur mit dem Recht „Berichte mit Beträgen“).</summary>
    RevenueByTreatment,

    /// <summary>Wie viele Termine waren „nicht erschienen“ oder storniert.</summary>
    NoShows,

    /// <summary>Welche Gäste mit Termin in einem Zeitraum haben Geburtstag (heute, diese Woche); nie eine Liste des ganzen Kundenstamms.</summary>
    Birthdays,

    /// <summary>Kontakte des Betriebs aus den Betriebsinformationen: Versicherung, Installateur, Buchhaltung, Lohnverrechnung, Notfall, „Wer ist für die Heizung zuständig?“.</summary>
    BusinessContacts,

    /// <summary>Verlauf eines Gastes: Besuche, nächster Termin, Nichterscheinen und Absagen, Käufe ohne Beträge („Historie Christian Steiner“).</summary>
    GuestHistory,

    /// <summary>Gutscheine, Pakete, offene Rechnungen, offene Anzahlungen und Kundenrabatt eines Gastes (nur mit dem Recht zu buchen).</summary>
    GuestBalance,

    /// <summary>Stand des Anamnesebogens eines Gastes; Hinweise daraus nur mit dem Recht, Gesundheitsdaten zu sehen.</summary>
    GuestAnamnesis,

    /// <summary>Wer kommt als Nächstes: die nächsten gebuchten Termine ab jetzt.</summary>
    NextArrivals,

    /// <summary>Wer ist noch nicht da: laufende Termine von heute ohne Einchecken.</summary>
    NotCheckedIn,

    /// <summary>Wer hat heute abgesagt: Termine, die heute storniert wurden (nicht vom Betrieb).</summary>
    CancelledToday,

    /// <summary>Fällt für einen bestimmten Termin eine Ausfallgebühr an, wenn der Gast jetzt absagt.</summary>
    CancellationFee,

    /// <summary>Wer steht auf der Warteliste.</summary>
    Waitlist,

    /// <summary>Wann hat eine Person Pause und Dienstende an einem Tag („Wann hat Sophie Pause?“).</summary>
    PersonBreak,

    /// <summary>Wer ist Ersthelfer oder für Kassa, Lager, Brandschutz, Hygiene zuständig; Ersthelfer im Dienst heute zuerst.</summary>
    DutyHolders,

    /// <summary>Freie Zeiten eines Raums an einem Tag und Raumsperren mit Grund („Wann ist der Paarraum morgen frei?“, „Ist der Hamam gesperrt?“).</summary>
    RoomFreeTimes,

    /// <summary>Bestand eines Materials je Lagerort mit Mindestbestand („Haben wir das Arganöl noch?“).</summary>
    StockLevel,

    /// <summary>Welche Materialien liegen unter dem Mindestbestand („Was ist bald aus?“).</summary>
    StockLow,

    /// <summary>Offene Bestellungen mit dem voraussichtlichen Liefertermin („Wann kommt die Lieferung?“).</summary>
    OpenOrders,
}

/// <summary>
/// Eine Betriebsfrage in Bausteinen: das Thema und was die Frage dazu nennt. <paramref name="Subject"/> ist ein Name (Person, Gast, Behandlung, Raum, Gutscheincode)
/// oder bei manchen Themen der Fragetext, in dem der Antwortgeber den Namen selbst sucht. <paramref name="From"/> und <paramref name="Until"/> sind Kalendertage
/// des Standorts (beide einschließlich). <paramref name="Detail"/> verfeinert das Thema (z. B. <c>week</c>, <c>phone</c>, <c>person</c>, <c>packages</c>).
/// <paramref name="Part"/> ist die Tageszeit, die die Frage nennt („heute nachmittag“); die Grenzen sind die der Warteliste.
/// </summary>
public sealed record BusinessQuestion(BusinessTopic Topic, string Text, string? Subject, DateOnly? From, DateOnly? Until, string? Detail = null, DayPart? Part = null);

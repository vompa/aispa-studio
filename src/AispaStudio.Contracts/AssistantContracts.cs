namespace AispaStudio.Contracts;

/// <summary>Für wen der KI-Assistent antwortet; bestimmt, was er auswertet und vorschlägt.</summary>
public enum AssistantAudience
{
    /// <summary>Rezeption: freie Zeiten, Kunden suchen, Tagesüberblick.</summary>
    Reception,

    /// <summary>Inhaber und Standortleitung: dazu Auswertungen.</summary>
    Management,

    /// <summary>Therapeut:innen: eigener Tagesüberblick und freie Zeiten, ohne Buchen.</summary>
    Therapist,
}

public enum AssistantSuggestionKind
{
    /// <summary>Freie Zeit buchen: öffnet die Buchung mit diesen Angaben; gebucht wird erst nach Bestätigung.</summary>
    BookSlot,

    OpenCustomer,

    OpenAppointment,

    /// <summary>Alle Termine eines Gastes: die Oberfläche zeigt sie über sieben Tage (Kunden-Filter im Kalender).</summary>
    ShowCustomerAppointments,

    /// <summary>„Freie Zeit suchen“ mit Behandlung, Person, Raum und Tag vorbelegt öffnen; gesucht wird erst nach dem Klick.</summary>
    SearchFreeTime,

    /// <summary>Auftrag „buche …“ (Ä319): „Neuer Termin“ mit Gast, Behandlung, Person, Tag (<c>Day</c>) und Beginn (<c>Start</c>) vorausgefüllt; gebucht wird erst per Knopf.</summary>
    NewAppointment,

    /// <summary>Auftrag „verschiebe …“: der Termin (<c>AppointmentId</c>, Beginn <c>Start</c>) mit „Verschieben“ auf <c>NewStart</c> vorausgefüllt.</summary>
    MoveAppointment,

    /// <summary>Auftrag „sag … ab“: die Rückfrage zum Stornieren des Termins, <c>Note</c> nennt die Gebühr; abgesagt wird erst nach dem Knopf.</summary>
    CancelAppointment,

    /// <summary>Auftrag „schick … den Bogen“: der nächste Termin des Gastes mit dem Abschnitt Anamnese (Tablet-Link, Papierbogen).</summary>
    SendQuestionnaire,

    /// <summary>Auftrag „biete … an“: der Wunsch auf der Warteliste (<c>TargetId</c>) mit „Freie Zeit suchen“.</summary>
    OfferWaitlist,

    /// <summary>„offene Rechnung …“: die Kassa mit der offenen Rechnung (<c>TargetId</c>) am Standort <c>LocationId</c>.</summary>
    OpenBill,
}

/// <summary>Frage an den Assistenten, optional für einen Standort (sonst der erste mit Zugang).</summary>
/// <param name="Page">Seite, von der aus gefragt wird (Seitenkennung der Hilfe, z. B. <c>kalender</c> oder <c>masterdata</c>).</param>
/// <param name="Surface">Oberfläche: <c>windows-app</c>, <c>verwaltung</c> oder <c>handy</c>; passende Abläufe der Hilfe kommen zuerst.</param>
/// <param name="Reference">Was gerade offen ist: „sie“, „er“ und „der Gast“ meinen diesen Gast, wenn die Frage keinen Namen nennt.</param>
/// <param name="GuestId">Gast, den die Person aus einer Auswahl gewählt hat (<see cref="AssistantFollowUp.GuestId"/>); gilt vor Namen und Bezug.</param>
public sealed record AssistantQuestion(string Question, Guid? LocationId, string? Page = null, string? Surface = null, AssistantReference? Reference = null,
    Guid? GuestId = null);

/// <summary>
/// Bezug einer Frage auf das, was offen ist (Ä315). Die API nimmt den Gast des offenen Termins, sonst die offene Kundenkarte, sonst den zuletzt genannten Gast
/// dieser Leiste; einen Termin nur an einem Standort, den der Zugang sieht.
/// </summary>
public sealed record AssistantReference(Guid? AppointmentId, Guid? CustomerId, Guid? LastGuestId);

/// <summary>
/// Verweis in die Hilfe: Thema und Abschnitt. Die Oberfläche zeigt Schritte und Bilder aus ihrer eingebauten Hilfe
/// (<c>HelpSectionView</c> in der Windows-App, <c>HelpSection</c> im Web); <c>Excerpt</c> ist der erste Satz daraus.
/// </summary>
public sealed record AssistantHelpLink(string Topic, string? Section, string Title, string Excerpt);

/// <summary>
/// Ein Vorschlag, den die Oberfläche als Knopf anbietet. Der Assistent führt nichts selbst aus; Aufträge (<see cref="AssistantSuggestionKind.NewAppointment"/> bis
/// <see cref="AssistantSuggestionKind.OpenBill"/>) öffnen eine Maske, gespeichert wird erst per Knopf der Person (ADR 0072, Nachtrag Slice 147).
/// <c>Day</c>: Tag ohne Uhrzeit, wenn der Auftrag keine nennt. <c>NewStart</c>: vorgeschlagener neuer Beginn beim Verschieben. <c>TargetId</c>: Wunsch der Warteliste
/// oder Rechnung. <c>Note</c>: Hinweis für die Maske (Gebühr einer Absage).
/// </summary>
public sealed record AssistantSuggestion(
    AssistantSuggestionKind Kind,
    string Label,
    Guid? LocationId,
    Guid? TreatmentId,
    Guid? EmployeeId,
    Guid? CustomerId,
    Guid? AppointmentId,
    DateTimeOffset? Start,
    Guid? RoomId = null,
    DateOnly? Day = null,
    DateTimeOffset? NewStart = null,
    Guid? TargetId = null,
    string? Note = null);

/// <summary>Kennzahl einer Auswertung mit dem Wert des Vergleichszeitraums.</summary>
public sealed record AssistantFigure(string Label, string Value, string? Previous);

/// <summary>Was ein Antwortgeber geliefert hat (ADR 0072).</summary>
public enum AssistantBlockKind
{
    /// <summary>Die Antwort steht im Text und gegebenenfalls in der Tabelle.</summary>
    Answer,

    /// <summary>Die Frage ist klar, aber das Feld ist im Spa nicht hinterlegt: der Text sagt das und der Link führt zur Einstellung.</summary>
    NotStored,

    /// <summary>Die Frage ist klar, aber es gibt dazu nichts (kein Gast, keine Behandlung, keine Person dieses Namens).</summary>
    NotFound,

    /// <summary>Die Person darf das nicht sehen. Das Angebot (<c>FollowUps</c>) nennt, wenn möglich, die Auswertung ohne Beträge.</summary>
    Denied,

    /// <summary>Keine Absicht erkannt und kein Handbuchtreffer: der Text bietet zwei Fragen an (<c>FollowUps</c>).</summary>
    NoIntent,
}

/// <summary>Tabelle einer Antwort, höchstens zehn Zeilen; jede Zeile hat so viele Zellen wie es Spalten gibt.</summary>
public sealed record AssistantTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public enum AssistantLinkKind
{
    /// <summary>Die Einstellung, in der das Fehlende eingetragen wird (Verwaltung, Seite und Reiter).</summary>
    Setting,

    /// <summary>„Im Bericht öffnen“: der Bericht mit vorbelegtem Standort und Zeitraum.</summary>
    Report,

    /// <summary>Zeile einer Personenliste: der Wochenplan im Bereich Team, in der Woche von <c>From</c> am Standort <c>LocationId</c>.</summary>
    Roster,
}

/// <summary>
/// Verweis aus einer Antwort in die Verwaltung. <c>Page</c> und <c>Tab</c> sind Seite und Reiter der Verwaltung (<c>masterData</c>/<c>locations</c>,
/// <c>reports</c>/<c>management</c>); die Windows-App öffnet dort die native Maske (Sprungziele im Masken-Inventar).
/// <c>Field</c> ist die Feldkennung (z. B. <c>location.directions</c>): die Maske scrollt zum Feld und setzt den Fokus darauf. <c>TargetId</c> ist bei Feldern
/// einer Behandlung deren Kennung.
/// </summary>
public sealed record AssistantLink(
    AssistantLinkKind Kind, string Label, string Page, string? Tab, Guid? LocationId = null, DateOnly? From = null, DateOnly? Until = null,
    Guid? TargetId = null, string? Field = null);

/// <summary>
/// Abschnitt „Unvollständig“ einer Antwort (ADR 0072, Nachtrag Runde 5b): <c>Text</c> nennt, was fehlt („Unvollständig: Anreise und Telefon fehlen beim Standort Wien.“),
/// <c>Where</c> die Maske („Stammdaten › Standorte › Anschrift und Anreise“), <c>Fields</c> die Feldkennungen. <c>Link</c> öffnet die Maske am Feld und gibt es nur,
/// wer das Recht dazu hat; sonst steht in <c>Hint</c>, wer es eintragen kann („Nur Leitung“).
/// </summary>
public sealed record AssistantIncomplete(string Text, string Where, IReadOnlyList<string> Fields, AssistantLink? Link, string? Hint);

/// <summary>
/// Eine Frage, die die Oberfläche als Knopf anbietet und mit einem Klick stellt („Auswertung ohne Beträge“). <c>GuestId</c>: Auswahl bei mehreren passenden Gästen;
/// die Oberfläche schickt die Frage mit diesem Gast (<see cref="AssistantQuestion.GuestId"/>).
/// </summary>
public sealed record AssistantFollowUp(string Label, string Question, Guid? GuestId = null);

/// <summary>
/// Antwort eines Antwortgebers (ADR 0072): die Fakten stehen fest (<c>Text</c>, <c>Table</c>), die KI darf höchstens den Satz umformulieren, nie eine
/// Zahl ändern. <c>Topic</c> ist die Kennung des Themas (z. B. <c>OpeningHours</c>). <c>Source</c> nennt, woher die Angabe stammt, z. B. „Öffnungszeiten des
/// Standorts Wien Innere Stadt“. <c>Copy</c> ist der Text für „Kopieren“ (Kontaktdaten). <c>Period</c> ist bei Auswertungen der Zeitraum in Worten.
/// <c>GuestId</c> ist der Gast, um den es in der Antwort ging; die Oberfläche merkt ihn sich für „sie“ und „der Gast“ in der nächsten Frage.
/// <c>Items</c>: bei Listen-Antworten (Tagesüberblick, freie Zeiten, Kundensuche, wer arbeitet) die Zeilen; <c>Text</c> ist dann nur der Kopfsatz mit der Zahl.
/// </summary>
public sealed record AssistantBlock(
    AssistantBlockKind Kind,
    string Topic,
    string Text,
    AssistantTable? Table,
    string Source,
    AssistantLink? Link,
    string? Copy,
    string? Period,
    IReadOnlyList<AssistantFollowUp> FollowUps,
    IReadOnlyList<AssistantIncomplete>? Incomplete = null,
    Guid? GuestId = null,
    IReadOnlyList<AssistantItem>? Items = null);

/// <summary>
/// Zeile einer Listen-Antwort (Ä316): links <c>Lead</c> (Uhrzeit, Tag), daneben <c>Title</c> (Gast, Person) und darunter <c>Detail</c> (Behandlung und Person, Dienstzeit).
/// Die ganze Zeile ist der Sprung: <c>Suggestion</c> (Kalender, Kundenkarte, Buchen) oder <c>Link</c> (Wochenplan); <c>Action</c> sagt, was ein Klick tut („Im Kalender zeigen“).
/// Der Kopfsatz der Antwort nennt nur die Zahl; jeder Eintrag steht genau einmal in der Liste.
/// </summary>
public sealed record AssistantItem(string Title, string? Detail, string? Lead, AssistantSuggestion? Suggestion, AssistantLink? Link, string Action);

/// <summary>
/// Antwort des Assistenten. Zahlen und Zeiten stammen aus dem Kalender und der Kassa, nicht von der KI;
/// die KI versteht nur die Frage. <c>Disclosure</c> wird immer angezeigt (AI Act Art. 50).
/// <c>Block</c>: bei Fragen zum Betrieb (Öffnungszeiten, Team, Angebot, Aktionen, Auswertungen) die strukturierte Antwort mit Quelle und Verweis.
/// <c>Unanswered</c>: die Frage hatte keinen Treffer; die Oberfläche legt diesen Text auf dem PC für die Liste „Offene Fragen an die Hilfe“ ab.
/// Kundennamen, E-Mail-Adressen und Telefonnummern sind daraus entfernt (Slice 56); bei jeder beantworteten Frage <c>null</c>.
/// </summary>
public sealed record AssistantAnswer(
    AssistantAudience Audience,
    string Text,
    IReadOnlyList<AssistantFigure> Figures,
    IReadOnlyList<AssistantSuggestion> Suggestions,
    string Disclosure,
    IReadOnlyList<AssistantHelpLink> Help,
    AssistantBlock? Block = null,
    string? Unanswered = null);

/// <summary>Frage eines Kunden in der Online-Buchung, z. B. „Massage nächste Woche abends?“.</summary>
public sealed record OnlineAssistantQuestion(string Question);

/// <summary>Vorgeschlagene freie Zeit; gebucht wird über die normale Online-Buchung.</summary>
public sealed record OnlineSuggestedTime(Guid TreatmentId, string TreatmentName, DateTimeOffset Start);

public sealed record OnlineAssistantAnswer(string Text, string Language, IReadOnlyList<OnlineSuggestedTime> Suggestions, string Disclosure);

/// <summary>
/// Wunsch aus der KI-Anfrage im Kalender (Strg+K) als bearbeitbare Bausteine, in Ortszeit des Standorts.
/// Nur <c>From</c> heißt „ungefähr um“, <c>From</c> und <c>Until</c> ein Zeitfenster.
/// </summary>
public sealed record CalendarWish(
    DateOnly Date,
    TimeOnly? From,
    TimeOnly? Until,
    Guid? TreatmentId,
    Guid? EmployeeId,
    IReadOnlyList<Guid> ExcludedEmployeeIds,
    Guid? RoomId = null,
    int? Guests = null);

/// <param name="Text">Wunsch in eigenen Worten oder eine Ergänzung („lieber Donnerstag“, „nicht bei Marco“); leer, wenn nur die Bausteine geändert wurden.</param>
/// <param name="Wish">Bisheriger Wunsch; der Text ergänzt oder ersetzt einzelne Bausteine.</param>
/// <param name="Date">Tag, der im Kalender offen ist; gilt, wenn weder Text noch Wunsch einen Tag nennen.</param>
/// <param name="Rejected">Schon angebotene oder abgelehnte Beginnzeiten; sie werden nicht noch einmal vorgeschlagen.</param>
public sealed record CalendarRequestInput(string? Text, CalendarWish? Wish, DateOnly? Date = null, IReadOnlyList<DateTimeOffset>? Rejected = null);

public enum CalendarWishPart
{
    Day,
    Time,
    Treatment,
    Employee,
    Excluded,
    Room,
    Guests,
}

/// <summary>Ein Baustein des Wunsches zum Anzeigen und Entfernen; <c>Id</c> nennt bei „Excluded“ die Person.</summary>
public sealed record CalendarWishChip(CalendarWishPart Part, string Text, Guid? Id = null);

/// <summary>Ein Vorschlag mit Person, Raum und Preis. Zeiten rechnet der Planer, nicht die KI.</summary>
/// <param name="MinutesFromWish">Abstand zur Wunschzeit in Minuten (negativ: früher); 0 ohne Wunschzeit.</param>
/// <param name="Reason">Kurz, warum dieser Vorschlag: „zur Wunschzeit“, „15 Min. später“, „schließt lückenlos an“.</param>
public sealed record CalendarProposal(
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid TreatmentId,
    string TreatmentName,
    Guid EmployeeId,
    string EmployeeName,
    Guid RoomId,
    string RoomName,
    Guid? EquipmentId,
    decimal Price,
    int MinutesFromWish,
    string Reason);

/// <summary>Antwort auf die KI-Anfrage: verstandener Wunsch, seine Bausteine und die drei besten Vorschläge. Gebucht wird nichts.</summary>
/// <param name="Text">Zusammenfassung oder Rückfrage („Für welche Behandlung?“).</param>
/// <param name="Disclosure">Hinweis auf die KI, immer anzeigen (AI Act Art. 50).</param>
public sealed record CalendarRequestResult(
    CalendarWish Wish,
    IReadOnlyList<CalendarWishChip> Chips,
    IReadOnlyList<CalendarProposal> Proposals,
    string Text,
    string Disclosure,
    IReadOnlyList<Guid>? TreatmentChoices = null,
    IReadOnlyList<CalendarQuestion>? Questions = null);

/// <summary>Antwort auf eine Rückfrage als Knopf: Beschriftung und der Wunsch mit der Antwort darin; die Oberfläche schickt ihn zurück.</summary>
public sealed record CalendarAnswer(string Label, CalendarWish Wish);

/// <summary>Rückfrage der KI-Anfrage („Für eine oder zwei Personen?“) mit den möglichen Antworten als Knöpfe.</summary>
public sealed record CalendarQuestion(string Text, IReadOnlyList<CalendarAnswer> Answers);

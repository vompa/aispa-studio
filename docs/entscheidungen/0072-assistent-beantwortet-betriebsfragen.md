# 0072. Der Assistent beantwortet Betriebsfragen aus den eigenen Daten

- **Status:** angenommen
- **Datum:** 22.10.2026
- **Entschieden von:** Auftraggeber (Runde 5, Ä36: Antworten aus den Daten statt aus dem Handbuch), Ausgestaltung Claude

## Kontext

Auf „Wie lange haben wir heute offen?“ nannte der Assistent bisher ein Handbuchthema, obwohl die Öffnungszeiten im Spa stehen. Der Auftraggeber will, dass er Fragen zum Betrieb aus den Einstellungen und Daten des Spas beantwortet: Öffnungszeiten und Feiertage, Kontakt und Anreise, Stornoregel, wer arbeitet, wie voll der Tag ist, welcher Raum frei ist, was eine Behandlung kostet, wann ein Gast zuletzt da war, welche Aktionen laufen, und Auswertungen wie Top-Behandlungen oder Auslastung je Person. Dabei gelten die Regeln aus [ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md) (die KI schlägt vor, Pflichtzahlen rechnet sie nie, [ADR 0010](0010-kassa-compliance-by-design.md)) und die Rechte aus [ADR 0070](0070-rollen-manager-und-backoffice.md) („Berichte mit Beträgen“).

## Optionen

| Option | Bewertung |
| --- | --- |
| Die Daten in die Anfrage an das Sprachmodell geben und es antworten lassen | schnell gebaut, aber das Modell kann Zahlen verändern oder erfinden, jede Frage schickt Betriebsdaten an einen Dienst, ohne API-Schlüssel gibt es nichts, und die Rechte müssten im Prompt „erklärt“ werden |
| Das Modell ruft Werkzeuge auf, die die Dienste befragen | richtige Zahlen, aber das Modell entscheidet, was es abfragt und wie es das Ergebnis wiedergibt; Rechteprüfung und Antwort bleiben schwer zu testen; wieder nichts ohne Schlüssel |
| Feiertage von einem externen Dienst holen | immer aktuell, aber Abhängigkeit, Datenschutz und Ausfall; die Tabelle mit Land, Bundesland, Kanton und Osterformel gibt es schon ([`PublicHolidays`](../../src/AispaStudio.Api/Payroll/PublicHolidays.cs), Lohnverrechnung) |
| Regelbasierte Antwortgeber, die KI erkennt nur die Absicht und glättet höchstens den Satz | gewählt |

## Entscheidung

- **Aufbau.** Absicht erkennen, den passenden **Antwortgeber** aufrufen, Antwort als strukturierten **Block** zurückgeben, die Oberflächen zeigen ihn. Ein Antwortgeber (`IAnswerGiver`, Ordner `Assistant/Answers`) ist eine Klasse je Thema; er nutzt nur Dienste und Abfragen, die es schon gibt, prüft die Rechte selbst, erfindet nichts und nennt die Quelle. Es gibt keinen neuen Rechenweg am Kern vorbei; was zu einer Auswertung fehlte (Rangliste der Gäste, Auslastung je Raum, Termine je Person, Zahl der Nicht-erschienen), steht im Berichtsdienst (`ManagementReports`), nicht im Assistenten.
- **Themen** (`BusinessTopic`, 23, je einer mit Antwortgeber, ein Test verlangt das): Öffnungszeiten, Kontakt, Feiertage, Anreise, Stornoregel; Wer arbeitet, ist eine Person da; Termine noch heute, wie voll ist der Tag, welcher Raum ist frei, Sauna; Preis und Dauer, wer bietet an, Gast zuletzt da, Gast nächste Woche, Gutschein; Aktionen; Top-Behandlungen, Top-Gäste, meiste Termine, Auslastung, Umsatz je Behandlung, Nicht-erschienen.
- **Absicht: erst Regeln, dann Modell.** `BusinessIntents` erkennt Thema, Name, Tag und Zeitraum an Schlüsselwörtern und Mustern („heute“, „Samstag“, „1. November“, „letztes Quartal“, „im September“, „seit 1.10.“); trifft eine Regel, wird das Modell nicht gefragt. Sonst ordnet das Sprachmodell (`ModelAssistantClassifier`, Claude oder das lokale Modell) die Frage in dasselbe Schema (Aufgabe `Business`, Thema, Name, Tag oder Zeitraum). Fragen zur Bedienung („Wie mache ich …?“) gehören dem Handbuch und kommen nie zu den Antwortgebern. Mit und ohne API-Schlüssel (`Ai:Provider=RuleBased`) kommen deshalb für erkannte Fragen **dieselben Fakten**; das Modell erweitert nur, was die Regeln nicht kennen.
- **Der Antwortblock** (`AssistantBlock` an `AssistantAnswer.Block`): Art (Antwort, nichts hinterlegt, nichts gefunden, keine Berechtigung, keine Absicht), Thema, Satz, Tabelle (höchstens zehn Zeilen), **Quelle**, Verweis (Einstellung oder „Im Bericht öffnen“ mit Standort und Zeitraum), Text zum Kopieren (Kontaktdaten, Anreise), Zeitraum in Worten und Angebote als Fragen. Fehlt ein Feld, sagt der Block „Dazu ist nichts hinterlegt“ und verweist auf die Einstellung (nur, wenn die Person sie ändern darf).
- **Die KI formuliert höchstens.** Mit Claude darf `ModelAnswerPhraser` den Satz einer Antwort aus Daten umformulieren. Er sieht nur Frage und Fakten. `AnswerGuard` verwirft die Formulierung, wenn sie eine Ziffer enthält, die nicht in den Fakten steht, ein Zahlwort („zwei“, „hundert“), das dort nicht steht, oder länger als 600 Zeichen ist; dann bleibt der Satz aus den Daten. Tabelle, Quelle und Verweis berührt das Modell nie; „nichts hinterlegt“ und „keine Berechtigung“ werden nie umformuliert. Grenze: Namen und andere Wörter prüft die Wache nicht.
- **Rechte** (im Antwortgeber, nicht in der Oberfläche; der Endpunkt lässt zusätzlich zum Kalenderrecht auch `ViewFinance` zu, damit das Backoffice Auswertungen fragen kann):

  | Thema | Recht | Sonst |
  | --- | --- | --- |
  | Öffnungszeiten, Kontakt, Feiertage, Anreise, Stornoregel, Team, Termine und Belegung, Angebot, Aktionen | `ViewCalendar` | „Dafür fehlt die Berechtigung“ (Backoffice) |
  | Gast zuletzt da oder nächster Termin | `ViewCalendar`, nur Termine an eigenen Standorten, ohne Kontakt- und Gesundheitsdaten | wie oben |
  | Gutschein-Restwert | `Book` (wie das Nachsehen an der Kassa) | abgelehnt |
  | Top-Behandlungen, meiste Termine, Auslastung, Nicht-erschienen, Top-Gäste nach Besuchen | `ViewFinance` | abgelehnt, ohne Angebot |
  | Umsatz je Behandlung, Top-Gäste nach Ausgaben | `ViewAmountReports` | abgelehnt, mit Angebot der Auswertung ohne Beträge als Frage |

  Das Backoffice bekommt vom Assistenten nur Auswertungen und das Handbuch. Therapeut:innen sehen bei den Terminen nur die eigenen. Kassa- und Steuer-Pflichtzahlen kommen in keiner Antwort vor und rechnet nie die KI.
- **Feiertage im Code.** Die Tabelle `PublicHolidays` (Österreich je Bundesland, Deutschland je Bundesland, Schweiz je Kanton, bewegliche Feiertage über die Osterformel) gilt auch für den Assistenten; das Feld Bundesland/Kanton am Standort gab es schon. Ohne Region nennt die Antwort die landesweiten Feiertage und sagt es.
- **Neue Felder.** `Anreise` am Standort (Freitext bis 500 Zeichen, Teil des Kontakts) und `Stornoregel` in den Buchungseinstellungen (Freitext bis 500 Zeichen, steuert nichts). Verwaltung, Hilfe und Beispieldaten; Migration `AssistentAnreiseUndStornoregel`.
- **Rückfall.** Erkennt weder Regel noch Modell eine Absicht und findet das Handbuch nichts Klares, antwortet der Assistent „Dazu habe ich nichts gefunden. Meinen Sie vielleicht eine dieser Fragen?“ mit zwei Fragen als Knöpfe, je nach Rolle. Eine Frage mit erkannter Behandlung geht wie bisher in die Suche nach freien Zeiten.
- **Oberflächen und Kennzeichnung.** Die Windows-App („KI und Hilfe“) und der Web-Assistent zeigen den Block mit der Kennzeichnung aus Block G: Pille „KI“, Hinweiszeile, „Warum diese Antwort?“ mit der Quelle und Rückmeldung. „Im Bericht öffnen“ und „Einstellung öffnen“ führen in die Verwaltung (in der App eingebettet, im Web die Seite mit Reiter, Standort und Zeitraum). Der Platzhalter des Fragefelds nennt Beispiele. Eine Antwort schlägt nur vor und führt nichts aus.

## Begründung

Fakten aus Einstellungen und Daten sind keine Sprachaufgabe: Sie stehen fest, lassen sich prüfen und müssen mit und ohne Schlüssel gleich lauten. Das Modell ist dort nützlich, wo es um Sprache geht (die Frage verstehen, Umschreibungen erkennen, einen Satz glätten), und wird dort begrenzt, wo ein Fehler schadet (Zahlen, Rechte). Die Rechte im Antwortgeber zu prüfen entspricht der Regel des Projekts, dass der Server bei jedem Aufruf prüft und die Oberfläche nur ausblendet. Dass der Berichtsdienst die Rangliste und die Raumauslastung liefert, hält den Grundsatz „Zahlen kommen vom Berichtsdienst“ ein, auch wenn die Rechenwege dafür neu sind.

## Folgen und Stand

- Code: `src/AispaStudio.Api/Assistant/Answers` (Absicht, Zeiträume, Antwortgeber, Blöcke, Formulierung), `RoleAssistant`, Ranglisten in `ManagementReports.Rankings.cs`, Aktionen für Zeiträume in `PromotionService`.
- Tests: Absicht und Zeiträume (95 Fälle), je Thema erlaubter und abgelehnter Fall, Feiertage nach Region, Rechte je Rolle, Backoffice über die API, Rückfall, Formulierung mit Skript-Modell (Zahlen bleiben); es gibt keine echten Claude-Aufrufe (`Ai:Provider=RuleBased` in der Testumgebung, auch wenn User-Secrets einen Schlüssel haben).
- Abweichungen von der Vorgabe und offene Punkte stehen im [PR-Protokoll](../umsetzung/pr-protokoll.md#runde-5-block-k2-assistent). Offen: Ausnahmen zu den Öffnungszeiten (Betriebsurlaub, besondere Tage) gibt es als Daten nicht; Feiertage in Schweizer Kantonen außerhalb Bern, Basel-Stadt und Zürich; Ausschalter der KI je Funktion und Rückmeldung an den Server (aus Block G).

## Quellen

- [ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md), [ADR 0010](0010-kassa-compliance-by-design.md), [ADR 0055](0055-hilfe-zu-jeder-einstellung-im-repository.md), [ADR 0057](0057-lokale-ki-optional-mit-ollama.md), [ADR 0070](0070-rollen-manager-und-backoffice.md), [ADR 0071](0071-aktionen-und-rabatte.md)
- Hilfe: [KI-Assistent fragen](../../web/src/help/content/ki-assistent-fragen.md), [Standorte](../../web/src/help/content/standorte.md)

## Nachtrag Runde 5b (Block K3, 22.10.2026)

Entschieden vom Auftraggeber (Runde 5b: Ä41, Ä42, Ä44, Ä45; Ä40, die Kantonsfeiertage, entfällt hier und kommt über die Kalender-Abos), Ausgestaltung Claude. Die Entscheidung oben gilt weiter; ergänzt wird:

- **Rechte, geändert** (Ä42): Auswertungen **ohne Beträge** beantwortet der Assistent jetzt jeder Person mit `ViewCalendar`, also auch der Rezeption (und der Therapie). Das sind Top-Behandlungen nach Anzahl, Auslastung in Prozent (je Person und Raum), Termine je Person, Nicht-erschienen und Top-Gäste nach Besuchen. Umsatz je Behandlung und Top-Gäste nach Ausgaben bleiben bei `ViewAmountReports`; der Berichtsdienst lässt Beträge ohne das Recht schon selbst weg. Der Endpunkt ändert sich nicht (`RequirePermission(ViewCalendar)` mit `AlsoAllow(ViewFinance, instead ViewCalendar)` lässt beide Wege zu). „Im Bericht öffnen“ gibt es weiter nur mit `ViewFinance`, weil der Management-Bericht diese Seite verlangt. Die Tabelle oben gilt mit dieser Zeile:

  | Thema | Recht | Sonst |
  | --- | --- | --- |
  | Top-Behandlungen, meiste Termine, Auslastung, Nicht-erschienen, Top-Gäste nach Besuchen | `ViewCalendar` oder `ViewFinance` | abgelehnt (nur ohne beide) |
  | Umsatz je Behandlung, Top-Gäste nach Ausgaben | `ViewAmountReports` | abgelehnt, mit Angebot der Auswertung ohne Beträge als Frage |

- **Abschnitt „Unvollständig“** (Ä44): konnte ein Antwortgeber antworten, fehlen dem Spa aber Angaben zum Thema, hängt `BusinessAnswers` an den Block (`AssistantBlock.Incomplete`) je Maske einen Hinweis („Unvollständig: Anreise fehlt beim Standort Wien.“) mit dem Ort in Worten („Stammdaten › Standorte › Anreise und Parkplätze“) und, wer das Recht hat, dem Verweis „Jetzt ergänzen“, sonst dem Text „Nur Leitung“. Welche Angaben ein Thema braucht, nennt jeder Antwortgeber (`IAnswerGiver.Fields`, Feldkennungen wie `location.directions`); ob sie leer sind, prüft zentral `FieldGaps`. Die Zuordnung **Feldkennung → Maske** (Verwaltungsseite und Reiter, Ort in Worten, Beschriftung, nötiges Recht) steht in einer Tabelle (`FieldMasks`, 14 Felder: Öffnungszeiten, Bundesland oder Kanton, Telefon, E-Mail, Adresse, Anreise, Stornoregel, Räume, Bereiche, Website der Firmendaten, Preis und Dauer einer Behandlung, Aktionen, Betriebsinformationen); **alle** Verweise der Antwortgeber entstehen dort, keiner wird von Hand gebaut. Die Themen: Öffnungszeiten (Bundesland), Kontakt und Anreise, Feiertage, Stornoregel, Aktionen und die Behandlungen bei Preis und Dauer. Ist die Angabe ganz leer, bleibt „Dazu ist nichts hinterlegt“ und der Verweis (jetzt mit Feldkennung) führt ebenfalls an das Feld.
- **Das Feld im Verweis:** `AssistantLink` trägt `Field` (Feldkennung) und `TargetId` (Behandlung). Die Verwaltung öffnet die Maske am Feld: Web als Adresse mit `feld=…` (die Maske scrollt hin und setzt den Fokus über `data-field`), die Windows-App über dieselbe Adresse in der eingebetteten Verwaltung wie bei „Im Bericht öffnen“. Nach dem Speichern meldet die Verwaltung „gespeichert“ (Web: Ereignis im Fenster, eingebettet: Nachricht `{"type":"saved"}` an die App), und die Leiste „KI und Hilfe“ bietet „Daten gespeichert. Frage wiederholen?“ an, aber nur, wenn die Person über „Jetzt ergänzen“ dorthin gekommen ist.
- **Geburtstage** (Ä45): neues Thema `Birthdays` (mit den Betriebsinformationen aus Block M und den Schulferien aus Block K4 26 Themen, ein Antwortgeber mehr): „Wer hat heute Geburtstag?“, „Wer hat diese Woche Geburtstag?“. Recht `ViewCalendar`, Quelle „Termine und Kundendaten“. Datenschutz: nur Gäste **mit Termin im Zeitraum** am Standort (nie eine Liste des Kundenstamms), kein Alter in der Antwort; wer nur die eigenen Termine sieht, bekommt nur Gäste aus den eigenen Terminen. Den Geburtstag selbst rechnet der Server (`Birthdays`, `GuestBirthday` am Kalendertag und an der Kundenkarte); der 29. Februar gilt in Jahren ohne Schalttag am 28. Februar.
- **Anreise im Kundenbereich** (Ä41, Antwort auf die Frage (5) des PR-Protokolls K2): der Text „Anreise und Parkplätze“ ist nicht mehr nur für Team und Assistent. Der Kundenbereich zeigt ihn beim Standort des Termins mit „Route planen“ (Link in die Karten-App, ohne Standortabfrage), die Bestätigungsmail bekommt die Zeile „Anreise“ mit Link.

## Nachtrag Slice 147 (Runde R07, Ä319, 04.11.2026)

Ausgestaltung Claude im unbeaufsichtigten Lauf. Die Entscheidung oben gilt weiter; ergänzt wird, wie der Assistent mit **Aufträgen** umgeht („buche …“, „verschiebe …“, „sag … ab“, „schick … den Bogen“, „biete … an“, „offene Rechnung …“):

- **Nur als Vorschlag in der Maske** (ADR 0008): Der Server erkennt den Auftrag am Satzanfang nach Regeln (`AssistantCommands`, gleich mit und ohne Claude-Schlüssel) und liefert eine Antwort mit genau einem Vorschlag (`AssistantSuggestionKind.NewAppointment`, `MoveAppointment`, `CancelAppointment`, `SendQuestionnaire`, `OfferWaitlist`, `OpenBill`). Die Windows-App öffnet damit erst nach dem Klick auf den Knopf die bestehende Maske: „Neuer Termin“, „Verschieben“ im Termin, die Rückfrage zum Stornieren, den Termin mit dem Abschnitt Anamnese, die freien Zeiten zum Wunsch auf der Warteliste oder die Rechnung in der Kassa. Gespeichert, gebucht oder abgesagt wird nur mit dem Knopf der Maske; der Assistent selbst ändert nie Daten.
- **Vorausgefüllt, Lücken bleiben leer:** Gast, Behandlung, Person, Tag und Uhrzeit liest derselbe regelbasierte Leser wie bei „Wunsch eintippen“; was der Satz nicht nennt, bleibt leer (z. B. „Beginn“ ohne Uhrzeit). Ohne Namen gilt der offene Termin bzw. die offene Kundenkarte, bei mehreren passenden Gästen zuerst die Auswahl.
- **Kennzeichnung:** Die Buchungsmaske und das Termin-Panel zeigen oben den Hinweis „Vorschlag des KI-Assistenten, bitte prüfen …“ (InfoBar); Warteliste und Kassa sind reine Sprünge und bekommen keinen Hinweis.
- **Rechte:** Aufträge gibt es nur mit `AccessRight.Book`, auch für die Kassa und den Anamnesebogen; ohne das Recht antwortet der Assistent mit einem Satz statt eines Knopfs, und die App zeigt Auftragsknöpfe nur mit Buchungsrecht. Die Absage nennt die Gebühr nach der Stornoregel wie die Frage „… fällt eine Gebühr an“ (Betrag nur mit Kassarecht).
- **Nicht gebaut:** Speichern ohne Knopfdruck; Aufträge an Lager oder Dienstplan.
- Code: `src/AispaStudio.Api/Assistant/AssistantCommands.cs`, `AssistantOrders.cs`; App `CalendarViewModel.AssistantOrders.cs`, `ShellViewModel.OpenAssistantOrderAsync`. Tests: `AssistantCommandTests` (API), `AssistantActionsTests` (App).

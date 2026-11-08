Status: closed

# Runde R07 – Alltag der Rezeption: Kassa, KI und Hilfe, Kunden, Verwaltung und Hilfe ruhiger und lesbarer

Geöffnet am 03.11.2026 nach dem Abschluss von R06. Alle Slices bauen auf Masken, die es schon gibt; sie ordnen, kürzen und machen lesbar, neue Fachfunktionen gibt es nur in Slice 147 (Aufträge an den Assistenten).

## Quelle
- Die Punkte des Auftraggebers vom 03.11.2026 in der Sitzung nach dem Abschluss von R06, gesammelt mit „ich schreibe wieder fertig“ und mit „Fertig, bitte mit den schon gelpanten eine neue Runde für die Umsetzung“ freigegeben (Ä344–Ä354), jeweils mit Bildschirmfoto und dem Auftrag, wie eine Rezeptionistin zu denken, die das täglich verwendet.
- Die Vorschläge zu jedem Punkt standen in der Sitzung; die Slice-Dateien übernehmen sie als Umfang.
- Aus R06 übernommen: Ä313 (Testdaten für Belege) und Ä319 (Aufträge an den Assistenten).

## Eingang
| Kennung | Bereich | Befund | Größe | Quelle |
|---|---|---|---|---|
| Ä313 | Kassa, Beispieldaten | „Testdaten für Belege fehlen noch“: gemeint ist, dass der Reiter „Belege“ der Kassa keine oder zu wenige Belege zeigt (aus R06) – erledigt in Slice 146 | S | Auftraggeber, 02.11.2026; Deutung Auftraggeber, 03.11.2026 |
| Ä319 | API, Windows-App, Assistent | Aufträge wie „buche“, „verschiebe“, „sag ab“ öffnen keine Maske → vorausgefüllte Maske, gespeichert erst per Knopf (aus R06) | L | Claude, 02.11.2026 (Vorschlag im Fragenkatalog) |
| Ä344 | Windows-App, Kassa | „Heute abzurechnen“ ist zu klein, Namen und Behandlungen sind abgeschnitten; Artikel und Gutscheine nehmen den Platz → Liste als breite Hauptspalte mit Suche und Gruppen – erledigt in Slice 137 | M | Auftraggeber, 03.11.2026 |
| Ä345 | Windows-App, Kassa | Mit offenem „KI und Hilfe“ wird die Kassa noch enger → die Liste behält ihre Mindestbreite – erledigt in Slice 137 | S | Auftraggeber, 03.11.2026 (Bild) |
| Ä346 | API, Assistent | „Top-Behandlungen“ wiederholt die Liste im Kopfsatz und nummeriert Plätze bei Gleichstand | S | Claude, 03.11.2026 (aus dem Bild des Auftraggebers) |
| Ä347 | Windows-App, KI und Hilfe | Das Panel zeigt beim Öffnen die alte Antwort → zurücksetzen beim Schließen, kleiner Verlauf „Zuletzt gefragt“ – erledigt in Slice 138 | M | Auftraggeber, 03.11.2026 |
| Ä348 | Windows-App, Kunden | „Neue Kundin“ ist unübersichtlich: Anrede und Titel zuerst, keine Prüfung auf Dubletten | M | Auftraggeber, 03.11.2026 |
| Ä349 | Windows-App, Kunden | Kundenkarte: Werte unterscheiden sich nicht von Überschriften, zu viele Karten, wirkt alt – erledigt in Slice 141 | L | Auftraggeber, 03.11.2026 |
| Ä350 | Windows-App, Verwaltung, Team | Dienstplan: Werte kaum lesbar, Legende kryptisch, Stunden losgelöst | M | Auftraggeber, 03.11.2026 |
| Ä351 | Windows-App, Verwaltung, Lager | Lager unübersichtlich, Tabelle mit neun Spalten, zu weites Scrollen → Unterreiter | L | Auftraggeber, 03.11.2026 |
| Ä352 | Windows-App, Verwaltung, Zahlen | „Zahlen“ wirkt unfertig: zwei Reiterebenen, Tagesstart als Fließtext | M | Auftraggeber, 03.11.2026 |
| Ä353 | Windows-App, alle Masken | Inhaberin und Leitung müssen in denselben Masken auch verwalten können → Kompromiss: eine Maske je Bereich, Tiefe nach Rolle | – | Auftraggeber, 03.11.2026 |
| Ä354 | Hilfe | Hilfe wirkt unfertig: Einrückungen, Brotkrumen-Knopf am Rand, Inhaltsverzeichnis vor dem Inhalt | M | Auftraggeber, 03.11.2026 |

## Slices
| Slice | Name | Befunde | Hängt ab von | Modell | Neue Kennungen |
|---|---|---|---|---|---|
| 137 | Kassa: „Heute abzurechnen“ als Hauptliste | Ä344, Ä345 | – | opus | Ä355–Ä357 |
| 138 | KI und Hilfe: leer nach dem Schließen, „Zuletzt gefragt“ | Ä347 | – | sonnet | Ä358–Ä360 |
| 139 | Assistent: Ranglisten ohne Wiederholung und mit Gleichstand | Ä346 | – | sonnet | Ä361–Ä363 |
| 140 | Neue Kundin: Name und Telefon zuerst, Dubletten beim Tippen | Ä348 | – | sonnet | Ä364–Ä366 |
| 141 | Kundenkarte: ruhiger Kopf, „Gut zu wissen“ und Verlauf | Ä349 | 140 | opus | Ä367–Ä369 |
| 142 | Dienstplan: lesbare Woche mit Schicht-Chips | Ä350 | – | opus | Ä370–Ä372 |
| 143 | Lager: Unterreiter und „Was ist zu tun“ | Ä351 | – | opus | Ä373–Ä375 |
| 144 | Zahlen: eine Reiterebene, Tagesstart als Übersicht | Ä352 | – | opus | Ä376–Ä378 |
| 145 | Hilfe: ruhiges Layout mit Bereichen und Inhaltsspalte | Ä354 | – | opus | Ä379–Ä381 |
| 146 | Kassa: der Reiter „Belege“ zeigt die Belege der Beispieldaten | Ä313 | 137 | sonnet | Ä382–Ä384 |
| 147 | Assistent: Aufträge öffnen die vorausgefüllte Maske | Ä319 | 138, 139 | opus | Ä385–Ä387 |

Reihenfolge: höchstens zwei Slices zugleich, Slices mit gemeinsamen Dateien nacheinander. Kassa: 137, dann 146. KI: 138 und 139, dann 147. Kunden: 140, dann 141. Verwaltung und Hilfe: 142, 143, 144 und 145 unabhängig. Ä353 ist kein eigener Slice: Slice 137 hält den Grundsatz in docs/gestaltung/masken.md fest, die übrigen Masken-Slices wenden ihn an. Prüfstufe 3 (Standard); Meilensteine sind das Ende der Kunden-Kette (nach 141) und der Rundenabschluss.

## Entscheidungen
Ruling: An der Kassa ist „Heute abzurechnen“ die breite Hauptliste links mit Suchfeld für Gast, Code und Artikel, ungekürzten Zeilen und den Gruppen „Wartet auf Abrechnung“, „Später heute“ und „Erledigt“; Artikel und Gutscheine stehen als zugeklapptes Panel neben der Rechnung, und die Liste behält auch mit offenem „KI und Hilfe“ ihre Breite (Ä344, Ä345) – User, 03.11.2026
Ruling: „KI und Hilfe“ beginnt nach dem Schließen wieder wie beim ersten Aufruf, mit „Neu beginnen“ und einem Verlauf der letzten Fragen von heute je Person, nur lokal auf dem Gerät und ohne gespeicherte Antworten (Ä347) – User, 03.11.2026
Ruling: Ranglisten des Assistenten wiederholen die Liste nicht im Kopfsatz und nummerieren gleiche Werte nicht (Ä346) – User, 03.11.2026
Ruling: Die Maske „Neue Kundin“ fragt zuerst Vorname, Nachname, Telefon und E-Mail, zeigt Dubletten beim Tippen und bietet „Anlegen und Termin buchen“; Anrede, Titel, Rufname und Notiz stehen unter „Weitere Angaben“ (Ä348) – User, 03.11.2026
Ruling: Die Kundenkarte bekommt einen Kopf mit Aktionen, eine Kennzahlen-Zeile, „Gut zu wissen“ als Chips, den Verlauf als zweite Spalte und keine Abschnitte für Leeres; Werte heben sich klar von Bezeichnungen ab, statt vieler Karten gibt es eine Fläche (Ä349) – User, 03.11.2026
Ruling: Dienstplan, Lager und „Zahlen“ bekommen ein ruhiges, modernes Layout mit „Was ist zu tun“ oben, lesbaren Werten und Unterreitern statt langem Scrollen (Ä350, Ä351, Ä352) – User, 03.11.2026
Ruling: Eine Maske je Bereich, deren Tiefe die Rolle bestimmt: oben der Alltag der Rezeption, dahinter die Verwaltung für Inhaberin und Leitung; getrennte Masken nur, wenn die Aufgabe eine andere ist (Ä353) – User, 03.11.2026
Ruling: Die Hilfe ordnet Themen nach Bereichen mit Rollenfilter, zeigt den Inhalt in einer lesbaren Spalte und das Inhaltsverzeichnis daneben (Ä354) – User, 03.11.2026
Ruling: Ä313 meint den Reiter „Belege“ der Kassa, der keine oder zu wenige Belege zeigt – User, 03.11.2026
Ruling: Die Aufträge an den Assistenten (Ä319) kommen in diese Runde – User, 03.11.2026
Build decision (slice 137): Auto-Lauf; der Plan work/plans/2026-11-03-slice-137.md gilt mit seinen Annahmen 1 bis 10, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 137): Bezahlen rückt unter 1160 px Seitenbreite, mit offenem Panel unter 1350 px unter die Rechnung statt unter 1100 px (Annahme 6), und die Seite rollt dann als Ganzes: bei 1280 px Bildschirm ist die Seite 1192 px breit, mit offenem Panel blieben der Rechnung sonst 4 px; die Grenzen sind Rand 32 + Liste 380 + Rechnung 310 + Leiste 44 bzw. Panel 232 + Bezahlen 340 + Abstände 48. Ohne rollende Seite drückte das rund 750 px hohe Bezahlen Rechnung und Panel auf 81 px (nur im Bild sichtbar, rot auf 09eed50a) – Claude, 03.11.2026
Build decision (slice 137): Die Einstellung heißt `PosSalesPanelOpen` (Standard zugeklappt) statt `PosSalesColumnCollapsed`; der alte Wert verfällt einmalig, ein früher aufgeklapptes Panel beginnt also einmal zugeklappt – Claude, 03.11.2026
Build decision (slice 137): Tests aus Slice 129 an die neue Anordnung angepasst, nicht abgeschwächt: Reihenfolge Liste < Rechnung < Panel < Bezahlen, Tests mit offenem Panel klappen es ausdrücklich auf, „Aufgeklappt bleibt nach Neustart“ ersetzt „Eingeklappt bleibt“; beim Zuklappen prüft der Test, dass Rechnung und Liste zusammen den ganzen frei gewordenen Platz bekommen (die Liste hat jetzt Anteil 2*), statt „Rechnung + 180 px“ – Claude, 03.11.2026
Build decision (slice 137): Die Zeilen von „Heute abzurechnen“ sind Einträge einer `ListBox` (Pfeiltasten) statt Knöpfe; der Namenstest prüft deshalb die `ListBoxItem`s – Claude, 03.11.2026
Build decision (slice 137): AC 4 (Code vom Handscanner) prüft der bestehende Test `Kassa_Code_9001234567890_scannen_legt_den_Artikel_mit_Menge_1_auf_den_Beleg`, der jetzt im zusammengelegten Suchfeld läuft und grün blieb; kein doppelter Test – Claude, 03.11.2026
Build decision (slice 137): Rot-Nachweis der Desktop-Tests auf dem Wegwerf-Branch `slice-137-red` (Stand 6a846497 mit den neuen Tests, Commit 5b059c2d); dort liest der Test die Einstellung unter dem alten Namen, und der ViewModel-Suchtest war geleert, weil `ArticleHits` und `ShowNoHits` fehlen (rot durch fehlende Mitglieder, nicht ausgeführt) – Claude, 03.11.2026
Build decision (slice 137): `PosDayEntryDto` trägt den ganzen `AppointmentDto` bzw. `AreaVisitDto` (genau eines gesetzt) statt Kind, Id und Ende aus dem Plan-Abschnitt „Dateien“: die App braucht beide für das bestehende `AddAppointmentAsync`/`AddVisitAsync`, und der Kalender-Weg „Kassieren“ bleibt unverändert – Claude, 03.11.2026
Build decision (slice 137): Der Hinweis ohne Rechnung lautet „In „Heute abzurechnen“ einen Gast wählen oder einen Code scannen.“ statt „Links einen Gast wählen …“ (Annahme 10): `DesignTokenTests` verbietet Richtungswörter, und in der schmalen Anordnung stimmt „links“ nicht – Claude, 03.11.2026
Build decision (slice 137): Enter im Suchfeld sucht wie im Plan (Annahme 8) nur den Code; eine erste Fassung setzte die erste passende Zeile auf die Rechnung (Review: „Steiner“ + Enter hätte einen von zwei Gästen ungewählt genommen, ein kurzer Code hätte einen Eintritt getroffen). Ein Klick auf eine Gruppenüberschrift setzt keinen Gast mehr auf die Rechnung, und die HTTP-Tests der Tagesliste laufen mit fester Uhr (sonst ab dem Testtag 01.03.2027 rot) (Review) – Claude, 03.11.2026
Build decision (slice 137): Ä353 ist als Regel R5a in docs/gestaltung/masken.md festgehalten und bleibt in der Eingangstabelle offen, weil die übrigen Masken-Slices ihn anwenden – Claude, 03.11.2026
Build decision (slice 138): Auto-Lauf; der Plan work/plans/2026-11-03-slice-138.md gilt mit seinen Annahmen 1 bis 10, Spalte „Plan“ damit angehakt – Claude, 03.11.2026
Build decision (slice 138): Spalte „Tests red“ angehakt mit dem Rot-Lauf auf 0239ce68 (Prüfnachweis): zehn der zwölf Tests der ViewModel-Gruppe rot auf einem Gerüst ohne Verhalten, die zwei Schutztests (Seitenwechsel, Lesen alter Einstellungen) bestanden dort schon; die sechs Ansichtstests entstanden nach der Umsetzung, ihre Wirkung belegt eine Gegenprobe (Zurücksetzen beim Schließen ausgeschaltet: alle vier Schließen-Tests rot) – Claude, 03.11.2026
Build decision (slice 138): „Neu beginnen“ steht in der Zeile „Assistent“ der Ansicht, nicht im gemeinsamen Kopf `SidePanelHeader` neben dem Kreuz; der Kopf gehört allen Seitenleisten, ein Platz dort wäre eine Änderung am gemeinsamen Steuerelement (Ä359) – Claude, 03.11.2026; nachgezogen in T-018 (Kopf mit Platz für eine Aktion)
Build decision (slice 138): Die Frage kommt in den Verlauf, sobald sie gestellt wird, nicht erst bei gelungener Antwort; der Verlauf liegt als `AssistantHistory` (Person, Tag, Fragen) in `einstellungen.json`, wird beim Abmelden gelöscht und gilt nur für die Person und den Kalendertag (lokale Zeit) – Claude, 03.11.2026
Build decision (slice 138): Zurückgesetzt wird nur beim ausdrücklichen Schließen (`Dismiss()`: Kreuz, Esc, Strg+Leertaste, Knopf unten rechts, geschlossener Reiter im Kalender), nicht bei jedem Wechsel von `IsOpen`; der Kalender schaltet „offen“ auch beim Wechsel des Reiters aus, dann bleibt der Stand im Hintergrund (Fund der Durchsicht) – Claude, 03.11.2026
Build decision (slice 138): Nur getippter Text (und die festen Beispiele) kommt in „Zuletzt gefragt“; der Text eines Angebots der Antwort (`AssistantFollowUp`) stammt vom Server und wird nicht gemerkt; ein Verlauf von gestern oder einer anderen Person wird beim Laden auch vom Gerät genommen (Fund der Durchsicht) – Claude, 03.11.2026
Build decision (slice 138): `Settings.cs` und `ShellViewModel.cs` sind nicht in den Dateien der Slice genannt, aber für „je Person“ und „beim Abmelden“ nötig; beide Änderungen sind klein (Feld, `HistoryOwner`, `ForgetHistory()` vor dem Abmelden) – Claude, 03.11.2026
Ruling: Slice 138 wird nach dem Einmischen von dev (Slice 137) ohne erneuten Testlauf auf dem eingemischten Stand übernommen; geprüft war der Stand vor dem Einmischen, die Prüfungen danach wurden abgebrochen – User, 03.11.2026
Build decision (slice 140): Auftrag aus Code Guardian (T-002, Plan work/slices/140-app-customer-new-form.md), ohne Planfreigabe-Datei unter work/plans; die Annahmen stehen hier – Claude, 03.11.2026
Build decision (slice 140): „Schon vorhanden?“ fragt die Kundensuche (`GET api/customers?search=`) mit dem Nachnamen und den letzten sieben Ziffern des Telefons, höchstens drei Treffer, „Huber, Anna“ wie in der Liste; die Matcher-Erkennung (`POST api/customers/duplicates`, ähnlicher Name, gleiche Mailadresse) entfällt in der Kundenmaske und bleibt in der Buchungsmaske. Die zwei Tests dazu in `InboxCustomerTests` sind durch Entsprechungen in `CustomerNewFormTests` ersetzt (Treffer, „Diese Kundin nehmen“, zu wenig Angaben) – Claude, 03.11.2026
Build decision (slice 140): Die API formatiert das Telefon beim Anlegen und Ändern (`PhoneNumbers.Format`, Vorwahl des Spa-Landes): Österreich trennt nach „1“ (Wien) oder drei Ziffern, Deutschland nur Mobilnummern (drei Ziffern), die Schweiz nach zwei, andere Länder und deutsches Festnetz bleiben ungetrennt; was sich nicht als Nummer lesen lässt, bleibt wie getippt – Claude, 03.11.2026
Build decision (slice 140): Ein einzelnes Wort aus der Suche ist der Nachname (vorher Vorname), zwei Wörter Vor- und Nachname; der Fokus steht beim Öffnen immer im Vornamen. Enter in einem Feld legt an (Code-Behind der Ansicht, ohne `IsDefault`, weil der sonst auf jedes Enter im Fenster reagiert), Esc bricht ab (`CancelCard`), „Abbrechen“ verwirft die Eingabe – Claude, 03.11.2026
Build decision (slice 140): „Anlegen und Termin buchen“ ruft `CustomersViewModel.BookRequested`; die Shell wechselt in den Kalender und `CalendarViewModel.OpenBookingFor` öffnet die Buchung wie „+ Termin“ mit dem Gast – Claude, 03.11.2026
Build decision (slice 142): Auftrag aus Code Guardian (T-004, Plan work/slices/142-app-roster-week-layout.md) im unbeaufsichtigten Nachtlauf, ohne Planfreigabe-Datei unter work/plans; die Annahmen stehen hier – Claude, 04.11.2026
Build decision (slice 142): Das Soll neben dem Namen („8 / 40 h“) ist die Summe der regelmäßigen Wochenarbeitszeit der Person über alle Standorte aus den Team-Stammdaten; ohne Wochenarbeitszeit steht nur „8 h“. Die API liefert kein Soll und bekommt keines (Planungslogik bleibt unberührt) – Claude, 04.11.2026
Build decision (slice 142): Chip-Fläche = Farbe der Person um 78 % zu Weiß aufgehellt, Schrift fest `#1a2321` in beiden Farbschemata, damit der Kontrast nie vom Thema abhängt (sechs Farben, darunter Schwarz und Weiß, rechnen ≥ 4,5:1 nach); Balkenfarben aus bestehenden Tokens (Urlaub accent, Krank danger, Schulung warn, Frei surface-3), kein neues Token – Claude, 04.11.2026 (Nachtrag: „Schrift fest `#1a2321` in beiden Farbschemata“ abgelöst in T-039; die Tinte ist jetzt das Token `RosterChipInkBrush`, hell `#1a2321`, dunkel `#e2eae7`)
Build decision (slice 142): Der Abwesenheitsbalken liegt als Überlagerung über den Tageszellen und nimmt keine Klicks an; jeder Tag darunter bleibt einzeln anklickbar und öffnet das Bearbeiten dieses Tages. Ein Balken fasst aufeinanderfolgende Ganztage derselben Art zusammen; gemischte Tage (Dienst und Abwesenheit) zeigen Chips – Claude, 04.11.2026
Build decision (slice 142): Die Zeile „Besetzung“ zählt Personen mit mindestens einem Dienst am gewählten Standort; eine Warnung gibt es nicht, weil sich keine Mindestbesetzung hinterlegen lässt (der Plan verbietet neue Regeln dazu). Auf der Karte einer Person fehlt die Zeile – Claude, 04.11.2026
Build decision (slice 142): Die Legendenzeile entfällt samt dem Hinweis „Zelle anklicken zum Planen“; die Tests dazu (`Legend`, `PublishText`, `DayHeads`) prüfen jetzt Band, Köpfe und Stundentext. Der alte Test der Zellabstände (12/8, Rand 4) prüft die neue Geometrie (4/6, Rand 2), weil der Chip die Zellbreite füllt; „nicht umgebrochen, nicht abgeschnitten“ bleibt – Claude, 04.11.2026
Build decision (slice 142): Auf der Personenkarte ist die Namensspalte nur 72 px breit (Platz für die Stunden), damit die Tage nicht aus dem Bild rutschen – Claude, 04.11.2026
Build decision (slice 144): Auftrag aus Code Guardian (T-006, Plan work/slices/144-app-reports-briefing-layout.md) im unbeaufsichtigten Nachtlauf, ohne Planfreigabe-Datei unter work/plans; die Annahmen stehen hier – Claude, 04.11.2026
Build decision (slice 144): „Zahlen“ ist eine Maske mit einer Reiterebene (Tagesstart · Mein Tag · Hygiene · Berichte · Betriebsinfo). Die Gruppe „Zahlen“ der Verwaltung hat dafür nur noch den Bereich „Zahlen“ (Seite `reports`); der Bereich „Betriebsinfo“ und `AdminPageViewModel.BusinessInfo` entfallen, die Betriebsinfo ist ein Reiter von `ReportsViewModel` (eingebettetes `BusinessInfoViewModel`). Sprünge auf `businessInfo` (Assistent) öffnen „Zahlen“ beim Reiter Betriebsinfo – Claude, 04.11.2026
Build decision (slice 144): Berichte und Betriebsinfo nur mit dem Recht `ViewFinance` („Berichtsrecht“, wörtlich nach der Definition of Done). Vorher sah die Rezeption (nur `ViewCalendar`) die Betriebsinfo in der Verwaltung; das entfällt in der App (der Assistent beantwortet weiter Einträge der Stufe „Team“, die API prüft jede Aktion selbst, an den Rechten der API ändert sich nichts) – Claude, 04.11.2026 – aufgehoben in T-025: die Betriebsinfo sehen alle, die buchen dürfen (Auftraggeber, 05.11.2026; dev)
Build decision (slice 144): „Berichte“ ist ein Reiter, Management und Pflichtberichte sind Unterreiter rechts im Reiter (Ä353: Alltag oben, Verwaltung dahinter), ihre Inhalte bleiben unverändert (Plan: nichts an den Berichten ändern). Der Befehlsparameter „reports“ des Reiters bleibt beim offenen Unterreiter – Claude, 04.11.2026
Build decision (slice 144): Der erste erlaubte Reiter in der Reihenfolge der Leiste ist der Startreiter (Tagesstart vor Mein Tag); ein Konto nur mit Kalenderrecht beginnt bei Mein Tag. Weil das Laden des Tagesstarts auf die KI-Zusammenfassung wartet, wartet `EnsureLoadedAsync` jetzt beim Öffnen von „Zahlen“ bis zum Ende des Nachfragens; alle Aufrufer in der App starten es ohne zu warten – Claude, 04.11.2026
Build decision (slice 144): „Im Dienst“ heißt, die Person hat im Tagesstart Zeiten (`Times` nicht leer); wer abwesend ist, hat statt Zeiten den Zustand der API („Urlaub“), der als Pille an die Stelle der Leiste tritt. Die Zeitleiste reicht von der Öffnung bis zum Schluss des Standorts an diesem Wochentag, ohne Öffnungszeiten vom ersten Dienstbeginn bis zum letzten Dienstende; ein Dienst außerhalb weitet sie – Claude, 04.11.2026
Build decision (slice 144): Der Anfangsbestand der Kassa kommt aus dem Satz der API („Kassa AT1: Anfangsbestand laut letztem Abschluss 250,00 €.“) und steht als Wert; „noch kein Tagesabschluss“ steht als „kein Abschluss“, jede andere Kassazeile als Warnband. Ein neues Feld in der API ist ausgeschlossen (Plan: keine neuen Kennzahlen) – Claude, 04.11.2026
Build decision (slice 144): Status und Wertezeile gehören allen drei Alltagsreitern über eine gemeinsame Ansicht (`ReportOverviewView`): Der Status ist die vorhandene InfoBar (Erfolg „Nichts Dringendes offen“ oder Warnung mit den Punkten darunter), neu sind nur die Wertezeile (`TextBlock.bigvalue`) und die Zeitleiste (`StaffBar`), beide in docs/design.md 5.8 festgehalten – Claude, 04.11.2026
Build decision (slice 144): Mein Tag zeigt als Status die Hinweise der API (sonst „Keine Behandlungen heute“ bzw. „Nichts Dringendes offen“) und die Werte Behandlungen, Vorbereitung offen und Dienst; Hygiene zeigt „Alles erledigt“ oder „n Punkte offen, m überfällig“ und die Werte Räume, Offen, Überfällig, Abgenommen. Abschnitte statt gerahmter Karten (`Border.section` der Kundenkarte) – Claude, 04.11.2026
Build decision (slice 144): Bestehende Tests mit der alten Reihenfolge angepasst, Absicht unverändert: Startreiter Tagesstart statt Mein Tag (`AdminReportsTests`), Bereich „Zahlen“ statt „Berichte“ und „Betriebsinfo“ (`AdminPageTests`, `ViewTests.AdminReports/AdminNavigation/Assistant/JumpTargets`); der Test zum Nachfragen der KI wartet nicht mehr auf `EnsureLoadedAsync` – Claude, 04.11.2026
Build decision (slice 146): ANNAHME: Die Ursache von Ä313 liegt in der Anzeige, nicht in den Beispieldaten — der neue API-Test `DemoReceiptsTests` (Einspielen am 22.10.2026 um 12 Uhr UTC) findet an Kasse WIEN-1 für heute und gestern Verkäufe, und der Endpunkt filtert richtig. Der Reiter „Belege“ war fest auf heute verdrahtet und ohne Tageswahl; vor der Öffnung der Kassa ist heute leer.
Build decision (slice 146): ANNAHME: Die Tageswahl sind zwei Pfeile neben der Überschrift („Tag davor“, „Tag danach“), kein Datumsfeld — die Beispieldaten reichen acht Wochen zurück, der Weg Tag für Tag genügt für die Abnahme, ein Datumsfeld wäre mehr Oberfläche. „Tag danach“ ist an heute gesperrt; beim Öffnen des Reiters steht wieder heute da.
Build decision (slice 146): ANNAHME: Die Beispieldaten selbst bleiben unverändert (rund 2.200 Belege) — beide Wege von `IDemoOps` sind nicht angefasst, `DemoBulkTests` entfällt als Prüfung.

## Überraschungen
- Slice 137 (Ä355): Termine mit bezahlter Anzahlung fehlten in „Heute abzurechnen“, weil die alte Abfrage jede Rechnungszeile zum Termin als „abgerechnet“ zählte, auch die Anzahlung; die Tagesliste zählt Anzahlungszeilen nicht mehr (behoben in Slice 137).
- Slice 137: Im Rot-Lauf der API-Tests auf 3572286e scheiterte `Tagesliste_nennt_Person_Raum_und_den_Eintrittspreis_des_Bereichs` an der Buchung um 9 Uhr (Anna hat da noch keinen Dienst), nicht am fehlenden Verhalten; die Buchungen stehen jetzt auf 10 Uhr, die beiden anderen HTTP-Tests waren aus dem richtigen Grund rot (404 bzw. alte Regel).
- Slice 137: Die Tests zur schmalen Kassa waren grün, obwohl Bezahlen in der Zeile unter der Rechnung Rechnung und Panel auf 81 px Höhe drückte; erst das Bild zeigte es. Die Tests prüfen jetzt die Höhe.
- Slice 137: Die TextBox des Suchfelds und die `ListBox` behandeln Pfeiltasten selbst; ein `KeyDown`-Handler im XAML sah sie nie, erst per Tunnel (`AddHandler(…, RoutingStrategies.Tunnel)`).
Slice 138: Der Pfeil nach oben im leeren Fragefeld bestand schon (Ä316, nur im Speicher); er liest jetzt den Verlauf von heute und wirkt damit auch nach dem Zurücksetzen und nach einem Neustart.
- Slice 142: Der Chip passte nicht in die alte Zelle (Innenabstand 12/8, Rand 4 ergab 72 px für „09:00–17:00“ plus Chip-Rand); Zelle und Test wurden auf 4/6 und Rand 2 umgestellt. Das Bild der Personenkarte zeigte außerdem, dass die leere Namensspalte die Tage aus dem Bild schob; Tests sahen es nicht.
Slice 141: Nach dem Umbau der Kundenkarte warf das Zurücksetzen des Fokus beim Schließen eines Abschnitts (`FocusCard`) „Sequence contains no matching element“: das erste Feld eines ausgeblendeten Abschnitts hing nicht im visuellen Baum, die Suche nach dem Abschnitt lief ins Leere. Die Suche geht jetzt über den logischen Baum und fällt sonst auf den Öffner zurück.
- Slice 144: Der erste Testlauf der Maske hing 10 Minuten ohne Meldung: Mit Tagesstart als erstem Reiter wartet `EnsureLoadedAsync` auf das Nachfragen der KI, und im Test löst nur ein Testzeitgeber es aus. In der App ruft niemand es wartend auf (alle `_ =`). Hängende Testläufe künftig mit `timeout` starten.
- Slice 143: Der Bildtest der Übersicht endete im Reiter „Bestellen“, weil `Render` das Bild am Ende der Prüfung speichert und der Test vorher den Link anklickte; der Klick-Test hat jetzt einen eigenen Bildnamen (`verwaltung-lager-uebersicht-link`). Das Seitenpanel reservierte 316 px Breite, auch ohne gewählte Zeile; erst das Bild zeigte die leere Fläche rechts neben der Liste.
- Slice 143: Tests, die „Ändern“ oder „Buchen“ in der Bestandszeile klicken, brauchen jetzt zuerst die gewählte Zeile (die Knöpfe stehen im Seitenpanel); `OpenStock` in den Fenstertests öffnet deshalb den Reiter „Bestand“, `OpenStockOverview` lässt die Übersicht stehen.
- Slice 146: Das Nachstellen blieb grün: Der API-Test für „Belege heute und gestern an WIEN-1“ bestand sofort (rund 3,5 Minuten je Einspielzeit, über die Dienste), also fehlte nichts in den Beispieldaten. Erst das Lesen von `PosViewModel.Day.cs` und der Ansicht zeigte die feste Tageswahl „heute“. Ein Rot-Lauf des Desktop-Tests war nicht möglich, weil die neuen Mitglieder erst mit der Änderung kompilieren.
- Slice 147: `EntityHistoryTests.Given_Rezeption_aendert_Telefonnummer…` ist auf `dev` rot, weil die API seit Slice 140 Telefonnummern als „+43 664 …“ speichert und der Test „0664 111 → 0664 222“ erwartet; nicht von Slice 147, für den Rundenabschluss (T-010); behoben im Rundenabschluss (5cb3b73d).
- Rundenabschluss: `TermsAndTooltipTests.Given_ein_fester_Tooltip_der_Windows_App…` war rot: der Tooltip an „+ Wünsche“ (Slice 141) zählte auf („Öl, Druck, Musik, Temperatur oder …“); gekürzt in 5cb3b73d, die Aufzählung steht in der Hilfe (`kunden-kennzahlen`).
- Rundenabschluss: `OnlineRefundTests.Belastete_Ausfallgebuehr_wird_nach_Storno_erstattet` war in `dotnet test --solution` rot (Timeout in `WaitForAsync`; 3887 von 3892 grün, 2 übersprungen), danach zweimal einzeln grün: Last, kein Fehler. ViewTests 1184 von 1184 und die Web-Kette (366 Tests) waren grün.

## Zurückgestellte Kleinigkeiten
- Slice 137 (Review): Zurück vom Reiter „Belege“ auf „Kassieren“ setzt den Fokus nicht ins Suchfeld (nur beim Öffnen der Kassa); Pfeil nach unten führt nicht in die Artikeltreffer, wenn keine Zeile passt (Tab erreicht sie); der Screenreader nennt die Gruppe („Wartet auf Abrechnung“) nicht mit der ersten Zeile; die HTTP-Tests der Tagesliste prüfen keinen zweiten Standort (Mandantenfilter und `LocationId` wie in den übrigen Kassa-Abfragen).
Slice 138: „Zuletzt gefragt“ ist für den Bildschirmleser eine lose Reihe von Knöpfen ohne Listenbezug (Überschrift nicht als Bezeichnung verknüpft); ein Klick auf einen Eintrag während einer laufenden Anfrage setzt nur das Feld, wie die Beispiele; der Ansichtstest zur Seite „Hilfe“ nutzt `ShowPage("hilfe")` statt der offenen `HelpView`.
Ä358 (Slice 138): Die Handbuchbilder `ki-antwort-*.png` und `ki-betrieb-*.png` zeigen die Leiste noch ohne „Neu beginnen“ und ohne „Zuletzt gefragt“; sie sind nicht falsch, aber veraltet und werden beim nächsten Neuerzeugen der Bilder angepasst. – erledigt in T-026 (dev)
Ä359 (Slice 138): „Neu beginnen“ gehört nach dem Wortlaut des Auftraggebers in den Kopf neben das Kreuz; dafür braucht `SidePanelHeader` einen Platz für eine Aktion (und `ISidePanel` eine Angabe dazu), was alle Seitenleisten berührt. – erledigt in T-018 (dev, `SidePanelHeader.Action` und `ISidePanel.PanelAction`)
Ä360 (Slice 138): Der Verlauf wird nur beim Öffnen, Fragen und Abmelden neu bewertet, nicht per Zeitgeber um Mitternacht; bleibt die Leiste über Mitternacht offen, steht die Liste bis zur nächsten Frage oder zum nächsten Öffnen da. – erledigt in T-029 (dev, Zeitgeber der Leiste)
Ä364 (Slice 140): Das Telefon wird erst beim Speichern zu „+43 664 1234567“ (die API entscheidet), nicht schon beim Verlassen des Feldes; die App zeigt das Format danach in der Kundenkarte. Dafür bräuchte die App eine eigene Kopie der Regeln oder einen Aufruf je Feld. – erledigt in T-019 (dev, gemeinsame Regel in `AispaStudio.Contracts`)
Ä365 (Slice 140): Die Kundenmaske erkennt eine gleiche Mailadresse nicht mehr als Dublette (die Suche läuft nur über Nachname und Telefon, wie im Plan); die Buchungsmaske erkennt sie weiter.
Ä366 (Slice 140): Die Handbuchbilder `kunden*.png` zeigen die Maske „Neue Kundin“ noch in der alten Anordnung; sie werden beim nächsten Neuerzeugen der Bilder angepasst (der Text ist angepasst). – geprüft in T-026: kein Hilfebild zeigt die Maske „Neue Kundin“
Ä370 (Slice 142): „Klick auf eine Zelle öffnet das Bearbeiten als Popover an der Zelle“ ist nicht gebaut; das Bearbeiten bleibt unter der Tabelle. Die Fokus- und Tastaturtests des Tageseditors (`ViewTests.RosterDayFocus`) und die Flyout-Fallen (Fokus, DataContext, Headless) machen den Umbau zu einer eigenen Aufgabe. – erledigt in T-017 (dev, Popover im Baum der Ansicht unter der Zelle, `RosterPlanView.PlaceDay`)
Ä371 (Slice 142): Die Warnung „Besetzung unter Mindestbesetzung“ fehlt, weil es keine hinterlegte Mindestbesetzung gibt; sie braucht zuerst eine Einstellung und eine API-Regel (der Plan verbietet beides).
Ä372 (Slice 142): Die aufgehellten Chips sind im dunklen Farbschema hell auf dunklem Grund; lesbar (Kontrast gemessen), aber auffälliger als nötig. Eine dunkle Chip-Variante braucht ein Token-Paar in `Colors.axaml` und `docs/design.md`. **Erledigt in T-039** (06.11.2026): `RosterChipBaseBrush` und `RosterChipInkBrush`, Anteil der Personenfarbe hell 22 %, dunkel 45 %.
Ä367 (Slice 141): Der Verlauf der Kundenkarte zeigt keine Gutscheine und keine „letzten Käufe“ (Artikel); die API liefert dafür keine Daten je Gast, und neue Abfragen gehören nicht in diese Slice. – erledigt in T-020 (dev, `CustomerInsights.Vouchers`/`Purchases`, nur mit dem Recht zu buchen)
Ä368 (Slice 141): Ein Klick auf Telefon oder E-Mail im Kopf kopiert den Wert ohne sichtbare Bestätigung (kein Toast); der Bildschirmleser nennt die Aktion über den Namen „Telefon … kopieren“.
Ä369 (Slice 141): In der Spalte „Verlauf“ heißt der aufklappbare Änderungsverlauf (gemeinsames Element `HistorySectionView`) ebenfalls „Verlauf“; eine eigene Überschrift dafür („Änderungen“) beträfe alle Masken mit diesem Element.
Ä376 (Slice 144): Die Betriebsinfo ist für die Rezeption in der App nicht mehr zu öffnen (nur mit `ViewFinance`, nach der Definition of Done); wer sie dort braucht, fragt den Assistenten. Soll die Rezeption die Einträge der Stufe „Team“ weiter lesen können, braucht der Reiter ein eigenes Recht oder eine Ausnahme – Entscheidung des Auftraggebers. – entschieden in T-025: ja, der Reiter ist für alle sichtbar, die buchen dürfen (Auftraggeber, 05.11.2026; dev)
Ä377 (Slice 144), erledigt in T-037 (Feld `Registers` im Tagesstart, die Regex in der App entfallen): Der Anfangsbestand der Kassa wird aus dem Satz der API gelesen; ändert sich der Wortlaut in `DailyReports`, steht die Zeile als Warnband statt als Wert (der Test `Die_Kassa_zeigt_den_Anfangsbestand_als_Wert…` hält es fest). Ein eigenes Feld im Tagesstart wäre belastbarer, ist aber eine API-Änderung.
Ä378 (Slice 144): Der Kopf mit fünf Reitern, Standort und Datum nebeneinander ist nur bei normaler Fensterbreite geprüft (1440 px im Bildtest); bei schmalem Fenster ist kein Umbruch vorgesehen. – erledigt in T-038 (dev): `ReflowRow` schiebt Standort und Datum in Zeile 2, die Reiter bleiben in einer Zeile
Ä379 (Slice 145): „Auf dieser Seite“ steht ab 1180 px Fensterbreite rechts, darunter als „Inhalt“ über dem Text. Die Grenze ist geschätzt und nur in jsdom (gestubbtes `matchMedia`) geprüft, nicht im echten WebView2 mit offenem Panel; die Breite des Browsers im Panel ist nicht gemessen.
Ä380 (Slice 145): „Erste Schritte“ je Rolle steht nicht mehr in der Navigation (jedes Thema nur einmal), sondern auf der Startseite der Hilfe (ohne Thema). Wer mit einem Thema einsteigt, sieht sie nicht; der Rollenfilter ersetzt sie dort. – erledigt in T-028 (dev): „Erste Schritte“ ist die erste Gruppe der Navigation, mit Rolle nur deren Einstiege, unter „Alle“ alle Einstiege je einmal; diese Themen stehen nicht noch einmal in ihrem Bereich (Auftraggeber, 05.11.2026)
Ä381 (Slice 145): Die Schublade in der Verwaltung benutzt weiter die alte Übersicht (`HelpIndex` ohne `page`); nur die Seite `/help/` hat das neue Layout.
Ä373 (Slice 143): Die Bedarfsprüfung lädt weiter mit dem Recht „Book“, steht aber nur im Reiter „Bestellen“ (ManageLocation); eine Rezeption ohne Lagerrecht fragt die API also für eine Anzeige an, die sie nie sieht. Ändern würde die Rechte-Tests der Slice 123 umdrehen; besser zusammen mit der Frage, ob die Rezeption die Bedarfsprüfung sehen soll. – erledigt in T-025 (dev): die Rezeption öffnet „Bestellen“ und sieht dort nur die Bedarfsprüfung; die Vorgabe aus Slice 143 „Rezeption sieht Bestellen nicht“ ist aufgehoben (Auftraggeber, 05.11.2026)
Ä374 (Slice 143): Bei einem gewählten Lagerort fehlt die Spalte „Reicht für“ (leer), weil `StoragePlaceLevelDto` keinen Richtwert liefert; der Chip „nachbestellen“ hängt dort nur am Mindestbestand. Braucht ein zusätzliches Feld in der API (kein Breaking Change nach ADR 0085). – erledigt in T-021 (dev, Feld `TreatmentsCovered` am Lagerort)
Ä375 (Slice 143): Die Schwelle „nur noch für wenige Behandlungen“ ist fest auf höchstens 5 Behandlungen gesetzt (`StockOverviewViewModel.FewTreatments`); der Plan nennt keinen Wert und keine Einstellung. Eine Einstellung je Material oder Standort wäre ein eigener Auftrag.
- Slice 146 (Ä382): Das Handbuchbild `kassa-belege.png` zeigt den Kopf der Belegliste noch ohne Pfeile; es wurde in dieser Slice nicht neu erzeugt (kein Bildlauf, keine vollen Suiten). – erledigt in T-026 (dev)
Ä385 (Slice 147): „schick … den Bogen“ öffnet den Termin, scrollt aber nicht zum Abschnitt Anamnese und wählt keinen Weg (Tablet, Papier, Foto) vor; dafür bräuchte das Termin-Panel ein Sprungziel. – erledigt in T-022 (dev, Sprung zum Abschnitt; ein Weg wird bewusst nicht vorgewählt)
Ä386 (Slice 147): Aufträge erkennt der Assistent nur am Satzanfang („kannst du steiner buchen“ bleibt eine Frage); eine Erkennung mitten im Satz bräuchte Claude oder mehr Regeln und weitere Gegenproben gegen Hilfefragen.
Ä387 (Slice 147): Der Hinweis auf den KI-Vorschlag ist nicht im Bild geprüft (kein Bildtest für „Neuer Termin“ mit Hinweis); beim nächsten Neuerzeugen der Handbuchbilder ein Bild `ki-auftrag-buchen` ergänzen. – erledigt in T-026 (dev, eigener Bildtest)

## Offene Entscheidungen
NEEDS-DECISION: Bestätigt die Datenschutz-Beratung, dass alle Rollen mit Kundenzugriff (auch die Rezeption) die Anamnese lesen und fotografierte Bögen prüfen dürfen (Ä309, Auftraggeber 02.11.2026)? (from R05) – entschieden in T-013: ja (Auftraggeber, 05.11.2026)
NEEDS-DECISION: Passt es, dass Slice 93 nur drei Bausteine baut, „Liste mit Detail“ erst mit Slice 94 entsteht und „Feld mit Fragezeichen“ entfällt? (from R05) – entschieden in T-014: ja (Auftraggeber, 05.11.2026); „Liste mit Detail“ bleibt nach Slice 94 (Annahme 7) ohne eigenen Baustein, gemeinsam sind `INativeArea`, `UnsavedChanges` und `SaveBar`
NEEDS-DECISION: Bleibt die Stempeluhr in der Handy-App, obwohl E6 „keine Zeiterfassung per Handy“ nennt? Empfehlung: lassen, bis feststeht, ob E6 auch den Bestand meint. (from R05) – entschieden in T-015: keine Stempeluhr; Arbeitszeiten trägt die Leitung ein (Auftraggeber, 05.11.2026; dev)

## Manuelle Testliste
### Slice 137 – Kassa: „Heute abzurechnen“ als Hauptliste
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und in der Navigation **Kassa** öffnen
1. Die Kassa ansehen, ohne etwas anzuklicken.
Expected: Links steht „Heute abzurechnen“ mit dem Cursor im Suchfeld „Gast, Code oder Artikel“, darunter „Wartet auf Abrechnung“ und „Später heute“ mit ungekürzten Namen und Behandlungen, „Erledigt (n)“ zugeklappt; „Artikel und Gutscheine“ ist eine schmale Leiste neben der Rechnung.
2. Den Vornamen eines wartenden Gastes tippen, **Pfeil nach unten** und **Enter** drücken.
Expected: Die Liste zeigt nur noch passende Zeilen; nach Enter steht der Termin auf der offenen Rechnung.
3. „Verleih“ ins Suchfeld tippen.
Expected: Unter „Artikel“ stehen die passenden Artikel; ein Klick setzt den Artikel auf die Rechnung.
4. Rechts oben „KI und Hilfe“ öffnen.
Expected: Die Liste bleibt mindestens so breit wie vorher (380 px); bei einem Bildschirm mit 1280 px steht „Bezahlen“ unter der Rechnung, die Rechnung bleibt lesbar hoch, und die Seite rollt bis **Bezahlen und Beleg erstellen**.
5. Die Leiste „Artikel und Gutscheine“ anklicken, die App schließen und wieder öffnen.
Expected: Das Panel ist aufgeklappt und bleibt es nach dem Neustart.
6. Einen Termin mit bezahlter Anzahlung (Kalender, Termin mit Online-Anzahlung) in der Liste suchen.
Expected: Der Termin steht unter „Wartet auf Abrechnung“ oder „Später heute“; auf der Rechnung wird die Anzahlung abgezogen.
Closes: Ä344, Ä345, Ä355
Result: ☐

### Slice 138 – KI und Hilfe: leer nach dem Schließen, „Zuletzt gefragt“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden und „KI und Hilfe“ mit Strg+Leertaste öffnen
1. „wer ist heute noch frei“ fragen, dann mit Esc schließen und mit Strg+Leertaste wieder öffnen.
Expected: Das Fragefeld ist leer und hat den Fokus, keine alte Antwort steht da, die Erklärung zur aktuellen Seite ist zu sehen, unter dem Fragefeld steht „Zuletzt gefragt“ mit „wer ist heute noch frei“.
2. Die Frage unter „Zuletzt gefragt“ anklicken.
Expected: Der Assistent antwortet neu (frische Antwort, nicht die vorige); die Frage steht oben in der Liste.
3. Mit dem Kreuz schließen, wieder öffnen, bei leerem Fragefeld die Taste Pfeil nach oben drücken.
Expected: Die letzte Frage steht im Feld.
4. Eine Frage stellen und „Neu beginnen“ in der Zeile „Assistent“ anklicken.
Expected: Antwort und Feld sind leer, die Leiste bleibt offen, der Fokus steht im Fragefeld; „Neu beginnen“ ist danach nicht mehr zu sehen.
5. Dieselbe Frage mit anderer Schreibweise noch einmal stellen und mehr als fünf verschiedene Fragen stellen.
Expected: Doppelte stehen nur einmal da und rücken nach oben; es bleiben höchstens fünf Fragen.
6. Auf der Seite „Hilfe“ „KI und Hilfe“ öffnen.
Expected: Die Leiste zeigt nur den Assistenten, nicht „Zu jeder Einstellung …“ und nicht „Alle Hilfethemen“.
7. Abmelden, als Inhaberin anmelden („KI und Hilfe“ öffnen), dann wieder als Rezeption anmelden.
Expected: Als Inhaberin ist „Zuletzt gefragt“ leer, als Rezeption nach dem Abmelden ebenfalls (der Verlauf wird beim Abmelden gelöscht).
Closes: Ä347
Result: ☐

### Slice 139 – Assistent: Ranglisten ohne Wiederholung und mit Gleichstand
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und „KI und Hilfe“ mit Strg+Leertaste öffnen
1. „top behandlungen diesen monat“ fragen.
Expected: Der Kopfsatz nennt bei gleichen Werten Gruppen („7 Behandlungen mit je 2 Terminen, danach 3 mit je 1 Termin“) statt aller Namen; in der Tabelle tragen gleiche Werte denselben Platz (1, 1, 1 … dann 8), nicht 1 bis 7.
2. Mit Beispieldaten, in denen die Termine verschieden sind (z. B. „top behandlungen seit 1.1.“), dieselbe Frage stellen.
Expected: Der Kopfsatz nennt die ersten drei mit Platz und Zahl („1. Massage (12), 2. …“), die Tabelle trägt die Plätze 1, 2, 3 …
3. „top kunden diesen monat“ fragen.
Expected: Gleich viele Besuche (oder gleiche Ausgaben) ergeben ebenfalls Gruppen im Kopfsatz und denselben Platz in der Tabelle.
4. „welche artikel verkaufen sich am besten“ fragen.
Expected: Es gibt keine Artikel-Rangliste (nicht Teil dieser Aufgabe); der Assistent antwortet wie bisher ohne eine Liste mit Plätzen.
Closes: Ä346
Result: ☐

### Slice 140 – Neue Kundin: Name und Telefon zuerst, Dubletten beim Tippen
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden und in der Navigation **Kunden** öffnen
1. Oben **Neue Kundin / neuer Kunde** anklicken, ohne etwas anderes anzuklicken.
Expected: Die Maske zeigt Vorname, Nachname, Telefon und E-Mail; der Cursor steht im Vornamen; „Weitere Angaben“ ist zu, darunter stehen „Anlegen und Termin buchen“ (Hauptknopf), „Anlegen“ und „Abbrechen“; der Hinweis lautet „Nur ein Name ist Pflicht.“
2. Nur mit der Tastatur: „Mia“, Tab, „Teststein“, Tab, „0664 1234567“, Enter.
Expected: Die Kundin ist angelegt, ihre Karte ist offen, das Telefon steht als „+43 664 1234567“.
3. Neue Kundin öffnen, im Nachnamen „Huber“ tippen (eine Kundin „Huber“ muss es geben).
Expected: Nach kurzer Pause erscheint „Schon vorhanden?“ mit „Huber, <Vorname>“ und der Nummer; **Diese Kundin nehmen** öffnet die vorhandene Karte, es wird nichts angelegt.
4. Neue Kundin öffnen, im Telefon eine vorhandene Nummer in anderer Schreibweise tippen (z. B. „0664 …“ statt „+43 664 …“).
Expected: „Schon vorhanden?“ zeigt die Kundin mit dieser Nummer.
5. Esc drücken.
Expected: Die Maske schließt ohne Anlage; beim nächsten Öffnen ist sie leer.
6. Im Suchfeld links „Gruber“ tippen (niemand dieses Namens).
Expected: Unter dem Suchfeld stehen „Keine Kundin gefunden.“ und **Neue Kundin „Gruber“ anlegen**; ein Klick öffnet die Maske mit „Gruber“ im Nachnamen.
7. Vorname und Nachname ausfüllen und **Anlegen und Termin buchen** wählen.
Expected: Die Kundin ist angelegt, der Kalender öffnet die Buchung mit ihr als Gast.
8. Neue Kundin öffnen, nichts eintragen, auf das ausgegraute „Anlegen“ zeigen.
Expected: Der Hinweis nennt den Grund („Verfügbar, sobald ein Vor- oder Nachname eingetragen ist.“).
9. **Weitere Angaben** aufklappen.
Expected: Anrede, Titel vorangestellt, Rufname im Haus und Notiz erscheinen.
Closes: Ä348
Result: ☐

### Slice 141 – Kundenkarte: ruhiger Kopf, „Gut zu wissen“ und Verlauf
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden und in der Navigation **Kunden** öffnen
1. Eine Stammkundin mit Wünschen öffnen (z. B. Helga aus den Beispieldaten).
Expected: Oben stehen Name, Chip „★★ Stammgast“, Anrede, Telefon und E-Mail (ein Klick kopiert den Wert), rechts **Termin buchen** als einziger Hauptknopf und **…**; darunter eine Zeile mit Besuchen, „zuletzt am …“ und Umsatz, Warnungen (nicht erschienen, späte Absage) in Warnfarbe mit Dreieck; links „Gut zu wissen“ mit Wünschen als Chips, rechts „Verlauf“ mit „Demnächst“ und „Zuletzt“. Werte stehen in kräftiger Schrift, Bezeichnungen grau.
2. Mit der Maus über den Abschnitt „Wünsche“ fahren.
Expected: Erst jetzt erscheint rechts der Stift; ein Klick öffnet die Bearbeitung, der Cursor steht im ersten Feld.
3. Einen Wunsch ändern, speichern; danach erneut öffnen und mit Esc schließen.
Expected: Der Chip zeigt den neuen Wunsch; nach Esc ist nichts gespeichert, und der Fokus liegt wieder auf dem Stift.
4. Einen Gast ohne Wünsche, Kennzeichen, Vermerk und Lieblingstermine öffnen (z. B. eine eben angelegte Kundin).
Expected: Es gibt keine leeren Abschnitte; „Noch nicht erfasst“ zeigt **+ Wünsche**, **+ Kennzeichen**, **+ Rezeptionsvermerk**, **+ Termine am liebsten**; „Nächster Termin: keiner“ mit **buchen**.
5. **+ Kennzeichen** wählen, dann Esc.
Expected: Der Abschnitt „Kennzeichen“ öffnet mit dem Fokus im Feld; nach Esc verschwindet er wieder, der Fokus liegt auf „+ Kennzeichen“.
6. Als Rezeption das Menü **…** öffnen.
Expected: „Bearbeiten (Name, Anrede, Kontakt, Notiz)“, „Weitere E-Mail-Adresse“, „Rechnungsempfänger“, „Stammrabatt“; kein „Leitung informieren …“ und kein „Nicht mehr buchen …“.
7. Abmelden, als Inhaberin anmelden, dieselbe Karte öffnen und **…** öffnen.
Expected: Zusätzlich stehen „Leitung informieren …“ und „Nicht mehr buchen …“ unter einem Trennstrich.
8. Eine Kundin mit eingechecktem Termin heute öffnen.
Expected: Neben **Termin buchen** steht **Kassieren**; ein Klick öffnet die Kassa mit diesem Termin.
9. Im Kopf auf das Telefon klicken und in ein anderes Programm einfügen.
Expected: Eingefügt wird die Nummer.
Closes: Ä349
Result: ☐

### Slice 142 – Dienstplan: lesbare Woche mit Schicht-Chips
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und **Verwaltung** › **Team** › **Wochenplan** öffnen
1. Für Lena Huber am Montag einen Dienst lassen, für Dienstag bis Freitag Urlaub eintragen (Zelle anklicken, **Urlaub**, **Speichern**, jeweils ohne Haken bei „Sofort gültig“).
Expected: Von Dienstag bis Freitag liegt ein durchgehender Balken „Urlaub Di–Fr“ über den Zellen; Montag zeigt den Dienst als gefüllten Chip in Lenas Farbe mit dunkler Zeit; neben ihrem Namen steht „8 / 40 h“ (bei einer Wochenarbeitszeit von 40 Stunden).
2. Auf die Mitte des Balkens, über dem Mittwoch, klicken.
Expected: Das Bearbeiten öffnet sich unter der Tabelle für Mittwoch (nicht für den ersten Tag des Balkens).
3. Oben auf das Band achten.
Expected: „Entwurf: 4 Änderungen nicht veröffentlicht“ mit **Veröffentlichen** und **Verwerfen**; jede geänderte Zelle trägt einen gestrichelten Rand; eine Legendenzeile gibt es nicht.
4. In der Woche, die heute enthält, die Spalte von heute ansehen.
Expected: Kopf und Zellen von heute sind hervorgehoben (Kopf in der Hauptfarbe, Zellen hinterlegt); in einer anderen Woche ist nichts hervorgehoben.
5. Unter die Tabelle schauen.
Expected: Die Zeile „Besetzung“ nennt je Tag, wie viele Personen am Standort im Dienst sind; ein freier Tag ohne Eintrag zeigt ein schwaches „frei“ statt eines leeren Kastens.
6. **Veröffentlichen** wählen.
Expected: Das Band verschwindet, die gestrichelten Ränder auch, die Meldung nennt die veröffentlichten Tage.
7. Als Rezeption anmelden und denselben Wochenplan öffnen.
Expected: Dieselbe Woche ist zu sehen; ein Klick auf eine Zelle öffnet kein Bearbeiten, es gibt kein Band und kein Veröffentlichen.
8. Zum hellen und zum dunklen Farbschema wechseln.
Expected: Die Zeit im Chip und der Text im Balken bleiben in beiden gut lesbar.
Closes: Ä350
Result: ☐

### Slice 143 – Lager: Unterreiter und „Was ist zu tun“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und **Verwaltung** › **Lager** öffnen
1. Die Reiterleiste ansehen.
Expected: Links stehen **Übersicht**, **Bestand**, **Bestellen**, **Chargen**, **Bewegungen**, nach einem feinen Trenner **Inventur**, **Lagerorte**, **Lieferanten** und **Verbrauch**; das Lager öffnet in der **Übersicht**.
2. In der Übersicht „Was ist zu tun“ lesen (Beispieldaten: ein Bestellvorschlag, ein Material unter dem Mindestbestand, eine ablaufende Charge).
Expected: Je Punkt eine Zeile mit Zahl, Einzelheiten in kleiner Schrift und einem Link („Bestellen öffnen“, „Im Bestand ansehen“, „Chargen öffnen“); darunter klein „Lagerwert … am Standort“; kein Kennzahlen-Kasten.
3. **Bestellen öffnen** wählen.
Expected: Der Reiter **Bestellen** öffnet sich: oben **Bedarfsprüfung** mit **Zeitraum**, Liste und – falls es Hinweise gibt – eine einzeilige Zeile „n Hinweise zur Bedarfsprüfung“ (aufklappbar, keine gelbe Textwand), darunter der Bestellvorschlag mit **Bestellung anlegen**.
4. **Bestellung anlegen** wählen.
Expected: Je Lieferant entsteht ein Entwurf in der Liste darunter; die Zahl der Positionen in der Übersicht ändert sich nach dem Neuladen nicht von allein.
5. Zurück in die **Übersicht**, **Im Bestand ansehen** beim Material unter dem Mindestbestand wählen.
Expected: Der **Bestand** zeigt vier Spalten (Material, Bestand mit Balken, Reicht für, Status); das Material mit zu wenig Bestand trägt den Chip „nachbestellen“, der Balken ist warnfarben und kürzer als halb; „Nur unter Minimum“ ist gesetzt.
6. Eine Zeile anklicken.
Expected: Rechts öffnet das Seitenpanel mit Einheit, Mindestbestand, Verbrauch geplant, Bestellt, Bestellvorschlag, Läuft ab, Wert und den Lagerorten sowie **Buchen**, **Nachfüllen** (bei gewähltem Lagerort) und **Ändern**; das Kreuz schließt es.
7. In das Feld **Material suchen oder scannen** „tuch“ tippen, dann einen Materialcode scannen oder eintippen und Enter drücken.
Expected: Beim Tippen filtert die Liste nach dem Namen; nach Enter ist die Zeile des Materials gewählt (Seitenpanel offen), mit Buchungsrecht öffnet zugleich der Wareneingang mit dem Cursor in **Menge**; das Feld ist danach leer.
8. **Chargen** und **Bewegungen** öffnen.
Expected: **Chargen** zeigt bald ablaufende Chargen, die Abverkaufsliste und Abgelaufenes zum Ausbuchen; die Zahl im Reiter nennt die offenen Chargen. **Bewegungen** zeigt die Bewegungen und darunter die Rückverfolgung einer Charge wie bisher.
9. Als Rezeption anmelden und **Verwaltung** › **Lager** öffnen.
Expected: Zu sehen sind **Übersicht**, **Bestand** und **Bewegungen**; **Bestellen**, **Chargen**, **Inventur** und die übrigen Verwaltungsreiter und der Trenner fehlen; in der Übersicht steht kein Punkt „nachbestellen“ und der Punkt zu ablaufenden Chargen hat keinen Link; **Bewegung buchen** funktioniert.
10. Zum hellen und zum dunklen Farbschema wechseln.
Expected: Zahlen, Chips, Balken und Seitenpanel bleiben in beiden gut lesbar.
Closes: Ä351
Result: ☐

### Slice 144 – Zahlen: eine Reiterebene, Tagesstart als Übersicht
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und **Verwaltung** › **Zahlen** öffnen
1. Die Reiter ansehen.
Expected: Genau eine Reihe Reiter (Tagesstart, Mein Tag, Hygiene, Berichte, Betriebsinfo), Tagesstart ist gewählt; Standort und Datum stehen rechts im Kopf auf der Höhe der Reiter; darunter gibt es keine zweite Reiterreihe.
2. Den Tagesstart lesen.
Expected: Zuerst ein Satz: „Nichts Dringendes offen“ (Erfolg) oder „n Punkte brauchen Aufmerksamkeit“ (Warnung) mit den Punkten darunter. Dann eine Zeile mit Behandlungen, Auslastung, Im Dienst und Abwesend, die Werte groß und dunkel, die Bezeichnungen klein darunter. Das Personal steht als Leiste von der Öffnung bis zum Schluss, die Dienstzeiten daneben; Gäste, Räume und Handbuch stehen als Abschnitte mit feinen Linien ohne gerahmte Karten.
3. Für eine Person Urlaub im Dienstplan eintragen (Verwaltung › Team › Wochenplan) und den Tagesstart für diesen Tag öffnen.
Expected: Die Person steht in „Personal“ mit der Pille „Urlaub“ statt der Leiste, und „Abwesend“ zählt sie mit.
4. Das Datum und den Standort wechseln.
Expected: Status, Werte und Personal laden neu, es bleiben keine alten Zahlen stehen.
5. In der Kassa einen Tagesabschluss lassen und die Kassa im Tagesstart ansehen.
Expected: Der Anfangsbestand steht als Wert („Kassa AT1: Anfangsbestand 250,00 €“); eine Warnung der Kasse (Signatur) steht darunter als Warnband.
6. **Mein Tag** wählen, eine Vorbereitung abhaken und eine Notiz speichern.
Expected: Oben der Status („Keine Behandlungen heute“, „Nichts Dringendes offen“ oder die Hinweise) und die Werte Behandlungen, Vorbereitung offen, Dienst; die Termine stehen als Abschnitte; Abhaken zählt „Vorbereitung offen“ herunter.
7. **Hygiene** wählen und einen Punkt abhaken.
Expected: Oben „Alles erledigt“ oder „n Punkte offen, m überfällig“ und die Werte Räume, Offen, Überfällig, Abgenommen; das Abhaken ändert die Zahlen, der Fokus bleibt auf der Zeile.
8. **Berichte** wählen und zwischen **Management** und **Pflichtberichte** wechseln.
Expected: Die Unterreiter stehen rechts über dem Inhalt; bei Management ersetzen Von und Bis das Datum; die Berichte sehen aus wie vorher.
9. **Betriebsinfo** wählen, **Neuer Eintrag** wählen, eine Bezeichnung tippen und in eine andere Maske der Verwaltung wechseln.
Expected: Standort und Datum verschwinden bei der Betriebsinfo; beim Wechsel fragt die App nach den ungespeicherten Änderungen.
10. Als Rezeption anmelden und **Verwaltung** › **Zahlen** öffnen.
Expected: Nur Tagesstart, Mein Tag und Hygiene; **Berichte** und **Betriebsinfo** fehlen.
11. Als Inhaberin im Assistenten nach dem Installateur fragen und **Betriebsinformationen öffnen** wählen.
Expected: „Zahlen“ öffnet beim Reiter **Betriebsinfo**.
12. Zum hellen und zum dunklen Farbschema wechseln.
Expected: Werte, Bezeichnungen, die Pille „Urlaub“ und die Leiste bleiben in beiden gut lesbar.
Closes: Ä352
Result: ☐

### Slice 145 – Hilfe: ruhiges Layout mit Bereichen und Inhaltsspalte
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten und als Rezeption anmelden; die Hilfe öffnet mit dem Fragezeichen neben einer Einstellung oder über **KI und Hilfe** (alternativ `/help/?rolle=rezeption` im Browser)
1. Die Hilfe ohne Thema öffnen.
Expected: Links (etwa 280 px) Suche, der Chip „Meine Rolle: Rezeption“ und eingeklappte Gruppen nach Bereich (Kalender, Kassa, Kunden zuerst); rechts die Startseite mit „Erste Schritte“ je Rolle.
2. Den Chip „Meine Rolle: Rezeption“ wählen.
Expected: Er heißt „Alle“, und die Gruppen zeigen auch die Themen, die nur Leitung oder Inhaberin brauchen; ein weiterer Klick stellt den Filter zurück.
3. Die Gruppe **Kalender** öffnen und **Status setzen** wählen.
Expected: Das Thema steht genau einmal in der Navigation (seit T-028 unter „Erste Schritte“, weil es ein Einstieg der Rezeption ist), der aktive Eintrag ist eine Pille ohne Unterstreichung, die Gruppe bleibt offen.
4. Im Thema den Text bei voller Breite (1920 px) lesen.
Expected: Brotkrumen als kleiner schlichter Text, Titel, Rollen als Chip, der Text höchstens etwa 720 px breit; „Auf dieser Seite“ steht rechts neben dem Text und läuft beim Scrollen mit, der gelesene Abschnitt ist markiert; Schritte sind nummerierte Blöcke, Bilder haben runde Ecken, Links im Text sind unterstrichen, in Navigation und Verzeichnis nicht.
5. Dasselbe Thema bei 1280 px Fensterbreite mit offenem Panel „KI und Hilfe“ öffnen.
Expected: Statt der rechten Spalte steht ein zugeklapptes „Inhalt“ über dem Text; aufgeklappt führen die Einträge zum Abschnitt.
6. Nach „Termin buchen“ und „Gutschein“ suchen und je einen Treffer lesen, einmal als Rezeption und einmal als Inhaberin.
Expected: Die Treffer öffnen das Thema, die Navigation und der Text scrollen getrennt, Zurück und Vor im Browser gehen durch die Themen.
Closes: Ä354
Result: ☐

### Slice 146 – Kassa: der Reiter „Belege“ zeigt die Belege der Beispieldaten
Start: Beispieldaten neu laden (`docker compose down -v`), `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden und **Kassa** › **Belege** öffnen
1. Die Überschrift über der Belegliste und die beiden Pfeile daneben ansehen.
Expected: Die Überschrift lautet „Belege heute“; die Liste zeigt die Belege von heute bis jetzt, neueste zuerst, der neueste ist gewählt und rechts zu sehen (hat die Kassa heute noch nicht kassiert, steht „Heute noch keine Belege an dieser Kasse.“); der Pfeil **Tag danach** ist grau.
2. **Tag davor** wählen.
Expected: Die Überschrift lautet „Belege gestern“; die Liste zeigt die Belege von gestern (viele, mit Karte, bar und geteilter Zahlung), der neueste ist gewählt; **Tag danach** ist jetzt anklickbar.
3. Noch zweimal **Tag davor** wählen.
Expected: Die Überschrift nennt den Tag mit Wochentag und Datum („Belege Samstag, 17. Oktober 2026“); an einem Öffnungstag stehen Belege in der Liste, an einem Ruhetag „An diesem Tag gab es keine Belege an dieser Kasse.“.
4. Mit **Tag danach** zurück bis heute gehen.
Expected: Bei jedem Schritt wechselt die Liste mit; an heute ist **Tag danach** wieder grau.
5. Auf einen Vortag gehen und oben die Kasse eines anderen Standorts wählen.
Expected: Der Vortag bleibt gewählt und die Liste zeigt die Belege der anderen Kasse an diesem Tag.
6. Zu **Kassieren** wechseln und wieder **Belege** öffnen.
Expected: Die Überschrift lautet wieder „Belege heute“.
7. Zum hellen und zum dunklen Farbschema wechseln.
Expected: Überschrift und Pfeile bleiben in beiden gut lesbar; die Pfeile haben einen Hinweis („Tag davor“, „Tag danach“) und sind mit der Tabulatortaste erreichbar.
Closes: Ä313
Result: ☐

### Slice 147 – Assistent: Aufträge öffnen die vorausgefüllte Maske
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kalender** öffnen und „KI und Hilfe“ (Strg+Leertaste) aufklappen
1. „buche steiner morgen 10 uhr massage“ fragen (bei mehreren Steiners einen wählen).
Expected: Die Antwort nennt Gast, Behandlung und Zeit, dass erst mit „Termin buchen“ gebucht wird, und zeigt einen Knopf „Neuer Termin …“; im Kalender ist noch nichts gebucht.
2. Den Knopf wählen, dann **Abbrechen**.
Expected: „Neuer Termin“ öffnet sich am morgigen Tag mit Gast, Massage und Beginn 10:00 und oben dem Hinweis „Vorschlag des KI-Assistenten, bitte prüfen …“; nach **Abbrechen** ist kein Termin entstanden.
3. Schritt 1 und 2 wiederholen und diesmal **Termin buchen** wählen.
Expected: Der Termin steht im Kalender.
4. „verschiebe den termin von steiner auf übermorgen 14 uhr“ fragen und den Knopf wählen; einmal schließen, einmal **Verschieben**.
Expected: Der Termin öffnet sich mit dem Hinweis oben, „Verschieben“ ist offen mit Datum und 14:00; erst **Verschieben** verlegt den Termin.
5. „sag den termin von steiner ab“ fragen und den Knopf wählen; einmal **Abbrechen**, einmal **Stornieren**.
Expected: Die Rückfrage „Termin stornieren?“ nennt die Gebühr nach der Stornoregel; nach **Abbrechen** bleibt der Termin, nach **Stornieren** ist er storniert.
6. „schick steiner den bogen“ fragen (Gast mit Termin) und den Knopf wählen.
Expected: Der Termin öffnet sich mit dem Hinweis oben; im Abschnitt Anamnese stehen Tablet, Papier und Foto; gesendet wird erst mit dem Knopf dort.
7. „offene rechnung <Gast mit offener Rechnung>“ fragen und den Knopf wählen; einmal zurück, einmal kassieren.
Expected: Die Kassa öffnet mit dieser Rechnung; ohne Kassieren bleibt sie offen.
8. Als Therapie anmelden und „buche steiner morgen 10 uhr massage“ fragen.
Expected: Die Antwort sagt, dass das Recht „Buchen“ fehlt; es gibt keinen Knopf.
Closes: Ä319
Result: ☐

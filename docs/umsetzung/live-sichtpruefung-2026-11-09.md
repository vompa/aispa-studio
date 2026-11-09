# Live-Sichtprüfung der Windows-App aus Sicht der Rezeption (09.11.2026)

Stand: Branch `claude/live-sichtpruefung-fe3810` (Inhalt von `dev` mit den Rezeptions-Lücken A bis H und dem Live-Tester). Es wurde nichts in `src/` geändert; die Befunde sind Vorschläge, umgesetzt wird erst nach Durchsicht durch den Auftraggeber. Regeln: [texte.md](../gestaltung/texte.md), [meldungen.md](../gestaltung/meldungen.md). Belegbilder (verkleinert): [live-sichtpruefung/](live-sichtpruefung/).

## 1. Lauf

**Umgebung.** Die Windows-App (Release) lief gegen einen eigenen Docker-Stack aus dem Worktree (`docker compose -p aispa-sicht`, API auf Port 5186, frische Demo-Daten), nicht gegen den AppHost: Auf Port 5086 lief schon der Stack einer anderen Sitzung (gleicher `src/`-Stand), und dessen Datenbank neu zu laden war nicht freigegeben. Tempo `-Speed fast`, Rolle Rezeption, angemeldet als Nina Kovač.

**Ergebnis nach Themen** (31 Themen, Schlüssel nach [Arbeitsweise](../arbeitsweise.md)):

| Stand | Themen |
| --- | --- |
| grün, Bilder gesichtet (28) | `signin`, `signin-other-account`, `overview`, `customer-search`, `customer-search-alias`, `customer`, `customer-duplicate`, `customer-minimal`, `customer-empty`, `customer-phone-format`, `customer-badmail`, `appointment`, `appointment-conflict`, `appointment-closed`, `checkin`, `checkin-late`, `checkin-undo-late`, `checkin-noshow`, `reschedule`, `reschedule-past`, `reschedule-checked-in`, `cancel`, `cancel-restore`, `cancel-esc`, `cancel-no-fee`, `checkout`, `receipt-void-empty-reason`, `day-closing` |
| nicht durchgelaufen (3) | `appointment-past` (Tester-Annahme „gestern hat Dienst“ stimmt am Montag nicht, gestern war Sonntag), `voucher` und `voucher-invalid` (Ursache siehe T5) |

`sick-leave` ist kein Thema des Testers und wurde nicht live gespielt (nur Quelltext, Abschnitt 3). „Bilder gesichtet“ heißt: die Bilder der Dialoge, Meldungen und Fehlerbilder wurden einzeln angesehen; die übrigen Schrittbilder (rund 600) wurden anhand der grünen Prüfungen und Stichproben nicht einzeln gesichtet.

**Dauer.** Ein Lauf über 17 Themen (79 Schritte) dauerte reine Schrittzeit 339 s (rund 6 Minuten mit Start und Anmeldung), 61 Schritte (12 Themen) 234 bis 245 s, ein Einzelthema 30 bis 110 s. Gesamt waren 28 Anläufe nötig (Bericht je Lauf unter `.test-logs/live/`, nicht im Repository), weil der Tester beim ersten roten Schritt endet.

**Rote Schritte und Ursache** (alle waren Tester- oder Datenfehler, kein roter Schritt zeigte einen Fehler der App):

| Schritt | Ursache | Wiederholt |
| --- | --- | --- |
| „Abgemeldet beginnen“ (2 Läufe) | Erststart: „Abmelden“ nicht gefunden bzw. `NoClickablePoint`, die App war noch nicht gezeichnet | ja, danach grün |
| „Erste freie Zeit buchen“ (mehrere Läufe) | Suche nach der Meldung mit Name „Meldung“; die App meldet den Text selbst als Name (T1) | ja; Suche angepasst |
| „Einchecken“, „Termin stornieren …“ (je mehrere Läufe) | Mausklick auf einen Knopf im frisch geöffneten Seitenpanel kam nicht an, nur Hover (Befund B8) | ja; Knöpfe lösen jetzt per UI Automation aus |
| „Karte … nicht gefunden“ nach `appointment-closed` | Esc auf „Neuer Termin“ öffnet „Änderungen verwerfen?“, der Tester antwortete nicht (T2) | ja; Tester antwortet „Verwerfen“ |
| „Freie Stelle von gestern“ | gestern war Sonntag, keine Karte (T4) | nicht behoben |
| „Wert“, „Betrag“, „Gezählter Bestand“ | Feldnamen in der Kassa heißen jetzt „… (EUR)“, „Gezählter Bestand (EUR)“ ist Pflicht (T6) | ja; Namen angepasst |
| `voucher`, `voucher-invalid` | T5 | nicht behoben |
| „Einchecken geht nur am Tag des Termins …“ | Maniküre war nach drei Läufen heute ausgebucht, der Lauf buchte auf Donnerstag (T9) | Datenbank des eigenen Stacks neu geladen |

## 2. Befunde der App

Schwere: **hoch** Fehler oder Fehlbedienung am Gast, **mittel** wöchentlich störend, **niedrig** Komfort. „Beleg“ nennt das Bild oder die Quelle; „live“ heißt: im Lauf gesehen, „Quelltext“: nur gelesen.

| Nr | Stelle | Beleg | Ist | Soll (Regel) | Schwere | Vorschlag |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | Kassa, Reiter „Kassenlade & Abschluss“, Thema `day-closing` | live, [b08](live-sichtpruefung/b08-tagesabschluss-vorher.jpg), [b07](live-sichtpruefung/b07-tagesabschluss-differenz.jpg) | „Tagesabschluss durchführen“ speichert sofort und unveränderlich („Der Abschluss wird unveränderlich gespeichert“), auch bei einem Zählbestand von 800,00 € gegen einen Soll-Bestand von 974,20 €. Danach steht „Differenz −174,20 €“, ohne Rückfrage, ohne Pflicht zur Notiz. | Nicht umkehrbare Aktion: Rückfrage mit Frage als Titel, Folge zuerst, Verb als Antwort (meldungen.md Abschnitt 2 „Dialog“, Entscheidungsfolge; texte.md). Bei einer Differenz Hinweis am Objekt (M3) und Notiz verlangen. | hoch | Rückfrage „Tagesabschluss durchführen?“ mit „Der Abschluss lässt sich nicht mehr ändern. Differenz zum Soll: −174,20 €.“, Antworten „Abschließen“ und „Abbrechen“; ab Differenz ungleich 0 die Notiz verpflichtend. |
| B2 | Kassa, Liste „Heute abzurechnen“, Klick auf einen noch nicht eingecheckten Termin | Quelltext `PosViewModel.cs` 779 bis 793 (nicht live: Verbindung wegnehmen war im Lauf nicht möglich) | Die Rückfrage „Einchecken und kassieren?“ kommt vor dem Server-Aufruf; ohne Verbindung folgt erst danach die Fehlermeldung (`Error = checkedIn.Error`). | Fehler nennen Problem und Weg (M5); wer nicht einchecken kann, soll nicht erst gefragt werden. | mittel | Bei fehlender Verbindung die Zeile sperren oder die Rückfrage durch „Ohne Verbindung geht das nicht“ ersetzen. |
| B3 | Kalender, Toast nach „Stornierung zurücknehmen“ | live, [b02](live-sichtpruefung/b02-toast-wieder-gebucht-abgeschnitten.jpg) | „Termin wieder gebucht: 14:35–15:20 (Elif Demir, Hand- und F…“ wird abgeschnitten; der Text hat 74 Zeichen. | Toast eine Zeile, höchstens 60 Zeichen, ohne Schlusspunkt (M1). | mittel | Kurzform „Wieder gebucht: Do 14:35“ (Quelle `CalendarViewModel.cs` um Zeile 1430, `Describe`). |
| B4 | Kalender, Toast „Gebucht: …“ | live (Probe der Meldung), Quelle `CalendarViewModel.FreeTime.cs` 204 | „Gebucht: Anna Bauer, Montag, 9. November 2026 09:05“ (51 Zeichen) benutzt das lange Datum; ab einem langen Namen sind es über 60 Zeichen. | Toast höchstens 60 Zeichen, Beispiel in M1: „Termin verschoben auf Do 14:30“. | mittel | Kurzes Datum („Mo 09:05“, am Tag selbst nur die Zeit). |
| B5 | Kunden, Maske „Neue Kundin“, Thema `customer-badmail` | live, [b03](live-sichtpruefung/b03-kunden-fehler-ueber-der-liste.jpg) | „Keine gültige E-Mail-Adresse.“ steht als rote Leiste **über der Kundenliste** links, weit vom E-Mail-Feld rechts; am Feld selbst ist nichts markiert. Der Text nennt keinen Weg. | Feldfehler am Feld (meldungen.md Abschnitt 2), Meldung zu einer Aktion über deren Knopf (M7, masken.md R8); Fehler nennen Problem und Weg (M5). | mittel | Fehler am Feld („Die E-Mail-Adresse braucht ein @.“) und Fokus auf das Feld; Leiste über „Anlegen“ nur für Serverfehler. |
| B6 | Kassa, Spalte „Artikel und Gutscheine“ | live, [b06](live-sichtpruefung/b06-kassa-kacheln.jpg) | Artikelkacheln brechen mitten im Wort („Gesichtscr eme Hydro“, „Körperpeel ing Kokos“, „Tageseintri tt Sauna“, „Zirbenkiss en“); der Knopf heißt „Wertgutschein ver…“ (abgeschnitten). | Kein Abschneiden oder falscher Umbruch (Auftrag, texte.md). | mittel | Kacheln in einer Spalte oder breiter, Silbentrennung statt Wortbruch, Knopf „Verkaufen“ unter der Überschrift „Wertgutschein“. |
| B7 | Kalender, Panel „Neuer Termin“, Meldung „Zeitraum ist belegt.“ | live, [b04](live-sichtpruefung/b04-zeitraum-belegt-panel.jpg) | Die Meldung steht am unteren Rand des Panels und ist angeschnitten (nur der Kopf und der Beginn der Konfliktzeile); „Termin buchen“ und „Zum Konflikt“ sind erst nach dem Scrollen zu sehen. | Meldung zu einer Aktion direkt über dem Knopf dieser Aktion, sichtbar (M7). | mittel | Nach dem Fehler die Meldung in den sichtbaren Bereich scrollen (`BringIntoView`) oder die Knopfleiste am Panelrand festsetzen. |
| B8 | Seitenpanel (Termin), Knöpfe „Einchecken“ und „Termin stornieren …“ | live: Einchecken in drei, Stornieren in zwei Läufen | Ein Mausklick direkt nach dem Öffnen des Panels löste nichts aus; Bild: nur Hover und Tooltip („Einchecken (E)“, „Stornieren (Entf)“), Zustand unverändert. Per UI Automation (Invoke) wirkt derselbe Knopf sofort. Unklar, ob das nur die synthetische Maus des Testers trifft oder die Rezeption im Betrieb. | Ein Klick auf einen sichtbaren, freigegebenen Knopf wirkt (Fehlbedienung am Gast). | mittel, ungeklärt | Council und Code Guardian prüfen: Baut das Panel nach dem Öffnen asynchron neu auf (Fokus, L16)? Mit echter Maus nachstellen. |
| B9 | Kalender, Rückfrage „Änderungen verwerfen?“ | live, [b05](live-sichtpruefung/b05-aenderungen-verwerfen.jpg) | Titel Frage, Antworten „Verwerfen“ und „Weiter bearbeiten“ (Verben, kurz), Fokus auf „Weiter bearbeiten“. Der Text „Was Sie hier eingetragen haben, geht beim Schließen verloren.“ redet die Person an. | Sachlich, Sie-frei (M5); Folge zuerst. | niedrig | „Die Eingaben gehen beim Schließen verloren.“ |
| B10 | Kalender, Rückfrage „Termin stornieren?“ (Rezeption, außerhalb der Stornofrist) | live, [b01](live-sichtpruefung/b01-storno-dialog.jpg) | Titel Frage, Antworten „Stornieren“ und „Abbrechen“, Fokus im Feld „Grund“ (vorbelegt „An der Rezeption storniert“). Der Text beginnt mit dem Termin samt Wochentag und Jahr, die Folge steht erst im zweiten Teil. | Text nennt zuerst die Folge (texte.md). | niedrig | „Der Termin ist danach storniert und blockiert keine Zeit mehr: Anna Bauer, Do 12.11., 14:35.“ |
| B11 | Kunden, Maske „Neue Kundin“, Hinweis „Schon vorhanden?“ | live, [b10](live-sichtpruefung/b10-schon-vorhanden.jpg) | Der Knopf „Diese Kundin nehmen“ steht auch bei „Moser, Alexander“. | Geschlechtsneutrale Form wie „Kundin / Kunde“ in der Maske. | niedrig | „Diese Person nehmen“ oder „Übernehmen“. |
| B12 | Kunden, Liste nach „Anlegen“ mit nur dem Nachnamen | live, [b03](live-sichtpruefung/b03-kunden-fehler-ueber-der-liste.jpg) | Der Eintrag heißt „Live1109022313n,“ mit hängendem Komma. | Kein Zeichen ohne Inhalt. | niedrig | Komma nur, wenn der Vorname da ist. |
| B13 | Kalender, Panel „Neuer Termin“, Karte „Vorschläge“ | live, [b04](live-sichtpruefung/b04-zeitraum-belegt-panel.jpg), [b05](live-sichtpruefung/b05-aenderungen-verwerfen.jpg) | Der Text neben „Freie Zeit suchen“ und „Übernehmen“ bricht in einer engen Spalte auf fünf bis sechs Zeilen. | Kein falscher Umbruch. | niedrig | Knöpfe unter den Text legen. |
| B14 | Kassa, Rückfrage „Einchecken und kassieren?“ | Quelltext `PosViewModel.cs` 781 | Text beginnt mit dem Zustand („Der Gast ist noch nicht eingecheckt.“), dann die Folge; Antwort „Einchecken und kassieren“ (drei Wörter, Verben). | Folge zuerst (texte.md). | niedrig | „Der Termin wird eingecheckt und auf die Rechnung gelegt.“ |

**Positiv geprüft** (kein Befund): Toast „… eingecheckt“ mit „Rückgängig“ und Schließen ([b09](live-sichtpruefung/b09-toast-eingecheckt-ok.jpg)); Tastenleiste im Kalender stimmt mit den Tasten überein (E Einchecken, K Kassieren, Entf Stornieren, Strg+Z Rückgängig, ? Alle Tasten); „−1 Tag“ beim Verschieben eines Termins von heute ist gesperrt (`reschedule-past`); bei einem eingecheckten Termin fehlt „Verschieben …“; im Panel eines stornierbaren Termins fehlt für die Rezeption „Ausfallgebühr belasten …“; im Reiter „Kassenlade & Abschluss“ gibt es für die Rezeption nur „Einlage“, der Hinweis „Entnahmen darf nur, wer die Kassenaufsicht hat.“ erklärt es; die Rückfragen „Änderungen verwerfen?“ und „Termin stornieren?“ schließen mit Esc.

## 3. Nicht geprüft (und warum)

- **Startbestand aus dem Review:** Fokus im Seitenpanel (Buchstaben-Kürzel greifen nicht, solange ein Feld den Fokus hat, L16) nur mittelbar durch B8, nicht gezielt; **Umbruch der Hinweiszeile im 420 Pixel breiten Rückfragedialog** nicht geprüft, weil die Stornofrist-Zeile nur nach der Frist erscheint und die Standortregel nicht gesetzt wurde (die Rückfrage in [b01](live-sichtpruefung/b01-storno-dialog.jpg) ist rund 490 Pixel breit und bricht den Text sauber); **Handbuchbilder mit geänderten Texten** nicht verglichen; **Tastenleiste und Tooltips gegen `web/src/help/content`** nur die Tastenleiste im Kalender, nicht gegen das Handbuch.
- Nur im Quelltext gelesen, nicht live: „… krank melden?“ (`CalendarViewModel.Absence.cs` 73: Titel Frage, Folge zuerst, Antwort „Krank melden“, in Ordnung), „Pause löschen?“ und „Sperrzeit löschen?“ (`CalendarViewModel.Notes.cs` 159: Folge „Die Zeit … ist danach wieder frei.“, Antwort „Löschen“, in Ordnung).
- Nicht gespielt: Storno-Dialog mit Stornofrist (Standortregel 24 Stunden und 50 %), Verschieben auf gestern mit Karte gesperrt und Server-409, Einchecken an einem anderen Tag (Knopf fehlt, Tastenleiste nennt den Grund), Tab „Entnahme“ für die Leitung, Pille „Keine Kontaktmöglichkeit“, Taste V, Fehlerfälle (Verbindung weg, 400 und 409 vom Server), Gutschein verkaufen und ungültiger Gutschein-Code (T5). Für diese Fälle braucht der Tester Zusatzschritte; die Läufe oben haben sie nicht von selbst getroffen.
- Tempo `slow` und `normal` wurden nicht gelaufen; Hell- und Dunkelschema nicht verglichen.

## 4. Befunde des Testers (für `tests/AispaStudio.LiveTests`)

Die folgenden Änderungen am Tester stehen auf diesem Branch, damit der Lauf weiterging; sie gehören nach Freigabe in einen eigenen Commit.

| Nr | Befund | Änderung |
| --- | --- | --- |
| T1 | `Toast()` suchte ein Element mit Name „Meldung“; Avalonia meldet den Text der Meldung selbst als Name und Hilfetext. Alle Toast-Prüfungen scheiterten damit, obwohl die App den Toast zeigt. | Suche nach Name oder Hilfetext; `ExpectToast` schreibt einen Befund in den Bericht, statt den Lauf zu beenden. |
| T2 | Esc auf „Neuer Termin“ mit Eingaben öffnet „Änderungen verwerfen?“; `appointment-closed` ließ die Rückfrage offen, das nächste Thema scheiterte. | `ClosePanelWithEscape` klickt „Verwerfen“. |
| T3 | Klicks direkt nach dem Öffnen des Panels (Einchecken, Stornieren) kamen nicht an (B8). | Knöpfe mit Klasse `Button` lösen per Invoke aus, kurze Pause nach dem Öffnen. |
| T4 | `appointment-past`: „Eine Karte von gestern“ gibt es am Montag nicht (Sonntag ohne Dienst). | offen: Tag davor mit Dienst wählen oder das Thema am Montag überspringen. |
| T5 | `voucher`: Ab Schritt 6 („Bar bezahlen“) stand ein Explorer-Fenster („Repositories“, alle Einträge markiert) im Vordergrund; der Tester schickte Strg+A und Tasten dorthin (nichts gelöscht, nur markiert). Ursache unklar (nicht das App-Fenster; das Fenster wurde nach dem Minimieren wieder sichtbar). `voucher-invalid`: Die Meldung „Diesen Gutschein-Code gibt es nicht.“ erscheint ohne offene Rechnung nicht. | offen. Der Tester sollte vor jeder Eingabe prüfen, dass das App-Fenster im Vordergrund ist, und sonst abbrechen (Sicherheit). |
| T6 | Feldnamen der Kassa haben sich geändert („Wert (EUR)“, „Betrag (EUR)“); „Gezählter Bestand (EUR)“ ist jetzt Pflicht für den Tagesabschluss. | Namen angepasst, Zählbestand 800,00 € eingetragen (erzeugt die Differenz in B1). |
| T7 | Der Bereich „Artikel und Gutscheine“ merkt sich offen oder zu; ein Klick auf die Kopfzeile schloss ihn. | `OpenSales` klappt nur auf, wenn „Wert (EUR)“ fehlt. |
| T8 | Der Tester endet beim ersten roten Schritt; Sammelläufe über 30 Themen brauchen deshalb Wiederholungen. | Vorschlag: roter Schritt zählt, der Lauf geht mit dem nächsten Thema weiter. |
| T9 | Die Freie-Zeit-Suche für „Maniküre mit Lack“ ist nach etwa drei Läufen am selben Tag ausgebucht; der Lauf bucht dann nicht heute, Einchecken scheitert. | Datenbank neu laden oder eine zweite Behandlung wählen. |

## 5. Bewertung

**Verfahren und Grenzen.** Die Bewertung machte ein Opus-Agent in einem Lauf, der das Befund-Gate von Code Guardian (drei Linsen: Ausführung, Absicht, Umgebung) und die Rollen des Councils (Rezeptionistin, Entwickler, Gestalter) zusammenfasst; es ist nicht der volle Lauf der Skills `code-guardian` und `llm-council` mit getrennten Agenten und fünf Beratern. Er las nur Quelltext und Bilder, startete nichts und baute nichts. Die Zahl der Schweregrade nach Prüfung weicht an zwei Stellen von der Liste ab: B1 bleibt als begründete Ausnahme „hoch“ (nicht umkehrbarer Kassenabschluss), B2 sinkt auf „niedrig“.

**Empfehlung.** Umsetzen in dieser Reihenfolge, je Aufgabe ein Commit: A1 (Rückfrage vor dem Tagesabschluss), A2 (Toast-Texte kurz), A3 (E-Mail-Fehler am Feld), A4 (fester Fuß in „Neuer Termin“), A5 (Kassa-Artikel einspaltig), A6 (Rückfragetexte), A7 (Einchecken ohne Verbindung), danach der Tester als eigener Commit (T1 bis T9 mit Vordergrundprüfung). **Vor jeder App-Änderung zu B8** die Gegenprobe (T-Probe): Mausklick mit Pause ohne Invoke, zehn Läufe; erst wenn dabei Klicks verloren gehen, gibt es eine App-Aufgabe. Entscheidungen des Auftraggebers: Notizpflicht bei Differenz im Tagesabschluss (ab 0,01 € oder ab einer Schwelle) und die neutrale Form „Diese Kundin nehmen“ (B11, Vorschlag „Übernehmen“).

Grundlage: `docs/umsetzung/live-sichtpruefung-2026-11-09.md` (Abschnitte 1 bis 4), `docs/gestaltung/meldungen.md`, `docs/gestaltung/texte.md`, `docs/design.md` (5.1, 5.10, 5.27), `CLAUDE.md`, Belegbilder b01 bis b08 und b10, Quelltext im Worktree `live-sichtpruefung-fe3810`. Alle Pfade relativ zum Worktree. Nichts im Repository geändert, nichts ausgeführt (kein Build, kein Test, kein App-Lauf).

### 0. Kurzfassung

| Nr | Urteil | Schwere laut Lauf | Schwere nach Prüfung | Aufgabe |
| --- | --- | --- | --- | --- |
| B1 | BESTÄTIGT (keine Rückfrage im Quelltext, nichts übersehen) | hoch | hoch als Sonderfall (unumkehrbarer Kassenbeleg); nach wörtlicher Skala mittel | A1 |
| B2 | BESTÄTIGT (Codepfad), Auswirkung klein | mittel | niedrig | A7 |
| B3 | BESTÄTIGT; Kürzung ist gewollt (`FitToast`), Ursache ist der zu lange Text | mittel | mittel (zusammen mit B4 und „Verschoben auf …“) | A2 |
| B4 | BESTÄTIGT; das lange Datum ist per Test festgeschrieben | mittel | mittel (mit B3) | A2 |
| B5 | BESTÄTIGT | mittel | mittel | A3 |
| B6 | BESTÄTIGT, unabhängig von der Fensterbreite | mittel | mittel | A5 |
| B7 | BESTÄTIGT; verletzt zusätzlich design.md 5.1 (Knopfleiste fest im Fuß) | mittel | mittel | A4 |
| B8 | UNENTSCHIEDEN, eher Tester (Tempo `fast` ohne Pause, Gegenprobe vermischt) | mittel, ungeklärt | offen; kein App-Auftrag vor der Gegenprobe | T-Probe |
| B9 | BESTÄTIGT | niedrig | niedrig | A6 |
| B10 | GEWOLLT (Aufbau entspricht dem Beispiel in design.md 5.27); nur das lange Datum weicht ab | niedrig | niedrig | A6 (nur Datum) |
| B11 | GEWOLLT (Slice 140, Handbuch, Handtests) | niedrig | niedrig, Entscheidung des Auftraggebers | A6 nach Ja |
| B12 | BESTÄTIGT | niedrig | niedrig | A3 |
| B13 | BESTÄTIGT | niedrig | niedrig | A4 |
| B14 | BESTÄTIGT (Folge nicht zuerst, Antwort mit drei Wörtern) | niedrig | niedrig | A6 |
| T4 | BESTÄTIGT (Tester-Annahme) | – | – | T-Commit |
| T5 | Teil 1 nicht prüfbar (Ursache), Schutz nur teilweise; Teil 2 BESTÄTIGT als Tester-Fehler, App-Verhalten GEWOLLT | – | Sicherheit: hoch für den Tester | T-Commit |
| T8 | BESTÄTIGT | – | – | T-Commit |

### 1. Prüfung je Befund

Linsen: (a) Ausführung, (b) Absicht, (c) Umgebung.

#### B1 Tagesabschluss ohne Rückfrage — BESTÄTIGT

- (a) `PosView.axaml:875` bindet „Tagesabschluss durchführen“ direkt an `CloseDayCommand`. `PosViewModel.Day.cs:341-363` (`CloseDayAsync`) schickt sofort `POST …/day-closings`; kein `AskAsync`/`ConfirmRequest`. Die einzigen `ConfirmRequest` der Kassa stehen in `PosViewModel.cs:781` (Einchecken und kassieren), `PosViewModel.cs:1169` (Rechnung verwerfen) und `PosViewModel.Day.cs:261` (Beleg stornieren). Der Lauf hat also nichts übersehen. Der Server (`PosService.cs:1450-1468`) prüft nur „nicht negativ“ und offene Kartenzahlungen; eine Notiz bei Differenz verlangt er nicht.
- (b) design.md 5.27 nennt als Anlass „Löschen, Stornieren, Verwerfen von Änderungen“; der Abschluss steht dort nicht ausdrücklich, ist aber nicht umkehrbar (Hilfe `kassa-tagesabschluss.md:48`, „lässt sich nicht ändern“). Weder Handbuch noch ADR nennen den fehlenden Dialog als gewollt. Die Notizpflicht bei Differenz ist nirgends festgelegt; die Hilfe sagt nur „den Grund eintragen“ (`kassa-tagesabschluss.md:36`).
- (c) Die Vorschau „Differenz −174,20 €“ steht schon vor dem Klick in der Karte (`PosView.axaml:870`); „ohne Hinweis“ stimmt also nicht ganz, „ohne Rückfrage“ stimmt. **Zusätzlicher Fund:** Die Vorschau rechnet mit dem Bericht, der beim Öffnen des Reiters geladen wurde (`PosViewModel.Day.cs:197-202`, `297-312`); der Server rechnet beim Abschluss neu (`PosService.cs:1436-1448`). Kommt zwischendurch ein Beleg dazu, speichert der Server eine andere Differenz als angezeigt. Das stützt die Rückfrage mit frisch geladenem Bericht.
- Ursache: fehlender Dialog in `CloseDayAsync`. Weg: vor dem POST `LoadReportAsync`, dann `ConfirmRequest("Tagesabschluss durchführen?", "Der Abschluss lässt sich danach nicht mehr ändern. Differenz zum Soll: … .", "Abschließen")`. Notizpflicht nur serverseitig (`PosService.DayReportAsync`, Feldfehler `Note`), weil Geschäftslogik in die API gehört; sie ist eine neue Regel und braucht ein Ja des Auftraggebers und einen Hilfetext.
- Aufwand: Rückfrage M (Dialog, Neuladen, Anpassung `tests/AispaStudio.Desktop.Tests/PosTests.cs:763-784`, Hilfe und Handbuchbild `tagesabschluss-4`, Live-Tester `day-closing` antwortet „Abschließen“); Notizpflicht zusätzlich M (API, Test in `tests/AispaStudio.Api.Tests/PosTests.cs`, Desktop-Feldfehler). Abhängigkeit: Notizpflicht wartet auf den Auftraggeber.

#### B2 Rückfrage vor dem Server-Aufruf — BESTÄTIGT, Auswirkung klein

- (a) `PosViewModel.cs:779-794`: erst `AskAsync`, dann `POST …/status`, Fehler in `Error`. Stimmt.
- (b) Kein Widerspruch zu einer Regel: der Fehler nennt den Server-Text. Die Shell zeigt ohne Verbindung schon „Kassieren geht erst wieder mit Verbindung.“ (`ShellViewModel.cs:410-414`, InfoBar `ShellView.axaml:121`).
- (c) Die Tagesliste ist nicht im Offline-Speicher (`Services/Offline.cs:102-105`, `api/pos/locations/…/day-list` fehlt); nach einem Neuladen ohne Verbindung ist sie leer. Der Fall entsteht nur, wenn die Verbindung bei offener Kassa abreißt. Der Client kennt den Zustand (`IAispaApi.IsOffline`, `src/AispaStudio.Client/IAispaApi.cs:14`).
- Weg: in `AddUnbilledAsync` vor der Rückfrage `_api.IsOffline` prüfen und eine Meldung mit Weg setzen. Aufwand S. Schwere niedrig (selten, Banner sagt es schon, kein Schaden).

#### B3 und B4 Toasts zu lang — BESTÄTIGT, gemeinsame Ursache

- (a) `CalendarViewModel.cs:1430` „Termin wieder gebucht: {Describe}“, `Describe` (`:1499-1505`) hängt Zeit, Person und Raum an. `CalendarViewModel.FreeTime.cs:204` nutzt `DayLayout.DateText` (`DayLayout.cs:1234`, „dddd, d. MMMM yyyy“). `ShowToast` kürzt jeden Text über `FitToast` (`CalendarViewModel.Problems.cs:105-109`) auf 60 Zeichen mit „…“; das Abschneiden im Bild b02 ist also diese Kürzung, nicht die Breite (Toast `MaxWidth` 760, Text 540, `CalendarView.axaml:2946-2952`).
- (b) M1 und design.md 5.10 verlangen höchstens 60 Zeichen mit dem Beispiel „Termin verschoben auf Do 14:30“; design.md 9 und 10 „Umbruch statt Kürzen“. `FitToast` ist das gewollte Sicherheitsnetz (Test `CalendarNoticeTests.cs:157-163`), die Texte selbst sind zu lang. Das lange Datum in „Gebucht: …“ ist per Test festgeschrieben (`tests/AispaStudio.Desktop.Tests/CalendarTests.cs:316`), also eine bewusste, aber regelwidrige Wahl.
- (c) **Zusätzlicher Fund, häufiger als B3:** „Verschoben auf {DescribeDay}“ (`CalendarViewModel.cs:1319`, `:1370`, `:1386-1387`) und „Zurück auf {Describe}“ (`:1432`) haben dieselbe Ursache; ein Verschieben auf einen anderen Tag ergibt rund 80 Zeichen und wird gekürzt.
- Weg: eine Kurzform für „wann“ (heute nur „14:35“, sonst „Do 12.11. 14:35“), in allen vier Texten statt `DateText`/`Describe`; Person und Raum nur, wenn sie sich geändert haben. Aufwand S bis M (Tests `CalendarTests.cs:316`, `UndoCancelCheckInTests.cs:98`, `FreeTimeSearchTests.cs:226/246`, `WaitlistTests.cs:170`, `CalendarRequestTests.cs:220` prüfen nur den Anfang „Gebucht: Name“ und bleiben grün, wenn die Form „Gebucht: Name, Do 09:05“ bleibt). Der Live-Tester prüft „Gebucht: {customer},“ (`ReceptionJourney.Appointments.cs:153`) und bleibt passend.
- Nebenbei (bekannt, kein neuer Befund): der Toast steht links (`CalendarView.axaml:2946`, `HorizontalAlignment="Left"`), Soll ist mittig (design.md 5.10); offen als A17 im PR-Protokoll (`pr-protokoll.md:2438`, `:2445`). Das Schließen-Kreuz des Toasts hat nur einen Tooltip, keinen `AutomationProperties.Name` (`CalendarView.axaml:2960`; design.md 5.1 verlangt ihn bei Symbol-Knöpfen).

#### B5 E-Mail-Fehler über der Liste — BESTÄTIGT

- (a) `CustomersViewModel.cs:590-600` setzt bei jedem Fehler `Error = result.Error`; die InfoBar dafür steht in der Spalte der Trefferliste (`CustomersView.axaml:30`). Das E-Mail-Feld (`:84-86`) hat kein `Error`, obwohl `FormField.Error` existiert (`Controls/FormField.cs:33`). Der Text kommt vom Server (`CustomerEndpoints.cs:71`, Prüfung nur auf „@“). `ApiResult` trägt keine Feldfehler (`src/AispaStudio.Client/ApiResult.cs:7`), eine Zuordnung zum Feld geht also nur clientseitig.
- (b) meldungen.md Abschnitt 2 (Feldfehler am Feld), M5, M7; masken.md R8.
- (c) keine Umgebungsfrage.
- Weg: clientseitige Prüfung wie der Server („enthält @“, Muster gibt es schon in `BusinessInfoViewModel.cs:840`), `FormField Error` am Feld, Fokus aufs Feld; die Server-InfoBar in die Maske über die Knopfleiste verschieben. Aufwand M.

#### B6 Kacheln brechen im Wort — BESTÄTIGT

- (a) Spalte fest 232 px (`PosView.axaml:465`), Artikel als `UniformGrid Columns="2"` (`:507`), Name mit `TextWrapping="Wrap" MaxLines="2"` (`:515`); Avalonia bricht ein zu langes Wort mitten im Wort. „Wertgutschein verkaufen“ hat bewusst `TextTrimming` (`:545`).
- (b) design.md 9 und 10: „Umbruch statt Kürzen“; der Kommentar `:535` zeigt, dass die Enge bekannt ist.
- (c) Unabhängig von Fenstergröße und offener Seitenleiste „KI und Hilfe“, weil die Spalte fest breit ist.
- Weg: Artikel einspaltig (Name eine Zeile, Preis rechts) oder Spalte breiter; Knopf „Verkaufen“ unter der Gruppe „Gutschein verkaufen“ ohne Trimming. Silbentrennung ist in Avalonia nicht vorhanden (nicht geprüft, ob weiche Trennstriche wirken); nicht darauf bauen. Aufwand S, plus Handbuchbilder der Kassa (`ViewTests.HelpFlowSteps.cs:246-250` sucht „Wertgutschein verkaufen“; ein neuer Name ändert Test, Hilfe und Live-Tester `ReceptionJourney.Pos.cs:80`).

#### B7 Meldung im Panel angeschnitten — BESTÄTIGT

- (a) „Neuer Termin“ ist ein langer `StackPanel` im scrollenden Inhalt; InfoBar `Error`, Konflikt und „Termin buchen“ stehen am Ende (`CalendarView.axaml:1380-1386`). Kein `BringIntoView` nach einem Fehler (nur `FocusInPanel` für benannte Elemente, `CalendarView.axaml.cs:223-235`). Die letzte Zeile der Seitenleiste ist die Reiterleiste (`:3091`), keine Knopfleiste.
- (b) M7 (Meldung direkt über dem Knopf, sichtbar) und design.md 5.1: „In der Seitenleiste steht die Leiste fest im Fuß … nicht im scrollenden Inhalt“. Die zweite Regel ist im Lauf nicht genannt, wiegt aber schwerer.
- Weg: Fuß mit InfoBar, Konflikt und „Termin buchen“ fest unter dem Scrollbereich (gleiches Muster dann für alle Masken der Seitenleiste); als kleiner Zwischenschritt `BringIntoView` auf die InfoBar nach einem Fehler. Aufwand M (fester Fuß, Handbuchbilder „Neuer Termin“), Zwischenschritt S.

#### B8 Klick nach Panel-Öffnen kam nicht an — UNENTSCHIEDEN, eher Tester

- (a) Im Quelltext fand sich kein asynchroner Neuaufbau **oberhalb** der Knöpfe: die nachgeladenen Karten (Anzahlung, Karte, Erinnerungen, Einwilligung, Anamnese, `CalendarViewModel.cs:856-857`) stehen unter dem Aktionsblock (`CalendarView.axaml:287-360`), Einchecken und Stornieren liegen darüber (`:144-150`, `:231-239`). Was beim Öffnen wirkt: das Panel blendet 200 ms ein und gleitet 20 px (`Themes/Styles.axaml:1510-1534`, nur ohne Tastaturmodus); ein Live-Echo desselben Termins ersetzt `Selected` durch ein neues ViewModel (`CalendarViewModel.cs:756-758`), wobei Avalonia die Vorlage vermutlich wiederverwendet (nicht geprüft). Der Kalender gibt die Maus beim Loslassen frei (`CalendarView.axaml.cs:776`); ein hängender Pointer-Capture ist nicht zu sehen.
- (c) Der Lauf lief mit `-Speed fast`: `StepDelay` ist dann 0 (`LiveOptions.cs:55`), `Highlight` entfällt (`LiveSession.cs:459-462`). Der Tester klickt also, sobald der Knopf im UIA-Baum steht (Abfrage alle 200 ms, `LiveSession.cs:144`), möglicherweise mitten im Einblenden. Ein Mensch braucht länger als 200 ms. Die Gegenprobe T3 hat **zwei** Dinge zugleich geändert (Invoke statt Maus **und** Pause, `ReceptionJourney.Pos.cs:42`, „ein zu früher Klick geht ins Leere“); sie trennt Tester und App also nicht.
- (b) Keine Regel macht das Verhalten gewollt.
- Fazit: Ohne getrennte Gegenprobe kein App-Auftrag. Probe: Mausklick mit 300 ms Pause und ohne Invoke (zehn Läufe); bleibt es grün, ist B8 ein Tester-Befund. Zusätzlich Handtest mit echter Maus (Karte anklicken, sofort Einchecken, zehnmal). Erst wenn auch dann Klicks verloren gehen, Ursache in der App suchen (Treffertest während der Transition, Neuaufbau durch Live-Echo).

#### B9 „Was Sie hier eingetragen haben …“ — BESTÄTIGT

`CalendarViewModel.PanelTabs.cs:468`. M5 „Sie-frei“; texte.md T1 erlaubt „Sie“ nur, wo eine Person unmittelbar angesprochen werden muss. Aufwand S, kein Test prüft den Text (grep über `tests/`).

#### B10 Storno-Rückfrage — GEWOLLT (Aufbau), Datum zu lang

`AppointmentViewModel.cs:269`: „Der Termin von {Title} am {DateText}, {TimeText}, ist danach storniert …“. Das ist genau das Muster aus design.md 5.27 („Der Termin von Mia Muster am Mi. 21.10. 10:00 ist danach nicht mehr im Kalender.“); die Regel zählt diesen Satz als „Folge zuerst“. Abweichung nur beim Datum: `DateText` liefert „Donnerstag, 12. November 2026“ statt „Do 12.11.“ (b01). Vorschlag: nur das Datum kürzen (gleiche Hilfe wie A2), Satzbau lassen. Aufwand S.

#### B11 „Diese Kundin nehmen“ — GEWOLLT

Festgelegt in Slice 140 (`work/slices/140-app-customer-new-form.md:15`), Handbuch `kunden-anlegen.md:17`, Handtest `docs/umsetzung/handtests.md:109-110`, Live-Tester `ReceptionJourney.Customers.cs:116-120`. texte.md T12 erlaubt „Kundin/Kunde“ für den Stammdatensatz, eine Pflicht zur neutralen Form steht nirgends; die Maske selbst sagt „Neue Kundin / neuer Kunde“. Änderung nur nach Ja des Auftraggebers, dann mit Hilfe, Handtest und Tester im selben Commit (ADR 0056). Gleiches gilt für „Keine Kundin gefunden.“ (`CustomersView.axaml:35`).

#### B12 Hängendes Komma — BESTÄTIGT

`CustomersView.axaml:54` setzt `<Run Text=", " />` immer; der Screenreader-Name kommt aus `Converters.cs:16` und hat dasselbe Komma. Weg: ein Konverter für Anzeige und Name, Komma nur mit Vorname. Aufwand S.

#### B13 Vorschläge brechen in der engen Spalte — BESTÄTIGT

`CalendarView.axaml:1284-1297` (`Grid ColumnDefinitions="*,Auto"`, Knopf rechts nimmt die Breite); dasselbe Muster bei `ColumnScopeText` (`:1359-1363`). Weg: Knopf unter den Text. Aufwand S, gemeinsam mit B7.

#### B14 „Einchecken und kassieren?“ — BESTÄTIGT

`PosViewModel.cs:781-782`: Zustand zuerst, Antwort „Einchecken und kassieren“ (drei Wörter, design.md 5.27 verlangt ein bis zwei). Weg: „Der Termin wird eingecheckt und auf die Rechnung gelegt.“, Antwort „Einchecken“ wäre zu ungenau; Council-Rat siehe Abschnitt 2. Aufwand S. Der Server-Text „Der Gast ist noch nicht eingecheckt.“ (`tests/AispaStudio.Api.Tests/StatusDayTests.cs:73`) ist ein anderer Text und bleibt.

#### T4 `appointment-past` am Montag — BESTÄTIGT

`ReceptionJourney.Appointments.cs:232-243` geht genau einen Tag zurück und sucht dort eine Karte. Weg: bis zu sieben Tage zurück bis zu einem Tag mit Karte. Aufwand S. **Nebenfund (nicht geprüft):** `:253-254` klickt ohne gebuchten Termin „Heute“ und dann „Nächster Tag“; der Kommentar sagt „Tag des Laufs“, das landet aber auf morgen, falls „Heute“ auf heute springt.

#### T5 Fremdes Fenster und Gutschein-Code — teils BESTÄTIGT

- Teil 1 (Explorer im Vordergrund): Ursache aus dem Quelltext nicht prüfbar. Der Schutz ist nur teilweise da: `Type` setzt jetzt `Window.SetForeground()` (`LiveSession.cs:405-406`), aber `Press` (`:413`), der Tastenweg in `ReceptionJourney.Account.cs:86-87` und Klicks per Koordinate prüfen das Vordergrundfenster nicht, und `SetForeground` kann unter Windows scheitern, ohne zu werfen. Weg: vor jeder Tastatur-Eingabe prüfen, dass das Vordergrundfenster zum App-Prozess gehört, sonst Lauf abbrechen. Schwere für den Tester hoch (Tasten in fremde Fenster). Aufwand S.
- Teil 2: Das Feld „Gutschein-Code“ gibt es nur in einer Zahlungszeile einer offenen Rechnung (`PosView.axaml:648`, `PosViewModel.OnlineInfo.cs:33-50`). Das App-Verhalten ist GEWOLLT; der Tester (`ReceptionJourney.Pos.cs:94-103`) muss erst eine Rechnung anlegen und die Zahlungsart Gutschein wählen. Aufwand S.

#### T8 Lauf endet beim ersten roten Schritt — BESTÄTIGT

`LiveSession.cs:115-121` wirft weiter, `LiveJourney.cs:90-95` bricht die Schleife ab. Weg: Fehler je Thema fangen, App in einen Grundzustand bringen (Dialog schließen, Panel schließen, Kalender heute), abhängige Themen (`Needs`, `LiveJourney.cs:69-83`) überspringen und im Bericht nennen. Aufwand M.

#### Nebenbei zu T1 (nicht beauftragt, nicht geklärt)

Der Toast-Text trägt ausdrücklich `AutomationProperties.Name="Meldung"` (`CalendarView.axaml:2952`), der Lauf fand aber den Text als Namen. Möglich ist, dass der `ToolTip.Tip` oder die Text-Peer-Implementierung den Namen überschreibt; das ist nur in einem Lauf mit einem UIA-Inspektor zu klären.

### 2. Council: nur abweichender Rat

Drei Rollen: Rezeptionistin im Alltag (R), Entwickler mit Blick auf Risiko und Tests (E), Gestalter nach Fluent 2 und Regeln (G). Wo alle drei dasselbe raten, steht nichts.

| Nr | R | E | G | Ergebnis |
| --- | --- | --- | --- | --- |
| B1 Rückfrage | Eine Rückfrage am Abend ist in Ordnung, aber nur mit der Differenz im Text, sonst klickt man sie weg. | Immer fragen, vorher den Bericht neu laden; sonst stimmt die Differenz im Dialog nicht. | Immer fragen (nicht umkehrbar), Text Folge zuerst, Antwort „Abschließen“. | Einig: immer fragen, mit frischer Differenz im Text. |
| B1 Notizpflicht | Gegen Pflicht bei jeder Abweichung: 0,10 € Rundung am Abend kostet Zeit. Lieber Pflicht ab einer Schwelle. | Pflicht nur im Server, sonst umgehbar; Schwelle wäre eine Einstellung mit Hilfetext (ADR 0055). | Pflichtfeld am Feld markieren, nicht als Leiste. | Entscheidung des Auftraggebers: Pflicht ab 0,01 € oder ab einer Schwelle. Vorerst nur die Rückfrage umsetzen. |
| B1 Schwere | mittel (nicht am Gast) | hoch (unveränderlicher Z-Bericht, Buchhaltungsexport bucht die Differenz) | – | hoch als begründete Ausnahme der Skala; erste Aufgabe. |
| B3/B4 | Name des Gastes muss im Toast bleiben, Raum und Person nicht. | Eine Hilfsfunktion für „wann“, sonst bleibt „Verschoben auf“ lang. | 60 Zeichen ohne „…“ als Prüfung im Test. | Einig auf „Gebucht: Anna Bauer, Do 09:05“ und „Wieder gebucht: Anna Bauer, Do 14:35“. |
| B5 | Fehler soll beim Tippen oder beim Verlassen des Felds kommen, nicht erst nach „Anlegen“. | Prüfung beim Verlassen und beim Anlegen, gleiche Regel wie der Server („@“), keine strengere. | Feldfehler unter dem Feld, Fokus aufs Feld (meldungen.md M2: erstes ungültiges Feld bekommt den Fokus). | Beim Verlassen und beim Anlegen prüfen. |
| B6 | Kacheln wie bisher zum Antippen, zwei Spalten sind schneller. | Einspaltig ist am einfachsten und testbar. | Umbruch statt Kürzen; Kacheln dürfen höher sein. | Zielkonflikt: einspaltige Liste mit Name und Preis in einer Zeile (R verliert wenig, Namen sind lesbar). |
| B7 | Knopf „Termin buchen“ immer sehen, ohne zu scrollen. | Fester Fuß ändert die gemeinsame Seitenleiste; erst nur „Neuer Termin“, dann die anderen. | Fester Fuß ist Regel (design.md 5.1), nicht nur `BringIntoView`. | Fester Fuß für „Neuer Termin“ jetzt, Rest als eigene Aufgabe. |
| B8 | Wenn das an der Rezeption passiert, ist es hoch: man glaubt, eingecheckt zu haben. | Ohne getrennte Gegenprobe nichts in der App ändern. | – | Erst Gegenprobe und Handtest; bei Bestätigung Schwere hoch. |
| B11 | „Diese Kundin nehmen“ bei einem Herrn wirkt seltsam. | Änderung zieht Hilfe, Handtest und Tester nach. | „Übernehmen“ ist kurz, ein Verb und neutral. | Auftraggeber fragen; Vorschlag „Übernehmen“. |
| B14 | „Einchecken und kassieren“ sagt genau, was passiert; kürzen macht es unklarer. | – | Höchstens zwei Wörter (5.27). | Zielkonflikt; Rat: Text umstellen (Folge zuerst), Knopf lassen und die Ausnahme im PR nennen. |

### 3. Empfehlung: Aufgaben, je Aufgabe ein Commit, in dieser Reihenfolge

| Reihe | Titel | Befunde | Dateien | Test, der den Fehler zeigt | Aufwand |
| --- | --- | --- | --- | --- | --- |
| A1 | Kassa: Rückfrage vor dem Tagesabschluss mit frischer Differenz | B1 (ohne Notizpflicht) | `ViewModels/PosViewModel.Day.cs`, `web/src/help/content/kassa-tagesabschluss.md` (+ Bild `tagesabschluss-4`), `tests/AispaStudio.LiveTests/ReceptionJourney.Pos.cs` (`day-closing` antwortet) | `PosTests`: „Tagesabschluss fragt vorher; Abbrechen speichert nichts“ (StubApi ohne `POST …/day-closings`, `FakeDialogs` mit Abbrechen); „die Rückfrage nennt die Differenz des neu geladenen Berichts“; bestehender Test `PosTests.cs:763-784` antwortet „Abschließen“ | M |
| A2 | Kalender: Toasts mit kurzem Datum, ohne Kürzung | B3, B4, „Verschoben auf“/„Zurück auf“ | `ViewModels/CalendarViewModel.cs` (1319, 1370, 1386, 1430, 1432, 1499), `CalendarViewModel.FreeTime.cs:204`, `DayLayout.cs` | `CalendarNoticeTests`: „Wieder gebucht“, „Gebucht“ und „Verschoben auf“ mit Demo-Namen (z. B. „Elif Demir“, „Hand- und Fußpflegeplatz“) enden nicht auf „…“; `CalendarTests.cs:316` auf die Kurzform ändern | S–M |
| A3 | Kunden: E-Mail-Fehler am Feld, Liste ohne hängendes Komma | B5, B12 | `ViewModels/CustomersViewModel.cs` (590-600), `CustomersViewModel.NewForm.cs`, `Views/CustomersView.axaml` (30, 54, 84-86), `ViewModels/Converters.cs:16` | `CustomerNewFormTests`: „keine-adresse“ setzt den Feldfehler, kein POST, `Error` bleibt leer; Konverter-Test „Nachname ohne Vorname ohne Komma“ | M |
| A4 | Kalender: „Neuer Termin“ mit festem Fuß, Vorschläge mit Knopf darunter | B7, B13 | `Views/CalendarView.axaml` (1281-1297, 1359-1363, 1380-1386), Handbuchbilder „Neuer Termin“ | `ViewTests` (headless, Fensterhöhe wie im Lauf): nach „Zeitraum ist belegt.“ liegen InfoBar und „Termin buchen“ ganz im sichtbaren Bereich der Seitenleiste | M |
| A5 | Kassa: Artikel einspaltig, Gutschein-Knopf ohne Kürzung | B6 | `Views/PosView.axaml` (504-547), `ViewTests.PosSalesColumn.cs`, `ViewTests.HelpFlowSteps.cs`, ggf. Live-Tester bei neuem Knopfnamen | `ViewTests.PosSalesColumn`: jeder Artikelname steht in einer Zeile (TextBlock-Höhe = eine Zeilenhöhe) und kein Knopftext ist gekürzt | S |
| A6 | Texte der Rückfragen nach 5.27 und M5 | B9, B10 (nur Datum), B14, B11 nach Ja | `CalendarViewModel.PanelTabs.cs:468`, `AppointmentViewModel.cs:269`, `PosViewModel.cs:781-782`; bei B11 `CustomersView.axaml:100`, `kunden-anlegen.md:17`, `handtests.md:109-110`, `ReceptionJourney.Customers.cs` | Je ein Assert auf den Text der `ConfirmRequest` über `FakeDialogs` (kein „Sie“, Folge zuerst, kurzes Datum) | S |
| A7 | Kassa: ohne Verbindung keine Rückfrage zum Einchecken | B2 | `ViewModels/PosViewModel.cs:779` | `PosTests`: bei `IsOffline` fragt `AddUnbilledAsync` nicht und nennt den Weg | S |
| T | Live-Tester: Änderungen T1 bis T9 als eigener Commit, dann T4, T5 (Vordergrund-Prüfung vor jeder Taste, Gutschein mit offener Rechnung), T8 (weiter nach rotem Thema) | T1–T9 | `tests/AispaStudio.LiveTests/*` | Gegenprobe mit `AISPA_LIVE_SKIP_CLICK` (`LiveSession.cs:201-213`) zeigt, dass ein ausgelassener Klick rot wird und der Lauf mit dem nächsten Thema weitergeht | M |
| T-Probe | B8 trennen: Mausklick mit Pause ohne Invoke | B8 | `LiveSession.cs:191-198` (vorübergehend Schalter) | zehn Läufe `checkin` und `cancel`; Ergebnis entscheidet, ob es eine App-Aufgabe gibt | S |

Nicht als Aufgabe empfohlen: B10-Satzbau (gewollt), B11 ohne Ja des Auftraggebers, Notizpflicht bei Differenz ohne Ja, Toast-Position (schon A17).

### 4. Nur durch einen weiteren Lauf oder Handtest zu klären

1. B8: Gehen Mausklicks direkt nach dem Öffnen des Panels auch mit echter Maus oder mit Tester-Pause (300 ms, ohne Invoke) verloren?
2. B8: Ändert ein Live-Echo des eigenen Termins (`CalendarViewModel.cs:756-758`) kurz nach dem Öffnen das Panel sichtbar (Flackern, Fokus)? Nur mit Protokoll der eingehenden Live-Updates prüfbar.
3. B1: Weicht die gespeicherte Differenz von der Vorschau ab, wenn zwischen Öffnen des Reiters und Abschluss ein Beleg entsteht? (Aus dem Quelltext erwartet, nicht nachgestellt.)
4. B2: Bleibt die Tagesliste der Kassa bei Verbindungsabbruch stehen, und welche Meldung kommt nach der Rückfrage?
5. B5: Kommt der Fehler bei anderen Feldern (Telefon) genauso über der Liste an?
6. B6: Wirken weiche Trennstriche in Avalonia-`TextBlock` (falls die Kacheln doch zweispaltig bleiben sollen)?
7. B7: Ist der Fehler bei anderen Fensterhöhen (Full HD ohne „KI und Hilfe“) überhaupt sichtbar angeschnitten?
8. T1: Warum meldet der Toast-Text seinen Inhalt statt „Meldung“ als UIA-Namen, obwohl `AutomationProperties.Name="Meldung"` gesetzt ist?
9. T5: Woher kam das Explorer-Fenster im Vordergrund?
10. Tempo `slow` und `normal`, Hell- und Dunkelschema (schon im Lauf als offen genannt).

### 5. Was nicht geprüft wurde

- Kein Build, kein Test, keine App gestartet; alle Aussagen über das Verhalten stammen aus dem Quelltext und den Bildern.
- Avalonia-Interna (Treffertest während einer Opacity-/Transform-Transition, Wiederverwendung der Vorlage bei neuem `Selected`) nicht nachgelesen; als Annahme markiert.
- Bilder b05 und b09 nicht angesehen (B9 nur über den Quelltext geprüft).
- `docs/gestaltung/masken.md` nicht gelesen; R8 nur über die Verweise in meldungen.md bewertet.
- Ob Avalonia weiche Trennstriche unterstützt, nicht geprüft.


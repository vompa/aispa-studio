Status: closed

# Runde R03 – Entscheidungen aus R02 und Befunde der fluent2-web-Durchsicht

## Quelle
- Die drei Entscheidungen zu den offenen Punkten der Runde R02: `work/rounds/R02-ui-decisions.md` unter „Offene Entscheidungen“ (Rulings vom 26.10.2026, Auftraggeber).
- Die Befunde B71 bis B88 aus den fluent2-web-Durchsichten der Slices 49 bis 52 und 45: `work/rounds/R02-ui-decisions.md` unter „Überraschungen“. Jede Slice kennzeichnet ihre Kennungen dort am Ende der Befundzeile mit „– erledigt in Slice NN“, vor dem Squash.
- Die manuelle Testliste der Runde R02 ist noch nicht durchgegangen; was der Auftraggeber dort meldet, kommt als weitere Slices in diese Runde oder in die nächste.

## Eingang
Befunde der fluent2-web-Durchsicht (nur Kennung): B71, B72, B73, B74, B75, B76, B77, B78, B79, B80, B82, B83, B84, B85, B86, B87; dazu die Rulings zu Slice 36, 38 und 47 aus R02. B81 und B88 sind durch das Ruling unten erledigt.

Notizen des Auftraggebers aus dem Test der Runde R02 (26.10.2026, Windows-App, Kalender und Rundenabschluss):

| Kennung | Wo | Beobachtet → Erwartet | Größe | Quelle |
|---|---|---|---|---|
| Ä77 | Windows-App, Warteliste (Panel) | „Erledigt …“ ist unklar: ist der Termin schon gebucht, warum steht der Eintrag dann in der Warteliste → Knopf und Zustand klar benennen | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 75 |
| Ä78 | Windows-App, Kalender, Kopf (Datumsauswahl) | Nach Wahl eines anderen Tages ändert sich die Tagesbeschreibung und die geöffnete Auswahl springt → Auswahl bleibt ruhig stehen | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 76 |
| Ä79 | Windows-App, Kalender, Neuer Termin | „Kein geeigneter Raum ist zu dieser Zeit frei“ wirkt nicht plausibel bei freiem Kalender → prüfen, ob der Raumplaner stimmt, und die Meldung erklärt Ursache und Weg | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 72 |
| Ä80 | Windows-App, Kalender, Meldungen | Fehlermeldungen stehen einmal unten (Neuer Termin) und einmal oben (Verschieben im Panel) → eine Regel für den Ort | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 72 |
| Ä81 | Windows-App, Termin-Panel | „Stornieren“ wirkt erst nach „Einchecken“ erreichbar → Ablauf der Aktionen im Panel neu durchdenken | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 75 |
| Ä82 | Windows-App, Neuer Termin, Kundensuche | Klick auf den richtigen Treffer soll den Namen ins Suchfeld übernehmen → gewählter Kunde ist erkennbar | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 70 |
| Ä83 | Windows-App, Kalender, Neuer Termin | Am Sonntag (Standort geschlossen) lässt sich der ausgefüllte Termin nicht schnell auf einen anderen Tag legen → Ziehen der Vorschau und Datumsfeld in der Maske | M | Auftraggeber, 26.10.2026 (Test von R02); Eintragen an Schließtagen erledigt in Slice 68; Datumsfeld in der Maske erledigt in Slice 70, Ziehen der Vorschau erledigt in Slice 71 |
| Ä84 | Windows-App und API, Prüfung beim Buchen | Prüfung und Meldung „Standort geschlossen“ dauern sehr lange → messen, einfache Fälle in der App prüfen, Zwischenspeicher erwägen | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 78 |
| Ä85 | Windows-App, Kalender, Neuer Termin | Ein Klick auf einen Slot schließt die offene, schon ausgefüllte Anlage → Eingaben bleiben, der Termin wird verlegt | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 70 |
| Ä86 | Windows-App, Neuer Termin, Reihenfolge | Der Kunde soll als Erstes gewählt werden, daraus ergeben sich Vorschläge für Behandlung und Notizen | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 70 |
| Ä87 | Windows-App, Kalender, Notiz | Notiz ist nach dem Speichern nicht sichtbar, in der Bearbeitung öffnet sich etwas Fremdes; an Schließtagen sollen Notizen funktionieren | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 69 |
| Ä88 | Windows-App, Kalender, Notizbalken | Der Notizbalken soll sich mit der Maus in der Höhe ändern lassen | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 71 |
| Ä89 | Windows-App, Kalender, Pause und Schließtage | Pausenbalken ebenso ziehbar; die Rezeption darf an Schließtagen alles eintragen, die Bearbeitung zeigt nur einen Hinweis | M | Auftraggeber, 26.10.2026 (Test von R02); Eintragen an Schließtagen erledigt in Slice 68; Pausenbalken ziehen erledigt in Slice 71 |
| Ä90 | Windows-App, Kalender, Meldungsleiste | Nach dem Eintragen einer Pause an einem Schließtag öffnet die Leiste „Meldungen“ gefiltert auf „Hamam“, ohne erkennbaren Grund; gestrichelte Platzhalter bleiben stehen | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 69; liegen gebliebene Platzhalter erledigt in Slice 71 |
| Ä91 | Windows-App, Kalender, Raumansicht | Räume nach Kategorien sortieren, einzelne Räume sperrbar (Reinigung, Defekt, sonstiges), Umschalter „Alle Räume“ und „Nur verfügbare Räume“ | L | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 77 |
| Ä92 | Windows-App, Kalender, Raumansicht | Keine Vorschau beim Hamam, Sauna und Ruheraum fehlen in der Raumansicht; Umschalten bei offener Bearbeitung soll den Raum in den Fokus bringen, die Vorschau ist ziehbar | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 77 |
| Ä93 | Windows-App, Kalender, Ziehen | Das Ziehen eines Termins bricht auch ab, wenn der Mauszeiger bewusst die Kalenderfläche verlässt | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 71 |
| Ä94 | Windows-App, Meldung „Nicht verschoben“ | Die Meldung soll einen Weg zum Konflikt bieten: Wochenansicht der Person mit Fokus auf den Tag und den belegenden Termin | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 72 |
| Ä95 | Windows-App und API, Stornieren | Versehentlich storniert: Rückgängig per Knopf und Strg+Z, später „Stornierung zurücknehmen“; Nachricht an den Gast wartet mindestens 10 Minuten, einstellbar; Strg+Z auch in der Tastenleiste | L | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 73 und 74 |
| Ä96 | Windows-App, Kalender, Tastenleiste | Die Legende unten hat mit der Maus keine Funktion → Einträge werden Knöpfe | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 76 |
| Ä97 | Windows-App, Termin-Panel, Einchecken | Verklickt beim Einchecken: Rückgängig sofort und später in der Bearbeitung, nicht mehr nach dem Kassieren | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 74 |
| Ä98 | Rundenabschluss | Prüfen, ob eine Prüfung von Eingaben und Formularen (Skills input-validation, form-validation) am Ende jeder Runde laufen soll | S | Auftraggeber, 26.10.2026 (Test von R02) |
| Ä99 | Windows-App, Kalender | Der Kalender soll sich per Kürzel auf den Zustand beim ersten Öffnen zurücksetzen lassen (Filter, Ansicht, Zeitraum), ohne eigenen Knopf, Anzeige unten in der Tastenleiste | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 76 |
| Ä100 | Windows-App, Tagesüberblick | Nur der Termin unter „Als Nächstes“ ist anklickbar → „Überfällig“ als Liste mit bis zu 5 Einträgen, Kennzahlen bleiben Anzeige | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 76 |
| Ä101 | Windows-App und Web, Begriffe | Ein Begriff für die Menschen im Kalender: „Mitarbeitende“ statt Personen, Team, MA, Therapeut | S | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 79 |
| Ä102 | Windows-App und Web, Tooltips | Alle Tooltips durchsehen: kurz, ein Satz, einheitlicher Begriff | M | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 79 |
| Ä103 | Alle Oberflächen, Sprache | Mehrsprachige Oberfläche: Aufwandsschätzung und gemeinsame i18n-Lösung für Web und Windows-App, Ressourcen automatisch erzeugt | L | Auftraggeber, 26.10.2026 (Test von R02) – erledigt in Slice 80 (Vorlage; Umsetzung erst nach Entscheidung des Auftraggebers) |

## Slices
| Slice | Name | Befunde | Hängt ab von | Modell | Neue Kennungen |
|---|---|---|---|---|---|
| 57 | Rückfragedialog: Startfokus nach Brief 5.27 | Ruling Slice 38 aus R02 | – | sonnet | – |
| 58 | Kundenbereich auf der eigenen Website einbetten | Ruling Slice 47 aus R02 | – | opus | – |
| 59 | Brief: Quelle des Werts der Statusfarbe „Erledigt“ | Ruling Slice 36 aus R02 | – | haiku | – |
| 60 | Formularbaustein in Stammdaten und Personal | B71, B76 | – | sonnet | B89–B93 |
| 61 | Formularbaustein in Anmeldung, Einrichtung und Betreiber | B72, B80 | – | sonnet | B94–B98 |
| 62 | Formularbaustein in Dienstplan, Zeiten, Ausfall-Assistent und Berichten | B77, B78 | – | sonnet | B99–B103 |
| 63 | Formularbaustein in Kalender, Posteingang und Kassa | B84, B87 | – | sonnet | B104–B108 |
| 64 | Anamnese-Fenster als natives Dialogfenster | B73 | – | sonnet | B109–B111 |
| 65 | Restliche Hinweise auf den Tooltip-Baustein, Kopieren mit Rückmeldung | B74, B75, B86 | 60 | sonnet | B112–B115 |
| 66 | Zustände im Kontrastdesign von Windows | B82, B85, B83 | – | sonnet | B116–B119 |
| 67 | Tastaturtests für die Berichte | B79 | – | sonnet | B120–B124 |
| 68 | Schließtage: die Rezeption darf alles eintragen | Ä83, Ä89 (Ruling) | – | opus | Ä104–Ä106 |
| 69 | Notizen und Pausen an Schließtagen, keine fremde Meldungsleiste | Ä87, Ä90 | 68 | sonnet | Ä107–Ä109 |
| 70 | Neuer Termin: Kunde zuerst, Treffer übernehmen, Tag wählen, nichts verwerfen | Ä86, Ä82, Ä83, Ä85 | 68 | sonnet | Ä110–Ä112 |
| 71 | Ziehen im Kalender: Vorschau, Notiz, Pause, Abbruch | Ä83, Ä88, Ä89, Ä93, Ä90, Ä94 | 70 | opus | Ä113–Ä115 |
| 72 | Meldungen im Kalender: ein Ort, Ursache, Weg zum Konflikt | Ä79, Ä80, Ä94 | – | sonnet | Ä116–Ä118 |
| 73 | Wartezeit vor der Nachricht an den Gast | Ä95 (Ruling) | – | opus | Ä119–Ä121 |
| 74 | Rückgängig für Stornieren und Einchecken | Ä95, Ä97 | 73 | opus | Ä122–Ä124 |
| 75 | Ablauf im Termin-Panel und klare Warteliste | Ä81, Ä77 | 74 | sonnet | Ä125–Ä127 |
| 76 | Kalenderkopf, Tastenleiste, Zurücksetzen, Tagesüberblick | Ä78, Ä96, Ä99, Ä100 (Rulings) | – | sonnet | Ä128–Ä130 |
| 77 | Raumansicht: Kategorien, gesperrte Räume, Sauna und Ruheraum | Ä91, Ä92 | 71 | sonnet | Ä131–Ä133 |
| 78 | Prüfung beim Buchen schneller | Ä84 | 68 | opus | Ä134–Ä136 |
| 79 | Ein Begriff „Mitarbeitende“, Tooltips durchsehen | Ä101, Ä102 | 75, 76, 77 | sonnet | Ä137–Ä139 |
| 80 | Mehrsprachigkeit: Entscheidungsvorlage mit Aufwand | Ä103 | – | opus | Ä140–Ä142 |
| 81 | Prüfschritt für Eingaben am Rundenende | Ä98 | – | sonnet | Ä143–Ä145 |

Parallel möglich: 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68, 72, 73, 76, 80 und 81 haben keine Abhängigkeit; 65 kommt nach 60 (beide ändern `admin/StaffPage.tsx`). Im Kalender der Windows-App folgen nacheinander: 68 → 69, 70, 78 → 71 → 77; 73 → 74 → 75; 79 erst nach 75, 76 und 77. Konflikte beim Squash sind vor allem in Protokoll, Stand-Seite und Rundenakte zu erwarten.

## Entscheidungen
Ruling: Die Runde setzt die drei Entscheidungen aus R02 um (Rulings in `work/rounds/R02-ui-decisions.md`, 26.10.2026) und die Befunde B71 bis B88 der fluent2-web-Durchsicht; Slice-Plan wie oben – Auftraggeber, 26.10.2026
Ruling: B81 und B88 (Schriftgrößen in `px` statt `rem`) werden nicht umgestellt: Slice 36 hat `px` als bewusste Abweichung im Brief festgehalten, weil Brief W1 `px` verlangt und der Zoom des Browsers `px` mitskaliert – Claude, 26.10.2026
Ruling: Die volle Prüfkette läuft mit `npm test -- --maxWorkers=3` (work/conventions.md), weil `npm test` ohne Begrenzung beim Rundenabschluss von R02 zweimal in Zeitüberschreitungen lief – Auftraggeber, 26.10.2026
Ruling: GUI-Tests laufen immer mit einem Benutzer mit Inhaber-Rechten, damit alle Masken sichtbar sind; nur Tests einer Rechtegrenze setzen ihre Rolle ausdrücklich (work/conventions.md „Oberflächen prüfen“) – Auftraggeber, 26.10.2026
Ruling: Die Rezeption darf an Schließtagen und an Tagen ohne Dienst Termine, Pausen, Notizen, Sperrzeiten und Sauna-Eintritte ganz normal eintragen; die Bearbeitung zeigt nur einen Hinweis; die Online-Buchung bleibt gesperrt; Nachtrag zu ADR 0073 (Slice 68, Ä89, Ä87) – Auftraggeber, 26.10.2026
Ruling: Die Nachricht an den Gast bei Absagen und Verschiebungen des Teams wartet mindestens 10 Minuten und ist einstellbar (Slice 73, Ä95) – Auftraggeber, 26.10.2026
Ruling: Die Ansicht des Kalenders lässt sich per Kürzel zurücksetzen, ohne eigenen Knopf, Anzeige unten in der Tastenleiste (Slice 76, Ä99) – Auftraggeber, 26.10.2026
Ruling: Im Tagesüberblick ist „Überfällig“ eine Liste mit bis zu 5 Einträgen, die Kennzahlen bleiben Anzeige (Slice 76, Ä100) – Auftraggeber, 26.10.2026
Ruling: Für die Menschen im Kalender gilt der Begriff „Mitarbeitende“ (Slice 79, Ä101); der Auftraggeber hat den Slice-Plan mit diesem Begriff am 26.10.2026 bestätigt – Auftraggeber, 26.10.2026
Ruling: Die Mehrsprachigkeit (Ä103) wird zuerst als Entscheidungsvorlage mit Aufwandsschätzung bearbeitet (Slice 80); gebaut wird erst nach Entscheidung des Auftraggebers – Auftraggeber, 26.10.2026
Ruling: Die Wartezeit vor der Nachricht an den Gast gilt für die Absage wegen einer Schließzeit und die Benachrichtigung der Warteliste; sie steht je Standort (0 bis 60 Minuten, Standard 10, auch für bestehende Standorte), beginnt der Termin in weniger als 2 Stunden, geht die Nachricht sofort; „Nicht senden“ lässt den Termin abgesagt (Slice 73, ADR 0081) – Claude, 26.10.2026
Ruling: Slice 73 hat keinen „Tests rot“-Lauf gegen die fehlende Funktion: die Tests wurden vor dem Code geschrieben, liefen aber erst mit ihm (ohne die neuen Typen und Endpunkte bauen sie nicht); die Spalte ist abgehakt – Claude, 26.10.2026
Ruling: Eine Stornierung wird über `POST /api/appointments/{id}/restore` zurückgenommen: Zeit, Person, Raum und Gerät müssen frei sein, sonst antwortet der Server mit 409 und die Rückfrage nennt den Konflikt und bietet „Verschieben …“ an (dann nimmt `restore` mit neuem Beginn zurück). Der Termin ist danach immer „Gebucht“, auch wenn er vor dem Stornieren eingecheckt war (Slice 74, Ä95) – Claude, 26.10.2026
Ruling: „Einchecken zurücknehmen“ setzt „Eingecheckt“ auf „Gebucht“ (`POST …/status`), solange der Termin nicht kassiert ist; kassiert heißt „Erledigt“ oder eine Kassenzeile zum Termin, auch auf einer offenen Rechnung (sonst 400, Weg über den Beleg an der Kassa, ADR 0010) (Slice 74, Ä97) – Claude, 26.10.2026
Ruling: Eine zurückgenommene Stornierung hält die wartende Absage-Nachricht zurück (`ClosureNoticeState.Cancelled`); ist sie schon raus, entscheidet das Team in der Rückfrage über eine Korrektur an den Gast, eine neue `ClosureNotice` mit `IsCorrection` (Migration `CancellationCorrection`, Sprache und Kanal der Absage) (Slice 74, Ä95, Ä121) – Claude, 26.10.2026
Ruling: Slice 74 hat für die Windows-App keinen „Tests rot“-Lauf gegen die fehlende Funktion: die Tests lassen sich ohne die neuen Mitglieder des ViewModels nicht bauen. Die API-Tests liefen vorher rot (10 von 11 Fällen: 404 bzw. 200 statt 400); die Spalte ist abgehakt – Claude, 26.10.2026
Ruling: Slice 62 – Plan mit den Annahmen 1 bis 6 freigegeben (autonome Abarbeitung): Kontrollkästchen bleiben in `label.inline`, Tagespanel und Nachtrag bleiben ohne `<form>` (B99), gesperrte Knöpfe nennen ihren Grund über `ReasonButton`/`aria-disabled` – Claude, 26.10.2026
Ruling: Im Termin-Panel stehen „Einchecken zurücknehmen“ und „Termin stornieren …“ im Aktionsblock statt hinter „Mehr“; „Stornieren“ geht bei „Gebucht“ und „Eingecheckt“ ohne Einchecken (Slice 75, Ä81) – Claude, 26.10.2026
Ruling: Die Warteliste schließt eine Anfrage mit „Anfrage abschließen …“ und den Gründen „Gebucht“, „Abgesagt“, „Nicht erreichbar“; „Abgesagt“ und „Nicht erreichbar“ senden beide `NoLongerWanted`, es gibt keinen neuen Server-Status (Slice 75, Ä77) – Claude, 26.10.2026
Ruling: Die Spalten Plan, Tests rot, Green, Docs und Checks von Slice 75 sind abgehakt; „Tests rot“ zeigte sich als Übersetzungsfehler (die neuen Namen `ShowCancelAction` und `WaitlistRow.CloseLabel` fehlten), nicht als Laufzeitfehler – Claude, 26.10.2026
Ruling: Slice 80 ist eine reine Dokument-Slice ohne Code: „Tests rot“ und „Green“ sind abgehakt, weil es nichts gibt, was rot oder grün sein könnte; der Abnahmefall wird als Durchsicht geprüft (manuelle Testliste) – Claude, 27.10.2026
Ruling: Die Vorlage `docs/konzepte/mehrsprachigkeit.md` empfiehlt, die Wörterbücher im Build der Entwicklung zu erzeugen und zu prüfen (Weg B), mit Pflichttexten nur von Hand freigegeben; der Auftraggeber entscheidet über Weg, Sprachen, Umfang und Freigabe (Slice 80, Ä103) – Claude, 27.10.2026
Ruling: Slice 58 legt die Websites je Spa fest (`Tenant.EmbedAllowedOrigins`, nur `https://host[:port]`, höchstens 10, keine Platzhalter) und liefert `frame-ancestors` in einem eigenen durchsetzenden Header neben dem Report-Only-Header; für nginx fragt `auth_request` die API (ADR 0080, Nachtrag) – Claude, 27.10.2026
Ruling: Die Spalte „Tests rot“ von Slice 58 ist abgehakt: Die neuen API-Tests konnten vor der Umsetzung nicht übersetzt werden (`EmbeddingSettings` fehlte), sie waren also rot, aber nicht als Laufzeitfehler – Claude, 27.10.2026

Ruling: Slice 57 – `ConfirmDialog` bekommt die Option `initialFocus` (`cancel`, `confirm`, `field`); ohne Angabe folgt sie `danger` (zerstörend `cancel`, sonst `confirm`). „Feld“ ist das erste Text-, Zahl-, Mehrzeilen- oder Auswahlfeld in `children`, Kontrollkästchen zählen nicht. Fehlt das Ziel oder ist der Knopf gesperrt, bleibt der Fokus auf „Abbrechen“ – Claude, 27.10.2026 (Plan-Annahmen 1 und 2)
Ruling: Slice 57 – Handbuch und ADR bleiben unverändert; `docs/design.md` 5.27 nennt für „Termine absagen?“ jetzt den Fokus im ersten Feld statt auf „Abbrechen“ (die Slice-Datei verlangt „Feld“, die Brief-Zeile widersprach); die Abweichung stand sonst nur in der Rundenakte R02 (dort gestrichen), `docs/gestaltung/meldungen.md` enthält keinen Vermerk. Die zerstörende Aktion ist nie Startfokus: `initialFocus="confirm"` wirkt nur bei `danger` aus. „Tagesabschluss?“ (neutral, aber unumkehrbar) behält „Abbrechen“ (`initialFocus="cancel"`), weil die Slice sie nicht nennt; der Auftraggeber kann sie auf den primären Knopf stellen Spalte „Tests red“: drei der sechs neuen Tests waren vor der Umsetzung rot (neutral, Feld, Angabe „confirm“), die übrigen sichern Randfälle – Claude, 27.10.2026 (Plan-Annahme 4)
Ruling: Slice 59 – der Satz zur Statusfarbe „Erledigt“ steht unter der Tabelle „2.4 Kalenderstatus“ in `docs/design.md` und nicht in der Zelle, weil die Tabelle dafür keine Spalte hat; die Farbwerte bleiben unverändert, kein ADR – Claude, Annahme der Slice 59 (der Auftraggeber bestätigt den Platz beim Test). Spalten „Tests rot“ und „Green“ sind abgehakt, weil es nichts gibt, was rot oder grün sein könnte (reine Doku-Slice, wie Slice 80); der Abnahmefall wird über die manuelle Testliste geprüft – Claude, 27.10.2026 (Plan-Annahme 5)
Ruling: Slice 64 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): `Modal` ist ein natives `<dialog>` nach dem Muster von `ConfirmDialog`, Esc über das Dialogfenster, Titel-ID per `useId`; der Inhalt des Bogens bleibt unverändert – Claude, 27.10.2026
Ruling: Slice 66 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): ein Block `@media (forced-colors: active)` je Stylesheet (`index.css`, `booking.css`, `myday.css`, `operator.css`, `stock.css`) nur mit Systemfarben, Mein Tag und Betreiber nur für ihre vorhandenen Zustände (erledigt, gesperrt, Fehler) – Claude, 27.10.2026
Ruling: Slice 67 – Plan mit den Annahmen 1 bis 4 freigegeben (autonome Abarbeitung): kein neues Paket (`@testing-library/user-event` fehlt), stattdessen der Test-Helfer `reports/tabOrder.ts` (Tab-Reihenfolge nach HTML-Regeln, `pressTab`/`walkTab`); Statusregionen stehen von Anfang an im Baum (Tagesstart, Hygiene, Karten des Tagesplans, TSE-Export, Management, Pflichtberichte), leere Regionen ohne Außenabstand – Claude, 27.10.2026
Ruling: Slice 63 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): nur die sieben Dateien aus dem Umfang auf `FormField`, Feldfehler (Name des neuen Kunden) ans Feld mit Fokus, Serverfehler der Aktionen bleiben `role="alert"`, jeder `<button` in `calendar/`, `inbox/`, `pos/` trägt `type` – Claude, 27.10.2026
Ruling: Slice 68 – Schließtag heißt: am Ortstag bleibt nach Abzug der Schließzeiten kein Öffnungsfenster oder am Standort hat niemand Dienst; ein halber Tag Schließzeit, eine Uhrzeit außerhalb der Öffnungszeit an einem offenen Tag und eine einzelne Person ohne Dienst an einem Tag mit Dienstplan bleiben wie bisher – Claude, 26.10.2026 (Plan-Annahme 1)
Ruling: Slice 68 – „Rezeption“ ist die Buchungsquelle `Reception` (Team: Buchen und Verschieben im Kalender); Online, KI-Terminannahme, Plattformen und Assistent planen weiter streng (`PlanningContext.House` nur dort wahr) – Claude, 26.10.2026 (Plan-Annahme 2)
Ruling: Slice 68 – Der Hinweis „Der Standort hat an diesem Tag geschlossen.“ steht in `AppointmentDto.Warnings` (vor den Lagerhinweisen) und in `AreaVisitDto.Warnings`; die Maske „Neuer Termin“ der Windows-App zeigt ihn vorab, ohne etwas zu sperren; Satz an einer Stelle in `BookingNotices.ClosedDay` – Claude, 26.10.2026 (Plan-Annahmen 3, 4, 6)
Ruling: Slice 68 – Fünf bestehende API-Tests hielten das alte Verhalten fest (409 für die Rezeption an Tagen ohne Öffnungszeit oder Dienst) und erwarten jetzt 201 mit Hinweis; die Erwartung „keine Zeiten für Gäste“ blieb in allen – Claude, 26.10.2026 (Plan-Annahme 8)

Ruling: Slice 81 ist ein reines Konzeptdokument; die Spalten „Tests rot“ und „Green“ sind abgehakt, weil es keinen Code und keine Tests gibt (Abnahme ist das Lesen des Dokuments) – Slice 81, 27.10.2026
Ruling: Die Skills `input-validation` und `form-validation` werden nicht installiert; Vorschlag und Empfehlung stehen in `docs/konzepte/eingabepruefung-rundenabschluss.md` – Slice 81, Annahme, vom Auftraggeber zu bestätigen
Ruling: Slice 72 – Ortsregel für Meldungen im Kalender: Eine Meldung zu einer Aktion steht direkt über dem Knopf dieser Aktion im selben Panel (Neuer Termin, gemeinsame Buchung, Eintrag, Verschieben-Karte im Termin-Panel); ohne Panel (Ziehen) in der Meldungszeile über dem Raster. Festgehalten als M7 in `docs/gestaltung/meldungen.md` – Claude, 27.10.2026 (Plan-Annahme 1)
Ruling: Slice 72 – „Zum Konflikt“ ersetzt „Termin anzeigen“ an der Konfliktmeldung und an „Nicht verschoben“: Woche (7-Tage-Ansicht) der belegenden Person, bei einem Raum im Weg des Raums, ab dem Tag des Termins, Termin markiert; das Anklicken einer Zeile markiert weiter nur – Claude, 27.10.2026 (Plan-Annahme 2)
Ruling: Slice 72 – Der Raumplaner rechnet richtig: „Kein geeigneter Raum ist zu dieser Zeit frei“ erschien in Wien, weil Anti-Aging (wie alle Gesichtsbehandlungen) dort nur die Kosmetikkabine hat; zwei freie Räume lösen die Meldung nie aus (Test). Geändert hat sich nur die Erklärung: belegte Räume mit Belegung und `BookingConflict.NextFree` (nächste freie Zeit, nur Räume gerechnet) – Claude, 27.10.2026 (Plan-Annahme 4)
Ruling: Slice 76 – Plan mit den Annahmen 1 bis 5 freigegeben (autonome Abarbeitung): Kürzel Strg+R für „Ansicht zurücksetzen“ (zurück auf kein Filter, Teamansicht, heutigen Tag, keine Hervorhebung; Zoom, „Alle zeigen“, Standort und offene Leisten bleiben), Datumsknopf mit Mindestbreite 340, gesperrte Einträge der Tastenleiste mit Grund im Hinweis, „Überfällig“ als Liste mit fünf Einträgen unter der unveränderten Zahlenzeile – Slice 76, Annahme, vom Auftraggeber zu bestätigen
Ruling: Die Spalte „Tests rot“ von Slice 76: rot war der Test der Datumsauswahl (Knopfbreite 323 gegen 264 nach dem Wechsel auf Mittwoch); die Tests der Tastenleiste, des Zurücksetzens und der Liste entstanden nach dem Code und waren nie rot – Slice 76
Ruling: Slice 70 – Plan mit den Annahmen 1 bis 8 freigegeben (autonome Abarbeitung): Vorschläge aus `GET api/customers/{id}/summary` und `GET api/reports/customers/{id}/preferences` (kein neues Kundenfeld), Behandlung und Wunsch per Knopf übernehmbar, die gewohnte Person nur als Hinweis, Name im Suchfeld per `InHouseName`, Datumsfeld mit Sprung des Kalenders, Klick auf einen anderen freien Slot verlegt den angefangenen Termin – Slice 70, Annahme, vom Auftraggeber zu bestätigen
Ruling: Slice 70 – Die Spalte „Tests rot“: die Tests entstanden vor dem Code und waren rot, weil sie neue Mitglieder (`SuggestedTreatment`, `HasChosenCustomer`, `DateText` …) nutzen und nicht übersetzten; ein Rot-Lauf gegen den alten Stand mit Stubs wurde nicht nachgeholt – Slice 70

Ruling: Slice 69 – Plan mit den Annahmen 1 bis 3 übernommen (autonome Abarbeitung): nach dem Speichern einer Sperrzeit, Pause oder Notiz bleibt die Seitenleiste zu, kein Nachbar-Reiter geht auf; Kreuz und Esc zeigen ihn weiter – Slice 69, Annahme, vom Auftraggeber zu bestätigen
Ruling: Slice 69 – Die Spalten „Tests rot“ und „Grün“ gelten für die Reiter-Ursache: zwei Tests (Notiz speichern, Pause eintragen) mit Termin- und Meldungsreiter waren rot (die Leiste „Meldungen“ ging auf), jetzt grün; der dritte (Neuladen) war nie rot, weil der Server die Notiz am Schließtag schon lieferte – Slice 69
Ruling: Slice 78 – Plan mit den Annahmen 1 bis 6 übernommen (autonome Abarbeitung): die Regel „offen, Schließtag oder geschlossen“ steht als `OpeningRules` in `AispaStudio.Contracts` und wird von Raumplaner und Windows-App genutzt; „Neuer Termin“ zeigt die Meldung „Der Standort hat zu dieser Zeit geschlossen.“ bzw. „… in dieser Zeit geschlossen (Art).“ sofort und ohne Aufruf; der Server lehnt solche Zeiten vor dem Planungskontext ab; der Zwischenspeicher `OpeningCache` (je Standort und Woche, 5 Minuten, verfällt bei jeder Änderung an Schließzeiten oder Öffnungszeiten) dient nur dieser Ablehnung, der Planer lädt beim Buchen weiter frisch; kein ADR, kein neues Paket – Slice 78, 27.10.2026
Ruling: Slice 78 – Die Spalte „Tests rot“: rot waren die drei Vorabprüfungs-Tests der Windows-App (Meldung fehlte) und der Server-Test der Abnahme 1 (22 statt höchstens 6 Datenbankbefehle). Die Server-Tests zum Verfall des Zwischenspeichers (Abnahme 2) waren vor dem Bau grün, weil es noch keinen Zwischenspeicher gab; zwei davon (geänderte und gelöschte Schließzeit, neue Öffnungszeiten) wären ohne den Verfall rot, der Test „neue Schließzeit“ nicht: ein veralteter Eintrag „offen“ lässt den Planer mit frischen Daten dieselbe Meldung geben (Durchsicht). `OpeningRulesTests` waren rot, weil `OpeningRules` noch nicht übersetzte – Slice 78

Ruling: Slice 71 – Plan mit den Annahmen 1 bis 5 übernommen (autonome Abarbeitung): Vorschau ziehen nur für „Termin“ (Ablegen wie der Klick auf einen anderen Slot, Ä85); Ränder ziehen nur an Notizen mit Uhrzeit und Pausen (6 px Greifbereich, Raster 5 Minuten, mindestens 5 Minuten); neuer Endpunkt `PATCH /api/blocks/{id}` mit `BlockChange`; Abbruch, wenn der Mauszeiger den sichtbaren Kalender (`BodyScroll`) um mehr als 8 px verlässt; ein Neuzeichnen ohne offenen Eintrag räumt die Viertelstunde unter dem Mauszeiger – Slice 71, Annahme, vom Auftraggeber zu bestätigen
Ruling: Slice 71 – Die Spalte „Tests rot“: die ViewModel-Tests liefen gegen Stubs der neuen Methoden rot (kein Ziehen, Platzhalter „13:00“ blieb nach dem Neuladen, Esc schloss die Maske statt das Ziehen abzubrechen); die Headless-Maustests und der API-Test entstanden zusammen mit dem Code und waren nicht einzeln rot – Slice 71
Ruling: Slice 77 – Plan mit den Annahmen 1 bis 5 übernommen (autonome Abarbeitung): die Stammdaten haben keine Raumkategorie, deshalb folgt sie aus dem Typ („Behandlungsräume“, „Sauna und Ruhe“); „gesperrt“ für den Umschalter heißt: Raumsperren decken die ganze Öffnungszeit des Tages ab; der Umschalter „Alle Räume“ / „Nur verfügbare Räume“ steht nur in der Raumansicht, nur wenn er etwas ändert, und wird nicht gespeichert – Auftraggeber, 27.10.2026
Ruling: Slice 77 – Die Spalte „Tests rot“: die sieben neuen Tests (`RoomViewTests`) ließen sich vor der Umsetzung nicht übersetzen (neue Eigenschaften `Category`, `IsArea`, `CanToggleRooms`, `OnlyAvailableRooms`); außerdem widersprach `GroupAndAreaTests.Bereiche_stehen_nicht_im_Kalender…` der Anforderung Ä92. Sie ist umbenannt (`Bereiche_zeigen_Belegung_und_Eintritte`) und erwartet jetzt „Raum Zeder, Finnische Sauna“; die Anforderung hat sich geändert, der Test ist nicht abgeschwächt – Auftraggeber, 27.10.2026
Ruling: Slice 77 – Drei bestehende Ansichtstests folgen der neuen Anforderung, ohne eine Prüfung zu streichen: `Raumansicht_hat_in_Pixeln_…` erwartet 11 statt 9 Spalten (neun Räume plus Sauna und Ruheraum), die Liste der Chips ohne Reiterzustand in `ViewTests.States.cs` nennt auch `HeadRoomFilter` (wie `HeadStaffFilter`); der Hinweis am Raumkopf behält höchstens fünf Zeilen, die Kategorie steht nur im Hinweis der Bereiche – Auftraggeber, 27.10.2026
Ruling: Slice 77 – Keine Zeitgrenze und keine Wartezeit in einem Test angehoben – Auftraggeber, 27.10.2026
Ruling: Slice 79 – Plan mit den Annahmen 1 bis 5 übernommen (autonome Abarbeitung): „Mitarbeitende“ ist der Sammelbegriff, „Team“ der Name von Ansicht und Stammdatenseite, „Therapeut:in“ nur Rolle und Gästewunsch (`docs/gestaltung/texte.md` T12); neue Regel T22 für Tooltips (ein Satz, höchstens zwei Kommas, ohne die sichtbare Beschriftung); Hinweis von „Alle zeigen (n)“: „Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund“ – Slice 79, Annahme, vom Auftraggeber zu bestätigen
Ruling: Slice 79 – Die Hinweise der Kopfchips des Kalenders (Überblick, Bereiche, Warteliste, „Alle zeigen“, Räume, Meldungen, Kalender-Abos) nennen den Chip nur noch in der kompakten Kopfzeile (`ChipTip`), wo der Chip bloß Symbol und Zahl zeigt; die Hilfe sagt dort weiter „der Text steht im Hinweis“ – Slice 79
Ruling: Slice 79 – Die Spalte „Tests rot“: rot waren die vier neuen Tests der Windows-App (`TermsAndTooltipTests`: Begriffe, feste Tooltips mit rund zwanzig Verstößen, Kopfchips, Wortlaut „Alle zeigen“) und der Hilfetest in `terms.test.ts` (zwei Stellen in `seite-kalender.md`); die Web-Tests zu `title` und zu Texten der Oberfläche fanden von Anfang an nichts – Slice 79
Ruling: Slice 79 – Keine Zeitgrenze und keine Wartezeit in einem Test angehoben; kein Test abgeschwächt, kein Wortlaut eines bestehenden Tests geändert – Slice 79

## Überraschungen
- Ä134 (Slice 78): Das Laden eines Kalendertags (`GET …/calendar`) braucht 48 bis 56 Datenbankbefehle und dauert ruhig 0,2 s, unter Last 0,6 bis 3,5 s (einzelne Abfragen auf Termine, Raumsperren und Dienstplan bis 1,1 s); das ist die größere Wartezeit beim Tageswechsel, aber nicht Teil der Prüfung beim Buchen. Vorschlag: Abfragen bündeln und messen wie hier (Plan der Slice 78, Abschnitt „Messung“).
- Ä135 (Slice 78): Die Vorabprüfung in der App gilt nur für „Neuer Termin“; Verschieben im Panel, Ziehen, gemeinsame Buchung und Sauna-Eintritt prüfen weiter nur auf dem Server, und `AreaVisitService` sowie `GroupPlanner` rechnen „offen“ noch mit eigenem Code statt mit `OpeningRules`.
- Slice 78: Der bestehende Test `ConflictNoticeTests.Buchen_zeigt_die_Liste_in_Ortszeit…` buchte vor der Öffnungszeit (8:30 gegen 9 Uhr) und verließ sich auf den Stub des Servers; mit der Vorabprüfung meldete die App „geschlossen“. Der Beginn steht jetzt auf 9:00, die Prüfung ist unverändert.
- Ä136 (Slice 78): Ändert ein anderer Arbeitsplatz die Schließzeiten, sieht der offene Kalender das erst beim nächsten Laden des Tages (es gibt keine Live-Nachricht für Schließzeiten); bis dahin kann die Vorabprüfung eine Zeit ablehnen, die der Server schon annehmen würde, und umgekehrt entscheidet der Server.
- Slice 60: Der Prüftest suchte auf `dev` 78 umhüllende `<label>` mit Text-, Auswahl- oder Mehrzeilenfeld in 15 Dateien von `admin/`; sie sind alle auf `FormField` umgestellt (Umbau per Skript, danach von Hand geprüft). Ein Pflichtfeld trägt jetzt den Stern im Namen des Feldes („Vollständiger Name *“); zwei bestehende Tests fragten den Namen exakt ab und lesen ihn jetzt mit Regex (`/^Vollständiger Name( \*)?$/`, wie schon beim Preis), ein dritter sucht den Hex-Wert der Kalenderfarbe im `.form-field` statt im `<label>`.
- Slice 60: Behandlungen und Person ließen „Speichern“ bei leerem Namen gesperrt (`disabled`) – ohne Grund am Feld. Jetzt bleibt der Knopf frei, der Fehler steht am Feld und der Fokus springt hin. Der gesperrte Knopf bei unlesbarem Preis bleibt (Slice 12/40, durch Tests festgelegt). Die Formulare der Zugänge (Slice 42/45/48: `SubmitButton` mit Grund, `guardedSubmit`) sind nicht angefasst.
- Ä119 (Slice 73): Eine Verschiebung des Teams verschickt heute keine Nachricht an den Gast, nur die Erinnerungen folgen dem neuen Beginn. Die Wartezeit hat dort nichts zu verzögern; wer später eine Verschiebe-Nachricht baut, nutzt `GuestMessageTiming.SendAt` und `ClosureNotice.ScheduledFor` (ADR 0081).
- Ä120 (Slice 73): Die Zwei-Stunden-Regel kennt bei Wartelisten-Einträgen keinen Beginn, dort gilt nur die Wartezeit. Ob ein Wunsch, der schon morgen früh liegt, trotzdem warten soll, ist nicht entschieden.
- Ä121 (Slice 73, Hinweis für Slice 74): Ein „Absage zurücknehmen“ muss eine noch wartende Nachricht (`ClosureNoticeState.Queued` mit `ScheduledFor` in der Zukunft) mit zurücknehmen (`ClosureCancellations.WithdrawAsync`), sonst erfährt der Gast von einer Absage, die es nicht mehr gibt. Dazu: die Mail über die Erstattung einer Online-Anzahlung (`PaymentRefunded`) geht weiter sofort raus und verrät die Absage vor dem Ende der Wartezeit; nicht entschieden, ob sie ebenfalls wartet.
- Die Testmandanten der API stellen die Wartezeit auf 0 (`TestSpa.CreateAsync`); ohne das hätten alle Absage-Tests auf eine zehn Minuten spätere Zustellung gewartet.
- `ClosureCancellationTests.Absagen_sagt_die_gewaehlten_Termine_ab…` lief in einem Sammellauf mit sechs Klassen einmal in die 20-Sekunden-Grenze von `WaitForNoticesAsync` (HTTP 499), einzeln und im Klassenlauf grün: Last, keine Regression.
- Slice 65: Der Prüftest `tooltipUsage.test.ts` verbietet jetzt jedes native `title` an `span`, `td`, `li` und `th`; auf `dev` standen dort fünf (Mandantenname in `App.tsx`, KI-Pille, zwei Sprachkürzel in `CancelAppointmentsDialog.tsx`, `td` der Mitteilungstabelle). `LocationsTab.tsx` und `RebookSection.tsx` trugen schon keins mehr. Die Namens-`title` an Knöpfen („Diese Antwort war hilfreich“) bleiben (NICHT bauen).
- Slice 65: Rot-Lauf nachgeholt: die neuen Tests gegen den Stand von `dev` (nur Testdateien ausgecheckt) – 5 von 25 Tests rot (beide Prüftests, „Link kopiert.“, Link ohne Zwischenablage, Sperrgrund des Empfangsgeräts), die übrigen grün.
- Slice 65: „Kopieren“ heißt jetzt „Einladungslink kopieren“ (Wortlaut der Abnahme); die Hilfe `zugaenge` und `zugaenge-person-einladen` nennt den Knopf, die Meldung „Link kopiert.“ und den Weg ohne Zwischenablage. Das Empfangsgerät steht jetzt in einem `<form>` mit `SubmitButton` und nennt den Grund („Name fehlt.“, „Abmelden nach: 1 bis 120 Minuten.“).
- Ä122 (Slice 74): Eine Anzahlung, die mit der Absage wegen einer Schließzeit über die Kassa zurückging (Storno-Beleg), kommt beim Zurücknehmen nicht wieder: der Beleg bleibt storniert. Die Rückfrage sagt es (aus `ClosureNotice.RefundAmount`) und verweist an die Kassa. Ob die Anzahlung mit zurückgenommen werden soll, ist nicht entschieden.
- Ä123 (Slice 74): „Rückgängig“ gibt es nur für den einzelnen Termin im Panel (Knopf, Taste E und Entf, Rechtsklick). „Ganze Buchung stornieren“, „Diesen und alle folgenden“ (Serie) und „Alle einchecken“ laufen über andere Wege und haben keines; die Termine einer gemeinsamen Buchung lassen sich aber einzeln über ihre durchgestrichene Zeile zurücknehmen.
- Ä124 (Slice 74): Der Vorstatus fehlt am Termin. „Stornierung zurücknehmen“ setzt deshalb immer „Gebucht“; war der Gast schon eingecheckt, checkt die Rezeption neu ein. Ein neues Feld dafür wäre eine eigene Slice.
- Slice 74: Die eindeutige Kennung (`ClosureId`, `AppointmentId`) von `ClosureNotice` ließ keine zweite Nachricht (Korrektur) zum selben Termin zu (500 beim Zurücknehmen); der Schlüssel enthält jetzt `IsCorrection`.
- Slice 74: `Strg+Z` war schon für die Verschiebung belegt; der Rückgängig-Mechanismus (`MoveUndo`, Meldung, `CanUndo`) trägt jetzt eine Art (`UndoKind`). Die Tastenleiste zeigt „Strg+Z Rückgängig“ nur, solange `CanUndo` gilt; `RunOnAppointmentAsync` setzt seine eigene Meldung nur noch, wenn das Panel keine mit „Rückgängig“ gesetzt hat.
- Slice 74 (Durchsicht): Das Kassieren setzt den Status nicht auf „Erledigt“, ein bezahlter Termin bleibt „Eingecheckt“. Der Kalendertag nennt deshalb die Termine mit Kassenzeile (`CalendarDay.BilledAppointmentIds`), und das Panel bietet „Einchecken zurücknehmen“ für sie nicht an. Die Nachricht zählt nur bei einer Absage des Betriebs (`CancelledByBusiness`) und nur die Absage selbst, nicht frühere Korrekturen; Korrekturen zählen nicht als abgesagter Termin.

- Slice 61: Die Registrierung (`SignupForm`) hat kein Passwort (Passkey); „Passwörter stimmen nicht überein“ gibt es in Einrichtung, „Passwort vergessen“ und Passwortänderung. Dort steht der Fehler jetzt am zweiten Passwortfeld („… wiederholen“), per `aria-describedby` verbunden, und der Fokus springt hin.
- Slice 61: Der Betreiber-Einstieg lädt `stock.css` nicht (Bundle-Budget); `operator.css` bekommt deshalb die Regeln des Bausteins (Beschriftung, Hinweis, Fehler, roter Rand) selbst. `check:bundle`: Betreiber 76,8 kB gzip von 100 kB.
- Slice 61: Der Serverfehler der Anmeldung am Betreiber („Der Schlüssel stimmt nicht.“) bleibt eine Ansage (`role="alert"`) und hängt zusätzlich am Feld; die Fehler von „Spa sperren“ und „Nummer zuordnen“ stehen als Feldfehler (Fokus ins Feld, keine Ansage).
- Slice 61: Neue Befunde: B94 – die PIN-Felder der Schnellanmeldung (`auth/QuickSwitch.tsx`, zwei Formulare mit umhüllendem `<label>`) sind nicht Teil von B72/B80 und stehen in der Ausnahmeliste des Prüftests; B95 – zwei vorhandene Texte tragen „bitte“ gegen texte.md T8 („Bitte wähle jetzt ein eigenes …“ in `PasswordChangeForm`, „Bitte oben rechts …“ in `SignupForm`).
- Slice 62: Der Prüftest fand in `roster/`, `time/TimesheetPage.tsx` und `reports/*View.tsx` je ein umhüllendes `<label>` mit Feld im Tagespanel (Notiz), im Nachtrag (Grund), im Tagesplan (Person, Vorlieben, Nachbereitung), in Hygiene (Abnahme durch, PIN) und Management (Standort, Von, Bis); alle stehen jetzt in `FormField`. Die Dienst-Zeilen und Beginn/Ende des Nachtrags trugen nur ein `aria-label` und haben jetzt eine sichtbare Beschriftung mit demselben Namen.
- Slice 62 (Durchsicht): „Kommen“ in der Stempeluhr bleibt `disabled`, wenn kein Standort da ist (ein Grund „Standort wählen“ wäre ohne Auswahl, bei einem Standort, unerfüllbar); Fehler „Grund angeben.“ im Nachtrag setzt den Fokus ins Feld (R8) statt `role="alert"`.
- Slice 62: Der Nachtrag braucht `required` am Grund weiter (Test Slice 39 liest `field.required`); ohne `<form>` löst das keine Browser-Prüfung aus.
- Slice 62: Neue Befunde: B99 – Tagespanel des Dienstplans und Nachtrag sind keine `<form>` (Enter sendet nicht, Pflichtfehler als Meldung), zweiter Teil von B78; B100 – die Auswahlfelder „Standort“ in der Dienstplan-Leiste und „Standort zum Stempeln“ tragen nur ein `aria-label`, keine sichtbare Beschriftung.
- Slice 75: Die Warteliste liefert der Server nur mit Status „Wartet“; der Filter beim Laden im Client ist eine zusätzliche Absicherung (Test mit gebuchtem Eintrag im Stub). Ein Eintrag, der in der geöffneten Liste gebucht wird, verlässt sie wie bisher sofort.
- Slice 75: Der Prüftest der Panel-Reihenfolge ist auf das ViewModel beschränkt (`ShowCancelAction`, `ShowUndoCheckIn`); die Reihenfolge der Knöpfe im Bild ist nur im Quelltext des Panels festgelegt.
- Slice 80, Ä140: Der Kommentar zu `Language` in `OnlineContracts.cs` (Zeile 62) sagt, andere Sprachen als Deutsch, Englisch, Französisch und Italienisch bekämen Englisch. `GuestLanguage.Normalize` fällt aber auf Deutsch zurück (Ä52), ebenso `detectLanguage` im Kundenbereich. Der Kommentar ist veraltet; nicht geändert (NICHT bauen).
- Slice 80, Ä141: Die Liste der Sprachen steht an mindestens sieben Stellen: `GuestLanguage.Supported`, `languageNames` und `locales` in `web/src/booking/i18n.tsx`, `OnlineLanguage.Culture`, `AnamnesisForm`, die Reihenfolge in `CancelClosureDialog.cs` (Zeile 323) und in `closuresModel.ts` (Zeile 201). Eine neue Sprache berührt alle; die Vorlage schlägt eine einzige Liste im Katalog vor.
- Slice 80, Ä142: `lang="de"` steht fest in den fünf `index.html`, und Datum, Zahl und Währung hängen an 342 Stellen an fester Kultur (112 Windows-App, 155 API, 75 Web), nicht an einer Sprache der Benutzerin. Nicht geändert.
- Slice 80: Ein erster Zähllauf der Satz-Literale in C# zeigte für die API 1.921 statt 2.938 Treffer, weil der reguläre Ausdruck Zeichenketten mit `$"…"` falsch auseinanderschnitt; das Zählskript im Anhang der Vorlage zählt sie mit. Zahlen aus zwei Zählarten nie mischen.
- Slice 58: Der bestehende Test `TeamWebRootTests` sagte „kein `Content-Security-Policy`-Header auf `/buchen/`“; das gilt nicht mehr, weil `frame-ancestors` einen durchsetzenden Header braucht (in `Content-Security-Policy-Report-Only` sperrt der Browser nichts). Der Test prüft jetzt den neuen Header; der Report-Only-Header und die Team-Oberfläche ohne Richtlinie bleiben abgesichert, nichts abgeschwächt.
- Slice 58 (Durchsicht): Drei Funde behoben – `/buchen/index.html` lieferte im Einzelplatz keinen Header (jetzt auch `.html`), der Endpunkt für nginx hing am Modul Online-Buchung (jetzt außerhalb), Umlaute in der Adresse hätten den Header ungültig gemacht (jetzt nur Punycode); führende Nullen im Port werden entfernt.
- Slice 58: Die Unteranfrage von nginx erbt die Abfrage (`?spa=`) der Seite nicht (`$arg_spa` ist dort leer); das Spa kommt deshalb in `X-Original-URI` (`$request_uri`).
- Slice 59: Der Brief nennt für den Kontrast 4,6:1 die Rundenakte R02. Die Zahl steht in der R01 (`R01-ui-review.md`, Ruling 25.10.2026); R02 hält die Entscheidung zu `StDone` (dort nur die Nachmessung 3,93:1 der Empfehlung). Der Satz in `docs/design.md` nennt beide Stellen.

- Slice 64: Das Dialogfenster liegt im Top-Layer; die Druckregel musste vom alten `.modal-backdrop` (`position: static`) auf das Fenster selbst (`position: absolute; inset: 0`) umgestellt werden, sonst füllt der Bogen nur die Fläche des Fensters. Im Browser mit den Druckregeln als `@media all` geprüft: nur der Bogen sichtbar, volle Breite, kein Schleier, kein Rahmen.
- Slice 64: Neuer Befund B109 – `stock/stockUi.tsx` baut sein Fenster als `div.stock-modal-backdrop` ohne `<dialog>` (gleicher Mangel wie B73: kein Fokusfang, Hintergrund nicht inert); nicht Teil der Slice.
- Slice 64: Der Prüftest `components/confirmDialogUsage.test.ts` erlaubte `showModal()` nur in `ConfirmDialog`; er nennt jetzt auch `anamnesis/Modal.tsx` (großes Fenster, keine Rückfrage). Die Regel (keine selbstgebauten Rückfragen) bleibt, kein Test entfällt.
- Slice 66: Der Prüftest `forcedColors.test.ts` liest die Quelltexte; ob der Browser die Regeln im Kontrastdesign anwendet, kann nur die manuelle Testliste zeigen (jsdom kennt weder Medium noch Farben). B116 bis B119 wurden nicht gebraucht.
- Slice 66: `button[aria-pressed='true']` hatte in der Verwaltung außerhalb der Kopfleiste keine eigene Darstellung (Kalenderleiste, Hilfe-Rückmeldung, Assistent); im Kontrastdesign zeigt es jetzt Highlight, im normalen Design bleibt es unverändert.
- Slice 63: Der Prüftest fand 71 umhüllende `<label>` mit Feld nicht, sondern 15 in den sieben Dateien (Umbau per Skript, Auswahlfelder und die Antwort mit wechselnder Beschriftung von Hand). Nur „Neuer Kunde“ steht in einem `<form>`; Abnahme 2 war dort schon grün, der Prüftest sichert sie. Zusätzlich trägt jetzt jeder der 65 Knöpfe in `calendar/`, `inbox/` und `pos/` ein `type` (B87), auch außerhalb von Formularen.
- Slice 63: Der Fehler „Vor- oder Nachname angeben.“ stand als `role="alert"` unter dem Formular; am Feld liest ihn der Screenreader über `aria-describedby`, sobald der Fokus ins Feld „Vorname“ springt. Der alte Test auf `role="alert"` prüft jetzt den Text über `aria-describedby`.
- Neuer Befund B104 (Slice 63): `calendar/RebookConsentSection.tsx` (Sprache der Mail) und `calendar/GroupBookingPanel.tsx` (Gästeauswahl, Pause) nutzen noch umhüllende `<label>` mit Feld und gehörten nicht zum Umfang; der Prüftest nennt nur die sieben Dateien.
- Neuer Befund B105 (Slice 63): Die Felder „Artikel“, „Menge“, „Freie Position“, „Steuersatz“ und „Grund für das Storno“ der Kassa tragen nur `aria-label` ohne sichtbare Beschriftung; ein `FormField` würde die Maske umgestalten (ausgeschlossen) und bleibt offen.
Ä104 (Slice 68): Paar und Spa-Tag lassen sich an einem Schließtag weiter nicht buchen, weil `GroupPlanner` die Öffnungszeiten selbst prüft (`FreeStarts`, `PlanAreas`); die Gruppenbuchung bekommt den Hausmodus und den Hinweis noch nicht.
Ä105 (Slice 68): Der Hinweis in den Masken für Sperrzeit, Notiz und Pause fehlt (sie sperren nie, zeigen aber den Tag nicht als geschlossen); der Sauna-Eintritt meldet den Hinweis nur bei fehlendem Öffnungsfenster, nicht bei einem Tag ohne Dienst.
Ä106 (Slice 68): „Freie Zeit suchen“, Serien und der Vorschlag der Rezeption bieten an einem Schließtag keine Zeiten an, obwohl die Rezeption dort eintragen darf; der Weg ist heute der Klick auf eine Stelle im Kalender. Ebenso lehnen die Übernahme eines Vorschlags aus dem Posteingang und die Serien-Vorschau an einem Schließtag weiter ab.
Durchsicht durch den Opus-Prüfer (Slice 68): behoben wurden (1) der Hausmodus beim Verschieben durch Treatwell, den Gast-Weg und den Ausfall-Assistenten (jetzt nur der Kalender-Endpunkt, `MoveAsync(…, house: true)`), (2) ein Tag ohne Dienst hob alle Zeitgrenzen auf (jetzt nur innerhalb der Öffnungszeiten, halbe Schließzeiten bleiben abgelehnt), (3) abwesende Personen (Urlaub, Krankenstand) werden am Tag ohne Dienst nicht vorgeschlagen, (4) der Hinweis beim Sauna-Eintritt erscheint auch in der Windows-App. Zurückgestellt: die Maske kennt nur eine einzelne ganz deckende Schließzeit (Server: mehrere zusammen), der Sauna-Eintritt kennt „Tag ohne Dienst“ nicht, der reine Planer-Test fehlt (der Weg ist über die API getestet), `MemoryDemoOps.Book` plant ohne Hausmodus (heute ohne Folgen).
Die Basisprüfung lief erst nach einem zweiten Anlauf durch: der erste Build im Hintergrund kollidierte mit einer zweiten Sitzungs-Shell auf dieselben `obj`-Dateien (CS2012); ein Build zugleich im Worktree.

- Ä143 (Slice 81): Das Repository `FRmicrow/dataFootV1`, aus dem das Verzeichnis `claudskills.com` beide Skills führt, ist nicht mehr abrufbar (404); der Wortlaut von `input-validation` und `form-validation` ließ sich nicht lesen, das Dokument stützt sich auf Beschreibung und eine gleichnamige Fassung von aj-geddes.
- Ä144 (Slice 81): axe-core läuft in 34 Testdateien des Webs, aber nichts meldet eine Seite mit Formular ohne axe-Test; Vorschlag `formCoverage.test.ts` im Konzeptdokument, nicht gebaut.
- Ä145 (Slice 81): Die Windows-App hat Scan-Tests über alle Seiten, aber keinen für die Eingabefelder (Fehler am Feld, Fokus auf das erste ungültige Feld); nur als Durchsicht vorgeschlagen.
- Ä116 (Slice 72): Konflikte mit Sperrzeiten, Geräten oder Pausen haben kein „Zum Konflikt“ (kein Termin dahinter); die Meldung nennt sie nur. Ein Weg zur Sperrzeit (öffnen, ändern) wäre neue Funktion.
- Ä117 (Slice 72): „Verschieben“ im Termin-Panel zeigt den Konflikt als Text mit Person und Raum, hat aber keinen Knopf „Zum Konflikt“ (der Aufruf gibt nur den Fehlertext zurück); die Liste mit Knopf erscheint dort nur beim Zurücknehmen einer Stornierung.
- Ä118 (Slice 72): Die „nächste freie Zeit“ bei „Kein Raum frei“ rechnet nur die Räume; die gewählte Person kann dann noch belegt sein, die Buchung scheitert beim nächsten Versuch mit der Meldung der Person.
- Slice 72: Rot-Lauf: die neuen Tests der Slice waren rot, weil sie neue Mitglieder (`BookingConflict.NextFree`, `ConflictLinks`, `ErrorInMove` …) nutzen und nicht übersetzten; ein eigener Rot-Lauf gegen den alten Stand mit stubs wurde nicht nachgeholt. Zwei bestehende Erwartungen in `ConflictNoticeTests` (Zeile mit Raum) und der Aktionstext „Zum Konflikt“ in zwei Tests änderten sich mit der verlangten Wirkung.
- Ä145 (Slice 81): Die Windows-App hat Scan-Tests über alle Seiten, aber keinen für die Eingabefelder (Fehler am Feld, Fokus auf das erste ungültige Feld); nur als Durchsicht vorgeschlagen.- Ä128 (Slice 76): Ob die geöffnete Datumsauswahl auch ohne Breitenwechsel des Knopfs springt (Plattform-Popup), ist im Headless-Test nicht zu sehen; die Mindestbreite von 340 fängt nur kürzere Titel, ein Feiertag mit langem Namen verbreitert den Knopf weiter.
- Ä129 (Slice 76): Das Handbuchbild `kalender-tastenhilfe` zeigt die Tastenhilfe ohne den neuen Eintrag „Strg+R“; nicht neu erzeugt.
- Ä130 (Slice 76): „und n weitere“ steht wörtlich nach der Slice-Datei über der Liste „Überfällig“; unter der Liste würde es gewohnter gelesen, der Auftraggeber entscheidet.
- Ä110 (Slice 70): Die gewohnte Person ist nur ein Hinweis („Meist bei …“), kein Knopf: sie zu übernehmen hieße, den Termin in die Spalte dieser Person zu verlegen. Ein Knopf „Dorthin verlegen“ wäre eine kleine Folge des neuen Verlegens (`BookingViewModel.MoveTo`).
- Ä111 (Slice 70): Ein Klick auf einen bestehenden Termin im Kalender schließt die offene, ausgefüllte Maske „Neuer Termin“ weiter ohne Rückfrage (`Open(item)` ruft `ClosePanels`); nur Kreuz, Esc und Strg+W fragen. Dasselbe gilt für Rechtsklick „Pause/Sperrzeit/Notiz hier“, Taste B und Taste N ohne Spalte unter dem Mauszeiger: sie ersetzen die Terminmaske ohne Rückfrage. Slice 71 fasst das Klickverhalten im Kalender an.
- Ä112 (Slice 70): Beim Verlegen bleibt die Reihenfolge der Behandlungsliste vom ersten Slot (Hinweise „passt genau“ und „länger als die freie Zeit“ gelten dann für den alten Slot); die Vorschau rechnet die Passung neu. Ein Datum, an dem die gewählte Person keinen Dienst hat, lässt die Vorschau verschwinden und umgeht die Rückfrage „Person ohne Dienst“ (Fund des Prüfers).

- Slice 69 (Ä87, Ä90): Die Ursache für den fremden Termin und die gefilterte Leiste „Meldungen“ ist der Reiter-Mechanismus der Seitenleiste, nicht der Schließtag: Schließt das Speichern die Eingabe, zeigt `RemoveTabAndShowNext` den Nachbar-Reiter (zuletzt geöffneter Termin, Leiste mit altem Filter „Hamam“). Das traf jeden Tag; an Schließtagen fiel es auf. Die Notiz stand am Schließtag schon im Kalender, sobald Slice 68 das Eintragen erlaubte.

- Slice 71: Solange der Kalender die Maus beim Ziehen eingefangen hat, meldet Avalonia kein `PointerExited`; ein Ziehen lief deshalb außerhalb des Kalenders weiter und legte beim Loslassen ab (Ursache von Ä93). Der Abbruch prüft jetzt bei jeder Bewegung die Lage zum sichtbaren Ausschnitt `BodyScroll`.
- Slice 71: Die Zeiten einer Belegung (`ResourceBooking.From`, `Until`) sind `init`-Eigenschaften; das Ändern einer Sperrzeit läuft deshalb über `ExecuteUpdateAsync`, die Ausschlussbedingung der Datenbank fängt ein gleichzeitiges Belegen weiter ab.
- Slice 71: Die gestrichelten Platzhalter mit Uhrzeit („11:30“, „11:45“, „19:00“) sind die Viertelstunde unter dem Mauszeiger (`Hover`): `Rebuild` ließ sie ohne offenen Eintrag stehen, bis sich die Maus wieder bewegte.
- Ä131 (Slice 77): Die Räume stehen nach Kategorie sortiert, aber ohne eigene Gruppenüberschrift über den Spalten; die Kategorie steht nur im Hinweis am Spaltenkopf. Die Stammdaten kennen keine Raumkategorie (nur Behandlungsraum und Bereich); eine echte Kategorie je Raum (etwa „Massage“, „Kosmetik“) wäre eine Änderung der Raumverwaltung, die diese Slice nicht baut.
- Ä132 (Slice 77): Der Zustand „Nur verfügbare Räume“ wird nicht gespeichert (anders als „Alle zeigen“ bei den Personen) und gilt nur in der Raumansicht des Tages; „nicht verfügbar“ heißt: die Raumsperren decken die ganze Öffnungszeit ab. Ein Raum mit Sperre nur am Nachmittag bleibt sichtbar. Der Auftraggeber entscheidet, ob er auch teilweise gesperrte Räume ausblenden oder den Zustand je Gerät merken soll.
- Ä133 (Slice 77): Die Eintritte von Sauna und Ruheraum sind Blöcke ohne Bearbeitung im Kalender: kein Ziehen, kein Stornieren per Klick, keine Raumpflege-Streifen; Bearbeiten geht weiter über das Fenster „Sauna-Eintritt“ und die Seitenleiste „Sauna und Ruheraum“. Überlappende Eintritte stehen in Bahnen nebeneinander.
- Ä137 (Slice 79): Der Hinweis „Alle zeigen“ wiederholte die Beschriftung und zählte Gründe auf; er war einer von rund zwanzig Tooltips der Windows-App mit mehr als einem Satz, einer Aufzählung oder dem Namen des Elements davor (Kopfchips, Kundenkarte, Suchfeld, Zoom, Bogenhinweis). Ein Knopf nur mit Symbol (Notiz ändern, Kopieren, Geburtstag) behält seinen Namen als Tooltip, weil der Tooltip dort die Beschriftung ist (T22).
- Ä138 (Slice 79): Die Sammelbegriffe „MA“, „Personen ohne Dienst“, „Therapeuten“ und „Mitarbeiter“ standen schon fast nirgends mehr (Slice 75 bis 77 schrieben „Alle Mitarbeitenden“); übrig waren der Hinweis von „Alle zeigen“, zwei Stellen in `seite-kalender.md` und eine Meldung des Servers („Für Therapeuten …“, jetzt „Therapeut:innen“); `docs/design.md` zitiert Wortlaut des Auftraggebers „alle MA“ und bleibt.
- Ä139 (Slice 79): Die Web-Oberfläche hat nur sieben `title`-Hinweise („Hilfe“, „Weitere Aktionen“, „Entfernen“, die Antwortbewertung); alle sind kurz, ein Test hält sie so. „Mitarbeiter:in“ (die einzelne Person im Terminpanel, `seite-kalender.md`) blieb, weil es nicht der Sammelbegriff ist.

## Zurückgestellte Kleinigkeiten
- Slice 65 (Durchsicht): Die Tooltips an Sprachkürzel (Dialog „Termine absagen“, Mitteilungstabelle), KI-Pille und Betriebsname machen reinen Text fokussierbar (zusätzlicher Tab-Stopp) und wiederholen teils Text, den ein Screenreader schon liest (`stock-sr-only`, verstecktes „erstellt von der KI“); im Dialog steht das Ziel im `<label>` der Auswahl (Tippen schaltet um). Der Auftrag der Slice verlangt den Tooltip an genau diesen Stellen; Alternative wäre sichtbarer Text („Deutsch“). Der Auftraggeber entscheidet beim Test.
- Slice 60: Felder mit Hilfe-Knopf neben der Beschriftung (`.field` mit `.field-head` in `TeamTab` und `TreatmentsTab`, gebaut in Slice 39/48) haben schon `htmlFor` und bleiben; `FormField` hat keinen Platz für den Knopf. Hilfetexte unter dem Feld (`hint`) bekamen die Reiter nicht, weil das neue Texte wären (Hilfe-Knöpfe tragen sie schon).
- Slice 60: Knöpfe ohne `type` außerhalb eines `<form>` in `admin/` (Listen links, „Speichern“ der übrigen Reiter) bleiben unverändert; der Prüftest verlangt `type` nur im `<form>`. Mehrere Reiter ohne Pflichtfeld (Erinnerungen, Kassa, Zahlungen, KI, Vorbereitung) haben nur `FormField`, kein `<form>`: es gibt nichts zu prüfen, was zum ersten Fehler springen könnte.
- Slice 60: Das Aussehen der umgestellten Felder (Raster `form-grid`, Breite, Abstand) ist nur in den Tests, nicht im Bild geprüft; die Regel `.form-grid > …` richtet jetzt auch `.form-field` unten aus.
- Slice 60 (Durchsicht): „Neues Startpasswort“ und das Empfangsgerät in `StaffPage` stehen als `FormField` in der Aktionszeile (Beschriftung über dem Feld statt daneben); `aria-describedby` steht in `RoomChecklist`, `StaffPage` (Befristung) und `OnlineRulesSection` (Ausfallgebühr) nach `{...p}` und überschreibt es, folgenlos solange diese Felder weder Hinweis noch Fehler am `FormField` tragen; der Sprung des Fokus bei Behandlungen aus einem anderen Bereich als „Grunddaten“ ist nicht eigens getestet; `FormGroup` in den Firmendaten erbt `font-size: 12px` von `.admin fieldset` (Bild gegen den alten Stand prüfen).
- Slice 73: Ein Rennen zwischen „Nicht senden“ und dem Versand in derselben Sekunde ist nicht abgesichert; ein 10-Minuten-Test der echten Zustellung durch Wolverine fehlt (Framework-Verhalten angenommen); „Jetzt senden“ kurz vor der Sendezeit kann doppelt senden (kein Zustand „wird gesendet“); keine 403-Tests der neuen Endpunkte; der Fehler am Feld der Wartezeit wird nicht angesagt (Fokus bleibt am Knopf).
- Slice 74: Ein Rennen zwischen „Stornierung zurücknehmen“ und dem Versand der Absage in derselben Sekunde ist nicht abgesichert; keine 403-Tests der neuen Endpunkte; `GET …/restoration` antwortet für eine unbekannte Id mit 200 und „keine Nachricht“ statt 404; die Karte „Verschieben“ ist mit der alten Zeit vorbelegt und meldet bei unveränderter Zeit wieder den Konflikt; Zurücknehmen und Nachrichtenänderung laufen nicht in einer Transaktion; ein vorgemerkter Statuswechsel ohne Verbindung bekommt kein „Rückgängig“; die Handbuchbilder (`status-setzen-3`, `termin-stornieren-*`) sind nicht neu erzeugt, sie zeigen „Mehr“ und das Panel ohne die neuen Knöpfe.

- Slice 61: Der Wortlaut „Die beiden Passwörter stimmen nicht überein. Tippe sie gleich ein.“ ersetzt „Die beiden (neuen) Passwörter stimmen nicht überein.“; die Passwortregel (mindestens 8 Zeichen) steht nun im Browser und auf dem Server, der Server bleibt maßgeblich.
- Slice 61: Das Aussehen der Felder in Anmeldung und Betreiber ist nur über Tests, Build und Bundle geprüft, nicht im Bild.
- Slice 61 (Durchsicht): Serverfehler beim Sperren und Zuordnen stehen am Feld, obwohl masken.md R8 für „abgelehnt/nicht erreichbar“ eine Meldung über der Aktionsleiste vorsieht (die Abnahme verlangt es für „Nummer zuordnen“); Klammerzusätze („mindestens 8 Zeichen“, „sieht das Team“) gehören nach R6 in `hint`; „Bitte wähle jetzt …“ und die Anrede Du/Sie in den Anmeldemasken bleiben Altbestand; der Prüftest zählt die `<form>` nicht gegen die Treffer.
- Slice 62: Das Aussehen der umgestellten Felder (Dienst-Zeilen im Tagespanel, Beginn/Ende im Nachtrag, Raster der Berichte) ist nur über Tests und Build geprüft, nicht im Bild.
- Slice 62: Hygiene zeigt den Sperrgrund an jedem Abnahme-Knopf einzeln (ein Satz je Knopf), statt einmal je Block; so steht er beim Knopf, wie die manuelle Prüfung es verlangt.
- Slice 75 (Durchsicht): „Termin stornieren …“ und „Einchecken zurücknehmen“ sind wie die Hauptknöpfe aus, solange eine Karte (Zeiten, Notiz, Serie, Verschieben) offen ist; „Buchen“ der Warteliste ist gesperrt, solange „Abschließen“ offen ist (ein Primärknopf je Eintrag).
- Slice 75: Die Handbuchbilder `kalender-warteliste.png` (Knopf „Erledigt …“) und `status-setzen-3.png` (Mehr aufgeklappt) zeigen noch den alten Stand und sind nicht neu erzeugt (wie bei Slice 74).
- Slice 80: Die Preise für die Tokenkosten sind Beispielwerte (nicht abgerufen); die Rechtsfrage zu Belegen in anderen Sprachen, die Übersetzungsgüte einer lokalen KI und die Dauer der muttersprachlichen Durchsicht sind nicht geklärt. Das Zählskript liegt nur im Anhang der Vorlage und nicht als Datei im Repository.
- Slice 58: Die nginx-Konfiguration ist nur als Text geprüft (`BookingEmbeddingTests`), nicht gegen einen laufenden nginx; der Ablauf `auth_request` → API → Header ist im manuellen Test zu sehen (`docker compose up`). Der Browserlauf mit einem echten `iframe` steht in der manuellen Testliste.
- Slice 58: Ohne `?spa=` im Mehr-Spa-Betrieb (Seitenneuladen unter `/buchen/<Pfad>` ohne Kennung) gilt `'self'`; die Einbettungsadresse muss `?spa=` tragen (Hilfetext). Eine Erkennung über den Verweis (`Referer`) wäre unsicher und ist nicht Teil der Slice.
- Slice 58: Kein Handbuchbild zum neuen Abschnitt (ein Textfeld, Hilfeabschnitt `firmendaten#einbetten` vorhanden).

- Slice 57: „Termine absagen?“ lädt die Liste nach dem Öffnen; bis dahin hat „Abbrechen“ den Fokus, danach wandert er einmal ins erste Feld („Betreff“), solange niemand ihn bewegt hat (`MutationObserver` im Baustein). Der Brief nennt „das Feld“; dort gibt es Betreff und Nachricht, gewählt ist das erste.
- Slice 64: Der Fokus beim Öffnen liegt auf dem ersten Steuerelement des Fensters (Vorgabe von `showModal()`: der Schließen-Knopf); eine gezielte Startfokus-Regel wie bei der Rückfrage ist nicht verlangt.
- Slice 64: Die Druckvorschau selbst (Strg+P) ist nicht geöffnet worden, nur die Druckregeln als Bildschirmansicht; die manuelle Testliste enthält die Prüfung.
- Slice 67: Die Tab-Prüfung läuft in jsdom über einen eigenen Helfer, nicht über echtes Tab im Browser; ob der Fokusring sichtbar ist, zeigt nur die manuelle Testliste (Slice 43 deckt die Marke ab).
- Slice 67: Der bestehende Test `ComplianceView.test.tsx` („zeigt ohne das Recht …“) griff die Statusregion sofort ab; sie zeigt jetzt zuerst „Lade Pflichtberichte …“. Die Erwartung wartet nun auf den Hinweistext (`waitFor`); geprüft wird dasselbe, nichts abgeschwächt.
- Slice 67: Die Tests fanden fünf Lücken, alle behoben: die Meldungen von Hygiene („Abgenommen.“), Behandlungskarte („Notiz gespeichert.“), TSE-Export und der Hinweis „Beträge nur mit Berechtigung“ (Management, Pflichtberichte) wurden erst mit der Meldung in den Baum gesetzt; ebenso „Lade …“ und „Die KI fasst den Tag zusammen …“ im Tagesstart. Jetzt steht je Ansicht ein `role="status"`-Element von Anfang an da. Rot-Lauf: 6 der 14 neuen Tests rot vor der Umsetzung (alle Statusfälle), die Tab-Reihenfolge war schon richtig (8 grün).
- Slice 67: Neuer Befund B120 – `ManagementView` und `DayPlanView` zeigen beim Wechsel von Standort, Zeitraum oder Person keinen Ladezustand (die alte Tabelle bleibt stehen, bis die neue da ist), und niemand sagt den Wechsel an; zurückgestellt, weil ein Ladezustand für alle Berichte Gestaltung braucht (Brief 5.x).
- Slice 67: Neuer Befund B121 – `BriefingView` (Bereitstellen abhaken) ignoriert eine abgelehnte Anfrage still; die Hygiene zeigt Fehler in der Statusregion, der Tagesstart nicht. Zurückgestellt, weil die Fehlermeldungstexte nach `docs/gestaltung/meldungen.md` eine eigene Abnahme brauchen.
- Slice 63: Serverfehler unter Aktionsleisten (Kassieren, Stornieren, Eintritt buchen, Notiz) bleiben `role="alert"` unter dem Panel, weil sie die Aktion und kein Feld betreffen.
Slice 68: Hinweis in den Masken Sperrzeit, Notiz und Pause (Ä105); Gruppenbuchung am Schließtag (Ä104); Wartezeiten in Tests wurden nicht verlängert.

- Slice 81: Der Vorschlag ist weder in `work/conventions.md` noch im Rundenabschluss eingetragen (Plugin `vibe-coding` unberührt); der Prüftest `formCoverage.test.ts` wird erst nach einer Probe-Runde der Durchsicht entschieden.
- Slice 72: Das Handbuchbild `kalender-konflikt.png` ist neu erzeugt (mit „Zum Konflikt“); die Meldungsorte nennt die Hilfe in Worten, ein Bild der Verschieben-Karte mit Meldung gibt es nicht.
- Slice 72 (Durchsicht): „Verschieben“ im Termin-Panel hat noch keinen Knopf „Zum Konflikt“ (Ä117); `NextFreeRoomStart` läuft bei jeder abgelehnten Planung auch dort, wo Konflikte verworfen werden (Leistung, nicht gemessen); die Zeitangabe „Ein Raum ist frei ab“ rechnet keine Pausen der Öffnungszeit und keinen Folgetag; „Zum Konflikt“ lässt offene Panels stehen (anders als „Alle Termine des Gastes“); die Beispieldaten (Elif Demir, Mittwoch 11:45) sind nicht nachgestellt, nur der Fall im Testmandanten, die Kosmetikkabine als einziger Gesichtsraum in Wien stammt aus `DemoData`, eine Raumsperre in den Beispieldaten ist nicht geprüft.
- Slice 81: Der Vorschlag ist weder in `work/conventions.md` noch im Rundenabschluss eingetragen (Plugin `vibe-coding` unberührt); der Prüftest `formCoverage.test.ts` wird erst nach einer Probe-Runde der Durchsicht entschieden.- Slice 76: Die alte Zeile „Überfällig“ im Tagesüberblick (Klick hebt die überfälligen Termine hervor) bleibt als Kopf des neuen Abschnitts; die Liste öffnet jeden Termin einzeln.
Slice 70: Vorschlag der Person als Knopf (Ä110), Rückfrage beim Klick auf einen Termin (Ä111), Neusortierung der Behandlungsliste beim Verlegen (Ä112); das Handbuchbild `termin-buchen-3` zeigt noch die alte Reihenfolge der Felder und ist nicht neu erzeugt.

Ä107 (Slice 69): Auch „Termin buchen“ und „Gemeinsam buchen“ schließen ihre Eingabe über den Nachbar-Reiter; ein früher geöffneter Termin oder eine Leiste mit altem Filter kann danach aufgehen. Gleiche Ursache, gleicher Handgriff (`CloseWithoutNextTab`), nicht Teil der Slice.
Ä108 (Slice 69): Der Fokus nach dem Speichern wird nicht gezielt gesetzt; er geht an den Kalender zurück (bestehender Weg). Ein Fokus auf den neuen Zettel ist nicht gebaut.
Ä109 (Slice 69): „Notiz gespeichert“ erscheint auch, wenn ein Filter (Person ausgeblendet) den Zettel im Kalender verbirgt; nicht geprüft.
- Slice 78: Der Zwischenspeicher steht im Speicher eines Server-Prozesses; bei mehreren Instanzen und vor allem beim Abgleich des Einzelplatzes (`SyncRunner.ApplyAsync` schreibt Schließzeiten und Öffnungszeiten am Speicher vorbei) gilt eine Änderung dort erst nach höchstens 5 Minuten für die schnelle Ablehnung (gebucht wird nie gegen ihn). Steht in „Neuer Termin“ schon eine andere Meldung (z. B. Fehler beim Anlegen des Kunden), erscheint „geschlossen“ erst bei der nächsten Eingabe oder beim Klick auf „Termin buchen“. Die Abschaltung der Vorabprüfung in der 7-Tage-Ansicht hat keinen eigenen Test (Durchsicht). Liegt die Zeit außerhalb der Öffnungszeit und hat die gewählte Person „Nicht bei mir“ gesetzt, nennt die Antwort jetzt zuerst „geschlossen“. Das Handbuch ändert sich nicht (kein neuer Ablauf); Wartezeiten in Tests wurden nicht verlängert.

Ä113 (Slice 71): Ganze Notizen und Pausen lassen sich nicht verschieben, nur ihre Ränder; andere Sperrzeiten (Arzt, Schulung) haben keine ziehbaren Ränder. Der Endpunkt `PATCH /api/blocks/{id}` könnte beides tragen.
Ä114 (Slice 71): Die Vorschau von Paar/Spa-Tag, Pause, Sperrzeit und Notiz im offenen Fenster ist nicht ziehbar, nur die des Termins. Der Klick auf einen bestehenden Termin bei offener Terminmaske (Ä111) ist unverändert.
Ä115 (Slice 71): Beim Ziehen am oberen Rand oder an einer Vorschau scrollt der Kalender nicht von selbst mit; wer über den sichtbaren Ausschnitt hinaus will, bricht ab (Ä93) und scrollt vorher. Ein Ablegen der Vorschau auf eine Person ohne Dienst fragt wie der Klick (Rückfrage „Person ohne Dienst“).
- Sauna und Ruheraum (Slice 77): die Spalte öffnet beim Klick das Fenster „Sauna-Eintritt“ ohne die angeklickte Uhrzeit vorzubelegen (Beginn und Ende kommen wie bisher aus dem Fenster); die Vorschau folgt den Zeiten im Fenster.
- Slice 79: Der Hinweis des Schließzeit-Chips (`ClosureChipTip`) hat mehrere Sätze („Geschlossen: Betriebsurlaub, Mo. 1.3. bis Mi. 3.3. Wegen Betriebsurlaub geschlossen. 1 Termin noch offen.“); `ClosureTests` legt den Wortlaut fest, und er trägt Daten der Sperre. Er ist ein Hinweis aus Daten und fällt nicht unter den festen Test; Kürzen nur mit Zustimmung des Auftraggebers.
- Slice 79: Hinweise, die erst zur Laufzeit aus Daten entstehen (Notizen, Gründe der Spalten, Kartenzeilen), prüft der Test nicht; die Handbuchbilder sind nicht neu erzeugt (kein Bild zeigt einen der geänderten Hinweise).

## Offene Entscheidungen
NEEDS-DECISION: Mehrsprachigkeit (Ä103, Slice 80): Weg B (Wörterbücher im Build erzeugen und prüfen) bestätigen oder Weg A (erster Start auf dem Rechner des Spas) wählen?
NEEDS-DECISION: Mehrsprachigkeit (Ä103, Slice 80): Welche Sprachen zuerst (Englisch, danach Französisch und Italienisch?), nur Oberflächen oder auch Hilfe und Handbuchbilder, Sprache je Benutzerin oder je Standort, und wer gibt Pflichttexte frei?

NEEDS-DECISION: Eingabeprüfung (Ä98, Slice 81): Soll der Schritt „Eingabeprüfung“ (Durchsicht der geänderten Masken, später Prüftest) in den Rundenabschluss aufgenommen werden, und zwar zuerst nur die Durchsicht für R04?

## Manuelle Testliste
### Slice 57 – Rückfragedialog: Startfokus nach Brief 5.27
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), in der Verwaltung Aktionen öffnen
1. Bei einer laufenden Aktion „Archivieren“ drücken
Expected: Die Rückfrage „Aktion archivieren?“ öffnet mit dem Fokus auf „Archivieren“
2. Esc drücken
Expected: Die Rückfrage schließt, die Aktion bleibt laufend
3. Bei einer Schließzeit „Löschen“ drücken
Expected: Die Rückfrage öffnet mit dem Fokus auf „Abbrechen“
4. Bei einem Termin mit Anzahlung „Anzahlung retournieren …“ drücken
Expected: Der Fokus liegt im Feld „Grund“
Closes: Ruling Slice 38 (R02)
Result: ☐

### Slice 58 – Kundenbereich auf der eigenen Website einbetten
Start: App wie in work/conventions.md starten (Docker Desktop, `dotnet run --project src/AispaStudio.AppHost`), als Inhaberin anmelden
1. In der Verwaltung unter „Stammdaten“ › „Firmendaten“ zum Abschnitt „Kundenbereich einbetten“ gehen und das Fragezeichen neben „Einbetten erlaubt auf“ öffnen
Expected: Die Hilfe erklärt Format, Beispiele und was nicht erlaubt ist
2. `http://www.beispiel-spa.at` eintragen und „Speichern“ wählen
Expected: Am Feld steht ein Fehler mit dem Grund („Nur Adressen mit https:// sind erlaubt …“), der Fokus steht im Feld, nichts ist gespeichert
3. `https://www.beispiel-spa.at/buchen` eintragen und „Speichern“ wählen
Expected: Am Feld steht ein Fehler, der den Pfad als Grund nennt
4. Den Eintrag durch `https://localhost:8443` ersetzen, „Speichern“ wählen und die Seite neu laden
Expected: „Gespeichert.“ erscheint, nach dem Neuladen steht die Adresse im Feld
5. Eine Testseite mit `<iframe src="…/buchen/?spa=<Kennung des Spas>">` auf `https://localhost:8443` öffnen und die Browser-Konsole ansehen
Expected: Die Online-Buchung erscheint im Rahmen, die Konsole meldet keinen Verstoß gegen `frame-ancestors`
6. Dieselbe Testseite von einer nicht eingetragenen Adresse öffnen (z. B. `http://localhost:8000`)
Expected: Der Rahmen bleibt leer, die Konsole meldet, dass `frame-ancestors` das Einbetten verbietet
Closes: Ruling Slice 47 (R02)
Result: ☐

### Slice 59 – Quelle des Werts der Statusfarbe „Erledigt“
Start: Die Datei docs/design.md öffnen (keine App nötig)
1. Die Tabelle „2.4 Kalenderstatus“ lesen, Zeile „Erledigt“
Expected: Der helle Wert `#656c68` steht unverändert in der Zeile
2. Unter der Tabelle den Absatz zu „Erledigt“ lesen
Expected: Der Absatz nennt das Ruling vom 25.10.2026 (Rundenakte R02) und den Kontrast 4,6:1 auf `#efede8`
Closes: Brief-Vorgabe Slice 59 (R03)
Result: ☐

### Slice 60 – Formularbaustein in Stammdaten und Personal
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Web-Verwaltung öffnen
1. Stammdaten › Standorte: im Feld „Name“ den Text löschen und „Speichern“ wählen
Expected: Unter dem Feld steht „Der Name des Standorts fehlt. …“, der Cursor steht im Feld, nichts wird gespeichert
2. Stammdaten › Team: eine Person wählen, „Vollständiger Name“ leeren und „Speichern“ wählen
Expected: Der Fehler steht am Feld, der Cursor steht im Feld
3. Stammdaten › Firmendaten und Behandlungen (neue Behandlung ohne Namen): jeweils den Namen leer lassen und speichern
Expected: Der Fehler steht am Feld und der Fokus springt dorthin, auch wenn ein anderer Bereich der Behandlung offen war
4. Stammdaten › Standorte, Team, Behandlungen, Erinnerungen: Felder und Abstände ansehen
Expected: Beschriftung über dem Feld, Felder im Raster ohne Sprünge oder abgeschnittene Texte
Closes: B71, B76
Result: ☐

### Slice 61 – Formularbaustein in Anmeldung, Einrichtung und Betreiber
Start: Web-Verwaltung im Einzelplatz-Betrieb (leere Datenbank) bzw. Betreiber-Seite unter /betreiber/ öffnen
1. Einrichtung: alle Felder ausfüllen, bei „Passwort“ und „Passwort wiederholen“ verschiedene Werte eintippen und „Spa einrichten“ wählen
Expected: Unter „Passwort wiederholen“ steht „Die beiden Passwörter stimmen nicht überein. …“, der Cursor steht im Feld, keine Sprechblase des Browsers
2. Anmeldung: ohne Eingaben „Anmelden“ wählen
Expected: Unter „Benutzername“ und „Passwort“ stehen Fehler, der Cursor steht im Benutzernamen
3. Betreiber › Nummer zuordnen: Feld leer lassen und „Zuordnen“ wählen
Expected: Der Fehler steht unter dem Feld „Nummer“, der Cursor steht im Feld, keine Meldung unter dem Formular
4. Betreiber › Spa sperren: ohne Grund „Wirklich sperren“ wählen; Anmeldung ohne Schlüssel „Anmelden“ wählen
Expected: Der Fehler steht jeweils am Feld, der Cursor steht im Feld
5. Anmeldung, Einrichtung und Betreiber-Formulare ansehen
Expected: Beschriftung über dem Feld, Pflichtfelder mit Stern, roter Rand und Fehlertext nur bei Fehlern, nichts abgeschnitten
Closes: B72, B80
Result: ☐

### Slice 62 – Formularbaustein in Dienstplan, Zeiten, Ausfall-Assistent und Berichten
Start: Web-Verwaltung im Einzelplatz-Betrieb mit Beispieldaten; Schritt 1 als Rezeption, die übrigen als Inhaberin
1. Berichte › Hygiene als Rezeption (ohne Leitung) öffnen und „Tagesstart abnehmen“ per Tab erreichen
Expected: Der Knopf bekommt den Fokus und daneben steht sichtbar „Wählen Sie die Leitung und geben Sie ihre PIN ein.“; ein Klick tut nichts
2. Dienstplan: einen Tag öffnen, „Krank“ wählen, speichern, „Ersatz vorschlagen“, alle Termine abwählen und „Übernehmen (0)“ per Tab erreichen
Expected: Daneben steht „Wählen Sie mindestens einen Termin aus.“; der Knopf bleibt fokussierbar
3. Dienstplan: im Tagespanel „Krank“ wählen
Expected: Bei „Sofort gültig, ohne Veröffentlichen“ steht „Krankenstand gilt immer sofort.“
4. Dienstplan und Zeiterfassung: Tagespanel und „Zeit nachtragen“ ansehen
Expected: Beschriftungen über den Feldern (Dienst 1 Standort, Beginn, Ende, Notiz, Grund mit Stern), nichts abgeschnitten oder übereinander
Closes: B77, B78
Result: ☐

### Slice 63 – Formularbaustein in Kalender, Posteingang und Kassa
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Kalender öffnen
1. Kalender: auf eine freie Zeit klicken, „Kunde anlegen“ wählen und ohne Namen „Anlegen“ klicken
Expected: Unter „Vorname“ steht „Vor- oder Nachname angeben.“, das Feld „Vorname“ hat den Fokus und eine rote Umrandung
2. Im Termin-Panel „Notiz hinzufügen“, „Termin stornieren“ und „Ganze Buchung verschieben“ öffnen
Expected: Beschriftungen stehen über den Feldern (Notiz, Grund, Neuer Beginn), nichts ist abgeschnitten oder übereinander
3. Posteingang: eine Nachricht mit Antwortentwurf öffnen
Expected: „Betreff“ und „Antworttext“ stehen über ihren Feldern, die Antwort lässt sich bearbeiten
4. Kassa: eine Rechnung öffnen, Beleg ansehen, „Rechnung auf Firma“ öffnen
Expected: Name, Anschrift und UID haben Beschriftungen über den Feldern, die Maske sieht sonst unverändert aus
Closes: B84, B87
Result: ☐

### Slice 64 – Anamnese-Fenster als natives Dialogfenster
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), einen Termin mit Gast öffnen und den Abschnitt „Anamnese“ aufrufen
1. „Papierbogen drucken“ wählen
Expected: Das Fenster „Papierbogen“ öffnet über der Seite, der Hintergrund ist abgedunkelt und mit Tab nicht erreichbar
2. Esc drücken
Expected: Das Fenster schließt und der Fokus liegt wieder auf „Papierbogen drucken“
3. „Papierbogen drucken“ wählen und „Drucken“ drücken (Druckvorschau)
Expected: Die Vorschau zeigt nur den Bogen, so wie vorher, ohne grauen Hintergrund und ohne Fensterrand
Closes: B73
Result: ☐

### Slice 65 – Restliche Hinweise auf den Tooltip-Baustein, Kopieren mit Rückmeldung
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Web-Verwaltung öffnen
1. Personal › Einladen: Name und E-Mail eintragen, „Einladungslink erstellen“, dann „Einladungslink kopieren“ wählen
Expected: „Link kopiert.“ erscheint, der Link liegt in der Zwischenablage (in ein Textfeld einfügen)
2. Personal › Empfangsgerät: den Namen des Geräts leeren und den Knopf „Diesen Browser als Empfangsgerät anmelden“ mit Tab ansteuern
Expected: Der Knopf nennt „Name fehlt.“ als Grund und meldet nichts an
3. Schließtage › Mitteilungen: die Spalte mit Kanal und Sprachkürzel mit der Maus berühren und mit Tab ansteuern
Expected: Nach kurzer Verzögerung erscheint der Name der Sprache als Tooltip, auch per Tastatur
4. Kopfzeile: den Namen des Betriebs berühren; Assistent: die Pille „KI“ berühren
Expected: Der Betriebsname bzw. „Von der KI erstellt“ erscheint als Tooltip
Closes: B74, B75, B86
Result: ☐

### Slice 66 – Zustände im Kontrastdesign von Windows
Start: App wie in work/conventions.md starten, als Inhaberin anmelden; in Windows das Kontrastdesign einschalten (Einstellungen › Barrierefreiheit › Kontrastdesigns, z. B. „Aquatisch“)
1. Online-Buchung (Kundenbereich) öffnen, eine Leistung, einen Tag und eine Zeit wählen
Expected: Die gewählte Leistung, der gewählte Tag und die gewählte Zeit sind sofort erkennbar (Systemfarbe Highlight, kräftiger Rand)
2. In der Verwaltung den Kalender öffnen, „Wartelisten“ drücken
Expected: Der Knopf wirkt gedrückt (Highlight), ein zweiter Druck nimmt es zurück
3. Im Posteingang eine Nachricht wählen
Expected: Die gewählte Nachricht hat einen dicken Rand in Highlight
4. Im Kalender einen Termin wählen und einen eingecheckten und einen erledigten Termin ansehen
Expected: Gewählt zeigt einen Umriss, eingecheckt einen Rahmen, erledigt einen gestrichelten grauen Rahmen
5. Im Lager einen Reiter und eine Zeile wählen
Expected: Reiter und Zeile sind erkennbar gewählt
6. Kontrastdesign ausschalten; Kundenbereich, Mein Tag, Betreiber und Lager je in hellem und dunklem Modus ansehen (B83)
Expected: Text und Ränder sind gut lesbar
Closes: B82, B85, B83
Result: ☐

### Slice 67 – Tastaturtests für die Berichte
Start: App wie in work/conventions.md starten, als Inhaberin anmelden, Berichte öffnen
1. Nur mit der Tastatur: Tab durch „Mein Tag“ (Standort, Datum, Person, Vorbereitung, Vorlieben, Notiz speichern)
Expected: Der Fokus geht in Lesereihenfolge, ist immer sichtbar und landet nie auf etwas Verborgenem
2. Pfeiltasten wechseln den Reiter (Tagesstart, Hygiene, Management, Pflichtberichte); in jedem Tab durchgehen
Expected: Filter, Tabellenaktionen, Haken und Knöpfe sind erreichbar, der Fokusring ist sichtbar
3. Im Management den Zeitraum ändern und „Mit anderen Spas vergleichen“ öffnen, mit dem Screenreader zuhören
Expected: Meldungen (z. B. „Notiz gespeichert.“, „Abgenommen.“) werden angesagt, ohne dass der Fokus springt
Closes: B79
Result: ☐

### Slice 68 – Schließtage: die Rezeption darf alles eintragen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App im Aspire-Dashboard über `windows-app` starten, als Inhaberin anmelden
1. Im Kalender zu einem Sonntag wechseln (Standort ohne Öffnungszeit) und auf eine freie Stelle in einer Spalte klicken
Expected: Das Fenster „Neuer Termin“ öffnet; oben steht der Hinweis „Der Standort hat an diesem Tag geschlossen.“ und „Termin buchen“ ist nach Wahl von Gast und Behandlung aktiv
2. Gast wählen und „Termin buchen“ klicken
Expected: Der Termin steht im Kalender, eine Meldung nennt „Der Standort hat an diesem Tag geschlossen.“
3. Über „Neuer Eintrag“ eine Sperrzeit und eine Notiz am selben Sonntag eintragen
Expected: Beide werden gespeichert und stehen im Kalender
4. Sauna und Ruheraum öffnen und für den Sonntag einen Eintritt buchen
Expected: Der Eintritt wird gespeichert
5. Im Kundenbereich (Online-Buchung) den Sonntag wählen
Expected: Es gibt keine buchbaren Zeiten
Closes: Ä83, Ä89
Result: ☐

### Slice 69 – Notizen und Pausen an Schließtagen
Start: Docker Desktop starten, `cd web && npm install && cd ..`, `dotnet run --project src/AispaStudio.AppHost`, Windows-App im Aspire-Dashboard über `windows-app` starten, als Inhaberin anmelden
1. Im Kalender zu einem Sonntag wechseln, einen Termin oder die Leiste „Meldungen“ (mit Filter auf eine Person) öffnen und in einer Spalte „Notiz hier“ wählen
Expected: Das Fenster „Notiz“ öffnet mit der Uhrzeit der Stelle
2. Text „Handwerker kommen“, Uhrzeit 10:45, Bis 11:00 eintragen und „Notiz speichern“ klicken
Expected: Der Zettel steht im Kalender, die Meldung „Notiz gespeichert.“ erscheint, rechts öffnet sich kein Termin und keine Leiste
3. In einer Spalte „Pause hier“ wählen und „Pause eintragen“ klicken
Expected: Die Pause steht im Kalender, die Leiste „Meldungen“ geht nicht auf
4. Den Kalender neu laden (F5)
Expected: Zettel und Pause stehen weiter dort
Closes: Ä87, Ä90
Result: ☐

### Slice 70 – Neuer Termin: Kunde zuerst, Treffer übernehmen, Tag wählen, nichts verwerfen
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Auf eine freie Stelle klicken und im Panel „Neuer Termin“ in „Kundin oder Kunde“ „Demi“ eintippen
Expected: Das Panel beginnt mit dem Feld für den Kunden; darunter stehen Behandlung, Datum und Beginn, dann die Notiz; die Trefferliste zeigt „Tomasz Demir“
2. Den Treffer „Tomasz Demir“ anklicken
Expected: „Tomasz Demir“ steht im Suchfeld, darunter die Zeile des gewählten Kunden mit „Ändern“; die Trefferliste ist zu
3. Unter „Vorschläge“ bei „Zuletzt gebucht: …“ auf „Übernehmen“ drücken
Expected: Die Behandlung steht im Feld „Behandlung“, der Vorschlag verschwindet
4. In einer anderen Spalte auf eine andere freie Stelle im Kalender klicken
Expected: Das Panel bleibt offen, Name und Behandlung stehen noch, Datum und Beginn zeigen die neue Stelle, die gestrichelte Vorschau steht dort
5. Im Feld „Datum“ den nächsten Tag eintippen und mit Tab bestätigen
Expected: Der Kalender springt auf diesen Tag, die Vorschau steht dort; Name und Behandlung bleiben
6. Esc drücken
Expected: Die Rückfrage „Änderungen verwerfen?“ erscheint; „Weiter bearbeiten“ lässt das Panel offen
Closes: Ä82, Ä83, Ä85, Ä86
Result: ☐

### Slice 71 – Ziehen im Kalender: Vorschau, Notiz, Pause, Abbruch
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Einen gebuchten Termin mit der Maus fassen und ein Stück ziehen
Expected: Die gestrichelte Vorschau folgt der Maus, der Zeiger ist das Kreuz
2. Mit gedrückter Maustaste nach links über die Zeitleiste aus dem Kalender hinausfahren
Expected: Die Vorschau verschwindet, der Zeiger wird wieder zum Pfeil
3. Im Kalender loslassen
Expected: Der Termin steht an seinem alten Platz, es erscheint keine Meldung „Verschoben auf …“
4. Eine Person filtern (Klick auf den Spaltenkopf), auf eine freie Stelle am Montag klicken, einen Gast wählen
Expected: Das Fenster „Neuer Termin“ ist offen, die gestrichelte Vorschau steht am Montag
5. Die Vorschau auf Mittwoch 10:00 ziehen und loslassen
Expected: Im Fenster stehen der Mittwoch und 10:00, Gast und Behandlung bleiben, die Vorschau steht am Mittwoch
6. Den unteren Rand einer Pause 30 Minuten nach unten ziehen
Expected: Der Zeiger ist der Doppelpfeil, die Vorschau zeigt die neue Zeit; nach dem Loslassen steht „Pause geändert“ und die Pause ist länger
7. Den unteren Rand einer Notiz mit Uhrzeit 30 Minuten nach unten ziehen und danach in die Mitte des Zettels klicken
Expected: „Notiz geändert“ erscheint; im Fenster der Notiz steht unter „Bis“ die um 30 Minuten spätere Zeit
8. Das Fenster schließen und die Maus über dem Kalender stehen lassen, bis der Kalender neu lädt (F5)
Expected: Kein gestrichelter Platzhalter mit Uhrzeit bleibt stehen
Closes: Ä83, Ä88, Ä89, Ä93
Result: ☐

### Slice 72 – Meldungen im Kalender: ein Ort, Ursache, Weg zum Konflikt
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Einen Termin im Kalender auf die Zeit eines anderen Termins derselben Person ziehen
Expected: Der Termin springt zurück; die Meldung „Nicht verschoben“ steht über dem Kalender, nennt die belegende Person und den Raum und hat den Knopf „Zum Konflikt“
2. „Zum Konflikt“ drücken
Expected: Der Kalender zeigt die 7-Tage-Ansicht dieser Person ab dem Tag des Termins, der belegende Termin ist markiert
3. Im Termin-Panel „Verschieben …“ öffnen und einen belegten Tag mit Uhrzeit wählen, „Verschieben“ drücken
Expected: Die Meldung steht in der Karte „Verschieben“ direkt über dem Knopf und nennt Person und Raum
4. „Neuer Termin“ mit einer Gesichtsbehandlung von 90 Minuten zu einer Zeit buchen, in der die einzige Kosmetikkabine belegt ist
Expected: Über dem Knopf „Termin buchen“ steht „Zeitraum ist belegt.“ mit der Belegung der Kabine und der Zeile „Räume: Kein geeigneter Raum ist zu dieser Zeit frei. Ein Raum ist frei ab …“
Closes: Ä79, Ä80, Ä94
Result: ☐

### Slice 73 – Wartezeit vor der Nachricht an den Gast
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; im Browser die Verwaltung öffnen und als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Verwaltung › Stammdaten › Schließzeiten öffnen und oben das Feld „Wartezeit vor der Nachricht an den Gast (Minuten)“ ansehen
Expected: Das Feld zeigt 10; das Fragezeichen daneben öffnet die Hilfe zur Wartezeit
2. 61 eintragen und „Wartezeit speichern“ klicken
Expected: Die Meldung „Bitte eine ganze Zahl von 0 bis 60 eingeben.“ erscheint, nichts wird gespeichert
3. Eine Schließzeit mit einem Termin in mehr als 2 Stunden anlegen und „Termine absagen …“ bestätigen
Expected: Der Termin ist abgesagt; im Protokoll der Absagen steht „Nachricht an den Gast geht um … raus.“ mit der Uhrzeit in 10 Minuten und den Knöpfen „Jetzt senden“ und „Nicht senden“
4. Auf „Nicht senden“ klicken
Expected: Der Zustand heißt „nicht gesendet (zurückgenommen)“, die Knöpfe sind weg, in Mailpit (Port 8025) kommt keine Mail an, auch nicht nach 10 Minuten
5. Einen zweiten Termin absagen und auf „Jetzt senden“ klicken
Expected: Der Zustand wechselt auf „gesendet“, in Mailpit liegt die Absage
6. Eine Schließzeit mit einem Termin in weniger als 2 Stunden absagen
Expected: Die Nachricht geht sofort raus, im Protokoll steht kein „geht um … raus“
Closes: Ä95
Result: ☐

### Slice 74 – Rückgängig für Stornieren und Einchecken
Start: Docker Desktop starten, `cd web && npm install && cd ..`, dann `dotnet run --project src/AispaStudio.AppHost`; Windows-App über die Ressource `windows-app` starten und als Inhaberin anmelden (`inhaberin@demo.aispa.test`)
1. Im Tageskalender einen gebuchten Termin markieren, **Entf** drücken und die Rückfrage mit „Stornieren“ bestätigen
Expected: Unten steht „Termin storniert“ mit dem Knopf „Rückgängig“; in der Tastenleiste steht „Strg+Z Rückgängig“; der Termin ist aus dem Kalender verschwunden
2. **Strg+Z** drücken
Expected: Der Termin steht wieder an seiner Stelle und ist „Gebucht“; die Meldung lautet „Termin wieder gebucht …“; „Strg+Z Rückgängig“ ist aus der Tastenleiste verschwunden
3. Einen gebuchten Termin markieren, **E** drücken und danach unten auf „Rückgängig“ klicken
Expected: Erst „… eingecheckt“ mit „Rückgängig“, danach steht der Termin wieder auf „Gebucht“
4. Einen Termin einchecken, 10 Sekunden warten, den Termin anklicken und im Panel „Einchecken zurücknehmen“ wählen (seit Slice 75 ohne „Mehr“)
Expected: Der Termin steht wieder auf „Gebucht“
5. Einen Termin einchecken, kassieren und sein Panel öffnen
Expected: Im Panel gibt es kein „Einchecken zurücknehmen“ (auch nicht unter „Mehr“)
6. Einen Termin stornieren, in dieser Zeit für dieselbe Person und denselben Raum einen neuen Termin buchen, dann im Panel des stornierten Termins „Stornierung zurücknehmen“ wählen und bestätigen
Expected: Eine zweite Rückfrage nennt den Konflikt (Person, Raum, Zeit) und bietet „Verschieben …“ an; danach öffnet sich die Karte „Verschieben“ mit Tag und Beginn
7. Eine Schließzeit mit einem Termin absagen (Verwaltung › Schließzeiten), dann in der App den abgesagten Termin öffnen und „Stornierung zurücknehmen“ wählen
Expected: Die Rückfrage sagt, dass die Absage an den Gast noch nicht raus ist und nicht mehr gesendet wird; nach dem Zurücknehmen kommt in Mailpit (Port 8025) keine Absage an
Closes: Ä95, Ä97
Result: ☐

### Slice 75 – Ablauf im Termin-Panel und klare Warteliste
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Einen gebuchten Termin anklicken und das Panel ansehen, ohne „Mehr“ aufzuklappen
Expected: Unter dem Hauptknopf „Einchecken“ stehen „Verschieben …“ und rot „Termin stornieren …“; „Erledigt“ und „Nicht erschienen“ stehen erst unter „Mehr“
2. Auf „Termin stornieren …“ klicken, ohne vorher einzuchecken
Expected: Die Rückfrage „Termin stornieren?“ erscheint
3. Einen Termin einchecken und sein Panel ansehen
Expected: Hauptknopf „Kassieren“, darunter „Einchecken zurücknehmen“ und „Termin stornieren …“ (kein „Verschieben …“)
4. Die Warteliste öffnen und bei einem Eintrag „Anfrage abschließen …“ drücken
Expected: Die Auswahl „Grund“ nennt „Gebucht“, „Abgesagt“ und „Nicht erreichbar“, darunter steht ein erklärender Satz; der Knopf heißt „Abschließen“
5. „Gebucht“ wählen und „Abschließen“ drücken, danach die Warteliste neu öffnen
Expected: Der Eintrag steht nicht mehr in der Liste und nicht als „Wartet“ da
Closes: Ä77, Ä81
Result: ☐

### Slice 76 – Kalenderkopf, Tastenleiste, Zurücksetzen, Tagesüberblick
Start: App wie in work/conventions.md starten, als Inhaberin anmelden, Kalender öffnen
1. Auf den Tag im Kopf klicken, in der Auswahl einen anderen Tag wählen und die Auswahl erneut öffnen
Expected: Die Auswahl öffnet an derselben Stelle wie vorher, der Knopf wird bei kürzerem oder längerem Wochentag nicht breiter oder schmaler
2. Einen Termin anklicken und unten in der Tastenleiste „Esc Markierung aufheben“ anklicken
Expected: Die Markierung ist weg
3. Mit der Maus über „← → Tag“ fahren
Expected: Der Eintrag ist gesperrt und der Hinweis nennt den Grund
4. Einen Spaltenkopf (Filter „Elif Demir“) anklicken, dann Strg+R drücken
Expected: Der Filter ist weg, die Ansicht zeigt Team und heutigen Tag, der Eintrag „Strg+R Ansicht zurücksetzen“ verschwindet aus der Tastenleiste
5. Den Tagesüberblick öffnen, wenn überfällige Termine da sind, und einen Eintrag unter „Überfällig“ anklicken
Expected: Der Termin öffnet in der Seitenleiste; bei mehr als fünf steht „und n weitere“ über der Liste
Closes: Ä78, Ä96, Ä99, Ä100
Result: ☐

### Slice 77 – Raumansicht: Kategorien, gesperrte Räume, Sauna und Ruheraum
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Auf den Chip „Räume“ klicken
Expected: Zuerst die Behandlungsräume, am Ende „Finnische Sauna“ und „Ruheraum“ als Spalten; im Kopf der Sauna steht die Zahl der Plätze, im Hinweis „Sauna und Ruhe“
2. In Standorte und Räume den Hamam für heute ganztägig sperren (Reinigung), zurück in den Kalender
Expected: Die Spalte „Hamam“ ist schraffiert, in der Kopfzeile steht der Chip „Nur verfügbare Räume“
3. Den Chip „Nur verfügbare Räume“ klicken
Expected: Der Hamam verschwindet, der Chip heißt jetzt „Alle Räume“; ein zweiter Klick holt den Hamam zurück
4. In der Ansicht „Team“ einen Termin anfangen (Gast und Behandlung „Hamam-Ritual“ wählen) und auf „Räume“ umschalten
Expected: Der Hamam rückt ins Bild, die gestrichelte Vorschau steht in seiner Spalte und lässt sich ziehen
5. In die Spalte „Finnische Sauna“ klicken
Expected: Das Fenster „Sauna-Eintritt“ öffnet sich; mit Beginn und Ende steht die gestrichelte Vorschau in der Spalte der Sauna
Closes: Ä91, Ä92
Result: ☐

### Slice 78 – Prüfung beim Buchen schneller
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Zu einem Sonntag wechseln und auf eine freie Stelle in einer Spalte klicken
Expected: „Neuer Termin“ öffnet sofort mit dem Hinweis „Der Standort hat an diesem Tag geschlossen.“; „Termin buchen“ bleibt nach Wahl von Kunde und Behandlung aktiv
2. Zu einem Werktag wechseln, „Neuer Termin“ öffnen, Kunde wählen und als Beginn „19:45“ eintippen
Expected: Über „Termin buchen“ steht sofort „Der Standort hat zu dieser Zeit geschlossen.“, ohne Wartezeit
3. Beginn auf „10:00“ ändern
Expected: Die Meldung verschwindet
4. In der Verwaltung für den nächsten Montag eine Schließzeit ab 13:00 anlegen, zum Kalender zurück, den Montag öffnen, „Neuer Termin“ um 14:00 öffnen
Expected: Über „Termin buchen“ steht sofort „Der Standort ist in dieser Zeit geschlossen (Ausfall).“ bzw. mit der gewählten Art
5. Die Schließzeit in der Verwaltung auf Beginn 16:00 ändern, im Kalender den Montag neu laden (F5) und wieder „Neuer Termin“ um 14:00 öffnen und buchen
Expected: Keine Meldung, der Termin wird gebucht
Closes: Ä84
Result: ☐

### Slice 79 – Ein Begriff „Mitarbeitende“, Tooltips durchsehen
Start: App starten wie in work/conventions.md beschrieben (Inhaberin `inhaberin@demo.aispa.test`), Windows-App öffnen, Kalender
1. Im Kalender mit der Maus auf „Alle zeigen“ zeigen (einen Tag mit Mitarbeitenden ohne Dienst wählen)
Expected: Der Hinweis ist ein kurzer Satz: „Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund“
2. Auf „Alle zeigen“ klicken und noch einmal mit der Maus auf den Chip zeigen
Expected: Der Chip heißt „Nur mit Dienst“, der Hinweis lautet „Nur Mitarbeitende mit Dienst an diesem Standort“
3. Mit der Maus auf „Räume“, auf „Meldungen“ und auf das Suchfeld zeigen
Expected: Jeder Hinweis ist ein Satz ohne Aufzählung und wiederholt die Beschriftung nicht
4. Das Fenster schmal ziehen, bis die Chips nur noch Symbol und Zahl zeigen, und auf „Alle zeigen“ zeigen
Expected: Der Hinweis nennt zuerst den Namen des Chips, danach den Satz
Closes: Ä101, Ä102
Result: ☐

### Slice 80 – Mehrsprachigkeit: Entscheidungsvorlage mit Aufwand
Start: Die Datei docs/konzepte/mehrsprachigkeit.md öffnen (keine App nötig)
1. Den Abschnitt „1. Empfehlung“ lesen
Expected: Er nennt einen Weg (Wörterbücher im Build erzeugen), sagt, dass Pflichttexte nie von der KI kommen, und nennt die Zahl der Slices
2. Den Abschnitt „3. Bestand in Zahlen“ lesen
Expected: Die Tabelle nennt Zahlen für AXAML, C#, Web, Hilfe mit 189 Themen, API, Kundenbereich, Sprache je Gast, Kennzeichen am Termin und Handbuchbilder
3. Den Abschnitt „5. Drei Wege im Vergleich“ lesen
Expected: Drei Wege stehen in einer Tabelle mit denselben Kriterien, darunter steht die Antwort auf „beim ersten Start oder beim Build“
4. Den Abschnitt „7. Aufwandsschätzung in Slices“ lesen
Expected: Eine Tabelle zeigt die Slices mit Größe, Modell und Reihenfolge, darunter stehen Risiken und die Unsicherheit der Schätzung
5. Den Abschnitt „8. Offene Entscheidungen für den Auftraggeber“ lesen und die Fragen beantworten
Expected: Jede der fünf Fragen lässt sich mit einer klaren Antwort beantworten; die Antworten kommen als Ruling in die nächste Runde
Closes: Ä103
Result: ☐

### Slice 81 – Eingabeprüfung am Rundenende: Vorschlag lesen
Start: Kein Programm nötig, `docs/konzepte/eingabepruefung-rundenabschluss.md` öffnen
1. Abschnitt „Kurzfassung“ lesen
Expected: Die Frage „lohnt sich ein fester Schritt am Rundenende“ ist beantwortet (ja, aus eigenen Mitteln, nicht mit den beiden Skills)
2. Abschnitt 1 „Die beiden Skills“ lesen
Expected: Herkunft (Autor, Repository, Verzeichnis) und Inhalt beider Skills stehen da, samt dem Hinweis, dass der Wortlaut nicht lesbar war
3. Abschnitt 3 „Vorschlag“ lesen
Expected: Welche Prüfung, wann, mit welchem Ergebnisformat und mit welchem Aufwand ist beschrieben
Closes: Ä98
Result: ☐

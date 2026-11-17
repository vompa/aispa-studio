# Masken-Inventar: Web-Verwaltung → native Windows-App

> Slice 103 hat die Web-Team-Oberfläche und die eingebettete Verwaltung entfernt. Die Tabellen halten fest, wohin jede frühere Seite gewandert ist.

Stand: 01.11.2026, Runde R05, Slice 92 und 103 ([ADR 0084](../entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)).

Jede Seite und jeder Reiter der Web-Team-Oberfläche hat hier genau ein Ziel: die Slice der Runde R05, die ihn nativ in der Windows-App baut, „entfällt“ (nativ schon vorhanden) oder „bleibt“ (im Web). `web/src/maskInventory.test.ts` prüft seit Slice 103 nur noch den ADR-Index; die Seiten, gegen die er früher prüfte, gibt es nicht mehr.

Jede Slice hat ihren Bereich vollständig ersetzt, mit denselben Feldern, Regeln und Hilfetexten. Eingebettet öffnet die Windows-App nichts mehr.

## Seiten

| Seite | Reiter | Name in der Oberfläche | Ziel | Anmerkung |
|---|---|---|---|---|
| `calendar` | – | Kalender | entfällt | Kalender ist nativ (ADR 0053) |
| `roster` | – | Dienstplan | Slice 99 | Bereich „Arbeitszeit“ der Personenkarte (Abschnitt „Dienstplan und Abwesenheit“) und Ansicht „Wochenplan“ im Bereich Team (dort „Anträge“ als Seitenleiste, seit T-066); native Maske seit Slice 99 |
| `time` | – | Zeiten | Slice 99 | Bereich „Arbeitszeit“ der Personenkarte (Abschnitt „Zeiten“) und Ansicht „Zeiten“ im Bereich Team; native Maske seit Slice 99 |
| `inbox` | – | Posteingang | entfällt | Posteingang ist nativ |
| `pos` | – | Kassa | entfällt | Kassa ist nativ; Kassa-Einstellungen siehe Reiter `kassa` |
| `stock` | – | Lager | Slice 101 | nativ, Aufbau seit T-061 mit vier Reitern, Ansichten im Bestand und „Einrichtung“ (ADR 0098): Bestand, Bewegungen, Inventur, Lagerorte (Slice 101), Aufgaben, Lieferanten (Slice 112), Bestellvorschlag, Entwürfe und Senden (Slice 113), Lieferung buchen, Als bestellt markieren, Bestelltext, Bedarf und Chargen-Rückverfolgung (Slice 123); Lager-Verbesserungen danach in den Slices 112–116 |
| `promotions` | – | Aktionen | Slice 101 | nativ (Slice 101); seit 17.11.2026 (P4) drei Arten (befristet, kurzfristig frei, schwache Zeiten mit Wochenraster), „Neue Aktion“ fest oben, Vorschau des Gästepreises, Pausieren und Archivieren im Menü „⋯“, kein Löschen (ADR 0071 mit Nachtrag) |
| `reports` | – | Zahlen | Slice 102, R07 Slice 144 | nativ (Slice 102): Mein Tag, Tagesstart, Hygiene, Management mit Vergleich, Pflichtberichte; Balken im Vergleich offen (Ä226). Seit R07 Slice 144 (Ä352) eine Reiterebene: Tagesstart · Mein Tag · Hygiene · Berichte (Management, Pflichtberichte als Unterreiter) · Betriebsinfo „Mein Tag“ seit P9 (17.11.2026): Karte einspaltig bis 760 breit mit Kopf (Dauer, Raum, Status), Gerät abhakbar, Vorlieben eigener Abschnitt; Ort der Seite offen (Frage an den Auftraggeber). |
| `costs` | – | Kosten | Slice 102 | nativ (Slice 102) |
| `accounting` | – | Buchhaltung | Slice 102 | nativ (Slice 102); Exporte speichert die App unverändert |
| `payroll` | – | Lohn | Slice 100 | Bereich „Lohn und Akte“ der Personenkarte (Abschnitt „Lohn“) und Export im Bereich Team (umgesetzt in Slice 100) |
| `businessInfo` | – | Reiter „Betriebsinfo“ in Zahlen | Slice 102, R07 Slice 144 | nativ (Slice 102); seit R07 Slice 144 ein Reiter der Maske „Zahlen“, mit dem Recht auf Berichte mit Zahlen (`ViewFinance`) oder zu buchen (`Book`, seit T-025) |
| `masterData` | – | Stammdaten | Slice 93 | Verwaltungsgerüst; die Reiter verteilen sich wie unten |
| `staff` | – | Zugänge | Slice 98 | Bereich „Profil“ der Personenkarte (Abschnitt „Zugang und Rolle“); native Maske seit Slice 98, Bereiche seit T-065 |

## Reiter der Stammdaten

| Seite | Reiter | Name in der Oberfläche | Ziel | Anmerkung |
|---|---|---|---|---|
| `masterData` | `locations` | Standorte & Räume | Slice 95 | mit Raum-Checkliste |
| `masterData` | `closures` | Schließzeiten | Slice 95 | seit P2 (17.11.2026, Nachtrag zu ADR 0098) eine Liste für Schließzeiten und Raumsperren aus `closure-overview`; Formular als Seitenleiste; Wartezeit hinter „Einrichtung“ |
| `masterData` | `roomClosures` | Raumsperren | Slice 95 | seit T-064 (ADR 0098) auf der Seite „Schließzeiten“, seit P2 (17.11.2026) in deren gemeinsamer Liste; das Sprungziel bleibt und öffnet ein neues Formular „Einzelner Raum“ |
| `masterData` | `subscriptions` | Kalender-Abos | Slice 95 | Feiertage und Ferien (ADR 0075), keine Kunden-Abos |
| `masterData` | `team` | Team | Slice 98 | Personenkarte, Bereich Profil (Abschnitt Stammdaten); native Maske seit Slice 98, Bereiche seit T-065 |
| `masterData` | `treatments` | Behandlungen | Slice 94 | „Wer kann das?“ nur lesend mit Sprung ins Team; eigene Gruppe „Behandlungen“ im Balken (seit 17.11.2026); Bereiche Übersicht, Grunddaten und Preis, Durchführung, Qualifikationen und Räume; alte Abschnittsadressen bleiben gültig (`preise-und-verkauf` → Grunddaten und Preis) |
| `masterData` | `qualifications` | Qualifikationen | Slice 98 | Bereich „Profil“ der Personenkarte (Abschnitt „Qualifikationen“); native Maske seit Slice 98, Bereiche seit T-065 |
| `masterData` | `handbook` | Handbuch | Slice 97 | nativ als Rezeptionshandbuch mit Liste und Formular (Slice 97), daneben das Fragezeichen zur Hilfe (E1) |
| `masterData` | `reminders` | Erinnerungen | Slice 96 | seit P8 (17.11.2026): zwei Karten mit Standardabstand und Ausnahmen je Kategorie oder Behandlung (Tage leer = kein Hinweis), „Zuletzt verschickt“ ist das Versandprotokoll als Seitenleiste mit Filter, Zeitraum und Export (API `GET …/dispatch-log`); Nachrichten je Gast auf der Kundenkarte offen; Bereich steht nur bei eingeschaltetem Modul Erinnerungen oder Wiederbuchung, die Karte „Wiederbuchungs-Hinweise“ nur mit Modul Wiederbuchung, die Karte „Terminerinnerungen“ mit Versandprotokoll nur mit Modul Erinnerungen (T-081) |
| `masterData` | `intake` | Postfach & Nummern | Slice 97 | mit Absenderregeln; Bereich steht nur bei eingeschaltetem Modul Terminannahme (T-081) |
| `masterData` | `company` | Firmendaten | Slice 93 | erste native Maske, Vorbild der übrigen |
| `masterData` | `payments` | Zahlungen | Slice 96 | Schlüssel nur maskiert; Bereich steht nur bei eingeschaltetem Modul Online-Buchung (T-081) |
| `masterData` | `kassa` | Kassa | Slice 96 | RKSV/TSE unverändert |
| `masterData` | `ai` | KI | Slice 97 | |

## Sprungziele in die Verwaltung

Diese Stellen führten früher in die eingebettete Verwaltung an einer bestimmten Stelle. Jetzt öffnet jede eine native Maske. Ein Scan-Test (`ViewTests.JumpTargets`) und ein API-Test (`FieldMaskInventoryTests`) prüfen das gegen die Schlüssel in der Spalte „Ziel heute“.

| Von | Ziel heute | Nativ in |
|---|---|---|
| Kalender, Termin „Behandlungszeiten bearbeiten …“ | `masterData/treatments`, Bereich Durchführung bei der Gruppe Ablauf (Adresse „ablauf“, ADR 0098) | Slice 94 |
| Kalender, Schließtag | `masterData/closures` | Slice 95 |
| Kalender, Raum bearbeiten | `masterData/locations` | Slice 95 |
| Kalender, „Raum sperren …“ | `masterData/roomClosures` | Slice 95 |
| Kalender, Hinweise zum Lager | `stock` | Slice 101 |
| Termin: Kartengarantie, Stand der Anzahlung, Erinnerungsverlauf (bisher nur Team-Oberfläche) | Termin-Panel | Slice 124 |
| Kassa: Info zur Online-Rückerstattung, Gutscheinabfrage (bisher nur Team-Oberfläche) | Belege und „Bezahlen“ | Slice 124 |
| Kassa: Sofortüberweisung am Beleg (bisher nur Team-Oberfläche) | „Bezahlen“, Zahlungsart Sofortüberweisung | Slice 126 |
| Kalender: Lückenvorschläge des Tages (bisher nur Team-Oberfläche) | Menü „Lücken schließen“ | Slice 126 |
| Termin: Einwilligung für Wiederbuchungs-Hinweise (bisher nur Team-Oberfläche) | Termin-Panel | Slice 126 |
| Termin: Anamnese lesen, Tablet-Link, Papierbogen, Foto mit Prüfen und Bestätigen (bisher nur Team-Oberfläche) | Termin-Panel, Dialog „Papierbogen prüfen“ | Slice 127 |
| Assistent, „Jetzt ergänzen“ (`Api/Assistant/FieldMasks.cs`) | `masterData/locations` | Slice 95 |
| Assistent, „Jetzt ergänzen“ | `masterData/company` | Slice 93 |
| Assistent, „Jetzt ergänzen“ | `masterData/treatments` | Slice 94 |
| Assistent, „Jetzt ergänzen“ | `promotions` | Slice 101 |
| Assistent, „Jetzt ergänzen“ | `businessInfo` | Slice 102 |
| Assistent, Öffnungszeiten (`OpeningHoursAnswers.cs`) | `masterData/closures` | Slice 95 |
| Assistent, Schulferien (`SchoolHolidayAnswers.cs`) | `masterData/subscriptions` | Slice 95 |
| Assistent, „Im Bericht öffnen“ (`AnswerContext.cs`) | `reports/management` | Slice 102 |

Hilfethemen nennen ihre Seite (`seite:` im Kopf, z. B. `seite-time.md`); das ist kein Sprung in die Verwaltung und bleibt, solange die Hilfe die Seite beschreibt.

## Bleibt im Web

- **Hilfe und Handbuch:** Inhalte in `web/src/help/content/`, angezeigt im eingebetteten Browser der Windows-App (ADR 0079, E1): die Seite „Hilfe“ mit Verzeichnis und Suche und seit dem 17.11.2026 die Hilfe zu einem Fragezeichen in der Leiste „KI und Hilfe“ (`ansicht=leiste`, nur der Abschnitt; Kopf und Schließen nativ).
- **Kundenbereich** (`web/src/booking/`): wird die Handy-App für Kunden (Slice 108).
- **Mein Tag** (`web/src/myday/`): wird die Handy-App für Mitarbeitende (Slice 106) mit der Seite für die Leitung (Slice 109).
- **Dashboard für den Pausenraum:** neu (Slice 105).
- **Betreiberseite** (`web/src/operator/`): unverändert; sie dient dem Betrieb der Plattform und gehört nicht zur Verwaltung eines Spas.

## Schienen und Bereiche der Verwaltung

Ziel nach [ADR 0098](../entscheidungen/0098-verwaltungsmasken-eine-schiene-uebersicht-seitenleiste.md) (masken.md R15): je Seite höchstens eine Schiene mit höchstens fünf Einträgen. „Umgestellt“ nennt den Pull Request; leer heißt, die Maske zeigt noch den alten Stand.

| Gruppe | Schiene heute | Schiene neu | Umgestellt |
| --- | --- | --- | --- |
| Betrieb | 6 Masken | 5 Masken (Schließzeiten und Raumsperren auf einer Seite) | T-064, 16.11.2026 (PR offen); P2 (17.11.2026): eine Liste statt zwei Abschnitten, Formular als Seitenleiste, Nachtrag zu ADR 0098 |
| Angebot (seit 17.11.2026: Behandlungen und Aktionen als zwei Gruppen) | 2 Masken; Behandlung mit 7 Abschnittsreitern | 2 Gruppen mit je einer Maske, keine Reiterzeile; Behandlung mit 4 Bereichen (Übersicht · Grunddaten und Preis · Durchführung · Qualifikationen und Räume) | Schritt 3 (Behandlung, 16.11.2026); P3 (17.11.2026): acht Gruppen, Übersicht, Nachtrag zu ADR 0098; P4 (17.11.2026): Aktionen mit drei Arten, Nachtrag zu ADR 0071 |
| Team | 7 Ansichten; Personenkarte mit 10 Reitern | 5 Ansichten; Personenkarte mit 4 Bereichen, Verlauf als Seitenleiste | Personenkarte umgesetzt (T-065, 16.11.2026); Ansichten umgesetzt (T-066, 16.11.2026): Personen · Wochenplan · Zeiten · Lohn · Rechte und Zuständigkeiten, „Anträge“ als Seitenleiste im Wochenplan; Personenkarte und Liste neu geordnet (P5, 17.11.2026): fester Kopf mit Menü „⋯“, Abschnitte als Karten, Schalter und Chips, Regelarbeitszeit im Bereich „Arbeitszeit“, Liste mit festen Knöpfen, Standort-Chips und Kennzeichen nur bei Abweichung; Rechte und Zuständigkeiten als Tabelle (P11, 17.11.2026): eine Tabelle nach Themen mit Wirkung und Personen als Kennzeichen, „+ Person“ und „✕“ in der Zeile, darunter „Zertifikate und Liste der Zuständigkeiten“ |
| Lager | 9 Reiter | 4 Reiter (Übersicht mit Verbrauch, Bestand mit den Ansichten Material, Chargen und Bewegungen, Bestellen, Inventur), dazu die Seite „Einrichtung“ mit Lagerorten und Lieferanten | T-061, Schritt 2 (Pull Request offen); Export als Dialog statt Menü mit zehn Zeilen: P7, 17.11.2026 (Pull Request offen); Verbrauch als Tabelle bis 900 breit mit Abweichung in Menge und Prozent, Standard nur Abweichungen, Behandlungen zum Aufklappen: P6, 17.11.2026 (Pull Request offen) |
| Kassa & Geld | 4 Masken | 4 Masken | T-064, 16.11.2026 (PR offen): Warnungen oben |
| Kontakt | 3 Masken | 3 Masken | T-064, 16.11.2026 (PR offen): Erinnerungen 3 : 2, Warnung oben |
| Zahlen | 5 Reiter, „Berichte“ mit 2 Unterreitern | 5 Reiter, „Berichte“ mit 2 Abschnitten | T-063, 16.11.2026 (PR offen) |

## Anlegen und Entfernen (ADR 0099, P10a)

Stand: 17.11.2026. Bestandsaufnahme vor dem Muster aus [ADR 0099](../entscheidungen/0099-anlegen-entfernen-und-leerer-betrieb.md): „Neu“ fest über der Liste, Entfernen im Menü „⋯“ im Kopf der Karte, Löschen nur ohne Abhängiges, sonst „Nicht mehr verwenden“. Die Spalte „Lücke“ nennt, was fehlt; „P10b“ ist der Folge-PR dieses Punkts, „Maske“ der PR der jeweiligen Bahn.

| Objekt | Anlegen (App / API) | Löschen | Nicht mehr verwenden | Ort in der App | Lücke |
| --- | --- | --- | --- | --- | --- |
| Standort | „Standort anlegen“ fest über der Liste (P10b) / `POST api/locations` (Inhaber) | nur ohne Abhängiges (P10b): `GET …/deletion-check`, `DELETE api/locations/{id}`, sonst 409 mit Liste und dem Weg „Schließen ab Datum …“; der letzte Standort nie | „Schließen ab Datum …“ und „Wieder öffnen“ (P10b) / `POST api/locations/{id}/close` und `…/reopen`, neues Feld `ClosedFrom` | Betrieb › Standorte & Räume, Menü „⋯“ im Kopf der Karte | erledigt in P10b |
| Raum | Zeile „Neuer Raum“, jetzt über der Liste / `POST api/locations/{id}/rooms` | nie (Verweise von Terminen, Kosten, Sperrzeiten); `GET api/rooms/{id}/deletion-check` zählt (neu) | Menü „⋯“ der Zeile (neu) / `DELETE api/rooms/{id}` setzt `IsActive` aus, abgelehnt bei künftigen Terminen | Betrieb › Standorte & Räume › Räume | Wieder verwenden fehlt (`PUT` nur für aktive Räume): P10b |
| Gerät | Zeile „Neues Gerät“, jetzt über der Liste / `POST api/locations/{id}/equipment` | nie; `GET api/equipment/{id}/deletion-check` (neu) | Menü „⋯“ der Zeile (neu) / `DELETE api/equipment/{id}`, abgelehnt bei künftigen Terminen | Betrieb › Standorte & Räume › Geräte | Wieder verwenden fehlt: P10b |
| Person | „Person hinzufügen“ und „Person einladen“ fest über der Liste (P5) / `POST api/employees`, `POST api/staff/invitations`; eine Person ohne Zugang ist möglich | nein | „Aus dem Team nehmen …“ im Menü „⋯“ im Kopf der Karte mit Rückfrage (P5) / `DELETE api/employees/{id}` setzt `IsActive` aus, abgelehnt bei künftigen Terminen; ehemalige über `GET api/employees/former`; „Zugang sperren“ im selben Menü (Schalter „Zugang aktiv“, `PUT api/staff/{userId}`) | Team › Personenkarte | `deletion-check` mit gemeinsamem Dialog statt der einfachen Rückfrage: P10b |
| Behandlung | Anlegen in der Maske / `POST api/treatments` | nein | „Nicht mehr anbieten …“ im gemeinsamen Menü „⋯“ (P10b) und „Wieder anbieten“ im Kopf der Karte (P3) / `DELETE api/treatments/{id}` setzt `IsActive` aus, `POST …/reactivate` holt zurück; gebuchte Termine bleiben | Angebot › Behandlungen | erledigt (P3, P10b) |
| Lieferant | „Neu“ über der Liste (vorher letzte Zeile) / `POST api/stock/suppliers` | ja, wenn kein Material daran hängt (neu): `DELETE api/stock/suppliers/{id}`, sonst 409 mit der Liste; `deletion-check` (neu) | Menü „⋯“ (neu) / `PUT` mit `IsActive=false`; Bestellungen behalten den Namen | Lager › Einrichtung › Lieferanten | erledigt in P10a |
| Lagerort | „Neu“ über der Liste (vorher letzte Zeile) / `POST api/stock/locations/{id}/storage-places` | ja, wenn keine Bewegungen (vorher schon, jetzt mit `deletion-check`); Hauptlager nie | Menü „⋯“ (neu) / `PUT` mit `IsActive=false` (das Hauptlager nie) | Lager › Einrichtung › Lagerorte | erledigt in P10a |
| Material | Anlegen im Bestand / `POST api/stock/items` | nie (Lagerbewegungen und Rezepturen verweisen darauf) | „Nicht mehr verwenden“ und „Wieder verwenden“ im Menü „⋯“ der Karte (P10b) / `PUT` mit `IsActive` | Lager › Bestand › Material | erledigt in P10b |
| Qualifikation | Zeile in „Qualifikationen verwalten“ der Personenkarte / `POST api/qualifications` | ja, wenn niemand und keine Behandlung sie trägt (neu): `DELETE api/qualifications/{id}`, sonst 409; `deletion-check` (neu) | nein (kein `IsActive`) | Team › Personenkarte › Profil | Menü und Dialog in der App: P10b (Maske Personenkarte) |
| Aktion | „Neue Aktion“ über der Liste / `POST api/promotions` | nie (ADR 0071) | Archivieren und Zurückholen, umkehrbar / `POST …/archive`, `…/unarchive` | Angebot › Aktionen | bleibt beim Archivieren; Menü „⋯“ mit „Pausieren“ und „Archivieren“ (erledigt 17.11.2026) |
| Betriebskontakt | Anlegen in der Maske (darunter die Kategorie „Handwerker und Technik“) / `POST api/business-contacts` | nein | Archivieren und Zurückholen / `POST …/archive`, `…/unarchive` (`BusinessInfoViewModel.cs:878-938`) | Zahlen › Betriebsinfo | Handwerker bleiben Kategorie der Betriebsinfo (ADR 0099); Menü „⋯“ mit „Archivieren“ (erledigt 17.11.2026) |

Stand P10b (17.11.2026): Standort (Schließen ab Datum, Löschen nur ohne Abhängiges), Material („Nicht mehr verwenden“) und Behandlung (gemeinsames Menü) sind erledigt. Offen bleiben: Qualifikation „Löschen …“ in der Personenkarte und „Aus dem Team nehmen“ (Team, P5/P11), Wieder verwenden bei Räumen und Geräten. Aktion und Betriebskontakt haben ihr Menü „⋯“ seit dem 17.11.2026. „Neu“ über den übrigen Listen, die Leerzustände aller Seiten und der Test gegen leere API-Antworten sind in P10c erledigt (Abschnitt unten). Die Karte „Betrieb einrichten“ auf „Firmendaten“ steht (`GET api/setup/progress`).

Das Muster in der API ist `GET …/{id}/deletion-check` mit `DeletionCheckDto(CanDelete, Summary, Dependents, Alternative)`; die App zeigt es im gemeinsamen Dialog (`DeletionDialog`) und im Menü `MoreMenuButton`. In P10a ist es bei Lagerorten, Lieferanten, Räumen und Geräten eingebaut; die übrigen Masken folgen mit ihren PRs.

## Verwaltungsseiten: „Neu“ über der Liste und Leerzustand (P10c)

Stand: 17.11.2026, zweiter Teil von P10b. Bestandsaufnahme aller Seiten aus `AdminPageViewModel` gegen die Regel aus [ADR 0099](../entscheidungen/0099-anlegen-entfernen-und-leerer-betrieb.md): „Neu“ fest über der Liste, ein Leerzustand mit kurzem Satz und dem Knopf zum Anlegen als erster Aktion. „Vorher“ ist der Stand vor P10c, geprüft am leeren Betrieb (ein Standort, die Inhaberin, sonst nichts; `ViewTests.EmptyBusiness`); „jetzt“ der Stand nach P10c. Zeilenlisten innerhalb einer Karte (Arbeitszeiten, Preisregeln, Schritte, Mindestmengen, Ausnahmen der Wiederbuchungs-Hinweise) behalten ihre Zeile zum Hinzufügen unter der Liste ([Design-Brief, Zeilenlisten](../design.md)); die Regel gilt für Listen von Objekten, die eine Auswahl links und eine Karte rechts haben, und für ihre Formulare zum Anlegen.

| Gruppe › Seite | Liste | „Neu“ vorher | Leerzustand vorher | jetzt |
| --- | --- | --- | --- | --- |
| Betrieb › Firmendaten | keine; Karte „Betrieb einrichten“ | – | Karte zeigt den Stand (P10b) | unverändert |
| Betrieb › Standorte & Räume | Standorte | Knopf „Standort anlegen“ über der Liste (P10b) | nie leer (der letzte Standort bleibt) | unverändert |
| Betrieb › Standorte & Räume | Räume | Zeile über der Liste (P10a) | keiner | Satz „Noch kein Raum angelegt. Ohne Raum lässt sich nichts buchen.“ |
| Betrieb › Standorte & Räume | Geräte | Zeile über der Liste (P10a) | keiner | Satz „Noch kein Gerät angelegt.“ |
| Betrieb › Schließzeiten | Schließzeiten und Raumsperren | Knopf im Kopf der Seite | Karte mit „Schließzeit anlegen“ und „Raum sperren“ | unverändert |
| Betrieb › Kalender-Abos | Abos | Knopf „Neues Abo“ im Kopf | Satz „Es gibt noch kein Kalender-Abo.“ | unverändert |
| Betrieb › Handbuch | Einträge | Knopf über der Liste | Satz „Noch kein Eintrag im Handbuch.“ | unverändert |
| Behandlungen | Behandlungen | Knopf „Neue Behandlung“ über der Liste | **leere Seite** rechts | Karte „Noch keine Behandlung“ mit dem Satz und dem Knopf „Behandlung anlegen“ |
| Aktionen | Aktionen | Knopf „Neue Aktion“ über der Liste | Satz „Noch keine Aktion angelegt.“ | unverändert |
| Team (alle Ansichten) | Personen u. a. | Knöpfe über der Liste (P5) | Karte der Inhaberin | **nicht angefasst** (Maske Team, P5 und P11); in „Rechte und Zuständigkeiten“ stehen leer nur die Überschriften „Wer darf was?“ und „Zuständigkeiten“ ohne Satz: Sache von P11 |
| Lager › Bestand › Material | Material | Formular „Material anlegen“ unter der Liste, kein Knopf darüber | „Noch keine Einträge.“ | Knopf „Material anlegen“ im Kopf der Karte über der Liste (leert das Formular, Cursor in „Bezeichnung“), Satz „Noch kein Material angelegt.“ |
| Lager › Einrichtung › Lagerorte | Lagerorte | Knopf über der Liste (P10a) | nie leer (das Hauptlager gibt es immer) | unverändert |
| Lager › Einrichtung › Lieferanten | Lieferanten | Knopf über der Liste (P10a) | keiner | Satz „Noch kein Lieferant angelegt.“ |
| Lager › Übersicht, Chargen, Bewegungen, Bestellen, Inventur | nichts anzulegen | – | Sätze („Nichts zu tun …“, „Keine Aufgaben …“, „Noch keine Bestellungen.“) | unverändert |
| Kassa & Geld › Kassa, Zahlungen | keine Liste | – | – | unverändert |
| Kassa & Geld › Kosten | Räume, Personal | – (die Daten kommen aus Standorten und Team) | Überschrift mit Spaltenköpfen, kein Satz | Sätze „Noch kein Raum angelegt. Räume entstehen unter „Betrieb“ › „Standorte & Räume“.“ und „Noch niemand im Team. …“; Spaltenköpfe nur mit Zeilen |
| Kassa & Geld › Buchhaltung | Erlöskonten | Formular unter der Liste | – (drei Konten gibt es immer) | Formular „Erlöskonto anlegen“ über der Liste |
| Kontakt › Erinnerungen | Ausnahmen (Zeilenliste) | Auswahl „+ Ausnahme“ unter der Tabelle | Satz „Keine Ausnahmen …“ | unverändert (Zeilenliste) |
| Kontakt › Postfach & Nummern | Absenderregeln | Formular „Regel hinzufügen“ unter der Liste | Satz „Keine Regel. …“ | Formular über der Liste |
| Kontakt › Postfach & Nummern | Nummern | nur lesend (richtet der Betreiber ein) | Satz „Noch keine eigene Nummer. …“ | unverändert |
| Kontakt › KI | keine Liste | – | – | unverändert |
| Zahlen › Tagesstart, Mein Tag, Hygiene | Berichte, nichts anzulegen | – | Sätze („Nichts Dringendes offen“, „Keine Behandlungen heute“ …) | unverändert |
| Zahlen › Berichte (Management und Pflichtberichte) | Tabellen | – | Überschriften mit Spaltenköpfen, kein Satz | Sätze „Keine Umsätze im Zeitraum.“, „Keine Dienste im Zeitraum.“, „Keine Behandlungen im Zeitraum.“; Spaltenköpfe nur mit Zeilen |
| Zahlen › Betriebsinfo | Betriebskontakte | Knopf „Neuer Eintrag“ über der Liste | Satz „Noch nichts eingetragen. …“; eine leere Kategorie zeigte „Nichts gefunden.“ | Eine gewählte Kategorie ohne Eintrag (zum Beispiel „Handwerker und Technik“) zeigt „Noch nichts bei „<Kategorie>“. Mit „Neuer Eintrag“ beginnen.“; „Neuer Eintrag“ legt dann einen Eintrag in dieser Kategorie an |

Der Test `ViewTests.EmptyBusiness` hält das fest: er öffnet die Shell mit einem Standort und sonst leeren Antworten, wechselt in alle 33 Stellen der Verwaltung (alle Gruppen, Reiter, Ansichten des Lagers, der Zahlen und des Teams) und verlangt je Stelle keinen Absturz, keine Bindungsfehler, keine Fehlerleiste, keine Anfrage ohne vorbereitete Antwort und die Elemente und Sätze aus der Spalte „jetzt“.

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
| `promotions` | – | Aktionen | Slice 101 | nativ (Slice 101) |
| `reports` | – | Zahlen | Slice 102, R07 Slice 144 | nativ (Slice 102): Mein Tag, Tagesstart, Hygiene, Management mit Vergleich, Pflichtberichte; Balken im Vergleich offen (Ä226). Seit R07 Slice 144 (Ä352) eine Reiterebene: Tagesstart · Mein Tag · Hygiene · Berichte (Management, Pflichtberichte als Unterreiter) · Betriebsinfo |
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
| `masterData` | `closures` | Schließzeiten | Slice 95 | |
| `masterData` | `roomClosures` | Raumsperren | Slice 95 | seit T-064 (ADR 0098) der zweite Abschnitt der Seite „Schließzeiten“; das Sprungziel bleibt und rollt zum Abschnitt |
| `masterData` | `subscriptions` | Kalender-Abos | Slice 95 | Feiertage und Ferien (ADR 0075), keine Kunden-Abos |
| `masterData` | `team` | Team | Slice 98 | Personenkarte, Bereich Profil (Abschnitt Stammdaten); native Maske seit Slice 98, Bereiche seit T-065 |
| `masterData` | `treatments` | Behandlungen | Slice 94 | „Wer kann das?“ nur lesend mit Sprung ins Team |
| `masterData` | `qualifications` | Qualifikationen | Slice 98 | Bereich „Profil“ der Personenkarte (Abschnitt „Qualifikationen“); native Maske seit Slice 98, Bereiche seit T-065 |
| `masterData` | `handbook` | Handbuch | Slice 97 | nativ als Rezeptionshandbuch mit Liste und Formular (Slice 97), daneben das Fragezeichen zur Hilfe (E1) |
| `masterData` | `reminders` | Erinnerungen | Slice 96 | |
| `masterData` | `intake` | Postfach & Nummern | Slice 97 | mit Absenderregeln |
| `masterData` | `company` | Firmendaten | Slice 93 | erste native Maske, Vorbild der übrigen |
| `masterData` | `payments` | Zahlungen | Slice 96 | Schlüssel nur maskiert |
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

- **Hilfe und Handbuch:** Inhalte in `web/src/help/content/`, angezeigt im eingebetteten Browser der Windows-App (ADR 0079, E1).
- **Kundenbereich** (`web/src/booking/`): wird die Handy-App für Kunden (Slice 108).
- **Mein Tag** (`web/src/myday/`): wird die Handy-App für Mitarbeitende (Slice 106) mit der Seite für die Leitung (Slice 109).
- **Dashboard für den Pausenraum:** neu (Slice 105).
- **Betreiberseite** (`web/src/operator/`): unverändert; sie dient dem Betrieb der Plattform und gehört nicht zur Verwaltung eines Spas.

## Schienen und Bereiche der Verwaltung

Ziel nach [ADR 0098](../entscheidungen/0098-verwaltungsmasken-eine-schiene-uebersicht-seitenleiste.md) (masken.md R15): je Seite höchstens eine Schiene mit höchstens fünf Einträgen. „Umgestellt“ nennt den Pull Request; leer heißt, die Maske zeigt noch den alten Stand.

| Gruppe | Schiene heute | Schiene neu | Umgestellt |
| --- | --- | --- | --- |
| Betrieb | 6 Masken | 5 Masken (Schließzeiten und Raumsperren auf einer Seite) | T-064, 16.11.2026 (PR offen) |
| Angebot | 2 Masken; Behandlung mit 7 Abschnittsreitern | 2 Masken; Behandlung mit 4 Bereichen | Schritt 3 (Behandlung, 16.11.2026) |
| Team | 7 Ansichten; Personenkarte mit 10 Reitern | 5 Ansichten; Personenkarte mit 4 Bereichen, Verlauf als Seitenleiste | Personenkarte umgesetzt (T-065, 16.11.2026); Ansichten umgesetzt (T-066, 16.11.2026): Personen · Wochenplan · Zeiten · Lohn · Rechte und Zuständigkeiten, „Anträge“ als Seitenleiste im Wochenplan |
| Lager | 9 Reiter | 4 Reiter (Übersicht mit Verbrauch, Bestand mit den Ansichten Material, Chargen und Bewegungen, Bestellen, Inventur), dazu die Seite „Einrichtung“ mit Lagerorten und Lieferanten | T-061, Schritt 2 (Pull Request offen) |
| Kassa & Geld | 4 Masken | 4 Masken | T-064, 16.11.2026 (PR offen): Warnungen oben |
| Kontakt | 3 Masken | 3 Masken | T-064, 16.11.2026 (PR offen): Erinnerungen 3 : 2, Warnung oben |
| Zahlen | 5 Reiter, „Berichte“ mit 2 Unterreitern | 5 Reiter, „Berichte“ mit 2 Abschnitten | T-063, 16.11.2026 (PR offen) |

## Anlegen und Entfernen (ADR 0099, P10a)

Stand: 17.11.2026. Bestandsaufnahme vor dem Muster aus [ADR 0099](../entscheidungen/0099-anlegen-entfernen-und-leerer-betrieb.md): „Neu“ fest über der Liste, Entfernen im Menü „⋯“ im Kopf der Karte, Löschen nur ohne Abhängiges, sonst „Nicht mehr verwenden“. Die Spalte „Lücke“ nennt, was fehlt; „P10b“ ist der Folge-PR dieses Punkts, „Maske“ der PR der jeweiligen Bahn.

| Objekt | Anlegen (App / API) | Löschen | Nicht mehr verwenden | Ort in der App | Lücke |
| --- | --- | --- | --- | --- | --- |
| Standort | „Standort anlegen“ in der Liste / `POST api/locations` (Inhaber) | nein | nein (kein `IsActive`) | Betrieb › Standorte & Räume | Standort schließen ab Datum (neues Feld, Migration): P10b |
| Raum | Zeile „Neuer Raum“, jetzt über der Liste / `POST api/locations/{id}/rooms` | nie (Verweise von Terminen, Kosten, Sperrzeiten); `GET api/rooms/{id}/deletion-check` zählt (neu) | Menü „⋯“ der Zeile (neu) / `DELETE api/rooms/{id}` setzt `IsActive` aus, abgelehnt bei künftigen Terminen | Betrieb › Standorte & Räume › Räume | Wieder verwenden fehlt (`PUT` nur für aktive Räume): P10b |
| Gerät | Zeile „Neues Gerät“, jetzt über der Liste / `POST api/locations/{id}/equipment` | nie; `GET api/equipment/{id}/deletion-check` (neu) | Menü „⋯“ der Zeile (neu) / `DELETE api/equipment/{id}`, abgelehnt bei künftigen Terminen | Betrieb › Standorte & Räume › Geräte | Wieder verwenden fehlt: P10b |
| Person | Anlegen im Team, Einladen getrennt / `POST api/employees`, `POST api/staff/invitations` | nein | „Aus dem Team nehmen“ in der Personenkarte / `DELETE api/employees/{id}` setzt `IsActive` aus, abgelehnt bei künftigen Terminen; ehemalige über `GET api/employees/former` | Team › Personenkarte | Menü „⋯“ und Dialog statt eigenem Knopf: Maske Team (nicht in P10a) |
| Behandlung | Anlegen in der Maske / `POST api/treatments` | nein | „Nicht mehr anbieten“ / `DELETE api/treatments/{id}` setzt `IsActive` aus; gebuchte Termine bleiben | Angebot › Behandlungen | Reaktivieren fehlt, Menü „⋯“: Maske Behandlungen (nicht in P10a) |
| Lieferant | „Neu“ über der Liste (vorher letzte Zeile) / `POST api/stock/suppliers` | ja, wenn kein Material daran hängt (neu): `DELETE api/stock/suppliers/{id}`, sonst 409 mit der Liste; `deletion-check` (neu) | Menü „⋯“ (neu) / `PUT` mit `IsActive=false`; Bestellungen behalten den Namen | Lager › Einrichtung › Lieferanten | erledigt in P10a |
| Lagerort | „Neu“ über der Liste (vorher letzte Zeile) / `POST api/stock/locations/{id}/storage-places` | ja, wenn keine Bewegungen (vorher schon, jetzt mit `deletion-check`); Hauptlager nie | Menü „⋯“ (neu) / `PUT` mit `IsActive=false` (das Hauptlager nie) | Lager › Einrichtung › Lagerorte | erledigt in P10a |
| Material | Anlegen im Bestand / `POST api/stock/items` | nein | nur in der API (`IsActive` in `StockItemInput`); die App reicht den Wert nur durch (`StockItemFormViewModel.cs:306`) | Lager › Bestand › Material | Schalter in der App, Dialog: P10b |
| Qualifikation | Zeile in „Qualifikationen verwalten“ der Personenkarte / `POST api/qualifications` | ja, wenn niemand und keine Behandlung sie trägt (neu): `DELETE api/qualifications/{id}`, sonst 409; `deletion-check` (neu) | nein (kein `IsActive`) | Team › Personenkarte › Profil | Menü und Dialog in der App: P10b (Maske Personenkarte) |
| Aktion | „Neue Aktion“ über der Liste / `POST api/promotions` | nie (ADR 0071) | Archivieren und Zurückholen, umkehrbar / `POST …/archive`, `…/unarchive` | Angebot › Aktionen | bleibt beim Archivieren; Menü „⋯“: Maske Aktionen (nicht in P10a) |
| Betriebskontakt | Anlegen in der Maske (darunter die Kategorie „Handwerker und Technik“) / `POST api/business-contacts` | nein | Archivieren und Zurückholen / `POST …/archive`, `…/unarchive` (`BusinessInfoViewModel.cs:878-938`) | Zahlen › Betriebsinfo | Handwerker bleiben Kategorie der Betriebsinfo (ADR 0099); Menü „⋯“: Maske Betriebsinfo |

Das Muster in der API ist `GET …/{id}/deletion-check` mit `DeletionCheckDto(CanDelete, Summary, Dependents, Alternative)`; die App zeigt es im gemeinsamen Dialog (`DeletionDialog`) und im Menü `MoreMenuButton`. In P10a ist es bei Lagerorten, Lieferanten, Räumen und Geräten eingebaut; die übrigen Masken folgen mit ihren PRs.

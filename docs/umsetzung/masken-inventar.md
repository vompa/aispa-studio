# Masken-Inventar: Web-Verwaltung → native Windows-App

> Slice 103 hat die Web-Team-Oberfläche und die eingebettete Verwaltung entfernt. Die Tabellen halten fest, wohin jede frühere Seite gewandert ist.

Stand: 01.11.2026, Runde R05, Slice 92 und 103 ([ADR 0084](../entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)).

Jede Seite und jeder Reiter der Web-Team-Oberfläche hat hier genau ein Ziel: die Slice der Runde R05, die ihn nativ in der Windows-App baut, „entfällt“ (nativ schon vorhanden) oder „bleibt“ (im Web). `web/src/maskInventory.test.ts` prüft seit Slice 103 nur noch den ADR-Index; die Seiten, gegen die er früher prüfte, gibt es nicht mehr.

Jede Slice hat ihren Bereich vollständig ersetzt, mit denselben Feldern, Regeln und Hilfetexten. Eingebettet öffnet die Windows-App nichts mehr.

## Seiten

| Seite | Reiter | Name in der Oberfläche | Ziel | Anmerkung |
|---|---|---|---|---|
| `calendar` | – | Kalender | entfällt | Kalender ist nativ (ADR 0053) |
| `roster` | – | Dienstplan | Slice 99 | Reiter „Dienstplan und Abwesenheit“ der Personenkarte und Ansicht „Wochenplan“ im Bereich Team (dort auch „Anträge“); native Maske seit Slice 99 |
| `time` | – | Zeiten | Slice 99 | Reiter „Zeiten“ der Personenkarte und Ansicht „Zeiten“ im Bereich Team; native Maske seit Slice 99 |
| `inbox` | – | Posteingang | entfällt | Posteingang ist nativ |
| `pos` | – | Kassa | entfällt | Kassa ist nativ; Kassa-Einstellungen siehe Reiter `kassa` |
| `stock` | – | Lager | Slice 101 | nativ: Bestand, Bewegungen, Inventur, Lagerorte (Slice 101), Aufgaben, Lieferanten (Slice 112), Bestellvorschlag, Entwürfe und Senden (Slice 113), Lieferung buchen, Als bestellt markieren, Bestelltext, Bedarf und Chargen-Rückverfolgung (Slice 123); Lager-Verbesserungen danach in den Slices 112–116 |
| `promotions` | – | Aktionen | Slice 101 | nativ (Slice 101) |
| `reports` | – | Zahlen | Slice 102, R07 Slice 144 | nativ (Slice 102): Mein Tag, Tagesstart, Hygiene, Management mit Vergleich, Pflichtberichte; Balken im Vergleich offen (Ä226). Seit R07 Slice 144 (Ä352) eine Reiterebene: Tagesstart · Mein Tag · Hygiene · Berichte (Management, Pflichtberichte als Unterreiter) · Betriebsinfo |
| `costs` | – | Kosten | Slice 102 | nativ (Slice 102) |
| `accounting` | – | Buchhaltung | Slice 102 | nativ (Slice 102); Exporte speichert die App unverändert |
| `payroll` | – | Lohn | Slice 100 | Reiter „Lohn“ der Personenkarte und Export im Bereich Team (umgesetzt in Slice 100) |
| `businessInfo` | – | Reiter „Betriebsinfo“ in Zahlen | Slice 102, R07 Slice 144 | nativ (Slice 102); seit R07 Slice 144 ein Reiter der Maske „Zahlen“, mit dem Recht auf Berichte mit Zahlen (`ViewFinance`) oder zu buchen (`Book`, seit T-025) |
| `masterData` | – | Stammdaten | Slice 93 | Verwaltungsgerüst; die Reiter verteilen sich wie unten |
| `staff` | – | Zugänge | Slice 98 | Reiter „Zugang und Rolle“ der Personenkarte; native Maske seit Slice 98 |

## Reiter der Stammdaten

| Seite | Reiter | Name in der Oberfläche | Ziel | Anmerkung |
|---|---|---|---|---|
| `masterData` | `locations` | Standorte & Räume | Slice 95 | mit Raum-Checkliste |
| `masterData` | `closures` | Schließzeiten | Slice 95 | |
| `masterData` | `roomClosures` | Raumsperren | Slice 95 | |
| `masterData` | `subscriptions` | Kalender-Abos | Slice 95 | Feiertage und Ferien (ADR 0075), keine Kunden-Abos |
| `masterData` | `team` | Team | Slice 98 | Personenkarte, Reiter Stammdaten; native Maske seit Slice 98 |
| `masterData` | `treatments` | Behandlungen | Slice 94 | „Wer kann das?“ nur lesend mit Sprung ins Team |
| `masterData` | `qualifications` | Qualifikationen | Slice 98 | Reiter „Qualifikationen“ der Personenkarte; native Maske seit Slice 98 |
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
| Kalender, Termin „Behandlungszeiten bearbeiten …“ | `masterData/treatments`, Bereich Ablauf | Slice 94 |
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

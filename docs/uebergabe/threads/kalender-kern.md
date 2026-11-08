# Übergabe Thread „Kalender-Kern“

Stand: 18.10.2026, 21:40 UTC. Auf Anweisung des Auftraggebers gestoppt.

Repository `vompa/aispa-studio`, Branch `claude/kalender-kern-9m7c4i`. Zuständig waren Fachmodell, Kalender, Stammdaten, Rollen und Zugänge je Standort, Dienstplan, Mandantentrennung und Ausfall-Assistent. Details zu jedem PR stehen in `docs/umsetzung/pr-protokoll.md`.

## Gebaut und gemergt

| PR | Inhalt |
| --- | --- |
| #3 | Fachmodell und Kalender, Doppelbuchungsschutz in PostgreSQL (`Scheduling/`, `web/src/calendar`) |
| #4 | Stammdaten-Oberfläche |
| #6 | Vier feste Rollen, Zugang je Standort, `RequirePermission`, Einladung per Link (`Access/`) |
| #10 | Dienstplan, `RosterService.ShiftsAsync` als einzige Quelle für Kalender und Planer |
| #12 | Mandantentrennung mit globalem Filter (`Tenancy/`) |
| #31 | Zeiterfassung in Abschnitten, Korrektur nur mit Grund (`TimeTracking/`) |
| #35 | Ausfall-Assistent (Vorschläge, Übernehmen, Mail an Gäste) |
| #38 | Kontaktdaten je Standort, Kategorie an der Behandlung |
| #42 | Preis je Standort |
| #50 | Paar-Termin und Spa-Tag (`GroupPlanner`, Plätze je Raum) |
| #51 | Bereiche wie Sauna oder Ruheraum (`AreaVisit`) |
| #54 | Dienstplan als Entwurf, Veröffentlichen mit Fristhinweis |
| #58 | Lückenbewertung freier Zeiten im Kalender |
| #61 | Paar und Spa-Tag als Ganzes verschieben |
| #68, #72 | Befristete Zugänge, Mail 7 Tage vor Ablauf, Verlängern um 30 Tage |
| #75 | Lücken durch Verschieben schließen (nur Vorschläge) |
| #86 | Schnellwechsel per PIN am Empfangsgerät |
| #90 | Abmelden nach Leerlauf am Empfangsgerät |

## Entscheidungen

- ADR 0023: Doppelbuchungsschutz in der Datenbank (EXCLUDE-Bedingung je Ressource).
- ADR 0024: Mandantentrennung in einer gemeinsamen Datenbank.
- ADR 0025: Dienstplan gibt Hinweise zur Arbeitszeit, sperrt aber nicht.
- ADR 0031: Zeiterfassung in Abschnitten, Korrekturen nur mit Grund und Protokoll.
- ADR 0042: PIN gilt nur auf einem von der Leitung angemeldeten Empfangsgerät, Sperre nach fünf Fehlversuchen.
- Kleinere Entscheidungen ohne eigenen ADR stehen im PR-Protokoll. Die wichtigsten davon:
  - Belegungslogik nur im Backend (`ResourcePlanner`).
  - Der Preis wird bei der Buchung festgehalten.
  - Assistenten schlagen nur vor und ändern nichts von selbst.
  - Ein befristeter Zugang gilt für Inhaber nie.

## Halb fertig

- **Ausfall-Assistent: Ersatz an anderen Standorten.** Fertig gebaut und lokal grün geprüft (446 Backend-Tests, 140 Web-Tests). Der Stand liegt auf dem Branch, Commit `8c6d558` enthält schon das aktuelle main. Es gibt dazu noch keinen PR. Der Abschnitt im PR-Protokoll und die Zeile im Stand tragen den Platzhalter `#130`. Er wird beim Öffnen des PR durch die Nummer ersetzt.
  - Inhalt: Geht am eigenen Standort nichts, kommen Vorschläge von den anderen Standorten, die die Person sieht. Sie sind nicht vorausgewählt.
  - Beim Übernehmen wird der Termin verlegt (`Appointment.LocationId` ist jetzt änderbar). Der alte Kalender nimmt ihn live heraus, und die frei gewordene Zeit geht an die Warteliste. Die Mail nennt den neuen Standort, der Preis bleibt.
- Sonst gibt es keine offenen PRs und keine bekannten Fehler.

## Nächste Punkte, in dieser Reihenfolge

1. PR für den Ersatz an anderen Standorten öffnen, `#130` ersetzen, nach grüner CI mergen.
2. Ausfall-Assistent: Gäste ohne Ersatz per SMS oder WhatsApp informieren. Die Kanäle dafür gibt es schon aus den Erinnerungen in `Reminders/`.
3. Ausfall-Assistent: Ersatz an anderen Tagen vorschlagen. Danach Paar- und Spa-Tag-Buchungen an einen anderen Standort verlegen.
4. Leerlaufzeit eines schon angemeldeten Empfangsgeräts ändern. Dazu eine Liste aller Empfangsgeräte des Spas.
5. Schnellwechsel und Leerlauf auch in der Windows-App, abgestimmt mit dem Thread „Windows-App ausbauen“.
6. Lücken durch Verschieben mehrerer Termine hintereinander schließen (Ketten).
7. Feiertage weiterer Kantone und einzelner Gemeinden.

## Überlegungen für später

- Chipkarte oder NFC für den Schnellwechsel. Dafür reicht dieselbe Geräte-Logik wie bei der PIN.
- Entfernung zwischen Standorten oder einen bevorzugten Ausweichstandort hinterlegen. Heute ist die Reihenfolge alphabetisch.
- Lückenbewertung auch in Online-Buchung und KI-Terminannahme anbieten. Das gehört den anderen Threads.
- SSO für größere Spas.

## Hinweise für die Arbeit im Code

- Lokale Tests laufen gegen PostgreSQL mit `AISPA_TEST_POSTGRES`. Die volle Prüfkette steht in `CLAUDE.md`.
- `docs/umsetzung/pr-protokoll.md` und `docs/stand-und-offene-punkte.md` kollidieren bei parallelen PRs fast immer. Beim Einmischen von main beide Seiten behalten.
- ADR-Nummern vor dem Merge prüfen. ADR 0042 hieß zuerst 0041.
- Migrationen nach dem Einmischen von main bei Bedarf neu datieren, damit sie nach denen aus main liegen.

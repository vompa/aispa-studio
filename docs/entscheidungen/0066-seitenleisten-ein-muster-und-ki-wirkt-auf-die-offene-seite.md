# 0066. Rechte Seitenleisten nach einem Muster; KI und Hilfe wirkt auf die offene Seite

- **Status:** angenommen
- **Datum:** 21.10.2026
- **Entschieden von:** Auftraggeber (Aufträge vom 21.10.2026), Ausgestaltung Claude

## Kontext

Die Windows-App hatte neun rechte Seitenleisten (Termin, Neuer Eintrag, Freie Zeit, Wunsch eintippen, Warteliste, Ausfall-Assistent, Raum und Kundendaten im Panel, KI und Hilfe) mit je eigenem Kopf, unterschiedlichem Schließen-Knopf, zwei Breiten und ohne gemeinsame Regeln für Esc, Fokus und Bewegung. „KI und Hilfe“ konnte nur Texte zeigen.

## Entscheidung

- **Ein Muster (Fluent 2):** fester Kopf (`SidePanelHeader`: Titel und Untertitel links, Schließen-Kreuz rechts oben), darunter scrollt der Inhalt; jede Leiste sagt Titel und Untertitel über `ISidePanel`. Breite 360 Pixel für alle, nur das Formular für Kundendaten ist breiter (560).
- **Nie zwei Leisten:** die neuere macht der älteren Platz (`ShellViewModel`): öffnet sich im Kalender eine Leiste, klappt „KI und Hilfe“ zu (ohne das als Wunsch der Person zu speichern), und umgekehrt. Gilt nur, solange der Kalender die offene Seite ist. *Geändert (Runde 6, Ä66, 23.10.2026, Auftraggeber):* Im Kalender macht „KI und Hilfe“ keinen Platz mehr und schließt auch nichts mit, sondern ist ein Reiter der Seitenleiste (Design-Brief 5.28); die Leiste der Shell gilt nur noch auf den Seiten ohne Reiterschiene.
- **Tastatur und Fokus:** Esc schließt die oberste Leiste, Strg+Leertaste öffnet und schließt „KI und Hilfe“. Leisten zum Eingeben setzen den Fokus ins erste Feld (bei „Freie Zeit suchen“ in die Behandlung); beim Ansehen eines Termins bleibt der Fokus im Kalender, damit die Pfeiltasten weiter wirken. Beim Schließen geht der Fokus zurück (Kalender bzw. Knopf unten rechts).
- **Bewegung:** Einblenden 200 ms abbremsend mit kurzem Weg von rechts, Schließen 120 ms; Kurzbefehle und abgeschaltete Windows-Animationen ohne Bewegung (`PanelPresence`, `ReducedMotion`). Begründung: was hunderte Male am Tag per Taste geschieht, darf nicht verzögern; was per Maus geschieht, braucht den kurzen räumlichen Bezug.
- **Zustand je Seite:** „KI und Hilfe“ offen oder zu gilt je Seite (`CompanionPages`), mit dem zuletzt gewählten Stand als Vorgabe.
- **KI wirkt auf die offene Seite, schlägt nur vor ([ADR 0008](0008-ki-schlaegt-vor-mensch-bestaetigt.md)):** neue Vorschlagsarten `ShowCustomerAppointments` und `SearchFreeTime`, Knopf „Jetzt suchen“ unter der Hilfe zu freien Zeiten. Ausgeführt wird erst nach dem Klick.

## Folgen und Stand

- Dazu im selben Stand: „Freie Zeit suchen“ mit Raum, Gast und mindestens drei Vorschlägen über bis zu 14 Tage (Endpunkt `availability` mit `roomId`, `minSlots`, `days`), „Wunsch eintippen“ mit Behandlungskarten, Rückfragen und Raum-/Gäste-Bausteinen, Warteliste mit Freie Zeiten zeigen, Buchen und Entfernen (`POST /api/waitlist/{id}/close`, ohne Migration), Betriebsabläufe im Kalender ([ADR 0065](0065-raumpflege-tagesstart-und-abnahme.md)).
- **Nicht geprüft:** Verhalten unter echtem Windows (Fokus nach Esc im Zusammenspiel mit dem Narrator, Animationen bei „Animationen anzeigen“ aus).
- Offen: der Grund beim Entfernen von der Warteliste wird nur als gebucht oder ausgetragen gespeichert (ohne Migration keine eigene Spalte); der zweite Gast einer Paar-Anfrage wird über „Paar oder Spa-Tag“ ergänzt; Raum-Infos bleiben ein Flyout am Raumnamen.

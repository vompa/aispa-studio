# Slice 72 – Meldungen im Kalender: ein Ort, Ursache, Weg zum Konflikt

**Ziel:** Meldungen im Kalender stehen an einer Stelle, nennen Ursache und Person und bieten einen Weg zum Konflikt; „kein Raum frei“ ist erklärt und geprüft.
**Runde:** R03
**Quelle:** R03 – Ä79, Ä80, Ä94
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä116–Ä118

## Umfang
- Regel für den Ort festlegen und umsetzen: eine Meldung steht direkt bei der auslösenden Handlung oder immer an derselben Stelle; Neuer Termin, Verschieben im Panel und „Nicht verschoben“ folgen derselben Regel
- „Zeitraum ist belegt“ und „Nicht verschoben“ nennen die belegende Person und den Raum und haben einen Knopf „Zum Konflikt“, der die Wochenansicht dieser Person mit dem Tag und dem Termin im Fokus öffnet
- „Kein geeigneter Raum ist zu dieser Zeit frei“: mit dem Beispiel aus dem Test nachstellen (Anti-Aging, 90 Minuten, Mittwoch 11:45, Elif Demir) und prüfen, ob der Raumplaner richtig rechnet; die Meldung nennt die Räume und eine nächste freie Zeit

## Muster übernehmen
- src/AispaStudio.Api/Scheduling/ResourcePlanner.cs – Raumprüfung
- tests/AispaStudio.Desktop.Tests/CalendarNoticeTests.cs – Tests der Meldungen
- docs/gestaltung/meldungen.md – Regeln für Meldungen

## NICHT bauen
- Neue Meldungsarten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 72 "Meldungen im Kalender: ein Ort, Ursache, Weg zum Konflikt" um (diese Datei).
1. Stelle zuerst einen Plan vor (Dateien, Klassen, Tests) mit einem Abschnitt
   „Annahmen (bei Freigabe bestätigen)“ für jeden offenen Punkt und einem kurzen „Review-Fokus“,
   speichere ihn unter work/plans/ und warte auf Freigabe.
2. Schreibe die Abnahmetests dieser Slice als fehlschlagende Tests (ROT).
3. Implementiere das Minimum bis grün. Halte jede Regel in CLAUDE.md und work/conventions.md ein.
4. Prüfe die Definition of Done. Halte Entscheidungen, Überraschungen und zurückgestellte
   Kleinigkeiten in der Rundenakte und in der Protokolldatei fest, die work/conventions.md nennt.
```

## Definition of Done
- [ ] Prüfungen aus work/conventions.md grün
- [ ] Kein Test abgeschwächt oder übersprungen
- [ ] Keine neuen Pakete ohne Freigabe
- [ ] Entscheidungen festgehalten
- [ ] Given ein belegter Zeitraum When eine Verschiebung abgelehnt wird Then steht die Meldung an einer festen Stelle und nennt Person und Raum
- [ ] Given die Meldung „Nicht verschoben“ When „Zum Konflikt“ gedrückt wird Then zeigt der Kalender die Woche der Person mit dem belegenden Termin im Fokus
- [ ] Given zwei freie Behandlungsräume When ein Termin gebucht wird Then meldet der Raumplaner nicht „kein Raum frei“
- [ ] Manuell: Termin auf eine belegte Zeit verschieben: die Meldung steht dort, wo man gerade arbeitet, und „Zum Konflikt“ springt zum belegenden Termin

## Nicht in dieser Slice
–

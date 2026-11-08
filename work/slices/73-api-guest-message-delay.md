# Slice 73 – Wartezeit vor der Nachricht an den Gast

**Ziel:** Nachrichten an den Gast über Absagen und Verschiebungen, die das Team auslöst, gehen erst nach einer einstellbaren Wartezeit raus (Standard 10 Minuten), damit ein Versehen noch zurückgenommen werden kann.
**Runde:** R03
**Quelle:** R03 – Ä95 (Ruling)
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä119–Ä121

## Umfang
- Einstellung „Wartezeit vor der Nachricht an den Gast“ je Standort in der Verwaltung, 0 bis 60 Minuten, Standard 10; Hilfetext und Fragezeichen (ADR 0055), Endpunkt mit `RequirePermission`
- Absagen und Verschiebungen des Teams planen die Mail mit Sendezeitpunkt über die Wolverine-Warteschlange; Nachrichten, die der Gast selbst auslöst, gehen sofort; beginnt der Termin in weniger als 2 Stunden, geht die Nachricht sofort
- Das Panel zeigt „Nachricht an den Gast geht um 14:32 raus“ mit „Jetzt senden“ und „Nicht senden“; neuer ADR (nächste freie Nummer prüfen) mit der Regel

## Muster übernehmen
- src/AispaStudio.Api/Mail/Mail.cs – Versand der Nachrichten
- src/AispaStudio.Api/Closures/ClosureCancellations.cs – Absagen mit Nachricht
- tests/AispaStudio.Api.Tests/ClosureTests.cs – Tests der Absagen

## NICHT bauen
- Rückgängig-Knopf und Strg+Z (Slice 74)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 73 "Wartezeit vor der Nachricht an den Gast" um (diese Datei).
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
- [ ] Given die Wartezeit 10 Minuten When das Team einen Termin absagt Then wird die Mail für 10 Minuten später geplant und noch nicht gesendet
- [ ] Given ein Termin in 90 Minuten When er abgesagt wird Then geht die Nachricht sofort raus
- [ ] Given eine geplante Nachricht When „Nicht senden“ gedrückt wird Then geht sie nie raus
- [ ] Manuell: Einen Termin absagen: im Panel steht „Nachricht an den Gast geht um … raus“, „Nicht senden“ verhindert sie

## Nicht in dieser Slice
–

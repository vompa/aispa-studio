# Slice 78 – Prüfung beim Buchen schneller

**Ziel:** Die Prüfung beim Buchen und die Meldung „Standort geschlossen“ kommen ohne spürbare Wartezeit.
**Runde:** R03
**Quelle:** R03 – Ä84
**Hängt ab von:** 68
**Modell:** opus
**IDs für neue Befunde:** Ä134–Ä136

## Umfang
- Messen: wo liegt die Zeit (Aufruf, Raumplaner auf dem Server, Oberfläche); Ergebnis mit Zahlen im Protokoll
- Einfache Fälle in der App vorab prüfen (Standort geschlossen, Tag ohne Dienst, Ende vor Beginn) ohne Server, mit denselben Regeln wie der Server (eine Quelle der Regel, kein zweiter Rechenweg für Pflichtzahlen)
- Zwischenspeicher für Öffnungszeiten und Schließzeiten je Standort und Woche, mit Ablauf bei Änderung; Belegungslogik bleibt im Backend (`ResourcePlanner`)

## Muster übernehmen
- src/AispaStudio.Api/Scheduling/ResourcePlanner.cs – Raumplaner
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Closures.cs – Schließzeiten in der App

## NICHT bauen
- Belegungslogik in die Oberfläche verlegen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 78 "Prüfung beim Buchen schneller" um (diese Datei).
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
- [ ] Given ein Schließtag When der Termin geprüft wird Then liegt die Antwort in unter 300 Millisekunden vor
- [ ] Given geänderte Schließzeiten When erneut geprüft wird Then gilt der neue Stand (Zwischenspeicher ungültig)
- [ ] Manuell: Neuer Termin am Sonntag: der Hinweis „geschlossen“ erscheint sofort

## Nicht in dieser Slice
–

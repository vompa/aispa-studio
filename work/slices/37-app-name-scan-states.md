# Slice 37 – Namens-Scan je Zustand

**Ziel:** Der Namens-Scan prüft nicht nur die Ausgangszustände, sondern auch offene Seitenleisten, Dialoge und Bearbeiten-Zustände.
**Runde:** R02
**Quelle:** R02 – W65
**Hängt ab von:** 33, 34, 35
**Modell:** sonnet

## Umfang
- Scan aus `ViewTests.FocusOrder.cs` auf Zustände erweitern: Termin-Panel, Neuer Eintrag, Rückfragedialog, Kundenkarten im Bearbeiten-Zustand, Kassa mit Rechnung
- Gefundene Lücken in derselben Slice benennen

## Muster übernehmen
- tests/AispaStudio.Desktop.Tests/ViewTests.FocusOrder.cs – vorhandener Namens-Scan

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 37 "Namens-Scan je Zustand" um (diese Datei).
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
- [ ] Given jede Seitenleiste, jeder Dialog und jeder Bearbeiten-Zustand der Liste When der Namens-Scan läuft Then hat jedes Bedienelement einen lesbaren Namen
- [ ] Manuell: Mit Narrator ein Termin-Panel und eine Rückfrage durchgehen: jedes Element hat einen Namen

## Nicht in dieser Slice
–

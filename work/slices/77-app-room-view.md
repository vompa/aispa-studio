# Slice 77 – Raumansicht: Kategorien, gesperrte Räume, Sauna und Ruheraum

**Ziel:** Die Raumansicht ordnet Räume nach Kategorien, zeigt gesperrte Räume, hat den Umschalter „Alle Räume“ und „Nur verfügbare Räume“, zeigt Sauna und Ruheraum und bringt beim Umschalten den bearbeiteten Raum samt Vorschau in den Fokus.
**Runde:** R03
**Quelle:** R03 – Ä91, Ä92
**Hängt ab von:** 71
**Modell:** sonnet
**IDs für neue Befunde:** Ä131–Ä133

## Umfang
- Räume in der Raumansicht nach Kategorie der Stammdaten gruppieren (Kategorie vorhanden prüfen, sonst nach Typ)
- Gesperrte Räume (Raumsperren aus ADR 0073: Reinigung, technischer Defekt, sonstiges) im Kalender erkennbar mit Grund; Umschalter „Alle Räume“ und „Nur verfügbare Räume“ (nicht gesperrt und geöffnet), wie bei den Mitarbeitenden
- Sauna und Ruheraum als Kategorie „Sauna und Ruhe“ mit Belegung über den Tag und der Vorschau eines neuen Eintritts
- Beim Umschalten von der Mitarbeitenden- in die Raumansicht mit offener Bearbeitung rückt der Raum ins Bild und die Vorschau ist sichtbar und ziehbar (Slice 71)

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.RoomClosures.cs – Raumsperren im Kalender
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.EntryPreview.cs – Vorschau
- tests/AispaStudio.Desktop.Tests/RoomClosureTests.cs – Tests der Raumsperren

## NICHT bauen
- Neue Raumverwaltung in den Stammdaten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 77 "Raumansicht: Kategorien, gesperrte Räume, Sauna und Ruheraum" um (diese Datei).
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
- [ ] Given zwei Kategorien von Räumen When die Raumansicht öffnet Then stehen die Räume nach Kategorie sortiert
- [ ] Given ein gesperrter Raum When „Nur verfügbare Räume“ gewählt wird Then fehlt er in der Ansicht
- [ ] Given die Raumansicht When sie öffnet Then stehen Finnische Sauna und Ruheraum als Spalten da
- [ ] Given offene Bearbeitung eines Termins im Hamam When zur Raumansicht gewechselt wird Then ist der Hamam sichtbar und die Vorschau steht dort
- [ ] Manuell: Raumansicht: Hamam sperren (Reinigung), „Nur verfügbare Räume“: er verschwindet; Sauna und Ruheraum sind da

## Nicht in dieser Slice
–

# Slice 71 – Ziehen im Kalender: Vorschau, Notiz, Pause, Abbruch

**Ziel:** Die Vorschau des neuen Termins, Notiz- und Pausenbalken lassen sich mit der Maus ziehen und in der Höhe ändern; ein Ziehen bricht ab, wenn der Mauszeiger die Kalenderfläche verlässt; keine Platzhalter bleiben liegen.
**Runde:** R03
**Quelle:** R03 – Ä83, Ä88, Ä89, Ä93, Ä90, Ä94
**Hängt ab von:** 70
**Modell:** opus
**IDs für neue Befunde:** Ä113–Ä115

## Umfang
- Vorschau des neuen Termins mit der Maus auf einen anderen Tag und eine andere Zeit ziehen, die Eingaben bleiben
- Notiz- und Pausenbalken in der Höhe ziehen (Beginn und Ende), wie bei Terminen
- Ziehen bricht ab, wenn der Mauszeiger die Kalenderfläche bewusst verlässt (wie Esc); die gestrichelten Platzhalter (11:30, 11:45, 19:00 in den Bildern) verschwinden, wenn nichts offen ist

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.EntryPreview.cs – Vorschau
- tests/AispaStudio.Desktop.Tests/CalendarLaneTests.cs – Spuren und Ziehen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Keyboard.cs – Abbruch mit Esc

## NICHT bauen
- Neue Ansichten
- Ziehen in der Raumansicht (Slice 77)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 71 "Ziehen im Kalender: Vorschau, Notiz, Pause, Abbruch" um (diese Datei).
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
- [ ] Given die Vorschau eines neuen Termins When sie auf Mittwoch 10:00 gezogen wird Then steht der Termin im Formular am Mittwoch um 10:00
- [ ] Given ein Notizbalken When sein unteres Ende um 30 Minuten gezogen wird Then steht „Bis“ um 30 Minuten später
- [ ] Given ein gezogener Termin When der Mauszeiger die Kalenderfläche verlässt Then liegt der Termin wieder an seinem alten Platz
- [ ] Given kein offener Termin When der Kalender neu gezeichnet wird Then steht kein gestrichelter Platzhalter da
- [ ] Manuell: Einen Termin ziehen und mit der Maus aus dem Kalender hinausfahren: er springt zurück, nichts wird verschoben

## Nicht in dieser Slice
–

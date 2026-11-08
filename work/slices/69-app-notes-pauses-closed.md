# Slice 69 – Notizen und Pausen an Schließtagen, keine fremde Meldungsleiste

**Ziel:** Notizen und Pausen funktionieren an Schließtagen; nach dem Speichern öffnet sich weder ein fremder Termin noch die Leiste „Meldungen“ mit einem Filter.
**Runde:** R03
**Quelle:** R03 – Ä87, Ä90
**Hängt ab von:** 68
**Modell:** sonnet
**IDs für neue Befunde:** Ä107–Ä109

## Umfang
- Notiz und Pause an einem Schließtag speichern und im Kalender zeigen; die Maske meldet „Notiz gespeichert“ nur, wenn sie im Kalender sichtbar ist
- Ursache finden, warum nach dem Speichern der stornierte Termin eines anderen Tages im Panel aufgeht und warum nach einer Pause die Leiste „Meldungen“ auf „Hamam“ filtert; beheben, mit Test
- Der Fokus nach dem Speichern bleibt in der Maske oder geht an den neuen Eintrag

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.EntryPreview.cs – Vorschau und Eintragsmasken
- src/AispaStudio.Desktop/ViewModels/CalendarEntryViewModel.cs – Eintrag (Notiz, Pause)
- tests/AispaStudio.Desktop.Tests/CalendarDayNotesTests.cs – Tests der Tagesnotizen

## NICHT bauen
- Neue Funktionen für Notizen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 69 "Notizen und Pausen an Schließtagen, keine fremde Meldungsleiste" um (diese Datei).
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
- [ ] Given ein Schließtag When eine Notiz gespeichert wird Then steht sie im Kalender und das Panel zeigt sie
- [ ] Given ein Schließtag When eine Pause eingetragen wird Then öffnet sich die Leiste „Meldungen“ nicht
- [ ] Given eine gespeicherte Notiz When der Kalender neu geladen wird Then steht sie weiter dort
- [ ] Manuell: Am Sonntag eine Notiz „Handwerker kommen“ 10:45–11:00 speichern: sie erscheint im Kalender, rechts öffnet sich nichts Fremdes

## Nicht in dieser Slice
–

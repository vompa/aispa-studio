# Slice 76 – Kalenderkopf, Tastenleiste, Zurücksetzen, Tagesüberblick

**Ziel:** Die Datumsauswahl springt nicht mehr, die Tastenleiste ist anklickbar, die Ansicht lässt sich per Kürzel zurücksetzen, und der Tagesüberblick zeigt die überfälligen Termine.
**Runde:** R03
**Quelle:** R03 – Ä78, Ä96, Ä99, Ä100 (Rulings)
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** Ä128–Ä130

## Umfang
- Datumsauswahl bleibt beim Wechsel des Tages an ihrem Platz (feste Breite der Tagesbeschreibung oder Popup am Knopfrand verankert)
- Jeder Eintrag der Tastenleiste unten ist ein Knopf, der dasselbe tut wie die Taste; gesperrt mit Grund im Tooltip, wenn die Taste nichts bewirkt
- Kürzel „Ansicht zurücksetzen“ (Filter, Ansicht Team oder Räume, Zeitraum, Tag auf den Zustand beim ersten Öffnen), ohne eigenen Knopf, Eintrag in der Tastenleiste nur, wenn die Ansicht abweicht; offene Bearbeitungen bleiben
- Tagesüberblick: „Überfällig“ als Liste mit bis zu 5 Einträgen, jeder öffnet den Termin, darüber „und n weitere“; Kennzahlen bleiben Anzeige

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Keyboard.cs – Tastenleiste und Kürzel
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Filter.cs – Filter der Ansicht
- tests/AispaStudio.Desktop.Tests/CalendarHeaderTests.cs – Tests der Kopfzeile

## NICHT bauen
- Eigener Knopf zum Zurücksetzen
- Neue Kennzahlen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 76 "Kalenderkopf, Tastenleiste, Zurücksetzen, Tagesüberblick" um (diese Datei).
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
- [ ] Given die Datumsauswahl offen When ein anderer Tag gewählt wird Then behält das Popup seine Position
- [ ] Given die Tastenleiste When „Esc Markierung aufheben“ angeklickt wird Then wird die Markierung aufgehoben
- [ ] Given ein gefilterter Kalender When das Kürzel zum Zurücksetzen gedrückt wird Then zeigt er wieder die Ansicht des ersten Öffnens und der Eintrag verschwindet aus der Tastenleiste
- [ ] Given sieben überfällige Termine When der Tagesüberblick öffnet Then stehen fünf Einträge und „und 2 weitere“ da
- [ ] Manuell: Filter „Elif Demir“ setzen, Kürzel drücken: der Filter ist weg; Tagesüberblick: ein überfälliger Termin ist anklickbar

## Nicht in dieser Slice
–

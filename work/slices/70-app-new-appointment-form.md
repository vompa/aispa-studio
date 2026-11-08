# Slice 70 – Neuer Termin: Kunde zuerst, Treffer übernehmen, Tag wählen, nichts verwerfen

**Ziel:** Die Maske „Neuer Termin“ beginnt mit dem Kunden, schlägt daraus Behandlung und Notizen vor, übernimmt den gewählten Namen, hat ein Datumsfeld und verwirft Eingaben nicht, wenn man einen anderen Zeitslot anklickt.
**Runde:** R03
**Quelle:** R03 – Ä86, Ä82, Ä83, Ä85
**Hängt ab von:** 68
**Modell:** sonnet
**IDs für neue Befunde:** Ä110–Ä112

## Umfang
- Reihenfolge: Kunde, Behandlung, Beginn, Notiz; nach der Kundenwahl Vorschläge (zuletzt gebuchte Behandlung, gewohnte Person) und die Wünsche aus der Kundenkarte, jeweils übernehmbar
- Klick auf einen Treffer der Kundensuche schreibt den Namen ins Suchfeld und zeigt den gewählten Kunden erkennbar
- Datumsfeld neben „Beginn“; ein Klick auf einen anderen Zeitslot im Kalender verlegt den angefangenen Termin dorthin statt die Maske zu schließen; verworfen wird nur mit „Abbrechen“ oder Esc, mit Rückfrage, wenn schon etwas eingetragen ist

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/AppointmentViewModel.Customer.cs – Kundensuche in der Maske
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.EntryPreview.cs – Vorschau des neuen Termins
- tests/AispaStudio.Desktop.Tests/CalendarPanelTests.cs – Tests des Panels

## NICHT bauen
- Neue Kundenfelder

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 70 "Neuer Termin: Kunde zuerst, Treffer übernehmen, Tag wählen, nichts verwerfen" um (diese Datei).
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
- [ ] Given die Suche „demi“ When der Treffer „Tomasz Demir“ angeklickt wird Then steht „Tomasz Demir“ im Suchfeld
- [ ] Given eine ausgefüllte Maske When ein anderer Slot im Kalender angeklickt wird Then bleiben alle Eingaben und die Vorschau steht am neuen Slot
- [ ] Given ein gewählter Kunde mit gebuchter Behandlung When die Maske den Kunden übernimmt Then bietet sie diese Behandlung als Vorschlag an
- [ ] Manuell: Neuer Termin: „Demi“ suchen, Treffer wählen, dann einen anderen Slot anklicken: Name und Behandlung bleiben stehen

## Nicht in dieser Slice
–

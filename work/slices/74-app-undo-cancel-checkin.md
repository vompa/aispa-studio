# Slice 74 – Rückgängig für Stornieren und Einchecken

**Ziel:** Ein versehentliches Stornieren oder Einchecken lässt sich sofort per Knopf und Strg+Z und später im Panel zurücknehmen, bis kassiert ist.
**Runde:** R03
**Quelle:** R03 – Ä95, Ä97
**Hängt ab von:** 73
**Modell:** opus
**IDs für neue Befunde:** Ä122–Ä124

## Umfang
- Meldung „Termin storniert“ und „Eingecheckt“ mit Knopf „Rückgängig“; Strg+Z macht dasselbe, solange die Meldung steht; Eintrag „Strg+Z Rückgängig“ in der Tastenleiste nur, solange es etwas rückgängig zu machen gibt
- Im Panel eines stornierten Termins „Stornierung zurücknehmen“ (prüft, ob Zeit, Person und Raum noch frei sind, sonst bietet die Rückfrage „Verschieben“ an), im Panel eines eingecheckten „Einchecken zurücknehmen“ unter „Mehr“; nach dem Kassieren nicht mehr, dann führt der Weg über den Beleg an der Kassa
- Zurückgenommene Stornierung hält eine noch wartende Nachricht aus Slice 73 zurück; ist sie schon raus, fragt die Rückfrage, ob der Gast eine Korrektur bekommt

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.Keyboard.cs – Tastenkürzel
- src/AispaStudio.Desktop/ViewModels/AppointmentViewModel.Panel.cs – Aktionen im Panel
- tests/AispaStudio.Desktop.Tests/CalendarKeyboardTests.cs – Tests der Kürzel

## NICHT bauen
- Rückgängig für beliebige andere Aktionen
- Rückgängig nach dem Kassieren

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 74 "Rückgängig für Stornieren und Einchecken" um (diese Datei).
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
- [ ] Given ein soeben stornierter Termin When Strg+Z gedrückt wird Then steht der Termin wieder auf „Gebucht“
- [ ] Given ein eingecheckter Termin When „Einchecken zurücknehmen“ gedrückt wird Then steht er wieder auf „Gebucht“
- [ ] Given ein kassierter Termin When sein Panel geöffnet wird Then gibt es kein „Einchecken zurücknehmen“
- [ ] Given ein stornierter Termin, dessen Zeit inzwischen belegt ist When „Stornierung zurücknehmen“ gedrückt wird Then nennt die Rückfrage den Konflikt und bietet „Verschieben“ an
- [ ] Manuell: Einen Termin stornieren und Strg+Z drücken: er ist wieder gebucht; in der Tastenleiste stand der Eintrag nur kurz

## Nicht in dieser Slice
–

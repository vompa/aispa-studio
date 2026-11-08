# Slice 82 – Neuer Termin: Notiz, Reiter, Eingaben beim Wechsel der Art

**Ziel:** In „Neuer Termin“ ist die Notiz mehrzeilig, der Reiter unten nennt den Gast mit Kennzeichen „in Bearbeitung“, und beim Wechsel der Art gehen keine Eingaben verloren.
**Runde:** R04
**Quelle:** R04 – Ä148, Ä149, Ä153
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä159–Ä161

## Umfang
- Notiz in „Neuer Termin“ mehrzeilig (`AcceptsReturn`, `TextWrapping`), wächst mit dem Text bis zu einer Höchsthöhe (Ä148); dieselbe Regel für die Notiz in Paar/Spa, Pause, Sperrzeit und Sauna prüfen
- Reiter unten im Seitenpanel: „Neuer Termin · <Gast>“ mit Kennzeichen „in Bearbeitung“ (Punkt oder Pille nach Brief), solange nicht gebucht; ohne Gast nur „Neuer Termin“ (Ä149)
- Wechsel der Art (Termin, Paar/Spa, Pause, Sperrzeit, Notiz, Sauna) übernimmt, was in der neuen Art ein Feld hat: Termin → Paar/Spa (Gast als erste Person, Behandlung als erster Schritt, Datum, Beginn, Notiz), Termin → Sauna (Gast, Datum, Beginn, Notiz, Personen 1), Paar/Spa → Termin (erste Person mit Behandlung), Termin ↔ Pause/Sperrzeit (Beginn, Länge der Belegung), Termin → Notiz (Beginn, Notiztext); beim Zurückwechseln stehen die Werte der alten Art wieder da, solange das Panel offen ist (Ä153)
- „Freie Zeit suchen“ aus „Neuer Termin“ nimmt die Notiz mit, und sie landet im gebuchten Termin (offen aus dem Nachtrag „Neuer Termin bucht in der Spalte“ vom 27.10.2026)
- Warteliste: gibt es aus „Neuer Termin“ einen Weg „Auf die Warteliste“, übernimmt er Gast, Behandlung, Wunschtag und Notiz; gibt es ihn nicht, als neuen Befund festhalten statt bauen

## Muster übernehmen
- src/AispaStudio.Desktop/ViewModels/BookingViewModel.cs – Maske Neuer Termin, `MoveTo` behält Eingaben (Ä85)
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.NewEntry.cs – `CreateEntry` je Art
- src/AispaStudio.Desktop/ViewModels/SidePanels.cs – Reiter und Untertitel der Seitenpanels

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/ViewModels/BookingViewModel.cs
- src/AispaStudio.Desktop/ViewModels/CalendarViewModel.NewEntry.cs
- src/AispaStudio.Desktop/ViewModels/GroupBookingViewModel.cs
- src/AispaStudio.Desktop/ViewModels/SidePanels.cs

## NICHT bauen
- Neue Warteliste-Funktion
- Übernahme der Notiz in den Folgetermin (bleibt „Folgetermin“)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 82 "Neuer Termin: Notiz, Reiter, Eingaben beim Wechsel der Art" um (diese Datei).
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
- [ ] Given „Neuer Termin“ mit Gast Andrea Auer When auf „Paar/Spa“ gewechselt wird Then steht Andrea Auer als erste Person mit der gewählten Behandlung, Datum und Beginn da
- [ ] Given „Neuer Termin“ mit Gast When auf „Sauna“ gewechselt wird Then sind Gast, Datum und Notiz übernommen
- [ ] Given ein Wechsel Termin → Paar/Spa → Termin When man zurückkommt Then stehen Gast, Behandlung, Beginn und Notiz unverändert da
- [ ] Given eine Notiz mit drei Zeilen When sie eingetippt wird Then ist das Feld drei Zeilen hoch und nichts ist abgeschnitten
- [ ] Given „Neuer Termin“ mit Gast Andrea Auer When man den Reiter unten ansieht Then steht „Neuer Termin · Andrea Auer“ mit dem Kennzeichen „in Bearbeitung“
- [ ] Manuell: Termin mit Gast und Notiz anfangen, auf Paar/Spa und Sauna wechseln und zurück: nichts geht verloren
- [ ] Manuell: Lange Notiz eintippen: Feld wächst
- [ ] Manuell: Reiter unten nennt den Gast

## Nicht in dieser Slice
–

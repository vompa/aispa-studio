# Slice 18 – Pfeiltasten-Auswahl im Kalender wird angesagt

**Ziel:** Wer im Kalender mit den Pfeiltasten wechselt, hört den neu gewählten Termin.
**Runde:** R01
**Quelle:** R01 – W50 (Teil 2)
**Hängt ab von:** 17
**Modell:** –

## Umfang
- Auswahlwechsel in CalendarViewModel.Keyboard.cs setzt den UI-Automation-Fokus auf die Karte oder löst eine Ansage mit ihrem Namen aus

## Muster übernehmen
- der Peer aus Slice 17

## NICHT bauen
- Ansagen für andere Kalenderaktionen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 18 "Pfeiltasten-Auswahl im Kalender wird angesagt" um (diese Datei).
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
- [ ] Given Termin A ist ausgewählt When Pfeil rechts gedrückt wird Then meldet ein Headless-Test ein Fokus- oder Ansage-Ereignis mit dem Namen des nächsten Termins
- [ ] Manuell: Narrator an, Kalender, Termin wählen, Pfeiltasten: jeder neue Termin wird angesagt

## Nicht in dieser Slice
Neue Tastenkürzel

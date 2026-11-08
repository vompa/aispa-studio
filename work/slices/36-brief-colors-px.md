# Slice 36 – Farben, Kontrast und Schriftgrößen im Brief

**Ziel:** Statusfarbe „Erledigt“ ist lesbar, die Kontrastregel des Briefs ist eindeutig, und px als Schriftgröße ist als Abweichung festgehalten.
**Runde:** R02
**Quelle:** R02 – W62, W66, B29, B30
**Hängt ab von:** –
**Modell:** haiku

## Umfang
- `StDone` hell in App (`Colors.axaml`) und Web (`--st-done`) auf `#656c68` (4,6:1 auf `#efede8`), dunkel bleibt `#97a6a2`; übrige `--st-*`-Paare unter 4,5:1 prüfen (B29)
- Brief 2.6: 3:1 nur für Rahmen von Feldern, Kontrollkästchen und Optionsfeldern (W66)
- Brief: px statt rem als bewusste Abweichung festhalten, weil Brief W1 px verlangt und Browser-Zoom px mitskaliert (B30)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 36 "Farben, Kontrast und Schriftgrößen im Brief" um (diese Datei).
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
- [ ] Given `StDone` auf `StDoneBg` hell When ein Test den Kontrast rechnet Then ist er mindestens 4,5:1
- [ ] Given `--st-done` auf `--st-done-bg` When ein Web-Test den Kontrast rechnet Then ist er mindestens 4,5:1
- [ ] Given der Brief When man 2.6 liest Then gilt 3:1 nur für Felder, Kontrollkästchen und Optionsfelder
- [ ] Manuell: Erledigten Termin im Kalender ansehen (hell und dunkel): Balken und Text gut lesbar

## Nicht in dieser Slice
–

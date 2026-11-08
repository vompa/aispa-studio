# Slice 46 – Kleinigkeiten Web, zweiter Durchgang

**Ziel:** Kleine Darstellungspunkte im Web in einem Durchgang beheben.
**Runde:** R02
**Quelle:** R02 – B11, B20, B22, B24, B32, B42, B43, B63, B65
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Farbwahl mit Hex-Wert als Text (B11); Fragezeichen 24 px Zielgröße (B20); Hilfebilder mit width/height (B22); KI-Pille mit unsichtbarem Text statt aria-label (B24)
- Kundenbereich im Kontrastdesign: Rahmen wo nötig (B32); „Rest bar/Karte“ im Anzeigeformat (B42); „Bar zu zahlen“ und „Rückgeld“ mit `role="status"` (B43)
- Schriftgewicht 700 → 600 (B63); Toasts blenden 120 ms aus (B65)

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 46 "Kleinigkeiten Web, zweiter Durchgang" um (diese Datei).
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
- [ ] Given das Fragezeichen When gemessen Then ist die Klickfläche mindestens 24 × 24 px
- [ ] Given „Rest bar“ bei 12,5 offen When gedrückt Then steht „12,50“ im Feld
- [ ] Given index.css When ein Prüftest `font-weight: 700` sucht Then gibt es keinen Treffer
- [ ] Manuell: Kassa: „Rest bar“ drücken, Feld zeigt den Betrag mit zwei Nachkommastellen

## Nicht in dieser Slice
–

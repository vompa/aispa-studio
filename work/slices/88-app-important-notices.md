# Slice 88 – Wichtige Meldungen sichtbar und einheitlich

**Ziel:** Meldungen, die ein Ergebnis verhindern oder eine Entscheidung verlangen, sind in der ganzen Windows-App InfoBars mit Ursache und Weg, nie kleiner grauer Text.
**Runde:** R04
**Quelle:** R04 – Ä156
**Hängt ab von:** 84
**Modell:** sonnet
**IDs für neue Befunde:** Ä177–Ä179

## Umfang
- Anlass: „In den nächsten 14 Tagen ist nichts frei.“ in „Freie Zeit suchen“ als kleiner grauer Text; wird eine InfoBar mit Weg („Tageszeit: egal“, „Auf die Warteliste“)
- Plausibilität: prüfen, ob für die AISPA Signaturmassage nachmittags wirklich 14 Tage nichts frei ist (Filter Tageszeit, heutige Uhrzeit)
- Durchsicht aller Seiten nach Meldungen in `muted small` u. ä., einstufen, wichtige auf InfoBar umstellen; Regel in `docs/gestaltung/meldungen.md` schärfen
- Scan-Test: bekannte Meldungen bzw. Eigenschaften dürfen nur in einer InfoBar stehen

## Muster übernehmen
- docs/gestaltung/meldungen.md – Meldungsregeln
- src/AispaStudio.Desktop/Controls/InfoBar.cs – InfoBar mit Aktion

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/
- src/AispaStudio.Desktop/ViewModels/FreeTimeViewModel.cs
- docs/gestaltung/meldungen.md

## NICHT bauen
- Neue Meldungsart

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R04-*.md und diese Slice-Datei.
Setze Slice 88 "Wichtige Meldungen sichtbar und einheitlich" um (diese Datei).
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
- [ ] Given „Freie Zeit suchen“ ohne Ergebnis in 14 Tagen When die Liste leer ist Then steht eine InfoBar mit Ursache und mindestens einem Weg als Knopf
- [ ] Given alle Seiten When der Scan-Test läuft Then steht keine wichtige Meldung als kleiner grauer Text
- [ ] Manuell: Freie Zeit suchen ohne Ergebnis: deutliche Meldung mit Weg

## Nicht in dieser Slice
–

# Slice 45 – Tooltip-Baustein im Web

**Ziel:** Erklärungen, die heute nur im `title` stehen, erscheinen über einen Tooltip-Baustein auch bei Tastaturfokus.
**Runde:** R02
**Quelle:** R02 – B68
**Hängt ab von:** –
**Modell:** sonnet

## Umfang
- Gemeinsame `Tooltip`-Komponente (Popover API, 250 ms, auch bei Fokus, Esc schließt) in `web/src/components/`
- Die 67 `title`-Stellen sichten: wo der Text mehr als ein Name ist, auf den Baustein umstellen

## Muster übernehmen
–

## NICHT bauen
–

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 45 "Tooltip-Baustein im Web" um (diese Datei).
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
- [ ] Given ein Element mit Tooltip When es den Tastaturfokus bekommt Then erscheint der Tooltip nach 250 ms und Esc schließt ihn
- [ ] Given die Quelltexte When ein Prüftest `title=` mit mehr als einem Satz sucht Then gibt es keinen Treffer
- [ ] Manuell: Kalender der Verwaltung: Live-Stand per Tab fokussieren: Erklärung erscheint

## Nicht in dieser Slice
–

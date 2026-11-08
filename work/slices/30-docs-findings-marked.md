# Slice 30 – Befundliste: erledigte Befunde gekennzeichnet

**Ziel:** Die Befundliste zeigt bei jedem umgesetzten Befund, in welcher Slice er erledigt wurde.
**Runde:** R02
**Quelle:** R02 – Ruling 14 aus R01
**Hängt ab von:** –
**Modell:** haiku

## Umfang
- docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md: Zusatz „erledigt in Slice NN“ je Befund der Slices 01–29 (Quelle: Spalte „Befunde“ in work/rounds/R01-ui-review.md)
- Regel in work/conventions.md unter „Muss mitgeändert werden“: jede Slice kennzeichnet ihre Befunde in der Befundliste

## Muster übernehmen
–

## NICHT bauen
- Befunde der Runde R02 (kennzeichnet jede R02-Slice selbst)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 30 "Befundliste: erledigte Befunde gekennzeichnet" um (diese Datei).
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
- [ ] Given die Befundliste When ein Prüftest die Befunde der Slices 01–29 liest Then trägt jeder davon „erledigt in Slice NN“ mit der richtigen Nummer
- [ ] Given work/conventions.md When man „Muss mitgeändert werden“ liest Then steht dort die Befundliste
- [ ] Manuell: Befundliste öffnen, W50 suchen: „erledigt in Slice 17, 18“ steht dabei

## Nicht in dieser Slice
–

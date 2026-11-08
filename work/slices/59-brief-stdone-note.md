# Slice 59 – Brief: Quelle des Werts der Statusfarbe „Erledigt“

**Ziel:** Der Brief nennt beim Wert der Statusfarbe „Erledigt“ das Ruling als Quelle, ohne eigenen ADR.
**Runde:** R03
**Quelle:** R03 – Ruling Slice 36 aus R02
**Hängt ab von:** –
**Modell:** haiku
**IDs für neue Befunde:** –

## Umfang
- `docs/design.md`, Tabelle der Statusfarben: ein Satz beim Wert `#656c68` mit Verweis auf das Ruling vom 25.10.2026 (Rundenakte R02) und dem Kontrast 4,6:1 auf `#efede8`

## Muster übernehmen
- docs/design.md:87 – Zeile „Erledigt“ der Statusfarben

## NICHT bauen
- Neuer ADR
- Andere Farbwerte

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 59 "Brief: Quelle des Werts der Statusfarbe „Erledigt“" um (diese Datei).
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
- [ ] Given `docs/design.md` When man die Statusfarbe „Erledigt“ liest Then verweist ein Satz auf das Ruling vom 25.10.2026 und nennt 4,6:1
- [ ] Manuell: `docs/design.md` öffnen, Statusfarben: beim Wert für „Erledigt“ steht der Verweis auf das Ruling

## Nicht in dieser Slice
–

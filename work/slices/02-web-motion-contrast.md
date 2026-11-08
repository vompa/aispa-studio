# Slice 02 – Bewegung, Hochkontrast und Druckzustand im Web

**Ziel:** Die Verwaltung respektiert „Animationen aus“, zeigt den Fokus auch im Windows-Kontrastdesign und reagiert sichtbar auf Druck.
**Runde:** R01
**Quelle:** R01 – B5, B6, B64
**Hängt ab von:** –
**Modell:** –

## Umfang
- Globaler Block `@media (prefers-reduced-motion: reduce)` in web/src/index.css (keine Übergänge, keine Animationen)
- `outline: none` mit `box-shadow` als Fokus durch `outline: 2px solid transparent` ergänzen: stock.css (7 Stellen aus B6) und businessInfo.css
- `.appointment` in index.css mit `border: 1px solid transparent` statt `border: none`
- Druckzustand `button:active:not(:disabled)` (Hintergrund `--surface-3`, `scale(0.98)`, 100 ms); Hover-Regeln in `@media (hover: hover)`

## Muster übernehmen
- web/src/booking/booking.css:50 – vorhandener Block für reduzierte Bewegung

## NICHT bauen
- B10 Feldunterkante `--fg-3` (bekannt, eigener Punkt im Brief 11.4)
- B65 Ausblenden der Toasts (offene Entscheidung)
- B32 Hochkontrast im Kundenbereich (offene Entscheidung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 02 "Bewegung, Hochkontrast und Druckzustand im Web" um (diese Datei).
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
- [ ] Given index.css When eine Prüfung die Datei liest Then gibt es einen Block `@media (prefers-reduced-motion: reduce)`, der `transition-duration` und `animation` für alle Elemente abschaltet
- [ ] Given index.css, stock.css und businessInfo.css When eine Prüfung jede Regel mit `outline: none` sucht Then steht in derselben Regel ein sichtbarer Ersatz (`outline: 2px solid transparent`) oder sie ist entfernt (vorher 8 Stellen ohne)
- [ ] Given index.css When eine Prüfung nach `:active` sucht Then gibt es die Regel für Knöpfe mit `scale(0.98)`, und keine Hover-Regel für Knöpfe steht außerhalb von `@media (hover: hover)`
- [ ] Manuell: Windows „Animationseffekte“ aus, Verwaltung öffnen: Menüs und Toasts erscheinen ohne Übergang
- [ ] Manuell: Windows-Kontrastdesign an, Lager öffnen und mit Tab durch die Knöpfe gehen: der Fokusrahmen ist sichtbar

## Nicht in dieser Slice
Neue Animationen

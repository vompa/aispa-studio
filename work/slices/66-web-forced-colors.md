# Slice 66 – Zustände im Kontrastdesign von Windows

**Ziel:** Gewählte und gedrückte Zustände bleiben im Kontrastdesign von Windows sichtbar, in der Verwaltung wie in den schlanken Oberflächen.
**Runde:** R03
**Quelle:** R03 – B82, B85, B83
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B116–B119

## Umfang
- `@media (forced-colors: active)` in `web/src/index.css` und in den Stylesheets von Kundenbereich, Mein Tag, Betreiber und Lager: gewählte Zeit, Tag und Leistung (`.choice`, `.day`, `.time`), Knöpfe mit `aria-pressed`, gewählte Nachricht (`.mail.selected`) und Statusfarben der Termine mit Systemfarben (`Highlight`, `HighlightText`, Rahmen)
- Prüftest wie `interactionStates.test.ts`: jede der fünf Dateien hat eine Regel für ihre Zustandsselektoren
- Die Sichtprüfung von Kontrast hell und dunkel der vier Stylesheets (B83) in die manuelle Testliste

## Muster übernehmen
- web/src/interactionStates.test.ts – Prüftest für Zustände im Hochkontrast (R01 Slice 02)

## NICHT bauen
- Neue Farben
- Umstellung von `px` auf `rem` (siehe Ruling zu B81 und B88)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 66 "Zustände im Kontrastdesign von Windows" um (diese Datei).
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
- [ ] Given die fünf Stylesheets When ein Prüftest `forced-colors: active` sucht Then hat jede eine Regel für ihre Zustandsselektoren
- [ ] Given der Kundenbereich When gebaut wird Then bleibt er unter 100 kB gzip
- [ ] Manuell: Windows-Kontrastdesign einschalten, Kundenbereich buchen: gewählter Tag und gewählte Zeit sind erkennbar
- [ ] Manuell: Kundenbereich, Mein Tag, Betreiber und Lager hell und dunkel ansehen: Text und Ränder gut lesbar (B83)

## Nicht in dieser Slice
–

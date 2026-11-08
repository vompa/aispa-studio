# Slice 15 – Kundenkarten: nur eine Karte im Bearbeiten-Zustand

**Ziel:** In den Kundenkarten ist immer höchstens eine Karte im Bearbeiten-Zustand, mit genau einem Primärknopf.
**Runde:** R01
**Quelle:** R01 – W38 (Teil 1), W-Muster 4 (Teil)
**Hängt ab von:** 08
**Modell:** –

## Umfang
- Die sechs Karten mit eigenem Bearbeiten-Zustand (CustomersView :343-529): Öffnen einer Karte schließt eine andere offene; Umgang mit ungespeicherten Änderungen legt der Plan als Annahme fest

## Muster übernehmen
- src/AispaStudio.Desktop/Views/CustomerContactForm.axaml:155 – Speichern als einziger Primärknopf

## NICHT bauen
- Esc, Enter und Startfokus (Slice 16)
- W-Muster 8 `MaskBehavior` für alle Masken (dokumentierte Abweichung)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 15 "Kundenkarten: nur eine Karte im Bearbeiten-Zustand" um (diese Datei).
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
- [ ] Given die Karte „Kennzeichen“ im Bearbeiten-Zustand When „Wünsche bearbeiten“ geöffnet wird Then ist nur die Karte „Wünsche“ im Bearbeiten-Zustand
- [ ] Given eine Karte im Bearbeiten-Zustand When ein Headless-Test die sichtbaren `primary`-Knöpfe der Kundenseite zählt Then ist es genau einer
- [ ] Manuell: Kunden › eine Kundin › „Kennzeichen“ bearbeiten, dann „Wünsche“ bearbeiten: nur eine Karte ist offen

## Nicht in dieser Slice
Neue Karten

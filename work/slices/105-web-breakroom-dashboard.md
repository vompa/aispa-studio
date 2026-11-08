# Slice 105 – Web: Dashboard für den Pausenraum

**Ziel:** Ein gekoppelter Bildschirm im Pausenraum zeigt den Tag im Überblick: Tagesleiste je Person, freie Räume und nächste Termine; nur lesen, Initialen statt Namen, nie Gesundheitsdaten, live aktualisiert.
**Runde:** R05
**Quelle:** R05 – Ä192
**Hängt ab von:** 104
**Modell:** sonnet
**IDs für neue Befunde:** Ä235–Ä237

## Umfang
- Neues Webmodul (zuschaltbar nach ADR 0041) unter eigenem Pfad, Vollbild ohne Bedienelemente
- Inhalt (E5): Tagesleiste je Person, freie Räume, nächste Termine mit Initialen; sonst nichts
- Live über die bestehende Verbindung (web/src/live/connection.ts), Neuverbindung ohne Zutun
- Lesbar aus 3 m Abstand, dunkles und helles Thema, Werte aus docs/design.md

## Muster übernehmen
- web/src/myday/ – eigener Einstieg mit main.tsx
- web/src/live/connection.ts

## Voraussichtlich geänderte Dateien
- web/src/dashboard/
- web/vite.config.ts
- src/AispaStudio.Api/

## NICHT bauen
- Eingaben jeder Art
- volle Gastnamen
- Umsatzzahlen
- Hinweise der Leitung, Geburtstage, Auslastung, Zuständige des Tages (E5)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 105 "Web: Dashboard für den Pausenraum" um (diese Datei).
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
- [ ] Given ein Termin wird an der Rezeption verschoben When das Dashboard läuft Then zeigt es die neue Zeit innerhalb von 5 Sekunden
- [ ] Given ein Termin mit Gast „Anna Huber“ When das Dashboard ihn zeigt Then steht dort „A. H.“
- [ ] Manuell: Dashboard im Vollbild auf einem zweiten Bildschirm einen Vormittag laufen lassen

## Nicht in dieser Slice
–

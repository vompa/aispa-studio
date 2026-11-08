# Slice 53 – Hilfe: Seitenaufbau, Navigation und Typografie

**Ziel:** Jede Hilfeseite hat einen klaren Aufbau mit Brotkrumen, Inhaltsverzeichnis, Hinweiskästen, Bildunterschriften und verwandten Themen, in Typografie und Abständen nach dem Brief.
**Runde:** R02
**Quelle:** R02 – Auftraggeber, 25.10.2026 (Hilfe professioneller); Brief der Rundenakte
**Hängt ab von:** 32, 36
**Modell:** sonnet

## Umfang
- `help/HelpApp.tsx`, `HelpDrawer.tsx`, `helpPage.css`: Kopf mit Brotkrumen „Bereich › Thema“ und Rollenkennzeichen aus dem Frontmatter, „Auf dieser Seite“ aus den `h2`, Fuß „Verwandte Themen“ aus den `hilfe:`-Links, Zurück zur Übersicht
- `help/markdown.ts`: Hinweiskästen aus `> Hinweis:`, `> Tipp:`, `> Achtung:` (Symbol, Farbe nach Brief 5.15), nummerierte Schritte mit Abstand, Bilder mit Bildunterschrift und Rahmen
- Suche: Trefferzahl als `role="status"`, Treffer mit hervorgehobenem Begriff und Bereich
- Abschnitt „Hilfe“ in docs/design.md mit den festen Werten (Typografie, Abstände, Kästen); `web/src/help/README.md` nennt die neuen Auszeichnungen

## Muster übernehmen
- web/src/help/HelpApp.tsx:71 – vorhandene Seitennavigation
- docs/design.md – Wertequelle

## NICHT bauen
- Videos und GIFs, Rückmeldung je Thema, Rollenfilter (offene Entscheidungen)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R02-*.md und diese Slice-Datei.
Setze Slice 53 "Hilfe: Seitenaufbau, Navigation und Typografie" um (diese Datei).
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
- [ ] Given ein Thema mit drei `h2` When es angezeigt wird Then stehen Brotkrumen, drei Einträge unter „Auf dieser Seite“ und die verwandten Themen aus seinen Links
- [ ] Given ein Absatz `> Achtung: …` When gerendert Then ist er ein Kasten mit Symbol und `role="note"`
- [ ] Given die Suche nach „Kasse“ When Treffer da sind Then nennt ein `role="status"` die Zahl und jeder Treffer hebt „Kasse“ hervor
- [ ] Manuell: Hilfe in der Windows-App öffnen, „Termin verschieben“: Brotkrumen, Inhaltsverzeichnis, verwandte Themen und Kästen sind da; in der Verwaltung im Browser gleich

## Nicht in dieser Slice
–

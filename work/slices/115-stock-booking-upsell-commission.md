# Slice 115 – Lager im Buchungsablauf: Produktempfehlung im Termin und an der Kassa

**Ziel:** Zu jeder Behandlung lassen sich passende Verkaufsartikel hinterlegen; das Termin-Panel und die Kassa schlagen sie beim Abschluss vor, nur wenn sie am Verkaufsregal vorrätig sind. Eine Provision auf Produktverkauf gibt es nicht (E12).
**Runde:** R05
**Quelle:** R05 – Ä251
**Hängt ab von:** 101, 94
**Modell:** opus
**IDs für neue Befunde:** Ä268–Ä270

## Umfang
- Zuordnung Behandlung → empfohlene Verkaufsartikel in der Behandlungsmaske (Slice 94)
- Vorschlag „Passt dazu“ im Termin-Panel beim Abschließen und an der Kassa; nur Artikel mit Bestand am Verkaufsregal; ein Klick legt ihn auf den Beleg
- Hilfe nachziehen

## Muster übernehmen
- native Lager-Maske aus Slice 101
- src/AispaStudio.Api/Pos/PosEndpoints.cs – `BookReceiptStock`
- docs/konzepte/lager-konkurrenz.md – Ist-Stand, Konkurrenz, Empfehlungen

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Stock/
- src/AispaStudio.Api/Pos/
- src/AispaStudio.Desktop/Views/PosView.axaml
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- tests/AispaStudio.Api.Tests/
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- KI-Empfehlungen
- Rabatte über Aktionen hinaus
- Provision auf Produktverkauf (E12)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 115 "Lager im Buchungsablauf: Produktempfehlung im Termin und an der Kassa" um (diese Datei).
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
- [ ] Given „Hot Stone 60“ empfiehlt „Massageöl Lavendel“ und das Regal hat 3 Stück When die Rezeption den Termin abschließt Then schlägt die Kassa das Öl vor
- [ ] Given das Regal ist leer When der Termin abgeschlossen wird Then gibt es keinen Vorschlag
- [ ] Manuell: Termin abschließen, Vorschlag annehmen, Beleg und Lagerbestand prüfen

## Nicht in dieser Slice
–

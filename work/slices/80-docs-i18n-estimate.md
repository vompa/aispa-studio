# Slice 80 – Mehrsprachigkeit: Entscheidungsvorlage mit Aufwand

**Ziel:** Eine Vorlage zeigt, was eine mehrsprachige Oberfläche für Windows-App und Web kostet und wie die Ressourcen automatisch entstehen können; der Auftraggeber entscheidet danach.
**Runde:** R03
**Quelle:** R03 – Ä103
**Hängt ab von:** –
**Modell:** opus
**IDs für neue Befunde:** Ä140–Ä142

## Umfang
- Bestand zählen: Texte in AXAML, C#, TSX, Hilfe, Fehlermeldungen der API, wie der Kundenbereich es heute macht, Sprache je Gast
- Gemeinsame i18n-Lösung für Web und Windows-App entwerfen (eine Quelle der Schlüssel, Erzeugung der Ressourcen beim Build oder beim ersten Start) und drei Wege vergleichen: beim ersten Start auf dem Rechner, beim Build mit Prüfung vor der Auslieferung, Mischform; mit Datenschutz (keine Gästedaten an Dienste), Pflichttexte (Kassa, Belege nie von der KI) und Kosten
- Aufwandsschätzung in Slices mit Reihenfolge und Risiken; Datei `docs/konzepte/mehrsprachigkeit.md`; kein Code

## Muster übernehmen
- docs/konzepte/ – bestehende Konzeptdokumente
- docs/entscheidungen/0079-eine-hilfe-fuer-windows-app-und-web.md – gemeinsame Darstellung der Hilfe

## NICHT bauen
- Umsetzung der Mehrsprachigkeit
- Neue Pakete

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 80 "Mehrsprachigkeit: Entscheidungsvorlage mit Aufwand" um (diese Datei).
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
- [ ] Given `docs/konzepte/mehrsprachigkeit.md` When man sie liest Then nennt sie den Bestand in Zahlen, drei Wege im Vergleich, eine Empfehlung und eine Schätzung in Slices
- [ ] Manuell: Das Dokument lesen: die Empfehlung ist klar und die Frage „beim ersten Start oder beim Build“ ist beantwortet

## Nicht in dieser Slice
–

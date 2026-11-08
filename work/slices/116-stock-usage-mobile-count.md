# Slice 116 – Lager: Soll-Ist-Verbrauch und Inventur am Handy

**Ziel:** Ein Bericht zeigt je Artikel und Behandlung, wie viel laut Rezept verbraucht sein sollte und wie viel die Inventur tatsächlich ergibt (Schwund, Überdosierung); die Handy-App für Mitarbeitende kann eine Inventur zählen und Wareneingang per Kamera-Scan buchen.
**Runde:** R05
**Quelle:** R05 – Ä251
**Hängt ab von:** 106, 114
**Modell:** sonnet
**IDs für neue Befunde:** Ä271–Ä273

## Umfang
- Bericht Soll-Ist: Differenz zwischen Inventur und gebuchtem Soll je Zeitraum, aufgeteilt nach Behandlungen; Rechnung in `Reporting`
- Inventur am Handy: Zähl-Sitzung je Lagerort mit Zwischenstand, Abschluss erst durch die Leitung in der Windows-App
- Kamera-Scan (BarcodeDetector, Rückfall manuelle Eingabe) für Inventur und Wareneingang
- Hilfe nachziehen

## Muster übernehmen
- web/src/stock/StockCount.tsx – Inventur
- docs/konzepte/lager-konkurrenz.md – Ist-Stand, Konkurrenz, Empfehlungen
- Phorest Go: Inventur am Handy

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Api/Stock/
- src/AispaStudio.Api/Reporting/
- web/src/myday/
- tests/AispaStudio.Api.Tests/
- web/src/help/content/

## NICHT bauen
- Abschluss der Inventur am Handy
- Verkauf am Handy

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 116 "Lager: Soll-Ist-Verbrauch und Inventur am Handy" um (diese Datei).
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
- [ ] Given Soll 300 ml und Inventur zeigt 420 ml Verbrauch When der Bericht läuft Then steht 120 ml Mehrverbrauch mit den Behandlungen, die den Artikel nutzen
- [ ] Given Lena zählt am Handy den Raumschrank 2 When sie die Zählung abschickt Then sieht die Managerin die Zählung zum Abschluss in der Windows-App
- [ ] Manuell: Inventur eines Raumschranks am Handy zählen und in der Windows-App abschließen

## Nicht in dieser Slice
–

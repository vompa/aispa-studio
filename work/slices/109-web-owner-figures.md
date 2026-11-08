# Slice 109 – Web: Kennzahlen des Tages für die Leitung am Handy

**Ziel:** Inhaberin und Managerin sehen in der Handy-App für Mitarbeitende eine lesende Seite mit Kennzahlen des Tages (Auslastung, Umsatz laut Kassa, Absagen, offene Anfragen) (E3).
**Runde:** R05
**Quelle:** R05 – Ä195
**Hängt ab von:** 106
**Modell:** sonnet
**IDs für neue Befunde:** Ä247–Ä249

## Umfang
- Seite „Heute im Spa“ nur mit Recht ViewFinance bzw. ViewAmountReports
- Zahlen nur aus bestehenden Bericht-Endpunkten, keine KI-Zahlen
- Hilfe nachziehen

## Muster übernehmen
- web/src/reports/ – bestehende Bericht-Endpunkte

## Voraussichtlich geänderte Dateien
- web/src/myday/
- web/src/help/content/

## NICHT bauen
- Eingaben
- neue Kennzahlen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 109 "Web: Kennzahlen des Tages für die Leitung am Handy" um (diese Datei).
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
- [ ] Given eine Rezeptionistin When sie die Handy-App öffnet Then gibt es keine Seite „Heute im Spa“
- [ ] Given heute 3 200 Euro laut Kassa When die Inhaberin die Seite öffnet Then steht 3 200 Euro
- [ ] Manuell: Am Handy als Inhaberin anmelden und mit dem Tagesbericht vergleichen

## Nicht in dieser Slice
–

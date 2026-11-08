# Slice 67 – Tastaturtests für die Berichte

**Ziel:** Tabellen und Filter der Berichtsansichten haben Tastaturtests; was die Tests an Lücken finden, ist behoben oder als Befund festgehalten.
**Runde:** R03
**Quelle:** R03 – B79
**Hängt ab von:** –
**Modell:** sonnet
**IDs für neue Befunde:** B120–B124

## Umfang
- Für jede Ansicht unter `reports/` ein Test mit `userEvent.tab`: Filter und Aktionen in Lesereihenfolge erreichbar, Fokus nie im Unsichtbaren
- Statusmeldungen der Berichte stehen in einer `role="status"`-Region, die von Anfang an da ist
- Lücken, die die Tests zeigen, beheben, soweit sie in die Sitzung passen; sonst als Befund mit einer Kennung aus dem reservierten Bereich in der Rundenakte

## Muster übernehmen
- web/src/reports/ComplianceView.test.tsx – vorhandene Tests einer Berichtsansicht
- web/src/reports/HygieneView.test.tsx – vorhandene Tests einer Berichtsansicht

## NICHT bauen
- Neue Berichte
- Umgestaltung der Tabellen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R03-*.md und diese Slice-Datei.
Setze Slice 67 "Tastaturtests für die Berichte" um (diese Datei).
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
- [ ] Given jede Berichtsansicht When per Tab durch die Seite gegangen wird Then erreichen Filter und Aktionen den Fokus in Lesereihenfolge
- [ ] Given ein Filterwechsel When das Ergebnis kommt Then steht die Meldung in einer `role="status"`-Region, die schon vor dem Wechsel da war
- [ ] Manuell: Berichte nur mit der Tastatur durchgehen: Filter, Tabellen und Aktionen sind erreichbar, der Fokus ist sichtbar

## Nicht in dieser Slice
–

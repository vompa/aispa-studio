# Slice 99 – Team: Dienstplan, Abwesenheiten und Zeiten

**Ziel:** Dienstplan mit Abwesenheiten und die Zeiterfassung sind nativ im Bereich Team: je Person als Reiter der Personenkarte, für alle als Wochenplan im selben Bereich; Anträge aus der Handy-App (Slice 106) werden hier genehmigt.
**Runde:** R05
**Quelle:** R05 – Ä191, Ä250
**Hängt ab von:** 98
**Modell:** opus
**IDs für neue Befunde:** Ä217–Ä219

## Umfang
- Reiter „Dienstplan und Abwesenheit“ und „Zeiten“ an der Personenkarte
- Ansicht „Wochenplan“ für das ganze Team im Bereich Team (web/src/roster/), Klick auf eine Person öffnet ihre Karte
- Zeiterfassung (web/src/time/) nativ in denselben Reitern bzw. als Teamübersicht
- Liste offener Abwesenheitsanträge mit Genehmigen und Ablehnen (die API-Seite liefert Slice 106; bis dahin leer)
- Hilfe und Handbuchbilder neu

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/roster/
- src/AispaStudio.Desktop/Views/CalendarView.axaml – Raster

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Roster*
- src/AispaStudio.Desktop/Views/Admin/Time*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- Lohnabrechnung (Slice 100)
- automatische Dienstplanung

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 99 "Team: Dienstplan, Abwesenheiten und Zeiten" um (diese Datei).
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
- [ ] Given Lena hat am Dienstag Dienst 9–17 When die Managerin ihn auf 12–20 verschiebt Then bietet der Kalender Lena am Dienstag erst ab 12 Uhr an
- [ ] Given eine Abwesenheit mit gebuchten Terminen When sie gespeichert wird Then warnt die Maske mit der Zahl der betroffenen Termine
- [ ] Manuell: Woche im Dienstplan anlegen und im Kalender gegenprüfen

## Nicht in dieser Slice
–

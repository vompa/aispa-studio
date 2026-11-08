# Slice 100 – Team: Lohn und Provision

**Ziel:** Lohnangaben und Provision je Person sind ein Reiter der Personenkarte; der Lohn-Export für alle liegt im Bereich Team.
**Runde:** R05
**Quelle:** R05 – Ä191, Ä250
**Hängt ab von:** 99
**Modell:** sonnet
**IDs für neue Befunde:** Ä220–Ä222

## Umfang
- Reiter „Lohn und Provision“ an der Personenkarte
- Lohn-Export (web/src/payroll/) im Bereich Team mit Zeitraum, Vorschau und Export als Datei
- Hilfe nachziehen

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/payroll/

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Payroll*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Lohnarten

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 100 "Team: Lohn und Provision" um (diese Datei).
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
- [ ] Given der Oktober When die Inhaberin den Lohn-Export speichert Then enthält die Datei dieselben Zahlen wie der bisherige Web-Export
- [ ] Manuell: Export für einen Monat speichern und in Excel öffnen

## Nicht in dieser Slice
–

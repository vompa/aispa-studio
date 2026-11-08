# Slice 01 – Tabellenköpfe mit scope und Zahlenspalten rechts

**Ziel:** Jeder Tabellenkopf der Verwaltung nennt seine Richtung für den Screenreader, Zahlenspalten stehen samt Kopf rechts.
**Runde:** R01
**Quelle:** R01 – B4, B56
**Hängt ab von:** –
**Modell:** –

## Umfang
- `scope="col"` bzw. `scope="row"` an allen 73 `<th>` in den 11 Dateien aus B4 (CostsPage, AccountingPage, ManagementView, LocationsTab, SenderRulesSection, ComplianceView, PayrollPage, BillPanel, TeamTab, RosterPage, TimesheetPage)
- Zahlenspalten rechtsbündig samt Kopf (`th` mit derselben Klasse wie `td.amount`): PayrollPage „Menge“, AccountingPage „Soll“, „Haben“, „Betrag“, „USt“, CostsPage
- Vitest-Prüfung über `web/src`, die jedes `<th>` ohne `scope` meldet

## Muster übernehmen
- docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md – B4 (Dateien und Zahlen je Datei), B56 (Spalten)

## NICHT bauen
- `<caption>` für alle Tabellen
- Neugestaltung der Tabellen

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R01-*.md und diese Slice-Datei.
Setze Slice 01 "Tabellenköpfe mit scope und Zahlenspalten rechts" um (diese Datei).
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
- [ ] Given die Quelltexte in web/src When die neue Prüfung läuft Then findet sie 0 `<th>` ohne `scope` (vorher 73)
- [ ] Given die Buchungstabelle der Buchhaltung When sie gerendert wird Then tragen die Köpfe „Soll“, „Haben“, „Betrag“, „USt“ dieselbe rechtsbündige Klasse wie ihre Werte
- [ ] Manuell: Verwaltung › Buchhaltung öffnen: die Köpfe „Soll“, „Haben“, „Betrag“, „USt“ stehen rechts über den Zahlen

## Nicht in dieser Slice
Sortierbare Tabellen, neue Spalten

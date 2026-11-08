# Slice 102 – Native Verwaltung: Berichte, Kosten, Buchhaltung und Geschäftsinfo

**Ziel:** Berichte je Rolle, Kosten je Behandlung, der Buchhaltungs-Export (DATEV) und die Geschäftsinfo sind nativ; „Im Bericht öffnen“ des Assistenten führt in den nativen Bericht.
**Runde:** R05
**Quelle:** R05 – Ä191
**Hängt ab von:** 93
**Modell:** opus
**IDs für neue Befunde:** Ä226–Ä228

## Umfang
- Berichte (web/src/reports/) nativ mit Standort und Zeitraum; Pflichtzahlen kommen weiter nur aus der API
- Kosten (web/src/costs/) nativ
- Buchhaltung (web/src/accounting/) nativ mit DATEV-Export als Datei
- Geschäftsinfo (web/src/businessinfo/) nativ
- Sprungziele des Assistenten „Im Bericht öffnen“ (`reports/management` mit `standort`, `von`, `bis`) und „Jetzt ergänzen“ (`businessInfo`) nativ
- Hilfe und Handbuchbilder neu

## Muster übernehmen
- Verwaltungsgerüst und Firmendaten aus Slice 93
- web/src/reports/

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/Admin/Reports*
- src/AispaStudio.Desktop/Views/Admin/Costs*
- src/AispaStudio.Desktop/Views/Admin/Accounting*
- src/AispaStudio.Desktop/Views/Admin/BusinessInfo*
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Kennzahlen
- Diagramm-Bibliothek ohne Freigabe

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 102 "Native Verwaltung: Berichte, Kosten, Buchhaltung und Geschäftsinfo" um (diese Datei).
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
- [ ] Given der Assistent bietet „Im Bericht öffnen“ für Oktober When die Inhaberin klickt Then öffnet der native Umsatzbericht mit Standort und Oktober
- [ ] Given der DATEV-Export für Oktober When er nativ gespeichert wird Then ist die Datei Byte für Byte gleich dem bisherigen Web-Export
- [ ] Manuell: Monatsbericht öffnen und mit dem bisherigen Web-Bericht vergleichen

## Nicht in dieser Slice
–

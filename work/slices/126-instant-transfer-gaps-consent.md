# Slice 126 – Kassa und Kalender: Sofortüberweisung, Lückenvorschläge und Einwilligung nativ

**Ziel:** Die drei Funktionen, die außer den Funden von Slice 103 nur in der Web-Team-Oberfläche liegen, werden nativ: Sofortüberweisung an der Kassa, Lückenvorschläge im Kalender und die Einwilligung für Wiederbuchungs-Hinweise am Termin.
**Runde:** R05
**Quelle:** R05 – Ä290, Ä291, Ä292
**Hängt ab von:** 124
**Modell:** sonnet
**IDs für neue Befunde:** Ä297–Ä299

## Umfang
- Kassa: Sofortüberweisung am offenen Beleg aus `GET api/pos/bills/{billId}/instant-transfer` wie in `web/src/pos/BillPanel.tsx`
- Kalender: Lückenvorschläge des Tages aus `GET api/locations/{locationId}/gap-suggestions` wie in `web/src/calendar/GapPanel.tsx`
- Termin: Einwilligung für Wiederbuchungs-Hinweise anzeigen, anfordern und widerrufen über `api/customers/{customerId}/marketing-consent` wie in `web/src/calendar/RebookConsentSection.tsx`
- Masken-Inventar und Hilfe nachziehen

## Muster übernehmen
- die Termin- und Kassa-Karten aus Slice 124
- web/src/pos/BillPanel.tsx, web/src/calendar/GapPanel.tsx, web/src/calendar/RebookConsentSection.tsx – Verhalten und Texte
- docs/gestaltung/masken.md und docs/gestaltung/meldungen.md

## Voraussichtlich geänderte Dateien
- src/AispaStudio.Desktop/Views/CalendarView.axaml
- src/AispaStudio.Desktop/ViewModels/
- src/AispaStudio.Desktop/Views/PosView.axaml
- docs/umsetzung/masken-inventar.md
- tests/AispaStudio.Desktop.Tests/
- web/src/help/content/

## NICHT bauen
- neue Zahlungsabläufe oder neue Rechenregeln in der App; Beträge, Vorschläge und Regeln kommen aus der API
- Entfernen der Web-Masken (macht Slice 103)

## Prompt
```
Lies CLAUDE.md, work/conventions.md, die Rundenakte work/rounds/R05-*.md und diese Slice-Datei.
Setze Slice 126 "Kassa und Kalender: Sofortüberweisung, Lückenvorschläge und Einwilligung nativ" um (diese Datei).
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
- [ ] Given ein offener Beleg When an der Kassa die Sofortüberweisung gewählt wird Then zeigt die Kassa, was die API dafür liefert, mit dem offenen Betrag
- [ ] Given ein Tag mit Lückenvorschlägen When der Kalender diesen Tag zeigt Then stehen die Vorschläge der API dort, ohne eigene Rechnung in der App
- [ ] Given ein Gast ohne Einwilligung When die Rezeption sie am Termin anfordert Then zeigt der Termin den neuen Stand
- [ ] Given ein Gast mit Einwilligung When die Rezeption sie widerruft Then zeigt der Termin den Widerruf
- [ ] Manuell: Sofortüberweisung, Lückenvorschläge und Einwilligung als Inhaberin im echten Fenster mit Testdaten durchgehen

## Nicht in dieser Slice
–
